using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using DigitalIntelligenceBridge.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DigitalIntelligenceBridge.Services;

public interface IReleaseCenterService
{
    bool IsConfigured { get; }
    Task<ReleaseCenterCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);
    Task<ReleaseCenterClientDownloadResult> DownloadLatestClientPackageAsync(IProgress<ReleaseCenterDownloadProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<ReleaseCenterPluginDownloadResult> DownloadAvailablePluginPackagesAsync(CancellationToken cancellationToken = default);
    Task<ReleaseCenterPluginPrepareResult> PrepareCachedPluginPackagesAsync(CancellationToken cancellationToken = default);
    Task<ReleaseCenterPluginActivateResult> ActivatePreparedPluginPackagesAsync(CancellationToken cancellationToken = default);
    Task<ReleaseCenterPluginRollbackResult> RestoreLatestPluginBackupAsync(CancellationToken cancellationToken = default);
}

public sealed record ReleaseCenterCheckResult(
    bool IsSuccess,
    string Summary,
    string ClientSummary,
    string PluginSummary,
    string Detail,
    string SiteSummary,
    string AuthorizedPluginSummary,
    string AuthorizedPluginDetail);
public sealed record ReleaseCenterClientDownloadResult(bool IsSuccess, string Summary, string Detail, string Version, string CacheDirectory, string PackagePath);
public sealed record ReleaseCenterDownloadProgress(string Stage, string Status, long BytesReceived, long? TotalBytes, double BytesPerSecond, TimeSpan? EstimatedRemaining);
public sealed record ReleaseCenterPluginDownloadResult(bool IsSuccess, string Summary, string Detail, int DownloadedCount, string CacheDirectory);
public sealed record ReleaseCenterPluginPrepareResult(bool IsSuccess, string Summary, string Detail, int PreparedCount, string StagingDirectory);
public sealed record ReleaseCenterPluginActivateResult(bool IsSuccess, string Summary, string Detail, int ActivatedCount, string RuntimePluginRoot);
public sealed record ReleaseCenterPluginRollbackResult(bool IsSuccess, string Summary, string Detail, int RestoredCount, string RuntimePluginRoot);

