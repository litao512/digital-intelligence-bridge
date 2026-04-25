using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DigitalIntelligenceBridge.Models;

public sealed class ResourceDescriptor
{
    [JsonPropertyName("resourceId")]
    public string ResourceId { get; set; } = string.Empty;

    [JsonPropertyName("resourceCode")]
    public string ResourceCode { get; set; } = string.Empty;

    [JsonPropertyName("resourceName")]
    public string ResourceName { get; set; } = string.Empty;

    [JsonPropertyName("resourceType")]
    public string ResourceType { get; set; } = string.Empty;

    [JsonPropertyName("pluginCode")]
    public string? PluginCode { get; set; }

    [JsonPropertyName("bindingScope")]
    public string? BindingScope { get; set; }

    [JsonPropertyName("configVersion")]
    public int ConfigVersion { get; set; }

    [JsonPropertyName("configPayload")]
    public JsonElement ConfigPayload { get; set; }

    [JsonPropertyName("secretPayload")]
    public string? SecretPayload { get; set; }

    [JsonPropertyName("secretRef")]
    public string? SecretRef { get; set; }

    [JsonPropertyName("capabilities")]
    public IReadOnlyList<string> Capabilities { get; set; } = [];
}
