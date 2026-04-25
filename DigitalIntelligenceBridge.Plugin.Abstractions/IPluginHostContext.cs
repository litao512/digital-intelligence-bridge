using System.Collections.Generic;

namespace DigitalIntelligenceBridge.Plugin.Abstractions;

public interface IPluginHostContext
{
    string HostVersion { get; }

    string PluginDirectory { get; }

    void LogInformation(string message);

    IReadOnlyList<AuthorizedRuntimeResource> GetAuthorizedResources();

    bool TryGetResource(string usageKey, out AuthorizedRuntimeResource? resource);
}
