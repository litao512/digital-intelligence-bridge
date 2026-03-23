using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DigitalIntelligenceBridge.Views;

/// <summary>
/// 外部插件页面统一承载视图。
/// </summary>
public partial class PluginHostView : UserControl
{
    public PluginHostView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
