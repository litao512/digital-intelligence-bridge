using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AvaloniaDemo.ViewModels;

namespace AvaloniaDemo.Views;

public partial class MainWindow : Window
{
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
}
