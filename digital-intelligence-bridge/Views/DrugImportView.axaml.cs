using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DigitalIntelligenceBridge.Views;

/// <summary>
/// 医保药品导入工具视图
/// </summary>
public partial class DrugImportView : UserControl
{
    public DrugImportView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
