using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Models;
using DigitalIntelligenceBridge.Services;
using DigitalIntelligenceBridge.ViewModels;
using Microsoft.Extensions.Options;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class SettingsViewModelClientDownloadTests
{
    [Fact]
    public async Task DownloadClientPackageCommand_ShouldPopulateClientDownloadSummary_AndRequireExit_WhenServiceReturnsSuccess()
    {
        var packagePath = Path.Combine(Path.GetTempPath(), "dib-settings-download-tests", Guid.NewGuid().ToString("N"), "dib-win-x64-portable-1.0.1.zip");
        Directory.CreateDirectory(Path.GetDirectoryName(packagePath)!);
        File.WriteAllText(packagePath, "zip");
        var upgradeService = new RecordingClientUpgradeService();
        var vm = CreateVm(new StubReleaseCenterService(packagePath), upgradeService);

        vm.DownloadClientPackageCommand.Execute();
        await WaitForDownloadAsync(vm);
        await WaitForConditionAsync(() => vm.ClientPackageDownloadDetail.Contains("进度："));

        Assert.Equal("客户端更新包已缓存：1.0.1", vm.ClientPackageDownloadSummary);
        Assert.Equal("客户端升级包已就绪，可以立即升级。", vm.ClientUpgradeNotice);
        Assert.True(vm.StartClientUpgradeCommand.CanExecute());
        Assert.Contains("进度：", vm.ClientPackageDownloadDetail);
        Assert.Contains("速度：", vm.ClientPackageDownloadDetail);
        Assert.NotNull(vm.LastClientPackageDownloadAt);

        vm.StartClientUpgradeCommand.Execute();

        Assert.Equal(packagePath, upgradeService.LastPackagePath);
        Assert.Equal("客户端升级已启动", vm.ClientUpgradeNotice);
    }

    [Fact]
    public async Task CancelClientPackageDownloadCommand_ShouldCancelRunningDownload()
    {
        var service = new BlockingReleaseCenterService();
        var vm = CreateVm(service);

        vm.DownloadClientPackageCommand.Execute();
        await WaitForConditionAsync(() => vm.IsClientPackageDownloadRunning);

        Assert.True(vm.CancelClientPackageDownloadCommand.CanExecute());

        vm.CancelClientPackageDownloadCommand.Execute();
        await WaitForDownloadAsync(vm);

        Assert.Equal("客户端下载已取消", vm.ClientPackageDownloadSummary);
        Assert.Contains("已取消", vm.ClientPackageDownloadDetail);
        Assert.Empty(vm.ClientUpgradeNotice);
        Assert.False(vm.IsClientPackageDownloadRunning);
        Assert.False(vm.CancelClientPackageDownloadCommand.CanExecute());
    }

    private static async Task WaitForDownloadAsync(SettingsViewModel vm)
    {
        for (var i = 0; i < 50; i++)
        {
            if (!vm.IsClientPackageDownloadRunning)
            {
                return;
            }

            await Task.Delay(20);
        }
    }

    private static async Task WaitForConditionAsync(Func<bool> condition)
    {
        for (var i = 0; i < 50; i++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }
    }

    private static SettingsViewModel CreateVm(IReleaseCenterService releaseCenterService, IClientUpgradeService? clientUpgradeService = null)
    {
        var settings = new AppSettings
        {
            Application = new ApplicationConfig { Name = "TestApp", Version = "1.0.0" },
            Tray = new TrayConfig { IconPath = "Assets/avalonia-logo.ico", ShowNotifications = true },
            Plugin = new PluginConfig { PluginDirectory = "plugins-tests" },
            Logging = new LoggingConfig { LogPath = "logs" },
        };

        return new SettingsViewModel(
            new StubApplicationService(),
            new StubTrayService(),
            new NullLoggerService<SettingsViewModel>(),
            Options.Create(settings),
            new StubSupabaseService(),
            releaseCenterService,
            clientUpgradeService: clientUpgradeService);
    }

    private sealed class RecordingClientUpgradeService : IClientUpgradeService
    {
        public string? LastPackagePath { get; private set; }

        public ClientUpgradeStartResult StartUpgrade(string packagePath)
        {
            LastPackagePath = packagePath;
            return new ClientUpgradeStartResult(true, "客户端升级已启动", "detail");
        }
    }

    private sealed class StubReleaseCenterService : IReleaseCenterService
    {
        private readonly string _packagePath;

        public StubReleaseCenterService(string? packagePath = null)
        {
            _packagePath = packagePath ?? @"C:\cache\dib-win-x64-portable-1.0.1.zip";
        }

        public bool IsConfigured => true;
        public Task<ReleaseCenterCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ReleaseCenterCheckResult(true, "ok", "client", "plugin", "detail", "site", "authorized", "authorized-detail"));
        public Task<ResourceDiscoverySnapshot> DiscoverResourcesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ResourceDiscoverySnapshot());
        public Task<AuthorizedResourceSnapshot> GetAuthorizedResourcesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new AuthorizedResourceSnapshot());
        public Task<ResourceApplicationSubmitResult> ApplyResourceAsync(string resourceId, string pluginCode, string reason, CancellationToken cancellationToken = default) => Task.FromResult(new ResourceApplicationSubmitResult(true, "ok", "apply-test", "Submitted"));
        public Task<ReleaseCenterClientDownloadResult> DownloadLatestClientPackageAsync(IProgress<ReleaseCenterDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            progress?.Report(new ReleaseCenterDownloadProgress("downloading", "正在下载客户端更新包", 50, 100, 2048, TimeSpan.FromSeconds(1)));
            progress?.Report(new ReleaseCenterDownloadProgress("verifying", "正在校验客户端更新包", 100, 100, 0, TimeSpan.Zero));
            progress?.Report(new ReleaseCenterDownloadProgress("completed", "客户端下载完成", 100, 100, 0, TimeSpan.Zero));
            return Task.FromResult(new ReleaseCenterClientDownloadResult(true, "客户端更新包已缓存：1.0.1", "detail", "1.0.1", Path.GetDirectoryName(_packagePath) ?? "C:\\cache", _packagePath));
        }
        public Task<ReleaseCenterPluginDownloadResult> DownloadAvailablePluginPackagesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ReleaseCenterPluginDownloadResult(true, "插件包已缓存 1 项", "detail", 1, "C:\\cache"));
        public Task<ReleaseCenterPluginPrepareResult> PrepareCachedPluginPackagesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ReleaseCenterPluginPrepareResult(true, "已生成 1 个预安装目录", "detail", 1, "C:\\staging"));
        public Task<ReleaseCenterPluginActivateResult> ActivatePreparedPluginPackagesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ReleaseCenterPluginActivateResult(true, "已激活 1 个插件目录", "detail", 1, "C:\\runtime"));
        public Task<ReleaseCenterPluginRollbackResult> RestoreLatestPluginBackupAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ReleaseCenterPluginRollbackResult(true, "已恢复最近一次插件备份", "detail", 1, "C:\\runtime"));
    }

    private sealed class BlockingReleaseCenterService : IReleaseCenterService
    {
        public bool IsConfigured => true;
        public Task<ReleaseCenterCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ReleaseCenterCheckResult(true, "ok", "client", "plugin", "detail", "site", "authorized", "authorized-detail"));
        public Task<ResourceDiscoverySnapshot> DiscoverResourcesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ResourceDiscoverySnapshot());
        public Task<AuthorizedResourceSnapshot> GetAuthorizedResourcesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new AuthorizedResourceSnapshot());
        public Task<ResourceApplicationSubmitResult> ApplyResourceAsync(string resourceId, string pluginCode, string reason, CancellationToken cancellationToken = default) => Task.FromResult(new ResourceApplicationSubmitResult(true, "ok", "apply-test", "Submitted"));
        public async Task<ReleaseCenterClientDownloadResult> DownloadLatestClientPackageAsync(IProgress<ReleaseCenterDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            progress?.Report(new ReleaseCenterDownloadProgress("downloading", "正在下载客户端更新包", 10, 100, 1024, TimeSpan.FromSeconds(5)));
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new ReleaseCenterClientDownloadResult(true, "unexpected", string.Empty, "1.0.1", "C:\\cache", "C:\\cache\\dib-win-x64-portable-1.0.1.zip");
        }
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
        public string GetApplicationName() => "TestApp";
        public string GetVersion() => "1.0.0";
        public Task InitializeAsync() => Task.CompletedTask;
        public Task OnShutdownAsync() => Task.CompletedTask;
        public Task OnStartedAsync() => Task.CompletedTask;
        public void RestartApplication() { }
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



