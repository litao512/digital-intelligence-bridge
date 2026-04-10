using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Plugin.Abstractions;
using DigitalIntelligenceBridge.ViewModels;
using Microsoft.Extensions.Options;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class MainWindowPluginNavigationTests
{
    [Fact]
    public void Constructor_ShouldAppendExternalPluginMenus_WithoutReusingInstalledPluginSemantics()
    {
        var settings = new AppSettings();
        IReadOnlyList<PluginMenuItem> externalMenus =
        [
            new PluginMenuItem { Id = "medical-drug-import.home", Name = "医保导入插件", Icon = "🧩", Order = 30 }
        ];

        var vm = new MainWindowViewModel(new TestLogger(), Options.Create(settings), null, null, externalMenus);

        var pluginMenu = Assert.Single(vm.MenuItems, item => item.IsExternalPlugin);
        Assert.Contains(vm.MenuItems, item => item.Id == "home" && item.ViewType == MainViewType.Home);
        Assert.Equal("medical-drug-import.home", pluginMenu.Id);
        Assert.Equal(MainViewType.PluginHost, pluginMenu.ViewType);
        Assert.True(pluginMenu.IsInstalled);
        Assert.False(pluginMenu.IsPlaceholder);
        Assert.Equal("plugin:medical-drug-import.home", pluginMenu.TargetKey);
    }

    [Fact]
    public void Constructor_ShouldNotCreateBuiltInDrugImportMenu_EvenWhenEnabled()
    {
        var settings = new AppSettings
        {
            MedicalDrugImport = new MedicalDrugImportConfig
            {
                Enabled = true
            }
        };

        var vm = new MainWindowViewModel(new TestLogger(), Options.Create(settings));

        Assert.DoesNotContain(vm.MenuItems, item => item.Id == "drug-import");
    }

    [Fact]
    public void NavigateCommand_ShouldOpenPluginHostTab_WhenNavigatingToExternalPluginTarget()
    {
        var settings = new AppSettings();
        IReadOnlyList<PluginMenuItem> externalMenus =
        [
            new PluginMenuItem { Id = "medical-drug-import.home", Name = "医保导入插件", Icon = "🧩", Order = 30 }
        ];
        var vm = new MainWindowViewModel(new TestLogger(), Options.Create(settings), null, null, externalMenus);

        vm.NavigateCommand.Execute("plugin:medical-drug-import.home");

        Assert.Equal(MainViewType.PluginHost, vm.CurrentView);
        Assert.Equal(TabItemType.PluginHost, vm.SelectedTab!.TabType);
        Assert.Equal("plugin:medical-drug-import.home", vm.SelectedTab.TargetKey);
        Assert.Contains(vm.MenuItems, item => item.TargetKey == "plugin:medical-drug-import.home" && item.IsSelected);
    }

    private sealed class TestLogger : Services.ILoggerService<MainWindowViewModel>
    {
        public void LogCritical(string message, params object[] args) { }
        public void LogDebug(string message, params object[] args) { }
        public void LogError(string message, params object[] args) { }
        public void LogError(Exception exception, string message, params object[] args) { }
        public void LogInformation(string message, params object[] args) { }
        public void LogWarning(string message, params object[] args) { }
    }
}

