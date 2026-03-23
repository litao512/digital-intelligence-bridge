using MedicalDrugImport.Plugin.Models;

namespace MedicalDrugImport.Plugin.Services;

public interface IDrugExcelImportService
{
    Task<DrugImportPreview> ValidateAsync(string filePath, CancellationToken cancellationToken = default);

    Task<DrugImportPreview> ValidateStructureAsync(string filePath, CancellationToken cancellationToken = default);

    IAsyncEnumerable<DrugImportRow> ReadRowsAsync(string filePath, CancellationToken cancellationToken = default);
}
