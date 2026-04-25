using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DigitalIntelligenceBridge.Plugin.Abstractions;

public sealed class AuthorizedRuntimeResource
{
    private static JsonElement CreateEmptyConfiguration()
    {
        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }

    [JsonPropertyName("resourceId")]
    public string ResourceId { get; set; } = string.Empty;

    [JsonPropertyName("resourceCode")]
    public string ResourceCode { get; set; } = string.Empty;

    [JsonPropertyName("resourceName")]
    public string ResourceName { get; set; } = string.Empty;

    [JsonPropertyName("resourceType")]
    public string ResourceType { get; set; } = string.Empty;

    [JsonPropertyName("usageKey")]
    public string UsageKey { get; set; } = string.Empty;

    [JsonPropertyName("bindingScope")]
    public string? BindingScope { get; set; }

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("capabilities")]
    public IReadOnlyList<string> Capabilities { get; set; } = [];

    [JsonPropertyName("configuration")]
    public JsonElement Configuration { get; set; } = CreateEmptyConfiguration();
}
