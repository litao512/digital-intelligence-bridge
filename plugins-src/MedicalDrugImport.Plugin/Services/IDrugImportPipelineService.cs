using MedicalDrugImport.Plugin.Models;

namespace MedicalDrugImport.Plugin.Services;

public interface IDrugImportPipelineService
{
    Task<DrugImportBatch> ImportAsync(string filePath, CancellationToken cancellationToken = default);
}
