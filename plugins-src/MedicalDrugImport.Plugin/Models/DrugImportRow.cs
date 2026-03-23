namespace MedicalDrugImport.Plugin.Models;

public class DrugImportRow
{
    public Guid BatchId { get; set; }
    public string SourceSheet { get; set; } = string.Empty;
    public int RowNumber { get; set; }
    public string BusinessKey { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public Dictionary<string, string?> RawData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string?> NormalizedData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
