using System;
using System.Text.Json;
using DigitalIntelligenceBridge.Models;
using DigitalIntelligenceBridge.Plugin.Abstractions;
using DigitalIntelligenceBridge.Services;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class AuthorizedResourceCacheServiceTests
{
    [Fact]
    public void SaveSnapshot_ShouldStoreCurrentSnapshot_WhenSnapshotIsProvided()
    {
        var service = new AuthorizedResourceCacheService();
        var snapshot = new AuthorizedResourceCacheSnapshot
        {
            SiteId = "site-001",
            SnapshotVersion = 3,
            SyncedAt = new DateTimeOffset(2026, 4, 14, 9, 0, 0, TimeSpan.Zero),
            ExpiresAt = new DateTimeOffset(2026, 4, 14, 9, 30, 0, TimeSpan.Zero),
            Resources =
            [
                new AuthorizedPluginResourceSet
                {
                    PluginCode = "PatientRegistration",
                    Resources =
                    [
                        new AuthorizedRuntimeResource
                        {
                            ResourceId = "resource-001",
                            ResourceCode = "registration-db",
                            ResourceName = "患者登记数据库",
                            ResourceType = "PostgreSQL",
                            UsageKey = "registration-db",
                            BindingScope = "Site",
                            Version = 1,
                            Capabilities = ["read", "write"],
                            Configuration = JsonDocument.Parse("""{"connectionString":"Host=db.local;Database=patients"}""").RootElement.Clone()
                        }
                    ]
                }
            ]
        };

        service.SaveSnapshot(snapshot);

        var currentSnapshot = service.GetCurrentSnapshot();

        Assert.NotNull(currentSnapshot);
        Assert.Equal("site-001", currentSnapshot!.SiteId);
        Assert.Equal(3, currentSnapshot.SnapshotVersion);
        Assert.Single(currentSnapshot.Resources);
    }

    [Fact]
    public void SaveSnapshot_ShouldIsolateCachedSnapshot_WhenCallerMutatesOriginalSnapshot()
    {
        var service = new AuthorizedResourceCacheService();
        var snapshot = new AuthorizedResourceCacheSnapshot
        {
            SiteId = "site-010",
            SnapshotVersion = 11,
            SyncedAt = new DateTimeOffset(2026, 4, 14, 12, 0, 0, TimeSpan.Zero),
            ExpiresAt = new DateTimeOffset(2026, 4, 14, 12, 30, 0, TimeSpan.Zero),
            Resources =
            [
                new AuthorizedPluginResourceSet
                {
                    PluginCode = "PatientRegistration",
                    Resources =
                    [
                        new AuthorizedRuntimeResource
                        {
                            ResourceId = "resource-401",
                            ResourceCode = "registration-db",
                            ResourceName = "患者登记数据库",
                            ResourceType = "PostgreSQL",
                            UsageKey = "registration-db",
                            Version = 1,
                            Capabilities = ["read"],
                            Configuration = JsonDocument.Parse("""{"connectionString":"Host=db.local;Database=patients"}""").RootElement.Clone()
                        }
                    ]
                }
            ]
        };

        service.SaveSnapshot(snapshot);

        snapshot.SiteId = "site-mutated";
        snapshot.Resources[0].PluginCode = "MutatedPlugin";
        snapshot.Resources[0].Resources[0].ResourceName = "已被修改";

        var currentSnapshot = service.GetCurrentSnapshot();

        Assert.NotNull(currentSnapshot);
        Assert.Equal("site-010", currentSnapshot!.SiteId);
        Assert.Equal("PatientRegistration", currentSnapshot.Resources[0].PluginCode);
        Assert.Equal("患者登记数据库", currentSnapshot.Resources[0].Resources[0].ResourceName);
    }

    [Fact]
    public void GetCurrentSnapshot_ShouldIsolateCachedSnapshot_WhenCallerMutatesReturnedSnapshot()
    {
        var service = new AuthorizedResourceCacheService();
        service.SaveSnapshot(new AuthorizedResourceCacheSnapshot
        {
            SiteId = "site-011",
            SnapshotVersion = 12,
            SyncedAt = new DateTimeOffset(2026, 4, 14, 12, 15, 0, TimeSpan.Zero),
            ExpiresAt = new DateTimeOffset(2026, 4, 14, 12, 45, 0, TimeSpan.Zero),
            Resources =
            [
                new AuthorizedPluginResourceSet
                {
                    PluginCode = "MedicalDrugImport",
                    Resources =
                    [
                        new AuthorizedRuntimeResource
                        {
                            ResourceId = "resource-402",
                            ResourceCode = "drug-import-db",
                            ResourceName = "药品导入数据库",
                            ResourceType = "SqlServer",
                            UsageKey = "business-db",
                            Version = 1,
                            Capabilities = ["read"],
                            Configuration = JsonDocument.Parse("""{"server":"sql01"}""").RootElement.Clone()
                        }
                    ]
                }
            ]
        });

        var firstSnapshot = service.GetCurrentSnapshot();
        Assert.NotNull(firstSnapshot);

        firstSnapshot!.SiteId = "site-mutated";
        firstSnapshot.Resources[0].PluginCode = "MutatedPlugin";
        firstSnapshot.Resources[0].Resources[0].ResourceName = "已被修改";

        var secondSnapshot = service.GetCurrentSnapshot();

        Assert.NotNull(secondSnapshot);
        Assert.Equal("site-011", secondSnapshot!.SiteId);
        Assert.Equal("MedicalDrugImport", secondSnapshot.Resources[0].PluginCode);
        Assert.Equal("药品导入数据库", secondSnapshot.Resources[0].Resources[0].ResourceName);
    }

    [Fact]
    public void GetResourcesForPlugin_ShouldReturnMatchingResources_WhenPluginExists()
    {
        var service = new AuthorizedResourceCacheService();
        service.SaveSnapshot(new AuthorizedResourceCacheSnapshot
        {
            SiteId = "site-002",
            SnapshotVersion = 5,
            SyncedAt = new DateTimeOffset(2026, 4, 14, 10, 0, 0, TimeSpan.Zero),
            ExpiresAt = new DateTimeOffset(2026, 4, 14, 10, 30, 0, TimeSpan.Zero),
            Resources =
            [
                new AuthorizedPluginResourceSet
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
                        }
                    ]
                },
                new AuthorizedPluginResourceSet
                {
                    PluginCode = "PatientRegistration",
                    Resources =
                    [
                        new AuthorizedRuntimeResource
                        {
                            ResourceId = "resource-201",
                            ResourceCode = "registration-db",
                            ResourceName = "患者登记数据库",
                            ResourceType = "PostgreSQL",
                            UsageKey = "registration-db",
                            Version = 2,
                            Capabilities = ["read", "write"],
                            Configuration = JsonDocument.Parse("""{"connectionString":"Host=db.local;Database=patients"}""").RootElement.Clone()
                        }
                    ]
                }
            ]
        });

        var resourceSet = service.GetResourcesForPlugin("MedicalDrugImport");

        Assert.Equal("MedicalDrugImport", resourceSet.PluginCode);
        Assert.Single(resourceSet.Resources);
        Assert.Equal("business-db", resourceSet.Resources[0].UsageKey);
    }

    [Fact]
    public void GetResourcesForPlugin_ShouldIsolateCachedResources_WhenCallerMutatesReturnedResources()
    {
        var service = new AuthorizedResourceCacheService();
        service.SaveSnapshot(new AuthorizedResourceCacheSnapshot
        {
            SiteId = "site-012",
            SnapshotVersion = 13,
            SyncedAt = new DateTimeOffset(2026, 4, 14, 12, 30, 0, TimeSpan.Zero),
            ExpiresAt = new DateTimeOffset(2026, 4, 14, 13, 0, 0, TimeSpan.Zero),
            Resources =
            [
                new AuthorizedPluginResourceSet
                {
                    PluginCode = "PatientRegistration",
                    Resources =
                    [
                        new AuthorizedRuntimeResource
                        {
                            ResourceId = "resource-501",
                            ResourceCode = "registration-db",
                            ResourceName = "患者登记数据库",
                            ResourceType = "PostgreSQL",
                            UsageKey = "registration-db",
                            Version = 1,
                            Capabilities = ["read", "write"],
                            Configuration = JsonDocument.Parse("""{"connectionString":"Host=db.local;Database=patients"}""").RootElement.Clone()
                        }
                    ]
                }
            ]
        });

        var firstResourceSet = service.GetResourcesForPlugin("PatientRegistration");
        firstResourceSet.PluginCode = "MutatedPlugin";
        firstResourceSet.Resources[0].ResourceName = "已被修改";

        var secondResourceSet = service.GetResourcesForPlugin("PatientRegistration");

        Assert.Equal("PatientRegistration", secondResourceSet.PluginCode);
        Assert.Equal("患者登记数据库", secondResourceSet.Resources[0].ResourceName);
    }

    [Fact]
    public void GetResourcesForPlugin_ShouldReturnEmptySet_WhenPluginMissing()
    {
        var service = new AuthorizedResourceCacheService();
        service.SaveSnapshot(new AuthorizedResourceCacheSnapshot
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
                    Resources =
                    [
                        new AuthorizedRuntimeResource
                        {
                            ResourceId = "resource-301",
                            ResourceCode = "registration-db",
                            ResourceName = "患者登记数据库",
                            ResourceType = "PostgreSQL",
                            UsageKey = "registration-db",
                            Version = 1,
                            Capabilities = ["read"],
                            Configuration = JsonDocument.Parse("""{"connectionString":"Host=db.local;Database=patients"}""").RootElement.Clone()
                        }
                    ]
                }
            ]
        });

        var resourceSet = service.GetResourcesForPlugin("MissingPlugin");

        Assert.Equal("MissingPlugin", resourceSet.PluginCode);
        Assert.Empty(resourceSet.Resources);
    }

    [Fact]
    public void SaveSnapshot_ShouldPersistSnapshotToDisk_WhenConfigRootIsAvailable()
    {
        using var sandbox = new TestConfigSandbox();
        var service = new AuthorizedResourceCacheService();
        var snapshot = CreateSnapshot();

        service.SaveSnapshot(snapshot);

        var cacheFilePath = Configuration.ConfigurationExtensions.GetAuthorizedResourcesCacheFilePath();
        Assert.True(File.Exists(cacheFilePath));

        var restoredService = new AuthorizedResourceCacheService();
        var restoredSnapshot = restoredService.GetCurrentSnapshot();

        Assert.NotNull(restoredSnapshot);
        Assert.Equal(snapshot.SiteId, restoredSnapshot!.SiteId);
        Assert.Equal(snapshot.Resources[0].Resources[0].UsageKey, restoredSnapshot.Resources[0].Resources[0].UsageKey);
    }

    [Fact]
    public void Constructor_ShouldIgnoreCorruptedCacheFile_WhenSnapshotFileIsInvalidJson()
    {
        using var sandbox = new TestConfigSandbox();
        var cacheFilePath = Configuration.ConfigurationExtensions.GetAuthorizedResourcesCacheFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(cacheFilePath)!);
        File.WriteAllText(cacheFilePath, "{ invalid json");

        var service = new AuthorizedResourceCacheService();

        Assert.Null(service.GetCurrentSnapshot());
    }

    [Fact]
    public void SaveSnapshot_ShouldPersistOnlyMinimalConfigurationFields_WhenSnapshotContainsExtraMetadata()
    {
        using var sandbox = new TestConfigSandbox();
        var service = new AuthorizedResourceCacheService();
        var snapshot = new AuthorizedResourceCacheSnapshot
        {
            SiteId = "site-minimized",
            SnapshotVersion = 22,
            SyncedAt = new DateTimeOffset(2026, 4, 17, 10, 0, 0, TimeSpan.Zero),
            ExpiresAt = new DateTimeOffset(2026, 4, 17, 10, 30, 0, TimeSpan.Zero),
            Resources =
            [
                new AuthorizedPluginResourceSet
                {
                    PluginCode = "PatientRegistration",
                    Resources =
                    [
                        new AuthorizedRuntimeResource
                        {
                            ResourceId = "resource-minimized",
                            ResourceCode = "registration-db",
                            ResourceName = "患者登记数据库",
                            ResourceType = "PostgreSQL",
                            UsageKey = "registration-db",
                            BindingScope = "PluginAtSite",
                            Version = 4,
                            Capabilities = ["read", "write"],
                            Configuration = JsonDocument.Parse(
                                """
                                {
                                  "host": "db.minimized.local",
                                  "port": 5432,
                                  "database": "registration",
                                  "username": "dib",
                                  "password": "secret",
                                  "searchPath": "public",
                                  "internalNote": "should-not-persist",
                                  "ownerOrganizationId": "org-001"
                                }
                                """).RootElement.Clone()
                        }
                    ]
                }
            ]
        };

        service.SaveSnapshot(snapshot);

        var cacheFilePath = Configuration.ConfigurationExtensions.GetAuthorizedResourcesCacheFilePath();
        using var document = JsonDocument.Parse(File.ReadAllText(cacheFilePath));
        var configuration = document.RootElement
            .GetProperty("resources")[0]
            .GetProperty("resources")[0]
            .GetProperty("configuration");

        Assert.Equal("db.minimized.local", configuration.GetProperty("host").GetString());
        Assert.Equal("registration", configuration.GetProperty("database").GetString());
        Assert.False(configuration.TryGetProperty("internalNote", out _));
        Assert.False(configuration.TryGetProperty("ownerOrganizationId", out _));
    }

    private static AuthorizedResourceCacheSnapshot CreateSnapshot()
    {
        return new AuthorizedResourceCacheSnapshot
        {
            SiteId = "site-persisted",
            SnapshotVersion = 21,
            SyncedAt = new DateTimeOffset(2026, 4, 17, 9, 0, 0, TimeSpan.Zero),
            ExpiresAt = new DateTimeOffset(2026, 4, 17, 9, 30, 0, TimeSpan.Zero),
            Resources =
            [
                new AuthorizedPluginResourceSet
                {
                    PluginCode = "PatientRegistration",
                    Resources =
                    [
                        new AuthorizedRuntimeResource
                        {
                            ResourceId = "resource-persisted",
                            ResourceCode = "registration-db",
                            ResourceName = "患者登记数据库",
                            ResourceType = "PostgreSQL",
                            UsageKey = "registration-db",
                            BindingScope = "PluginAtSite",
                            Version = 3,
                            Capabilities = ["read", "write"],
                            Configuration = JsonDocument.Parse("""{"host":"db.persisted.local","port":5432}""").RootElement.Clone()
                        }
                    ]
                }
            ]
        };
    }
}
