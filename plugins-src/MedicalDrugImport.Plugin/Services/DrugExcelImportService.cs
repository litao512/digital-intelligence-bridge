using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Xml;
using MedicalDrugImport.Plugin.Models;

namespace MedicalDrugImport.Plugin.Services;

public class DrugExcelImportService : IDrugExcelImportService
{
    private const string OfficeRelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public async Task<DrugImportPreview> ValidateAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return await ValidateInternalAsync(filePath, includeRowCounts: true, cancellationToken);
    }

    public async Task<DrugImportPreview> ValidateStructureAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return await ValidateInternalAsync(filePath, includeRowCounts: false, cancellationToken);
    }

    private async Task<DrugImportPreview> ValidateInternalAsync(
        string filePath,
        bool includeRowCounts,
        CancellationToken cancellationToken)
    {
        var preview = new DrugImportPreview
        {
            FilePath = filePath
        };

        if (!File.Exists(filePath))
        {
            preview.Errors.Add($"文件不存在: {filePath}");
            preview.Summary = "文件不存在";
            return preview;
        }

        using var archive = ZipFile.OpenRead(filePath);
        var sharedStrings = ReadSharedStrings(archive);
        var workbook = OpenXmlEntry(archive, "xl/workbook.xml");
        var workbookRels = OpenXmlEntry(archive, "xl/_rels/workbook.xml.rels");
        var workbookSheets = ReadWorkbookSheets(workbook, workbookRels);

        foreach (var templateSheet in DrugExcelTemplate.Default.Sheets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!workbookSheets.TryGetValue(templateSheet.Name, out var worksheetPath))
            {
                preview.Errors.Add($"缺少工作表: {templateSheet.Name}");
                continue;
            }

            var worksheet = OpenXmlEntry(archive, worksheetPath);
            var analysis = AnalyzeWorksheet(
                worksheet,
                templateSheet.Columns,
                sharedStrings,
                includeRowCounts,
                cancellationToken);
            if (includeRowCounts)
            {
                preview.SheetRowCounts[templateSheet.Name] = analysis.RowCount;
            }

            if (!analysis.HeaderMatches)
            {
                preview.Errors.Add($"工作表表头不匹配: {templateSheet.Name}");
            }
        }

        preview.IsValid = preview.Errors.Count == 0;
        preview.Summary = preview.IsValid ? "工作表完整" : string.Join("；", preview.Errors);
        await Task.CompletedTask;
        return preview;
    }

    public async IAsyncEnumerable<DrugImportRow> ReadRowsAsync(
        string filePath,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            yield break;
        }

        using var archive = ZipFile.OpenRead(filePath);
        var sharedStrings = ReadSharedStrings(archive);
        var workbook = OpenXmlEntry(archive, "xl/workbook.xml");
        var workbookRels = OpenXmlEntry(archive, "xl/_rels/workbook.xml.rels");
        var workbookSheets = ReadWorkbookSheets(workbook, workbookRels);

        foreach (var templateSheet in DrugExcelTemplate.Default.Sheets)
        {
            if (!workbookSheets.TryGetValue(templateSheet.Name, out var worksheetPath))
            {
                continue;
            }

            var worksheet = OpenXmlEntry(archive, worksheetPath);
            foreach (var row in ReadWorksheetRows(templateSheet, worksheet, sharedStrings, cancellationToken))
            {
                yield return row;
                await Task.Yield();
            }
        }
    }

    private static Stream OpenXmlEntry(ZipArchive archive, string path)
    {
        return archive.GetEntry(path)?.Open()
               ?? throw new InvalidDataException($"缺少 OpenXML 部件: {path}");
    }

    private static Dictionary<string, string> ReadWorkbookSheets(Stream workbookStream, Stream workbookRelsStream)
    {
        var relMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using (workbookRelsStream)
        using (var reader = XmlReader.Create(workbookRelsStream, new XmlReaderSettings { IgnoreWhitespace = true }))
        {
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "Relationship")
                {
                    continue;
                }

                var id = reader.GetAttribute("Id");
                var target = reader.GetAttribute("Target");
                if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(target))
                {
                    relMap[id] = $"xl/{target.TrimStart('/')}";
                }
            }
        }

        var sheets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using (workbookStream)
        using (var reader = XmlReader.Create(workbookStream, new XmlReaderSettings { IgnoreWhitespace = true }))
        {
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "sheet")
                {
                    continue;
                }

                var name = reader.GetAttribute("name");
                var relId = reader.GetAttribute("id", OfficeRelationshipNamespace);
                if (!string.IsNullOrWhiteSpace(name) &&
                    !string.IsNullOrWhiteSpace(relId) &&
                    relMap.TryGetValue(relId, out var worksheetPath))
                {
                    sheets[name] = worksheetPath;
                }
            }
        }

        return sheets;
    }

    private static WorksheetAnalysis AnalyzeWorksheet(
        Stream worksheetStream,
        IReadOnlyList<string> expectedHeader,
        IReadOnlyList<string> sharedStrings,
        bool includeRowCounts,
        CancellationToken cancellationToken)
    {
        using (worksheetStream)
        {
            using var reader = XmlReader.Create(worksheetStream, new XmlReaderSettings { IgnoreWhitespace = true });

            var rowIndex = 0;
            var rowCount = 0;
            var headerMatches = false;

            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "row")
                {
                    continue;
                }

                rowIndex++;
                using var rowReader = reader.ReadSubtree();
                var values = ReadRowValues(rowReader, sharedStrings);

                if (rowIndex == 1)
                {
                    headerMatches = expectedHeader.SequenceEqual(values);
                    if (!headerMatches || !includeRowCounts)
                    {
                        break;
                    }

                    continue;
                }

                if (includeRowCounts && values.Any(value => !string.IsNullOrWhiteSpace(value)))
                {
                    rowCount++;
                }
            }

            return new WorksheetAnalysis(headerMatches, rowCount);
        }
    }

    private static IEnumerable<DrugImportRow> ReadWorksheetRows(
        DrugExcelSheetTemplate templateSheet,
        Stream worksheetStream,
        IReadOnlyList<string> sharedStrings,
        CancellationToken cancellationToken)
    {
        using (worksheetStream)
        {
            using var reader = XmlReader.Create(worksheetStream, new XmlReaderSettings { IgnoreWhitespace = true });

            var rowIndex = 0;
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "row")
                {
                    continue;
                }

                rowIndex++;
                if (rowIndex == 1)
                {
                    continue;
                }

                using var rowReader = reader.ReadSubtree();
                var values = ReadRowValues(rowReader, sharedStrings);
                if (!values.Any(value => !string.IsNullOrWhiteSpace(value)))
                {
                    continue;
                }

                yield return CreateImportRow(templateSheet, rowIndex, values);
            }
        }
    }

    private static List<string> ReadRowValues(XmlReader rowReader, IReadOnlyList<string> sharedStrings)
    {
        var values = new List<string>();
        while (rowReader.Read())
        {
            if (rowReader.NodeType != XmlNodeType.Element || rowReader.LocalName != "c")
            {
                continue;
            }

            var columnIndex = GetColumnIndex(rowReader.GetAttribute("r"));
            EnsureCapacity(values, columnIndex + 1);
            using var cellReader = rowReader.ReadSubtree();
            values[columnIndex] = ReadCellValue(cellReader, rowReader.GetAttribute("t"), sharedStrings);
        }

        return values;
    }

    private static string ReadCellValue(XmlReader cellReader, string? cellType, IReadOnlyList<string> sharedStrings)
    {
        while (cellReader.Read())
        {
            if (cellReader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (cellReader.LocalName == "t")
            {
                return cellReader.ReadElementContentAsString();
            }

            if (cellReader.LocalName != "v")
            {
                continue;
            }

            var rawValue = cellReader.ReadElementContentAsString();
            if (string.Equals(cellType, "s", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(rawValue, out var sharedStringIndex) &&
                sharedStringIndex >= 0 &&
                sharedStringIndex < sharedStrings.Count)
            {
                return sharedStrings[sharedStringIndex];
            }

            return rawValue;
        }

        return string.Empty;
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return Array.Empty<string>();
        }

        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { IgnoreWhitespace = true });

        var values = new List<string>();
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "si")
            {
                continue;
            }

            using var itemReader = reader.ReadSubtree();
            values.Add(ReadSharedStringItem(itemReader));
        }

        return values;
    }

    private static string ReadSharedStringItem(XmlReader itemReader)
    {
        var parts = new List<string>();
        while (itemReader.Read())
        {
            if (itemReader.NodeType == XmlNodeType.Element && itemReader.LocalName == "t")
            {
                parts.Add(itemReader.ReadElementContentAsString());
            }
        }

        return string.Concat(parts);
    }

    private static DrugImportRow CreateImportRow(
        DrugExcelSheetTemplate templateSheet,
        int rowNumber,
        IReadOnlyList<string> values)
    {
        var row = new DrugImportRow
        {
            SourceSheet = templateSheet.Name,
            RowNumber = rowNumber
        };

        for (var i = 0; i < templateSheet.Columns.Count; i++)
        {
            var column = templateSheet.Columns[i];
            var value = i < values.Count ? values[i]?.Trim() : string.Empty;
            row.RawData[column] = string.IsNullOrWhiteSpace(value) ? null : value;
        }

        var drugCode = row.RawData.GetValueOrDefault("药品代码");
        row.BusinessKey = drugCode ?? string.Empty;

        AddNormalizedValue(row, "drug_code", "药品代码");
        AddNormalizedValue(row, "drug_name_cn", "注册名称");
        AddNormalizedValue(row, "dosage_form", "剂型");
        AddNormalizedValue(row, "specification", "规格");
        AddNormalizedValue(row, "packaging_material", "包装材质");
        AddNormalizedValue(row, "min_pack_qty", "最小包装数量");
        AddNormalizedValue(row, "min_dose_unit", "最小制剂单位");
        AddNormalizedValue(row, "min_pack_unit", "最小包装单位");
        AddNormalizedValue(row, "approval_no", "批准文号");
        AddNormalizedValue(row, "drug_base_code", "药品本位码");
        AddNormalizedValue(row, "marketing_status", "市场状态");
        AddNormalizedValue(row, "remarks", "备注");
        AddNormalizedValue(row, "goods_id", "goods_id");
        AddNormalizedValue(row, "old_drug_code", "曾用码");
        AddNormalizedValue(row, "old_goods_id", "曾用goods_id");

        if (string.IsNullOrWhiteSpace(row.BusinessKey))
        {
            row.ErrorCode = "MISSING_DRUG_CODE";
            row.ErrorMessage = "药品代码不能为空";
        }

        return row;
    }

    private static void AddNormalizedValue(DrugImportRow row, string targetKey, string sourceColumn)
    {
        var value = row.RawData.GetValueOrDefault(sourceColumn);
        if (!string.IsNullOrWhiteSpace(value))
        {
            row.NormalizedData[targetKey] = value;
        }
    }

    private static int GetColumnIndex(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
        {
            return 0;
        }

        var column = 0;
        foreach (var ch in cellReference)
        {
            if (!char.IsLetter(ch))
            {
                break;
            }

            column = (column * 26) + (char.ToUpperInvariant(ch) - 'A' + 1);
        }

        return Math.Max(0, column - 1);
    }

    private static void EnsureCapacity(List<string> values, int length)
    {
        while (values.Count < length)
        {
            values.Add(string.Empty);
        }
    }

    private sealed record WorksheetAnalysis(bool HeaderMatches, int RowCount);
}
