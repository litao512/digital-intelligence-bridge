using System;
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
    public PluginHostViewModel(
        Control content,
        string? errorMessage = null,
        string? pluginName = null,
        string? pluginId = null,
        string? pluginVersion = null)
    {
        Content = content;
        ErrorMessage = errorMessage ?? string.Empty;
        PluginName = pluginName ?? string.Empty;
        PluginId = pluginId ?? string.Empty;
        PluginVersion = pluginVersion ?? string.Empty;
    }

    public Control Content { get; }

    public string ErrorMessage { get; }

    public string PluginName { get; }

    public string PluginId { get; }

    public string PluginVersion { get; }

    public string PluginVersionText => FormatVersionText(PluginVersion);

    public bool HasPluginMetadata => !string.IsNullOrWhiteSpace(PluginName) || !string.IsNullOrWhiteSpace(PluginId);

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

    private static string FormatVersionText(string version)
    {
        var normalized = version.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? normalized : $"v{normalized}";
    }
}
