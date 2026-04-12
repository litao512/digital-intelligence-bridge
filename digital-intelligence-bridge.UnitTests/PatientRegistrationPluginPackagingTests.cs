using System;
using System.IO;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class PatientRegistrationPluginPackagingTests
{
    [Fact]
    public void PluginOutput_ShouldContainQRCoderDependency()
    {
        var pluginOutput = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "plugins-src",
            "PatientRegistration.Plugin",
            "bin",
            "Debug",
            "net10.0"));

        var qrCoderPath = Path.Combine(pluginOutput, "QRCoder.dll");

        Assert.True(
            File.Exists(qrCoderPath),
            $"插件输出目录缺少 QRCoder.dll：{qrCoderPath}");
    }
}
