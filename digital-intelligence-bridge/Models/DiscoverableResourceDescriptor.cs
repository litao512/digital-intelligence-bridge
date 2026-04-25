using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DigitalIntelligenceBridge.Models;

public sealed class DiscoverableResourceDescriptor
{
    [JsonPropertyName("resourceId")]
    public string ResourceId { get; set; } = string.Empty;

    [JsonPropertyName("resourceCode")]
    public string ResourceCode { get; set; } = string.Empty;

    [JsonPropertyName("resourceName")]
    public string ResourceName { get; set; } = string.Empty;

    [JsonPropertyName("resourceType")]
    public string ResourceType { get; set; } = string.Empty;

    [JsonPropertyName("visibilityScope")]
    public string? VisibilityScope { get; set; }

    [JsonPropertyName("matchedPlugins")]
    public IReadOnlyList<string> MatchedPlugins { get; set; } = [];
}
