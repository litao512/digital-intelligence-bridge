using Avalonia.Controls;
using DigitalIntelligenceBridge.Plugin.Abstractions;
using DigitalIntelligenceBridge.Plugin.Host;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class PluginLoaderServiceTests
{
    [Fact]
    public void LoadPlugin_ShouldCreatePluginModule_WhenEntryTypeImplementsContract()
    {
        var service = new PluginLoaderService();
        var assemblyPath = typeof(SamplePlugin).Assembly.Location;
        var plugin = new LoadedPlugin
        {
            PluginDirectory = Path.GetDirectoryName(assemblyPath) ?? string.Empty,
            ManifestPath = Path.Combine(Path.GetDirectoryName(assemblyPath) ?? string.Empty, "plugin.json"),
            Manifest = new PluginManifest
            {
                Id = "sample",
                Name = "样例插件",
                Version = "0.1.0",
                EntryAssembly = Path.GetFileName(assemblyPath),
                EntryType = typeof(SamplePlugin).FullName ?? string.Empty,
                MinHostVersion = "1.0.0"
            }
        };

        var result = service.LoadPlugin(plugin);

        Assert.NotNull(result.Module);
        Assert.Empty(result.ErrorMessage);
        Assert.IsType<SamplePlugin>(result.Module);
    }

    [Fact]
    public void LoadPlugin_ShouldReturnError_WhenEntryTypeDoesNotImplementContract()
    {
        var service = new PluginLoaderService();
        var assemblyPath = typeof(NotAPlugin).Assembly.Location;
        var plugin = new LoadedPlugin
        {
            PluginDirectory = Path.GetDirectoryName(assemblyPath) ?? string.Empty,
            ManifestPath = Path.Combine(Path.GetDirectoryName(assemblyPath) ?? string.Empty, "plugin.json"),
            Manifest = new PluginManifest
            {
                Id = "bad-plugin",
                Name = "坏插件",
                Version = "0.1.0",
                EntryAssembly = Path.GetFileName(assemblyPath),
                EntryType = typeof(NotAPlugin).FullName ?? string.Empty,
                MinHostVersion = "1.0.0"
            }
        };

        var result = service.LoadPlugin(plugin);

        Assert.Null(result.Module);
        Assert.Contains("IPluginModule", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    public sealed class SamplePlugin : IPluginModule
    {
        public void Initialize(IPluginHostContext context)
        {
        }

        public Control CreateContent(string menuId)
        {
            return new TextBlock { Text = menuId };
        }

        public IReadOnlyList<PluginMenuItem> CreateMenuItems()
        {
            return [new PluginMenuItem { Id = "sample-menu", Name = "样例菜单", Order = 10 }];
        }

        public PluginManifest GetManifest()
        {
            return new PluginManifest { Id = "sample", Name = "样例插件", Version = "0.1.0" };
        }
    }

    public sealed class NotAPlugin
    {
    }
}
