using System.Text.Json;
using DigitalIntelligenceBridge.Plugin.Abstractions;

namespace DigitalIntelligenceBridge.Plugin.Host;

public class PluginCatalogService
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IReadOnlyList<LoadedPlugin> DiscoverManifests(string pluginRootDirectory)
    {
        if (string.IsNullOrWhiteSpace(pluginRootDirectory) || !Directory.Exists(pluginRootDirectory))
        {
            return [];
        }

        var results = new List<LoadedPlugin>();
        foreach (var pluginDirectory in Directory.GetDirectories(pluginRootDirectory))
        {
            var manifestPath = Path.Combine(pluginDirectory, "plugin.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            var manifest = TryReadManifest(manifestPath);
            if (manifest is null || !IsValidManifest(manifest))
            {
                continue;
            }

            results.Add(new LoadedPlugin
            {
                Manifest = manifest,
                PluginDirectory = pluginDirectory,
                ManifestPath = manifestPath
            });
        }

        return results;
    }

    private static PluginManifest? TryReadManifest(string manifestPath)
    {
        try
        {
            return JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(manifestPath), ManifestJsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsValidManifest(PluginManifest manifest)
    {
        return !string.IsNullOrWhiteSpace(manifest.Id) &&
               !string.IsNullOrWhiteSpace(manifest.Name) &&
               !string.IsNullOrWhiteSpace(manifest.Version) &&
               !string.IsNullOrWhiteSpace(manifest.EntryAssembly) &&
               !string.IsNullOrWhiteSpace(manifest.EntryType) &&
               !string.IsNullOrWhiteSpace(manifest.MinHostVersion);
    }
}
