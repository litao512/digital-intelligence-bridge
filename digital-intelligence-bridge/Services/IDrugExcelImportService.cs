using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DigitalIntelligenceBridge.Models;

namespace DigitalIntelligenceBridge.Services;

/// <summary>
/// 医保药品 Excel 导入契约
/// </summary>
public interface IDrugExcelImportService
{
    Task<DrugImportPreview> ValidateAsync(string filePath, CancellationToken cancellationToken = default);

    Task<DrugImportPreview> ValidateStructureAsync(string filePath, CancellationToken cancellationToken = default);

    IAsyncEnumerable<DrugImportRow> ReadRowsAsync(string filePath, CancellationToken cancellationToken = default);
}
