using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Services;
using DigitalIntelligenceBridge.ViewModels;
using Microsoft.Extensions.Options;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class SettingsViewModelReleaseCenterTests
{
    [Fact]
    public async Task CheckUpdateCommand_ShouldPopulateReleaseCenterSummaries_WhenServiceReturnsSuccess()
    {
        var vm = CreateVm(new StubReleaseCenterService(new ReleaseCenterCheckResult(
            true,
            "发现 2 类可用更新",
            "客户端最新版本：1.2.0（最低升级版本：1.0.0）",
            "插件更新：2 个可用插件（就诊登记 1.0.1、床旁巡视 1.0.0）",
            "channel=stable",
            "当前站点：门诊登记台 1 / 11111111-1111-1111-1111-111111111111",
            "授权插件：2 个",
            "就诊登记 / patient-registration / 1.0.1")));

        vm.CheckUpdateCommand.Execute();
        await WaitForUpdateCheckAsync(vm);

        Assert.Equal("发现 2 类可用更新", vm.UpdateCheckSummary);
        Assert.Contains("1.2.0", vm.ClientUpdateSummary);
        Assert.Contains("2 个可用插件", vm.PluginUpdateSummary);
        Assert.Contains("channel=stable", vm.UpdateCheckDetail);
        Assert.Contains("门诊登记台 1", vm.SiteAuthorizationSummary);
        Assert.Contains("2 个", vm.AuthorizedPluginSummary);
        Assert.Contains("patient-registration", vm.AuthorizedPluginDetail);
        Assert.NotNull(vm.LastUpdateCheckAt);
    }

    [Fact]
    public async Task CheckUpdateCommand_ShouldExposeServiceMissingState_WhenReleaseCenterServiceIsNull()
    {
        var vm = CreateVm();

        vm.CheckUpdateCommand.Execute();
        await WaitForUpdateCheckAsync(vm);

        Assert.Equal("检查更新不可用", vm.UpdateCheckSummary);
        Assert.Equal("客户端更新：未注册服务", vm.ClientUpdateSummary);
        Assert.Equal("插件更新：未注册服务", vm.PluginUpdateSummary);
        Assert.Equal("站点：未注册服务", vm.SiteAuthorizationSummary);
        Assert.Equal("授权插件：未注册服务", vm.AuthorizedPluginSummary);
    }

    [Fact]
    public void RestartApplicationCommand_ShouldInvokeApplicationRestart()
    {
        var appService = new StubApplicationService();
        var vm = CreateVm(appService: appService);

        vm.RestartApplicationCommand.Execute();

        Assert.True(appService.RestartCalled);
    }

    private static async Task WaitForUpdateCheckAsync(SettingsViewModel vm)
    {
        for (var i = 0; i < 50; i++)
        {
            if (!vm.IsUpdateCheckRunning)
            {
                return;
            }

            await Task.Delay(20);
        }
    }

    private static SettingsViewModel CreateVm(IReleaseCenterService? releaseCenterService = null, StubApplicationService? appService = null)
    {
        var settings = new AppSettings
        {
            Application = new ApplicationConfig { Name = "TestApp", Version = "1.0.0" },
            Tray = new TrayConfig { IconPath = "Assets/avalonia-logo.ico", ShowNotifications = true },
            Plugin = new PluginConfig { PluginDirectory = "plugins-tests" },
            Logging = new LoggingConfig { LogPath = "logs" },
        };

        return new SettingsViewModel(
            appService ?? new StubApplicationService(),
            new StubTrayService(),
            new NullLoggerService<SettingsViewModel>(),
            Options.Create(settings),
            new StubSupabaseService(),
            releaseCenterService);
    }

    private sealed class StubReleaseCenterService(ReleaseCenterCheckResult result) : IReleaseCenterService
    {
        public bool IsConfigured => true;
        public Task<ReleaseCenterCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default) => Task.FromResult(result);
        public Task<ReleaseCenterClientDownloadResult> DownloadLatestClientPackageAsync(IProgress<ReleaseCenterDownloadProgress>? progress = null, CancellationToken cancellationToken = default) => Task.FromResult(new ReleaseCenterClientDownloadResult(true, "没有可下载的客户端更新包", string.Empty, string.Empty, "C:\\cache", string.Empty));
        public Task<ReleaseCenterPluginDownloadResult> DownloadAvailablePluginPackagesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ReleaseCenterPluginDownloadResult(true, "插件包已缓存 0 项", string.Empty, 0, "C:\\cache"));
        public Task<ReleaseCenterPluginPrepareResult> PrepareCachedPluginPackagesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ReleaseCenterPluginPrepareResult(true, "已生成 0 个预安装目录", string.Empty, 0, "C:\\staging"));
        public Task<ReleaseCenterPluginActivateResult> ActivatePreparedPluginPackagesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ReleaseCenterPluginActivateResult(true, "已激活 0 个插件目录", string.Empty, 0, "C:\\runtime"));
        public Task<ReleaseCenterPluginRollbackResult> RestoreLatestPluginBackupAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ReleaseCenterPluginRollbackResult(true, "已恢复最近一次插件备份", string.Empty, 0, "C:\\runtime"));
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
        public bool RestartCalled { get; private set; }
        public string GetApplicationName() => "TestApp";
        public string GetVersion() => "1.0.0";
        public Task InitializeAsync() => Task.CompletedTask;
        public Task OnShutdownAsync() => Task.CompletedTask;
        public Task OnStartedAsync() => Task.CompletedTask;
        public void RestartApplication() => RestartCalled = true;
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



