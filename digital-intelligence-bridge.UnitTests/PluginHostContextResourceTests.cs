using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DigitalIntelligenceBridge.Models;
using DigitalIntelligenceBridge.Services;
using DigitalIntelligenceBridge.Plugin.Abstractions;
using DigitalIntelligenceBridge.Plugin.Host;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public sealed class PluginHostContextResourceTests
{
    [Fact]
    public void GetAuthorizedResources_ShouldReturnCurrentPluginResources_WhenCacheServiceProvidesResources()
    {
        var cacheService = new AuthorizedResourceCacheService();
        cacheService.SaveSnapshot(new AuthorizedResourceCacheSnapshot
        {
            SiteId = "site-001",
            SnapshotVersion = 1,
            SyncedAt = new DateTimeOffset(2026, 4, 14, 9, 0, 0, TimeSpan.Zero),
            ExpiresAt = new DateTimeOffset(2026, 4, 14, 9, 30, 0, TimeSpan.Zero),
            Resources =
            [
                new AuthorizedPluginResourceSet
                {
                    PluginCode = "PatientRegistration",
                    Resources = CreateResources()
                }
            ]
        });
        var context = new PluginHostContext("1.0.0", @"C:\plugins\patient-registration", "PatientRegistration", cacheService, _ => { });

        var authorizedResources = context.GetAuthorizedResources();

        Assert.Equal(2, authorizedResources.Count);
        Assert.Equal("registration-db", authorizedResources[0].UsageKey);
        Assert.Equal("ocr-service", authorizedResources[1].UsageKey);

        cacheService.SaveSnapshot(new AuthorizedResourceCacheSnapshot
        {
            SiteId = "site-001",
            SnapshotVersion = 2,
            SyncedAt = new DateTimeOffset(2026, 4, 14, 9, 15, 0, TimeSpan.Zero),
            ExpiresAt = new DateTimeOffset(2026, 4, 14, 9, 45, 0, TimeSpan.Zero),
            Resources =
            [
                new AuthorizedPluginResourceSet
                {
                    PluginCode = "PatientRegistration",
                    Resources =
                    [
                        CreateResource("resource-003", "ehr-api", "电子病历服务", "HttpService", "ehr-api", "Plugin", 4, ["invoke"], """{"endpoint":"https://ehr.example.local"}""")
                    ]
                }
            ]
        });

        var refreshedResources = context.GetAuthorizedResources();

        Assert.Single(refreshedResources);
        Assert.Equal("ehr-api", refreshedResources[0].UsageKey);
    }

    [Fact]
    public void TryGetResource_ShouldReturnMatchingResource_WhenUsageKeyExistsInCacheService()
    {
        var cacheService = new AuthorizedResourceCacheService();
        cacheService.SaveSnapshot(new AuthorizedResourceCacheSnapshot
        {
            SiteId = "site-002",
            SnapshotVersion = 1,
            SyncedAt = new DateTimeOffset(2026, 4, 14, 10, 0, 0, TimeSpan.Zero),
            ExpiresAt = new DateTimeOffset(2026, 4, 14, 10, 30, 0, TimeSpan.Zero),
            Resources =
            [
                new AuthorizedPluginResourceSet
                {
                    PluginCode = "PatientRegistration",
                    Resources = CreateResources()
                }
            ]
        });
        var context = new PluginHostContext("1.0.0", @"C:\plugins\patient-registration", "PatientRegistration", cacheService, _ => { });

        var found = context.TryGetResource("ocr-service", out var resource);

        Assert.True(found);
        Assert.NotNull(resource);
        Assert.Equal("ocr-service", resource!.UsageKey);
        Assert.Equal("OCR 服务", resource.ResourceName);
    }

    [Fact]
    public void TryGetResource_ShouldReturnFalseAndNull_WhenUsageKeyMissingInCacheService()
    {
        var cacheService = new AuthorizedResourceCacheService();
        cacheService.SaveSnapshot(new AuthorizedResourceCacheSnapshot
        {
            SiteId = "site-003",
            SnapshotVersion = 1,
            SyncedAt = new DateTimeOffset(2026, 4, 14, 11, 0, 0, TimeSpan.Zero),
            ExpiresAt = new DateTimeOffset(2026, 4, 14, 11, 15, 0, TimeSpan.Zero),
            Resources =
            [
                new AuthorizedPluginResourceSet
                {
                    PluginCode = "PatientRegistration",
                    Resources = CreateResources()
                }
            ]
        });
        var context = new PluginHostContext("1.0.0", @"C:\plugins\patient-registration", "PatientRegistration", cacheService, _ => { });

        var found = context.TryGetResource("missing-resource", out var resource);

        Assert.False(found);
        Assert.Null(resource);
    }

    private static IReadOnlyList<AuthorizedRuntimeResource> CreateResources()
    {
        return
        [
            CreateResource("resource-001", "patient-registration-db", "患者登记数据库", "PostgreSQL", "registration-db", "Site", 1, ["read", "write"], """{"host":"db.local"}"""),
            CreateResource("resource-002", "ocr-service", "OCR 服务", "HttpService", "ocr-service", "Plugin", 2, ["invoke"], """{"endpoint":"https://ocr.example.local"}""")
        ];
    }

    private static AuthorizedRuntimeResource CreateResource(
        string resourceId,
        string resourceCode,
        string resourceName,
        string resourceType,
        string usageKey,
        string bindingScope,
        int version,
        IReadOnlyList<string> capabilities,
        string configurationJson)
    {
        return new AuthorizedRuntimeResource
        {
            ResourceId = resourceId,
            ResourceCode = resourceCode,
            ResourceName = resourceName,
            ResourceType = resourceType,
            UsageKey = usageKey,
            BindingScope = bindingScope,
            Version = version,
            Capabilities = capabilities,
            Configuration = JsonDocument.Parse(configurationJson).RootElement.Clone()
        };
    }

}
