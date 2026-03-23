using DigitalIntelligenceBridge.Models;
using DigitalIntelligenceBridge.Services;
using DigitalIntelligenceBridge.ViewModels;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class DrugImportViewModelTests
{
    [Fact]
    public async Task ValidateCommand_ShouldUpdatePreviewState_WhenValidationSucceeds()
    {
        var vm = CreateViewModel();
        vm.SelectedFilePath = @"C:\imports\medical.xlsx";

        await vm.ValidateAsync();

        Assert.True(vm.IsPreviewValid);
        Assert.Equal("工作表完整", vm.PreviewSummary);
        Assert.Equal(2, vm.SheetRowCounts.Count);
        Assert.Equal("总表（270419）: 120", vm.SheetRowCounts[0]);
    }

    [Fact]
    public async Task ImportCommand_ShouldUpdateBatchSummary_WhenImportSucceeds()
    {
        var vm = CreateViewModel();
        vm.SelectedFilePath = @"C:\imports\medical.xlsx";

        await vm.ValidateAsync();
        await vm.ImportAsync();

        Assert.Equal("Succeeded", vm.BatchResult);
        Assert.Equal(10, vm.RawCount);
        Assert.Equal(8, vm.CleanCount);
        Assert.Equal(2, vm.ErrorCount);
        Assert.True(vm.CanSync);
    }

    [Fact]
    public async Task SyncCommand_ShouldUpdateSyncSummary_WhenSyncSucceeds()
    {
        var vm = CreateViewModel();
        vm.SelectedFilePath = @"C:\imports\medical.xlsx";

        await vm.ValidateAsync();
        await vm.ImportAsync();
        await vm.SyncAsync();

        Assert.Equal("Succeeded", vm.SyncResult);
        Assert.Equal(3, vm.SyncUpdateCount);
        Assert.True(vm.CanRetrySync);
    }

    [Fact]
    public async Task ImportAsync_ShouldSetErrorMessage_WhenValidationFails()
    {
        var vm = CreateViewModel(new StubDrugExcelImportService(new DrugImportPreview
        {
            FilePath = "broken.xlsx",
            IsValid = false,
            Errors = new List<string> { "缺少工作表: 关联关系表" }
        }));
        vm.SelectedFilePath = "broken.xlsx";

        await vm.ValidateAsync();

        Assert.False(vm.IsPreviewValid);
        Assert.Contains("关联关系表", vm.ErrorMessage);
        Assert.False(vm.CanImport);
    }

    [Fact]
    public async Task RetrySyncAsync_ShouldUseLastBatchId_WhenPreviousSyncFailed()
    {
        var sync = new StubSqlServerDrugSyncService
        {
            FirstResult = new DrugImportBatch
            {
                Result = "Failed",
                SyncUpdateCount = 0,
                LastError = "timeout"
            },
            RetryResult = new DrugImportBatch
            {
                Result = "Succeeded",
                SyncUpdateCount = 5
            }
        };

        var vm = CreateViewModel(syncService: sync);
        vm.SelectedFilePath = @"C:\imports\medical.xlsx";

        await vm.ValidateAsync();
        await vm.ImportAsync();
        await vm.SyncAsync();
        await vm.RetrySyncAsync();

        Assert.Equal(2, sync.Calls.Count);
        Assert.Equal(sync.Calls[0], sync.Calls[1]);
        Assert.Equal("Succeeded", vm.SyncResult);
        Assert.Equal(5, vm.SyncUpdateCount);
    }

    [Fact]
    public async Task ImportAsync_ShouldExposeLastBatchContext_WhenImportCompletes()
    {
        var vm = CreateViewModel();
        vm.SelectedFilePath = @"C:\imports\medical.xlsx";

        await vm.ValidateAsync();
        await vm.ImportAsync();

        Assert.True(vm.HasLastBatchContext);
        Assert.Equal("11111111-1111-1111-1111-111111111111", vm.LastBatchIdText);
        Assert.Contains(@"C:\imports\medical.xlsx", vm.LastBatchSummary);
    }

    [Fact]
    public async Task RetrySyncAsync_ShouldNotReimportExcel_WhenRetryingSync()
    {
        var pipeline = new StubDrugImportPipelineService();
        var sync = new StubSqlServerDrugSyncService
        {
            FirstResult = new DrugImportBatch
            {
                Result = "Failed",
                SyncUpdateCount = 0,
                LastError = "network"
            },
            RetryResult = new DrugImportBatch
            {
                Result = "Succeeded",
                SyncUpdateCount = 4
            }
        };

        var vm = CreateViewModel(pipelineService: pipeline, syncService: sync);
        vm.SelectedFilePath = @"C:\imports\medical.xlsx";

        await vm.ValidateAsync();
        await vm.ImportAsync();
        await vm.SyncAsync();
        await vm.RetrySyncAsync();

        Assert.Equal(1, pipeline.Calls);
        Assert.Equal(2, sync.Calls.Count);
    }

    private static DrugImportViewModel CreateViewModel(
        IDrugExcelImportService? excelService = null,
        IDrugImportPipelineService? pipelineService = null,
        ISqlServerDrugSyncService? syncService = null)
    {
        return new DrugImportViewModel(
            excelService ?? new StubDrugExcelImportService(),
            pipelineService ?? new StubDrugImportPipelineService(),
            syncService ?? new StubSqlServerDrugSyncService(),
            new NullLoggerService<DrugImportViewModel>());
    }

    private sealed class StubDrugExcelImportService : IDrugExcelImportService
    {
        private readonly DrugImportPreview _preview;

        public StubDrugExcelImportService(DrugImportPreview? preview = null)
        {
            _preview = preview ?? new DrugImportPreview
            {
                FilePath = @"C:\imports\medical.xlsx",
                IsValid = true,
                Summary = "工作表完整",
                SheetRowCounts = new Dictionary<string, int>
                {
                    ["总表（270419）"] = 120,
                    ["新增（559）"] = 12
                }
            };
        }

        public Task<DrugImportPreview> ValidateAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_preview);
        }

        public Task<DrugImportPreview> ValidateStructureAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_preview);
        }

        public async IAsyncEnumerable<DrugImportRow> ReadRowsAsync(string filePath, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield break;
        }
    }

    private sealed class StubDrugImportPipelineService : IDrugImportPipelineService
    {
        public int Calls { get; private set; }

        public Task<DrugImportBatch> ImportAsync(string filePath, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new DrugImportBatch
            {
                BatchId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                SourceFile = filePath,
                Stage = "Import",
                Result = "Succeeded",
                RawCount = 10,
                CleanCount = 8,
                ErrorCount = 2
            });
        }
    }

    private sealed class StubSqlServerDrugSyncService : ISqlServerDrugSyncService
    {
        public DrugImportBatch FirstResult { get; set; } = new()
        {
            Result = "Succeeded",
            SyncUpdateCount = 3
        };

        public DrugImportBatch RetryResult { get; set; } = new()
        {
            Result = "Succeeded",
            SyncUpdateCount = 3
        };

        public List<Guid> Calls { get; } = new();

        public Task<DrugImportBatch> SyncBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
        {
            Calls.Add(batchId);
            return Task.FromResult(Calls.Count == 1 ? FirstResult : RetryResult);
        }
    }

    private sealed class NullLoggerService<T> : ILoggerService<T>
    {
        public void LogCritical(string message, params object[] args) { }
        public void LogDebug(string message, params object[] args) { }
        public void LogError(string message, params object[] args) { }
        public void LogError(Exception exception, string message, params object[] args) { }
        public void LogInformation(string message, params object[] args) { }
        public void LogWarning(string message, params object[] args) { }
    }
}
