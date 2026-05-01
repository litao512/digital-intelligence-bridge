using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Models;
using DigitalIntelligenceBridge.Services;
using DigitalIntelligenceBridge.Plugin.Abstractions;
using DigitalIntelligenceBridge.Plugin.Host;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class ReleaseCenterServiceTests
{
    [Fact]
    public async Task CheckForUpdatesAsync_ShouldReturnAvailableUpdates_WhenManifestHasNewerClientAndPlugins()
    {
        var service = CreateService("""
{
  "latestVersion": "1.2.0",
  "minUpgradeVersion": "1.0.0"
}
""", """
{
  "plugins": [
    { "name": "就诊登记", "version": "1.0.1" },
    { "name": "床旁巡视", "version": "1.0.0" }
  ]
}
""");

        var result = await service.CheckForUpdatesAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("发现 2 类可用更新", result.Summary);
        Assert.Contains("1.2.0", result.ClientSummary);
        Assert.Contains("2 个可用插件", result.PluginSummary);
        Assert.Contains("就诊登记 1.0.1", result.PluginSummary);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ShouldReturnConfiguredFailure_WhenReleaseCenterDisabled()
    {
        var handler = new StubHttpMessageHandler(_ => throw new Xunit.Sdk.XunitException("不应发起 HTTP 请求"));
        var service = new ReleaseCenterService(
            new HttpClient(handler),
            NullLogger<ReleaseCenterService>.Instance,
            Options.Create(new AppSettings
            {
                Application = new ApplicationConfig { Version = "1.0.0" },
                ReleaseCenter = new ReleaseCenterConfig { Enabled = false, BaseUrl = "http://example.com", Channel = "stable" }
            }));

        var result = await service.CheckForUpdatesAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("未配置发布中心", result.Summary);
        Assert.Equal("客户端更新：未配置", result.ClientSummary);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ShouldGenerateAndPersistSiteId_WhenMissing()
    {
        using var sandbox = new TestConfigSandbox();

        var settings = new AppSettings
        {
            Application = new ApplicationConfig { Version = "1.0.0" },
            ReleaseCenter = new ReleaseCenterConfig
            {
                Enabled = true,
                BaseUrl = "http://release-center.local",
                Channel = "stable",
                SiteId = string.Empty,
                SiteName = "门诊登记台 1"
            }
        };

        var service = CreateService("""
{
  "latestVersion": "1.2.0",
  "minUpgradeVersion": "1.0.0"
}
""", """
{
  "plugins": []
}
""", settings);

        var result = await service.CheckForUpdatesAsync();

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(settings.ReleaseCenter.SiteId));
        Assert.Contains($"siteId={settings.ReleaseCenter.SiteId}", result.Detail);

        var persistedJson = await File.ReadAllTextAsync(ConfigurationExtensions.GetConfigFilePath());
        using var document = JsonDocument.Parse(persistedJson);
        var persistedSiteId = document.RootElement
            .GetProperty("ReleaseCenter")
            .GetProperty("SiteId")
            .GetString();
        Assert.Equal(settings.ReleaseCenter.SiteId, persistedSiteId);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ShouldKeepExistingSiteId_OnRepeatedCalls()
    {
        var settings = new AppSettings
        {
            Application = new ApplicationConfig { Version = "1.0.0" },
            ReleaseCenter = new ReleaseCenterConfig
            {
                Enabled = true,
                BaseUrl = "http://release-center.local",
                Channel = "stable",
                SiteId = string.Empty,
                SiteName = "门诊登记台 1"
            }
        };

        var service = CreateService("""
{
  "latestVersion": "1.2.0",
  "minUpgradeVersion": "1.0.0"
}
""", """
{
  "plugins": []
}
""", settings);

        await service.CheckForUpdatesAsync();
        var siteId = settings.ReleaseCenter.SiteId;
        var second = await service.CheckForUpdatesAsync();

        Assert.Equal(siteId, settings.ReleaseCenter.SiteId);
        Assert.Contains($"siteId={siteId}", second.Detail);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ShouldAppendSiteId_WhenRequestingPluginManifest()
    {
        HttpRequestMessage? pluginManifestRequest = null;
        HttpRequestMessage? heartbeatRequest = null;
        string heartbeatBody = string.Empty;
        string pluginManifestBody = string.Empty;
        var settings = new AppSettings
        {
            Application = new ApplicationConfig { Version = "1.0.0" },
            ReleaseCenter = new ReleaseCenterConfig
            {
                Enabled = true,
                BaseUrl = "http://release-center.local",
                Channel = "stable",
                AnonKey = "anon-key",
                SiteId = "11111111-1111-1111-1111-111111111111",
                SiteName = "门诊登记台 1"
            }
        };

        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri.Contains("/rpc/register_site_heartbeat", StringComparison.OrdinalIgnoreCase))
            {
                heartbeatRequest = request;
                heartbeatBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"siteId\":\"11111111-1111-1111-1111-111111111111\"}", Encoding.UTF8, "application/json")
                };
            }

            if (request.RequestUri!.AbsoluteUri.Contains("/rpc/get_site_plugin_manifest", StringComparison.OrdinalIgnoreCase))
            {
                pluginManifestRequest = request;
                pluginManifestBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;
            }

            var content = request.RequestUri!.AbsoluteUri.Contains("client-manifest", StringComparison.OrdinalIgnoreCase)
                ? """
{
  "latestVersion": "1.2.0",
  "minUpgradeVersion": "1.0.0"
}
"""
                : """
{
  "plugins": []
}
""";

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            };
        });

        var service = new ReleaseCenterService(
            new HttpClient(handler),
            NullLogger<ReleaseCenterService>.Instance,
            Options.Create(settings));

        await service.CheckForUpdatesAsync();

        Assert.NotNull(heartbeatRequest);
        Assert.Equal(HttpMethod.Post, heartbeatRequest!.Method);
        Assert.Equal("anon-key", heartbeatRequest.Headers.GetValues("apikey").Single());
        Assert.Equal("dib_release", heartbeatRequest.Headers.GetValues("Accept-Profile").Single());
        Assert.Equal("dib_release", heartbeatRequest.Headers.GetValues("Content-Profile").Single());

        using var heartbeatDocument = JsonDocument.Parse(heartbeatBody);
        Assert.Equal("11111111-1111-1111-1111-111111111111", heartbeatDocument.RootElement.GetProperty("p_site_id").GetString());
        Assert.Equal("门诊登记台 1", heartbeatDocument.RootElement.GetProperty("p_site_name").GetString());
        Assert.Equal("stable", heartbeatDocument.RootElement.GetProperty("p_channel_code").GetString());

        Assert.NotNull(pluginManifestRequest);
        Assert.Equal(HttpMethod.Post, pluginManifestRequest!.Method);
        Assert.Equal("anon-key", pluginManifestRequest.Headers.GetValues("apikey").Single());
        Assert.Equal("Bearer anon-key", pluginManifestRequest.Headers.Authorization?.ToString());
        Assert.Equal("dib_release", pluginManifestRequest.Headers.GetValues("Accept-Profile").Single());
        Assert.Equal("dib_release", pluginManifestRequest.Headers.GetValues("Content-Profile").Single());

        using var pluginManifestDocument = JsonDocument.Parse(pluginManifestBody);
        Assert.Equal("11111111-1111-1111-1111-111111111111", pluginManifestDocument.RootElement.GetProperty("p_site_id").GetString());
        Assert.Equal("stable", pluginManifestDocument.RootElement.GetProperty("p_channel_code").GetString());
    }

    [Theory]
    [InlineData("1.0.1", "1.0.0", 1)]
    [InlineData("1.0.0", "1.0.1", -1)]
    [InlineData("1.0.0-beta.1", "1.0.0", 0)]
    [InlineData("1.0.3-dev.3", "1.0.3-dev.2", 1)]
    [InlineData("1.0.3-dev.2", "1.0.3-dev.3", -1)]
    [InlineData("1.0", "1.0.0", 0)]
    public void CompareVersions_ShouldReturnExpectedResult(string left, string right, int expectedSign)
    {
        var result = ReleaseCenterService.CompareVersions(left, right);

        Assert.Equal(expectedSign, Math.Sign(result));
    }

    [Fact]
    public async Task GetAuthorizedResourcesAsync_ShouldPostInstalledPluginRequirements_AndReturnSnapshot()
    {
        var runtimeRoot = Path.Combine(Path.GetTempPath(), "dib-runtime-plugins", Guid.NewGuid().ToString("N"));
        var pluginDirectory = Path.Combine(runtimeRoot, "MedicalDrugImport");
        Directory.CreateDirectory(pluginDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(pluginDirectory, "plugin.json"),
            """
            {
              "id": "medical-drug-import",
              "name": "医保药品导入",
              "version": "0.1.0",
              "entryAssembly": "MedicalDrugImport.Plugin.dll",
              "entryType": "MedicalDrugImport.Plugin.MedicalDrugImportPlugin",
              "minHostVersion": "1.0.0",
              "resourceRequirements": [
                {
                  "resourceType": "PostgreSQL",
                  "usageKey": "business-db",
                  "required": true,
                  "description": "业务库"
                }
              ]
            }
            """);

        HttpRequestMessage? requestMessage = null;
        string requestBody = string.Empty;
        var settings = new AppSettings
        {
            Application = new ApplicationConfig { Version = "1.0.0" },
            ReleaseCenter = new ReleaseCenterConfig
            {
                Enabled = true,
                BaseUrl = "http://release-center.local",
                Channel = "stable",
                AnonKey = "anon-key",
                SiteId = "11111111-1111-1111-1111-111111111111",
                SiteName = "门诊登记台 1",
                RuntimePluginRoot = runtimeRoot
            }
        };

        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri.Contains("/rpc/get_site_authorized_resources", StringComparison.OrdinalIgnoreCase))
            {
                requestMessage = request;
                requestBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "resources": [
                            {
                              "resourceId": "6f9e12d5-0a7a-4f93-bf24-e202c7d8d07f",
                              "resourceCode": "postgres-main",
                              "resourceName": "业务库",
                              "resourceType": "PostgreSQL",
                              "pluginCode": "medical-drug-import",
                              "bindingScope": "PluginAtSite",
                              "configVersion": 2,
                              "configPayload": {
                                "host": "127.0.0.1",
                                "port": 5432
                              },
                              "secretRef": "vault://resource-center/postgres-main/password",
                              "capabilities": ["read", "write"]
                            }
                          ]
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                };
            }

            throw new Xunit.Sdk.XunitException($"未预期的请求：{request.RequestUri}");
        });

        try
        {
            var service = new ReleaseCenterService(
                new HttpClient(handler),
                NullLogger<ReleaseCenterService>.Instance,
                Options.Create(settings));

            var snapshot = await service.GetAuthorizedResourcesAsync();

            Assert.NotNull(requestMessage);
            Assert.Equal(HttpMethod.Post, requestMessage!.Method);
            Assert.Equal("anon-key", requestMessage.Headers.GetValues("apikey").Single());

            using var document = JsonDocument.Parse(requestBody);
            Assert.Equal("11111111-1111-1111-1111-111111111111", document.RootElement.GetProperty("p_site_id").GetString());
            var plugins = document.RootElement.GetProperty("p_plugins_json");
            var plugin = Assert.Single(plugins.EnumerateArray());
            Assert.Equal("medical-drug-import", plugin.GetProperty("pluginCode").GetString());
            var requirements = Assert.Single(plugin.GetProperty("requirements").EnumerateArray());
            Assert.Equal("PostgreSQL", requirements.GetProperty("resourceType").GetString());
            Assert.Equal("business-db", requirements.GetProperty("usageKey").GetString());

            var resource = Assert.Single(snapshot.Resources);
            Assert.Equal("postgres-main", resource.ResourceCode);
            Assert.Equal("medical-drug-import", resource.PluginCode);
            Assert.Equal("vault://resource-center/postgres-main/password", resource.SecretRef);
        }
        finally
        {
            if (Directory.Exists(runtimeRoot))
            {
                Directory.Delete(runtimeRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GetAuthorizedResourcesAsync_ShouldWriteAuthorizedResourcesToCache_WhenCacheServiceProvided()
    {
        var runtimeRoot = Path.Combine(Path.GetTempPath(), "dib-runtime-plugins", Guid.NewGuid().ToString("N"));
        var pluginDirectory = Path.Combine(runtimeRoot, "MedicalDrugImport");
        Directory.CreateDirectory(pluginDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(pluginDirectory, "plugin.json"),
            """
            {
              "id": "medical-drug-import",
              "name": "医保药品导入",
              "version": "0.1.0",
              "entryAssembly": "MedicalDrugImport.Plugin.dll",
              "entryType": "MedicalDrugImport.Plugin.MedicalDrugImportPlugin",
              "minHostVersion": "1.0.0",
              "resourceRequirements": [
                {
                  "resourceType": "PostgreSQL",
                  "usageKey": "business-db",
                  "required": true,
                  "description": "业务库"
                }
              ]
            }
            """);

        var cacheService = new AuthorizedResourceCacheService();
        var settings = new AppSettings
        {
            Application = new ApplicationConfig { Version = "1.0.0" },
            ReleaseCenter = new ReleaseCenterConfig
            {
                Enabled = true,
                BaseUrl = "http://release-center.local",
                Channel = "stable",
                AnonKey = "anon-key",
                SiteId = "11111111-1111-1111-1111-111111111111",
                SiteName = "门诊登记台 1",
                RuntimePluginRoot = runtimeRoot
            }
        };

        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri.Contains("/rpc/get_site_authorized_resources", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "resources": [
                            {
                              "resourceId": "6f9e12d5-0a7a-4f93-bf24-e202c7d8d07f",
                              "resourceCode": "postgres-main",
                              "resourceName": "业务库",
                              "resourceType": "PostgreSQL",
                              "pluginCode": "medical-drug-import",
                              "bindingScope": "PluginAtSite",
                              "configVersion": 2,
                              "configPayload": {
                                "host": "127.0.0.1",
                                "port": 5432
                              },
                              "secretRef": "vault://resource-center/postgres-main/password",
                              "capabilities": ["read", "write"]
                            }
                          ]
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                };
            }

            throw new Xunit.Sdk.XunitException($"未预期的请求：{request.RequestUri}");
        });

        try
        {
            var service = new ReleaseCenterService(
                new HttpClient(handler),
                NullLogger<ReleaseCenterService>.Instance,
                Options.Create(settings),
                new PluginCatalogService(),
                cacheService);

            var snapshot = await service.GetAuthorizedResourcesAsync();

            Assert.Single(snapshot.Resources);
            var cachedSnapshot = cacheService.GetCurrentSnapshot();
            Assert.NotNull(cachedSnapshot);
            Assert.Single(cachedSnapshot!.Resources);
            Assert.Equal("medical-drug-import", cachedSnapshot.Resources[0].PluginCode);
            Assert.Single(cachedSnapshot.Resources[0].Resources);
            Assert.Equal("business-db", cachedSnapshot.Resources[0].Resources[0].UsageKey);
        }
        finally
        {
            if (Directory.Exists(runtimeRoot))
            {
                Directory.Delete(runtimeRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GetAuthorizedResourcesAsync_ShouldReturnSnapshot_WhenCacheWriteFails()
    {
        var runtimeRoot = Path.Combine(Path.GetTempPath(), "dib-runtime-plugins", Guid.NewGuid().ToString("N"));
        var pluginDirectory = Path.Combine(runtimeRoot, "MedicalDrugImport");
        Directory.CreateDirectory(pluginDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(pluginDirectory, "plugin.json"),
            """
            {
              "id": "medical-drug-import",
              "name": "医保药品导入",
              "version": "0.1.0",
              "entryAssembly": "MedicalDrugImport.Plugin.dll",
              "entryType": "MedicalDrugImport.Plugin.MedicalDrugImportPlugin",
              "minHostVersion": "1.0.0",
              "resourceRequirements": [
                {
                  "resourceType": "PostgreSQL",
                  "usageKey": "business-db",
                  "required": true,
                  "description": "业务库"
                }
              ]
            }
            """);

        var settings = new AppSettings
        {
            Application = new ApplicationConfig { Version = "1.0.0" },
            ReleaseCenter = new ReleaseCenterConfig
            {
                Enabled = true,
                BaseUrl = "http://release-center.local",
                Channel = "stable",
                AnonKey = "anon-key",
                SiteId = "11111111-1111-1111-1111-111111111111",
                SiteName = "门诊登记台 1",
                RuntimePluginRoot = runtimeRoot
            }
        };

        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri.Contains("/rpc/get_site_authorized_resources", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "resources": [
                            {
                              "resourceId": "6f9e12d5-0a7a-4f93-bf24-e202c7d8d07f",
                              "resourceCode": "postgres-main",
                              "resourceName": "业务库",
                              "resourceType": "PostgreSQL",
                              "pluginCode": "medical-drug-import",
                              "bindingScope": "PluginAtSite",
                              "configVersion": 2,
                              "configPayload": {
                                "host": "127.0.0.1",
                                "port": 5432
                              },
                              "capabilities": ["read", "write"]
                            }
                          ]
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                };
            }

            throw new Xunit.Sdk.XunitException($"未预期的请求：{request.RequestUri}");
        });

        try
        {
            var service = new ReleaseCenterService(
                new HttpClient(handler),
                NullLogger<ReleaseCenterService>.Instance,
                Options.Create(settings),
                new PluginCatalogService(),
                new ThrowingAuthorizedResourceCacheService());

            var snapshot = await service.GetAuthorizedResourcesAsync();

            Assert.Single(snapshot.Resources);
            Assert.Equal("medical-drug-import", snapshot.Resources[0].PluginCode);
        }
        finally
        {
            if (Directory.Exists(runtimeRoot))
            {
                Directory.Delete(runtimeRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GetAuthorizedResourcesAsync_ShouldKeepPreviousCache_WhenAuthorizedResourcesMissingPluginCode()
    {
        var runtimeRoot = Path.Combine(Path.GetTempPath(), "dib-runtime-plugins", Guid.NewGuid().ToString("N"));
        var pluginDirectory = Path.Combine(runtimeRoot, "MedicalDrugImport");
        Directory.CreateDirectory(pluginDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(pluginDirectory, "plugin.json"),
            """
            {
              "id": "medical-drug-import",
              "name": "医保药品导入",
              "version": "0.1.0",
              "entryAssembly": "MedicalDrugImport.Plugin.dll",
              "entryType": "MedicalDrugImport.Plugin.MedicalDrugImportPlugin",
              "minHostVersion": "1.0.0",
              "resourceRequirements": [
                {
                  "resourceType": "PostgreSQL",
                  "usageKey": "business-db",
                  "required": true,
                  "description": "业务库"
                }
              ]
            }
            """);

        var cacheService = new AuthorizedResourceCacheService();
        cacheService.SaveSnapshot(new AuthorizedResourceCacheSnapshot
        {
            SiteId = "site-legacy",
            SnapshotVersion = 7,
            SyncedAt = new DateTimeOffset(2026, 4, 17, 8, 0, 0, TimeSpan.Zero),
            ExpiresAt = new DateTimeOffset(2026, 4, 17, 8, 30, 0, TimeSpan.Zero),
            Resources =
            [
                new AuthorizedPluginResourceSet
                {
                    PluginCode = "medical-drug-import",
                    Resources =
                    [
                        new AuthorizedRuntimeResource
                        {
                            ResourceId = "legacy-resource",
                            ResourceCode = "legacy-postgres",
                            ResourceName = "历史业务库",
                            ResourceType = "PostgreSQL",
                            UsageKey = "business-db",
                            BindingScope = "PluginAtSite",
                            Version = 1,
                            Capabilities = ["read"],
                            Configuration = JsonDocument.Parse("""{"host":"legacy-db.local"}""").RootElement.Clone()
                        }
                    ]
                }
            ]
        });

        var settings = new AppSettings
        {
            Application = new ApplicationConfig { Version = "1.0.0" },
            ReleaseCenter = new ReleaseCenterConfig
            {
                Enabled = true,
                BaseUrl = "http://release-center.local",
                Channel = "stable",
                AnonKey = "anon-key",
                SiteId = "11111111-1111-1111-1111-111111111111",
                SiteName = "门诊登记台 1",
                RuntimePluginRoot = runtimeRoot
            }
        };

        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri.Contains("/rpc/get_site_authorized_resources", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "resources": [
                            {
                              "resourceId": "broken-resource",
                              "resourceCode": "postgres-main",
                              "resourceName": "业务库",
                              "resourceType": "PostgreSQL",
                              "pluginCode": "",
                              "bindingScope": "PluginAtSite",
                              "configVersion": 2,
                              "configPayload": {
                                "host": "127.0.0.1",
                                "port": 5432
                              },
                              "capabilities": ["read", "write"]
                            }
                          ]
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                };
            }

            throw new Xunit.Sdk.XunitException($"未预期的请求：{request.RequestUri}");
        });

        try
        {
            var service = new ReleaseCenterService(
                new HttpClient(handler),
                NullLogger<ReleaseCenterService>.Instance,
                Options.Create(settings),
                new PluginCatalogService(),
                cacheService);

            var snapshot = await service.GetAuthorizedResourcesAsync();

            Assert.Single(snapshot.Resources);
            var cachedSnapshot = cacheService.GetCurrentSnapshot();
            Assert.NotNull(cachedSnapshot);
            Assert.Equal("site-legacy", cachedSnapshot!.SiteId);
            var pluginResources = Assert.Single(cachedSnapshot.Resources);
            var resource = Assert.Single(pluginResources.Resources);
            Assert.Equal("legacy-resource", resource.ResourceId);
            Assert.Equal("business-db", resource.UsageKey);
        }
        finally
        {
            if (Directory.Exists(runtimeRoot))
            {
                Directory.Delete(runtimeRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GetAuthorizedResourcesAsync_ShouldPreserveDistinctUsageKeys_WhenPluginDeclaresMultipleSameTypeRequirementsAndResponseOrderChanges()
    {
        var runtimeRoot = Path.Combine(Path.GetTempPath(), "dib-runtime-plugins", Guid.NewGuid().ToString("N"));
        var pluginDirectory = Path.Combine(runtimeRoot, "MedicalDrugImport");
        Directory.CreateDirectory(pluginDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(pluginDirectory, "plugin.json"),
            """
            {
              "id": "medical-drug-import",
              "name": "医保药品导入",
              "version": "0.1.0",
              "entryAssembly": "MedicalDrugImport.Plugin.dll",
              "entryType": "MedicalDrugImport.Plugin.MedicalDrugImportPlugin",
              "minHostVersion": "1.0.0",
              "resourceRequirements": [
                {
                  "resourceType": "PostgreSQL",
                  "usageKey": "business-db",
                  "required": true,
                  "description": "业务库"
                },
                {
                  "resourceType": "PostgreSQL",
                  "usageKey": "audit-db",
                  "required": true,
                  "description": "审计库"
                }
              ]
            }
            """);

        var cacheService = new AuthorizedResourceCacheService();
        var settings = new AppSettings
        {
            Application = new ApplicationConfig { Version = "1.0.0" },
            ReleaseCenter = new ReleaseCenterConfig
            {
                Enabled = true,
                BaseUrl = "http://release-center.local",
                Channel = "stable",
                AnonKey = "anon-key",
                SiteId = "11111111-1111-1111-1111-111111111111",
                SiteName = "门诊登记台 1",
                RuntimePluginRoot = runtimeRoot
            }
        };

        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri.Contains("/rpc/get_site_authorized_resources", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "resources": [
                            {
                              "resourceId": "db4d0f3a-930f-4c7a-88c7-bff61f6f7f5a",
                              "resourceCode": "postgres-audit",
                              "resourceName": "业务库审计库",
                              "resourceType": "PostgreSQL",
                              "pluginCode": "medical-drug-import",
                              "bindingScope": "PluginAtSite",
                              "configVersion": 1,
                              "configPayload": {
                                "host": "127.0.0.1",
                                "port": 5433
                              },
                              "secretRef": "vault://resource-center/postgres-audit/password",
                              "capabilities": ["read"]
                            },
                            {
                              "resourceId": "6f9e12d5-0a7a-4f93-bf24-e202c7d8d07f",
                              "resourceCode": "postgres-main",
                              "resourceName": "业务库主库",
                              "resourceType": "PostgreSQL",
                              "pluginCode": "medical-drug-import",
                              "bindingScope": "PluginAtSite",
                              "configVersion": 2,
                              "configPayload": {
                                "host": "127.0.0.1",
                                "port": 5432
                              },
                              "secretRef": "vault://resource-center/postgres-main/password",
                              "capabilities": ["read", "write"]
                            }
                          ]
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                };
            }

            throw new Xunit.Sdk.XunitException($"未预期的请求：{request.RequestUri}");
        });

        try
        {
            var service = new ReleaseCenterService(
                new HttpClient(handler),
                NullLogger<ReleaseCenterService>.Instance,
                Options.Create(settings),
                new PluginCatalogService(),
                cacheService);

            await service.GetAuthorizedResourcesAsync();

            var cachedSnapshot = cacheService.GetCurrentSnapshot();
            Assert.NotNull(cachedSnapshot);
            var pluginResources = Assert.Single(cachedSnapshot!.Resources);
            Assert.Equal("medical-drug-import", pluginResources.PluginCode);
            Assert.Equal(2, pluginResources.Resources.Count);
            Assert.Equal("business-db", pluginResources.Resources[0].UsageKey);
            Assert.Equal("audit-db", pluginResources.Resources[1].UsageKey);
        }
        finally
        {
            if (Directory.Exists(runtimeRoot))
            {
                Directory.Delete(runtimeRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DiscoverResourcesAsync_ShouldPostInstalledPluginRequirements_AndReturnSections()
    {
        var runtimeRoot = Path.Combine(Path.GetTempPath(), "dib-runtime-plugins", Guid.NewGuid().ToString("N"));
        var pluginDirectory = Path.Combine(runtimeRoot, "PatientRegistration");
        Directory.CreateDirectory(pluginDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(pluginDirectory, "plugin.json"),
            """
            {
              "id": "patient-registration",
              "name": "就诊登记",
              "version": "0.1.0",
              "entryAssembly": "PatientRegistration.Plugin.dll",
              "entryType": "PatientRegistration.Plugin.PatientRegistrationPlugin",
              "minHostVersion": "1.0.0",
              "resourceRequirements": [
                {
                  "resourceType": "PostgreSQL",
                  "usageKey": "registration-db",
                  "required": true,
                  "description": "登记业务库"
                }
              ]
            }
            """);

        HttpRequestMessage? requestMessage = null;
        string requestBody = string.Empty;
        var settings = new AppSettings
        {
            Application = new ApplicationConfig { Version = "1.0.0" },
            ReleaseCenter = new ReleaseCenterConfig
            {
                Enabled = true,
                BaseUrl = "http://release-center.local",
                Channel = "stable",
                AnonKey = "anon-key",
                SiteId = "22222222-2222-2222-2222-222222222222",
                SiteName = "门诊登记台 2",
                RuntimePluginRoot = runtimeRoot
            }
        };

        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri.Contains("/rpc/discover_site_resources", StringComparison.OrdinalIgnoreCase))
            {
                requestMessage = request;
                requestBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "availableToApply": [
                            {
                              "resourceId": "8e903740-b757-4bb1-8246-884bc0aa6617",
                              "resourceCode": "postgres-outpatient-01",
                              "resourceName": "门诊业务 PostgreSQL",
                              "resourceType": "PostgreSQL",
                              "visibilityScope": "Shared",
                              "matchedPlugins": ["patient-registration"]
                            }
                          ],
                          "authorized": [
                            {
                              "resourceId": "6f9e12d5-0a7a-4f93-bf24-e202c7d8d07f",
                              "resourceCode": "ocr-gateway",
                              "resourceName": "OCR 网关",
                              "resourceType": "HttpService",
                              "pluginCode": "patient-registration",
                              "bindingScope": "PluginAtSite"
                            }
                          ],
                          "pendingApplications": [
                            {
                              "applicationId": "1d7f2a4d-c5b6-4723-9dbd-bac2f3d272a9",
                              "applicationType": "UseResource",
                              "resourceId": "8e903740-b757-4bb1-8246-884bc0aa6617",
                              "status": "UnderReview"
                            }
                          ]
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                };
            }

            throw new Xunit.Sdk.XunitException($"未预期的请求：{request.RequestUri}");
        });

        try
        {
            var service = new ReleaseCenterService(
                new HttpClient(handler),
                NullLogger<ReleaseCenterService>.Instance,
                Options.Create(settings));

            var snapshot = await service.DiscoverResourcesAsync();

            Assert.NotNull(requestMessage);
            using var document = JsonDocument.Parse(requestBody);
            Assert.Equal("22222222-2222-2222-2222-222222222222", document.RootElement.GetProperty("p_site_id").GetString());
            var plugin = Assert.Single(document.RootElement.GetProperty("p_plugins_json").EnumerateArray());
            Assert.Equal("patient-registration", plugin.GetProperty("pluginCode").GetString());

            var available = Assert.Single(snapshot.AvailableToApply);
            Assert.Equal("Shared", available.VisibilityScope);
            Assert.Equal("patient-registration", Assert.Single(available.MatchedPlugins));

            var authorized = Assert.Single(snapshot.Authorized);
            Assert.Equal("patient-registration", authorized.PluginCode);

            var pending = Assert.Single(snapshot.PendingApplications);
            Assert.Equal("UnderReview", pending.Status);
        }
        finally
        {
            if (Directory.Exists(runtimeRoot))
            {
                Directory.Delete(runtimeRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ApplyResourceAsync_ShouldPostResourceApplicationRequest()
    {
        HttpRequestMessage? requestMessage = null;
        string requestBody = string.Empty;
        var settings = new AppSettings
        {
            Application = new ApplicationConfig { Version = "1.0.0" },
            ReleaseCenter = new ReleaseCenterConfig
            {
                Enabled = true,
                BaseUrl = "http://release-center.local",
                Channel = "stable",
                AnonKey = "anon-key",
                SiteId = "33333333-3333-3333-3333-333333333333",
                SiteName = "门诊登记台 3"
            }
        };

        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri.Contains("/rpc/apply_site_resource", StringComparison.OrdinalIgnoreCase))
            {
                requestMessage = request;
                requestBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "success": true,
                          "message": "申请已提交",
                          "applicationId": "apply-001",
                          "status": "Submitted"
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                };
            }

            throw new Xunit.Sdk.XunitException($"未预期的请求：{request.RequestUri}");
        });

        var service = new ReleaseCenterService(
            new HttpClient(handler),
            NullLogger<ReleaseCenterService>.Instance,
            Options.Create(settings));

        var result = await service.ApplyResourceAsync("resource-001", "patient-registration", "需要访问业务库");

        Assert.NotNull(requestMessage);
        Assert.True(result.IsSuccess);
        Assert.Equal("apply-001", result.ApplicationId);
        using var document = JsonDocument.Parse(requestBody);
        Assert.Equal("33333333-3333-3333-3333-333333333333", document.RootElement.GetProperty("p_site_id").GetString());
        Assert.Equal("resource-001", document.RootElement.GetProperty("p_resource_id").GetString());
        Assert.Equal("patient-registration", document.RootElement.GetProperty("p_plugin_code").GetString());
        Assert.Equal("站点信息：门诊登记台 3；申请说明：需要访问业务库", document.RootElement.GetProperty("p_reason").GetString());
    }

    [Fact]
    public async Task ApplyResourceAsync_ShouldUseSiteNameOnlyInReason()
    {
        string requestBody = string.Empty;
        var settings = new AppSettings
        {
            Application = new ApplicationConfig { Version = "1.0.0" },
            ReleaseCenter = new ReleaseCenterConfig
            {
                Enabled = true,
                BaseUrl = "http://release-center.local",
                Channel = "stable",
                AnonKey = "anon-key",
                SiteId = "44444444-4444-4444-4444-444444444444",
                SiteName = "门诊登记台 5"
            }
        };

        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri.Contains("/rpc/apply_site_resource", StringComparison.OrdinalIgnoreCase))
            {
                requestBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "success": true,
                          "message": "申请已提交",
                          "applicationId": "apply-002",
                          "status": "Submitted"
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                };
            }

            throw new Xunit.Sdk.XunitException($"未预期的请求：{request.RequestUri}");
        });

        var service = new ReleaseCenterService(
            new HttpClient(handler),
            NullLogger<ReleaseCenterService>.Instance,
            Options.Create(settings));

        await service.ApplyResourceAsync("resource-002", "patient-registration", "需要用于门诊实名登记");

        using var document = JsonDocument.Parse(requestBody);
        var reason = document.RootElement.GetProperty("p_reason").GetString();
        Assert.NotNull(reason);
        Assert.Contains("站点信息：门诊登记台 5", reason, StringComparison.Ordinal);
        Assert.DoesNotContain("第一人民医院", reason, StringComparison.Ordinal);
        Assert.Contains("需要用于门诊实名登记", reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAuthorizedResourcesAsync_ShouldLogWarning_WhenReleaseCenterAnonKeyMissing()
    {
        var logger = new CapturingLogger<ReleaseCenterService>();
        var handler = new StubHttpMessageHandler(_ => throw new Xunit.Sdk.XunitException("不应发起 HTTP 请求"));
        var settings = new AppSettings
        {
            Application = new ApplicationConfig { Version = "1.0.0" },
            ReleaseCenter = new ReleaseCenterConfig
            {
                Enabled = true,
                BaseUrl = "http://release-center.local",
                Channel = "stable",
                AnonKey = string.Empty,
                SiteId = "55555555-5555-5555-5555-555555555555",
                SiteName = "门诊登记台 6"
            }
        };

        var service = new ReleaseCenterService(
            new HttpClient(handler),
            logger,
            Options.Create(settings));

        var snapshot = await service.GetAuthorizedResourcesAsync();

        Assert.Empty(snapshot.Resources);
        Assert.Contains(
            logger.WarningMessages,
            message => message.Contains("ReleaseCenter.AnonKey 未配置", StringComparison.Ordinal));
    }

    private static ReleaseCenterService CreateService(string clientManifest, string pluginManifest, AppSettings? settings = null)
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var content = request.RequestUri!.AbsoluteUri.Contains("client-manifest", StringComparison.OrdinalIgnoreCase)
                ? clientManifest
                : pluginManifest;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            };
        });

        return new ReleaseCenterService(
            new HttpClient(handler),
            NullLogger<ReleaseCenterService>.Instance,
            Options.Create(settings ?? new AppSettings
            {
                Application = new ApplicationConfig { Version = "1.0.0" },
                ReleaseCenter = new ReleaseCenterConfig
                {
                    Enabled = true,
                    BaseUrl = "http://release-center.local",
                    Channel = "stable"
                }
            }));
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
        }
    }

    private sealed class ThrowingAuthorizedResourceCacheService : DigitalIntelligenceBridge.Services.IAuthorizedResourceCacheService
    {
        public void SaveSnapshot(AuthorizedResourceCacheSnapshot snapshot)
        {
            throw new InvalidOperationException("缓存写入失败");
        }

        public AuthorizedResourceCacheSnapshot? GetCurrentSnapshot()
        {
            return null;
        }

        public AuthorizedPluginResourceSet GetResourcesForPlugin(string pluginCode)
        {
            return new AuthorizedPluginResourceSet
            {
                PluginCode = pluginCode,
                Resources = []
            };
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> WarningMessages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                WarningMessages.Add(formatter(state, exception));
            }
        }
    }
}



