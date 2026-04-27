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

    private sealed class StubPluginUpdateOrchestrator(PluginUpdateRunResult result) : IPluginUpdateOrchestrator
    {
        public Task<PluginUpdateRunResult> RunAsync(PluginUpdateTrigger trigger, CancellationToken cancellationToken = default)
            => Task.FromResult(result);
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
