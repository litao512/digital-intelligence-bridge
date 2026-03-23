namespace DigitalIntelligenceBridge.Plugin.Abstractions;

public class PluginMenuItem
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;

    public int Order { get; set; }
}
