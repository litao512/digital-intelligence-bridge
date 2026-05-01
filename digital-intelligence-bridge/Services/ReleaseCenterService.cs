using System;
using System.Collections.Concurrent;
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
using DigitalIntelligenceBridge.Models;
using DigitalIntelligenceBridge.Plugin.Abstractions;
using DigitalIntelligenceBridge.Plugin.Host;
using PluginRuntimeResource = DigitalIntelligenceBridge.Plugin.Abstractions.AuthorizedRuntimeResource;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DigitalIntelligenceBridge.Services;

public interface IReleaseCenterService
{
    bool IsConfigured { get; }
    Task<ReleaseCenterCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);
    Task<ResourceDiscoverySnapshot> DiscoverResourcesAsync(CancellationToken cancellationToken = default);
    Task<AuthorizedResourceSnapshot> GetAuthorizedResourcesAsync(CancellationToken cancellationToken = default);
    Task<ResourceApplicationSubmitResult> ApplyResourceAsync(string resourceId, string pluginCode, string reason, CancellationToken cancellationToken = default);
    Task<ReleaseCenterClientDownloadResult> DownloadLatestClientPackageAsync(IProgress<ReleaseCenterDownloadProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<ReleaseCenterPluginDownloadResult> DownloadAvailablePluginPackagesAsync(CancellationToken cancellationToken = default);
    Task<ReleaseCenterPluginDownloadResult> DownloadPluginPackageAsync(string pluginId, string? version = null, IProgress<ReleaseCenterDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
        => DownloadAvailablePluginPackagesAsync(cancellationToken);
    Task<ReleaseCenterPluginPrepareResult> PrepareCachedPluginPackagesAsync(CancellationToken cancellationToken = default);
    Task<ReleaseCenterPluginPrepareResult> PreparePluginPackageAsync(string pluginId, string? version = null, CancellationToken cancellationToken = default)
        => PrepareCachedPluginPackagesAsync(cancellationToken);
    Task<ReleaseCenterPluginUninstallResult> MarkPluginForUninstallAsync(string pluginId, CancellationToken cancellationToken = default)
        => Task.FromResult(new ReleaseCenterPluginUninstallResult(false, "插件卸载不可用", "当前发布中心服务未实现插件卸载。", pluginId, string.Empty));
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
public sealed record ReleaseCenterPluginUninstallResult(bool IsSuccess, string Summary, string Detail, string PluginId, string StagingDirectory);
public sealed record ReleaseCenterPluginActivateResult(bool IsSuccess, string Summary, string Detail, int ActivatedCount, string RuntimePluginRoot);
public sealed record ReleaseCenterPluginRollbackResult(bool IsSuccess, string Summary, string Detail, int RestoredCount, string RuntimePluginRoot);
public sealed record ResourceApplicationSubmitResult(
    [property: JsonPropertyName("success")] bool IsSuccess,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("applicationId")] string ApplicationId,
    [property: JsonPropertyName("status")] string Status);

public sealed class ReleaseCenterService : IReleaseCenterService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> PluginPackageDownloadLocks = new(StringComparer.OrdinalIgnoreCase);

    private readonly HttpClient _httpClient;
    private readonly ILogger<ReleaseCenterService> _logger;
    private readonly AppSettings _appSettings;
    private readonly ReleaseCenterConfig _config;
    private readonly PluginConfig _pluginConfig;
    private readonly string _currentAppVersion;
    private readonly PluginCatalogService _pluginCatalogService;
    private readonly IAuthorizedResourceCacheService? _authorizedResourceCacheService;

    public ReleaseCenterService(HttpClient httpClient, ILogger<ReleaseCenterService> logger, IOptions<AppSettings> settings)
        : this(httpClient, logger, settings, new PluginCatalogService(), null)
    {
    }

    public ReleaseCenterService(HttpClient httpClient, ILogger<ReleaseCenterService> logger, IOptions<AppSettings> settings, PluginCatalogService pluginCatalogService)
        : this(httpClient, logger, settings, pluginCatalogService, null)
    {
    }

    public ReleaseCenterService(
        HttpClient httpClient,
        ILogger<ReleaseCenterService> logger,
        IOptions<AppSettings> settings,
        IAuthorizedResourceCacheService? authorizedResourceCacheService)
        : this(httpClient, logger, settings, new PluginCatalogService(), authorizedResourceCacheService)
    {
    }

    public ReleaseCenterService(
        HttpClient httpClient,
        ILogger<ReleaseCenterService> logger,
        IOptions<AppSettings> settings,
        PluginCatalogService pluginCatalogService,
        IAuthorizedResourceCacheService? authorizedResourceCacheService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _appSettings = settings.Value;
        _config = _appSettings.ReleaseCenter;
        _pluginConfig = _appSettings.Plugin;
        _currentAppVersion = _appSettings.Application.Version;
        _pluginCatalogService = pluginCatalogService;
        _authorizedResourceCacheService = authorizedResourceCacheService;
    }

    public bool IsConfigured => _config.Enabled && !string.IsNullOrWhiteSpace(_config.BaseUrl) && !string.IsNullOrWhiteSpace(_config.Channel);

    public async Task<AuthorizedResourceSnapshot> GetAuthorizedResourcesAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("ReleaseCenter 未启用或缺少 BaseUrl/Channel，已跳过授权资源刷新。");
            return new AuthorizedResourceSnapshot();
        }

        if (string.IsNullOrWhiteSpace(_config.AnonKey))
        {
            _logger.LogWarning("ReleaseCenter.AnonKey 未配置，已跳过授权资源刷新。请检查程序目录 appsettings.json 是否包含发布中心匿名密钥。");
            return new AuthorizedResourceSnapshot();
        }

        var payload = EnsureSiteHeartbeatPayload();
        var pluginRequirements = GetInstalledPluginRequirementGroups();

        using var request = BuildRpcRequest(
            "get_site_authorized_resources",
            new
            {
                p_channel_code = payload.Channel,
                p_site_id = payload.SiteId,
                p_client_version = payload.ClientVersion,
                p_plugins_json = BuildPluginRequirementsPayload(pluginRequirements)
            });

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = await JsonSerializer.DeserializeAsync<AuthorizedResourceSnapshot>(stream, cancellationToken: cancellationToken).ConfigureAwait(false)
               ?? new AuthorizedResourceSnapshot();
        TrySaveAuthorizedResourcesToCache(snapshot, pluginRequirements);
        return snapshot;
    }

    public async Task<ResourceDiscoverySnapshot> DiscoverResourcesAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(_config.AnonKey))
        {
            return new ResourceDiscoverySnapshot();
        }

        var payload = EnsureSiteHeartbeatPayload();
        var pluginRequirements = GetInstalledPluginRequirementGroups();

        using var request = BuildRpcRequest(
            "discover_site_resources",
            new
            {
                p_channel_code = payload.Channel,
                p_site_id = payload.SiteId,
                p_client_version = payload.ClientVersion,
                p_plugins_json = BuildPluginRequirementsPayload(pluginRequirements)
            });

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<ResourceDiscoverySnapshot>(stream, cancellationToken: cancellationToken).ConfigureAwait(false)
               ?? new ResourceDiscoverySnapshot();
    }

    public async Task<ResourceApplicationSubmitResult> ApplyResourceAsync(string resourceId, string pluginCode, string reason, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(_config.AnonKey))
        {
            return new ResourceApplicationSubmitResult(false, "资源中心不可用", string.Empty, string.Empty);
        }

        var payload = EnsureSiteHeartbeatPayload();
        var normalizedReason = BuildApplicationReason(payload.SiteName, reason);
        using var request = BuildRpcRequest(
            "apply_site_resource",
            new
            {
                p_channel_code = payload.Channel,
                p_site_id = payload.SiteId,
                p_client_version = payload.ClientVersion,
                p_resource_id = resourceId,
                p_plugin_code = pluginCode,
                p_reason = normalizedReason
            });

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<ResourceApplicationSubmitResult>(stream, cancellationToken: cancellationToken).ConfigureAwait(false)
               ?? new ResourceApplicationSubmitResult(false, "申请结果为空", string.Empty, string.Empty);
    }

    private static string BuildApplicationReason(string siteRegistrationLabel, string reason)
    {
        var label = siteRegistrationLabel?.Trim() ?? string.Empty;
        var requestReason = reason?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(label))
        {
            return requestReason;
        }

        if (string.IsNullOrWhiteSpace(requestReason))
        {
            return $"站点信息：{label}";
        }

        return $"站点信息：{label}；申请说明：{requestReason}";
    }

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
        string? targetPath = null;

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

            targetPath = Path.Combine(cacheDirectory, BuildClientDownloadFileName(clientManifest));
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
        catch (OperationCanceledException)
        {
            TryDeleteFile(targetPath);
            return new ReleaseCenterClientDownloadResult(false, "客户端下载已取消", "已取消客户端更新包下载。", string.Empty, cacheDirectory, string.Empty);
        }
        catch (Exception ex)
        {
            TryDeleteFile(targetPath);
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
            var installedVersions = GetInstalledPluginVersions();
            var downloadablePlugins = pluginManifest?.Plugins?
                .Where(item => !string.IsNullOrWhiteSpace(item.PackageUrl))
                .Where(item => HasPluginUpdate(item, installedVersions))
                .ToList() ?? new List<PluginItemDto>();
            if (downloadablePlugins.Count == 0)
            {
                return new ReleaseCenterPluginDownloadResult(true, "没有可下载的插件包", $"channel={_config.Channel}", 0, cacheDirectory);
            }

            var downloaded = new List<string>();
            foreach (var plugin in downloadablePlugins)
            {
                var downloadResult = await DownloadPluginPackageAsync(plugin.PluginId, plugin.Version, null, cancellationToken).ConfigureAwait(false);
                if (!downloadResult.IsSuccess)
                {
                    return downloadResult;
                }

                if (downloadResult.DownloadedCount > 0 && !string.IsNullOrWhiteSpace(downloadResult.Detail))
                {
                    downloaded.Add(downloadResult.Detail);
                }
            }

            return new ReleaseCenterPluginDownloadResult(true, $"插件包已缓存 {downloaded.Count} 项", string.Join(Environment.NewLine, downloaded), downloaded.Count, cacheDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "下载发布中心插件包失败");
            return new ReleaseCenterPluginDownloadResult(false, "插件包下载失败", ex.Message, 0, cacheDirectory);
        }
    }

    public async Task<ReleaseCenterPluginDownloadResult> DownloadPluginPackageAsync(string pluginId, string? version = null, IProgress<ReleaseCenterDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new ReleaseCenterPluginDownloadResult(false, "插件包下载不可用", "ReleaseCenter 未启用或缺少 BaseUrl/Channel。", 0, string.Empty);
        }

        var normalizedPluginId = pluginId.Trim();
        var cacheDirectory = ResolveCacheDirectory();
        Directory.CreateDirectory(cacheDirectory);
        string? targetPath = null;
        var ownsTargetFile = false;

        try
        {
            progress?.Report(new ReleaseCenterDownloadProgress("preparing", "正在准备插件下载", 0, null, 0, null));
            var pluginManifest = await GetPluginManifestAsync(cancellationToken).ConfigureAwait(false);
            var plugin = pluginManifest?.Plugins?
                .FirstOrDefault(item =>
                    string.Equals(item.PluginId, normalizedPluginId, StringComparison.OrdinalIgnoreCase)
                    && (string.IsNullOrWhiteSpace(version) || string.Equals(item.Version, version, StringComparison.OrdinalIgnoreCase)));

            if (plugin is null || string.IsNullOrWhiteSpace(plugin.PackageUrl))
            {
                return new ReleaseCenterPluginDownloadResult(true, "没有可下载的插件包", $"pluginId={normalizedPluginId}; version={version ?? "latest"}", 0, cacheDirectory);
            }

            var fileName = BuildDownloadFileName(plugin);
            targetPath = Path.Combine(cacheDirectory, fileName);
            var packageUrl = NormalizePackageUrl(plugin.PackageUrl!);

            var downloadLock = PluginPackageDownloadLocks.GetOrAdd(
                Path.GetFullPath(targetPath),
                _ => new SemaphoreSlim(1, 1));
            await downloadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            ownsTargetFile = true;
            try
            {
                if (TryValidateCachedPluginPackage(plugin, targetPath))
                {
                    progress?.Report(new ReleaseCenterDownloadProgress("completed", "插件包已在本地缓存", 0, null, 0, TimeSpan.Zero));
                    return new ReleaseCenterPluginDownloadResult(true, "插件包已缓存 1 项", targetPath, 1, cacheDirectory);
                }

                using var response = await _httpClient.GetAsync(packageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
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

                        progress?.Report(new ReleaseCenterDownloadProgress("downloading", "正在下载插件包", bytesReceived, totalBytes, bytesPerSecond, remaining));
                        lastReport = elapsed;
                    }
                }

                progress?.Report(new ReleaseCenterDownloadProgress("verifying", "正在校验插件包", bytesReceived, totalBytes ?? bytesReceived, 0, TimeSpan.Zero));
                var computedHash = Convert.ToHexString(incrementalHash.GetHashAndReset()).ToLowerInvariant();
                ValidateSha256(plugin, computedHash);
                progress?.Report(new ReleaseCenterDownloadProgress("completed", "插件包下载完成", bytesReceived, totalBytes ?? bytesReceived, 0, TimeSpan.Zero));
                return new ReleaseCenterPluginDownloadResult(true, "插件包已缓存 1 项", targetPath, 1, cacheDirectory);
            }
            finally
            {
                downloadLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            if (ownsTargetFile)
            {
                TryDeleteFile(targetPath);
            }
            return new ReleaseCenterPluginDownloadResult(false, "插件下载已取消", "已取消插件包下载。", 0, cacheDirectory);
        }
        catch (Exception ex)
        {
            if (ownsTargetFile)
            {
                TryDeleteFile(targetPath);
            }
            _logger.LogWarning(ex, "下载指定发布中心插件包失败: {PluginId}", normalizedPluginId);
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
                prepared.Add(PreparePluginZip(zipFile, stagingDirectory));
            }

            return Task.FromResult(new ReleaseCenterPluginPrepareResult(true, $"已生成 {prepared.Count} 个预安装目录", string.Join(Environment.NewLine, prepared), prepared.Count, stagingDirectory));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "准备插件预安装目录失败");
            return Task.FromResult(new ReleaseCenterPluginPrepareResult(false, "插件预安装失败", ex.Message, 0, stagingDirectory));
        }
    }

    public Task<ReleaseCenterPluginPrepareResult> PreparePluginPackageAsync(string pluginId, string? version = null, CancellationToken cancellationToken = default)
    {
        var normalizedPluginId = pluginId.Trim();
        var cacheDirectory = ResolveCacheDirectory();
        var stagingDirectory = ResolveStagingDirectory();
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            if (!Directory.Exists(cacheDirectory))
            {
                return Task.FromResult(new ReleaseCenterPluginPrepareResult(true, "没有可预安装的缓存包", cacheDirectory, 0, stagingDirectory));
            }

            var zipFile = Directory
                .GetFiles(cacheDirectory, "*.zip", SearchOption.TopDirectoryOnly)
                .Where(path => Path.GetFileName(path).StartsWith(normalizedPluginId, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(path => string.IsNullOrWhiteSpace(version) || Path.GetFileNameWithoutExtension(path).Contains(version, StringComparison.OrdinalIgnoreCase));

            if (zipFile is null)
            {
                return Task.FromResult(new ReleaseCenterPluginPrepareResult(true, "没有可预安装的缓存包", $"pluginId={normalizedPluginId}; version={version ?? "latest"}", 0, stagingDirectory));
            }

            var prepared = PreparePluginZip(zipFile, stagingDirectory);
            return Task.FromResult(new ReleaseCenterPluginPrepareResult(true, "已生成 1 个预安装目录", prepared, 1, stagingDirectory));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "准备指定插件预安装目录失败: {PluginId}", normalizedPluginId);
            return Task.FromResult(new ReleaseCenterPluginPrepareResult(false, "插件预安装失败", ex.Message, 0, stagingDirectory));
        }
    }

    public async Task<ReleaseCenterPluginUninstallResult> MarkPluginForUninstallAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        var normalizedPluginId = pluginId.Trim();
        var stagingDirectory = ResolveStagingDirectory();
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            var markerDirectory = Path.Combine(stagingDirectory, $"uninstall-{normalizedPluginId}");
            if (Directory.Exists(markerDirectory))
            {
                Directory.Delete(markerDirectory, recursive: true);
            }

            Directory.CreateDirectory(markerDirectory);
            var markerPath = Path.Combine(markerDirectory, "uninstall.json");
            await File.WriteAllTextAsync(
                markerPath,
                JsonSerializer.Serialize(new PluginUninstallMarker(normalizedPluginId, DateTimeOffset.UtcNow)),
                cancellationToken).ConfigureAwait(false);
            return new ReleaseCenterPluginUninstallResult(true, "插件已标记为待卸载", markerPath, normalizedPluginId, stagingDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "标记插件待卸载失败: {PluginId}", normalizedPluginId);
            return new ReleaseCenterPluginUninstallResult(false, "插件卸载标记失败", ex.Message, normalizedPluginId, stagingDirectory);
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

            var stagedDirectories = NormalizeStagedPluginDirectories(Directory.GetDirectories(stagingDirectory));
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
                var uninstallMarkerPath = Path.Combine(stagedDirectory, "uninstall.json");
                if (File.Exists(uninstallMarkerPath))
                {
                    var marker = ReadPluginUninstallMarker(uninstallMarkerPath);
                    var uninstallTargetDirectory = Path.Combine(runtimePluginRoot, marker.PluginId);
                    if (Directory.Exists(uninstallTargetDirectory))
                    {
                        var backupDirectory = Path.Combine(backupSession, marker.PluginId);
                        Directory.Move(uninstallTargetDirectory, backupDirectory);
                    }

                    Directory.Delete(stagedDirectory, recursive: true);
                    activated.Add($"{marker.PluginId}: uninstall");
                    continue;
                }

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
            _config.SiteName.Trim(),
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

    private IReadOnlyList<object> BuildPluginRequirementsPayload(IReadOnlyList<InstalledPluginRequirementGroup> pluginRequirements)
    {
        return pluginRequirements
            .Select(plugin => new
            {
                pluginCode = plugin.PluginCode,
                requirements = plugin.Requirements.Select(requirement => new
                {
                    resourceType = requirement.ResourceType,
                    usageKey = requirement.UsageKey,
                    required = requirement.Required,
                    description = requirement.Description
                }).ToArray()
            })
            .Cast<object>()
            .ToArray();
    }

    private IReadOnlyList<InstalledPluginRequirementGroup> GetInstalledPluginRequirementGroups()
    {
        try
        {
            var runtimeRoot = ResolveRuntimePluginRoot();
            if (!Directory.Exists(runtimeRoot))
            {
                return Array.Empty<InstalledPluginRequirementGroup>();
            }

            return _pluginCatalogService
                .DiscoverManifests(runtimeRoot)
                .Select(plugin => new InstalledPluginRequirementGroup(
                    plugin.Manifest.Id,
                    plugin.Manifest.ResourceRequirements
                        .Select(requirement => new InstalledPluginRequirement(
                            requirement.ResourceType,
                            requirement.UsageKey,
                            requirement.Required,
                            requirement.Description))
                        .ToArray()))
                .ToArray();
        }
        catch
        {
            return Array.Empty<InstalledPluginRequirementGroup>();
        }
    }

    private void TrySaveAuthorizedResourcesToCache(AuthorizedResourceSnapshot snapshot, IReadOnlyList<InstalledPluginRequirementGroup> installedRequirements)
    {
        if (_authorizedResourceCacheService is null)
        {
            return;
        }

        try
        {
            var groupedResources = snapshot.Resources
                .Where(resource => !string.IsNullOrWhiteSpace(resource.PluginCode))
                .GroupBy(resource => resource.PluginCode!, StringComparer.Ordinal)
                .Select(group => new AuthorizedPluginResourceSet
                {
                    PluginCode = group.Key,
                    Resources = MapPluginResources(group.Key, group, installedRequirements)
                })
                .ToArray();

            if (snapshot.Resources.Count > 0 && groupedResources.Length == 0)
            {
                _logger.LogWarning("授权资源同步结果包含 {Count} 条记录，但都缺少有效 PluginCode，已保留上一版缓存", snapshot.Resources.Count);
                return;
            }

            var syncedAt = DateTimeOffset.UtcNow;

            _authorizedResourceCacheService.SaveSnapshot(new AuthorizedResourceCacheSnapshot
            {
                SiteId = _config.SiteId,
                SnapshotVersion = groupedResources.Length,
                SyncedAt = syncedAt,
                ExpiresAt = syncedAt.AddMinutes(30),
                Resources = groupedResources
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "写入授权资源缓存失败，已降级为仅返回本次同步结果");
        }
    }

    private static PluginRuntimeResource MapRuntimeResource(ResourceDescriptor resource, UsageKeySequenceResolver usageKeyResolver)
    {
        return new PluginRuntimeResource
        {
            ResourceId = resource.ResourceId,
            ResourceCode = resource.ResourceCode,
            ResourceName = resource.ResourceName,
            ResourceType = resource.ResourceType,
            UsageKey = usageKeyResolver.Resolve(resource.ResourceType, resource.ResourceCode),
            BindingScope = resource.BindingScope,
            Version = resource.ConfigVersion,
            Capabilities = resource.Capabilities,
            Configuration = CloneJsonElement(resource.ConfigPayload)
        };
    }

    private static PluginRuntimeResource[] MapPluginResources(string pluginCode, IEnumerable<ResourceDescriptor> resources, IReadOnlyList<InstalledPluginRequirementGroup> installedRequirements)
    {
        var usageKeyResolver = CreateUsageKeyResolver(pluginCode, installedRequirements);
        return resources
            .OrderBy(resource => resource.ResourceType, StringComparer.Ordinal)
            .ThenBy(resource => resource.ResourceId, StringComparer.Ordinal)
            .ThenBy(resource => resource.ResourceCode, StringComparer.Ordinal)
            .Select(resource => MapRuntimeResource(resource, usageKeyResolver))
            .ToArray();
    }

    private static UsageKeySequenceResolver CreateUsageKeyResolver(string pluginCode, IReadOnlyList<InstalledPluginRequirementGroup> installedRequirements)
    {
        var pluginRequirements = installedRequirements.FirstOrDefault(item => string.Equals(item.PluginCode, pluginCode, StringComparison.Ordinal));
        return new UsageKeySequenceResolver(pluginRequirements?.Requirements ?? Array.Empty<InstalledPluginRequirement>());
    }

    private static JsonElement CloneJsonElement(JsonElement element)
    {
        using var document = JsonDocument.Parse(element.GetRawText());
        return document.RootElement.Clone();
    }

    private sealed record InstalledPluginRequirementGroup(string PluginCode, IReadOnlyList<InstalledPluginRequirement> Requirements);
    private sealed record InstalledPluginRequirement(string ResourceType, string UsageKey, bool Required, string Description);

    private sealed class UsageKeySequenceResolver
    {
        private readonly Dictionary<string, Queue<string>> _usageKeysByResourceType;

        public UsageKeySequenceResolver(IEnumerable<InstalledPluginRequirement> requirements)
        {
            _usageKeysByResourceType = requirements
                .GroupBy(requirement => requirement.ResourceType, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => new Queue<string>(group.Select(requirement => requirement.UsageKey).Where(usageKey => !string.IsNullOrWhiteSpace(usageKey))),
                    StringComparer.Ordinal);
        }

        public string Resolve(string resourceType, string fallbackUsageKey)
        {
            if (_usageKeysByResourceType.TryGetValue(resourceType, out var usageKeys) && usageKeys.Count > 0)
            {
                var usageKey = usageKeys.Dequeue();
                if (!string.IsNullOrWhiteSpace(usageKey))
                {
                    return usageKey;
                }
            }

            return fallbackUsageKey;
        }
    }

    private void PersistReleaseCenterIdentity()
    {
        var configPath = DigitalIntelligenceBridge.Configuration.ConfigurationExtensions.GetConfigFilePath();
        var userSettings = ConfigurationExtensions.CreateUserSettingsSnapshot(_appSettings);
        ConfigurationExtensions.SaveUserSettings(configPath, userSettings);
    }

    private string ResolveCacheDirectory() => !string.IsNullOrWhiteSpace(_config.CacheDirectory) ? _config.CacheDirectory : Path.Combine(ConfigurationExtensions.GetConfigRootDirectory(), "release-cache", "plugins", _config.Channel.Trim());
    private string ResolveClientCacheDirectory() => !string.IsNullOrWhiteSpace(_config.ClientCacheDirectory) ? _config.ClientCacheDirectory : Path.Combine(ConfigurationExtensions.GetConfigRootDirectory(), "release-cache", "clients", _config.Channel.Trim());
    private string ResolveStagingDirectory() => !string.IsNullOrWhiteSpace(_config.StagingDirectory) ? _config.StagingDirectory : Path.Combine(ConfigurationExtensions.GetConfigRootDirectory(), "release-staging", "plugins", _config.Channel.Trim());
    private string ResolveRuntimePluginRoot() => !string.IsNullOrWhiteSpace(_config.RuntimePluginRoot) ? _config.RuntimePluginRoot : ConfigurationExtensions.GetRuntimePluginsDirectory(_pluginConfig.PluginDirectory);
    private string ResolveBackupDirectory() => !string.IsNullOrWhiteSpace(_config.BackupDirectory) ? _config.BackupDirectory : ConfigurationExtensions.GetReleaseBackupsDirectory();
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
    private bool HasPluginUpdate(PluginItemDto plugin, IReadOnlyDictionary<string, string> installedVersions)
    {
        if (string.IsNullOrWhiteSpace(plugin.PluginId) || string.IsNullOrWhiteSpace(plugin.Version))
        {
            return true;
        }

        if (!installedVersions.TryGetValue(plugin.PluginId, out var installedVersion) || string.IsNullOrWhiteSpace(installedVersion))
        {
            return true;
        }

        return CompareVersions(plugin.Version, installedVersion) > 0;
    }

    private IReadOnlyDictionary<string, string> GetInstalledPluginVersions()
    {
        var runtimePluginRoot = ResolveRuntimePluginRoot();
        if (!Directory.Exists(runtimePluginRoot))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pluginJsonPath in Directory.GetFiles(runtimePluginRoot, "plugin.json", SearchOption.AllDirectories))
        {
            try
            {
                using var stream = File.OpenRead(pluginJsonPath);
                using var document = JsonDocument.Parse(stream);
                if (!document.RootElement.TryGetProperty("id", out var idProperty)
                    || !document.RootElement.TryGetProperty("version", out var versionProperty))
                {
                    continue;
                }

                var pluginId = idProperty.GetString();
                var version = versionProperty.GetString();
                if (string.IsNullOrWhiteSpace(pluginId) || string.IsNullOrWhiteSpace(version))
                {
                    continue;
                }

                if (!versions.TryGetValue(pluginId, out var current) || CompareVersions(version, current) > 0)
                {
                    versions[pluginId] = version;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "读取运行时插件版本失败: {PluginJsonPath}", pluginJsonPath);
            }
        }

        return versions;
    }

    private static string BuildClientSummary(ClientManifestDto? manifest) => manifest is null || string.IsNullOrWhiteSpace(manifest.LatestVersion) ? "客户端更新：暂无发布版本" : $"客户端最新版本：{manifest.LatestVersion}（最低升级版本：{manifest.MinUpgradeVersion ?? "未限制"}）";
    private static string BuildPluginSummary(PluginManifestDto? manifest) { var count = manifest?.Plugins?.Count ?? 0; if (count == 0) return "插件更新：暂无可用插件包"; var items = manifest!.Plugins!.Take(3).Select(item => $"{item.Name} {item.Version}"); return $"插件更新：{count} 个可用插件（{string.Join('、', items)}）"; }
    private static string BuildAuthorizedPluginSummary(PluginManifestDto? manifest) { var count = manifest?.Plugins?.Count ?? 0; return count == 0 ? "授权插件：当前站点未授权任何插件" : $"授权插件：{count} 个"; }
    private static string BuildAuthorizedPluginDetail(PluginManifestDto? manifest) { var items = manifest?.Plugins?.Select(item => $"{item.Name} / {item.PluginId} / {item.Version}") ?? Array.Empty<string>(); return string.Join(Environment.NewLine, items); }
    private static string BuildDownloadFileName(PluginItemDto plugin) { if (Uri.TryCreate(plugin.PackageUrl, UriKind.Absolute, out var uri)) { var fromUrl = Path.GetFileName(uri.LocalPath); if (!string.IsNullOrWhiteSpace(fromUrl)) return fromUrl; } var version = string.IsNullOrWhiteSpace(plugin.Version) ? "latest" : plugin.Version; return $"{plugin.PluginId}-{version}.zip"; }
    private static string BuildClientDownloadFileName(ClientManifestDto clientManifest) { if (Uri.TryCreate(clientManifest.PackageUrl, UriKind.Absolute, out var uri)) { var fromUrl = Path.GetFileName(uri.LocalPath); if (!string.IsNullOrWhiteSpace(fromUrl)) return fromUrl; } var version = string.IsNullOrWhiteSpace(clientManifest.LatestVersion) ? "latest" : clientManifest.LatestVersion; return $"dib-win-x64-portable-{version}.zip"; }
    private static void ValidateSha256(PluginItemDto plugin, byte[] bytes) { if (string.IsNullOrWhiteSpace(plugin.Sha256)) return; var computed = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(); if (!string.Equals(computed, plugin.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"插件 {plugin.PluginId} 的 SHA256 校验失败。"); }
    private static void ValidateSha256(PluginItemDto plugin, string computedHash) { if (string.IsNullOrWhiteSpace(plugin.Sha256)) return; if (!string.Equals(computedHash, plugin.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"插件 {plugin.PluginId} 的 SHA256 校验失败。"); }
    private static bool TryValidateCachedPluginPackage(PluginItemDto plugin, string targetPath)
    {
        if (!File.Exists(targetPath))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(plugin.Sha256))
        {
            return true;
        }

        var computed = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(targetPath))).ToLowerInvariant();
        return string.Equals(computed, plugin.Sha256, StringComparison.OrdinalIgnoreCase);
    }
    private static void ValidateClientSha256(ClientManifestDto clientManifest, string computedHash) { if (string.IsNullOrWhiteSpace(clientManifest.Sha256)) return; if (!string.Equals(computedHash, clientManifest.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("客户端更新包的 SHA256 校验失败。"); }
    private static string ReadPluginId(string pluginJsonPath) { using var stream = File.OpenRead(pluginJsonPath); using var document = JsonDocument.Parse(stream); if (!document.RootElement.TryGetProperty("id", out var idProperty) || string.IsNullOrWhiteSpace(idProperty.GetString())) throw new InvalidOperationException($"{pluginJsonPath} 缺少插件 id。"); return idProperty.GetString()!; }
    private static PluginUninstallMarker ReadPluginUninstallMarker(string markerPath) { using var stream = File.OpenRead(markerPath); return JsonSerializer.Deserialize<PluginUninstallMarker>(stream) ?? throw new InvalidOperationException($"{markerPath} 缺少卸载标记。"); }
    private static string PreparePluginZip(string zipFile, string stagingDirectory)
    {
        var packageName = Path.GetFileNameWithoutExtension(zipFile);
        var extractDirectory = ResolvePluginExtractDirectory(stagingDirectory, packageName);

        ZipFile.ExtractToDirectory(zipFile, extractDirectory);
        var pluginJsonPath = Directory.GetFiles(extractDirectory, "plugin.json", SearchOption.AllDirectories).FirstOrDefault();
        if (pluginJsonPath is null)
        {
            throw new InvalidOperationException($"缓存包 {Path.GetFileName(zipFile)} 缺少 plugin.json。");
        }

        var pluginId = ReadPluginId(pluginJsonPath);
        return $"{pluginId}: {extractDirectory}";
    }
    private static string ResolvePluginExtractDirectory(string stagingDirectory, string packageName)
    {
        var extractDirectory = Path.Combine(stagingDirectory, packageName);
        if (!Directory.Exists(extractDirectory))
        {
            return extractDirectory;
        }

        return Path.Combine(stagingDirectory, $"{packageName}-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}");
    }
    private static void CopyDirectory(string sourceDirectory, string targetDirectory) { Directory.CreateDirectory(targetDirectory); foreach (var file in Directory.GetFiles(sourceDirectory)) File.Copy(file, Path.Combine(targetDirectory, Path.GetFileName(file)), overwrite: true); foreach (var directory in Directory.GetDirectories(sourceDirectory)) CopyDirectory(directory, Path.Combine(targetDirectory, Path.GetFileName(directory))); }
    private static string[] NormalizeStagedPluginDirectories(string[] stagedDirectories)
    {
        var selected = new Dictionary<string, StagedPluginDirectory>(StringComparer.OrdinalIgnoreCase);
        var passthrough = new List<string>();

        foreach (var stagedDirectory in stagedDirectories)
        {
            var uninstallMarkerPath = Path.Combine(stagedDirectory, "uninstall.json");
            if (File.Exists(uninstallMarkerPath))
            {
                passthrough.Add(stagedDirectory);
                continue;
            }

            var pluginJsonPath = Directory.GetFiles(stagedDirectory, "plugin.json", SearchOption.AllDirectories).FirstOrDefault();
            if (pluginJsonPath is null)
            {
                passthrough.Add(stagedDirectory);
                continue;
            }

            var staged = ReadStagedPluginDirectory(stagedDirectory, pluginJsonPath);
            if (!selected.TryGetValue(staged.PluginId, out var current)
                || CompareVersions(staged.Version, current.Version) > 0)
            {
                if (current is not null && Directory.Exists(current.Directory))
                {
                    Directory.Delete(current.Directory, recursive: true);
                }

                selected[staged.PluginId] = staged;
            }
            else
            {
                Directory.Delete(stagedDirectory, recursive: true);
            }
        }

        return passthrough
            .Concat(selected.Values.Select(item => item.Directory))
            .ToArray();
    }

    private static StagedPluginDirectory ReadStagedPluginDirectory(string directory, string pluginJsonPath)
    {
        using var stream = File.OpenRead(pluginJsonPath);
        using var document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("id", out var idProperty) || string.IsNullOrWhiteSpace(idProperty.GetString()))
        {
            throw new InvalidOperationException($"{pluginJsonPath} 缺少插件 id。");
        }

        var version = document.RootElement.TryGetProperty("version", out var versionProperty)
            ? versionProperty.GetString() ?? "0.0.0"
            : "0.0.0";
        return new StagedPluginDirectory(directory, idProperty.GetString()!, version);
    }
    private static void TryDeleteFile(string? path) { if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) { File.Delete(path); } }
    public static int CompareVersions(string left, string right)
    {
        var leftParts = ParseVersionParts(left);
        var rightParts = ParseVersionParts(right);
        var max = Math.Max(leftParts.Length, rightParts.Length);
        for (var i = 0; i < max; i++)
        {
            var lv = i < leftParts.Length ? leftParts[i] : 0;
            var rv = i < rightParts.Length ? rightParts[i] : 0;
            if (lv != rv) return lv.CompareTo(rv);
        }

        return ComparePrereleaseVersions(left, right);
    }

    private static int[] ParseVersionParts(string value) => value.Split('-', 2)[0].Split('.', StringSplitOptions.RemoveEmptyEntries).Select(part => int.TryParse(part, out var parsed) ? parsed : 0).ToArray();
    private static int ComparePrereleaseVersions(string left, string right)
    {
        var leftPrerelease = ParsePrereleaseParts(left);
        var rightPrerelease = ParsePrereleaseParts(right);
        if (leftPrerelease.Length == 0 || rightPrerelease.Length == 0)
        {
            return 0;
        }

        var max = Math.Max(leftPrerelease.Length, rightPrerelease.Length);
        for (var i = 0; i < max; i++)
        {
            if (i >= leftPrerelease.Length) return -1;
            if (i >= rightPrerelease.Length) return 1;

            var leftToken = leftPrerelease[i];
            var rightToken = rightPrerelease[i];
            var leftIsNumber = int.TryParse(leftToken, out var leftNumber);
            var rightIsNumber = int.TryParse(rightToken, out var rightNumber);
            var comparison = leftIsNumber && rightIsNumber
                ? leftNumber.CompareTo(rightNumber)
                : string.Compare(leftToken, rightToken, StringComparison.OrdinalIgnoreCase);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }

    private static string[] ParsePrereleaseParts(string value)
    {
        var versionParts = value.Split('-', 2, StringSplitOptions.RemoveEmptyEntries);
        if (versionParts.Length < 2)
        {
            return Array.Empty<string>();
        }

        return versionParts[1]
            .Split('+', 2)[0]
            .Split('.', StringSplitOptions.RemoveEmptyEntries);
    }

    private sealed record SiteHeartbeatPayload(string SiteId, string SiteName, string Channel, string ClientVersion, string MachineName, DateTimeOffset CheckedAt);
    private sealed class ClientManifestDto { [JsonPropertyName("latestVersion")] public string? LatestVersion { get; set; } [JsonPropertyName("minUpgradeVersion")] public string? MinUpgradeVersion { get; set; } [JsonPropertyName("packageUrl")] public string? PackageUrl { get; set; } [JsonPropertyName("sha256")] public string? Sha256 { get; set; } }
    private sealed class PluginManifestDto { [JsonPropertyName("plugins")] public List<PluginItemDto>? Plugins { get; set; } }
    private sealed class PluginItemDto { [JsonPropertyName("pluginId")] public string PluginId { get; set; } = string.Empty; [JsonPropertyName("name")] public string Name { get; set; } = string.Empty; [JsonPropertyName("version")] public string Version { get; set; } = string.Empty; [JsonPropertyName("packageUrl")] public string? PackageUrl { get; set; } [JsonPropertyName("sha256")] public string? Sha256 { get; set; } }
    private sealed record PluginUninstallMarker([property: JsonPropertyName("pluginId")] string PluginId, [property: JsonPropertyName("requestedAt")] DateTimeOffset RequestedAt);
    private sealed record StagedPluginDirectory(string Directory, string PluginId, string Version);
}





