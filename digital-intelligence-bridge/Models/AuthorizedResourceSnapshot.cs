using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DigitalIntelligenceBridge.Models;

public sealed class AuthorizedResourceSnapshot
{
    [JsonPropertyName("resources")]
    public IReadOnlyList<ResourceDescriptor> Resources { get; set; } = [];
}
