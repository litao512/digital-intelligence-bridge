using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DigitalIntelligenceBridge.Models;

public sealed class AuthorizedResourceCacheSnapshot
{
    [JsonPropertyName("siteId")]
    public string SiteId { get; set; } = string.Empty;

    [JsonPropertyName("snapshotVersion")]
    public int SnapshotVersion { get; set; }

    [JsonPropertyName("syncedAt")]
    public DateTimeOffset SyncedAt { get; set; }

    [JsonPropertyName("expiresAt")]
    public DateTimeOffset ExpiresAt { get; set; }

    [JsonPropertyName("resources")]
    public IReadOnlyList<AuthorizedPluginResourceSet> Resources { get; set; } = [];
}
