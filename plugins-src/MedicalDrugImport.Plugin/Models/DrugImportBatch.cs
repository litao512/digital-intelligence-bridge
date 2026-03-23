namespace MedicalDrugImport.Plugin.Models;

public class DrugImportBatch
{
    public Guid BatchId { get; set; } = Guid.NewGuid();
    public string SourceFile { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public int RawCount { get; set; }
    public int CleanCount { get; set; }
    public int ErrorCount { get; set; }
    public int SyncInsertCount { get; set; }
    public int SyncUpdateCount { get; set; }
    public DateTimeOffset? ValidatedAt { get; set; }
    public DateTimeOffset? ImportedAt { get; set; }
    public DateTimeOffset? SyncedAt { get; set; }
    public string LastError { get; set; } = string.Empty;
}
