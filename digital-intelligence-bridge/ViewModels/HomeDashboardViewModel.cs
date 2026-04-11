using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Services;
using Microsoft.Extensions.Options;
using Prism.Commands;
using Prism.Mvvm;

namespace DigitalIntelligenceBridge.ViewModels;

public sealed class HomePluginStatusItem : BindableBase
{
    private string _name = string.Empty;
    private string _version = "未识别";
    private string _status = string.Empty;

    public string PluginId { get; init; } = string.Empty;
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string Version { get => _version; set => SetProperty(ref _version, value); }
    public string Status { get => _status; set => SetProperty(ref _status, value); }
}

public sealed class HomeDashboardViewModel : ViewModelBase
{
    private readonly ILoggerService<HomeDashboardViewModel> _logger;
    private readonly IApplicationService _applicationService;
    private readonly AppSettings _settings;
    private readonly IReleaseCenterService? _releaseCenterService;
    private readonly Action _openSettingsAction;

    private string _siteDisplayName = "未配置站点名称";
    private string _siteStatus = "请先在设置中填写站点名称。";
    private string _siteGroupDisplayName = "待同步";
    private string _siteGroupStatus = "当前客户端未获取分组信息。";
    private string _authorizedPluginCountText = "0 个";
    private string _authorizedPluginSummaryText = "当前站点未授权任何插件";
    private string _pendingActionTitle = "需要完善站点信息";
    private string _pendingActionDetail = "请先进入设置填写站点名称。";
    private string _releaseCenterStatusText = "未检查";
    private string _lastUpdateCheckStatus = "尚未执行";
    private string _lastInitializationStatus = "尚未执行";
    private string _lastPrepareStatus = "尚未执行";
    private bool _isBusy;

    public ObservableCollection<HomePluginStatusItem> PluginItems { get; } = new();

    public string AppVersion => _applicationService.GetVersion();
    public string ChannelLabel => MapChannelLabel(_settings.ReleaseCenter.Channel);
    public string SiteDisplayName { get => _siteDisplayName; private set => SetProperty(ref _siteDisplayName, value); }
    public string SiteStatus { get => _siteStatus; private set => SetProperty(ref _siteStatus, value); }
    public string SiteGroupDisplayName { get => _siteGroupDisplayName; private set => SetProperty(ref _siteGroupDisplayName, value); }
    public string SiteGroupStatus { get => _siteGroupStatus; private set => SetProperty(ref _siteGroupStatus, value); }
    public string AuthorizedPluginCountText { get => _authorizedPluginCountText; private set => SetProperty(ref _authorizedPluginCountText, value); }
    public string AuthorizedPluginSummaryText { get => _authorizedPluginSummaryText; private set => SetProperty(ref _authorizedPluginSummaryText, value); }
    public string PendingActionTitle { get => _pendingActionTitle; private set => SetProperty(ref _pendingActionTitle, value); }
    public string PendingActionDetail { get => _pendingActionDetail; private set => SetProperty(ref _pendingActionDetail, value); }
    public string ReleaseCenterStatusText { get => _releaseCenterStatusText; private set => SetProperty(ref _releaseCenterStatusText, value); }
    public string LastUpdateCheckStatus { get => _lastUpdateCheckStatus; private set => SetProperty(ref _lastUpdateCheckStatus, value); }
    public string LastInitializationStatus { get => _lastInitializationStatus; private set => SetProperty(ref _lastInitializationStatus, value); }
    public string LastPrepareStatus { get => _lastPrepareStatus; private set => SetProperty(ref _lastPrepareStatus, value); }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                InitializeSitePluginsCommand.RaiseCanExecuteChanged();
                CheckUpdatesCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public DelegateCommand InitializeSitePluginsCommand { get; }
    public DelegateCommand CheckUpdatesCommand { get; }
    public DelegateCommand OpenSettingsCommand { get; }
    public DelegateCommand RestartApplicationCommand { get; }

    public HomeDashboardViewModel()
        : this(
            new NullLoggerService(),
            new DesignApplicationService(),
            Options.Create(new AppSettings()),
            null,
            () => { })
    {
    }

