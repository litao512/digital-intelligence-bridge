using Avalonia.Controls;
using DigitalIntelligenceBridge.Plugin.Abstractions;
using PatientRegistration.Plugin;
using PatientRegistration.Plugin.ViewModels;
using PatientRegistration.Plugin.Views;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public sealed class PatientRegistrationPluginViewTests
{
    [Fact]
    public void CreateContent_ShouldReturnHomeView_ForHomeMenu()
    {
        var plugin = new PatientRegistrationPlugin();
        plugin.Initialize(new StubPluginHostContext());

        var content = plugin.CreateContent("patient-registration.home");

        var view = Assert.IsType<PatientRegistrationHomeView>(content);
        Assert.IsType<PatientRegistrationViewModel>(view.DataContext);
    }

    [Fact]
    public void CreateContent_ShouldLogWarningStyleMessage_WhenDevelopmentModeEnabled()
    {
        var pluginDirectory = Path.Combine(Path.GetTempPath(), $"patient-registration-plugin-{Guid.NewGuid():N}");
        Directory.CreateDirectory(pluginDirectory);
        File.WriteAllText(
            Path.Combine(pluginDirectory, "plugin.settings.json"),
            """
            {
              "DevelopmentMode": {
                "Enabled": true
              }
            }
            """);

        try
        {
            var hostContext = new StubPluginHostContext(pluginDirectory);
            var plugin = new PatientRegistrationPlugin();
            plugin.Initialize(hostContext);

            _ = plugin.CreateContent("patient-registration.home");

            Assert.Contains(hostContext.Logs, message => message.Contains("开发模式", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(pluginDirectory, true);
        }
    }

    private sealed class StubPluginHostContext : IPluginHostContext
    {
        public StubPluginHostContext(string pluginDirectory = "plugins/PatientRegistration")
        {
            PluginDirectory = pluginDirectory;
        }

        public List<string> Logs { get; } = [];

        public string HostVersion => "1.0.0";

        public string PluginDirectory { get; }

        public void LogInformation(string message)
        {
            Logs.Add(message);
        }

        public IReadOnlyList<AuthorizedRuntimeResource> GetAuthorizedResources() => [];

        public bool TryGetResource(string usageKey, out AuthorizedRuntimeResource? resource)
        {
            resource = null;
            return false;
        }
    }
}
