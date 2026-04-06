using System.Collections.Generic;
using DigitalIntelligenceBridge.Models;
using DigitalIntelligenceBridge.Services;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class DrugImportRepositoryContractTests
{
    [Fact]
    public void ImportRepositoryContract_ShouldCoverBatchRawCleanErrorAndMerge()
    {
        var type = typeof(IDrugImportRepository);

        Assert.NotNull(type.GetMethod(nameof(IDrugImportRepository.CreateBatchAsync)));
        Assert.NotNull(type.GetMethod(nameof(IDrugImportRepository.InsertRawAsync)));
        Assert.NotNull(type.GetMethod(nameof(IDrugImportRepository.InsertRawBatchAsync)));
        Assert.NotNull(type.GetMethod(nameof(IDrugImportRepository.InsertCleanAsync)));
        Assert.NotNull(type.GetMethod(nameof(IDrugImportRepository.InsertCleanBatchAsync)));
        Assert.NotNull(type.GetMethod(nameof(IDrugImportRepository.InsertErrorAsync)));
        Assert.NotNull(type.GetMethod(nameof(IDrugImportRepository.InsertErrorBatchAsync)));
        Assert.NotNull(type.GetMethod(nameof(IDrugImportRepository.MergeBatchAsync)));
        Assert.NotNull(type.GetMethod(nameof(IDrugImportRepository.ExecuteInImportSessionAsync)));
        Assert.Equal(
            typeof(Task<DrugImportBatch>),
            type.GetMethod(nameof(IDrugImportRepository.CreateBatchAsync))!.ReturnType);
        Assert.Equal(
            typeof(Task),
            type.GetMethod(nameof(IDrugImportRepository.ExecuteInImportSessionAsync))!.ReturnType);
    }

    [Fact]
    public void SyncRepositoryContract_ShouldExposeAffectedCatalogRowsByBatch()
    {
        var type = typeof(IDrugCatalogSyncRepository);

        Assert.NotNull(type.GetMethod(nameof(IDrugCatalogSyncRepository.GetAffectedCatalogRowsAsync)));
        Assert.Equal(
            typeof(IAsyncEnumerable<DrugImportRow>),
            type.GetMethod(nameof(IDrugCatalogSyncRepository.GetAffectedCatalogRowsAsync))!.ReturnType);
    }
}

