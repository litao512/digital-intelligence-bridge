using System;
using System.Collections.Generic;

namespace DigitalIntelligenceBridge.Configuration;

public static class ConfigurationSafetyValidator
{
    public static string? DetectUnsafeConfiguration(AppSettings settings)
    {
        var reasons = new List<string>();

        if (string.Equals(settings.Application.Name, "TestApp", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("Application.Name=TestApp");
        }

        if (string.Equals(settings.Plugin.PluginDirectory, "plugins-tests", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("Plugin.PluginDirectory=plugins-tests");
        }

        if (!string.IsNullOrWhiteSpace(settings.ReleaseCenter.BaseUrl)
            && settings.ReleaseCenter.BaseUrl.Contains("release-center.local", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("ReleaseCenter.BaseUrl 包含 release-center.local");
        }

        return reasons.Count == 0 ? null : string.Join("；", reasons);
    }

    public static void EnsureSafeUserConfiguration(AppSettings settings, string? configPath = null)
    {
        if (string.Equals(Environment.GetEnvironmentVariable("DIB_ALLOW_UNSAFE_CONFIG"), "1", StringComparison.Ordinal))
        {
            return;
        }

        var reason = DetectUnsafeConfiguration(settings);
        if (string.IsNullOrWhiteSpace(reason))
        {
            return;
        }

        var pathText = string.IsNullOrWhiteSpace(configPath) ? "当前用户配置文件" : configPath;
        throw new InvalidOperationException(
            $"检测到测试配置污染：{reason}。当前不会继续启动。请修复或删除 {pathText}，再重新启动 DIB。");
    }
}
