using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Services;
using DigitalIntelligenceBridge.ViewModels;
using Microsoft.Extensions.Options;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class MainWindowDrugImportNavigationTests
{
    [Fact]
    public void Constructor_ShouldNotIncludeDrugImportMenu()
    {
        var settings = new AppSettings();

        var vm = new MainWindowViewModel(
            new NullLoggerService<MainWindowViewModel>(),
            Options.Create(settings));

        Assert.DoesNotContain(vm.MenuItems, item => item.Id == "drug-import");
    }

    [Fact]
    public void NavigateCommand_ShouldIgnoreLegacyDrugImportTarget()
    {
        var vm = new MainWindowViewModel(new NullLoggerService<MainWindowViewModel>());
        var initialTab = vm.SelectedTab;

        vm.NavigateCommand.Execute("drug-import");

        Assert.Equal(MainViewType.Home, vm.CurrentView);
        Assert.Same(initialTab, vm.SelectedTab);
        Assert.DoesNotContain("DrugImport", Enum.GetNames(typeof(MainViewType)));
        Assert.DoesNotContain("DrugImport", Enum.GetNames(typeof(TabItemType)));
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