    public HomeDashboardViewModel(
        ILoggerService<HomeDashboardViewModel> logger,
        IApplicationService applicationService,
        IOptions<AppSettings> appSettings,
        IReleaseCenterService? releaseCenterService,
        Action openSettingsAction)
    {
        _logger = logger;
        _applicationService = applicationService;
        _settings = appSettings.Value;
        _releaseCenterService = releaseCenterService;
        _openSettingsAction = openSettingsAction;

        InitializeSitePluginsCommand = new DelegateCommand(() => _ = InitializeSitePluginsAsync(), () => !IsBusy);
        CheckUpdatesCommand = new DelegateCommand(() => _ = RefreshAsync(), () => !IsBusy);
        OpenSettingsCommand = new DelegateCommand(() => _openSettingsAction());
        RestartApplicationCommand = new DelegateCommand(() => _applicationService.RestartApplication());

        RefreshLocalState(Array.Empty<AuthorizedPluginState>());
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var authorizedPlugins = Array.Empty<AuthorizedPluginState>();
            RefreshLocalState(authorizedPlugins);

            if (_releaseCenterService is null || !_releaseCenterService.IsConfigured)
            {
                ReleaseCenterStatusText = "发布中心未配置";
                LastUpdateCheckStatus = "尚未执行";
                return;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(8));
            var result = await _releaseCenterService.CheckForUpdatesAsync(cts.Token);
            authorizedPlugins = ParseAuthorizedPluginStates(result.AuthorizedPluginDetail);
            ReleaseCenterStatusText = result.IsSuccess ? "发布中心已连接" : "发布中心检查失败";
            LastUpdateCheckStatus = FormatStatusTimestamp(DateTime.Now, result.Summary);
            SiteStatus = result.SiteSummary;
            AuthorizedPluginCountText = $"{authorizedPlugins.Length} 个";
            AuthorizedPluginSummaryText = authorizedPlugins.Length == 0
                ? "当前站点未授权任何插件"
                : string.Join('、', authorizedPlugins.Take(3).Select(item => item.Name)) + (authorizedPlugins.Length > 3 ? $" 等 {authorizedPlugins.Length} 个" : string.Empty);
            RefreshLocalState(authorizedPlugins);
        }
        catch (Exception ex)
        {
            ReleaseCenterStatusText = "发布中心检查失败";
            LastUpdateCheckStatus = FormatStatusTimestamp(DateTime.Now, "检查更新失败");
            PendingActionTitle = "需要排查";
            PendingActionDetail = ex.Message;
            _logger.LogWarning("首页刷新发布中心状态失败: {Message}", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task InitializeSitePluginsAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.ReleaseCenter.SiteName))
        {
            PendingActionTitle = "需要完善站点信息";
            PendingActionDetail = "请先进入设置填写站点名称。";
            return;
        }

