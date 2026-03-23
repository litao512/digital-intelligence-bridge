using DigitalIntelligenceBridge.Plugin.Abstractions;
using DigitalIntelligenceBridge.Plugin.Host;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class PluginHostContextTests
{
    [Fact]
    public void PluginHostContext_ShouldExposeOnlyMinimalHostSurface()
    {
        var sink = new List<string>();
        var context = new PluginHostContext("1.0.0", @"C:\plugins\medical-drug-import", sink.Add);

        Assert.Equal("1.0.0", context.HostVersion);
        Assert.Equal(@"C:\plugins\medical-drug-import", context.PluginDirectory);

        context.LogInformation("plugin loaded");

        Assert.Single(sink);
        Assert.Equal("plugin loaded", sink[0]);
        Assert.DoesNotContain(typeof(IPluginHostContext).GetMembers(), member => member.Name.Contains("Container", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(IPluginHostContext).GetMembers(), member => member.Name.Contains("MainWindow", StringComparison.OrdinalIgnoreCase));
    }
}
