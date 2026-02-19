using System;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.VisualTree;
using DigitalIntelligenceBridge.ViewModels;

namespace DigitalIntelligenceBridge.Views;

public partial class MainWindow : Window
{
    private static readonly Animation TabFadeAnimation = new()
    {
        Duration = TimeSpan.FromMilliseconds(180),
        Easing = new CubicEaseOut(),
        Children =
        {
            new KeyFrame
            {
                Cue = new Cue(0d),
                Setters = { new Setter(OpacityProperty, 0.88d) }
            },
            new KeyFrame
            {
                Cue = new Cue(1d),
                Setters = { new Setter(OpacityProperty, 1d) }
            }
        }
    };

    public MainWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// 设置按钮点击
    /// </summary>
    private void SettingsButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.ShowSettingsViewCommand.Execute();
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // 逻辑由 TrayService 处理
        base.OnClosing(e);
    }

    private async void MainTabControl_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not TabControl tabControl)
        {
            return;
        }

        var presenter = tabControl.GetVisualDescendants()
            .OfType<ContentPresenter>()
            .FirstOrDefault();

        if (presenter != null)
        {
            await TabFadeAnimation.RunAsync(presenter);
        }
    }
}
