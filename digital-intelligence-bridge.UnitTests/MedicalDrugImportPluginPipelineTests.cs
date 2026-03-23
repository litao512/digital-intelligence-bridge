using MedicalDrugImport.Plugin.Models;
using MedicalDrugImport.Plugin.Services;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class MedicalDrugImportPluginPipelineTests
{
    [Fact]
    public async Task ImportAsync_ShouldUseStructureValidation_InsteadOfFullValidation()
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
            }
        ]);
        var repository = new StubDrugImportRepository(batchId);
        var service = new DrugImportPipelineService(excel, repository);

        var result = await service.ImportAsync(@"C:\imports\medical.xlsx");

        Assert.Equal(batchId, result.BatchId);
        Assert.Equal(1, excel.ValidateStructureCallCount);
        Assert.Equal(0, excel.ValidateCallCount);
    }

    private sealed class StubDrugExcelImportService : IDrugExcelImportService
    {
        private readonly IReadOnlyList<DrugImportRow> _rows;
        private readonly DrugImportPreview _preview;

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

        public int ValidateCallCount { get; private set; }

        public int ValidateStructureCallCount { get; private set; }

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

        public async IAsyncEnumerable<DrugImportRow> ReadRowsAsync(
            string filePath,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
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

        public Task<DrugImportBatch> CreateBatchAsync(string sourceFile, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DrugImportBatch
            {
                BatchId = _batchId,
                SourceFile = sourceFile
            });
        }

        public Task ExecuteInImportSessionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default)
        {
            return work(cancellationToken);
        }

        public Task InsertRawAsync(DrugImportRow row, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task InsertRawBatchAsync(IReadOnlyList<DrugImportRow> rows, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task InsertCleanAsync(DrugImportRow row, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task InsertCleanBatchAsync(IReadOnlyList<DrugImportRow> rows, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task InsertErrorAsync(DrugImportRow row, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task InsertErrorBatchAsync(IReadOnlyList<DrugImportRow> rows, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task MergeBatchAsync(Guid batchId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> CountAffectedCatalogRowsAsync(Guid batchId, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public IAsyncEnumerable<DrugImportRow> GetAffectedCatalogRowsAsync(Guid batchId, CancellationToken cancellationToken = default)
        {
            return AsyncEnumerable.Empty<DrugImportRow>();
        }
    }
}
