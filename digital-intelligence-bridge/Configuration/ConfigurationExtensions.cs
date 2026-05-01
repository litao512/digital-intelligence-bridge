using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DigitalIntelligenceBridge.Configuration;

/// <summary>
/// 配置服务扩展方法
/// </summary>
public static class ConfigurationExtensions
{
    public const string DefaultConfigRootName = "DibClient";
    private static readonly JsonSerializerOptions UserSettingsJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// 获取应用配置根目录（默认 LocalAppData，可通过环境变量 DIB_CONFIG_ROOT 覆盖）
    /// </summary>
    public static string GetConfigRootDirectory()
    {
        var overrideDir = Environment.GetEnvironmentVariable("DIB_CONFIG_ROOT");
        if (!string.IsNullOrWhiteSpace(overrideDir))
        {
            Directory.CreateDirectory(overrideDir);
            return overrideDir;
        }

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appDataPath, DefaultConfigRootName);
        Directory.CreateDirectory(appFolder);
        return appFolder;
    }

    /// <summary>
    /// 获取应用程序配置文件的完整路径
    /// </summary>
    public static string GetConfigFilePath()
    {
        return Path.Combine(GetConfigRootDirectory(), "appsettings.json");
    }

    public static string GetLogsDirectory(string logPath = "logs")
    {
        return Path.IsPathRooted(logPath)
            ? logPath
            : Path.Combine(GetConfigRootDirectory(), logPath);
    }

    public static string GetRuntimePluginsDirectory(string pluginDirectory = "plugins")
    {
        return Path.IsPathRooted(pluginDirectory)
            ? pluginDirectory
            : Path.Combine(GetConfigRootDirectory(), pluginDirectory);
    }

    public static string GetReleaseBackupsDirectory()
    {
        return Path.Combine(GetConfigRootDirectory(), "release-backups", "plugins");
    }

    public static string GetAuthorizedResourcesCacheFilePath()
    {
        return Path.Combine(GetConfigRootDirectory(), "resource-cache", "authorized-resources.json");
    }

    /// <summary>
    /// 将配置系统添加到服务容器
    /// </summary>
    public static IServiceCollection AddAppConfiguration(this IServiceCollection services)
    {
        // 获取用户配置路径
        var userConfigPath = GetConfigFilePath();
        var defaultConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        EnsureUserConfigExists(userConfigPath, defaultConfigPath);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile(userConfigPath, optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // 绑定配置到强类型对象
        services.Configure<AppSettings>(options =>
        {
            configuration.Bind(options);
            ConfigurationSafetyValidator.EnsureSafeUserConfiguration(options, userConfigPath);
        });

        return services;
    }
    /// <summary>
    /// 保存当前配置到用户配置文件
    /// </summary>
    public static void SaveAppSettings(this IConfiguration configuration, AppSettings settings)
    {
        var configPath = GetConfigFilePath();
        var userSettings = CreateUserSettingsSnapshot(settings);
        SaveUserSettings(configPath, userSettings);
    }

    public static void EnsureUserConfigExists(string userConfigPath, string defaultConfigPath)
    {
        if (File.Exists(userConfigPath))
        {
            NormalizeExistingUserConfig(userConfigPath, defaultConfigPath);
            return;
        }

        var defaultSettings = LoadDefaultSettings(defaultConfigPath);
        var userSettings = CreateUserSettingsSnapshot(defaultSettings);
        SaveUserSettings(userConfigPath, userSettings);
    }

    public static UserAppSettings CreateUserSettingsSnapshot(AppSettings settings)
    {
        return new UserAppSettings
        {
            Application = new UserApplicationConfig
            {
                MinimizeToTray = settings.Application.MinimizeToTray,
                StartWithSystem = settings.Application.StartWithSystem
            },
            Tray = new UserTrayConfig
            {
                ShowNotifications = settings.Tray.ShowNotifications
            },
            ReleaseCenter = new UserReleaseCenterConfig
            {
                Enabled = settings.ReleaseCenter.Enabled,
                BaseUrl = settings.ReleaseCenter.BaseUrl,
                Channel = settings.ReleaseCenter.Channel,
                SiteId = settings.ReleaseCenter.SiteId,
                SiteName = settings.ReleaseCenter.SiteName,
                SiteRemark = settings.ReleaseCenter.SiteRemark,
                CacheDirectory = settings.ReleaseCenter.CacheDirectory,
                ClientCacheDirectory = settings.ReleaseCenter.ClientCacheDirectory,
                StagingDirectory = settings.ReleaseCenter.StagingDirectory,
                RuntimePluginRoot = settings.ReleaseCenter.RuntimePluginRoot,
                BackupDirectory = settings.ReleaseCenter.BackupDirectory
            }
        };
    }

    public static void SaveUserSettings(string configPath, UserAppSettings userSettings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        var json = JsonSerializer.Serialize(userSettings, UserSettingsJsonOptions);
        File.WriteAllText(configPath, json);
    }

    internal static void RepairReleaseCenterSettings(string userConfigPath, string defaultConfigPath)
    {
        if (!File.Exists(userConfigPath) || !File.Exists(defaultConfigPath))
        {
            return;
        }

        AppSettings? userSettings;
        AppSettings? defaultSettings;
        try
        {
            userSettings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(userConfigPath));
            defaultSettings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(defaultConfigPath));
        }
        catch
        {
            return;
        }

        if (userSettings is null || defaultSettings is null)
        {
            return;
        }

        if (!IsValidReleaseCenterConfig(defaultSettings.ReleaseCenter))
        {
            return;
        }

        if (IsValidReleaseCenterConfig(userSettings.ReleaseCenter))
        {
            return;
        }

        userSettings.ReleaseCenter.Enabled = defaultSettings.ReleaseCenter.Enabled;
        userSettings.ReleaseCenter.BaseUrl = defaultSettings.ReleaseCenter.BaseUrl;
        userSettings.ReleaseCenter.Channel = defaultSettings.ReleaseCenter.Channel;
        userSettings.ReleaseCenter.AnonKey = defaultSettings.ReleaseCenter.AnonKey;

        File.WriteAllText(
            userConfigPath,
            JsonSerializer.Serialize(userSettings, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
    }

    private static bool IsValidReleaseCenterConfig(ReleaseCenterConfig config)
    {
        return config.Enabled
            && !string.IsNullOrWhiteSpace(config.BaseUrl)
            && !string.IsNullOrWhiteSpace(config.Channel)
            && !string.IsNullOrWhiteSpace(config.AnonKey);
    }

    /// <summary>
    /// 获取配置项的快速访问方法
    /// </summary>
    public static T? GetValue<T>(this IConfiguration configuration, string key, T? defaultValue = default)
    {
        return configuration.GetValue(key, defaultValue);
    }

    private static AppSettings LoadDefaultSettings(string defaultConfigPath)
    {
        if (!File.Exists(defaultConfigPath))
        {
            return new AppSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(defaultConfigPath)) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    private static void NormalizeExistingUserConfig(string userConfigPath, string defaultConfigPath)
    {
        try
        {
            var existingJson = File.ReadAllText(userConfigPath);
            var existingUserSettings = JsonSerializer.Deserialize<UserAppSettings>(existingJson) ?? new UserAppSettings();
            var defaultSettings = LoadDefaultSettings(defaultConfigPath);
            BackfillMissingReleaseCenterConnectionFields(existingJson, existingUserSettings, defaultSettings);
            SaveUserSettings(userConfigPath, existingUserSettings);
        }
        catch
        {
            // 保持原文件，避免在异常情况下覆盖潜在可恢复配置。
        }
    }

    private static void BackfillMissingReleaseCenterConnectionFields(
        string existingJson,
        UserAppSettings existingUserSettings,
        AppSettings defaultSettings)
    {
        using var document = JsonDocument.Parse(existingJson);
        if (!document.RootElement.TryGetProperty("ReleaseCenter", out var releaseCenterElement))
        {
            existingUserSettings.ReleaseCenter.Enabled = defaultSettings.ReleaseCenter.Enabled;
            existingUserSettings.ReleaseCenter.BaseUrl = defaultSettings.ReleaseCenter.BaseUrl;
            existingUserSettings.ReleaseCenter.Channel = defaultSettings.ReleaseCenter.Channel;
            return;
        }

        if (!releaseCenterElement.TryGetProperty("Enabled", out _))
        {
            existingUserSettings.ReleaseCenter.Enabled = defaultSettings.ReleaseCenter.Enabled;
        }

        if (!releaseCenterElement.TryGetProperty("BaseUrl", out _))
        {
            existingUserSettings.ReleaseCenter.BaseUrl = defaultSettings.ReleaseCenter.BaseUrl;
        }

        if (!releaseCenterElement.TryGetProperty("Channel", out _))
        {
            existingUserSettings.ReleaseCenter.Channel = defaultSettings.ReleaseCenter.Channel;
        }
    }
}
