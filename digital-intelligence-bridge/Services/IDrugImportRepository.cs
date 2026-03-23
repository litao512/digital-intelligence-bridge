using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DigitalIntelligenceBridge.Models;

namespace DigitalIntelligenceBridge.Services;

/// <summary>
/// PostgreSQL 医保药品导入仓储契约
/// </summary>
public interface IDrugImportRepository
{
    Task ExecuteInImportSessionAsync(
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default);

    Task<DrugImportBatch> CreateBatchAsync(string sourceFile, CancellationToken cancellationToken = default);

    Task InsertRawAsync(DrugImportRow row, CancellationToken cancellationToken = default);

    Task InsertRawBatchAsync(IReadOnlyList<DrugImportRow> rows, CancellationToken cancellationToken = default);

    Task InsertCleanAsync(DrugImportRow row, CancellationToken cancellationToken = default);

    Task InsertCleanBatchAsync(IReadOnlyList<DrugImportRow> rows, CancellationToken cancellationToken = default);

    Task InsertErrorAsync(DrugImportRow row, CancellationToken cancellationToken = default);

    Task InsertErrorBatchAsync(IReadOnlyList<DrugImportRow> rows, CancellationToken cancellationToken = default);

    Task MergeBatchAsync(Guid batchId, CancellationToken cancellationToken = default);
}
