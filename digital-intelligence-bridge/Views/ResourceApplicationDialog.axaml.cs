using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DigitalIntelligenceBridge.Models;
using DigitalIntelligenceBridge.ViewModels;

namespace DigitalIntelligenceBridge.Views;

public partial class ResourceApplicationDialog : Window
{
    public ResourceApplicationDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void ConfirmButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ResourceApplicationDialogViewModel vm)
        {
            var result = vm.TryConfirm();
            if (result.IsConfirmed)
            {
                Close(result);
            }
        }
    }

    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(new ResourceApplicationDialogResult(false, string.Empty));
    }
}
