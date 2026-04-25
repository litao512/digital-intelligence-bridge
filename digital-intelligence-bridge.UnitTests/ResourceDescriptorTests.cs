using System.Text.Json;
using DigitalIntelligenceBridge.Models;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class ResourceDescriptorTests
{
    [Fact]
    public void DeserializeAuthorizedResourceSnapshot_ShouldReadResourcesAndCapabilities()
    {
        const string json = """
                            {
                              "resources": [
                                {
                                  "resourceId": "6f9e12d5-0a7a-4f93-bf24-e202c7d8d07f",
                                  "resourceCode": "ocr-gateway",
                                  "resourceName": "OCR 网关",
                                  "resourceType": "HttpService",
                                  "pluginCode": "patient-registration",
                                  "bindingScope": "PluginAtSite",
                                  "configVersion": 3,
                                  "configPayload": {
                                    "baseUrl": "https://ocr.example.local",
                                    "timeoutSeconds": 20
                                  },
                                  "secretRef": "vault://resource-center/ocr-gateway/token",
                                  "capabilities": ["invoke"]
                                }
                              ]
                            }
                            """;

        var snapshot = JsonSerializer.Deserialize<AuthorizedResourceSnapshot>(json);

        var resource = Assert.Single(snapshot!.Resources);
        Assert.Equal("ocr-gateway", resource.ResourceCode);
        Assert.Equal("HttpService", resource.ResourceType);
        Assert.Equal("patient-registration", resource.PluginCode);
        Assert.Equal("PluginAtSite", resource.BindingScope);
        Assert.Equal(3, resource.ConfigVersion);
        Assert.Equal("vault://resource-center/ocr-gateway/token", resource.SecretRef);
        Assert.Single(resource.Capabilities);
        Assert.Equal("invoke", resource.Capabilities[0]);
        Assert.Equal("https://ocr.example.local", resource.ConfigPayload.GetProperty("baseUrl").GetString());
    }

    [Fact]
    public void DeserializeAuthorizedResourceSnapshot_ShouldDefaultCollections_WhenFieldsMissing()
    {
        const string json = """{ "resources": [ { "resourceId": "a7d87e59-1b01-4e1a-8b56-b307db8f839d", "resourceCode": "postgres-main", "resourceName": "业务库", "resourceType": "PostgreSQL" } ] }""";

        var snapshot = JsonSerializer.Deserialize<AuthorizedResourceSnapshot>(json);

        var resource = Assert.Single(snapshot!.Resources);
        Assert.Empty(resource.Capabilities);
        Assert.True(resource.ConfigPayload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null);
        Assert.Null(resource.SecretPayload);
        Assert.Null(resource.SecretRef);
    }

    [Fact]
    public void DeserializeResourceDiscoverySnapshot_ShouldReadAvailableAuthorizedAndPendingSections()
    {
        const string json = """
                            {
                              "availableToApply": [
                                {
                                  "resourceId": "8e903740-b757-4bb1-8246-884bc0aa6617",
                                  "resourceCode": "postgres-outpatient-01",
                                  "resourceName": "门诊业务 PostgreSQL",
                                  "resourceType": "PostgreSQL",
                                  "visibilityScope": "Shared",
                                  "matchedPlugins": ["medical-drug-import"]
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
                            """;

        var snapshot = JsonSerializer.Deserialize<ResourceDiscoverySnapshot>(json);

        var available = Assert.Single(snapshot!.AvailableToApply);
        Assert.Equal("Shared", available.VisibilityScope);
        Assert.Equal("medical-drug-import", Assert.Single(available.MatchedPlugins));

        var authorized = Assert.Single(snapshot.Authorized);
        Assert.Equal("patient-registration", authorized.PluginCode);

        var pending = Assert.Single(snapshot.PendingApplications);
        Assert.Equal("UseResource", pending.ApplicationType);
        Assert.Equal("UnderReview", pending.Status);
    }
}
