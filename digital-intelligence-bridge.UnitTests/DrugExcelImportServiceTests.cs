using DigitalIntelligenceBridge.Services;
using MedicalDrugImport.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class DrugExcelImportServiceTests
{
    [Fact]
    public void AddApplicationServices_ShouldNotRegisterBuiltInDrugExcelImportService()
    {
        using var sandbox = new TestConfigSandbox();
        var services = new ServiceCollection();

        services.AddApplicationServices();
        using var provider = services.BuildServiceProvider();

        Assert.Null(provider.GetService<IDrugExcelImportService>());
    }

    [Fact]
    public void AddApplicationServices_ShouldNotRegisterBuiltInDrugImportPipelineDependencies()
    {
        using var sandbox = new TestConfigSandbox();
        var services = new ServiceCollection();

        services.AddApplicationServices();
        using var provider = services.BuildServiceProvider();

        Assert.Null(provider.GetService<IDrugImportRepository>());
        Assert.Null(provider.GetService<IDrugCatalogSyncRepository>());
        Assert.Null(provider.GetService<IDrugImportPipelineService>());
        Assert.Null(provider.GetService<ISqlServerDrugSyncService>());
    }
}

