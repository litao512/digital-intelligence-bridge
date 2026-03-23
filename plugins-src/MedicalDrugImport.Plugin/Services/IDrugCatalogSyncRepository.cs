using MedicalDrugImport.Plugin.Models;

namespace MedicalDrugImport.Plugin.Services;

public interface IDrugCatalogSyncRepository
{
    Task<int> CountAffectedCatalogRowsAsync(Guid batchId, CancellationToken cancellationToken = default);

    IAsyncEnumerable<DrugImportRow> GetAffectedCatalogRowsAsync(Guid batchId, CancellationToken cancellationToken = default);
}
