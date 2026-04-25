using DigitalIntelligenceBridge.ViewModels;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class ResourceApplicationDialogViewModelTests
{
    [Fact]
    public void Confirm_ShouldFail_WhenReasonIsBlank()
    {
        var vm = new ResourceApplicationDialogViewModel("resource-1", "OCR 网关", "patient-registration", "HttpService");
        vm.ReasonInput = "   ";

        var result = vm.TryConfirm();

        Assert.False(result.IsConfirmed);
        Assert.Equal("申请理由不能为空，且长度需在 10 到 200 个字符之间。", vm.StatusMessage);
    }

    [Fact]
    public void Confirm_ShouldTrimReason_WhenReasonIsValid()
    {
        var vm = new ResourceApplicationDialogViewModel("resource-1", "OCR 网关", "patient-registration", "HttpService");
        vm.ReasonInput = "  需要为门诊登记插件接入 OCR 服务，用于窗口实时识别。  ";

        var result = vm.TryConfirm();

        Assert.True(result.IsConfirmed);
        Assert.Equal("需要为门诊登记插件接入 OCR 服务，用于窗口实时识别。", result.Reason);
        Assert.Equal("申请理由已确认。", vm.StatusMessage);
    }
}
