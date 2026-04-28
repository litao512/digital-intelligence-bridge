using System.Text.Json;
using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Services;
using DigitalIntelligenceBridge.ViewModels;
using Microsoft.Extensions.Options;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class PluginCenterViewModelTests
{
    [Fact]
    public async Task RefreshAsync_ShouldListRuntimeAndStagingPlugins()
    {
        using var sandbox = new TestConfigSandbox();
        CreatePluginManifest(Path.Combine(sandbox.RootDirectory, "runtime"), "patient-registration", "就诊登记", "1.0.2");
        CreatePluginManifest(Path.Combine(sandbox.RootDirectory, "staging", "patient-registration-1.0.3"), "patient-registration", "就诊登记", "1.0.3");

        var vm = new PluginCenterViewModel(
            new NullLoggerService<PluginCenterViewModel>(),
            Options.Create(CreateSettings(sandbox)),
            null);

        await vm.RefreshAsync();

        var item = Assert.Single(vm.PluginItems);
        Assert.Equal("就诊登记", item.Name);
        Assert.Equal("1.0.3", item.Version);
        Assert.Equal("待重启生效", item.Status);
        Assert.Equal("1 个", vm.PendingRestartCountText);
    }

    [Fact]
    public async Task RefreshAsync_ShouldUseClientPluginDirectory_WhenReleaseCenterRuntimeRootOverridesEmptyDirectory()
    {
        using var sandbox = new TestConfigSandbox();
        CreatePluginManifest(Path.Combine(sandbox.RootDirectory, "client-plugins"), "patient-registration", "就诊登记", "1.0.3");
        Directory.CreateDirectory(Path.Combine(sandbox.RootDirectory, "empty-runtime"));
        var settings = CreateSettings(sandbox);
        settings.Plugin.PluginDirectory = Path.Combine(sandbox.RootDirectory, "client-plugins");
        settings.ReleaseCenter.RuntimePluginRoot = Path.Combine(sandbox.RootDirectory, "empty-runtime");
        var vm = new PluginCenterViewModel(
            new NullLoggerService<PluginCenterViewModel>(),
            Options.Create(settings),
            null);

        await vm.RefreshAsync();

        var item = Assert.Single(vm.PluginItems);
        Assert.Equal("patient-registration", item.PluginId);
        Assert.Equal("就诊登记", item.Name);
        Assert.Equal("1.0.3", item.CurrentVersion);
    }

    [Fact]
    public async Task RefreshAsync_ShouldMarkPluginUpdatable_WhenAuthorizedVersionIsNewer()
    {
        using var sandbox = new TestConfigSandbox();
        CreatePluginManifest(Path.Combine(sandbox.RootDirectory, "runtime"), "patient-registration", "就诊登记", "1.0.2");
        var vm = new PluginCenterViewModel(
            new NullLoggerService<PluginCenterViewModel>(),
            Options.Create(CreateSettings(sandbox)),
            null);

        vm.SetAvailablePluginsForTesting("就诊登记 / patient-registration / 1.0.3");
        await vm.RefreshAsync();

        var item = Assert.Single(vm.PluginItems);
        Assert.Equal("1.0.2", item.CurrentVersion);
        Assert.Equal("1.0.3", item.LatestVersion);
        Assert.Equal("可更新", item.Status);
    }

    [Fact]
    public async Task RefreshAsync_ShouldMarkPluginMissing_WhenAuthorizedPluginIsNotInstalled()
    {
        using var sandbox = new TestConfigSandbox();
        var vm = new PluginCenterViewModel(
            new NullLoggerService<PluginCenterViewModel>(),
            Options.Create(CreateSettings(sandbox)),
            null);

        vm.SetAvailablePluginsForTesting("就诊登记 / patient-registration / 1.0.3");
        await vm.RefreshAsync();

        var item = Assert.Single(vm.PluginItems);
        Assert.Equal("未安装", item.CurrentVersion);
        Assert.Equal("1.0.3", item.LatestVersion);
        Assert.Equal("未安装", item.Status);
    }

    [Fact]
    public async Task RefreshAsync_ShouldMarkPluginLatest_WhenAuthorizedVersionEqualsRuntimeVersion()
    {
        using var sandbox = new TestConfigSandbox();
        CreatePluginManifest(Path.Combine(sandbox.RootDirectory, "runtime"), "patient-registration", "就诊登记", "1.0.3");
        var vm = new PluginCenterViewModel(
            new NullLoggerService<PluginCenterViewModel>(),
            Options.Create(CreateSettings(sandbox)),
            null);

        vm.SetAvailablePluginsForTesting("就诊登记 / patient-registration / 1.0.3");
        await vm.RefreshAsync();

        var item = Assert.Single(vm.PluginItems);
        Assert.Equal("1.0.3", item.CurrentVersion);
        Assert.Equal("1.0.3", item.LatestVersion);
        Assert.Equal("已最新", item.Status);
    }

    [Fact]
    public async Task RefreshAsync_ShouldKeepPendingRestart_WhenStagingPluginExists()
    {
        using var sandbox = new TestConfigSandbox();
        CreatePluginManifest(Path.Combine(sandbox.RootDirectory, "runtime"), "patient-registration", "就诊登记", "1.0.2");
        CreatePluginManifest(Path.Combine(sandbox.RootDirectory, "staging", "patient-registration-1.0.3"), "patient-registration", "就诊登记", "1.0.3");
        var vm = new PluginCenterViewModel(
            new NullLoggerService<PluginCenterViewModel>(),
            Options.Create(CreateSettings(sandbox)),
            null);

        vm.SetAvailablePluginsForTesting("就诊登记 / patient-registration / 1.0.3");
        await vm.RefreshAsync();

        var item = Assert.Single(vm.PluginItems);
        Assert.Equal("1.0.3", item.CurrentVersion);
        Assert.Equal("1.0.3", item.LatestVersion);
        Assert.Equal("待重启生效", item.Status);
    }

    [Fact]
    public async Task CheckUpdatesCommand_ShouldParseAuthorizedPluginsAndRefreshStatuses()
    {
        using var sandbox = new TestConfigSandbox();
        CreatePluginManifest(Path.Combine(sandbox.RootDirectory, "runtime"), "patient-registration", "就诊登记", "1.0.2");
        var orchestrator = new StubPluginUpdateOrchestrator(new PluginUpdateRunResult(
            true,
            "发现插件更新",
            string.Empty,
            false,
            DateTime.Now,
            new ReleaseCenterCheckResult(
                true,
                "ok",
                "client",
                "plugin",
                "detail",
                "site",
                "authorized",
                "就诊登记 / patient-registration / 1.0.3"),
            null,
            null));
        var vm = new PluginCenterViewModel(
            new NullLoggerService<PluginCenterViewModel>(),
            Options.Create(CreateSettings(sandbox)),
            orchestrator);

        vm.CheckUpdatesCommand.Execute();
        await WaitUntilAsync(() => !vm.IsBusy);

        var item = Assert.Single(vm.PluginItems);
        Assert.Equal("可更新", item.Status);
        Assert.Equal("1.0.3", item.LatestVersion);
        Assert.Equal(1, orchestrator.CheckCallCount);
        Assert.Equal(0, orchestrator.RunCallCount);
        Assert.Equal(0, orchestrator.InstallOrUpdateCallCount);
    }

    [Fact]
    public async Task RefreshAsync_ShouldExposeUpdateAction_WhenPluginIsUpdatable()
    {
        using var sandbox = new TestConfigSandbox();
        CreatePluginManifest(Path.Combine(sandbox.RootDirectory, "runtime"), "patient-registration", "就诊登记", "1.0.2");
        var vm = new PluginCenterViewModel(
            new NullLoggerService<PluginCenterViewModel>(),
            Options.Create(CreateSettings(sandbox)),
            new StubPluginUpdateOrchestrator());

        vm.SetAvailablePluginsForTesting("就诊登记 / patient-registration / 1.0.3");
        await vm.RefreshAsync();

        var item = Assert.Single(vm.PluginItems);
        Assert.Equal("更新", item.ActionText);
        Assert.True(item.CanExecuteAction);
    }

    [Fact]
    public async Task RefreshAsync_ShouldExposeInstallAction_WhenPluginIsMissing()
    {
        using var sandbox = new TestConfigSandbox();
        var vm = new PluginCenterViewModel(
            new NullLoggerService<PluginCenterViewModel>(),
            Options.Create(CreateSettings(sandbox)),
            new StubPluginUpdateOrchestrator());

        vm.SetAvailablePluginsForTesting("就诊登记 / patient-registration / 1.0.3");
        await vm.RefreshAsync();

        var item = Assert.Single(vm.PluginItems);
        Assert.Equal("安装", item.ActionText);
        Assert.True(item.CanExecuteAction);
    }

    [Fact]
    public async Task RefreshAsync_ShouldExposeUninstallAction_WhenPluginIsInstalled()
    {
        using var sandbox = new TestConfigSandbox();
        CreatePluginManifest(Path.Combine(sandbox.RootDirectory, "runtime"), "patient-registration", "就诊登记", "1.0.3");
        var vm = new PluginCenterViewModel(
            new NullLoggerService<PluginCenterViewModel>(),
            Options.Create(CreateSettings(sandbox)),
            new StubPluginUpdateOrchestrator());

        await vm.RefreshAsync();

        var item = Assert.Single(vm.PluginItems);
        Assert.Equal("卸载", item.ActionText);
        Assert.True(item.CanExecuteAction);
    }

    [Fact]
    public async Task PluginActionCommand_ShouldInstallOrUpdateRequestedPlugin()
    {
        using var sandbox = new TestConfigSandbox();
        CreatePluginManifest(Path.Combine(sandbox.RootDirectory, "runtime"), "patient-registration", "就诊登记", "1.0.2");
        var orchestrator = new StubPluginUpdateOrchestrator();
        var trayService = new StubTrayService();
        var vm = new PluginCenterViewModel(
            new NullLoggerService<PluginCenterViewModel>(),
            Options.Create(CreateSettings(sandbox)),
            orchestrator,
            trayService: trayService);

        vm.SetAvailablePluginsForTesting("就诊登记 / patient-registration / 1.0.3");
        await vm.RefreshAsync();
        vm.PluginItems[0].ActionCommand!.Execute();
        await WaitUntilAsync(() => !vm.IsBusy);

        Assert.Equal(1, orchestrator.InstallOrUpdateCallCount);
        Assert.Equal("patient-registration", orchestrator.InstallOrUpdatePluginId);
        Assert.Equal("1.0.3", orchestrator.InstallOrUpdateVersion);
        Assert.True(orchestrator.InstallOrUpdateProgressProvided);
        Assert.Contains(trayService.Notifications, item => item.Title == "插件下载开始");
        Assert.Contains(trayService.Notifications, item => item.Title == "插件下载完成");
    }

    [Fact]
    public async Task PluginActionCommand_ShouldSwitchToStopDownload_AndCancelActiveDownload()
    {
        using var sandbox = new TestConfigSandbox();
        CreatePluginManifest(Path.Combine(sandbox.RootDirectory, "runtime"), "patient-registration", "就诊登记", "1.0.2");
        var orchestrator = new StubPluginUpdateOrchestrator
        {
            HoldInstallUntilCancelled = true
        };
        var vm = new PluginCenterViewModel(
            new NullLoggerService<PluginCenterViewModel>(),
            Options.Create(CreateSettings(sandbox)),
            orchestrator);

        vm.SetAvailablePluginsForTesting("就诊登记 / patient-registration / 1.0.3");
        await vm.RefreshAsync();
        var item = vm.PluginItems[0];

        item.ActionCommand!.Execute();
        await WaitUntilAsync(() => item.ActionText == "停止下载");
        item.ActionCommand.Execute();
        await WaitUntilAsync(() => !vm.IsBusy);

        Assert.True(orchestrator.InstallOrUpdateCancellationObserved);
        Assert.Contains("下载已取消", vm.LastUpdateStatus);
    }

    [Fact]
    public async Task PluginActionCommand_ShouldMarkRequestedPluginForUninstall()
    {
        using var sandbox = new TestConfigSandbox();
        CreatePluginManifest(Path.Combine(sandbox.RootDirectory, "runtime"), "patient-registration", "就诊登记", "1.0.3");
        var orchestrator = new StubPluginUpdateOrchestrator();
        var vm = new PluginCenterViewModel(
            new NullLoggerService<PluginCenterViewModel>(),
            Options.Create(CreateSettings(sandbox)),
            orchestrator);

        await vm.RefreshAsync();
        vm.PluginItems[0].ActionCommand!.Execute();
        await WaitUntilAsync(() => !vm.IsBusy);

        Assert.Equal(1, orchestrator.UninstallCallCount);
        Assert.Equal("patient-registration", orchestrator.UninstallPluginId);
    }

    [Fact]
    public async Task RefreshAsync_ShouldExposeRestartAction_WhenUninstallMarkerExists()
    {
        using var sandbox = new TestConfigSandbox();
        CreatePluginManifest(Path.Combine(sandbox.RootDirectory, "runtime"), "patient-registration", "就诊登记", "1.0.3");
        var uninstallDirectory = Path.Combine(sandbox.RootDirectory, "staging", "uninstall-patient-registration");
        Directory.CreateDirectory(uninstallDirectory);
        await File.WriteAllTextAsync(Path.Combine(uninstallDirectory, "uninstall.json"), """
{
  "pluginId": "patient-registration",
  "requestedAt": "2026-04-28T00:00:00Z"
}
""");
        var vm = new PluginCenterViewModel(
            new NullLoggerService<PluginCenterViewModel>(),
            Options.Create(CreateSettings(sandbox)),
            new StubPluginUpdateOrchestrator());

        await vm.RefreshAsync();

        var item = Assert.Single(vm.PluginItems);
        Assert.Equal("待卸载", item.Status);
        Assert.Equal("重启生效", item.ActionText);
    }

    [Fact]
    public void PluginCenterViewAxaml_ShouldShowCurrentAndLatestVersionColumns()
    {
        var fullPath = FindRepositoryFile("digital-intelligence-bridge", "Views", "PluginCenterView.axaml");
        var xaml = File.ReadAllText(fullPath);

        Assert.Contains("当前版本", xaml);
        Assert.Contains("最新版本", xaml);
        Assert.Contains("Detail", xaml);
    }

    private static AppSettings CreateSettings(TestConfigSandbox sandbox)
    {
        return new AppSettings
        {
            Plugin = new PluginConfig
            {
                PluginDirectory = Path.Combine(sandbox.RootDirectory, "runtime")
            },
            ReleaseCenter = new ReleaseCenterConfig
            {
                RuntimePluginRoot = Path.Combine(sandbox.RootDirectory, "runtime"),
                StagingDirectory = Path.Combine(sandbox.RootDirectory, "staging")
            }
        };
    }

    private static void CreatePluginManifest(string pluginDirectory, string id, string name, string version)
    {
        Directory.CreateDirectory(pluginDirectory);
        File.WriteAllText(
            Path.Combine(pluginDirectory, "plugin.json"),
            JsonSerializer.Serialize(new
            {
                id,
                name,
                version
            }));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100; i++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException("等待插件中心命令执行完成超时。");
    }

    private static string FindRepositoryFile(params string[] relativePathParts)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(new[] { current.FullName }.Concat(relativePathParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException($"未找到仓库文件：{Path.Combine(relativePathParts)}");
    }

    private sealed class StubPluginUpdateOrchestrator : IPluginUpdateOrchestrator
    {
        private readonly PluginUpdateRunResult _result;

        public StubPluginUpdateOrchestrator()
            : this(new PluginUpdateRunResult(
                true,
                "ok",
                string.Empty,
                true,
                DateTime.Now,
                null,
                null,
                new ReleaseCenterPluginPrepareResult(true, "已生成 1 个预安装目录", string.Empty, 1, "staging")))
        {
        }

        public StubPluginUpdateOrchestrator(PluginUpdateRunResult result)
        {
            _result = result;
        }

        public int RunCallCount { get; private set; }
        public int CheckCallCount { get; private set; }
        public int InstallOrUpdateCallCount { get; private set; }
        public int UninstallCallCount { get; private set; }
        public bool HoldInstallUntilCancelled { get; init; }
        public bool InstallOrUpdateCancellationObserved { get; private set; }
        public string InstallOrUpdatePluginId { get; private set; } = string.Empty;
        public string InstallOrUpdateVersion { get; private set; } = string.Empty;
        public bool InstallOrUpdateProgressProvided { get; private set; }
        public string UninstallPluginId { get; private set; } = string.Empty;

        public Task<PluginUpdateRunResult> RunAsync(PluginUpdateTrigger trigger, CancellationToken cancellationToken = default)
        {
            RunCallCount++;
            return Task.FromResult(_result);
        }

        public Task<PluginUpdateRunResult> CheckAsync(PluginUpdateTrigger trigger, CancellationToken cancellationToken = default)
        {
            CheckCallCount++;
            return Task.FromResult(_result);
        }

        public async Task<PluginUpdateRunResult> InstallOrUpdateAsync(string pluginId, string? version = null, IProgress<ReleaseCenterDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            InstallOrUpdateCallCount++;
            InstallOrUpdatePluginId = pluginId;
            InstallOrUpdateVersion = version ?? string.Empty;
            InstallOrUpdateProgressProvided = progress is not null;
            progress?.Report(new ReleaseCenterDownloadProgress("downloading", "正在下载插件包", 50, 100, 0, TimeSpan.Zero));
            if (HoldInstallUntilCancelled)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    InstallOrUpdateCancellationObserved = true;
                    return new PluginUpdateRunResult(false, "插件下载已取消", "已取消插件包下载。", false, DateTime.Now, null, null, null);
                }
            }

            return _result;
        }

        public Task<PluginUpdateRunResult> UninstallAsync(string pluginId, CancellationToken cancellationToken = default)
        {
            UninstallCallCount++;
            UninstallPluginId = pluginId;
            return Task.FromResult(_result);
        }
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

    private sealed class StubTrayService : ITrayService
    {
        public List<(string Title, string Message)> Notifications { get; } = new();
        public bool IsWindowVisible => true;
        public bool IsExiting => false;
        public void Initialize(Avalonia.Controls.Window mainWindow) { }
        public void ShowWindow() { }
        public void HideWindow() { }
        public void ToggleWindow() { }
        public void ExitApplication() { }
        public void AddMenuItem(string header, Action callback, string? parentPath = null) { }
        public void RemoveMenuItem(string path) { }
        public void AddSeparator(string? parentPath = null) { }
        public void SetTooltip(string tooltip) { }
        public void SetShowNotifications(bool show) { }
        public void ShowNotification(string title, string message) => Notifications.Add((title, message));
    }
}
