using MedicalDrugImport.Plugin.Configuration;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class MedicalDrugImportPluginConfigurationTests
{
    [Fact]
    public void PluginSettings_ShouldNotExposeSensitiveConnectionSections()
    {
        Assert.Null(typeof(PluginSettings).GetProperty("Postgres"));
        Assert.Null(typeof(SqlServerSettings).GetProperty("ConnectionString"));
    }

    [Fact]
    public void Load_ShouldReadNonSensitivePluginSettings_WhenFileExists()
    {
        var pluginDirectory = CreateTempPluginDirectory("""
        {
          "DevelopmentMode": {
            "Enabled": true
          },
          "Excel": {
            "RequiredSheets": [ "总表（270419）" ]
          },
          "SqlServer": {
            "EnableWrites": true
          },
          "Import": {
            "BatchSize": 1200,
            "MaxSyncRowsPerRun": 80,
            "AllowUnsafeFullSync": true
          }
        }
        """);

        try
        {
            var settings = PluginConfigurationLoader.Load(pluginDirectory);

            Assert.True(settings.DevelopmentMode.Enabled);
            Assert.Single(settings.Excel.RequiredSheets);
            Assert.True(settings.SqlServer.EnableWrites);
            Assert.Equal(1200, settings.Import.BatchSize);
            Assert.Equal(80, settings.Import.MaxSyncRowsPerRun);
            Assert.True(settings.Import.AllowUnsafeFullSync);
        }
        finally
        {
            Directory.Delete(pluginDirectory, true);
        }
    }

    [Fact]
    public void Load_ShouldIgnoreEnvironmentVariables_EvenWhenDevelopmentModeEnabled()
    {
        var pluginDirectory = CreateTempPluginDirectory("""
        {
          "DevelopmentMode": {
            "Enabled": true
          },
          "Import": {
            "BatchSize": 1000,
            "MaxSyncRowsPerRun": 50
          }
        }
        """);

        const string batchSizeEnv = "MEDICAL_DRUG_IMPORT__IMPORT__BATCHSIZE";
        const string enableWritesEnv = "MEDICAL_DRUG_IMPORT__SQLSERVER__ENABLEWRITES";
        var previousBatchSize = Environment.GetEnvironmentVariable(batchSizeEnv);
        var previousEnableWrites = Environment.GetEnvironmentVariable(enableWritesEnv);

        try
        {
            Environment.SetEnvironmentVariable(batchSizeEnv, "2500");
            Environment.SetEnvironmentVariable(enableWritesEnv, "true");

            var settings = PluginConfigurationLoader.Load(pluginDirectory);

            Assert.Equal(1000, settings.Import.BatchSize);
            Assert.False(settings.SqlServer.EnableWrites);
        }
        finally
        {
            Environment.SetEnvironmentVariable(batchSizeEnv, previousBatchSize);
            Environment.SetEnvironmentVariable(enableWritesEnv, previousEnableWrites);
            Directory.Delete(pluginDirectory, true);
        }
    }

    [Fact]
    public void Load_ShouldReturnDefaultSettings_WhenFileDoesNotExist()
    {
        var pluginDirectory = Path.Combine(Path.GetTempPath(), $"plugin-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(pluginDirectory);

        try
        {
            var settings = PluginConfigurationLoader.Load(pluginDirectory);

            Assert.NotNull(settings);
            Assert.NotNull(settings.Excel);
            Assert.NotNull(settings.SqlServer);
            Assert.NotNull(settings.Import);
            Assert.False(settings.SqlServer.EnableWrites);
            Assert.True(settings.Import.BatchSize > 0);
            Assert.True(settings.Import.MaxSyncRowsPerRun > 0);
            Assert.False(settings.Import.AllowUnsafeFullSync);
        }
        finally
        {
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
            "MedicalDrugImport.Plugin",
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
            "MedicalDrugImport.Plugin",
            "plugin.development.json.example"));

        Assert.True(File.Exists(fullPath));

        var json = File.ReadAllText(fullPath);

        Assert.Contains("\"BusinessDbConnectionString\"", json, StringComparison.Ordinal);
        Assert.Contains("\"SyncTargetConnectionString\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Password=dev", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("User Id=sa", json, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateTempPluginDirectory(string pluginSettingsJson)
    {
        var pluginDirectory = Path.Combine(Path.GetTempPath(), $"plugin-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(pluginDirectory);
        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.settings.json"), pluginSettingsJson);
        return pluginDirectory;
    }
}
