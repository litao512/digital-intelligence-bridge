using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using DigitalIntelligenceBridge.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class DrugExcelImportServiceTests
{
    [Fact]
    public async Task ValidateAsync_ShouldFail_WhenRequiredSheetMissing()
    {
        var filePath = CreateWorkbook(includeRelationSheet: false);

        try
        {
            var service = new DrugExcelImportService();

            var preview = await service.ValidateAsync(filePath);

            Assert.False(preview.IsValid);
            Assert.Contains(preview.Errors, error => error.Contains("关联关系表"));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ValidateAsync_ShouldFail_WhenHeaderMismatch()
    {
        var filePath = CreateWorkbook(mutatedTotalHeader: "药品编码");

        try
        {
            var service = new DrugExcelImportService();

            var preview = await service.ValidateAsync(filePath);

            Assert.False(preview.IsValid);
            Assert.Contains(preview.Errors, error => error.Contains("总表（270419）"));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ValidateAsync_ShouldCountOnlyNonEmptyRows_WhenWorkbookMatchesTemplate()
    {
        var filePath = CreateWorkbook();

        try
        {
            var service = new DrugExcelImportService();

            var preview = await service.ValidateAsync(filePath);

            Assert.True(preview.IsValid);
            Assert.Equal(2, preview.SheetRowCounts["总表（270419）"]);
            Assert.Equal(1, preview.SheetRowCounts["新增（559）"]);
            Assert.Equal(1, preview.SheetRowCounts["变更（449）"]);
            Assert.Equal(1, preview.SheetRowCounts["关联关系表"]);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void AddApplicationServices_ShouldRegisterDrugExcelImportService()
    {
        var services = new ServiceCollection();

        services.AddApplicationServices();
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IDrugExcelImportService>());
    }

    [Fact]
    public async Task ReadRowsAsync_ShouldPopulateBusinessKeyAndMappedFields_WhenWorkbookMatchesTemplate()
    {
        var filePath = CreateWorkbook();

        try
        {
            var service = new DrugExcelImportService();

            var rows = new List<DigitalIntelligenceBridge.Models.DrugImportRow>();
            await foreach (var row in service.ReadRowsAsync(filePath))
            {
                rows.Add(row);
            }

            var totalRow = Assert.Single(rows, x => x.SourceSheet == "总表（270419）" && x.RowNumber == 2);
            Assert.Equal("A001", totalRow.BusinessKey);
            Assert.Equal("A001", totalRow.RawData["药品代码"]);
            Assert.Equal("药品甲", totalRow.RawData["注册名称"]);
            Assert.Equal("A001", totalRow.NormalizedData["drug_code"]);
            Assert.Equal("药品甲", totalRow.NormalizedData["drug_name_cn"]);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void AddApplicationServices_ShouldRegisterDrugImportPipelineDependencies()
    {
        var services = new ServiceCollection();

        services.AddApplicationServices();
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IDrugImportRepository>());
        Assert.NotNull(provider.GetRequiredService<IDrugCatalogSyncRepository>());
        Assert.NotNull(provider.GetRequiredService<IDrugImportPipelineService>());
        Assert.NotNull(provider.GetRequiredService<ISqlServerDrugSyncService>());
    }

    private static string CreateWorkbook(bool includeRelationSheet = true, string? mutatedTotalHeader = null)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"drug-import-{Guid.NewGuid():N}.xlsx");
        using var archive = ZipFile.Open(filePath, ZipArchiveMode.Create);

        WriteEntry(archive, "[Content_Types].xml",
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
              <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
              <Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
              <Override PartName="/xl/worksheets/sheet3.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
              <Override PartName="/xl/worksheets/sheet4.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
            </Types>
            """);

        WriteEntry(archive, "_rels/.rels",
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
            </Relationships>
            """);

        WriteEntry(archive, "xl/workbook.xml", CreateWorkbookXml(includeRelationSheet));
        WriteEntry(archive, "xl/_rels/workbook.xml.rels", CreateWorkbookRelsXml(includeRelationSheet));

        WriteEntry(
            archive,
            "xl/worksheets/sheet1.xml",
            CreateSheetXml(
                DrugExcelTemplate.Default.Sheets[0].Columns,
                new[]
                {
                    CreateRow(DrugExcelTemplate.Default.Sheets[0].Columns, mutatedTotalHeader: mutatedTotalHeader),
                    CreateRow(DrugExcelTemplate.Default.Sheets[0].Columns, drugCode: "A001", name: "药品甲"),
                    CreateEmptyRow(DrugExcelTemplate.Default.Sheets[0].Columns.Count),
                    CreateRow(DrugExcelTemplate.Default.Sheets[0].Columns, drugCode: "A002", name: "药品乙")
                }));

        WriteEntry(
            archive,
            "xl/worksheets/sheet2.xml",
            CreateSheetXml(
                DrugExcelTemplate.Default.Sheets[1].Columns,
                new[]
                {
                    CreateRow(DrugExcelTemplate.Default.Sheets[1].Columns),
                    CreateRow(DrugExcelTemplate.Default.Sheets[1].Columns, drugCode: "N001", name: "新增药品"),
                    CreateEmptyRow(DrugExcelTemplate.Default.Sheets[1].Columns.Count)
                }));

        WriteEntry(
            archive,
            "xl/worksheets/sheet3.xml",
            CreateSheetXml(
                DrugExcelTemplate.Default.Sheets[2].Columns,
                new[]
                {
                    CreateRow(DrugExcelTemplate.Default.Sheets[2].Columns),
                    CreateRow(DrugExcelTemplate.Default.Sheets[2].Columns, drugCode: "C001", name: "变更药品")
                }));

        WriteEntry(
            archive,
            "xl/worksheets/sheet4.xml",
            CreateSheetXml(
                DrugExcelTemplate.Default.Sheets[3].Columns,
                includeRelationSheet
                    ? new[]
                    {
                        CreateRow(DrugExcelTemplate.Default.Sheets[3].Columns),
                        CreateRelationRow()
                    }
                    : new[] { CreateRow(DrugExcelTemplate.Default.Sheets[3].Columns) }));

        return filePath;
    }

    private static string CreateWorkbookXml(bool includeRelationSheet)
    {
        var sheets = new StringBuilder();
        sheets.Append("""<sheet name="总表（270419）" sheetId="1" r:id="rId1"/>""");
        sheets.Append("""<sheet name="新增（559）" sheetId="2" r:id="rId2"/>""");
        sheets.Append("""<sheet name="变更（449）" sheetId="3" r:id="rId3"/>""");
        if (includeRelationSheet)
        {
            sheets.Append("""<sheet name="关联关系表" sheetId="4" r:id="rId4"/>""");
        }

        return $$"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                  xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <sheets>{{sheets}}</sheets>
        </workbook>
        """;
    }

    private static string CreateWorkbookRelsXml(bool includeRelationSheet)
    {
        var rels = new StringBuilder();
        rels.Append("""<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>""");
        rels.Append("""<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/>""");
        rels.Append("""<Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet3.xml"/>""");
        if (includeRelationSheet)
        {
            rels.Append("""<Relationship Id="rId4" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet4.xml"/>""");
        }

        return $$"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          {{rels}}
        </Relationships>
        """;
    }

    private static string CreateSheetXml(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        var sheetData = new XElement("sheetData");
        var rowIndex = 1;
        foreach (var row in rows)
        {
            var rowElement = new XElement("row", new XAttribute("r", rowIndex));
            for (var i = 0; i < row.Count; i++)
            {
                if (string.IsNullOrEmpty(row[i]))
                {
                    continue;
                }

                rowElement.Add(
                    new XElement(
                        "c",
                        new XAttribute("r", $"{ColumnName(i + 1)}{rowIndex}"),
                        new XAttribute("t", "inlineStr"),
                        new XElement("is", new XElement("t", row[i]))));
            }

            sheetData.Add(rowElement);
            rowIndex++;
        }

        var document = new XDocument(
            new XElement(
                XName.Get("worksheet", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"),
                sheetData.Elements()));

        return document.ToString(SaveOptions.DisableFormatting);
    }

    private static IReadOnlyList<string> CreateRow(
        IReadOnlyList<string> headers,
        string? drugCode = null,
        string? name = null,
        string? mutatedTotalHeader = null)
    {
        var row = headers.ToArray();
        if (drugCode is null && name is null)
        {
            if (!string.IsNullOrWhiteSpace(mutatedTotalHeader))
            {
                row[0] = mutatedTotalHeader;
            }

            return row;
        }

        Array.Fill(row, string.Empty);
        row[0] = drugCode ?? string.Empty;

        var nameIndex = headers.ToList().IndexOf("注册名称");
        if (nameIndex >= 0)
        {
            row[nameIndex] = name ?? string.Empty;
        }

        return row;
    }

    private static IReadOnlyList<string> CreateRelationRow()
    {
        return
        [
            "R001",
            "11111111-1111-1111-1111-111111111111",
            "OLD001",
            "22222222-2222-2222-2222-222222222222"
        ];
    }

    private static IReadOnlyList<string> CreateEmptyRow(int columnCount) => Enumerable.Repeat(string.Empty, columnCount).ToArray();

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string ColumnName(int index)
    {
        var name = string.Empty;
        while (index > 0)
        {
            index--;
            name = (char)('A' + (index % 26)) + name;
            index /= 26;
        }

        return name;
    }
}
