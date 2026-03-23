using Avalonia.Controls;

namespace DigitalIntelligenceBridge.Plugin.Abstractions;

public interface IPluginModule
{
    void Initialize(IPluginHostContext context);

    PluginManifest GetManifest();

    IReadOnlyList<PluginMenuItem> CreateMenuItems();

    Control CreateContent(string menuId);
}
