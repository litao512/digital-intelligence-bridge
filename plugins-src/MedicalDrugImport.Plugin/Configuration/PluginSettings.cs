using System.Collections.Generic;

namespace MedicalDrugImport.Plugin.Configuration;

public class PluginSettings
{
    public DevelopmentModeSettings DevelopmentMode { get; set; } = new();

    public ExcelSettings Excel { get; set; } = new();

    public SqlServerSettings SqlServer { get; set; } = new();

    public ImportSettings Import { get; set; } = new();
}

public class DevelopmentModeSettings
{
    public bool Enabled { get; set; }
}

public class ExcelSettings
{
    public List<string> RequiredSheets { get; set; } =
    [
        "总表（270419）",
        "新增（559）",
        "变更（449）",
        "关联关系表"
    ];
}

public class SqlServerSettings
{
    public bool EnableWrites { get; set; }
}

public class ImportSettings
{
    public int BatchSize { get; set; } = 1000;

    public int MaxSyncRowsPerRun { get; set; } = 50;

    public bool AllowUnsafeFullSync { get; set; }
}
