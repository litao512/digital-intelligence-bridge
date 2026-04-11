using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Services;
using DigitalIntelligenceBridge.ViewModels;
using Microsoft.Extensions.Options;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class SettingsViewModelPluginDownloadTests
{
    [Fact]
    public async Task DownloadPluginPackagesCommand_ShouldPopulateDownloadSummary_WhenServiceReturnsSuccess()
    {
        var vm = CreateVm(new StubReleaseCenterService());

        vm.DownloadPluginPackagesCommand.Execute();
        await WaitForDownloadAsync(vm);

        Assert.Equal("插件包已缓存 1 项", vm.PluginDownloadSummary);
        Assert.NotNull(vm.LastPluginDownloadAt);
    }

    private static async Task WaitForDownloadAsync(SettingsViewModel vm)
    {
        for (var i = 0; i < 50; i++)
        {
            if (!vm.IsPluginDownloadRunning)
            {
                return;
            }

            await Task.Delay(20);
        }
    }

    private static SettingsViewModel CreateVm(IReleaseCenterService releaseCenterService)
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
            releaseCenterService);
    }

    private sealed class StubReleaseCenterService : IReleaseCenterService
    {
        public bool IsConfigured => true;
        public Task<ReleaseCenterCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ReleaseCenterCheckResult(true, "ok", "client", "plugin", "detail", "site", "authorized", "authorized-detail"));
        public Task<ReleaseCenterClientDownloadResult> DownloadLatestClientPackageAsync(IProgress<ReleaseCenterDownloadProgress>? progress = null, CancellationToken cancellationToken = default) => Task.FromResult(new ReleaseCenterClientDownloadResult(true, "没有可下载的客户端更新包", string.Empty, string.Empty, "C:\\cache", string.Empty));
        public Task<ReleaseCenterPluginDownloadResult> DownloadAvailablePluginPackagesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ReleaseCenterPluginDownloadResult(true, "插件包已缓存 1 项", "detail", 1, "C:\\cache"));
        public Task<ReleaseCenterPluginPrepareResult> PrepareCachedPluginPackagesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ReleaseCenterPluginPrepareResult(true, "已生成 1 个预安装目录", "detail", 1, "C:\\staging"));
        public Task<ReleaseCenterPluginActivateResult> ActivatePreparedPluginPackagesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ReleaseCenterPluginActivateResult(true, "已激活 1 个插件目录", "detail", 1, "C:\\runtime"));
        public Task<ReleaseCenterPluginRollbackResult> RestoreLatestPluginBackupAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ReleaseCenterPluginRollbackResult(true, "已恢复最近一次插件备份", "detail", 1, "C:\\runtime"));
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





