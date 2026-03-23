using MedicalDrugImport.Plugin.Models;

namespace MedicalDrugImport.Plugin.Services;

public interface IDrugImportRepository
{
    Task ExecuteInImportSessionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default);

    Task<DrugImportBatch> CreateBatchAsync(string sourceFile, CancellationToken cancellationToken = default);

    Task InsertRawBatchAsync(IReadOnlyList<DrugImportRow> rows, CancellationToken cancellationToken = default);

    Task InsertCleanBatchAsync(IReadOnlyList<DrugImportRow> rows, CancellationToken cancellationToken = default);

    Task InsertErrorBatchAsync(IReadOnlyList<DrugImportRow> rows, CancellationToken cancellationToken = default);

    Task MergeBatchAsync(Guid batchId, CancellationToken cancellationToken = default);
}
