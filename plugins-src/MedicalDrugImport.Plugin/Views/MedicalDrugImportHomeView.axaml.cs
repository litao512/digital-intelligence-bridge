using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using MedicalDrugImport.Plugin.ViewModels;

namespace MedicalDrugImport.Plugin.Views;

public partial class MedicalDrugImportHomeView : UserControl
{
    public MedicalDrugImportHomeView()
        : this(new DrugImportPluginViewModel())
    {
    }

    public MedicalDrugImportHomeView(DrugImportPluginViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private DrugImportPluginViewModel? ViewModel => DataContext as DrugImportPluginViewModel;

    private async void OnValidateClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.ValidateAsync();
    }

    private async void OnImportClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.ImportAsync();
    }

    private async void OnSyncClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.SyncAsync();
    }

    private async void OnPreviewSyncClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.PreviewSyncAsync();
    }

    private async void OnRetrySyncClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.RetrySyncAsync();
    }
}
