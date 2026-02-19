using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DigitalIntelligenceBridge.ViewModels;

namespace DigitalIntelligenceBridge.Views;

/// <summary>
/// 设置页面视图
/// </summary>
public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
