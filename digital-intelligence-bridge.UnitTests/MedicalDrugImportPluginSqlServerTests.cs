using System.Data.Common;
using MedicalDrugImport.Plugin.Configuration;
using MedicalDrugImport.Plugin.Models;
using MedicalDrugImport.Plugin.Services;
using MedicalDrugImport.Plugin.ViewModels;
using Microsoft.Data.SqlClient;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class MedicalDrugImportPluginSqlServerTests
{
    [Fact]
    public void SyncService_ShouldUseConnectionString_FromPluginSettings()
    {
        var settings = new PluginSettings
        {
            SqlServer = new SqlServerSettings
            {
                ConnectionString = "Server=sql-host;Database=ChisDict;User Id=plugin;"
            }
        };

        var service = new TestSqlServerDrugSyncService(settings, new StubDrugCatalogSyncRepository([]));

        Assert.Equal("Server=sql-host;Database=ChisDict;User Id=plugin;", service.ConnectionString);
    }

    [Fact]
    public async Task SyncBatchAsync_ShouldUpsertRowsAndWriteSyncLog_WhenUsingPluginRepository()
    {
        var batchId = Guid.NewGuid();
        var repository = new StubDrugCatalogSyncRepository(
        [
            new DrugImportRow
            {
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
        var service = new TestSqlServerDrugSyncService(new PluginSettings
        {
            SqlServer = new SqlServerSettings
            {
                ConnectionString = "Server=sql-host;Database=ChisDict;User Id=plugin;",
                EnableWrites = true
            }
        }, repository);

        var batch = await service.SyncBatchAsync(batchId);

        Assert.Equal("Succeeded", batch.Result);
        Assert.Equal(1, batch.SyncUpdateCount);
        Assert.NotNull(batch.SyncedAt);
        Assert.Equal(3, service.ExecutedCommands.Count);
        Assert.Contains("set deadlock_priority low", service.ExecutedCommands[0].CommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("set lock_timeout", service.ExecutedCommands[0].CommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[药品编码]", service.ExecutedCommands[1].CommandText);
        Assert.Contains("[剂型]", service.ExecutedCommands[1].CommandText);
        Assert.Contains("[规格]", service.ExecutedCommands[1].CommandText);
        Assert.Contains(service.ExecutedCommands[1].Parameters, p => p.ParameterName == "@dosage_form");
        Assert.Contains(service.ExecutedCommands[1].Parameters, p => p.ParameterName == "@specification");
        Assert.Contains("insert into [dbo].[yb_同步记录]", service.ExecutedCommands[2].CommandText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncAsync_ShouldUpdateSummary_WhenLastBatchExists()
    {
        var batchId = Guid.NewGuid();
        var syncService = new StubSqlServerDrugSyncService(new DrugImportBatch
        {
            BatchId = batchId,
            Result = "Succeeded",
            SyncUpdateCount = 3
        });
        var viewModel = new DrugImportPluginViewModel(null, null, syncService)
        {
            LastBatchId = batchId
        };

        await viewModel.SyncAsync();

        Assert.Equal("Succeeded", viewModel.SyncResult);
        Assert.Equal(3, viewModel.SyncUpdateCount);
        Assert.Equal(batchId, syncService.CalledBatchIds.Single());
    }

    [Fact]
    public async Task RetrySyncAsync_ShouldExposeError_WhenSyncFails()
    {
        var batchId = Guid.NewGuid();
        var syncService = new ThrowingSqlServerDrugSyncService();
        var viewModel = new DrugImportPluginViewModel(null, null, syncService)
        {
            LastBatchId = batchId
        };

        await viewModel.RetrySyncAsync();

        Assert.Contains("sync failed", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(batchId, syncService.CalledBatchIds.Single());
    }

    [Fact]
    public async Task PreviewSyncAsync_ShouldUpdatePreviewSummary_WhenLastBatchExists()
    {
        var batchId = Guid.NewGuid();
        var syncService = new StubSqlServerDrugSyncService(
            syncResult: new DrugImportBatch
            {
                BatchId = batchId,
                Result = "Succeeded",
                SyncUpdateCount = 3
            },
            previewResult: new DrugImportBatch
            {
                BatchId = batchId,
                Stage = "SyncPreview",
                Result = "Blocked",
                SyncUpdateCount = 51,
                LastError = "当前批次待同步记录已超过安全阈值 MaxSyncRowsPerRun=50。"
            });
        var viewModel = new DrugImportPluginViewModel(null, null, syncService)
        {
            LastBatchId = batchId
        };

        await viewModel.PreviewSyncAsync();

        Assert.Contains("51", viewModel.SyncPreviewSummary, StringComparison.Ordinal);
        Assert.Contains("Blocked", viewModel.SyncPreviewSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MaxSyncRowsPerRun", viewModel.SyncPreviewSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(batchId, syncService.PreviewedBatchIds.Single());
    }

    [Fact]
    public async Task SyncBatchAsync_ShouldSplitLargeSyncIntoMultipleSessions_WhenRowCountExceedsBatchSize()
    {
        var rows = Enumerable.Range(1, 5)
            .Select(index => new DrugImportRow
            {
                BusinessKey = $"A{index:000}",
                NormalizedData = new Dictionary<string, string?>
                {
                    ["drug_code"] = $"A{index:000}",
                    ["drug_name_cn"] = $"药品{index}"
                }
            })
            .ToArray();
        var settings = new PluginSettings
        {
            SqlServer = new SqlServerSettings
            {
                ConnectionString = "Server=sql-host;Database=ChisDict;User Id=plugin;",
                EnableWrites = true
            },
            Import = new ImportSettings
            {
                BatchSize = 2
            }
        };
        var service = new CountingSqlServerDrugSyncService(settings, new StubDrugCatalogSyncRepository(rows));

        var batch = await service.SyncBatchAsync(Guid.NewGuid());

        Assert.Equal(5, batch.SyncUpdateCount);
        Assert.Equal(4, service.SessionCount);
        Assert.Equal(9, service.ExecutedCommands.Count);
    }

    [Fact]
    public async Task SyncBatchAsync_ShouldRejectWhenRowCountExceedsSafetyLimit_ByDefault()
    {
        var rows = Enumerable.Range(1, 4)
            .Select(index => new DrugImportRow
            {
                BusinessKey = $"A{index:000}",
                NormalizedData = new Dictionary<string, string?>
                {
                    ["drug_code"] = $"A{index:000}",
                    ["drug_name_cn"] = $"药品{index}"
                }
            })
            .ToArray();
        var settings = new PluginSettings
        {
            SqlServer = new SqlServerSettings
            {
                ConnectionString = "Server=sql-host;Database=ChisDict;User Id=plugin;",
                EnableWrites = true
            },
            Import = new ImportSettings
            {
                BatchSize = 2,
                MaxSyncRowsPerRun = 3
            }
        };
        var service = new CountingSqlServerDrugSyncService(settings, new StubDrugCatalogSyncRepository(rows));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SyncBatchAsync(Guid.NewGuid()));

        Assert.Contains("MaxSyncRowsPerRun", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(service.ExecutedCommands);
        Assert.Equal(0, service.SessionCount);
    }

    [Fact]
    public async Task SyncBatchAsync_ShouldReject_WhenSqlServerWritesAreDisabled()
    {
        var settings = new PluginSettings
        {
            SqlServer = new SqlServerSettings
            {
                ConnectionString = "Server=sql-host;Database=ChisDict;User Id=plugin;"
            }
        };
        var service = new CountingSqlServerDrugSyncService(settings, new StubDrugCatalogSyncRepository(
        [
            new DrugImportRow
            {
                BusinessKey = "A001",
                NormalizedData = new Dictionary<string, string?>
                {
                    ["drug_code"] = "A001",
                    ["drug_name_cn"] = "药品甲"
                }
            }
        ]));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SyncBatchAsync(Guid.NewGuid()));

        Assert.Contains("EnableWrites", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(service.ExecutedCommands);
    }

    [Fact]
    public async Task SyncBatchAsync_ShouldAllowLargeSync_WhenUnsafeFullSyncIsEnabled()
    {
        var rows = Enumerable.Range(1, 4)
            .Select(index => new DrugImportRow
            {
                BusinessKey = $"A{index:000}",
                NormalizedData = new Dictionary<string, string?>
                {
                    ["drug_code"] = $"A{index:000}",
                    ["drug_name_cn"] = $"药品{index}"
                }
            })
            .ToArray();
        var settings = new PluginSettings
        {
            SqlServer = new SqlServerSettings
            {
                ConnectionString = "Server=sql-host;Database=ChisDict;User Id=plugin;",
                EnableWrites = true
            },
            Import = new ImportSettings
            {
                BatchSize = 2,
                MaxSyncRowsPerRun = 3,
                AllowUnsafeFullSync = true
            }
        };
        var service = new CountingSqlServerDrugSyncService(settings, new StubDrugCatalogSyncRepository(rows));

        var batch = await service.SyncBatchAsync(Guid.NewGuid());

        Assert.Equal("Succeeded", batch.Result);
        Assert.Equal(4, batch.SyncUpdateCount);
        Assert.Equal(3, service.SessionCount);
    }

    [Fact]
    public async Task PreviewBatchAsync_ShouldReturnBlockedSummary_WithoutExecutingCommands()
    {
        var rows = Enumerable.Range(1, 4)
            .Select(index => new DrugImportRow
            {
                BusinessKey = $"A{index:000}",
                NormalizedData = new Dictionary<string, string?>
                {
                    ["drug_code"] = $"A{index:000}",
                    ["drug_name_cn"] = $"药品{index}"
                }
            })
            .ToArray();
        var settings = new PluginSettings
        {
            SqlServer = new SqlServerSettings
            {
                ConnectionString = "Server=sql-host;Database=ChisDict;User Id=plugin;",
                EnableWrites = true
            },
            Import = new ImportSettings
            {
                BatchSize = 2,
                MaxSyncRowsPerRun = 3
            }
        };
        var service = new CountingSqlServerDrugSyncService(settings, new StubDrugCatalogSyncRepository(rows));

        var preview = await service.PreviewBatchAsync(Guid.NewGuid());

        Assert.Equal("Blocked", preview.Result);
        Assert.Equal("SyncPreview", preview.Stage);
        Assert.Equal(4, preview.SyncUpdateCount);
        Assert.Contains("MaxSyncRowsPerRun", preview.LastError, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(service.ExecutedCommands);
        Assert.Equal(0, service.SessionCount);
    }

    [Fact]
    public async Task PreviewBatchAsync_ShouldUseRepositoryCount_WhenAvailable()
    {
        var batchId = Guid.NewGuid();
        var repository = new CountingOnlyDrugCatalogSyncRepository(88);
        var settings = new PluginSettings
        {
            SqlServer = new SqlServerSettings
            {
                ConnectionString = "Server=sql-host;Database=ChisDict;User Id=plugin;"
            },
            Import = new ImportSettings
            {
                MaxSyncRowsPerRun = 50
            }
        };
        var service = new CountingSqlServerDrugSyncService(settings, repository);

        var preview = await service.PreviewBatchAsync(batchId);

        Assert.Equal(88, preview.SyncUpdateCount);
        Assert.Equal("Blocked", preview.Result);
        Assert.Equal(1, repository.CountCalls);
        Assert.Equal(0, repository.EnumeratedRows);
    }

    [Fact]
    public async Task PreviewBatchAsync_ShouldReportReadOnlyMode_WhenSqlServerWritesAreDisabled()
    {
        var settings = new PluginSettings
        {
            SqlServer = new SqlServerSettings
            {
                ConnectionString = "Server=sql-host;Database=ChisDict;User Id=plugin;"
            }
        };
        var service = new CountingSqlServerDrugSyncService(settings, new StubDrugCatalogSyncRepository(
        [
            new DrugImportRow
            {
                BusinessKey = "A001",
                NormalizedData = new Dictionary<string, string?>
                {
                    ["drug_code"] = "A001",
                    ["drug_name_cn"] = "药品甲"
                }
            }
        ]));

        var preview = await service.PreviewBatchAsync(Guid.NewGuid());

        Assert.Equal("ReadOnly", preview.Result);
        Assert.Contains("EnableWrites", preview.LastError, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, preview.SyncUpdateCount);
        Assert.Empty(service.ExecutedCommands);
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
            foreach (var row in _rows)
            {
                yield return row;
                await Task.Yield();
            }
        }

        public Task<int> CountAffectedCatalogRowsAsync(Guid batchId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_rows.Count);
        }
    }

    private sealed class CountingOnlyDrugCatalogSyncRepository : IDrugCatalogSyncRepository
    {
        private readonly int _count;

        public CountingOnlyDrugCatalogSyncRepository(int count)
        {
            _count = count;
        }

        public int CountCalls { get; private set; }

        public int EnumeratedRows { get; private set; }

        public Task<int> CountAffectedCatalogRowsAsync(Guid batchId, CancellationToken cancellationToken = default)
        {
            CountCalls++;
            return Task.FromResult(_count);
        }

        public async IAsyncEnumerable<DrugImportRow> GetAffectedCatalogRowsAsync(Guid batchId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            EnumeratedRows++;
            await Task.Yield();
            yield break;
        }
    }

    private sealed class TestSqlServerDrugSyncService : SqlServerDrugSyncService
    {
        public TestSqlServerDrugSyncService(PluginSettings settings, IDrugCatalogSyncRepository repository)
            : base(settings, repository)
        {
        }

        public List<CapturedCommand> ExecutedCommands { get; } = new();

        protected override Task ExecuteInSessionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
        {
            return work(cancellationToken);
        }

        protected override Task ExecuteNonQueryAsync(string commandText, IReadOnlyCollection<DbParameter> parameters, CancellationToken cancellationToken)
        {
            ExecutedCommands.Add(new CapturedCommand(commandText, parameters.ToArray()));
            return Task.CompletedTask;
        }
    }

    private sealed class StubSqlServerDrugSyncService : ISqlServerDrugSyncService
    {
        private readonly DrugImportBatch _syncResult;
        private readonly DrugImportBatch _previewResult;

        public StubSqlServerDrugSyncService(DrugImportBatch syncResult, DrugImportBatch? previewResult = null)
        {
            _syncResult = syncResult;
            _previewResult = previewResult ?? syncResult;
        }

        public List<Guid> CalledBatchIds { get; } = new();
        public List<Guid> PreviewedBatchIds { get; } = new();

        public Task<DrugImportBatch> SyncBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
        {
            CalledBatchIds.Add(batchId);
            return Task.FromResult(_syncResult);
        }

        public Task<DrugImportBatch> PreviewBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
        {
            PreviewedBatchIds.Add(batchId);
            return Task.FromResult(_previewResult);
        }
    }

    private sealed class ThrowingSqlServerDrugSyncService : ISqlServerDrugSyncService
    {
        public List<Guid> CalledBatchIds { get; } = new();

        public Task<DrugImportBatch> SyncBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
        {
            CalledBatchIds.Add(batchId);
            throw new InvalidOperationException("sync failed");
        }

        public Task<DrugImportBatch> PreviewBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
        {
            CalledBatchIds.Add(batchId);
            throw new InvalidOperationException("sync failed");
        }
    }

    private sealed class CountingSqlServerDrugSyncService : SqlServerDrugSyncService
    {
        public CountingSqlServerDrugSyncService(PluginSettings settings, IDrugCatalogSyncRepository repository)
            : base(settings, repository)
        {
        }

        public int SessionCount { get; private set; }

        public List<CapturedCommand> ExecutedCommands { get; } = new();

        protected override async Task ExecuteInSessionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
        {
            SessionCount++;
            await work(cancellationToken);
        }

        protected override Task ExecuteNonQueryAsync(string commandText, IReadOnlyCollection<DbParameter> parameters, CancellationToken cancellationToken)
        {
            ExecutedCommands.Add(new CapturedCommand(commandText, parameters.ToArray()));
            return Task.CompletedTask;
        }
    }

    private sealed record CapturedCommand(string CommandText, IReadOnlyCollection<DbParameter> Parameters);
}

