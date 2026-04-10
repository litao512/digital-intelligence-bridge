using System.Text.Json;
using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Services;
using DigitalIntelligenceBridge.ViewModels;
using Microsoft.Extensions.Options;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class SettingsViewModelInitializationTests
{
    [Fact]
    public void SaveSiteProfileCommand_ShouldPersistSiteNameAndRemark()
    {
        using var sandbox = new TestConfigSandbox();

        var vm = CreateVm();
        vm.SiteNameInput = "门诊一楼登记台";
        vm.SiteRemarkInput = "收费处旁";

        vm.SaveSiteProfileCommand.Execute();

        Assert.Equal("门诊一楼登记台", vm.SiteNameInput);
        Assert.Equal("收费处旁", vm.SiteRemarkInput);
        Assert.Contains("门诊一楼登记台", vm.SiteProfileSummary);
        Assert.Contains("已保存", vm.SiteProfileStatus);

        var persistedJson = File.ReadAllText(ConfigurationExtensions.GetConfigFilePath());
        using var document = JsonDocument.Parse(persistedJson);
        var releaseCenter = document.RootElement.GetProperty("ReleaseCenter");
        Assert.Equal("门诊一楼登记台", releaseCenter.GetProperty("SiteName").GetString());
        Assert.Equal("收费处旁", releaseCenter.GetProperty("SiteRemark").GetString());
    }

    [Fact]
    public async Task InitializeSitePluginsCommand_ShouldBlock_WhenSiteNameMissing()
    {
        var service = new SequenceReleaseCenterService();
        var vm = CreateVm(service);
        service.ResetCounters();
        vm.SiteNameInput = string.Empty;

        Assert.False(vm.InitializeSitePluginsCommand.CanExecute());

        Assert.Equal(0, service.CheckUpdateCallCount);
        Assert.Equal("初始化本机插件：尚未执行", vm.SiteInitializationSummary);
    }

    [Fact]
    public async Task InitializeSitePluginsCommand_ShouldRunCheckDownloadPrepareSequence()
    {
        var service = new SequenceReleaseCenterService();
        var vm = CreateVm(service);
        service.ResetCounters();
        vm.SiteNameInput = "门诊一楼登记台";

        vm.InitializeSitePluginsCommand.Execute();
        await WaitForInitializationAsync(vm);

        Assert.Equal(1, service.CheckUpdateCallCount);
        Assert.Equal(1, service.PluginDownloadCallCount);
        Assert.Equal(1, service.PluginPrepareCallCount);
        Assert.Equal("初始化本机插件完成", vm.SiteInitializationSummary);
        Assert.Contains("重启 DIB 后生效", vm.RestartRequiredNotice);
        Assert.Contains("授权插件：1 个", vm.AuthorizedPluginSummary);
    }

    private static async Task WaitForInitializationAsync(SettingsViewModel vm)
    {
        var started = false;
        for (var i = 0; i < 100; i++)
        {
            started |= vm.IsSiteInitializationRunning;
            if (started && !vm.IsSiteInitializationRunning)
            {
                return;
            }

            await Task.Delay(20);
        }
    }

    private static SettingsViewModel CreateVm(IReleaseCenterService? releaseCenterService = null)
    {
        var settings = new AppSettings
        {
            Application = new ApplicationConfig { Name = "TestApp", Version = "1.0.0" },
            Tray = new TrayConfig { IconPath = "Assets/avalonia-logo.ico", ShowNotifications = true },
            Plugin = new PluginConfig { PluginDirectory = "plugins-tests" },
            Logging = new LoggingConfig { LogPath = "logs" },
            ReleaseCenter = new ReleaseCenterConfig
            {
                Enabled = true,
                BaseUrl = "http://release-center.local",
                Channel = "stable"
            }
        };

        return new SettingsViewModel(
            new StubApplicationService(),
            new StubTrayService(),
            new NullLoggerService<SettingsViewModel>(),
            Options.Create(settings),
            new StubSupabaseService(),
            releaseCenterService);
    }

    private sealed class SequenceReleaseCenterService : IReleaseCenterService
    {
        public bool IsConfigured => true;
        public int CheckUpdateCallCount { get; private set; }
        public int PluginDownloadCallCount { get; private set; }
        public int PluginPrepareCallCount { get; private set; }

        public void ResetCounters()
        {
            CheckUpdateCallCount = 0;
            PluginDownloadCallCount = 0;
            PluginPrepareCallCount = 0;
        }

        public Task<ReleaseCenterCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
        {
            CheckUpdateCallCount++;
            return Task.FromResult(new ReleaseCenterCheckResult(
                true,
                "发现 1 类可用更新",
                "客户端更新：暂无发布版本",
                "插件更新：1 个可用插件（就诊登记 1.0.1）",
                "channel=stable",
                "当前站点：门诊一楼登记台 / 11111111-1111-1111-1111-111111111111",
                "授权插件：1 个",
                "就诊登记 / patient-registration / 1.0.1"));
        }

        public Task<ReleaseCenterClientDownloadResult> DownloadLatestClientPackageAsync(IProgress<ReleaseCenterDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new ReleaseCenterClientDownloadResult(true, "没有可下载的客户端更新包", string.Empty, string.Empty, "C:\\cache", string.Empty));

        public Task<ReleaseCenterPluginDownloadResult> DownloadAvailablePluginPackagesAsync(CancellationToken cancellationToken = default)
        {
            PluginDownloadCallCount++;
            return Task.FromResult(new ReleaseCenterPluginDownloadResult(true, "插件包已缓存 1 项", "detail", 1, "C:\\cache"));
        }

        public Task<ReleaseCenterPluginPrepareResult> PrepareCachedPluginPackagesAsync(CancellationToken cancellationToken = default)
        {
            PluginPrepareCallCount++;
            return Task.FromResult(new ReleaseCenterPluginPrepareResult(true, "已生成 1 个预安装目录", "detail", 1, "C:\\staging"));
        }

        public Task<ReleaseCenterPluginActivateResult> ActivatePreparedPluginPackagesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ReleaseCenterPluginActivateResult(true, "已激活 1 个插件目录", "detail", 1, "C:\\runtime"));

        public Task<ReleaseCenterPluginRollbackResult> RestoreLatestPluginBackupAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ReleaseCenterPluginRollbackResult(true, "已恢复最近一次插件备份", "detail", 1, "C:\\runtime"));
    }

    private sealed class StubSupabaseService : ISupabaseService
    {
        public bool IsConfigured => true;
        public Task<SupabaseConnectionResult> CheckConnectionAsync(CancellationToken cancellationToken = default) => Task.FromResult(new SupabaseConnectionResult(true, true, System.Net.HttpStatusCode.OK, "ok"));
        public Task<SupabaseConnectionResult> CheckTableAccessAsync(string tableName, CancellationToken cancellationToken = default) => Task.FromResult(new SupabaseConnectionResult(true, true, System.Net.HttpStatusCode.OK, "table ok"));
    }

    private sealed class StubApplicationService : IApplicationService
    {
        public bool IsInitialized => true;
        public string GetApplicationName() => "TestApp";
        public string GetVersion() => "1.0.0";
        public Task InitializeAsync() => Task.CompletedTask;
        public Task OnShutdownAsync() => Task.CompletedTask;
        public Task OnStartedAsync() => Task.CompletedTask;
    }

    private sealed class StubTrayService : ITrayService
    {
        public bool IsWindowVisible => true;
        public bool IsExiting => false;
        public void AddMenuItem(string header, Action callback, string? parentPath = null) { }
        public void AddSeparator(string? parentPath = null) { }
        public void ExitApplication() { }
        public void HideWindow() { }
        public void Initialize(Avalonia.Controls.Window mainWindow) { }
        public void RemoveMenuItem(string path) { }
        public void SetShowNotifications(bool show) { }
        public void SetTooltip(string tooltip) { }
        public void ShowNotification(string title, string message) { }
        public void ShowWindow() { }
        public void ToggleWindow() { }
    }

    private sealed class NullLoggerService<T> : ILoggerService<T>
    {
        public void LogCritical(string message, params object[] args) { }
        public void LogDebug(string message, params object[] args) { }
        public void LogError(string message, params object[] args) { }
        public void LogError(Exception exception, string message, params object[] args) { }
        public void LogInformation(string message, params object[] args) { }
        public void LogWarning(string message, params object[] args) { }
    }
}

