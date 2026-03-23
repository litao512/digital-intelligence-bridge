namespace DigitalIntelligenceBridge.Plugin.Abstractions;

public interface IPluginHostContext
{
    string HostVersion { get; }

    string PluginDirectory { get; }

    void LogInformation(string message);
}
