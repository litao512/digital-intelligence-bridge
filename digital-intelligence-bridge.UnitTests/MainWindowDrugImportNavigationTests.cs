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

        Assert.DoesNotContain(vm.MenuItems, item => item.Id == "drug-import" && item.ViewType == MainViewType.DrugImport);
    }

    [Fact]
    public void NavigateCommand_ShouldOpenDrugImportTab_WhenNavigatingToDrugImport()
    {
        var vm = new MainWindowViewModel(new NullLoggerService<MainWindowViewModel>());

        vm.NavigateCommand.Execute(MainViewType.DrugImport);

        Assert.Equal(MainViewType.DrugImport, vm.CurrentView);
        Assert.NotNull(vm.SelectedTab);
        Assert.Equal(TabItemType.DrugImport, vm.SelectedTab!.TabType);
        Assert.Equal("医保药品导入同步工具", vm.SelectedTab.Title);
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

