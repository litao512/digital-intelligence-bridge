using System.Reflection;
using DigitalIntelligenceBridge.Plugin.Abstractions;

namespace DigitalIntelligenceBridge.Plugin.Host;

public class PluginLoaderService
{
    public LoadedPlugin LoadPlugin(LoadedPlugin plugin)
    {
        return LoadPlugin(plugin, null);
    }

    public LoadedPlugin LoadPlugin(LoadedPlugin plugin, string? hostVersion)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(hostVersion) && !plugin.Manifest.IsCompatibleWith(hostVersion))
            {
                plugin.ErrorMessage = $"插件要求宿主版本 >= {plugin.Manifest.MinHostVersion}，当前为 {hostVersion}";
                return plugin;
            }

            var assemblyPath = Path.Combine(plugin.PluginDirectory, plugin.Manifest.EntryAssembly);
            if (!File.Exists(assemblyPath))
            {
                plugin.ErrorMessage = $"未找到插件入口程序集: {plugin.Manifest.EntryAssembly}";
                return plugin;
            }

            var assembly = LoadAssembly(plugin.PluginDirectory, assemblyPath);
            var entryType = assembly.GetType(plugin.Manifest.EntryType, throwOnError: false);
            if (entryType is null)
            {
                plugin.ErrorMessage = $"未找到插件入口类型: {plugin.Manifest.EntryType}";
                return plugin;
            }

            if (!typeof(IPluginModule).IsAssignableFrom(entryType))
            {
                plugin.ErrorMessage = $"插件入口类型未实现 IPluginModule: {plugin.Manifest.EntryType}";
                return plugin;
            }

            plugin.Module = (IPluginModule?)Activator.CreateInstance(entryType);
            if (plugin.Module is null)
            {
                plugin.ErrorMessage = $"无法创建插件实例: {plugin.Manifest.EntryType}";
            }
        }
        catch (Exception ex)
        {
            plugin.ErrorMessage = ex.Message;
        }

        return plugin;
    }

    protected virtual Assembly LoadAssembly(string pluginDirectory, string assemblyPath)
    {
        var loadedAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly =>
                !string.IsNullOrWhiteSpace(assembly.Location) &&
                string.Equals(assembly.Location, assemblyPath, StringComparison.OrdinalIgnoreCase));
        if (loadedAssembly is not null)
        {
            return loadedAssembly;
        }

        var loadContext = new PluginLoadContext(pluginDirectory);
        return loadContext.LoadFromAssemblyPath(assemblyPath);
    }
}
