using Avalonia.Controls;
using DigitalIntelligenceBridge.Models;
using DigitalIntelligenceBridge.Services;
using DigitalIntelligenceBridge.ViewModels;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class ViewLocatorTests
{
    [Fact]
    public void Build_ShouldResolveDrugImportView_WhenGivenDrugImportViewModel()
    {
        var locator = new ViewLocator();
        var viewModel = new DrugImportViewModel(
            new StubDrugExcelImportService(),
            new StubDrugImportPipelineService(),
            new StubSqlServerDrugSyncService(),
            new NullLoggerService<DrugImportViewModel>());

        var view = locator.Build(viewModel);

        Assert.NotNull(view);
        Assert.IsType<DigitalIntelligenceBridge.Views.DrugImportView>(view);
    }

    [Fact]
    public void Match_ShouldReturnTrue_WhenDataIsViewModelBase()
    {
        var locator = new ViewLocator();

        Assert.True(locator.Match(new DummyViewModel()));
        Assert.False(locator.Match(new object()));
    }

    private sealed class DummyViewModel : ViewModelBase
    {
    }

    private sealed class StubDrugExcelImportService : IDrugExcelImportService
    {
        public Task<DrugImportPreview> ValidateAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DrugImportPreview { FilePath = filePath, IsValid = true });
        }

        public Task<DrugImportPreview> ValidateStructureAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DrugImportPreview { FilePath = filePath, IsValid = true });
        }

        public async IAsyncEnumerable<DrugImportRow> ReadRowsAsync(string filePath, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield break;
        }
    }

    private sealed class StubDrugImportPipelineService : IDrugImportPipelineService
    {
        public Task<DrugImportBatch> ImportAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DrugImportBatch());
        }
    }

    private sealed class StubSqlServerDrugSyncService : ISqlServerDrugSyncService
    {
        public Task<DrugImportBatch> SyncBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DrugImportBatch());
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

