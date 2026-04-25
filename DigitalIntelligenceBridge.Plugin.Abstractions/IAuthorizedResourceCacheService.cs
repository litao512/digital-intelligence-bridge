using System.Collections.Generic;

namespace DigitalIntelligenceBridge.Plugin.Abstractions;

public interface IAuthorizedResourceCacheService
{
    IReadOnlyList<AuthorizedRuntimeResource> GetResourcesForPlugin(string pluginCode);
}
