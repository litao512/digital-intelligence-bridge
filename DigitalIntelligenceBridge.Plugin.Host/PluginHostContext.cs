using DigitalIntelligenceBridge.Plugin.Abstractions;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace DigitalIntelligenceBridge.Plugin.Host;

public class PluginHostContext : IPluginHostContext
{
    private readonly Action<string> _logInformation;
    private readonly IAuthorizedResourceCacheService _authorizedResourceCacheService;
    private readonly string _pluginCode;

    public PluginHostContext(
        string hostVersion,
        string pluginDirectory,
        string pluginCode,
        IAuthorizedResourceCacheService authorizedResourceCacheService,
        Action<string> logInformation)
    {
        HostVersion = hostVersion;
        PluginDirectory = pluginDirectory;
        _pluginCode = pluginCode;
        _authorizedResourceCacheService = authorizedResourceCacheService;
        _logInformation = logInformation;
    }

    public string HostVersion { get; }

    public string PluginDirectory { get; }

    public void LogInformation(string message)
    {
        _logInformation(message);
    }

    public IReadOnlyList<AuthorizedRuntimeResource> GetAuthorizedResources()
    {
        return CloneResources(_authorizedResourceCacheService.GetResourcesForPlugin(_pluginCode));
    }

    public bool TryGetResource(string usageKey, out AuthorizedRuntimeResource? resource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(usageKey);

        var match = _authorizedResourceCacheService
            .GetResourcesForPlugin(_pluginCode)
            .FirstOrDefault(item => item.UsageKey == usageKey);
        if (match is null)
        {
            resource = null;
            return false;
        }

        resource = CloneResource(match);
        return true;
    }

    private static IReadOnlyList<AuthorizedRuntimeResource> CloneResources(IEnumerable<AuthorizedRuntimeResource> resources)
    {
        return resources.Select(CloneResource).ToArray();
    }

    private static AuthorizedRuntimeResource CloneResource(AuthorizedRuntimeResource resource)
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
}
