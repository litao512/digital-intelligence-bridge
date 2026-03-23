using System;
using System.Threading;
using System.Threading.Tasks;
using DigitalIntelligenceBridge.Models;

namespace DigitalIntelligenceBridge.Services;

/// <summary>
/// SQL Server 药品目录同步契约
/// </summary>
public interface ISqlServerDrugSyncService
{
    Task<DrugImportBatch> SyncBatchAsync(Guid batchId, CancellationToken cancellationToken = default);
}
