using System;
using System.IO;
using DigitalIntelligenceBridge.Configuration;

namespace DigitalIntelligenceBridge.Services;

public sealed record SiteProfileSaveResult(
    string SiteOrganization,
    string SiteName,
    string SiteRemark,
    string Summary,
    string Status);

public static class SiteProfileService
{
    public static bool HasRequiredProfile(ReleaseCenterConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return !string.IsNullOrWhiteSpace(config.SiteOrganization)
            && !string.IsNullOrWhiteSpace(config.SiteName);
    }

    public static string BuildDisplayName(string siteOrganization, string siteName)
    {
        var normalizedOrganization = siteOrganization?.Trim() ?? string.Empty;
        var normalizedSiteName = siteName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedOrganization))
        {
            return "未配置使用单位";
        }

        if (string.IsNullOrWhiteSpace(normalizedSiteName))
        {
            return "未配置站点名称";
        }

        return $"{normalizedOrganization} / {normalizedSiteName}";
    }

    public static string BuildRegistrationLabel(string siteOrganization, string siteName)
    {
        var normalizedOrganization = siteOrganization?.Trim() ?? string.Empty;
        var normalizedSiteName = siteName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedOrganization))
        {
            return normalizedSiteName;
        }

        if (string.IsNullOrWhiteSpace(normalizedSiteName))
        {
            return normalizedOrganization;
        }

        return $"{normalizedOrganization} / {normalizedSiteName}";
    }

    public static string BuildSummary(string siteOrganization, string siteName, string siteRemark)
    {
        var normalizedOrganization = siteOrganization?.Trim() ?? string.Empty;
        var normalizedSiteName = siteName?.Trim() ?? string.Empty;
        var normalizedSiteRemark = siteRemark?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedOrganization) && string.IsNullOrWhiteSpace(normalizedSiteName))
        {
            return "站点信息：未填写";
        }

        var displayName = BuildDisplayName(normalizedOrganization, normalizedSiteName);
        return string.IsNullOrWhiteSpace(normalizedSiteRemark)
            ? $"站点信息：{displayName}"
            : $"站点信息：{displayName} / {normalizedSiteRemark}";
    }

    public static SiteProfileSaveResult Save(AppSettings settings, string siteOrganization, string siteName, string siteRemark)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalizedOrganization = siteOrganization?.Trim() ?? string.Empty;
        var normalizedSiteName = siteName?.Trim() ?? string.Empty;
        var normalizedSiteRemark = siteRemark?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedOrganization))
        {
            throw new InvalidOperationException("请先填写使用单位。");
        }

        if (string.IsNullOrWhiteSpace(normalizedSiteName))
        {
            throw new InvalidOperationException("请先填写站点名称。");
        }

        settings.ReleaseCenter.SiteOrganization = normalizedOrganization;
        settings.ReleaseCenter.SiteName = normalizedSiteName;
        settings.ReleaseCenter.SiteRemark = normalizedSiteRemark;

        PersistAppSettings(settings);

        var displayName = BuildRegistrationLabel(normalizedOrganization, normalizedSiteName);
        var summary = BuildSummary(normalizedOrganization, normalizedSiteName, normalizedSiteRemark);
        return new SiteProfileSaveResult(
            normalizedOrganization,
            normalizedSiteName,
            normalizedSiteRemark,
            summary,
            $"站点信息已保存，后续站点注册与心跳将使用：{displayName}");
    }

    public static void PersistAppSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var configPath = ConfigurationExtensions.GetConfigFilePath();
        var userSettings = ConfigurationExtensions.CreateUserSettingsSnapshot(settings);
        ConfigurationExtensions.SaveUserSettings(configPath, userSettings);
    }
}
