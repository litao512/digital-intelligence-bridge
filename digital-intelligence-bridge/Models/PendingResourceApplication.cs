using System.Text.Json.Serialization;

namespace DigitalIntelligenceBridge.Models;

public sealed class PendingResourceApplication
{
    [JsonPropertyName("applicationId")]
    public string ApplicationId { get; set; } = string.Empty;

    [JsonPropertyName("applicationType")]
    public string ApplicationType { get; set; } = string.Empty;

    [JsonPropertyName("resourceId")]
    public string ResourceId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}
