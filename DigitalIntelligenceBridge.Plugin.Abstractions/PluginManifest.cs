namespace DigitalIntelligenceBridge.Plugin.Abstractions;

public class PluginManifest
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string EntryAssembly { get; set; } = string.Empty;

    public string EntryType { get; set; } = string.Empty;

    public string MinHostVersion { get; set; } = string.Empty;

    public IReadOnlyList<PluginResourceRequirement> ResourceRequirements { get; set; } = [];

    public bool IsCompatibleWith(string hostVersion)
    {
        if (!System.Version.TryParse(MinHostVersion, out var minimumVersion))
        {
            return true;
        }

        if (!System.Version.TryParse(hostVersion, out var currentVersion))
        {
            return true;
        }

        return currentVersion >= minimumVersion;
    }
}

public class PluginResourceRequirement
{
    public string ResourceType { get; set; } = string.Empty;

    public string UsageKey { get; set; } = string.Empty;

    public bool Required { get; set; }

    public string Description { get; set; } = string.Empty;
}
