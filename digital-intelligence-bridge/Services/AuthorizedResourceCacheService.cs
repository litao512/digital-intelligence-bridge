using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Models;
using DigitalIntelligenceBridge.Plugin.Abstractions;
using PluginResourceCacheService = DigitalIntelligenceBridge.Plugin.Abstractions.IAuthorizedResourceCacheService;

namespace DigitalIntelligenceBridge.Services;

public interface IAuthorizedResourceCacheService
{
    void SaveSnapshot(AuthorizedResourceCacheSnapshot snapshot);

    AuthorizedResourceCacheSnapshot? GetCurrentSnapshot();

    AuthorizedPluginResourceSet GetResourcesForPlugin(string pluginCode);
}

public sealed class AuthorizedResourceCacheService : IAuthorizedResourceCacheService, PluginResourceCacheService
{
    private static readonly string[] PersistedConfigurationKeys =
    [
        "connectionString",
        "host",
        "port",
        "database",
        "username",
        "user",
        "password",
        "searchPath",
        "sslMode",
        "server",
        "encrypt",
        "trustServerCertificate",
        "baseUrl",
        "endpoint",
        "projectRef",
        "apiKey",
        "serviceRoleKey",
        "anonKey",
        "timeoutSeconds",
        "token",
        "authType"
    ];

    private readonly object _syncRoot = new();
    private readonly string _cacheFilePath;
    private AuthorizedResourceCacheSnapshot? _currentSnapshot;

    public AuthorizedResourceCacheService()
        : this(ConfigurationExtensions.GetAuthorizedResourcesCacheFilePath())
    {
    }

    internal AuthorizedResourceCacheService(string cacheFilePath)
    {
        _cacheFilePath = cacheFilePath;
        _currentSnapshot = TryLoadSnapshot(cacheFilePath);
    }

    public void SaveSnapshot(AuthorizedResourceCacheSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var clonedSnapshot = CloneSnapshot(snapshot);

        lock (_syncRoot)
        {
            _currentSnapshot = clonedSnapshot;
            PersistSnapshot(clonedSnapshot);
        }
    }

    public AuthorizedResourceCacheSnapshot? GetCurrentSnapshot()
    {
        lock (_syncRoot)
        {
            return _currentSnapshot is null ? null : CloneSnapshot(_currentSnapshot);
        }
    }

    public AuthorizedPluginResourceSet GetResourcesForPlugin(string pluginCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginCode);

