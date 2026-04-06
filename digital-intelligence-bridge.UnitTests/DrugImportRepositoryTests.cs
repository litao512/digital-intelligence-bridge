using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Models;
using DigitalIntelligenceBridge.Services;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class DrugImportRepositoryTests
{
    [Fact]
    public async Task InsertRawAsync_ShouldBuildRawInsertCommand()
    {
        var repository = CreateRepository();
        var row = CreateRow();

        await repository.InsertRawAsync(row);

        Assert.Contains("insert into etl.drug_import_raw", repository.LastCommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("row_data", repository.LastCommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(repository.LastParameters, p => p.ParameterName == "batch_id" && p.Value is Guid value && value == row.BatchId);
        Assert.Contains(repository.LastParameters, p => p.ParameterName == "source_sheet" && p.Value is string value && value == row.SourceSheet);
        Assert.Contains(repository.LastParameters, p => p.ParameterName == "row_no" && p.Value is int value && value == row.RowNumber);
    }

    [Fact]
    public async Task InsertCleanAsync_ShouldBuildCleanInsertCommand()
    {
        var repository = CreateRepository();
        var row = CreateRow();

        await repository.InsertCleanAsync(row);

        Assert.Contains("insert into etl.drug_import_clean", repository.LastCommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("biz_key", repository.LastCommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(repository.LastParameters, p => p.ParameterName == "biz_key" && p.Value is string value && value == row.BusinessKey);
        Assert.Contains(repository.LastParameters, p => p.ParameterName == "normalized_data");
    }

    [Fact]
    public async Task InsertErrorAsync_ShouldBuildErrorInsertCommand()
    {
        var repository = CreateRepository();
        var row = CreateRow();
        row.ErrorCode = "REQUIRED_FIELD_MISSING";
        row.ErrorMessage = "药品代码不能为空";

        await repository.InsertErrorAsync(row);

        Assert.Contains("insert into etl.drug_import_error", repository.LastCommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(repository.LastParameters, p => p.ParameterName == "error_code" && p.Value is string value && value == "REQUIRED_FIELD_MISSING");
        Assert.Contains(repository.LastParameters, p => p.ParameterName == "error_message" && p.Value is string value && value == "药品代码不能为空");
    }

    [Fact]
    public async Task MergeBatchAsync_ShouldBuildUpsertUsingDrugCode()
    {
        var repository = CreateRepository();
        var batchId = Guid.NewGuid();

        await repository.MergeBatchAsync(batchId);

        Assert.Contains("insert into biz.drug_catalog", repository.LastCommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("on conflict (drug_code)", repository.LastCommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("where drug_code is not null", repository.LastCommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("normalized_data->>'drug_code'", repository.LastCommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(repository.LastParameters, p => p.ParameterName == "batch_id" && p.Value is Guid value && value == batchId);
    }

    [Fact]
    public async Task MergeBatchAsync_ShouldExcludeRelationSheetRows()
    {
        var repository = CreateRepository();

        await repository.MergeBatchAsync(Guid.NewGuid());

        Assert.Contains("source_sheet <> '关联关系表'", repository.LastCommandText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MergeBatchAsync_ShouldDeduplicateDrugCodeWithinBatch()
    {
        var repository = CreateRepository();

        await repository.MergeBatchAsync(Guid.NewGuid());

        Assert.Contains("row_number() over", repository.LastCommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("partition by normalized_data->>'drug_code'", repository.LastCommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("where rn = 1", repository.LastCommandText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormalizeConnectionString_ShouldConvertPostgresUri()
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
        Assert.Contains("SSL Mode=Require", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Keepalive=1", normalized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildRawCopyCommandText_ShouldTargetRawTable()
    {
        var method = typeof(DrugImportRepository).GetMethod(
            "BuildCopyCommandText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var sql = (string?)method!.Invoke(null, ["etl", "drug_import_raw", "batch_id, source_file, source_sheet, row_no, row_data"]);

        Assert.NotNull(sql);
        Assert.Contains("copy etl.drug_import_raw", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("from stdin", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("format binary", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildCleanCopyCommandText_ShouldTargetCleanTable()
    {
        var method = typeof(DrugImportRepository).GetMethod(
            "BuildCopyCommandText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var sql = (string?)method!.Invoke(null, ["etl", "drug_import_clean", "batch_id, source_sheet, row_no, biz_key, normalized_data"]);

        Assert.NotNull(sql);
        Assert.Contains("copy etl.drug_import_clean", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("biz_key", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("format binary", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildErrorCopyCommandText_ShouldTargetErrorTable()
    {
        var method = typeof(DrugImportRepository).GetMethod(
            "BuildCopyCommandText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var sql = (string?)method!.Invoke(null, ["etl", "drug_import_error", "batch_id, source_sheet, row_no, error_code, error_message, row_data"]);

        Assert.NotNull(sql);
        Assert.Contains("copy etl.drug_import_error", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("error_message", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("format binary", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static TestDrugImportRepository CreateRepository()
    {
        return new TestDrugImportRepository(Options.Create(new AppSettings
        {
            MedicalDrugImport = new MedicalDrugImportConfig
            {
                PostgresSchema = "etl"
            }
        }));
    }

    private static DrugImportRow CreateRow()
    {
        return new DrugImportRow
        {
            BatchId = Guid.NewGuid(),
            SourceSheet = "总表（270419）",
            RowNumber = 12,
            BusinessKey = "XA01ABD075A002010100483",
            RawData = new Dictionary<string, string?>
            {
                ["药品代码"] = "XA01ABD075A002010100483",
                ["注册名称"] = "地喹氯铵含片"
            },
            NormalizedData = new Dictionary<string, string?>
            {
                ["drug_code"] = "XA01ABD075A002010100483",
                ["drug_name_cn"] = "地喹氯铵含片"
            }
        };
    }

    private sealed class TestDrugImportRepository : DrugImportRepository
    {
        public TestDrugImportRepository(IOptions<AppSettings> settings) : base(settings)
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

