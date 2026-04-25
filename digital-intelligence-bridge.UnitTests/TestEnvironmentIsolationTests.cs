using System;
using System.IO;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class TestEnvironmentIsolationTests
{
    [Fact]
    public void UnitTests_ShouldUseIsolatedConfigRootByDefault()
    {
        var configRoot = Environment.GetEnvironmentVariable("DIB_CONFIG_ROOT");

        Assert.False(string.IsNullOrWhiteSpace(configRoot));
        Assert.StartsWith(Path.GetTempPath(), configRoot!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DibClient", configRoot!, StringComparison.OrdinalIgnoreCase);
    }
}