public sealed class ReleaseCenterService : IReleaseCenterService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ReleaseCenterService> _logger;
    private readonly AppSettings _appSettings;
    private readonly ReleaseCenterConfig _config;
    private readonly PluginConfig _pluginConfig;
    private readonly string _currentAppVersion;

    public ReleaseCenterService(HttpClient httpClient, ILogger<ReleaseCenterService> logger, IOptions<AppSettings> settings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _appSettings = settings.Value;
        _config = _appSettings.ReleaseCenter;
        _pluginConfig = _appSettings.Plugin;
        _currentAppVersion = _appSettings.Application.Version;
    }

    public bool IsConfigured => _config.Enabled && !string.IsNullOrWhiteSpace(_config.BaseUrl) && !string.IsNullOrWhiteSpace(_config.Channel);

    public async Task<ReleaseCenterCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new ReleaseCenterCheckResult(false, "未配置发布中心", "客户端更新：未配置", "插件更新：未配置", "ReleaseCenter 未启用或缺少 BaseUrl/Channel。", "站点：未配置", "授权插件：未配置", string.Empty);
        }

        try
        {
            var heartbeatPayload = EnsureSiteHeartbeatPayload();
            var channel = _config.Channel.Trim();
            var clientManifest = await GetClientManifestAsync(cancellationToken).ConfigureAwait(false);
            var pluginManifest = await GetPluginManifestAsync(cancellationToken).ConfigureAwait(false);
            var clientSummary = BuildClientSummary(clientManifest);
            var pluginSummary = BuildPluginSummary(pluginManifest);
            var siteSummary = $"当前站点：{heartbeatPayload.SiteName} / {heartbeatPayload.SiteId}";
            var authorizedPluginSummary = BuildAuthorizedPluginSummary(pluginManifest);
            var authorizedPluginDetail = BuildAuthorizedPluginDetail(pluginManifest);
            var updateCount = 0;
            if (HasClientUpdate(clientManifest)) updateCount++;
            if ((pluginManifest?.Plugins?.Count ?? 0) > 0) updateCount++;
            var summary = updateCount > 0 ? $"发现 {updateCount} 类可用更新" : "当前未发现可用更新";
            return new ReleaseCenterCheckResult(
                true,
                summary,
                clientSummary,
                pluginSummary,
                $"channel={channel}; currentAppVersion={_currentAppVersion}; siteId={heartbeatPayload.SiteId}; siteName={heartbeatPayload.SiteName}",
                siteSummary,
                authorizedPluginSummary,
                authorizedPluginDetail);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "检查发布中心更新失败");
            return new ReleaseCenterCheckResult(false, "检查更新失败", "客户端更新：失败", "插件更新：失败", ex.Message, "站点：检查失败", "授权插件：检查失败", string.Empty);
        }
    }

    public async Task<ReleaseCenterClientDownloadResult> DownloadLatestClientPackageAsync(IProgress<ReleaseCenterDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new ReleaseCenterClientDownloadResult(false, "客户端升级包下载不可用", "ReleaseCenter 未启用或缺少 BaseUrl/Channel。", string.Empty, string.Empty, string.Empty);
        }

        var cacheDirectory = ResolveClientCacheDirectory();
        Directory.CreateDirectory(cacheDirectory);

        try
        {
            progress?.Report(new ReleaseCenterDownloadProgress("preparing", "正在准备客户端下载", 0, null, 0, null));
            var clientManifest = await GetClientManifestAsync(cancellationToken).ConfigureAwait(false);
            if (clientManifest is null || string.IsNullOrWhiteSpace(clientManifest.LatestVersion) || string.IsNullOrWhiteSpace(clientManifest.PackageUrl))
            {
                return new ReleaseCenterClientDownloadResult(true, "没有可下载的客户端更新包", $"channel={_config.Channel}", string.Empty, cacheDirectory, string.Empty);
            }

            if (CompareVersions(clientManifest.LatestVersion, _currentAppVersion) <= 0)
            {
                return new ReleaseCenterClientDownloadResult(true, "当前客户端已是最新版本", $"current={_currentAppVersion}", clientManifest.LatestVersion, cacheDirectory, string.Empty);
            }

            var targetPath = Path.Combine(cacheDirectory, BuildClientDownloadFileName(clientManifest));
            using var response = await _httpClient.GetAsync(clientManifest.PackageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var target = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            using var incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            long bytesReceived = 0;
            var stopwatch = Stopwatch.StartNew();
            var lastReport = TimeSpan.Zero;

            while (true)
            {
                var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                incrementalHash.AppendData(buffer, 0, read);
                bytesReceived += read;

                var elapsed = stopwatch.Elapsed;
                var shouldReport = elapsed - lastReport >= TimeSpan.FromMilliseconds(250) || (totalBytes.HasValue && bytesReceived >= totalBytes.Value);
                if (shouldReport)
                {
                    var bytesPerSecond = elapsed.TotalSeconds > 0 ? bytesReceived / elapsed.TotalSeconds : 0;
                    TimeSpan? remaining = null;
                    if (totalBytes.HasValue && bytesPerSecond > 0)
                    {
                        remaining = TimeSpan.FromSeconds(Math.Max(0, (totalBytes.Value - bytesReceived) / bytesPerSecond));
                    }

                    progress?.Report(new ReleaseCenterDownloadProgress("downloading", "正在下载客户端更新包", bytesReceived, totalBytes, bytesPerSecond, remaining));
                    lastReport = elapsed;
                }
            }

            progress?.Report(new ReleaseCenterDownloadProgress("verifying", "正在校验客户端更新包", bytesReceived, totalBytes ?? bytesReceived, 0, TimeSpan.Zero));
            var computedHash = Convert.ToHexString(incrementalHash.GetHashAndReset()).ToLowerInvariant();
            ValidateClientSha256(clientManifest, computedHash);
            progress?.Report(new ReleaseCenterDownloadProgress("completed", "客户端下载完成", bytesReceived, totalBytes ?? bytesReceived, 0, TimeSpan.Zero));
            return new ReleaseCenterClientDownloadResult(true, $"客户端更新包已缓存：{clientManifest.LatestVersion}", targetPath, clientManifest.LatestVersion, cacheDirectory, targetPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "下载客户端更新包失败");
            progress?.Report(new ReleaseCenterDownloadProgress("failed", "客户端下载失败", 0, null, 0, null));
            return new ReleaseCenterClientDownloadResult(false, "客户端下载失败", ex.Message, string.Empty, cacheDirectory, string.Empty);
        }
    }
    public async Task<ReleaseCenterPluginDownloadResult> DownloadAvailablePluginPackagesAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new ReleaseCenterPluginDownloadResult(false, "插件包下载不可用", "ReleaseCenter 未启用或缺少 BaseUrl/Channel。", 0, string.Empty);
        }

        var cacheDirectory = ResolveCacheDirectory();
        Directory.CreateDirectory(cacheDirectory);

        try
        {
            var pluginManifest = await GetPluginManifestAsync(cancellationToken).ConfigureAwait(false);
            var downloadablePlugins = pluginManifest?.Plugins?.Where(item => !string.IsNullOrWhiteSpace(item.PackageUrl)).ToList() ?? new List<PluginItemDto>();
            if (downloadablePlugins.Count == 0)
            {
                return new ReleaseCenterPluginDownloadResult(true, "没有可下载的插件包", $"channel={_config.Channel}", 0, cacheDirectory);
            }

            var downloaded = new List<string>();
            foreach (var plugin in downloadablePlugins)
            {
                var fileName = BuildDownloadFileName(plugin);
                var targetPath = Path.Combine(cacheDirectory, fileName);
                var packageUrl = NormalizePackageUrl(plugin.PackageUrl!);
                var bytes = await _httpClient.GetByteArrayAsync(packageUrl, cancellationToken).ConfigureAwait(false);
                ValidateSha256(plugin, bytes);
                await File.WriteAllBytesAsync(targetPath, bytes, cancellationToken).ConfigureAwait(false);
                downloaded.Add(targetPath);
            }

            return new ReleaseCenterPluginDownloadResult(true, $"插件包已缓存 {downloaded.Count} 项", string.Join(Environment.NewLine, downloaded), downloaded.Count, cacheDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "下载发布中心插件包失败");
            return new ReleaseCenterPluginDownloadResult(false, "插件包下载失败", ex.Message, 0, cacheDirectory);
        }
    }

    public Task<ReleaseCenterPluginPrepareResult> PrepareCachedPluginPackagesAsync(CancellationToken cancellationToken = default)
    {
        var cacheDirectory = ResolveCacheDirectory();
        var stagingDirectory = ResolveStagingDirectory();
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            if (!Directory.Exists(cacheDirectory))
            {
                return Task.FromResult(new ReleaseCenterPluginPrepareResult(true, "没有可预安装的缓存包", cacheDirectory, 0, stagingDirectory));
            }

            var zipFiles = Directory.GetFiles(cacheDirectory, "*.zip", SearchOption.TopDirectoryOnly);
            if (zipFiles.Length == 0)
            {
                return Task.FromResult(new ReleaseCenterPluginPrepareResult(true, "没有可预安装的缓存包", cacheDirectory, 0, stagingDirectory));
            }

            var prepared = new List<string>();
            foreach (var zipFile in zipFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var packageName = Path.GetFileNameWithoutExtension(zipFile);
                var extractDirectory = Path.Combine(stagingDirectory, packageName);
                if (Directory.Exists(extractDirectory))
                {
                    Directory.Delete(extractDirectory, recursive: true);
                }

                ZipFile.ExtractToDirectory(zipFile, extractDirectory);
                var pluginJsonPath = Directory.GetFiles(extractDirectory, "plugin.json", SearchOption.AllDirectories).FirstOrDefault();
                if (pluginJsonPath is null)
                {
                    throw new InvalidOperationException($"缓存包 {Path.GetFileName(zipFile)} 缺少 plugin.json。");
                }

                var pluginId = ReadPluginId(pluginJsonPath);
                prepared.Add($"{pluginId}: {extractDirectory}");
            }

            return Task.FromResult(new ReleaseCenterPluginPrepareResult(true, $"已生成 {prepared.Count} 个预安装目录", string.Join(Environment.NewLine, prepared), prepared.Count, stagingDirectory));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "准备插件预安装目录失败");
            return Task.FromResult(new ReleaseCenterPluginPrepareResult(false, "插件预安装失败", ex.Message, 0, stagingDirectory));
        }
    }

    public Task<ReleaseCenterPluginActivateResult> ActivatePreparedPluginPackagesAsync(CancellationToken cancellationToken = default)
    {
        var stagingDirectory = ResolveStagingDirectory();
        var runtimePluginRoot = ResolveRuntimePluginRoot();
        var backupRoot = ResolveBackupDirectory();
        Directory.CreateDirectory(runtimePluginRoot);
        Directory.CreateDirectory(backupRoot);

        try
        {
            if (!Directory.Exists(stagingDirectory))
            {
                return Task.FromResult(new ReleaseCenterPluginActivateResult(true, "没有待激活的预安装目录", stagingDirectory, 0, runtimePluginRoot));
            }

            var stagedDirectories = Directory.GetDirectories(stagingDirectory);
            if (stagedDirectories.Length == 0)
            {
                return Task.FromResult(new ReleaseCenterPluginActivateResult(true, "没有待激活的预安装目录", stagingDirectory, 0, runtimePluginRoot));
            }

            var activated = new List<string>();
            var backupSession = Path.Combine(backupRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(backupSession);

            foreach (var stagedDirectory in stagedDirectories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pluginJsonPath = Directory.GetFiles(stagedDirectory, "plugin.json", SearchOption.AllDirectories).FirstOrDefault();
                if (pluginJsonPath is null)
                {
                    throw new InvalidOperationException($"预安装目录 {stagedDirectory} 缺少 plugin.json。");
                }

                var pluginId = ReadPluginId(pluginJsonPath);
                var sourcePluginDirectory = Path.GetDirectoryName(pluginJsonPath)!;
                var targetDirectory = Path.Combine(runtimePluginRoot, pluginId);

                if (Directory.Exists(targetDirectory))
                {
                    var backupDirectory = Path.Combine(backupSession, pluginId);
                    Directory.Move(targetDirectory, backupDirectory);
                }

                CopyDirectory(sourcePluginDirectory, targetDirectory);
                Directory.Delete(stagedDirectory, recursive: true);
                activated.Add($"{pluginId}: {targetDirectory}");
            }

            return Task.FromResult(new ReleaseCenterPluginActivateResult(true, $"已激活 {activated.Count} 个插件目录", string.Join(Environment.NewLine, activated), activated.Count, runtimePluginRoot));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "激活插件预安装目录失败");
            return Task.FromResult(new ReleaseCenterPluginActivateResult(false, "插件激活失败", ex.Message, 0, runtimePluginRoot));
        }
    }

    public Task<ReleaseCenterPluginRollbackResult> RestoreLatestPluginBackupAsync(CancellationToken cancellationToken = default)
    {
        var backupRoot = ResolveBackupDirectory();
        var runtimePluginRoot = ResolveRuntimePluginRoot();
        Directory.CreateDirectory(runtimePluginRoot);

        try
        {
            if (!Directory.Exists(backupRoot))
            {
                return Task.FromResult(new ReleaseCenterPluginRollbackResult(true, "没有可回滚的插件备份", backupRoot, 0, runtimePluginRoot));
            }

            var latestSession = Directory.GetDirectories(backupRoot)
                .OrderByDescending(Path.GetFileName)
                .FirstOrDefault();
            if (latestSession is null)
            {
                return Task.FromResult(new ReleaseCenterPluginRollbackResult(true, "没有可回滚的插件备份", backupRoot, 0, runtimePluginRoot));
            }

            var restored = new List<string>();
            foreach (var pluginBackupDir in Directory.GetDirectories(latestSession))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pluginId = Path.GetFileName(pluginBackupDir);
                var targetDirectory = Path.Combine(runtimePluginRoot, pluginId);
                if (Directory.Exists(targetDirectory))
                {
                    Directory.Delete(targetDirectory, recursive: true);
                }

                CopyDirectory(pluginBackupDir, targetDirectory);
                restored.Add($"{pluginId}: {targetDirectory}");
            }

            return Task.FromResult(new ReleaseCenterPluginRollbackResult(true, "已恢复最近一次插件备份", string.Join(Environment.NewLine, restored), restored.Count, runtimePluginRoot));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "恢复插件备份失败");
            return Task.FromResult(new ReleaseCenterPluginRollbackResult(false, "插件回滚失败", ex.Message, 0, runtimePluginRoot));
        }
    }

    private async Task<ClientManifestDto?> GetClientManifestAsync(CancellationToken cancellationToken)
    {
        return await _httpClient.GetFromJsonAsync<ClientManifestDto>(BuildManifestUrl("client-manifest.json"), cancellationToken).ConfigureAwait(false);
    }

    private async Task<PluginManifestDto?> GetPluginManifestAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_config.AnonKey))
        {
            return await _httpClient.GetFromJsonAsync<PluginManifestDto>(BuildManifestUrl("plugin-manifest.json", includeSiteId: true), cancellationToken).ConfigureAwait(false);
        }

        var payload = EnsureSiteHeartbeatPayload();
        await RegisterSiteHeartbeatAsync(payload, cancellationToken).ConfigureAwait(false);
        return await GetSiteScopedPluginManifestAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    private string BuildManifestUrl(string fileName, bool includeSiteId = false)
    {
        var baseUrl = _config.BaseUrl.TrimEnd('/');
        var channel = _config.Channel.Trim();
        var manifestUrl = $"{baseUrl}/storage/v1/object/public/dib-releases/manifests/{channel}/{fileName}";
        if (!includeSiteId || string.IsNullOrWhiteSpace(_config.SiteId))
        {
            return manifestUrl;
        }

        return $"{manifestUrl}?siteId={Uri.EscapeDataString(_config.SiteId)}";
    }

    private SiteHeartbeatPayload EnsureSiteHeartbeatPayload()
    {
        var changed = false;

        if (string.IsNullOrWhiteSpace(_config.SiteId))
        {
            _config.SiteId = Guid.NewGuid().ToString().ToLowerInvariant();
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(_config.SiteName))
        {
            _config.SiteName = Environment.MachineName;
            changed = true;
        }

        if (changed)
        {
            PersistReleaseCenterIdentity();
        }

        return new SiteHeartbeatPayload(
            _config.SiteId,
            _config.SiteName,
            _config.Channel.Trim(),
            _currentAppVersion,
            Environment.MachineName,
            DateTimeOffset.UtcNow);
    }

    private async Task RegisterSiteHeartbeatAsync(SiteHeartbeatPayload payload, CancellationToken cancellationToken)
    {
        using var request = BuildRpcRequest(
            "register_site_heartbeat",
            new
            {
                p_site_id = payload.SiteId,
                p_site_name = payload.SiteName,
                p_channel_code = payload.Channel,
                p_client_version = payload.ClientVersion,
                p_machine_name = payload.MachineName,
                p_installed_plugins_json = GetInstalledPluginIds(),
                p_event_type = "update_check"
            });

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async Task<PluginManifestDto?> GetSiteScopedPluginManifestAsync(SiteHeartbeatPayload payload, CancellationToken cancellationToken)
    {
        using var request = BuildRpcRequest(
            "get_site_plugin_manifest",
            new
            {
                p_channel_code = payload.Channel,
                p_site_id = payload.SiteId,
                p_client_version = payload.ClientVersion
            });

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<PluginManifestDto>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private HttpRequestMessage BuildRpcRequest(string rpcName, object payload)
    {
        var baseUrl = _config.BaseUrl.TrimEnd('/');
        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/rest/v1/rpc/{rpcName}");
        request.Headers.Add("apikey", _config.AnonKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.AnonKey);
        request.Headers.Add("Accept-Profile", "dib_release");
        request.Headers.Add("Content-Profile", "dib_release");
        request.Content = JsonContent.Create(payload);
        return request;
    }

    private IReadOnlyList<string> GetInstalledPluginIds()
    {
        try
        {
            var runtimeRoot = ResolveRuntimePluginRoot();
            if (!Directory.Exists(runtimeRoot))
            {
                return Array.Empty<string>();
            }

            return Directory.GetDirectories(runtimeRoot)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToArray()!;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private void PersistReleaseCenterIdentity()
    {
        var configPath = DigitalIntelligenceBridge.Configuration.ConfigurationExtensions.GetConfigFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        var json = JsonSerializer.Serialize(_appSettings, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(configPath, json);
    }

    private string ResolveCacheDirectory() => !string.IsNullOrWhiteSpace(_config.CacheDirectory) ? _config.CacheDirectory : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UniversalTrayTool", "release-cache", "plugins", _config.Channel.Trim());
    private string ResolveClientCacheDirectory() => !string.IsNullOrWhiteSpace(_config.ClientCacheDirectory) ? _config.ClientCacheDirectory : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UniversalTrayTool", "release-cache", "clients", _config.Channel.Trim());
    private string ResolveStagingDirectory() => !string.IsNullOrWhiteSpace(_config.StagingDirectory) ? _config.StagingDirectory : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UniversalTrayTool", "release-staging", "plugins", _config.Channel.Trim());
    private string ResolveRuntimePluginRoot() => !string.IsNullOrWhiteSpace(_config.RuntimePluginRoot) ? _config.RuntimePluginRoot : Path.Combine(AppContext.BaseDirectory, _pluginConfig.PluginDirectory);
    private string ResolveBackupDirectory() => !string.IsNullOrWhiteSpace(_config.BackupDirectory) ? _config.BackupDirectory : Path.Combine(AppContext.BaseDirectory, "release-backups", "plugins");
    private string NormalizePackageUrl(string packageUrl)
    {
        if (Uri.TryCreate(packageUrl, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.ToString();
        }

        var baseUri = new Uri(_config.BaseUrl.TrimEnd('/') + "/");
        return new Uri(baseUri, packageUrl).ToString();
    }

    private bool HasClientUpdate(ClientManifestDto? manifest) => manifest is not null && !string.IsNullOrWhiteSpace(manifest.LatestVersion) && CompareVersions(manifest.LatestVersion, _currentAppVersion) > 0;
    private static string BuildClientSummary(ClientManifestDto? manifest) => manifest is null || string.IsNullOrWhiteSpace(manifest.LatestVersion) ? "客户端更新：暂无发布版本" : $"客户端最新版本：{manifest.LatestVersion}（最低升级版本：{manifest.MinUpgradeVersion ?? "未限制"}）";
    private static string BuildPluginSummary(PluginManifestDto? manifest) { var count = manifest?.Plugins?.Count ?? 0; if (count == 0) return "插件更新：暂无可用插件包"; var items = manifest!.Plugins!.Take(3).Select(item => $"{item.Name} {item.Version}"); return $"插件更新：{count} 个可用插件（{string.Join('、', items)}）"; }
    private static string BuildAuthorizedPluginSummary(PluginManifestDto? manifest) { var count = manifest?.Plugins?.Count ?? 0; return count == 0 ? "授权插件：当前站点未授权任何插件" : $"授权插件：{count} 个"; }
    private static string BuildAuthorizedPluginDetail(PluginManifestDto? manifest) { var items = manifest?.Plugins?.Select(item => $"{item.Name} / {item.PluginId} / {item.Version}") ?? Array.Empty<string>(); return string.Join(Environment.NewLine, items); }
    private static string BuildDownloadFileName(PluginItemDto plugin) { if (Uri.TryCreate(plugin.PackageUrl, UriKind.Absolute, out var uri)) { var fromUrl = Path.GetFileName(uri.LocalPath); if (!string.IsNullOrWhiteSpace(fromUrl)) return fromUrl; } var version = string.IsNullOrWhiteSpace(plugin.Version) ? "latest" : plugin.Version; return $"{plugin.PluginId}-{version}.zip"; }
    private static string BuildClientDownloadFileName(ClientManifestDto clientManifest) { if (Uri.TryCreate(clientManifest.PackageUrl, UriKind.Absolute, out var uri)) { var fromUrl = Path.GetFileName(uri.LocalPath); if (!string.IsNullOrWhiteSpace(fromUrl)) return fromUrl; } var version = string.IsNullOrWhiteSpace(clientManifest.LatestVersion) ? "latest" : clientManifest.LatestVersion; return $"dib-win-x64-portable-{version}.zip"; }
    private static void ValidateSha256(PluginItemDto plugin, byte[] bytes) { if (string.IsNullOrWhiteSpace(plugin.Sha256)) return; var computed = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(); if (!string.Equals(computed, plugin.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"插件 {plugin.PluginId} 的 SHA256 校验失败。"); }
    private static void ValidateClientSha256(ClientManifestDto clientManifest, string computedHash) { if (string.IsNullOrWhiteSpace(clientManifest.Sha256)) return; if (!string.Equals(computedHash, clientManifest.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("客户端更新包的 SHA256 校验失败。"); }
    private static string ReadPluginId(string pluginJsonPath) { using var stream = File.OpenRead(pluginJsonPath); using var document = JsonDocument.Parse(stream); if (!document.RootElement.TryGetProperty("id", out var idProperty) || string.IsNullOrWhiteSpace(idProperty.GetString())) throw new InvalidOperationException($"{pluginJsonPath} 缺少插件 id。"); return idProperty.GetString()!; }
    private static void CopyDirectory(string sourceDirectory, string targetDirectory) { Directory.CreateDirectory(targetDirectory); foreach (var file in Directory.GetFiles(sourceDirectory)) File.Copy(file, Path.Combine(targetDirectory, Path.GetFileName(file)), overwrite: true); foreach (var directory in Directory.GetDirectories(sourceDirectory)) CopyDirectory(directory, Path.Combine(targetDirectory, Path.GetFileName(directory))); }
    public static int CompareVersions(string left, string right) { var leftParts = ParseVersionParts(left); var rightParts = ParseVersionParts(right); var max = Math.Max(leftParts.Length, rightParts.Length); for (var i = 0; i < max; i++) { var lv = i < leftParts.Length ? leftParts[i] : 0; var rv = i < rightParts.Length ? rightParts[i] : 0; if (lv != rv) return lv.CompareTo(rv); } return 0; }
    private static int[] ParseVersionParts(string value) => value.Split('-', 2)[0].Split('.', StringSplitOptions.RemoveEmptyEntries).Select(part => int.TryParse(part, out var parsed) ? parsed : 0).ToArray();

    private sealed record SiteHeartbeatPayload(string SiteId, string SiteName, string Channel, string ClientVersion, string MachineName, DateTimeOffset CheckedAt);
    private sealed class ClientManifestDto { [JsonPropertyName("latestVersion")] public string? LatestVersion { get; set; } [JsonPropertyName("minUpgradeVersion")] public string? MinUpgradeVersion { get; set; } [JsonPropertyName("packageUrl")] public string? PackageUrl { get; set; } [JsonPropertyName("sha256")] public string? Sha256 { get; set; } }
    private sealed class PluginManifestDto { [JsonPropertyName("plugins")] public List<PluginItemDto>? Plugins { get; set; } }
    private sealed class PluginItemDto { [JsonPropertyName("pluginId")] public string PluginId { get; set; } = string.Empty; [JsonPropertyName("name")] public string Name { get; set; } = string.Empty; [JsonPropertyName("version")] public string Version { get; set; } = string.Empty; [JsonPropertyName("packageUrl")] public string? PackageUrl { get; set; } [JsonPropertyName("sha256")] public string? Sha256 { get; set; } }
}





