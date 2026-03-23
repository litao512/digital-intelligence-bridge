using System.Threading;
using System.Threading.Tasks;
using DigitalIntelligenceBridge.Models;

namespace DigitalIntelligenceBridge.Services;

/// <summary>
/// 医保药品导入流水线契约
/// </summary>
public interface IDrugImportPipelineService
{
    Task<DrugImportBatch> ImportAsync(string filePath, CancellationToken cancellationToken = default);
}
