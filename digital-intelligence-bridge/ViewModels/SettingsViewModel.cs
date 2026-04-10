using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Services;
using Microsoft.Extensions.Options;
using Prism.Commands;

namespace DigitalIntelligenceBridge.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly IApplicationService _appService;
    private readonly ITrayService _trayService;
    private readonly ILoggerService<SettingsViewModel> _logger;
    private readonly AppSettings _settings;
    private readonly ISupabaseService? _supabaseService;
    private readonly IReleaseCenterService? _releaseCenterService;
    private bool _isDarkMode;
    private bool _startMinimizedToTray;
    private bool _startWithSystem;
    private bool _showTrayNotifications;
    private string _selectedTrayIcon = "默认图标";
    private int _autoSaveInterval = 5;
    private string _appName = "通用工具箱";
    private string _appVersion = "1.0.0";
    private string _selfCheckSummary = "尚未执行自检";
    private string _selfCheckNotice = string.Empty;
    private DateTime? _lastSelfCheckAt;
    private bool _isSelfCheckRunning;
    private string _updateCheckSummary = "尚未检查更新";
    private string _clientUpdateSummary = "客户端更新：尚未检查";
    private string _pluginUpdateSummary = "插件更新：尚未检查";
    private string _updateCheckDetail = string.Empty;
    private string _siteAuthorizationSummary = "站点：尚未检查";
    private string _authorizedPluginSummary = "授权插件：尚未检查";
    private string _authorizedPluginDetail = string.Empty;
    private DateTime? _lastUpdateCheckAt;
    private bool _isUpdateCheckRunning;
    private string _clientPackageDownloadSummary = "客户端升级包：尚未执行";
    private string _clientPackageDownloadDetail = string.Empty;
    private DateTime? _lastClientPackageDownloadAt;
    private bool _isClientPackageDownloadRunning;
    private string _clientUpgradeNotice = string.Empty;
    private string _pluginDownloadSummary = "插件包下载：尚未执行";
    private string _pluginDownloadDetail = string.Empty;
    private DateTime? _lastPluginDownloadAt;
    private bool _isPluginDownloadRunning;
    private string _pluginPrepareSummary = "插件预安装：尚未执行";
    private string _pluginPrepareDetail = string.Empty;
    private DateTime? _lastPluginPrepareAt;
    private bool _isPluginPrepareRunning;
    private string _pluginRollbackSummary = "插件回滚：尚未执行";
    private string _pluginRollbackDetail = string.Empty;
    private DateTime? _lastPluginRollbackAt;
    private bool _isPluginRollbackRunning;
    private string _restartRequiredNotice = string.Empty;
    private string _siteNameInput = string.Empty;
    private string _siteRemarkInput = string.Empty;
    private string _siteProfileSummary = "站点信息：未填写";
    private string _siteProfileStatus = string.Empty;
    private string _siteInitializationSummary = "初始化本机插件：尚未执行";
    private string _siteInitializationDetail = string.Empty;
    private bool _isSiteInitializationRunning;

    public ObservableCollection<SelfCheckItem> SelfCheckItems { get; } = new();

    public bool IsDarkMode { get => _isDarkMode; set { if (SetProperty(ref _isDarkMode, value)) ApplyTheme(value); } }
    public bool StartMinimizedToTray { get => _startMinimizedToTray; set => SetProperty(ref _startMinimizedToTray, value); }
    public bool StartWithSystem { get => _startWithSystem; set => SetProperty(ref _startWithSystem, value); }
    public bool ShowTrayNotifications { get => _showTrayNotifications; set { if (SetProperty(ref _showTrayNotifications, value)) _trayService?.SetShowNotifications(value); } }
    public string SelectedTrayIcon { get => _selectedTrayIcon; set => SetProperty(ref _selectedTrayIcon, value); }
    public int AutoSaveInterval { get => _autoSaveInterval; set => SetProperty(ref _autoSaveInterval, value); }
    public string AppName { get => _appName; set => SetProperty(ref _appName, value); }
    public string AppVersion { get => _appVersion; set => SetProperty(ref _appVersion, value); }
    public string SelfCheckSummary { get => _selfCheckSummary; set => SetProperty(ref _selfCheckSummary, value); }
    public string SelfCheckNotice { get => _selfCheckNotice; set => SetProperty(ref _selfCheckNotice, value); }
    public DateTime? LastSelfCheckAt { get => _lastSelfCheckAt; set => SetProperty(ref _lastSelfCheckAt, value); }
    public string UpdateCheckSummary { get => _updateCheckSummary; set => SetProperty(ref _updateCheckSummary, value); }
    public string ClientUpdateSummary { get => _clientUpdateSummary; set => SetProperty(ref _clientUpdateSummary, value); }
    public string PluginUpdateSummary { get => _pluginUpdateSummary; set => SetProperty(ref _pluginUpdateSummary, value); }
    public string UpdateCheckDetail { get => _updateCheckDetail; set => SetProperty(ref _updateCheckDetail, value); }
    public string SiteAuthorizationSummary { get => _siteAuthorizationSummary; set => SetProperty(ref _siteAuthorizationSummary, value); }
    public string AuthorizedPluginSummary { get => _authorizedPluginSummary; set => SetProperty(ref _authorizedPluginSummary, value); }
    public string AuthorizedPluginDetail { get => _authorizedPluginDetail; set => SetProperty(ref _authorizedPluginDetail, value); }
    public string ClientPackageDownloadSummary { get => _clientPackageDownloadSummary; set => SetProperty(ref _clientPackageDownloadSummary, value); }
    public string ClientPackageDownloadDetail { get => _clientPackageDownloadDetail; set => SetProperty(ref _clientPackageDownloadDetail, value); }
    public DateTime? LastClientPackageDownloadAt { get => _lastClientPackageDownloadAt; set => SetProperty(ref _lastClientPackageDownloadAt, value); }
    public string ClientUpgradeNotice { get => _clientUpgradeNotice; set => SetProperty(ref _clientUpgradeNotice, value); }
    public DateTime? LastUpdateCheckAt { get => _lastUpdateCheckAt; set => SetProperty(ref _lastUpdateCheckAt, value); }
    public string PluginDownloadSummary { get => _pluginDownloadSummary; set => SetProperty(ref _pluginDownloadSummary, value); }
    public string PluginDownloadDetail { get => _pluginDownloadDetail; set => SetProperty(ref _pluginDownloadDetail, value); }
    public DateTime? LastPluginDownloadAt { get => _lastPluginDownloadAt; set => SetProperty(ref _lastPluginDownloadAt, value); }
    public string PluginPrepareSummary { get => _pluginPrepareSummary; set => SetProperty(ref _pluginPrepareSummary, value); }
    public string PluginPrepareDetail { get => _pluginPrepareDetail; set => SetProperty(ref _pluginPrepareDetail, value); }
    public DateTime? LastPluginPrepareAt { get => _lastPluginPrepareAt; set => SetProperty(ref _lastPluginPrepareAt, value); }
    public string PluginRollbackSummary { get => _pluginRollbackSummary; set => SetProperty(ref _pluginRollbackSummary, value); }
    public string PluginRollbackDetail { get => _pluginRollbackDetail; set => SetProperty(ref _pluginRollbackDetail, value); }
    public DateTime? LastPluginRollbackAt { get => _lastPluginRollbackAt; set => SetProperty(ref _lastPluginRollbackAt, value); }
    public string RestartRequiredNotice { get => _restartRequiredNotice; set => SetProperty(ref _restartRequiredNotice, value); }
    public string SiteNameInput
    {
        get => _siteNameInput;
        set
        {
            if (SetProperty(ref _siteNameInput, value))
            {
                RaisePropertyChanged(nameof(CanInitializeSitePlugins));
                SaveSiteProfileCommand?.RaiseCanExecuteChanged();
                InitializeSitePluginsCommand?.RaiseCanExecuteChanged();
            }
        }
    }
    public string SiteRemarkInput
    {
        get => _siteRemarkInput;
        set => SetProperty(ref _siteRemarkInput, value);
    }
    public string SiteProfileSummary { get => _siteProfileSummary; set => SetProperty(ref _siteProfileSummary, value); }
    public string SiteProfileStatus { get => _siteProfileStatus; set => SetProperty(ref _siteProfileStatus, value); }
    public string SiteInitializationSummary { get => _siteInitializationSummary; set => SetProperty(ref _siteInitializationSummary, value); }
    public string SiteInitializationDetail { get => _siteInitializationDetail; set => SetProperty(ref _siteInitializationDetail, value); }
    public bool CanInitializeSitePlugins => !string.IsNullOrWhiteSpace(SiteNameInput);

    public bool IsSelfCheckRunning
    {
        get => _isSelfCheckRunning;
        private set
        {
            if (SetProperty(ref _isSelfCheckRunning, value))
            {
                RaisePropertyChanged(nameof(RunSelfCheckButtonText));
                RunSelfCheckCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsUpdateCheckRunning
    {
        get => _isUpdateCheckRunning;
        private set
        {
            if (SetProperty(ref _isUpdateCheckRunning, value))
            {
                RaisePropertyChanged(nameof(CheckUpdateButtonText));
                CheckUpdateCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsClientPackageDownloadRunning
    {
        get => _isClientPackageDownloadRunning;
        private set
        {
            if (SetProperty(ref _isClientPackageDownloadRunning, value))
            {
                RaisePropertyChanged(nameof(DownloadClientPackageButtonText));
                DownloadClientPackageCommand.RaiseCanExecuteChanged();
            }
        }
    }
    public bool IsPluginDownloadRunning
    {
        get => _isPluginDownloadRunning;
        private set
        {
            if (SetProperty(ref _isPluginDownloadRunning, value))
            {
                RaisePropertyChanged(nameof(DownloadPluginPackagesButtonText));
                DownloadPluginPackagesCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsPluginPrepareRunning
    {
        get => _isPluginPrepareRunning;
        private set
        {
            if (SetProperty(ref _isPluginPrepareRunning, value))
            {
                RaisePropertyChanged(nameof(PreparePluginPackagesButtonText));
                PreparePluginPackagesCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsPluginRollbackRunning
    {
        get => _isPluginRollbackRunning;
        private set
        {
            if (SetProperty(ref _isPluginRollbackRunning, value))
            {
                RaisePropertyChanged(nameof(RestoreLatestPluginBackupButtonText));
                RestoreLatestPluginBackupCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsSiteInitializationRunning
    {
        get => _isSiteInitializationRunning;
        private set
        {
            if (SetProperty(ref _isSiteInitializationRunning, value))
            {
                RaisePropertyChanged(nameof(InitializeSitePluginsButtonText));
                RaisePropertyChanged(nameof(CanInitializeSitePlugins));
                InitializeSitePluginsCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    public string RunSelfCheckButtonText => IsSelfCheckRunning ? "执行中..." : "运行自检";
    public string CheckUpdateButtonText => IsUpdateCheckRunning ? "检查中..." : "检查更新";
    public string DownloadClientPackageButtonText => IsClientPackageDownloadRunning ? "下载中..." : "下载客户端更新包";
    public string DownloadPluginPackagesButtonText => IsPluginDownloadRunning ? "下载中..." : "下载插件包";
    public string PreparePluginPackagesButtonText => IsPluginPrepareRunning ? "准备中..." : "生成预安装目录";
    public string RestoreLatestPluginBackupButtonText => IsPluginRollbackRunning ? "回滚中..." : "回滚最近备份";
    public string InitializeSitePluginsButtonText => IsSiteInitializationRunning ? "初始化中..." : "初始化本机插件";

    public DelegateCommand SaveSettingsCommand { get; }
    public DelegateCommand SaveSiteProfileCommand { get; }
    public DelegateCommand ExportDataCommand { get; }
    public DelegateCommand ImportDataCommand { get; }
    public DelegateCommand ClearAllDataCommand { get; }
    public DelegateCommand CheckUpdateCommand { get; }
    public DelegateCommand DownloadClientPackageCommand { get; }
    public DelegateCommand DownloadPluginPackagesCommand { get; }
    public DelegateCommand PreparePluginPackagesCommand { get; }
    public DelegateCommand RestoreLatestPluginBackupCommand { get; }
    public DelegateCommand InitializeSitePluginsCommand { get; }
    public DelegateCommand OpenLogFolderCommand { get; }
    public DelegateCommand RunSelfCheckCommand { get; }
    public DelegateCommand ExportSelfCheckReportCommand { get; }

    public SettingsViewModel(IApplicationService appService, ITrayService trayService, ILoggerService<SettingsViewModel> logger, IOptions<AppSettings> settings, ISupabaseService? supabaseService = null, IReleaseCenterService? releaseCenterService = null)
    {
        _appService = appService;
        _trayService = trayService;
        _logger = logger;
        _settings = settings.Value;
        _supabaseService = supabaseService;
        _releaseCenterService = releaseCenterService;
        LoadSettings();
        SaveSettingsCommand = new DelegateCommand(SaveSettings);
        SaveSiteProfileCommand = new DelegateCommand(SaveSiteProfile, () => !string.IsNullOrWhiteSpace(SiteNameInput));
        ExportDataCommand = new DelegateCommand(ExportData);
        ImportDataCommand = new DelegateCommand(ImportData);
        ClearAllDataCommand = new DelegateCommand(ClearAllData);
        CheckUpdateCommand = new DelegateCommand(() => _ = CheckUpdateAsync(), () => !IsUpdateCheckRunning);
        DownloadClientPackageCommand = new DelegateCommand(() => _ = DownloadClientPackageAsync(), () => !IsClientPackageDownloadRunning);
        DownloadPluginPackagesCommand = new DelegateCommand(() => _ = DownloadPluginPackagesAsync(), () => !IsPluginDownloadRunning);
        PreparePluginPackagesCommand = new DelegateCommand(() => _ = PreparePluginPackagesAsync(), () => !IsPluginPrepareRunning);
        RestoreLatestPluginBackupCommand = new DelegateCommand(() => _ = RestoreLatestPluginBackupAsync(), () => !IsPluginRollbackRunning);
        InitializeSitePluginsCommand = new DelegateCommand(() => _ = InitializeSitePluginsAsync(), () => !IsSiteInitializationRunning && CanInitializeSitePlugins);
        OpenLogFolderCommand = new DelegateCommand(OpenLogFolder);
        RunSelfCheckCommand = new DelegateCommand(() => _ = RunSelfCheckAsync(), () => !IsSelfCheckRunning);
        ExportSelfCheckReportCommand = new DelegateCommand(ExportSelfCheckReport);
        _ = RunSelfCheckAsync();
        _logger.LogInformation("设置页面已初始化");
    }

    private void LoadSettings()
    {
        IsDarkMode = false;
        StartMinimizedToTray = _settings.Application.MinimizeToTray;
        StartWithSystem = _settings.Application.StartWithSystem;
        ShowTrayNotifications = _settings.Tray.ShowNotifications;
        AutoSaveInterval = 5;
        AppName = _settings.Application.Name;
        AppVersion = _settings.Application.Version;
        SiteNameInput = _settings.ReleaseCenter.SiteName;
        SiteRemarkInput = _settings.ReleaseCenter.SiteRemark;
        SiteProfileSummary = string.IsNullOrWhiteSpace(SiteNameInput)
            ? "站点信息：未填写"
            : $"站点信息：{SiteNameInput}";
    }

    private void ApplyTheme(bool isDarkMode)
    {
        var app = Application.Current;
        if (app != null)
        {
            app.RequestedThemeVariant = isDarkMode ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
        }
        _logger.LogInformation($"主题已切换为: {(isDarkMode ? "深色" : "浅色")}");
    }

    private void SaveSettings()
    {
        try
        {
            _settings.Application.MinimizeToTray = StartMinimizedToTray;
            _settings.Application.StartWithSystem = StartWithSystem;
            _settings.Tray.ShowNotifications = ShowTrayNotifications;
            _logger.LogInformation("设置已保存");
        }
        catch (Exception ex)
        {
            _logger.LogError($"保存设置失败: {ex}");
        }
    }

    private void SaveSiteProfile()
    {
        var siteName = SiteNameInput.Trim();
        if (string.IsNullOrWhiteSpace(siteName))
        {
            SiteProfileStatus = "请先填写站点名称。";
            return;
        }

        _settings.ReleaseCenter.SiteName = siteName;
        _settings.ReleaseCenter.SiteRemark = SiteRemarkInput.Trim();
        PersistAppSettings();
        SiteProfileSummary = $"站点信息：{_settings.ReleaseCenter.SiteName}";
        SiteProfileStatus = "站点信息已保存，后续站点注册与心跳将使用新的站点名称。";
    }

    private void ExportData() => _logger.LogInformation("导出数据...");
    private void ImportData() => _logger.LogInformation("导入数据...");
    private void ClearAllData() => _logger.LogInformation("清除所有数据...");

    private async Task CheckUpdateAsync()
    {
        if (IsUpdateCheckRunning) return;
        IsUpdateCheckRunning = true;
        UpdateCheckSummary = "正在检查更新...";
        UpdateCheckDetail = string.Empty;
        try
        {
            if (_releaseCenterService is null)
            {
                UpdateCheckSummary = "检查更新不可用";
                ClientUpdateSummary = "客户端更新：未注册服务";
                PluginUpdateSummary = "插件更新：未注册服务";
                UpdateCheckDetail = "IReleaseCenterService 未注册。";
                SiteAuthorizationSummary = "站点：未注册服务";
                AuthorizedPluginSummary = "授权插件：未注册服务";
                AuthorizedPluginDetail = string.Empty;
                return;
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var result = await _releaseCenterService.CheckForUpdatesAsync(cts.Token);
            UpdateCheckSummary = result.Summary;
            ClientUpdateSummary = result.ClientSummary;
            PluginUpdateSummary = result.PluginSummary;
            UpdateCheckDetail = result.Detail;
            SiteAuthorizationSummary = result.SiteSummary;
            AuthorizedPluginSummary = result.AuthorizedPluginSummary;
            AuthorizedPluginDetail = result.AuthorizedPluginDetail;
            LastUpdateCheckAt = DateTime.Now;
        }
        catch (Exception ex)
        {
            UpdateCheckSummary = "检查更新失败";
            ClientUpdateSummary = "客户端更新：失败";
            PluginUpdateSummary = "插件更新：失败";
            UpdateCheckDetail = ex.Message;
            SiteAuthorizationSummary = "站点：检查失败";
            AuthorizedPluginSummary = "授权插件：检查失败";
            AuthorizedPluginDetail = string.Empty;
            _logger.LogError(ex, "检查发布中心更新失败");
        }
        finally
        {
            IsUpdateCheckRunning = false;
        }
    }

    private async Task DownloadClientPackageAsync()
    {
        if (IsClientPackageDownloadRunning) return;
        IsClientPackageDownloadRunning = true;
        ClientPackageDownloadSummary = "正在下载客户端更新包...";
        ClientPackageDownloadDetail = string.Empty;
        try
        {
            if (_releaseCenterService is null)
            {
                ClientPackageDownloadSummary = "客户端下载：未注册服务";
                ClientPackageDownloadDetail = "IReleaseCenterService 未注册。";
                return;
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            var progress = new Progress<ReleaseCenterDownloadProgress>(item =>
            {
                ClientPackageDownloadSummary = item.Stage switch
                {
                    "verifying" => "正在校验客户端更新包...",
                    "completed" => "客户端更新包下载完成，正在收尾...",
                    "failed" => "客户端下载失败",
                    _ => "正在下载客户端更新包..."
                };
                ClientPackageDownloadDetail = BuildClientDownloadDetail(item);
            });
            var result = await _releaseCenterService.DownloadLatestClientPackageAsync(progress, cts.Token);
            ClientPackageDownloadSummary = result.Summary;
            if (result.IsSuccess)
            {
                ClientPackageDownloadDetail = string.IsNullOrWhiteSpace(ClientPackageDownloadDetail)
                    ? (string.IsNullOrWhiteSpace(result.Detail) ? result.CacheDirectory : result.Detail)
                    : ClientPackageDownloadDetail + Environment.NewLine + $"文件：{result.PackagePath}";
            }
            else
            {
                ClientPackageDownloadDetail = string.IsNullOrWhiteSpace(result.Detail) ? result.CacheDirectory : result.Detail;
            }
            LastClientPackageDownloadAt = DateTime.Now;
            if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.PackagePath))
            {
                ClientUpgradeNotice = "客户端升级包已就绪，请退出 DIB 后执行升级。";
            }
        }
        catch (Exception ex)
        {
            ClientPackageDownloadSummary = "客户端下载失败";
            ClientPackageDownloadDetail = ex.Message;
            _logger.LogError(ex, "下载客户端更新包失败");
        }
        finally
        {
            IsClientPackageDownloadRunning = false;
        }
    }
    private async Task DownloadPluginPackagesAsync()
    {
        if (IsPluginDownloadRunning) return;
        IsPluginDownloadRunning = true;
        PluginDownloadSummary = "正在下载插件包...";
        PluginDownloadDetail = string.Empty;
        try
        {
            if (_releaseCenterService is null)
            {
                PluginDownloadSummary = "插件包下载：未注册服务";
                PluginDownloadDetail = "IReleaseCenterService 未注册。";
                return;
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await _releaseCenterService.DownloadAvailablePluginPackagesAsync(cts.Token);
            PluginDownloadSummary = result.Summary;
            PluginDownloadDetail = string.IsNullOrWhiteSpace(result.Detail) ? result.CacheDirectory : result.Detail;
            LastPluginDownloadAt = DateTime.Now;
            if (result.IsSuccess && result.DownloadedCount > 0)
            {
                RestartRequiredNotice = "已有插件更新就绪，重启 DIB 后生效。";
            }
        }
        catch (Exception ex)
        {
            PluginDownloadSummary = "插件包下载失败";
            PluginDownloadDetail = ex.Message;
            _logger.LogError(ex, "下载插件包失败");
        }
        finally
        {
            IsPluginDownloadRunning = false;
        }
    }

    private async Task PreparePluginPackagesAsync()
    {
        if (IsPluginPrepareRunning) return;
        IsPluginPrepareRunning = true;
        PluginPrepareSummary = "正在生成预安装目录...";
        PluginPrepareDetail = string.Empty;
        try
        {
            if (_releaseCenterService is null)
            {
                PluginPrepareSummary = "插件预安装：未注册服务";
                PluginPrepareDetail = "IReleaseCenterService 未注册。";
                return;
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await _releaseCenterService.PrepareCachedPluginPackagesAsync(cts.Token);
            PluginPrepareSummary = result.Summary;
            PluginPrepareDetail = string.IsNullOrWhiteSpace(result.Detail) ? result.StagingDirectory : result.Detail;
            LastPluginPrepareAt = DateTime.Now;
            if (result.IsSuccess && result.PreparedCount > 0)
            {
                RestartRequiredNotice = "已有插件更新就绪，重启 DIB 后生效。";
            }
        }
        catch (Exception ex)
        {
            PluginPrepareSummary = "插件预安装失败";
            PluginPrepareDetail = ex.Message;
            _logger.LogError(ex, "准备插件预安装目录失败");
        }
        finally
        {
            IsPluginPrepareRunning = false;
        }
    }

    private async Task RestoreLatestPluginBackupAsync()
    {
        if (IsPluginRollbackRunning) return;
        IsPluginRollbackRunning = true;
        PluginRollbackSummary = "正在恢复最近一次插件备份...";
        PluginRollbackDetail = string.Empty;
        try
        {
            if (_releaseCenterService is null)
            {
                PluginRollbackSummary = "插件回滚：未注册服务";
                PluginRollbackDetail = "IReleaseCenterService 未注册。";
                return;
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await _releaseCenterService.RestoreLatestPluginBackupAsync(cts.Token);
            PluginRollbackSummary = result.Summary;
            PluginRollbackDetail = string.IsNullOrWhiteSpace(result.Detail) ? result.RuntimePluginRoot : result.Detail;
            LastPluginRollbackAt = DateTime.Now;
            if (result.IsSuccess && result.RestoredCount > 0)
            {
                RestartRequiredNotice = "已有插件回滚就绪，重启 DIB 后生效。";
            }
        }
        catch (Exception ex)
        {
            PluginRollbackSummary = "插件回滚失败";
            PluginRollbackDetail = ex.Message;
            _logger.LogError(ex, "恢复插件备份失败");
        }
        finally
        {
            IsPluginRollbackRunning = false;
        }
    }

    private async Task InitializeSitePluginsAsync()
    {
        if (IsSiteInitializationRunning) return;

        var siteName = SiteNameInput.Trim();
        if (string.IsNullOrWhiteSpace(siteName))
        {
            SiteInitializationSummary = "初始化本机插件：请先填写站点名称";
            SiteInitializationDetail = "请先保存站点信息，再执行初始化。";
            return;
        }

        IsSiteInitializationRunning = true;
        SiteInitializationSummary = "初始化本机插件：正在执行";
        SiteInitializationDetail = "正在保存站点信息并检查更新...";

        try
        {
            SaveSiteProfile();
            await CheckUpdateAsync();

            if (_releaseCenterService is null)
            {
                SiteInitializationSummary = "初始化本机插件失败";
                SiteInitializationDetail = "IReleaseCenterService 未注册。";
                return;
            }

            SiteInitializationDetail = "正在下载授权插件包...";
            await DownloadPluginPackagesAsync();

            if (PluginDownloadSummary.Contains("失败", StringComparison.Ordinal))
            {
                SiteInitializationSummary = "初始化本机插件失败";
                SiteInitializationDetail = PluginDownloadDetail;
                return;
            }

            SiteInitializationDetail = "正在生成预安装目录...";
            await PreparePluginPackagesAsync();

            if (PluginPrepareSummary.Contains("失败", StringComparison.Ordinal))
            {
                SiteInitializationSummary = "初始化本机插件失败";
                SiteInitializationDetail = PluginPrepareDetail;
                return;
            }

            SiteInitializationSummary = "初始化本机插件完成";
            SiteInitializationDetail = "基础插件已就绪，重启 DIB 后生效。";
            RestartRequiredNotice = "已有插件更新就绪，重启 DIB 后生效。";
        }
        catch (Exception ex)
        {
            SiteInitializationSummary = "初始化本机插件失败";
            SiteInitializationDetail = ex.Message;
            _logger.LogError(ex, "初始化本机插件失败");
        }
        finally
        {
            IsSiteInitializationRunning = false;
        }
    }

    private static string BuildClientDownloadDetail(ReleaseCenterDownloadProgress item)
    {
        var progressText = BuildProgressText(item.BytesReceived, item.TotalBytes);
        var speedText = item.BytesPerSecond > 0 ? $"速度：{FormatBytes((long)item.BytesPerSecond)}/s" : "速度：计算中";
        var remainingText = item.EstimatedRemaining.HasValue ? $"预计剩余：{FormatRemaining(item.EstimatedRemaining.Value)}" : "预计剩余：计算中";
        return $"状态：{item.Status}{Environment.NewLine}进度：{progressText}{Environment.NewLine}{speedText}{Environment.NewLine}{remainingText}";
    }

    private static string BuildProgressText(long bytesReceived, long? totalBytes)
    {
        if (totalBytes.HasValue && totalBytes.Value > 0)
        {
            var percent = Math.Clamp((double)bytesReceived / totalBytes.Value * 100, 0, 100);
            return $"{FormatBytes(bytesReceived)} / {FormatBytes(totalBytes.Value)}（{percent:F1}%）";
        }

        return FormatBytes(bytesReceived);
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:F1} {units[unitIndex]}";
    }

    private static string FormatRemaining(TimeSpan remaining)
    {
        if (remaining.TotalHours >= 1)
        {
            return $"{(int)remaining.TotalHours} 小时 {remaining.Minutes} 分";
        }

        if (remaining.TotalMinutes >= 1)
        {
            return $"{remaining.Minutes} 分 {remaining.Seconds} 秒";
        }

        return $"{Math.Max(0, remaining.Seconds)} 秒";
    }

    private void PersistAppSettings()
    {
        var configPath = ConfigurationExtensions.GetConfigFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(configPath, json);
    }
    private void OpenLogFolder()
    {
        try
        {
            var logPath = ConfigurationExtensions.GetLogsDirectory(_settings.Logging.LogPath);
            if (Directory.Exists(logPath))
            {
                Process.Start(new ProcessStartInfo { FileName = logPath, UseShellExecute = true });
                _logger.LogInformation($"打开日志文件夹: {logPath}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"打开日志文件夹失败: {ex}");
        }
    }

    private async Task RunSelfCheckAsync()
    {
        if (IsSelfCheckRunning) return;
        IsSelfCheckRunning = true;
        SelfCheckNotice = "正在执行自检...";
        try
        {
            SelfCheckItems.Clear();
            var passed = 0;
            var total = 0;
            void AddResult(string name, bool ok, string detail)
            {
                total++;
                if (ok) passed++;
                SelfCheckItems.Add(new SelfCheckItem { Name = name, IsPassed = ok, Detail = detail });
            }

            var configPath = ConfigurationExtensions.GetConfigFilePath();
            AddResult("用户配置文件", File.Exists(configPath), configPath);
            var defaultConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            AddResult("默认配置文件", File.Exists(defaultConfigPath), defaultConfigPath);
            var iconAvailable = IsTrayIconAvailable(_settings.Tray.IconPath, out var iconDetail);
            AddResult("托盘图标文件", iconAvailable, iconDetail);
            var pluginDir = ConfigurationExtensions.GetRuntimePluginsDirectory(_settings.Plugin.PluginDirectory);
            var pluginExists = Directory.Exists(pluginDir);
            var pluginCount = pluginExists ? Directory.GetDirectories(pluginDir).Length + Directory.GetFiles(pluginDir, "*.dll").Length : 0;
            AddResult("插件目录", pluginExists, $"{pluginDir}（发现 {pluginCount} 项）");
            var logDir = ConfigurationExtensions.GetLogsDirectory(_settings.Logging.LogPath);
            var logWritable = EnsureWritable(logDir, out var writeDetail);
            AddResult("日志目录写入", logWritable, writeDetail);
            if (_supabaseService == null)
            {
                AddResult("Supabase 表访问", false, "ISupabaseService 未注册");
            }
            else
            {
                using var supaCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var tableResult = await _supabaseService.CheckTableAccessAsync("todos", supaCts.Token);
                var schema = string.IsNullOrWhiteSpace(_settings.Supabase.Schema) ? "public" : _settings.Supabase.Schema;
                AddResult("Supabase 表访问", tableResult.IsSuccess, $"{schema}.todos - {tableResult.Message}");
            }

            if (_releaseCenterService == null)
            {
                AddResult("发布中心更新检查", false, "IReleaseCenterService 未注册");
            }
            else
            {
                using var rcCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var updateResult = await _releaseCenterService.CheckForUpdatesAsync(rcCts.Token);
                AddResult("发布中心更新检查", updateResult.IsSuccess, updateResult.Summary);
            }

            LastSelfCheckAt = DateTime.Now;
            SelfCheckSummary = $"自检完成：{passed}/{total} 通过";
            SelfCheckNotice = $"自检完成，完成时间：{LastSelfCheckAt:HH:mm:ss}";
            _logger.LogInformation("设置自检完成：{Passed}/{Total} 通过", passed, total);
        }
        catch (Exception ex)
        {
            SelfCheckNotice = $"自检失败：{ex.Message}";
            _logger.LogError(ex, "设置自检执行失败");
        }
        finally
        {
            IsSelfCheckRunning = false;
        }
    }

    private void ExportSelfCheckReport()
    {
        try
        {
            if (SelfCheckItems.Count == 0)
            {
                RunSelfCheckAsync().GetAwaiter().GetResult();
            }

            var reportDir = ConfigurationExtensions.GetLogsDirectory(_settings.Logging.LogPath);
            Directory.CreateDirectory(reportDir);
            var fileName = $"selfcheck-{DateTime.Now:yyyyMMdd-HHmmss}.md";
            var filePath = Path.Combine(reportDir, fileName);
            var sb = new StringBuilder();
            sb.AppendLine("# 自检报告");
            sb.AppendLine();
            sb.AppendLine($"- 应用: {AppName}");
            sb.AppendLine($"- 版本: {AppVersion}");
            sb.AppendLine($"- 时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"- 汇总: {SelfCheckSummary}");
            sb.AppendLine();
            sb.AppendLine("| 项目 | 状态 | 详情 |");
            sb.AppendLine("|---|---|---|");
            foreach (var item in SelfCheckItems)
            {
                var status = item.IsPassed ? "通过" : "失败";
                var detail = item.Detail.Replace("|", "\\|");
                sb.AppendLine($"| {item.Name} | {status} | {detail} |");
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            _logger.LogInformation("已导出自检报告: {Path}", filePath);
            SelfCheckSummary = $"{SelfCheckSummary}（已导出报告）";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出自检报告失败");
        }
    }

    private static bool EnsureWritable(string directory, out string detail)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probeFile = Path.Combine(directory, $".probe-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probeFile, DateTime.Now.ToString("O"));
            File.Delete(probeFile);
            detail = directory;
            return true;
        }
        catch (Exception ex)
        {
            detail = $"{directory}（{ex.Message}）";
            return false;
        }
    }

    private static bool IsTrayIconAvailable(string iconPath, out string detail)
    {
        var fullPath = Path.Combine(AppContext.BaseDirectory, iconPath);
        if (File.Exists(fullPath))
        {
            detail = fullPath;
            return true;
        }

        try
        {
            var assemblyName = typeof(SettingsViewModel).Assembly.GetName().Name;
            var uri = new Uri($"avares://{assemblyName}/{iconPath}");
            using var _ = AssetLoader.Open(uri);
            detail = $"{uri}（通过资源加载）";
            return true;
        }
        catch (Exception ex)
        {
            detail = $"{fullPath}（文件不存在，且资源加载失败: {ex.Message}）";
            return false;
        }
    }
}

public class SelfCheckItem
{
    public string Name { get; set; } = string.Empty;
    public bool IsPassed { get; set; }
    public string Detail { get; set; } = string.Empty;
    public string StatusText => IsPassed ? "通过" : "失败";
    public string StatusIcon => IsPassed ? "✅" : "❌";
    public IBrush StatusBrush => IsPassed ? new SolidColorBrush(Color.Parse("#3BB346")) : new SolidColorBrush(Color.Parse("#F93920"));
}









