using System;
using System.Collections.Generic;
using System.Text.Json;
using DigitalIntelligenceBridge.Plugin.Abstractions;
using DigitalIntelligenceBridge.Models;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public sealed class AuthorizedResourceCacheModelsTests
{
    [Fact]
    public void AuthorizedResourceCacheSnapshot_ShouldExposeSiteIdentityAndGroupedResources_WhenPopulated()
    {
        var configuration = JsonDocument.Parse("""{"connectionString":"Host=db.local;Database=patients"}""").RootElement.Clone();
        var runtimeResource = new AuthorizedRuntimeResource
        {
            ResourceId = "resource-001",
            ResourceCode = "patient-registration-db",
            ResourceName = "患者登记数据库",
            ResourceType = "PostgreSQL",
            UsageKey = "registration-db",
            BindingScope = "Site",
            Version = 3,
            Capabilities = ["read", "write"],
            Configuration = configuration
        };

        var pluginResourceSet = new AuthorizedPluginResourceSet
        {
            PluginCode = "PatientRegistration",
            Resources = [runtimeResource]
        };

        var snapshot = new AuthorizedResourceCacheSnapshot
        {
            SiteId = "site-001",
            SnapshotVersion = 7,
            SyncedAt = new DateTimeOffset(2026, 4, 14, 8, 30, 0, TimeSpan.Zero),
            ExpiresAt = new DateTimeOffset(2026, 4, 14, 8, 45, 0, TimeSpan.Zero),
            Resources = [pluginResourceSet]
        };

        Assert.Equal("site-001", snapshot.SiteId);
        Assert.Equal(7, snapshot.SnapshotVersion);
        Assert.Equal(new DateTimeOffset(2026, 4, 14, 8, 30, 0, TimeSpan.Zero), snapshot.SyncedAt);
        Assert.Equal(new DateTimeOffset(2026, 4, 14, 8, 45, 0, TimeSpan.Zero), snapshot.ExpiresAt);
        Assert.Single(snapshot.Resources);
        Assert.Equal("PatientRegistration", snapshot.Resources[0].PluginCode);
        Assert.Single(snapshot.Resources[0].Resources);
        Assert.Equal("registration-db", snapshot.Resources[0].Resources[0].UsageKey);
        Assert.Equal("Host=db.local;Database=patients", snapshot.Resources[0].Resources[0].Configuration.GetProperty("connectionString").GetString());

        var json = JsonSerializer.Serialize(snapshot);
        using var document = JsonDocument.Parse(json);

        Assert.True(document.RootElement.TryGetProperty("siteId", out var siteId));
        Assert.Equal("site-001", siteId.GetString());
        Assert.True(document.RootElement.TryGetProperty("snapshotVersion", out var snapshotVersion));
        Assert.Equal(7, snapshotVersion.GetInt32());
        Assert.True(document.RootElement.TryGetProperty("resources", out var resources));
        Assert.Equal(JsonValueKind.Array, resources.ValueKind);
        Assert.True(resources[0].TryGetProperty("pluginCode", out var pluginCode));
        Assert.Equal("PatientRegistration", pluginCode.GetString());
        Assert.True(resources[0].TryGetProperty("resources", out var pluginResources));
        Assert.True(pluginResources[0].TryGetProperty("usageKey", out var usageKey));
        Assert.Equal("registration-db", usageKey.GetString());
        Assert.True(pluginResources[0].TryGetProperty("configuration", out var serializedConfiguration));
        Assert.Equal("Host=db.local;Database=patients", serializedConfiguration.GetProperty("connectionString").GetString());
    }

    [Fact]
    public void AuthorizedPluginResourceSet_ShouldGroupResourcesByPluginCode_WhenPopulated()
    {
        var pluginResourceSet = new AuthorizedPluginResourceSet
        {
            PluginCode = "MedicalDrugImport",
            Resources =
            [
                new AuthorizedRuntimeResource
                {
                    ResourceId = "resource-101",
                    ResourceCode = "drug-import-db",
                    ResourceName = "药品导入数据库",
                    ResourceType = "SqlServer",
                    UsageKey = "business-db",
                    Version = 1,
                    Capabilities = ["read"],
                    Configuration = JsonDocument.Parse("""{"server":"sql01"}""").RootElement.Clone()
                },
                new AuthorizedRuntimeResource
                {
                    ResourceId = "resource-102",
                    ResourceCode = "drug-sync-target",
                    ResourceName = "药品同步目标",
                    ResourceType = "HttpService",
                    UsageKey = "sync-target",
                    Version = 1,
                    Capabilities = ["invoke"],
                    Configuration = JsonDocument.Parse("""{"baseUrl":"https://sync.example.local"}""").RootElement.Clone()
                }
            ]
        };

        Assert.Equal("MedicalDrugImport", pluginResourceSet.PluginCode);
        Assert.Equal(2, pluginResourceSet.Resources.Count);
        Assert.Equal("business-db", pluginResourceSet.Resources[0].UsageKey);
        Assert.Equal("sync-target", pluginResourceSet.Resources[1].UsageKey);
    }

    [Fact]
    public void AuthorizedRuntimeResource_ShouldPreserveUsageKeyAndConfiguration_WhenPopulated()
    {
        var configuration = JsonDocument.Parse("""{"endpoint":"https://ocr.example.local","timeoutSeconds":15}""").RootElement.Clone();
        var resource = new AuthorizedRuntimeResource
        {
            ResourceId = "resource-201",
            ResourceCode = "ocr-service",
            ResourceName = "OCR 服务",
            ResourceType = "HttpService",
            UsageKey = "ocr-service",
            BindingScope = "Plugin",
            Version = 5,
            Capabilities = ["invoke", "retry"],
            Configuration = configuration
        };

        Assert.Equal("ocr-service", resource.UsageKey);
        Assert.Equal("https://ocr.example.local", resource.Configuration.GetProperty("endpoint").GetString());
        Assert.Equal(15, resource.Configuration.GetProperty("timeoutSeconds").GetInt32());

        var json = JsonSerializer.Serialize(resource);
        using var document = JsonDocument.Parse(json);

        Assert.True(document.RootElement.TryGetProperty("usageKey", out var usageKey));
        Assert.Equal("ocr-service", usageKey.GetString());
        Assert.True(document.RootElement.TryGetProperty("configuration", out var serializedConfiguration));
        Assert.Equal("https://ocr.example.local", serializedConfiguration.GetProperty("endpoint").GetString());
    }

    [Fact]
    public void AuthorizedResourceCacheSnapshot_ShouldRoundTripJsonContract_WhenSerializedAndDeserialized()
    {
        var snapshot = new AuthorizedResourceCacheSnapshot
        {
            SiteId = "site-002",
            SnapshotVersion = 9,
            SyncedAt = new DateTimeOffset(2026, 4, 14, 9, 0, 0, TimeSpan.Zero),
            ExpiresAt = new DateTimeOffset(2026, 4, 14, 9, 30, 0, TimeSpan.Zero),
            Resources =
            [
                new AuthorizedPluginResourceSet
                {
                    PluginCode = "MedicalDrugImport",
                    Resources =
                    [
                        new AuthorizedRuntimeResource
                        {
                            ResourceId = "resource-301",
                            ResourceCode = "drug-sync-target",
                            ResourceName = "药品同步目标",
                            ResourceType = "HttpService",
                            UsageKey = "sync-target",
                            BindingScope = "Plugin",
                            Version = 2,
                            Capabilities = ["invoke"],
                            Configuration = JsonDocument.Parse("""{"baseUrl":"https://sync.example.local"}""").RootElement.Clone()
                        }
                    ]
                }
            ]
        };

        var json = JsonSerializer.Serialize(snapshot);
        using var document = JsonDocument.Parse(json);

        Assert.Equal("site-002", document.RootElement.GetProperty("siteId").GetString());
        Assert.Equal(9, document.RootElement.GetProperty("snapshotVersion").GetInt32());
        Assert.Equal("MedicalDrugImport", document.RootElement.GetProperty("resources")[0].GetProperty("pluginCode").GetString());
        Assert.Equal("sync-target", document.RootElement.GetProperty("resources")[0].GetProperty("resources")[0].GetProperty("usageKey").GetString());
        Assert.True(document.RootElement.GetProperty("resources")[0].GetProperty("resources")[0].TryGetProperty("configuration", out var configuration));
        Assert.Equal("https://sync.example.local", configuration.GetProperty("baseUrl").GetString());

        var deserialized = JsonSerializer.Deserialize<AuthorizedResourceCacheSnapshot>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("site-002", deserialized!.SiteId);
        Assert.Equal(9, deserialized.SnapshotVersion);
        Assert.Equal("MedicalDrugImport", deserialized.Resources[0].PluginCode);
        Assert.Equal("sync-target", deserialized.Resources[0].Resources[0].UsageKey);
        Assert.Equal("https://sync.example.local", deserialized.Resources[0].Resources[0].Configuration.GetProperty("baseUrl").GetString());
    }
}
