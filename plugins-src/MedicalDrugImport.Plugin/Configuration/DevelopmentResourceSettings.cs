namespace MedicalDrugImport.Plugin.Configuration;

public sealed class DevelopmentResourceSettings
{
    public string BusinessDbConnectionString { get; set; } = string.Empty;

    public string SyncTargetConnectionString { get; set; } = string.Empty;
}
