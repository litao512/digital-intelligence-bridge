using System.ComponentModel;
using System.Runtime.CompilerServices;
using MedicalDrugImport.Plugin.Configuration;
using MedicalDrugImport.Plugin.Models;
using MedicalDrugImport.Plugin.Services;

namespace MedicalDrugImport.Plugin.ViewModels;

public class DrugImportPluginViewModel : INotifyPropertyChanged
{
    private readonly IDrugExcelImportService? _excelImportService;
    private readonly IDrugImportPipelineService? _importPipelineService;
    private readonly ISqlServerDrugSyncService? _sqlServerDrugSyncService;
    private string _selectedFilePath = string.Empty;
    private bool _isPreviewValid;
    private string _previewSummary = string.Empty;
    private string _importResult = string.Empty;
    private int _rawCount;
    private int _cleanCount;
    private int _errorCount;
    private string _syncResult = string.Empty;
    private int _syncUpdateCount;
    private string _syncPreviewSummary = string.Empty;
    private string _syncGuardSummary = string.Empty;
    private bool _canSyncToSqlServer;
    private string _errorMessage = string.Empty;
    private Guid? _lastBatchId;

    public DrugImportPluginViewModel()
    {
    }

    public DrugImportPluginViewModel(
        IDrugExcelImportService? excelImportService,
        IDrugImportPipelineService? importPipelineService = null,
        ISqlServerDrugSyncService? sqlServerDrugSyncService = null,
        PluginSettings? settings = null)
    {
        _excelImportService = excelImportService;
        _importPipelineService = importPipelineService;
        _sqlServerDrugSyncService = sqlServerDrugSyncService;
        CanSyncToSqlServer = settings?.SqlServer.EnableWrites ?? false;
        SyncGuardSummary = BuildSyncGuardSummary(settings);
    }

    public string SelectedFilePath
    {
        get => _selectedFilePath;
        set
        {
            if (_selectedFilePath == value)
            {
                return;
            }

            _selectedFilePath = value;
            OnPropertyChanged();
        }
    }

    public bool IsPreviewValid
    {
        get => _isPreviewValid;
        private set
        {
            if (_isPreviewValid == value)
            {
                return;
            }

            _isPreviewValid = value;
            OnPropertyChanged();
        }
    }

    public string PreviewSummary
    {
        get => _previewSummary;
        private set
        {
            if (_previewSummary == value)
            {
                return;
            }

            _previewSummary = value;
            OnPropertyChanged();
        }
    }

