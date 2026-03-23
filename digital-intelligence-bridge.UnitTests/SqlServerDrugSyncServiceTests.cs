using System.Data;
using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Models;
using DigitalIntelligenceBridge.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class SqlServerDrugSyncServiceTests
{
    [Fact]
    public async Task SyncBatchAsync_ShouldUpsertRowsUsingDrugCodeAndWriteSyncLog()
    {
        var batchId = Guid.NewGuid();
        var repository = new StubDrugCatalogSyncRepository(
        [
            new DrugImportRow
            {
                BatchId = batchId,
                BusinessKey = "A001",
                NormalizedData = new Dictionary<string, string?>
                {
                    ["drug_code"] = "A001",
                    ["drug_name_cn"] = "药品甲",
                    ["dosage_form"] = "片剂",
                    ["specification"] = "0.25mg"
                }
            }
        ]);

        var service = new TestSqlServerDrugSyncService(CreateSettings(), repository);

        var result = await service.SyncBatchAsync(batchId);

        Assert.Equal(1, result.SyncUpdateCount);
        Assert.Equal("Succeeded", result.Result);
        Assert.Equal(2, service.ExecutedCommands.Count);
        Assert.Contains("[药品编码]", service.ExecutedCommands[0].CommandText);
        Assert.Contains("where [药品编码] = @drug_code", service.ExecutedCommands[0].CommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("coalesce(@drug_name_cn, [中文名称])", service.ExecutedCommands[0].CommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(service.ExecutedCommands[0].Parameters, p => p.ParameterName == "@drug_code" && (string?)p.Value == "A001");
        Assert.Contains("insert into [dbo].[yb_同步记录]", service.ExecutedCommands[1].CommandText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncBatchAsync_ShouldKeepMissingFieldsAsNullParameters()
    {
        var batchId = Guid.NewGuid();
        var repository = new StubDrugCatalogSyncRepository(
        [
            new DrugImportRow
            {
                BatchId = batchId,
                BusinessKey = "A002",
                NormalizedData = new Dictionary<string, string?>
                {
                    ["drug_code"] = "A002"
                }
            }
        ]);

        var service = new TestSqlServerDrugSyncService(CreateSettings(), repository);

        await service.SyncBatchAsync(batchId);

        var upsert = service.ExecutedCommands[0];
        Assert.Contains(upsert.Parameters, p => p.ParameterName == "@drug_name_cn" && p.Value == DBNull.Value);
        Assert.Contains(upsert.Parameters, p => p.ParameterName == "@dosage_form" && p.Value == DBNull.Value);
        Assert.Contains(upsert.Parameters, p => p.ParameterName == "@specification" && p.Value == DBNull.Value);
    }

    [Fact]
    public async Task SyncBatchAsync_ShouldExecuteAllCommandsWithinSingleSession()
    {
        var batchId = Guid.NewGuid();
        var repository = new StubDrugCatalogSyncRepository(
        [
            new DrugImportRow
            {
                BatchId = batchId,
                BusinessKey = "A001",
                NormalizedData = new Dictionary<string, string?>
                {
                    ["drug_code"] = "A001"
                }
            },
            new DrugImportRow
            {
                BatchId = batchId,
                BusinessKey = "A002",
                NormalizedData = new Dictionary<string, string?>
                {
                    ["drug_code"] = "A002"
                }
            }
        ]);

        var service = new TestSqlServerDrugSyncService(CreateSettings(), repository);

        await service.SyncBatchAsync(batchId);

        Assert.Equal(1, service.SessionExecuteCount);
        Assert.Equal(3, service.ExecutedCommands.Count);
    }

    private static IOptions<AppSettings> CreateSettings()
    {
        return Options.Create(new AppSettings
        {
            MedicalDrugImport = new MedicalDrugImportConfig
            {
                SqlServer = new SqlServerConnectionConfig
                {
                    Host = "sqlserver.local",
                    Port = 1433,
                    Database = "ChisDict",
                    Username = "pluginUser",
                    Password = "pluginPassword",
                    Encrypt = true,
                    TrustServerCertificate = true
                }
            }
        });
    }

    private sealed class StubDrugCatalogSyncRepository : IDrugCatalogSyncRepository
    {
        private readonly IReadOnlyList<DrugImportRow> _rows;

        public StubDrugCatalogSyncRepository(IReadOnlyList<DrugImportRow> rows)
        {
            _rows = rows;
        }

        public async IAsyncEnumerable<DrugImportRow> GetAffectedCatalogRowsAsync(Guid batchId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var row in _rows.Where(row => row.BatchId == batchId))
            {
                yield return row;
                await Task.Yield();
            }
        }
    }

    private sealed class TestSqlServerDrugSyncService : SqlServerDrugSyncService
    {
        public TestSqlServerDrugSyncService(IOptions<AppSettings> settings, IDrugCatalogSyncRepository repository)
            : base(settings, repository)
        {
        }

        public List<CapturedCommand> ExecutedCommands { get; } = new();
        public int SessionExecuteCount { get; private set; }

        protected override Task ExecuteInSessionAsync(
            Func<CancellationToken, Task> work,
            CancellationToken cancellationToken)
        {
            SessionExecuteCount++;
            return work(cancellationToken);
        }

        protected override Task ExecuteNonQueryAsync(
            string commandText,
            IReadOnlyCollection<System.Data.Common.DbParameter> parameters,
            CancellationToken cancellationToken)
        {
            ExecutedCommands.Add(new CapturedCommand(commandText, parameters.ToArray()));
            return Task.CompletedTask;
        }
    }

    public sealed record CapturedCommand(string CommandText, IReadOnlyCollection<System.Data.Common.DbParameter> Parameters);
}
