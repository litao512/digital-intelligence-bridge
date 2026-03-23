using MedicalDrugImport.Plugin.Configuration;
using MedicalDrugImport.Plugin.Models;
using MedicalDrugImport.Plugin.Services;
using Npgsql;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class MedicalDrugImportPluginPostgresTests
{
    [Fact]
    public void Repository_ShouldUseConnectionString_FromPluginSettings()
    {
        var settings = new PluginSettings
        {
            Postgres = new PostgresSettings
            {
                ConnectionString = "Host=pg-host;Database=drugdb;Username=plugin;"
            }
        };

        var repository = new DrugImportRepository(settings);

        Assert.Equal("Host=pg-host;Database=drugdb;Username=plugin;", repository.ConnectionString);
    }

    [Fact]
    public async Task ImportAsync_ShouldProduceBatchSummary_WhenUsingPluginExcelService()
    {
        var filePath = MedicalDrugImportPluginExcelTestWorkbook.CreateWorkbook();

        try
        {
            var excelService = new DrugExcelImportService();
            var repository = new RecordingDrugImportRepository(new PluginSettings());
            var pipeline = new DrugImportPipelineService(excelService, repository);

            var batch = await pipeline.ImportAsync(filePath);

            Assert.Equal("Succeeded", batch.Result);
            Assert.Equal(5, batch.RawCount);
            Assert.Equal(5, batch.CleanCount);
            Assert.Equal(0, batch.ErrorCount);
            Assert.Equal(1, repository.MergeBatchCalls);
            Assert.True(repository.RawRows.Count > 0);
            Assert.True(repository.CleanRows.Count > 0);
            Assert.All(repository.RawRows, row => Assert.Equal(batch.BatchId, row.BatchId));
            Assert.All(repository.CleanRows, row => Assert.Equal(batch.BatchId, row.BatchId));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ImportAsync_ShouldExposeBatchSummary_WhenViewModelRunsImport()
    {
        var batchId = Guid.NewGuid();
        var viewModel = new MedicalDrugImport.Plugin.ViewModels.DrugImportPluginViewModel(
            null,
            new StubImportPipelineService(new DrugImportBatch
            {
                BatchId = batchId,
                Result = "Succeeded",
                RawCount = 10,
                CleanCount = 9,
                ErrorCount = 1
            }));
        viewModel.SelectedFilePath = "C:\\temp\\import.xlsx";

        await viewModel.ImportAsync();

        Assert.Equal(batchId, viewModel.LastBatchId);
        Assert.Equal("Succeeded", viewModel.ImportResult);
        Assert.Equal(10, viewModel.RawCount);
        Assert.Equal(9, viewModel.CleanCount);
        Assert.Equal(1, viewModel.ErrorCount);
    }

    [Fact]
    public async Task MergeBatchAsync_ShouldBuildUpsertUsingDrugCode_WhenPluginRepositoryMergesBatch()
    {
        var repository = new TestDrugImportRepository(new PluginSettings());
        var batchId = Guid.NewGuid();

        await repository.MergeBatchAsync(batchId);

        Assert.Contains("insert into biz.drug_catalog", repository.LastCommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("on conflict (drug_code)", repository.LastCommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("source_sheet <> '关联关系表'", repository.LastCommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(repository.LastParameters, p => p.ParameterName == "batch_id" && p.Value is Guid value && value == batchId);
    }

    [Fact]
    public void NormalizeConnectionString_ShouldConvertPostgresUri_WhenPluginUsesConnectionStringLoader()
    {
        var method = typeof(DrugImportRepository).GetMethod(
            "NormalizeConnectionString",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var normalized = (string?)method!.Invoke(
            null,
            ["postgresql://postgres.user:secret@127.0.0.1:5434/postgres?sslmode=require&keepalives=1"]);

        Assert.NotNull(normalized);
        Assert.Contains("Host=127.0.0.1", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Port=5434", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Database=postgres", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Username=postgres.user", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Password=secret", normalized, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RecordingDrugImportRepository : DrugImportRepository
    {
        public RecordingDrugImportRepository(PluginSettings settings) : base(settings)
        {
        }

        public List<DrugImportRow> RawRows { get; } = new();
        public List<DrugImportRow> CleanRows { get; } = new();
        public List<DrugImportRow> ErrorRows { get; } = new();
        public int MergeBatchCalls { get; private set; }

        public override Task ExecuteInImportSessionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default)
        {
            return work(cancellationToken);
        }

        public override Task InsertRawBatchAsync(IReadOnlyList<DrugImportRow> rows, CancellationToken cancellationToken = default)
        {
            RawRows.AddRange(rows);
            return Task.CompletedTask;
        }

        public override Task InsertCleanBatchAsync(IReadOnlyList<DrugImportRow> rows, CancellationToken cancellationToken = default)
        {
            CleanRows.AddRange(rows);
            return Task.CompletedTask;
        }

        public override Task InsertErrorBatchAsync(IReadOnlyList<DrugImportRow> rows, CancellationToken cancellationToken = default)
        {
            ErrorRows.AddRange(rows);
            return Task.CompletedTask;
        }

        public override Task MergeBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
        {
            MergeBatchCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class StubImportPipelineService : IDrugImportPipelineService
    {
        private readonly DrugImportBatch _batch;

        public StubImportPipelineService(DrugImportBatch batch)
        {
            _batch = batch;
        }

        public Task<DrugImportBatch> ImportAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_batch);
        }
    }

    private sealed class TestDrugImportRepository : DrugImportRepository
    {
        public TestDrugImportRepository(PluginSettings settings) : base(settings)
        {
        }

        public string LastCommandText { get; private set; } = string.Empty;

        public IReadOnlyCollection<NpgsqlParameter> LastParameters { get; private set; } = Array.Empty<NpgsqlParameter>();

        protected override Task ExecuteNonQueryAsync(
            string commandText,
            IReadOnlyCollection<NpgsqlParameter> parameters,
            CancellationToken cancellationToken)
        {
            LastCommandText = commandText;
            LastParameters = parameters;
            return Task.CompletedTask;
        }
    }
}

internal static class MedicalDrugImportPluginExcelTestWorkbook
{
    public static string CreateWorkbook()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"plugin-drug-import-{Guid.NewGuid():N}.xlsx");
        using var archive = System.IO.Compression.ZipFile.Open(filePath, System.IO.Compression.ZipArchiveMode.Create);

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

        WriteEntry(archive, "xl/workbook.xml", CreateWorkbookXml());
        WriteEntry(archive, "xl/_rels/workbook.xml.rels", CreateWorkbookRelsXml());

        WriteEntry(archive, "xl/worksheets/sheet1.xml", CreateSheetXml(
            DrugExcelTemplate.Default.Sheets[0].Columns,
            new[]
            {
                CreateRow(DrugExcelTemplate.Default.Sheets[0].Columns),
                CreateRow(DrugExcelTemplate.Default.Sheets[0].Columns, "A001", "药品甲"),
                CreateRow(DrugExcelTemplate.Default.Sheets[0].Columns, "A002", "药品乙")
            }));
        WriteEntry(archive, "xl/worksheets/sheet2.xml", CreateSheetXml(
            DrugExcelTemplate.Default.Sheets[1].Columns,
            new[]
            {
                CreateRow(DrugExcelTemplate.Default.Sheets[1].Columns),
                CreateRow(DrugExcelTemplate.Default.Sheets[1].Columns, "N001", "新增药品")
            }));
        WriteEntry(archive, "xl/worksheets/sheet3.xml", CreateSheetXml(
            DrugExcelTemplate.Default.Sheets[2].Columns,
            new[]
            {
                CreateRow(DrugExcelTemplate.Default.Sheets[2].Columns),
                CreateRow(DrugExcelTemplate.Default.Sheets[2].Columns, "C001", "变更药品")
            }));
        WriteEntry(archive, "xl/worksheets/sheet4.xml", CreateSheetXml(
            DrugExcelTemplate.Default.Sheets[3].Columns,
            new[]
            {
                CreateRow(DrugExcelTemplate.Default.Sheets[3].Columns),
                new [] { "R001", "11111111-1111-1111-1111-111111111111", "OLD001", "22222222-2222-2222-2222-222222222222" }
            }));

        return filePath;
    }

    private static string CreateWorkbookXml()
    {
        return """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                  xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <sheets>
            <sheet name="总表（270419）" sheetId="1" r:id="rId1"/>
            <sheet name="新增（559）" sheetId="2" r:id="rId2"/>
            <sheet name="变更（449）" sheetId="3" r:id="rId3"/>
            <sheet name="关联关系表" sheetId="4" r:id="rId4"/>
          </sheets>
        </workbook>
        """;
    }

    private static string CreateWorkbookRelsXml()
    {
        return """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/>
          <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet3.xml"/>
          <Relationship Id="rId4" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet4.xml"/>
        </Relationships>
        """;
    }

    private static string CreateSheetXml(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        var sheetData = new System.Xml.Linq.XElement("sheetData");
        var rowIndex = 1;
        foreach (var row in rows)
        {
            var rowElement = new System.Xml.Linq.XElement("row", new System.Xml.Linq.XAttribute("r", rowIndex));
            for (var i = 0; i < row.Count; i++)
            {
                if (string.IsNullOrEmpty(row[i]))
                {
                    continue;
                }

                rowElement.Add(
                    new System.Xml.Linq.XElement(
                        "c",
                        new System.Xml.Linq.XAttribute("r", $"{ColumnName(i + 1)}{rowIndex}"),
                        new System.Xml.Linq.XAttribute("t", "inlineStr"),
                        new System.Xml.Linq.XElement("is", new System.Xml.Linq.XElement("t", row[i]))));
            }

            sheetData.Add(rowElement);
            rowIndex++;
        }

        var document = new System.Xml.Linq.XDocument(
            new System.Xml.Linq.XElement(
                System.Xml.Linq.XName.Get("worksheet", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"),
                sheetData.Elements()));

        return document.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
    }

    private static IReadOnlyList<string> CreateRow(IReadOnlyList<string> headers, string? drugCode = null, string? name = null)
    {
        var row = headers.ToArray();
        if (drugCode is null && name is null)
        {
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

    private static void WriteEntry(System.IO.Compression.ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
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
