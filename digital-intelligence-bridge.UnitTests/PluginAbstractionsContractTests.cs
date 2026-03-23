using System.Reflection;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class PluginAbstractionsContractTests
{
    [Fact]
    public void PluginAbstractions_ShouldExposeMinimumExternalPluginContract()
    {
        var assembly = Assembly.Load("DigitalIntelligenceBridge.Plugin.Abstractions");

        var pluginModuleType = assembly.GetType("DigitalIntelligenceBridge.Plugin.Abstractions.IPluginModule");
        var hostContextType = assembly.GetType("DigitalIntelligenceBridge.Plugin.Abstractions.IPluginHostContext");
        var manifestType = assembly.GetType("DigitalIntelligenceBridge.Plugin.Abstractions.PluginManifest");
        var menuItemType = assembly.GetType("DigitalIntelligenceBridge.Plugin.Abstractions.PluginMenuItem");

        Assert.NotNull(pluginModuleType);
        Assert.NotNull(hostContextType);
        Assert.NotNull(manifestType);
        Assert.NotNull(menuItemType);

        Assert.NotNull(pluginModuleType!.GetMethod("Initialize"));
        Assert.NotNull(pluginModuleType.GetMethod("GetManifest"));
        Assert.NotNull(pluginModuleType.GetMethod("CreateMenuItems"));
        Assert.NotNull(pluginModuleType.GetMethod("CreateContent"));

        AssertProperty(manifestType!, "Id");
        AssertProperty(manifestType, "Name");
        AssertProperty(manifestType, "Version");
        AssertProperty(manifestType, "EntryAssembly");
        AssertProperty(manifestType, "EntryType");
        AssertProperty(manifestType, "MinHostVersion");

        AssertProperty(menuItemType!, "Id");
        AssertProperty(menuItemType, "Name");
        AssertProperty(menuItemType, "Icon");
        AssertProperty(menuItemType, "Order");
    }

    private static void AssertProperty(Type type, string propertyName)
    {
        Assert.NotNull(type.GetProperty(propertyName));
    }
}
