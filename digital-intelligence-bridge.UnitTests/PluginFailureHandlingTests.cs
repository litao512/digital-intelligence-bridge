using Avalonia.Controls;
using DigitalIntelligenceBridge.Plugin.Abstractions;
using DigitalIntelligenceBridge.Plugin.Host;
using DigitalIntelligenceBridge.ViewModels;
using Microsoft.Extensions.Options;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class PluginFailureHandlingTests
{
    [Fact]
    public void LoadPlugin_ShouldRejectPlugin_WhenMinHostVersionIsHigherThanCurrentHost()
    {
        var assemblyPath = typeof(PluginLoaderServiceTests.SamplePlugin).Assembly.Location;
        var plugin = new LoadedPlugin
        {
            PluginDirectory = Path.GetDirectoryName(assemblyPath) ?? string.Empty,
            ManifestPath = Path.Combine(Path.GetDirectoryName(assemblyPath) ?? string.Empty, "plugin.json"),
            Manifest = new PluginManifest
            {
                Id = "sample",
                Name = "样例插件",
                Version = "0.1.0",
                EntryAssembly = Path.GetFileName(assemblyPath),
                EntryType = typeof(PluginLoaderServiceTests.SamplePlugin).FullName ?? string.Empty,
                MinHostVersion = "9.9.9"
            }
        };

        var loaded = new PluginLoaderService().LoadPlugin(plugin, "1.0.0");

        Assert.Null(loaded.Module);
        Assert.Contains("9.9.9", loaded.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1.0.0", loaded.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PluginHostViewModel_ShouldIncludeBoundaryHint_WhenShowingError()
    {
        var viewModel = PluginHostViewModel.CreateError("插件页面创建失败: boom");

        var border = Assert.IsType<Border>(viewModel.Content);
        var panel = Assert.IsType<StackPanel>(border.Child);

        Assert.Contains(panel.Children.OfType<TextBlock>(), child => child.Text?.Contains("插件页面加载失败") == true);
        Assert.Contains(panel.Children.OfType<TextBlock>(), child => child.Text?.Contains("不影响其他模块") == true);
    }

    [Fact]
    public void NavigateCommand_ShouldKeepHealthyPluginUsable_WhenAnotherPluginFails()
    {
        IReadOnlyList<PluginMenuItem> menus =
        [
            new PluginMenuItem { Id = "good.home", Name = "好插件", Order = 100 },
            new PluginMenuItem { Id = "bad.home", Name = "坏插件", Order = 110 }
        ];
        IReadOnlyList<LoadedPlugin> plugins =
        [
            new LoadedPlugin
            {
                Manifest = new PluginManifest { Id = "good", Name = "好插件", Version = "0.1.0", MinHostVersion = "1.0.0" },
                Module = new HealthyPluginModule()
            },
            new LoadedPlugin
            {
                Manifest = new PluginManifest { Id = "bad", Name = "坏插件", Version = "0.1.0", MinHostVersion = "1.0.0" },
                Module = new ThrowingPluginModule()
            }
        ];

        var vm = new MainWindowViewModel(new TestMainWindowLogger(), Options.Create(new Configuration.AppSettings()), null, null, menus, plugins);

        vm.NavigateCommand.Execute("plugin:bad.home");

        var failedHost = Assert.IsType<PluginHostViewModel>(vm.SelectedTab!.Content);
        Assert.True(failedHost.HasError);

        vm.NavigateCommand.Execute("plugin:good.home");

        var healthyHost = Assert.IsType<PluginHostViewModel>(vm.SelectedTab!.Content);
        Assert.False(healthyHost.HasError);
        Assert.IsType<TextBlock>(healthyHost.Content);
    }

    private sealed class HealthyPluginModule : IPluginModule
    {
        public void Initialize(IPluginHostContext context)
        {
        }

        public PluginManifest GetManifest()
        {
            return new PluginManifest { Id = "good", Name = "好插件", Version = "0.1.0" };
        }

        public IReadOnlyList<PluginMenuItem> CreateMenuItems()
        {
            return [new PluginMenuItem { Id = "good.home", Name = "好插件", Order = 100 }];
        }

        public Control CreateContent(string menuId)
        {
            return new TextBlock { Text = $"healthy:{menuId}" };
        }
    }

    private sealed class ThrowingPluginModule : IPluginModule
    {
        public void Initialize(IPluginHostContext context)
        {
        }

        public PluginManifest GetManifest()
        {
            return new PluginManifest { Id = "bad", Name = "坏插件", Version = "0.1.0" };
        }

        public IReadOnlyList<PluginMenuItem> CreateMenuItems()
        {
            return [new PluginMenuItem { Id = "bad.home", Name = "坏插件", Order = 110 }];
        }

        public Control CreateContent(string menuId)
        {
            throw new InvalidOperationException("boom");
        }
    }

    private sealed class TestMainWindowLogger : Services.ILoggerService<MainWindowViewModel>
    {
        public void LogCritical(string message, params object[] args) { }
        public void LogDebug(string message, params object[] args) { }
        public void LogError(string message, params object[] args) { }
        public void LogError(Exception exception, string message, params object[] args) { }
        public void LogInformation(string message, params object[] args) { }
        public void LogWarning(string message, params object[] args) { }
    }
}

