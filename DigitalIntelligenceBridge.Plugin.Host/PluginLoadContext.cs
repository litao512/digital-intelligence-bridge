using System.Reflection;
using System.Runtime.Loader;
using DigitalIntelligenceBridge.Plugin.Abstractions;

namespace DigitalIntelligenceBridge.Plugin.Host;

public class PluginLoadContext : AssemblyLoadContext
{
    private readonly string _pluginDirectory;

    public PluginLoadContext(string pluginDirectory)
        : base(isCollectible: false)
    {
        _pluginDirectory = pluginDirectory;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (string.Equals(
                assemblyName.Name,
                typeof(IPluginModule).Assembly.GetName().Name,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var assemblyPath = Path.Combine(_pluginDirectory, $"{assemblyName.Name}.dll");
        if (!File.Exists(assemblyPath))
        {
            return null;
        }

        return LoadFromAssemblyPath(assemblyPath);
    }
}