    public Dictionary<string, int> SheetRowCounts { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Guid? LastBatchId
    {
        get => _lastBatchId;
        set
        {
            if (_lastBatchId == value)
            {
                return;
            }

            _lastBatchId = value;
            OnPropertyChanged();
        }
    }

    public string SyncResult
    {
        get => _syncResult;
        private set
        {
            if (_syncResult == value)
            {
                return;
            }

            _syncResult = value;
            OnPropertyChanged();
        }
    }

    public string ImportResult
    {
        get => _importResult;
        private set
        {
            if (_importResult == value)
            {
                return;
            }

            _importResult = value;
            OnPropertyChanged();
        }
    }

    public int RawCount
    {
        get => _rawCount;
        private set
        {
            if (_rawCount == value)
            {
                return;
            }

            _rawCount = value;
            OnPropertyChanged();
        }
    }

    public int CleanCount
    {
        get => _cleanCount;
        private set
        {
            if (_cleanCount == value)
            {
                return;
            }

            _cleanCount = value;
            OnPropertyChanged();
        }
    }

    public int ErrorCount
    {
        get => _errorCount;
        private set
        {
            if (_errorCount == value)
            {
                return;
            }

            _errorCount = value;
            OnPropertyChanged();
        }
    }

    public int SyncUpdateCount
    {
        get => _syncUpdateCount;
        private set
        {
            if (_syncUpdateCount == value)
            {
                return;
            }

            _syncUpdateCount = value;
            OnPropertyChanged();
        }
    }

    public string SyncPreviewSummary
    {
        get => _syncPreviewSummary;
        private set
        {
            if (_syncPreviewSummary == value)
            {
                return;
            }

            _syncPreviewSummary = value;
            OnPropertyChanged();
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (_errorMessage == value)
            {
                return;
            }

            _errorMessage = value;
            OnPropertyChanged();
        }
    }

    public bool CanSyncToSqlServer
    {
        get => _canSyncToSqlServer;
        private set
        {
            if (_canSyncToSqlServer == value)
            {
                return;
            }

            _canSyncToSqlServer = value;
            OnPropertyChanged();
        }
    }

    public string SyncGuardSummary
    {
        get => _syncGuardSummary;
        private set
        {
            if (_syncGuardSummary == value)
            {
                return;
            }

            _syncGuardSummary = value;
            OnPropertyChanged();
        }
    }

    public async Task ValidateAsync(CancellationToken cancellationToken = default)
    {
        if (_excelImportService is null || string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            return;
        }

        var preview = await _excelImportService.ValidateAsync(SelectedFilePath, cancellationToken);
        ApplyPreview(preview);
    }

    public async Task ImportAsync(CancellationToken cancellationToken = default)
    {
        if (_importPipelineService is null || string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            return;
        }

        ErrorMessage = string.Empty;

        try
        {
            var batch = await _importPipelineService.ImportAsync(SelectedFilePath, cancellationToken);
            LastBatchId = batch.BatchId;
            ImportResult = batch.Result;
            RawCount = batch.RawCount;
            CleanCount = batch.CleanCount;
            ErrorCount = batch.ErrorCount;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    public Task SyncAsync(CancellationToken cancellationToken = default)
    {
        return SyncInternalAsync(cancellationToken);
    }

    public Task RetrySyncAsync(CancellationToken cancellationToken = default)
    {
        return SyncInternalAsync(cancellationToken);
    }

    public async Task PreviewSyncAsync(CancellationToken cancellationToken = default)
    {
        if (_sqlServerDrugSyncService is null || LastBatchId is null)
        {
            return;
        }

        ErrorMessage = string.Empty;

        try
        {
            var preview = await _sqlServerDrugSyncService.PreviewBatchAsync(LastBatchId.Value, cancellationToken);
            SyncPreviewSummary = $"待同步 {preview.SyncUpdateCount} 条，状态 {preview.Result}。{preview.LastError}";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void ApplyPreview(DrugImportPreview preview)
    {
        IsPreviewValid = preview.IsValid;
        PreviewSummary = preview.Summary;
        SheetRowCounts.Clear();
        foreach (var pair in preview.SheetRowCounts)
        {
            SheetRowCounts[pair.Key] = pair.Value;
        }

        OnPropertyChanged(nameof(SheetRowCounts));
    }

    private async Task SyncInternalAsync(CancellationToken cancellationToken)
    {
        if (_sqlServerDrugSyncService is null || LastBatchId is null)
        {
            return;
        }

        ErrorMessage = string.Empty;

        try
        {
            var batch = await _sqlServerDrugSyncService.SyncBatchAsync(LastBatchId.Value, cancellationToken);
            SyncResult = batch.Result;
            SyncUpdateCount = batch.SyncUpdateCount;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static string BuildSyncGuardSummary(PluginSettings? settings)
    {
        var batchSize = settings?.Import.BatchSize ?? 1000;
        var maxSyncRows = settings?.Import.MaxSyncRowsPerRun ?? 50;
        var writesEnabled = settings?.SqlServer.EnableWrites ?? false;
        var unsafeFullSync = settings?.Import.AllowUnsafeFullSync ?? false;
        var writeMode = writesEnabled ? "已开启真实写入" : "只读模式";
        var unsafeMode = unsafeFullSync ? "允许超阈值全量" : "禁止超阈值全量";
        return $"SQL Server 当前{writeMode}；每批 {batchSize} 条；单次上限 {maxSyncRows} 条；{unsafeMode}。";
    }
}
