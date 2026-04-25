using System;
using System.IO;
using System.Text.Json;
using DigitalIntelligenceBridge.Services;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class SiteIdentityServiceTests
{
    [Fact]
    public void ExportAndImport_ShouldSucceed_WhenSnapshotIsValid()
    {
        using var sandbox = new TestConfigSandbox();
        var filePath = Path.Combine(sandbox.RootDirectory, "site-identity.json");
        var siteId = Guid.NewGuid().ToString();

        var exported = SiteIdentityService.Export(siteId, "第一人民医院", "门诊一楼", "收费处旁", filePath);
        var imported = SiteIdentityService.Import(filePath);

        Assert.True(imported.IsSuccess);
        Assert.NotNull(imported.Snapshot);
        Assert.Equal(exported.SiteId, imported.Snapshot!.SiteId);
        Assert.Equal("第一人民医院", imported.Snapshot.SiteOrganization);
        Assert.Equal("门诊一楼", imported.Snapshot.SiteName);
    }

    [Fact]
    public void Import_ShouldFail_WhenChecksumIsTampered()
    {
        using var sandbox = new TestConfigSandbox();
        var filePath = Path.Combine(sandbox.RootDirectory, "site-identity.json");
        var siteId = Guid.NewGuid().ToString();
        SiteIdentityService.Export(siteId, "第一人民医院", "门诊一楼", "收费处旁", filePath);

        var snapshot = JsonSerializer.Deserialize<SiteIdentitySnapshot>(File.ReadAllText(filePath));
        Assert.NotNull(snapshot);
        var tamperedSnapshot = snapshot! with { SiteName = "门诊二楼" };
        File.WriteAllText(filePath, JsonSerializer.Serialize(tamperedSnapshot, new JsonSerializerOptions { WriteIndented = true }));

        var imported = SiteIdentityService.Import(filePath);
        Assert.False(imported.IsSuccess);
        Assert.Contains("校验失败", imported.Detail);
    }

    [Fact]
    public void Export_ShouldThrow_WhenSiteIdIsInvalid()
    {
        using var sandbox = new TestConfigSandbox();
        var filePath = Path.Combine(sandbox.RootDirectory, "site-identity.json");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            SiteIdentityService.Export("not-a-guid", "第一人民医院", "门诊一楼", string.Empty, filePath));

        Assert.Contains("GUID", ex.Message);
    }
}
