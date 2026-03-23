using System;
using System.Collections.Generic;
using System.Threading;
using DigitalIntelligenceBridge.Models;

namespace DigitalIntelligenceBridge.Services;

/// <summary>
/// PostgreSQL 侧待同步药品目录查询契约
/// </summary>
public interface IDrugCatalogSyncRepository
{
    IAsyncEnumerable<DrugImportRow> GetAffectedCatalogRowsAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);
}
