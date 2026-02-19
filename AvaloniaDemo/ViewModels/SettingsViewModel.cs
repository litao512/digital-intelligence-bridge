using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using AvaloniaDemo.Configuration;
using AvaloniaDemo.Services;
using Microsoft.Extensions.Options;
using Prism.Commands;
using Prism.Mvvm;

namespace AvaloniaDemo.ViewModels;

/// <summary>
/// 设置页面视图模型
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    private readonly IApplicationService _appService;
    private readonly ITrayService _trayService;
    private readonly ILoggerService<SettingsViewModel> _logger;
    private readonly AppSettings _settings;

    // 外观设置
    private bool _isDarkMode;
    private bool _startMinimizedToTray;
    private bool _startWithSystem;

    // 托盘设置
    private bool _showTrayNotifications;
    private string _selectedTrayIcon = "默认图标";

    // 数据设置
    private int _autoSaveInterval = 5;

    // 关于信息
    private string _appName = "通用工具箱";
    private string _appVersion = "1.0.0";

    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (SetProperty(ref _isDarkMode, value))
            {
                // 应用主题切换
                ApplyTheme(value);
            }
        }
    }

    public bool StartMinimizedToTray
    {
        get => _startMinimizedToTray;
        set => SetProperty(ref _startMinimizedToTray, value);
    }

    public bool StartWithSystem
    {
        get => _startWithSystem;
        set => SetProperty(ref _startWithSystem, value);
    }

    public bool ShowTrayNotifications
    {
        get => _showTrayNotifications;
        set
        {
            if (SetProperty(ref _showTrayNotifications, value))
            {
                _trayService?.SetShowNotifications(value);
            }
        }
    }

    public string SelectedTrayIcon
    {
        get => _selectedTrayIcon;
        set => SetProperty(ref _selectedTrayIcon, value);
    }

    public int AutoSaveInterval
    {
        get => _autoSaveInterval;
        set => SetProperty(ref _autoSaveInterval, value);
    }

    public string AppName
    {
        get => _appName;
        set => SetProperty(ref _appName, value);
    }

    public string AppVersion
    {
        get => _appVersion;
        set => SetProperty(ref _appVersion, value);
    }

    // 命令
    public DelegateCommand SaveSettingsCommand { get; }
    public DelegateCommand ExportDataCommand { get; }
    public DelegateCommand ImportDataCommand { get; }
    public DelegateCommand ClearAllDataCommand { get; }
    public DelegateCommand CheckUpdateCommand { get; }
    public DelegateCommand OpenLogFolderCommand { get; }

    public SettingsViewModel(
        IApplicationService appService,
        ITrayService trayService,
        ILoggerService<SettingsViewModel> logger,
        IOptions<AppSettings> settings)
    {
        _appService = appService;
        _trayService = trayService;
        _logger = logger;
        _settings = settings.Value;

        // 加载当前设置
        LoadSettings();

        // 初始化命令
        SaveSettingsCommand = new DelegateCommand(SaveSettings);
        ExportDataCommand = new DelegateCommand(ExportData);
        ImportDataCommand = new DelegateCommand(ImportData);
        ClearAllDataCommand = new DelegateCommand(ClearAllData);
        CheckUpdateCommand = new DelegateCommand(CheckUpdate);
        OpenLogFolderCommand = new DelegateCommand(OpenLogFolder);

        _logger.LogInformation("设置页面已初始化");
    }

    private void LoadSettings()
    {
        // 从配置加载设置
        IsDarkMode = false; // TODO: 从配置读取
        StartMinimizedToTray = _settings.Application.MinimizeToTray;
        StartWithSystem = _settings.Application.StartWithSystem;
        ShowTrayNotifications = _settings.Tray.ShowNotifications;
        AutoSaveInterval = 5; // TODO: 从配置读取
        AppName = _settings.Application.Name;
        AppVersion = _settings.Application.Version;
    }

    private void ApplyTheme(bool isDarkMode)
    {
        // 应用主题切换
        var app = Application.Current;
        if (app != null)
        {
            app.RequestedThemeVariant = isDarkMode
                ? Avalonia.Styling.ThemeVariant.Dark
                : Avalonia.Styling.ThemeVariant.Light;
        }
        _logger.LogInformation($"主题已切换为: {(isDarkMode ? "深色" : "浅色")}");
    }

    private void SaveSettings()
    {
        try
        {
            // 保存设置到配置
            _settings.Application.MinimizeToTray = StartMinimizedToTray;
            _settings.Application.StartWithSystem = StartWithSystem;
            _settings.Tray.ShowNotifications = ShowTrayNotifications;

            // TODO: 保存到配置文件

            _logger.LogInformation("设置已保存");
        }
        catch (Exception ex)
        {
            _logger.LogError($"保存设置失败: {ex}");
        }
    }

    private void ExportData()
    {
        _logger.LogInformation("导出数据...");
        // TODO: 实现数据导出
    }

    private void ImportData()
    {
        _logger.LogInformation("导入数据...");
        // TODO: 实现数据导入
    }

    private void ClearAllData()
    {
        _logger.LogInformation("清除所有数据...");
        // TODO: 实现数据清除
    }

    private void CheckUpdate()
    {
        _logger.LogInformation("检查更新...");
        // TODO: 实现更新检查
    }

    private void OpenLogFolder()
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "logs");
            if (Directory.Exists(logPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = logPath,
                    UseShellExecute = true
                });
                _logger.LogInformation($"打开日志文件夹: {logPath}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"打开日志文件夹失败: {ex}");
        }
    }
}
