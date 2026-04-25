using Avalonia.Controls;
using DigitalIntelligenceBridge.ViewModels;
using DigitalIntelligenceBridge.Views;
using Microsoft.Extensions.Options;

namespace DigitalIntelligenceBridge.Services;

public interface ISiteRegistrationDialogService
{
    void Show(Window? owner);
}

public sealed class SiteRegistrationDialogService : ISiteRegistrationDialogService
{
    private readonly IOptions<Configuration.AppSettings> _settings;
    private readonly ILoggerService<SiteRegistrationDialogViewModel> _logger;
    private SiteRegistrationDialog? _dialog;

    public SiteRegistrationDialogService(
        IOptions<Configuration.AppSettings> settings,
        ILoggerService<SiteRegistrationDialogViewModel> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public void Show(Window? owner)
    {
        if (_dialog is { } existing)
        {
            existing.WindowState = WindowState.Normal;
            existing.Activate();
            return;
        }

        var dialog = new SiteRegistrationDialog
        {
            DataContext = new SiteRegistrationDialogViewModel(_settings, _logger)
        };
        dialog.Closed += (_, _) => _dialog = null;
        _dialog = dialog;

        if (owner is not null)
        {
            _ = dialog.ShowDialog(owner);
            return;
        }

        dialog.Show();
    }
}
