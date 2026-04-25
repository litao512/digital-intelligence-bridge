using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using DigitalIntelligenceBridge.Models;
using DigitalIntelligenceBridge.ViewModels;
using DigitalIntelligenceBridge.Views;

namespace DigitalIntelligenceBridge.Services;

public interface IResourceApplicationDialogService
{
    Task<ResourceApplicationDialogResult> ShowAsync(ResourceApplicationRequest request, CancellationToken cancellationToken = default);
}

public sealed class ResourceApplicationDialogService : IResourceApplicationDialogService
{
    public async Task<ResourceApplicationDialogResult> ShowAsync(ResourceApplicationRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var dialog = new ResourceApplicationDialog
            {
                DataContext = new ResourceApplicationDialogViewModel(
                    request.ResourceId,
                    request.ResourceName,
                    request.PluginCode,
                    request.ResourceType)
            };

            var owner = (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (owner is not null)
            {
                var result = await dialog.ShowDialog<ResourceApplicationDialogResult?>(owner);
                return result ?? new ResourceApplicationDialogResult(false, string.Empty);
            }

            dialog.Show();
            return new ResourceApplicationDialogResult(false, string.Empty);
        });
    }
}
