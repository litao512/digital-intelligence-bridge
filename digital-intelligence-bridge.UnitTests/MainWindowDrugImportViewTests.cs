using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class MainWindowDrugImportViewTests
{
    [Fact]
    public void MainWindowAxaml_ShouldRenderDrugImportView_WhenDrugImportTabSelected()
    {
        var filePath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "digital-intelligence-bridge",
            "Views",
            "MainWindow.axaml");
        var fullPath = Path.GetFullPath(filePath);

        var xaml = File.ReadAllText(fullPath);

        Assert.Contains("ConverterParameter=DrugImport", xaml);
        Assert.Contains("<views:DrugImportView", xaml);
    }
}
