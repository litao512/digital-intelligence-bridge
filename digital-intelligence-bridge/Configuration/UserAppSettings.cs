namespace DigitalIntelligenceBridge.Configuration;

public sealed class UserAppSettings
{
    public UserApplicationConfig Application { get; set; } = new();
    public UserTrayConfig Tray { get; set; } = new();
    public UserReleaseCenterConfig ReleaseCenter { get; set; } = new();
}

public sealed class UserApplicationConfig
{
    public bool MinimizeToTray { get; set; } = true;
    public bool StartWithSystem { get; set; }
}

public sealed class UserTrayConfig
{
    public bool ShowNotifications { get; set; } = true;
}

public sealed class UserReleaseCenterConfig
{
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string Channel { get; set; } = "stable";
    public string SiteId { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string SiteRemark { get; set; } = string.Empty;
    public string CacheDirectory { get; set; } = string.Empty;
    public string ClientCacheDirectory { get; set; } = string.Empty;
    public string StagingDirectory { get; set; } = string.Empty;
    public string RuntimePluginRoot { get; set; } = string.Empty;
    public string BackupDirectory { get; set; } = string.Empty;
}
