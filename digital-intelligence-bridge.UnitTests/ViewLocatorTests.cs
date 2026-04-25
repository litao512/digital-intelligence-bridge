using DigitalIntelligenceBridge.ViewModels;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class ViewLocatorTests
{
    [Fact]
    public void HostProject_ShouldNotExposeBuiltInDrugImportView()
    {
        var type = typeof(MainWindowViewModel).Assembly
            .GetType("DigitalIntelligenceBridge.Views.DrugImportView");

        Assert.Null(type);
    }

    [Fact]
    public void Match_ShouldReturnTrue_WhenDataIsViewModelBase()
    {
        var locator = new ViewLocator();

        Assert.True(locator.Match(new DummyViewModel()));
        Assert.False(locator.Match(new object()));
    }

    private sealed class DummyViewModel : ViewModelBase
    {
    }
}

