using System;
using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Services;
using Microsoft.Extensions.Options;

namespace DigitalIntelligenceBridge.ViewModels;

public sealed class SiteRegistrationDialogViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private readonly ILoggerService<SiteRegistrationDialogViewModel> _logger;
    private string _siteNameInput = string.Empty;
    private string _siteRemarkInput = string.Empty;
    private string _statusMessage = "修改后将用于后续站点注册与心跳。";

    public SiteRegistrationDialogViewModel()
        : this(
            Options.Create(new AppSettings()),
            new NullLoggerService())
    {
    }

    public SiteRegistrationDialogViewModel(
        IOptions<AppSettings> settings,
        ILoggerService<SiteRegistrationDialogViewModel> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        SiteNameInput = _settings.ReleaseCenter.SiteName;
        SiteRemarkInput = _settings.ReleaseCenter.SiteRemark;
    }

    public string SiteNameInput
    {
        get => _siteNameInput;
        set => SetProperty(ref _siteNameInput, value);
    }

    public string SiteRemarkInput
    {
        get => _siteRemarkInput;
        set => SetProperty(ref _siteRemarkInput, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool TrySave()
    {
        try
        {
            var result = SiteProfileService.Save(
                _settings,
                SiteNameInput,
                SiteRemarkInput);
            SiteNameInput = result.SiteName;
            SiteRemarkInput = result.SiteRemark;
            StatusMessage = result.Status;
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            _logger.LogWarning("保存站点资料失败: {Message}", ex.Message);
            return false;
        }
    }

    private sealed class NullLoggerService : ILoggerService<SiteRegistrationDialogViewModel>
    {
        public void LogCritical(string message, params object[] args) { }
        public void LogDebug(string message, params object[] args) { }
        public void LogError(string message, params object[] args) { }
        public void LogError(Exception exception, string message, params object[] args) { }
        public void LogInformation(string message, params object[] args) { }
        public void LogWarning(string message, params object[] args) { }
    }
}
