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
