using System.Collections.Generic;
using DigitalIntelligenceBridge.Models;
using DigitalIntelligenceBridge.Services;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class DrugExcelImportContractTests
{
    [Fact]
    public void Contract_ShouldExposeValidationAndStreamingTemplate()
    {
        var template = DrugExcelTemplate.Default;

        Assert.NotNull(template);
        Assert.Equal(4, template.Sheets.Count);
        Assert.Contains(template.Sheets, sheet => sheet.Name == "总表（270419）");
        Assert.Contains(template.Sheets, sheet => sheet.Columns.Contains("药品代码"));

        var serviceType = typeof(IDrugExcelImportService);
        Assert.NotNull(serviceType.GetMethod(nameof(IDrugExcelImportService.ValidateAsync)));
        Assert.NotNull(serviceType.GetMethod(nameof(IDrugExcelImportService.ValidateStructureAsync)));
        Assert.NotNull(serviceType.GetMethod(nameof(IDrugExcelImportService.ReadRowsAsync)));
        Assert.Equal(
            typeof(IAsyncEnumerable<DrugImportRow>),
            serviceType.GetMethod(nameof(IDrugExcelImportService.ReadRowsAsync))!.ReturnType);
    }
}

