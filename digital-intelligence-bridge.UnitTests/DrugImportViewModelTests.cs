using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class DrugImportViewModelTests
{
    [Fact]
    public void HostProject_ShouldNotContainBuiltInDrugImportViewModel()
    {
        var type = typeof(DigitalIntelligenceBridge.ViewModels.MainWindowViewModel).Assembly
            .GetType("DigitalIntelligenceBridge.ViewModels.DrugImportViewModel");

        Assert.Null(type);
    }
}
