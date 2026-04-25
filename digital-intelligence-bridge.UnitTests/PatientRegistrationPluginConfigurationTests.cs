using PatientRegistration.Plugin.Configuration;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public sealed class PatientRegistrationPluginConfigurationTests
{
    [Fact]
    public void PluginSettings_ShouldNotExposeSensitiveConnectionSections()
    {
        Assert.Null(typeof(PluginSettings).GetProperty("Postgres"));
    }

    [Fact]
    public void Load_ShouldReadNonSensitivePluginSettings_WhenFileExists()
    {
        var pluginDirectory = CreateTempPluginDirectory("""
        {
          "DevelopmentMode": {
            "Enabled": true
          },
          "Registration": {
            "EnableDirectPrint": false,
            "RequireEmptyDiagnosticConfirmation": false
          }
        }
        """);

        try
        {
            var settings = PluginConfigurationLoader.Load(pluginDirectory);

            Assert.True(settings.DevelopmentMode.Enabled);
            Assert.False(settings.Registration.EnableDirectPrint);
            Assert.False(settings.Registration.RequireEmptyDiagnosticConfirmation);
        }
        finally
        {
            Directory.Delete(pluginDirectory, true);
        }
    }

    [Fact]
    public void Load_ShouldIgnoreEnvironmentVariables()
    {
        var pluginDirectory = CreateTempPluginDirectory("""
        {
          "DevelopmentMode": {
            "Enabled": true
          }
        }
        """);

        const string envName = "PATIENT_REGISTRATION__REGISTRATION__ENABLEDIRECTPRINT";
        var previousValue = Environment.GetEnvironmentVariable(envName);

        try
        {
            Environment.SetEnvironmentVariable(envName, "false");

            var settings = PluginConfigurationLoader.Load(pluginDirectory);

            Assert.True(settings.Registration.EnableDirectPrint);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envName, previousValue);
            Directory.Delete(pluginDirectory, true);
        }
    }

    [Fact]
    public void PluginSettingsTemplate_ShouldNotContainSensitiveConnectionSections()
    {
        var fullPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "plugins-src",
            "PatientRegistration.Plugin",
            "plugin.settings.json"));

        var json = File.ReadAllText(fullPath);

        Assert.DoesNotContain("\"Postgres\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ConnectionString\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void DevelopmentTemplate_ShouldExist_AndUsePlaceholderValues()
    {
        var fullPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "plugins-src",
            "PatientRegistration.Plugin",
            "plugin.development.json.example"));

        Assert.True(File.Exists(fullPath));

        var json = File.ReadAllText(fullPath);

        Assert.Contains("\"RegistrationDbConnectionString\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Password=dev", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Host=127.0.0.1", json, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateTempPluginDirectory(string pluginSettingsJson)
    {
        var pluginDirectory = Path.Combine(Path.GetTempPath(), $"patient-registration-plugin-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(pluginDirectory);
        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.settings.json"), pluginSettingsJson);
        return pluginDirectory;
    }
}
