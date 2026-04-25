using DigitalIntelligenceBridge.Models;

namespace DigitalIntelligenceBridge.ViewModels;

public sealed class ResourceApplicationDialogViewModel : ViewModelBase
{
    private string _reasonInput = string.Empty;
    private string _statusMessage = "请说明用途、使用场景和涉及插件。";

    public ResourceApplicationDialogViewModel(string resourceId, string resourceName, string pluginCode, string resourceType)
    {
        ResourceId = resourceId;
        ResourceName = resourceName;
        PluginCode = pluginCode;
        ResourceType = resourceType;
    }

    public string ResourceId { get; }
    public string ResourceName { get; }
    public string PluginCode { get; }
    public string ResourceType { get; }

    public string ReasonInput
    {
        get => _reasonInput;
        set => SetProperty(ref _reasonInput, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ResourceApplicationDialogResult TryConfirm()
    {
        var reason = ReasonInput.Trim();
        if (reason.Length is < 10 or > 200)
        {
            StatusMessage = "申请理由不能为空，且长度需在 10 到 200 个字符之间。";
            return new ResourceApplicationDialogResult(false, string.Empty);
        }

        StatusMessage = "申请理由已确认。";
        return new ResourceApplicationDialogResult(true, reason);
    }
}
