using System.Diagnostics;
using PatientRegistration.Plugin.Models;
using PatientRegistration.Plugin.Services;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class PatientRegistrationQrPrintServiceTests
{
    [Fact]
    public async Task PrintAsync_ShouldLaunchHtmlThroughShellStartCommand_WhenPrintingHtml()
    {
        ProcessStartInfo? capturedStartInfo = null;
        var service = new QrPrintService(
            processStarter: startInfo =>
            {
                capturedStartInfo = startInfo;
                return null;
            });

        await service.PrintAsync(new PatientRegistrationPrintPayload
        {
            RegistrationId = Guid.Parse("200b3565-b062-4ad0-b9e8-65efe0751cb5"),
            PatientName = "测试患者",
            IdType = "id_card",
            IdNumberMasked = "**************1234",
            QrCodeContent = "REG-TEST-001"
        });

        Assert.NotNull(capturedStartInfo);
        Assert.NotEqual(".html", Path.GetExtension(capturedStartInfo.FileName));
        Assert.Contains("start", capturedStartInfo.Arguments, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("registration-200b3565b0624ad0b9e865efe0751cb5.html", capturedStartInfo.Arguments, StringComparison.Ordinal);
        Assert.True(capturedStartInfo.UseShellExecute);
        Assert.True(File.Exists(capturedStartInfo.Arguments.Split('"')[3]));
    }
}
