using System.Text.Json;
using DigitalIntelligenceBridge.Plugin.Abstractions;
using PatientRegistration.Plugin.Configuration;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public sealed class PatientRegistrationRuntimeResourceSettingsTests
{
    [Fact]
    public void ResolveRegistrationDbConnectionString_ShouldPreferHostResource_WhenAvailable()
    {
        var settings = new PluginSettings();
        var hostContext = new StubPluginHostContext(
            new AuthorizedRuntimeResource
            {
                ResourceId = "resource-001",
                ResourceCode = "registration-db",
                ResourceName = "就诊登记业务库",
                ResourceType = "PostgreSQL",
                UsageKey = "registration-db",
                BindingScope = "PluginAtSite",
                Version = 1,
                Capabilities = ["read", "write"],
                Configuration = JsonDocument.Parse("""{"host":"runtime-db","port":5433,"database":"registration","username":"dib","password":"secret","searchPath":"public"}""").RootElement.Clone()
            });

        var connectionString = RuntimeResourceResolver.ResolveRegistrationDbConnectionString(@"C:\plugins\patient-registration", settings, hostContext);

        Assert.Contains("Host=runtime-db", connectionString, StringComparison.Ordinal);
        Assert.Contains("Port=5433", connectionString, StringComparison.Ordinal);
        Assert.Contains("Database=registration", connectionString, StringComparison.Ordinal);
        Assert.Contains("Username=dib", connectionString, StringComparison.Ordinal);
        Assert.Contains("Password=secret", connectionString, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveRegistrationDbConnectionString_ShouldUseDevelopmentSettings_WhenDevelopmentModeEnabled()
    {
        var pluginDirectory = Path.Combine(Path.GetTempPath(), $"patient-registration-dev-{Guid.NewGuid():N}");
        Directory.CreateDirectory(pluginDirectory);
        File.WriteAllText(
            Path.Combine(pluginDirectory, "plugin.development.json"),
            """
            {
              "RegistrationDbConnectionString": "Host=local-db;Database=localdb;"
            }
            """);

        var settings = new PluginSettings
        {
            DevelopmentMode = new DevelopmentModeSettings
            {
                Enabled = true
            }
        };

        try
        {
            var connectionString = RuntimeResourceResolver.ResolveRegistrationDbConnectionString(pluginDirectory, settings, new StubPluginHostContext());
            Assert.Equal("Host=local-db;Database=localdb;", connectionString);
        }
        finally
        {
            Directory.Delete(pluginDirectory, true);
        }
    }

    private sealed class StubPluginHostContext(params AuthorizedRuntimeResource[] resources) : IPluginHostContext
    {
        private readonly IReadOnlyList<AuthorizedRuntimeResource> _resources = resources;

        public string HostVersion => "1.0.0";

        public string PluginDirectory => @"C:\plugins\patient-registration";

        public void LogInformation(string message)
        {
        }

        public IReadOnlyList<AuthorizedRuntimeResource> GetAuthorizedResources()
        {
            return _resources;
        }

        public bool TryGetResource(string usageKey, out AuthorizedRuntimeResource? resource)
        {
            resource = _resources.FirstOrDefault(item => string.Equals(item.UsageKey, usageKey, StringComparison.Ordinal));
            return resource is not null;
        }
    }
}
