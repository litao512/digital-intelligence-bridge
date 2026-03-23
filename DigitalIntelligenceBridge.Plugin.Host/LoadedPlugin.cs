using DigitalIntelligenceBridge.Plugin.Abstractions;

namespace DigitalIntelligenceBridge.Plugin.Host;

public class LoadedPlugin
{
    public PluginManifest Manifest { get; set; } = new();

    public IPluginModule? Module { get; set; }

    public string PluginDirectory { get; set; } = string.Empty;

    public string ManifestPath { get; set; } = string.Empty;

    public string ErrorMessage { get; set; } = string.Empty;
}
