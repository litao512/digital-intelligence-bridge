using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DigitalIntelligenceBridge.Services;

/// <summary>
/// 托盘服务实现
/// </summary>
public class TrayService : ITrayService
{
    private Window? _mainWindow;
    private TrayIcon? _trayIcon;
    private NativeMenu? _trayMenu;
    private readonly ILogger<TrayService> _logger;
    private readonly Dictionary<string, NativeMenuItem> _menuItems = new();
    private bool _isExiting = false;

    public bool IsWindowVisible => _mainWindow?.IsVisible ?? false;
    public bool IsExiting => _isExiting;

    public TrayService(ILogger<TrayService> logger)
    {
        _logger = logger;
    }

    public void Initialize(Window mainWindow)
    {
        _mainWindow = mainWindow;

        // 创建托盘菜单
        _trayMenu = new NativeMenu();

        // 添加默认菜单项
        AddDefaultMenuItems();

        var trayIcon = LoadIcon("Assets/avalonia-logo.ico") ?? _mainWindow.Icon;

        // 创建托盘图标
        _trayIcon = new TrayIcon
        {
            Icon = trayIcon,
            ToolTipText = "通用工具箱",
            Menu = _trayMenu
        };

        _trayIcon.Clicked += OnTrayIconClicked;

        // 处理窗口关闭事件
        _mainWindow.Closing += OnWindowClosing;

        // 将托盘图标添加到应用
        if (TrayIcon.GetIcons(Application.Current!) is TrayIcons icons)
        {
            icons.Add(_trayIcon);
        }

        _logger.LogInformation("托盘服务初始化完成");
    }

    private void AddDefaultMenuItems()
    {
        // 显示窗口
        var showItem = new NativeMenuItem("显示窗口");
        showItem.Click += (s, e) => ShowWindow();
        _trayMenu?.Add(showItem);
        _menuItems["show"] = showItem;

        // 分隔线
        _trayMenu?.Add(new NativeMenuItemSeparator());

        // 退出
        var exitItem = new NativeMenuItem("退出");
        exitItem.Click += (s, e) => ExitApplication();
        _trayMenu?.Add(exitItem);
        _menuItems["exit"] = exitItem;
    }

    private WindowIcon? LoadIcon(string iconPath)
    {
        try
        {
            var fullPath = Path.Combine(AppContext.BaseDirectory, iconPath);
            if (File.Exists(fullPath))
            {
                using var stream = File.OpenRead(fullPath);
                return new WindowIcon(stream);
            }

            // 尝试从资源加载
            var assemblyName = typeof(TrayService).Assembly.GetName().Name;
            var uri = new Uri($"avares://{assemblyName}/{iconPath}");
            var asset = AssetLoader.Open(uri);
            return new WindowIcon(asset);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "加载托盘图标失败");
            return null;
        }
    }

    private void OnTrayIconClicked(object? sender, EventArgs e)
    {
        ToggleWindow();
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!_isExiting)
        {
            e.Cancel = true;
            HideWindow();
        }
    }

    public void ShowWindow()
    {
        if (_mainWindow == null) return;

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();

        _logger.LogDebug("窗口已显示");
    }

    public void HideWindow()
    {
        if (_mainWindow == null) return;

        _mainWindow.Hide();

        _logger.LogDebug("窗口已隐藏");
    }

    public void ToggleWindow()
    {
        if (_mainWindow == null) return;

        if (_mainWindow.IsVisible)
        {
            HideWindow();
        }
        else
        {
            ShowWindow();
        }
    }

    public void ExitApplication()
    {
        _isExiting = true;

        // 清理托盘图标
        if (_trayIcon != null)
        {
            _trayIcon.Clicked -= OnTrayIconClicked;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        // 移除窗口事件处理
        if (_mainWindow != null)
        {
            _mainWindow.Closing -= OnWindowClosing;
        }

        _logger.LogInformation("应用程序正在退出");

        // 关闭应用
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    public void AddMenuItem(string header, Action callback, string? parentPath = null)
    {
        var menuItem = new NativeMenuItem(header);
        menuItem.Click += (s, e) => callback();

        if (string.IsNullOrEmpty(parentPath))
        {
            _trayMenu?.Add(menuItem);
        }
        else if (_menuItems.TryGetValue(parentPath, out var parentItem) && parentItem.Menu != null)
        {
            parentItem.Menu.Add(menuItem);
        }

        var path = string.IsNullOrEmpty(parentPath) ? header : $"{parentPath}/{header}";
        _menuItems[path] = menuItem;

        _logger.LogDebug("添加菜单项: {Path}", path);
    }

    public void RemoveMenuItem(string path)
    {
        if (_menuItems.TryGetValue(path, out var menuItem) && _trayMenu != null)
        {
            // 使用 System.Collections.Generic 命名空间中的 IList<T> 接口来避免冲突
            var menuList = (System.Collections.Generic.IList<NativeMenuItemBase>)_trayMenu;
            menuList.Remove(menuItem);
            _menuItems.Remove(path);

            _logger.LogDebug("移除菜单项: {Path}", path);
        }
    }

    public void AddSeparator(string? parentPath = null)
    {
        if (string.IsNullOrEmpty(parentPath))
        {
            _trayMenu?.Add(new NativeMenuItemSeparator());
        }
        else if (_menuItems.TryGetValue(parentPath, out var parentItem) && parentItem.Menu != null)
        {
            parentItem.Menu.Add(new NativeMenuItemSeparator());
        }
    }

    public void SetTooltip(string tooltip)
    {
        if (_trayIcon != null)
        {
            _trayIcon.ToolTipText = tooltip;
        }
    }

    private bool _showNotifications = true;

    public void SetShowNotifications(bool show)
    {
        _showNotifications = show;
        _logger.LogInformation("托盘通知设置已更改: {Show}", show);
    }

    public void ShowNotification(string title, string message)
    {
        if (!_showNotifications) return;

        // 注意：Avalonia 本身不提供原生通知 API
        // 实际实现可能需要使用平台特定的库
        // 这里仅记录日志，实际项目中可以使用 DesktopNotifications 等库
        _logger.LogInformation("托盘通知: [{Title}] {Message}", title, message);

        // 临时方案：使用 ToolTipText 显示短暂提示
        if (_trayIcon != null)
        {
            var originalTooltip = _trayIcon.ToolTipText;
            _trayIcon.ToolTipText = $"{title}: {message}";

            // 3秒后恢复原始提示
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                await System.Threading.Tasks.Task.Delay(3000);
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (_trayIcon != null)
                    {
                        _trayIcon.ToolTipText = originalTooltip;
                    }
                });
            });
        }
    }
}
