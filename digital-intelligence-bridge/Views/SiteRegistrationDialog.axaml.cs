using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DigitalIntelligenceBridge.ViewModels;

namespace DigitalIntelligenceBridge.Views;

public partial class SiteRegistrationDialog : Window
{
    public SiteRegistrationDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void SaveButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is SiteRegistrationDialogViewModel vm && vm.TrySave())
        {
            Close(true);
        }
    }

    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }
}
