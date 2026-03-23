using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace DigitalIntelligenceBridge.ViewModels;

/// <summary>
/// 外部插件页面统一承载视图模型。
/// </summary>
public class PluginHostViewModel : ViewModelBase
{
    public PluginHostViewModel(Control content, string? errorMessage = null)
    {
        Content = content;
        ErrorMessage = errorMessage ?? string.Empty;
    }

    public Control Content { get; }

    public string ErrorMessage { get; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public static PluginHostViewModel CreatePlaceholder(string pluginName)
    {
        return new PluginHostViewModel(
            new TextBlock
            {
                Text = $"{pluginName} 已接入插件宿主，后续将在此承载真实插件页面。",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14
            });
    }

    public static PluginHostViewModel CreateError(string errorMessage)
    {
        var panel = new StackPanel
        {
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(new TextBlock
        {
            Text = "插件页面加载失败",
            FontSize = 22,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        panel.Children.Add(new TextBlock
        {
            Text = errorMessage,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        panel.Children.Add(new TextBlock
        {
            Text = "该插件异常不影响其他模块继续使用。",
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            Opacity = 0.75
        });

        return new PluginHostViewModel(
            new Border
            {
                Padding = new Thickness(24),
                Child = panel
            },
            errorMessage);
    }
}
