using System;
using System.Collections.Generic;

namespace DigitalIntelligenceBridge.Configuration;

public class AppSettings
{
    public ApplicationConfig Application { get; set; } = new();
    public TrayConfig Tray { get; set; } = new();
    public PluginConfig Plugin { get; set; } = new();
    public SupabaseConfig Supabase { get; set; } = new();
    public ReleaseCenterConfig ReleaseCenter { get; set; } = new();
    public LoggingConfig Logging { get; set; } = new();
}

public class ApplicationConfig
{
    public string Name { get; set; } = "DIB客户端";
    public string Version { get; set; } = "1.0.0";
    public bool MinimizeToTray { get; set; } = true;
    public bool StartWithSystem { get; set; } = false;
}

public class TrayConfig
{
    public string IconPath { get; set; } = "Assets/avalonia-logo.ico";
    public bool ShowNotifications { get; set; } = true;
}

public class PluginConfig
{
    public string PluginDirectory { get; set; } = "plugins";
    public bool AutoLoad { get; set; } = true;
    public bool AllowUnsigned { get; set; } = false;
}

public class SupabaseConfig
{
    public string Url { get; set; } = "http://localhost:54321";
    public string AnonKey { get; set; } = string.Empty;
    public string ServiceRoleKey { get; set; } = string.Empty;
    public string Schema { get; set; } = "dib";
}

public class ReleaseCenterConfig
{
    public bool Enabled { get; set; } = false;
    public string BaseUrl { get; set; } = string.Empty;
    public string Channel { get; set; } = "stable";
    public string AnonKey { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string SiteRemark { get; set; } = string.Empty;
    public string CacheDirectory { get; set; } = string.Empty;
    public string ClientCacheDirectory { get; set; } = string.Empty;
    public string StagingDirectory { get; set; } = string.Empty;
    public string RuntimePluginRoot { get; set; } = string.Empty;
    public string BackupDirectory { get; set; } = string.Empty;
}

public class LoggingConfig
{
    public LogLevelConfig LogLevel { get; set; } = new();
    public string LogPath { get; set; } = "logs";
}

public class LogLevelConfig
{
    public string Default { get; set; } = "Information";
    public string Microsoft { get; set; } = "Warning";
}

