using System;
using System.Collections.Generic;

namespace DigitalIntelligenceBridge.Configuration;

/// <summary>
/// 应用程序配置根节点
/// </summary>
public class AppSettings
{
    /// <summary>
    /// 应用程序基本配置
    /// </summary>
    public ApplicationConfig Application { get; set; } = new();

    /// <summary>
    /// 系统托盘配置
    /// </summary>
    public TrayConfig Tray { get; set; } = new();

    /// <summary>
    /// 插件系统配置
    /// </summary>
    public PluginConfig Plugin { get; set; } = new();

    /// <summary>
    /// Supabase 后端配置
    /// </summary>
    public SupabaseConfig Supabase { get; set; } = new();

    /// <summary>
    /// 导航菜单配置
    /// </summary>
    public List<NavigationMenuItemConfig> Navigation { get; set; } = new();

    /// <summary>
    /// 日志配置
    /// </summary>
    public LoggingConfig Logging { get; set; } = new();
}

/// <summary>
/// 应用程序基本配置
/// </summary>
public class ApplicationConfig
{
    public string Name { get; set; } = "通用工具箱";
    public string Version { get; set; } = "1.0.0";
    public bool MinimizeToTray { get; set; } = true;
    public bool StartWithSystem { get; set; } = false;
}

/// <summary>
/// 系统托盘配置
/// </summary>
public class TrayConfig
{
    public string IconPath { get; set; } = "Assets/avalonia-logo.ico";
    public bool ShowNotifications { get; set; } = true;
}

/// <summary>
/// 插件系统配置
/// </summary>
public class PluginConfig
{
    public string PluginDirectory { get; set; } = "plugins";
    public bool AutoLoad { get; set; } = true;
    public bool AllowUnsigned { get; set; } = false;
}

/// <summary>
/// 导航菜单项配置
/// </summary>
public class NavigationMenuItemConfig
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsInstalled { get; set; } = true;
    public int Order { get; set; } = 0;
}

/// <summary>
/// Supabase 后端配置
/// </summary>
public class SupabaseConfig
{
    public string Url { get; set; } = "http://localhost:54321";
    public string AnonKey { get; set; } = string.Empty;
}

// 注：WebApplicationConfig 已移除，WebView 功能将作为可选插件在后续版本提供

/// <summary>
/// 日志配置
/// </summary>
public class LoggingConfig
{
    public LogLevelConfig LogLevel { get; set; } = new();
    public string LogPath { get; set; } = "logs";
}

/// <summary>
/// 日志级别配置
/// </summary>
public class LogLevelConfig
{
    public string Default { get; set; } = "Information";
    public string Microsoft { get; set; } = "Warning";
}
