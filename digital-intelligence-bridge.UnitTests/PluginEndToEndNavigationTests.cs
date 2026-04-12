using Avalonia.Controls;
using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Plugin.Abstractions;
using DigitalIntelligenceBridge.Plugin.Host;
using DigitalIntelligenceBridge.ViewModels;
using Microsoft.Extensions.Options;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class PluginEndToEndNavigationTests
{
    [Fact]
    public void LoadRuntimePlugins_ShouldDiscoverAndInitializeSamplePlugin()
    {
        using var sandbox = new TestConfigSandbox();
        PrepareRuntimePluginSandbox(sandbox.RootDirectory);

        var loadedPlugins = App.LoadRuntimePlugins(
            GetRepositoryRoot(),
            Options.Create(new AppSettings()),
            new PluginCatalogService(),
            new PluginLoaderService(),
            new TestAppLogger());

        var plugin = Assert.Single(loadedPlugins, item => item.Manifest.Id == "medical-drug-import");
        Assert.NotNull(plugin.Module);
        Assert.Single(plugin.Module!.CreateMenuItems());
    }

    [Fact]
    public void Constructor_ShouldAppendRuntimePluginMenus_AndOpenPluginPage()
    {
        using var sandbox = new TestConfigSandbox();
        PrepareRuntimePluginSandbox(sandbox.RootDirectory);

        var loadedPlugins = App.LoadRuntimePlugins(
            GetRepositoryRoot(),
            Options.Create(new AppSettings()),
            new PluginCatalogService(),
            new PluginLoaderService(),
            new TestAppLogger());
        var menus = loadedPlugins.SelectMany(plugin => plugin.Module!.CreateMenuItems()).ToList();
        var vm = new MainWindowViewModel(new TestMainWindowLogger(), Options.Create(new AppSettings()), null, null, menus, loadedPlugins);

        var pluginMenu = Assert.Single(vm.MenuItems, item => item.IsExternalPlugin);

        vm.NavigateCommand.Execute(pluginMenu.TargetKey);

        Assert.Equal(MainViewType.PluginHost, vm.CurrentView);
        var hostViewModel = Assert.IsType<PluginHostViewModel>(vm.SelectedTab!.Content);
        Assert.False(hostViewModel.HasError);
        Assert.Equal("MedicalDrugImport.Plugin.Views.MedicalDrugImportHomeView", hostViewModel.Content.GetType().FullName);
    }

    [Fact]
    public void NavigateCommand_ShouldFallbackToErrorHost_WithoutBreakingBuiltInNavigation_WhenPluginContentThrows()
    {
        var throwingPlugin = new LoadedPlugin
        {
            Manifest = new PluginManifest { Id = "broken-plugin", Name = "坏插件", Version = "0.1.0" },
            Module = new ThrowingPluginModule()
        };
        IReadOnlyList<PluginMenuItem> menus =
        [
            new PluginMenuItem { Id = "broken-plugin.home", Name = "坏插件", Icon = "x", Order = 200 }
        ];
        var vm = new MainWindowViewModel(new TestMainWindowLogger(), Options.Create(new AppSettings()), null, null, menus, [throwingPlugin]);

        vm.NavigateCommand.Execute("plugin:broken-plugin.home");

        var hostViewModel = Assert.IsType<PluginHostViewModel>(vm.SelectedTab!.Content);
        Assert.True(hostViewModel.HasError);

        vm.NavigateCommand.Execute("home");

        Assert.Equal(MainViewType.Home, vm.CurrentView);
        Assert.Equal(TabItemType.Home, vm.SelectedTab!.TabType);
    }

    private static string GetRepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }

    private static void PrepareRuntimePluginSandbox(string sandboxRoot)
    {
        var sourceDir = Path.Combine(GetRepositoryRoot(), "plugins", "MedicalDrugImport");
        var targetDir = Path.Combine(sandboxRoot, "plugins", "MedicalDrugImport");
        CopyDirectory(sourceDir, targetDir);
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var targetFile = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, targetFile, overwrite: true);
        }

        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            var childTarget = Path.Combine(targetDir, Path.GetFileName(directory));
            CopyDirectory(directory, childTarget);
        }
    }

    private sealed class ThrowingPluginModule : IPluginModule
    {
        public void Initialize(IPluginHostContext context)
        {
        }

        public PluginManifest GetManifest()
        {
            return new PluginManifest { Id = "broken-plugin", Name = "坏插件", Version = "0.1.0" };
        }

        public IReadOnlyList<PluginMenuItem> CreateMenuItems()
        {
            return [new PluginMenuItem { Id = "broken-plugin.home", Name = "坏插件", Order = 200 }];
        }

        public Control CreateContent(string menuId)
        {
            throw new InvalidOperationException("页面创建失败");
        }
    }

    private sealed class TestAppLogger : Services.ILoggerService<App>
    {
        public void LogCritical(string message, params object[] args) { }
        public void LogDebug(string message, params object[] args) { }
        public void LogError(string message, params object[] args) { }
        public void LogError(Exception exception, string message, params object[] args) { }
        public void LogInformation(string message, params object[] args) { }
        public void LogWarning(string message, params object[] args) { }
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