        lock (_syncRoot)
        {
            var snapshot = _currentSnapshot;
            if (snapshot is null)
            {
                return new AuthorizedPluginResourceSet
                {
                    PluginCode = pluginCode,
                    Resources = []
                };
            }

            var resourceSet = snapshot.Resources.FirstOrDefault(resource => resource.PluginCode == pluginCode);
            if (resourceSet is null)
            {
                return new AuthorizedPluginResourceSet
                {
                    PluginCode = pluginCode,
                    Resources = []
                };
            }

            return new AuthorizedPluginResourceSet
            {
                PluginCode = resourceSet.PluginCode,
                Resources = CloneResources(resourceSet.Resources)
            };
        }
    }

    IReadOnlyList<AuthorizedRuntimeResource> PluginResourceCacheService.GetResourcesForPlugin(string pluginCode)
    {
        return GetResourcesForPlugin(pluginCode).Resources;
    }

    private static AuthorizedResourceCacheSnapshot CloneSnapshot(AuthorizedResourceCacheSnapshot snapshot)
    {
        return new AuthorizedResourceCacheSnapshot
        {
            SiteId = snapshot.SiteId,
            SnapshotVersion = snapshot.SnapshotVersion,
            SyncedAt = snapshot.SyncedAt,
            ExpiresAt = snapshot.ExpiresAt,
            Resources = ClonePluginResourceSets(snapshot.Resources)
        };
    }

    private static IReadOnlyList<AuthorizedPluginResourceSet> ClonePluginResourceSets(IEnumerable<AuthorizedPluginResourceSet> resources)
    {
        return resources.Select(ClonePluginResourceSet).ToArray();
    }

    private static AuthorizedPluginResourceSet ClonePluginResourceSet(AuthorizedPluginResourceSet resourceSet)
    {
        return new AuthorizedPluginResourceSet
        {
            PluginCode = resourceSet.PluginCode,
            Resources = CloneResources(resourceSet.Resources)
        };
    }

    private static IReadOnlyList<AuthorizedRuntimeResource> CloneResources(IEnumerable<AuthorizedRuntimeResource> resources)
    {
        return resources.Select(CloneRuntimeResource).ToArray();
    }

    private static AuthorizedRuntimeResource CloneRuntimeResource(AuthorizedRuntimeResource resource)
    {
        return new AuthorizedRuntimeResource
        {
            ResourceId = resource.ResourceId,
            ResourceCode = resource.ResourceCode,
            ResourceName = resource.ResourceName,
            ResourceType = resource.ResourceType,
            UsageKey = resource.UsageKey,
            BindingScope = resource.BindingScope,
            Version = resource.Version,
            Capabilities = resource.Capabilities.ToArray(),
            Configuration = CloneConfiguration(resource.Configuration)
        };
    }

    private static JsonElement CloneConfiguration(JsonElement configuration)
    {
        using var document = JsonDocument.Parse(configuration.GetRawText());
        return document.RootElement.Clone();
    }

    private void PersistSnapshot(AuthorizedResourceCacheSnapshot snapshot)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_cacheFilePath)!);
        var tempFilePath = _cacheFilePath + ".tmp";
        var persistedSnapshot = CreatePersistedSnapshot(snapshot);
        var json = JsonSerializer.Serialize(persistedSnapshot, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(tempFilePath, json);

        if (File.Exists(_cacheFilePath))
        {
            File.Delete(_cacheFilePath);
        }

        File.Move(tempFilePath, _cacheFilePath);
    }

    private static AuthorizedResourceCacheSnapshot? TryLoadSnapshot(string cacheFilePath)
    {
        if (!File.Exists(cacheFilePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(cacheFilePath);
            var snapshot = JsonSerializer.Deserialize<AuthorizedResourceCacheSnapshot>(json);
            return snapshot is null ? null : CloneSnapshot(snapshot);
        }
        catch
        {
            return null;
        }
    }

    private static AuthorizedResourceCacheSnapshot CreatePersistedSnapshot(AuthorizedResourceCacheSnapshot snapshot)
    {
        return new AuthorizedResourceCacheSnapshot
        {
            SiteId = snapshot.SiteId,
            SnapshotVersion = snapshot.SnapshotVersion,
            SyncedAt = snapshot.SyncedAt,
            ExpiresAt = snapshot.ExpiresAt,
            Resources = snapshot.Resources.Select(CreatePersistedResourceSet).ToArray()
        };
    }

    private static AuthorizedPluginResourceSet CreatePersistedResourceSet(AuthorizedPluginResourceSet resourceSet)
    {
        return new AuthorizedPluginResourceSet
        {
            PluginCode = resourceSet.PluginCode,
            Resources = resourceSet.Resources.Select(CreatePersistedRuntimeResource).ToArray()
        };
    }

    private static AuthorizedRuntimeResource CreatePersistedRuntimeResource(AuthorizedRuntimeResource resource)
    {
        return new AuthorizedRuntimeResource
        {
            ResourceId = resource.ResourceId,
            ResourceCode = resource.ResourceCode,
            ResourceName = resource.ResourceName,
            ResourceType = resource.ResourceType,
            UsageKey = resource.UsageKey,
            BindingScope = resource.BindingScope,
            Version = resource.Version,
            Capabilities = resource.Capabilities.ToArray(),
            Configuration = MinimizeConfiguration(resource.Configuration)
        };
    }

    private static JsonElement MinimizeConfiguration(JsonElement configuration)
    {
        if (configuration.ValueKind != JsonValueKind.Object)
        {
            return CloneConfiguration(configuration);
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();

            foreach (var key in PersistedConfigurationKeys)
            {
                if (configuration.TryGetProperty(key, out var property))
                {
                    writer.WritePropertyName(key);
                    property.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        return JsonDocument.Parse(stream.ToArray()).RootElement.Clone();
    }
}
