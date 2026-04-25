using System.Collections.Generic;
using System.Text.Json.Serialization;
using DigitalIntelligenceBridge.Plugin.Abstractions;

namespace DigitalIntelligenceBridge.Models;

public sealed class AuthorizedPluginResourceSet
{
    [JsonPropertyName("pluginCode")]
    public string PluginCode { get; set; } = string.Empty;

    [JsonPropertyName("resources")]
    public IReadOnlyList<AuthorizedRuntimeResource> Resources { get; set; } = [];
}
