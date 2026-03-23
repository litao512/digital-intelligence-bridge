using System.Collections.Generic;

namespace DigitalIntelligenceBridge.Models;

/// <summary>
/// Excel 预检结果
/// </summary>
public class DrugImportPreview
{
    public string FilePath { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string Summary { get; set; } = string.Empty;
    public Dictionary<string, int> SheetRowCounts { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}
