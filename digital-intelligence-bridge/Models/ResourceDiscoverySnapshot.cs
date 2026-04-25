using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DigitalIntelligenceBridge.Models;

public sealed class ResourceDiscoverySnapshot
{
    [JsonPropertyName("availableToApply")]
    public IReadOnlyList<DiscoverableResourceDescriptor> AvailableToApply { get; set; } = [];

    [JsonPropertyName("authorized")]
    public IReadOnlyList<ResourceDescriptor> Authorized { get; set; } = [];

    [JsonPropertyName("pendingApplications")]
    public IReadOnlyList<PendingResourceApplication> PendingApplications { get; set; } = [];
}
