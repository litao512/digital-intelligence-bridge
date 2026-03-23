using DigitalIntelligenceBridge.Plugin.Abstractions;

namespace DigitalIntelligenceBridge.Plugin.Host;

public class PluginHostContext : IPluginHostContext
{
    private readonly Action<string> _logInformation;

    public PluginHostContext(string hostVersion, string pluginDirectory, Action<string> logInformation)
    {
        HostVersion = hostVersion;
        PluginDirectory = pluginDirectory;
        _logInformation = logInformation;
    }

    public string HostVersion { get; }

    public string PluginDirectory { get; }

    public void LogInformation(string message)
    {
        _logInformation(message);
    }
}
