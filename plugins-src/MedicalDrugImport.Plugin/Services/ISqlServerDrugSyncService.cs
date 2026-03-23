using MedicalDrugImport.Plugin.Models;

namespace MedicalDrugImport.Plugin.Services;

public interface ISqlServerDrugSyncService
{
    Task<DrugImportBatch> PreviewBatchAsync(Guid batchId, CancellationToken cancellationToken = default);

    Task<DrugImportBatch> SyncBatchAsync(Guid batchId, CancellationToken cancellationToken = default);
}
