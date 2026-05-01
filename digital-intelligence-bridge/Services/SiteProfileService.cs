using System;
using System.IO;
using DigitalIntelligenceBridge.Configuration;

namespace DigitalIntelligenceBridge.Services;

public sealed record SiteProfileSaveResult(
    string SiteName,
    string SiteRemark,
    string Summary,
    string Status);

public static class SiteProfileService
{
    public static bool HasRequiredProfile(ReleaseCenterConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return !string.IsNullOrWhiteSpace(config.SiteName);
    }

    public static string BuildDisplayName(string siteName)
    {
        var normalizedSiteName = siteName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedSiteName))
        {
            return "未配置站点名称";
        }

        return normalizedSiteName;
    }

    public static string BuildSummary(string siteName, string siteRemark)
    {
        var normalizedSiteName = siteName?.Trim() ?? string.Empty;
        var normalizedSiteRemark = siteRemark?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedSiteName))
        {
            return "站点信息：未填写";
        }

        var displayName = BuildDisplayName(normalizedSiteName);
        return string.IsNullOrWhiteSpace(normalizedSiteRemark)
            ? $"站点信息：{displayName}"
            : $"站点信息：{displayName} / {normalizedSiteRemark}";
    }

    public static SiteProfileSaveResult Save(AppSettings settings, string siteName, string siteRemark)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalizedSiteName = siteName?.Trim() ?? string.Empty;
        var normalizedSiteRemark = siteRemark?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedSiteName))
        {
            throw new InvalidOperationException("请先填写站点名称。");
        }

        settings.ReleaseCenter.SiteName = normalizedSiteName;
        settings.ReleaseCenter.SiteRemark = normalizedSiteRemark;

        PersistAppSettings(settings);

        var summary = BuildSummary(normalizedSiteName, normalizedSiteRemark);
        return new SiteProfileSaveResult(
            normalizedSiteName,
            normalizedSiteRemark,
            summary,
            $"站点信息已保存，后续站点注册与心跳将使用：{normalizedSiteName}");
    }

    public static void PersistAppSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var configPath = ConfigurationExtensions.GetConfigFilePath();
        var userSettings = ConfigurationExtensions.CreateUserSettingsSnapshot(settings);
        ConfigurationExtensions.SaveUserSettings(configPath, userSettings);
    }
}
