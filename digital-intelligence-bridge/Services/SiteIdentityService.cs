using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DigitalIntelligenceBridge.Configuration;

namespace DigitalIntelligenceBridge.Services;

public sealed record SiteIdentitySnapshot(
    int SchemaVersion,
    string SiteId,
    string SiteName,
    string SiteRemark,
    DateTimeOffset ExportedAtUtc,
    string Checksum);

public sealed record SiteIdentityImportResult(
    bool IsSuccess,
    string Summary,
    string Detail,
    SiteIdentitySnapshot? Snapshot);

public static class SiteIdentityService
{
    public const int CurrentSchemaVersion = 3;
    public const string DefaultFileName = "site-identity.json";

    public static string GetDefaultFilePath()
    {
        return Path.Combine(ConfigurationExtensions.GetConfigRootDirectory(), DefaultFileName);
    }

    public static SiteIdentitySnapshot Export(string siteId, string siteName, string siteRemark, string filePath)
    {
        var normalizedSiteId = siteId.Trim();
        var normalizedSiteName = siteName.Trim();
        var normalizedSiteRemark = siteRemark.Trim();

        if (string.IsNullOrWhiteSpace(normalizedSiteId))
        {
            throw new InvalidOperationException("站点 ID 为空，无法导出。");
        }

        if (!Guid.TryParse(normalizedSiteId, out _))
        {
            throw new InvalidOperationException("站点 ID 不是合法 GUID，无法导出。");
        }

        if (string.IsNullOrWhiteSpace(normalizedSiteName))
        {
            throw new InvalidOperationException("站点名称为空，无法导出。");
        }

        var exportedAtUtc = DateTimeOffset.UtcNow;
        var checksum = ComputeChecksum(normalizedSiteId, normalizedSiteName, normalizedSiteRemark, exportedAtUtc);
        var snapshot = new SiteIdentitySnapshot(
            CurrentSchemaVersion,
            normalizedSiteId,
            normalizedSiteName,
            normalizedSiteRemark,
            exportedAtUtc,
            checksum);

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(filePath, json);
        return snapshot;
    }

    public static SiteIdentityImportResult Import(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new SiteIdentityImportResult(false, "导入失败", $"文件不存在：{filePath}", null);
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<SiteIdentitySnapshot>(File.ReadAllText(filePath));
            if (snapshot is null)
            {
                return new SiteIdentityImportResult(false, "导入失败", "文件内容为空或无法解析。", null);
            }

            if (snapshot.SchemaVersion != CurrentSchemaVersion)
            {
                return new SiteIdentityImportResult(false, "导入失败", $"不支持的版本：{snapshot.SchemaVersion}", null);
            }

            if (string.IsNullOrWhiteSpace(snapshot.SiteId) || !Guid.TryParse(snapshot.SiteId, out _))
            {
                return new SiteIdentityImportResult(false, "导入失败", "SiteId 非法。", null);
            }

            if (string.IsNullOrWhiteSpace(snapshot.SiteName))
            {
                return new SiteIdentityImportResult(false, "导入失败", "SiteName 为空。", null);
            }

            var expected = ComputeChecksum(snapshot.SiteId.Trim(), snapshot.SiteName.Trim(), snapshot.SiteRemark?.Trim() ?? string.Empty, snapshot.ExportedAtUtc);
            if (!string.Equals(expected, snapshot.Checksum, StringComparison.OrdinalIgnoreCase))
            {
                return new SiteIdentityImportResult(false, "导入失败", "身份文件校验失败，文件可能已被篡改或损坏。", null);
            }

            return new SiteIdentityImportResult(true, "导入成功", $"已加载站点身份：{snapshot.SiteName} / {snapshot.SiteId}", snapshot);
        }
        catch (Exception ex)
        {
            return new SiteIdentityImportResult(false, "导入失败", ex.Message, null);
        }
    }

    private static string ComputeChecksum(string siteId, string siteName, string siteRemark, DateTimeOffset exportedAtUtc)
    {
        var canonical = $"{siteId}\n{siteName}\n{siteRemark}\n{exportedAtUtc:O}";
        return ComputeSha256(canonical);
    }

    private static string ComputeSha256(string canonical)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
