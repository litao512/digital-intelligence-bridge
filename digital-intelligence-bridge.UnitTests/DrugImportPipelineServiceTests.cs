using DigitalIntelligenceBridge.Models;
using DigitalIntelligenceBridge.Services;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class DrugImportPipelineServiceTests
{
    [Fact]
    public async Task ImportAsync_ShouldCreateBatchWriteRowsAndMerge()
    {
        var batchId = Guid.NewGuid();
        var excel = new StubDrugExcelImportService(
        [
            new DrugImportRow
            {
                SourceSheet = "总表（270419）",
                RowNumber = 2,
                BusinessKey = "A001",
                RawData = new Dictionary<string, string?> { ["药品代码"] = "A001" },
                NormalizedData = new Dictionary<string, string?> { ["drug_code"] = "A001" }
            },
            new DrugImportRow
            {
                SourceSheet = "总表（270419）",
                RowNumber = 3,
                BusinessKey = string.Empty,
                RawData = new Dictionary<string, string?> { ["药品代码"] = "" },
                ErrorCode = "REQUIRED_FIELD_MISSING",
                ErrorMessage = "药品代码不能为空"
            }
        ]);
        var repository = new StubDrugImportRepository(batchId);
        var service = new DrugImportPipelineService(excel, repository);

        var result = await service.ImportAsync(@"C:\imports\medical.xlsx");

        Assert.Equal(batchId, result.BatchId);
        Assert.Equal("Import", result.Stage);
        Assert.Equal("Succeeded", result.Result);
        Assert.Equal(2, result.RawCount);
        Assert.Equal(1, result.CleanCount);
        Assert.Equal(1, result.ErrorCount);
        Assert.Equal(batchId, repository.MergedBatchId);
        Assert.Single(repository.RawRows, row => row.BusinessKey == "A001");
        Assert.Single(repository.CleanRows, row => row.BusinessKey == "A001");
        Assert.Single(repository.ErrorRows, row => row.RowNumber == 3);
        Assert.All(repository.RawRows, row => Assert.Equal(batchId, row.BatchId));
        Assert.Equal(1, repository.ImportSessionExecutionCount);
        Assert.Equal(1, repository.RawBatchExecutionCount);
        Assert.Equal(1, repository.CleanBatchExecutionCount);
        Assert.Equal(1, repository.ErrorBatchExecutionCount);
        Assert.Equal(0, repository.SingleRawInsertCount);
        Assert.Equal(0, repository.SingleCleanInsertCount);
        Assert.Equal(0, repository.SingleErrorInsertCount);
        Assert.Equal(1, excel.ValidateStructureCallCount);
        Assert.Equal(0, excel.ValidateCallCount);
    }

    [Fact]
    public async Task ImportAsync_ShouldFailFast_WhenValidationFails()
    {
        var excel = new StubDrugExcelImportService(Array.Empty<DrugImportRow>(), new DrugImportPreview
        {
            FilePath = "broken.xlsx",
            IsValid = false,
            Errors = new List<string> { "缺少工作表: 关联关系表" }
        });
        var repository = new StubDrugImportRepository(Guid.NewGuid());
        var service = new DrugImportPipelineService(excel, repository);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ImportAsync("broken.xlsx"));

        Assert.Contains("关联关系表", ex.Message);
        Assert.Empty(repository.RawRows);
        Assert.Empty(repository.CleanRows);
        Assert.Empty(repository.ErrorRows);
        Assert.Null(repository.MergedBatchId);
        Assert.Equal(0, repository.ImportSessionExecutionCount);
        Assert.Equal(1, excel.ValidateStructureCallCount);
        Assert.Equal(0, excel.ValidateCallCount);
    }

    private sealed class StubDrugExcelImportService : IDrugExcelImportService
    {
        private readonly IReadOnlyList<DrugImportRow> _rows;
        private readonly DrugImportPreview _preview;
        public int ValidateCallCount { get; private set; }
        public int ValidateStructureCallCount { get; private set; }

        public StubDrugExcelImportService(IReadOnlyList<DrugImportRow> rows, DrugImportPreview? preview = null)
        {
            _rows = rows;
            _preview = preview ?? new DrugImportPreview
            {
                FilePath = "medical.xlsx",
                IsValid = true,
                Summary = "工作表完整"
            };
        }

        public Task<DrugImportPreview> ValidateAsync(string filePath, CancellationToken cancellationToken = default)
        {
            ValidateCallCount++;
            return Task.FromResult(_preview);
        }

        public Task<DrugImportPreview> ValidateStructureAsync(string filePath, CancellationToken cancellationToken = default)
        {
            ValidateStructureCallCount++;
            return Task.FromResult(_preview);
        }

        public async IAsyncEnumerable<DrugImportRow> ReadRowsAsync(string filePath, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var row in _rows)
            {
                yield return row;
                await Task.Yield();
            }
        }
    }

    private sealed class StubDrugImportRepository : IDrugImportRepository
    {
        private readonly Guid _batchId;

        public StubDrugImportRepository(Guid batchId)
        {
            _batchId = batchId;
        }

        public List<DrugImportRow> RawRows { get; } = new();
        public List<DrugImportRow> CleanRows { get; } = new();
        public List<DrugImportRow> ErrorRows { get; } = new();
        public Guid? MergedBatchId { get; private set; }
        public int ImportSessionExecutionCount { get; private set; }
        public int RawBatchExecutionCount { get; private set; }
        public int CleanBatchExecutionCount { get; private set; }
        public int ErrorBatchExecutionCount { get; private set; }
        public int SingleRawInsertCount { get; private set; }
        public int SingleCleanInsertCount { get; private set; }
        public int SingleErrorInsertCount { get; private set; }

        public Task<DrugImportBatch> CreateBatchAsync(string sourceFile, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DrugImportBatch
            {
                BatchId = _batchId,
                SourceFile = sourceFile
            });
        }

        public Task InsertRawAsync(DrugImportRow row, CancellationToken cancellationToken = default)
        {
            SingleRawInsertCount++;
            RawRows.Add(Clone(row));
            return Task.CompletedTask;
        }

        public Task InsertRawBatchAsync(IReadOnlyList<DrugImportRow> rows, CancellationToken cancellationToken = default)
        {
            RawBatchExecutionCount++;
            RawRows.AddRange(rows.Select(Clone));
            return Task.CompletedTask;
        }

        public Task InsertCleanAsync(DrugImportRow row, CancellationToken cancellationToken = default)
        {
            SingleCleanInsertCount++;
            CleanRows.Add(Clone(row));
            return Task.CompletedTask;
        }

        public Task InsertCleanBatchAsync(IReadOnlyList<DrugImportRow> rows, CancellationToken cancellationToken = default)
        {
            CleanBatchExecutionCount++;
            CleanRows.AddRange(rows.Select(Clone));
            return Task.CompletedTask;
        }

        public Task InsertErrorAsync(DrugImportRow row, CancellationToken cancellationToken = default)
        {
            SingleErrorInsertCount++;
            ErrorRows.Add(Clone(row));
            return Task.CompletedTask;
        }

        public Task InsertErrorBatchAsync(IReadOnlyList<DrugImportRow> rows, CancellationToken cancellationToken = default)
        {
            ErrorBatchExecutionCount++;
            ErrorRows.AddRange(rows.Select(Clone));
            return Task.CompletedTask;
        }

        public Task MergeBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
        {
            MergedBatchId = batchId;
            return Task.CompletedTask;
        }

        public Task ExecuteInImportSessionAsync(
            Func<CancellationToken, Task> work,
            CancellationToken cancellationToken = default)
        {
            ImportSessionExecutionCount++;
            return work(cancellationToken);
        }

        private static DrugImportRow Clone(DrugImportRow row)
        {
            return new DrugImportRow
            {
                BatchId = row.BatchId,
                SourceSheet = row.SourceSheet,
                RowNumber = row.RowNumber,
                BusinessKey = row.BusinessKey,
                RawData = new Dictionary<string, string?>(row.RawData, StringComparer.OrdinalIgnoreCase),
                NormalizedData = new Dictionary<string, string?>(row.NormalizedData, StringComparer.OrdinalIgnoreCase),
                ErrorCode = row.ErrorCode,
                ErrorMessage = row.ErrorMessage
            };
        }
    }
}
