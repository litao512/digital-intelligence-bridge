using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DigitalIntelligenceBridge.Services;
using Prism.Commands;

namespace DigitalIntelligenceBridge.ViewModels;

/// <summary>
/// 医保药品导入工具视图模型
/// </summary>
public class DrugImportViewModel : ViewModelBase
{
    private readonly IDrugExcelImportService _excelImportService;
    private readonly IDrugImportPipelineService _pipelineService;
    private readonly ISqlServerDrugSyncService _syncService;
    private readonly ILoggerService<DrugImportViewModel> _logger;

    private string _selectedFilePath = string.Empty;
    private bool _isPreviewValid;
    private string _previewSummary = "尚未预检";
    private string _batchResult = string.Empty;
    private string _syncResult = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    private int _rawCount;
    private int _cleanCount;
    private int _errorCount;
    private int _syncUpdateCount;
    private Guid? _lastBatchId;
    private string _lastBatchSourceFile = string.Empty;

    public DrugImportViewModel(
        IDrugExcelImportService excelImportService,
        IDrugImportPipelineService pipelineService,
        ISqlServerDrugSyncService syncService,
        ILoggerService<DrugImportViewModel> logger)
    {
        _excelImportService = excelImportService;
        _pipelineService = pipelineService;
        _syncService = syncService;
        _logger = logger;

        ValidateCommand = new DelegateCommand(async () => await ValidateAsync(), () => !IsBusy && !string.IsNullOrWhiteSpace(SelectedFilePath));
        ImportCommand = new DelegateCommand(async () => await ImportAsync(), () => !IsBusy && CanImport);
        SyncCommand = new DelegateCommand(async () => await SyncAsync(), () => !IsBusy && CanSync);
        RetrySyncCommand = new DelegateCommand(async () => await RetrySyncAsync(), () => !IsBusy && CanRetrySync);
    }

    public ObservableCollection<string> SheetRowCounts { get; } = new();

    public string SelectedFilePath
    {
        get => _selectedFilePath;
        set
        {
            if (SetProperty(ref _selectedFilePath, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public bool IsPreviewValid
    {
        get => _isPreviewValid;
        private set
        {
            if (SetProperty(ref _isPreviewValid, value))
            {
                RaisePropertyChanged(nameof(CanImport));
                RaiseCommandStates();
            }
        }
    }

    public string PreviewSummary
    {
        get => _previewSummary;
        private set => SetProperty(ref _previewSummary, value);
    }

    public string BatchResult
    {
        get => _batchResult;
        private set => SetProperty(ref _batchResult, value);
    }

    public string SyncResult
    {
        get => _syncResult;
        private set
        {
            if (SetProperty(ref _syncResult, value))
            {
                RaisePropertyChanged(nameof(CanRetrySync));
                RaiseCommandStates();
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public int RawCount
    {
        get => _rawCount;
        private set => SetProperty(ref _rawCount, value);
    }

    public int CleanCount
    {
        get => _cleanCount;
        private set => SetProperty(ref _cleanCount, value);
    }

    public int ErrorCount
    {
        get => _errorCount;
        private set => SetProperty(ref _errorCount, value);
    }

    public int SyncUpdateCount
    {
        get => _syncUpdateCount;
        private set => SetProperty(ref _syncUpdateCount, value);
    }

    public bool HasLastBatchContext => _lastBatchId.HasValue;

    public string LastBatchIdText => _lastBatchId?.ToString() ?? "无";

    public string LastBatchSummary => HasLastBatchContext
        ? $"{LastBatchIdText} | {_lastBatchSourceFile}"
        : "尚无已完成批次";

    public bool CanImport => IsPreviewValid;

    public bool CanSync => _lastBatchId.HasValue && BatchResult == "Succeeded";

    public bool CanRetrySync => _lastBatchId.HasValue && (SyncResult == "Failed" || SyncResult == "Succeeded");

    public DelegateCommand ValidateCommand { get; }

    public DelegateCommand ImportCommand { get; }

    public DelegateCommand SyncCommand { get; }

    public DelegateCommand RetrySyncCommand { get; }

    public async Task ValidateAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            ErrorMessage = "请先选择 Excel 文件";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var preview = await _excelImportService.ValidateAsync(SelectedFilePath);

            SheetRowCounts.Clear();
            foreach (var item in preview.SheetRowCounts)
            {
                SheetRowCounts.Add($"{item.Key}: {item.Value}");
            }

            IsPreviewValid = preview.IsValid;
            PreviewSummary = preview.Summary;
            ErrorMessage = preview.IsValid ? string.Empty : string.Join("；", preview.Errors);
        });
    }

    public async Task ImportAsync()
    {
        await RunBusyAsync(async () =>
        {
            var result = await _pipelineService.ImportAsync(SelectedFilePath);
            _lastBatchId = result.BatchId;
            _lastBatchSourceFile = string.IsNullOrWhiteSpace(result.SourceFile)
                ? SelectedFilePath
                : result.SourceFile;
            BatchResult = result.Result;
            RawCount = result.RawCount;
            CleanCount = result.CleanCount;
            ErrorCount = result.ErrorCount;
            ErrorMessage = result.LastError;
            SyncResult = string.Empty;
            SyncUpdateCount = 0;
            RaisePropertyChanged(nameof(CanSync));
            RaisePropertyChanged(nameof(CanRetrySync));
            RaisePropertyChanged(nameof(HasLastBatchContext));
            RaisePropertyChanged(nameof(LastBatchIdText));
            RaisePropertyChanged(nameof(LastBatchSummary));
        });
    }

    public async Task SyncAsync()
    {
        if (!_lastBatchId.HasValue)
        {
            ErrorMessage = "没有可同步的导入批次";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var result = await _syncService.SyncBatchAsync(_lastBatchId.Value);
            SyncResult = result.Result;
            SyncUpdateCount = result.SyncUpdateCount;
            ErrorMessage = result.LastError;
        });
    }

    public Task RetrySyncAsync()
    {
        return SyncAsync();
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _logger.LogError(ex, "医保药品导入工具执行失败");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RaiseCommandStates()
    {
        ValidateCommand.RaiseCanExecuteChanged();
        ImportCommand.RaiseCanExecuteChanged();
        SyncCommand.RaiseCanExecuteChanged();
        RetrySyncCommand.RaiseCanExecuteChanged();
        RaisePropertyChanged(nameof(CanSync));
        RaisePropertyChanged(nameof(CanRetrySync));
        RaisePropertyChanged(nameof(HasLastBatchContext));
        RaisePropertyChanged(nameof(LastBatchIdText));
        RaisePropertyChanged(nameof(LastBatchSummary));
    }
}