        IsBusy = true;
        try
        {
            var authorizedPlugins = Array.Empty<AuthorizedPluginState>();
            if (_releaseCenterService is not null && _releaseCenterService.IsConfigured)
            {
                using var checkCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                var checkResult = await _releaseCenterService.CheckForUpdatesAsync(checkCts.Token);
                authorizedPlugins = ParseAuthorizedPluginStates(checkResult.AuthorizedPluginDetail);
                ReleaseCenterStatusText = checkResult.IsSuccess ? "发布中心已连接" : "发布中心检查失败";
                LastUpdateCheckStatus = FormatStatusTimestamp(DateTime.Now, checkResult.Summary);
                SiteStatus = checkResult.SiteSummary;
                AuthorizedPluginCountText = $"{authorizedPlugins.Length} 个";
                AuthorizedPluginSummaryText = authorizedPlugins.Length == 0
                    ? "当前站点未授权任何插件"
                    : string.Join('、', authorizedPlugins.Take(3).Select(item => item.Name)) + (authorizedPlugins.Length > 3 ? $" 等 {authorizedPlugins.Length} 个" : string.Empty);
            }

            if (_releaseCenterService is null)
            {
                LastInitializationStatus = "初始化失败";
                PendingActionTitle = "需要排查";
                PendingActionDetail = "未注册发布中心服务。";
                return;
            }

            using var downloadCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var downloadResult = await _releaseCenterService.DownloadAvailablePluginPackagesAsync(downloadCts.Token);
            if (!downloadResult.IsSuccess)
            {
                LastInitializationStatus = "初始化失败";
                PendingActionTitle = "需要排查";
                PendingActionDetail = downloadResult.Detail;
                return;
            }

            using var prepareCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var prepareResult = await _releaseCenterService.PrepareCachedPluginPackagesAsync(prepareCts.Token);
            LastPrepareStatus = FormatStatusTimestamp(DateTime.Now, prepareResult.Summary);
            LastInitializationStatus = prepareResult.IsSuccess ? "初始化完成" : "初始化失败";
            RefreshLocalState(authorizedPlugins);
            if (prepareResult.IsSuccess && prepareResult.PreparedCount > 0)
            {
                PendingActionTitle = "需要重启";
                PendingActionDetail = "插件已预安装，重启 DIB 后生效。";
            }
        }
        catch (Exception ex)
        {
            LastInitializationStatus = "初始化失败";
            PendingActionTitle = "需要排查";
            PendingActionDetail = ex.Message;
            _logger.LogError(ex, "首页执行初始化本机插件失败");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshLocalState(IReadOnlyCollection<AuthorizedPluginState> authorizedPlugins)
    {
        SiteDisplayName = string.IsNullOrWhiteSpace(_settings.ReleaseCenter.SiteName)
            ? "未配置站点名称"
            : _settings.ReleaseCenter.SiteName.Trim();
        SiteStatus = string.IsNullOrWhiteSpace(_settings.ReleaseCenter.SiteName)
            ? "请先进入设置完善站点信息。"
            : string.IsNullOrWhiteSpace(_settings.ReleaseCenter.SiteRemark)
                ? "站点信息已登记。"
                : _settings.ReleaseCenter.SiteRemark.Trim();
        SiteGroupDisplayName = "待同步";
        SiteGroupStatus = "当前客户端未获取分组信息。";

        var runtimePlugins = ReadLocalPluginStates(ResolveRuntimePluginRoot(), "已生效");
        var stagingPlugins = ReadLocalPluginStates(ResolveStagingDirectory(), "待重启生效");
        var merged = MergePluginStates(runtimePlugins, stagingPlugins, authorizedPlugins);

        PluginItems.Clear();
        foreach (var item in merged)
        {
            PluginItems.Add(item);
        }

        if (authorizedPlugins.Count > 0)
        {
            AuthorizedPluginCountText = $"{authorizedPlugins.Count} 个";
            AuthorizedPluginSummaryText = string.Join('、', authorizedPlugins.Take(3).Select(item => item.Name))
                + (authorizedPlugins.Count > 3 ? $" 等 {authorizedPlugins.Count} 个" : string.Empty);
        }
        else
        {
            AuthorizedPluginCountText = "0 个";
            AuthorizedPluginSummaryText = "当前站点未授权任何插件";
        }

        var hasPendingRestart = merged.Any(item => string.Equals(item.Status, "待重启生效", StringComparison.Ordinal));
        var hasAuthorizedNotInstalled = merged.Any(item => string.Equals(item.Status, "已授权未安装", StringComparison.Ordinal));

        if (string.IsNullOrWhiteSpace(_settings.ReleaseCenter.SiteName))
        {
            PendingActionTitle = "需要完善站点信息";
            PendingActionDetail = "请先进入设置填写站点名称。";
        }
        else if (hasPendingRestart)
        {
            PendingActionTitle = "需要重启";
            PendingActionDetail = "插件已预安装，重启 DIB 后生效。";
        }
        else if (hasAuthorizedNotInstalled)
        {
            PendingActionTitle = "需要初始化";
            PendingActionDetail = "当前站点存在已授权但未安装插件，建议执行初始化本机插件。";
        }
        else
        {
            PendingActionTitle = "无需操作";
            PendingActionDetail = "当前终端已就绪。";
        }
    }

    private string ResolveRuntimePluginRoot()
    {
        if (!string.IsNullOrWhiteSpace(_settings.ReleaseCenter.RuntimePluginRoot))
        {
            return _settings.ReleaseCenter.RuntimePluginRoot;
        }

        return ConfigurationExtensions.GetRuntimePluginsDirectory(_settings.Plugin.PluginDirectory);
    }

    private string ResolveStagingDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_settings.ReleaseCenter.StagingDirectory))
        {
            return _settings.ReleaseCenter.StagingDirectory;
        }

        return Path.Combine(
            ConfigurationExtensions.GetConfigRootDirectory(),
            "release-staging",
            "plugins",
            _settings.ReleaseCenter.Channel.Trim());
    }

    private static IReadOnlyList<HomePluginStatusItem> MergePluginStates(
        IReadOnlyCollection<LocalPluginState> runtimePlugins,
        IReadOnlyCollection<LocalPluginState> stagingPlugins,
        IReadOnlyCollection<AuthorizedPluginState> authorizedPlugins)
    {
        var merged = new Dictionary<string, HomePluginStatusItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var plugin in runtimePlugins)
        {
            merged[plugin.PluginId] = new HomePluginStatusItem
            {
                PluginId = plugin.PluginId,
                Name = plugin.Name,
                Version = plugin.Version,
                Status = "已生效"
            };
        }

        foreach (var plugin in stagingPlugins)
        {
            merged[plugin.PluginId] = new HomePluginStatusItem
            {
                PluginId = plugin.PluginId,
                Name = plugin.Name,
                Version = plugin.Version,
                Status = "待重启生效"
            };
        }

        foreach (var plugin in authorizedPlugins)
        {
            if (merged.ContainsKey(plugin.PluginId))
            {
                continue;
            }

            merged[plugin.PluginId] = new HomePluginStatusItem
            {
                PluginId = plugin.PluginId,
                Name = plugin.Name,
                Version = plugin.Version,
                Status = "已授权未安装"
            };
        }

        return merged.Values
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static AuthorizedPluginState[] ParseAuthorizedPluginStates(string detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return [];
        }

        return detail
            .Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line =>
            {
                var parts = line.Split(" / ", StringSplitOptions.TrimEntries);
                if (parts.Length < 3)
                {
                    return null;
                }

                return new AuthorizedPluginState(parts[1], parts[0], parts[2]);
            })
            .Where(item => item is not null)
            .Cast<AuthorizedPluginState>()
            .ToArray();
    }

    private static IReadOnlyList<LocalPluginState> ReadLocalPluginStates(string rootDirectory, string status)
    {
        if (!Directory.Exists(rootDirectory))
        {
            return [];
        }

        var items = new List<LocalPluginState>();
        foreach (var pluginJsonPath in Directory.GetFiles(rootDirectory, "plugin.json", SearchOption.AllDirectories))
        {
            try
            {
                using var stream = File.OpenRead(pluginJsonPath);
                using var document = JsonDocument.Parse(stream);
                var root = document.RootElement;
                var pluginId = root.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
                if (string.IsNullOrWhiteSpace(pluginId))
                {
                    continue;
                }

                var name = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : pluginId;
                var version = root.TryGetProperty("version", out var versionElement) ? versionElement.GetString() : "未识别";
                items.Add(new LocalPluginState(pluginId!, name ?? pluginId!, version ?? "未识别", status));
            }
            catch
            {
                // 忽略损坏的插件描述文件，避免首页状态加载失败。
            }
        }

        return items
            .GroupBy(item => item.PluginId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static string FormatStatusTimestamp(DateTime timestamp, string summary)
        => $"{timestamp:yyyy-MM-dd HH:mm:ss} · {summary}";

    private static string MapChannelLabel(string channel)
        => string.Equals(channel, "stable", StringComparison.OrdinalIgnoreCase) ? "稳定版" : channel;

    private sealed record AuthorizedPluginState(string PluginId, string Name, string Version);
    private sealed record LocalPluginState(string PluginId, string Name, string Version, string Status);

    private sealed class NullLoggerService : ILoggerService<HomeDashboardViewModel>
    {
        public void LogCritical(string message, params object[] args) { }
        public void LogDebug(string message, params object[] args) { }
        public void LogError(string message, params object[] args) { }
        public void LogError(Exception exception, string message, params object[] args) { }
        public void LogInformation(string message, params object[] args) { }
        public void LogWarning(string message, params object[] args) { }
    }

    private sealed class DesignApplicationService : IApplicationService
    {
        public bool IsInitialized => true;
        public Task InitializeAsync() => Task.CompletedTask;
        public Task OnStartedAsync() => Task.CompletedTask;
        public Task OnShutdownAsync() => Task.CompletedTask;
        public string GetVersion() => "1.0.0";
        public string GetApplicationName() => "通用工具箱";
        public void RestartApplication() { }
    }
}
