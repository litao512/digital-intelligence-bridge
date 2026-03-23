using MedicalDrugImport.Plugin.Configuration;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class MedicalDrugImportPluginConfigurationTests
{
    [Fact]
    public void Load_ShouldReadPluginSettingsJson_WhenFileExists()
    {
        var pluginDirectory = CreateTempPluginDirectory("""
        {
          "Postgres": {
            "ConnectionString": "Host=pg-host;Database=drugdb;"
          },
          "SqlServer": {
            "ConnectionString": "Server=sql-host;Database=ChisDict;",
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

            Assert.Equal("Host=pg-host;Database=drugdb;", settings.Postgres.ConnectionString);
            Assert.Equal("Server=sql-host;Database=ChisDict;", settings.SqlServer.ConnectionString);
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
    public void Load_ShouldAllowEnvironmentVariablesToOverridePluginSettings()
    {
        var pluginDirectory = CreateTempPluginDirectory("""
        {
          "Postgres": {
            "ConnectionString": "Host=pg-host;Database=drugdb;"
          },
          "Import": {
            "BatchSize": 1000,
            "MaxSyncRowsPerRun": 50
          }
        }
        """);

        const string batchSizeEnv = "MEDICAL_DRUG_IMPORT__IMPORT__BATCHSIZE";
        const string maxSyncRowsEnv = "MEDICAL_DRUG_IMPORT__IMPORT__MAXSYNCROWSPERRUN";
        const string postgresEnv = "MEDICAL_DRUG_IMPORT__POSTGRES__CONNECTIONSTRING";
        const string enableWritesEnv = "MEDICAL_DRUG_IMPORT__SQLSERVER__ENABLEWRITES";
        var previousBatchSize = Environment.GetEnvironmentVariable(batchSizeEnv);
        var previousMaxSyncRows = Environment.GetEnvironmentVariable(maxSyncRowsEnv);
        var previousPostgres = Environment.GetEnvironmentVariable(postgresEnv);
        var previousEnableWrites = Environment.GetEnvironmentVariable(enableWritesEnv);

        try
        {
            Environment.SetEnvironmentVariable(batchSizeEnv, "2500");
            Environment.SetEnvironmentVariable(maxSyncRowsEnv, "25");
            Environment.SetEnvironmentVariable(postgresEnv, "Host=env-host;Database=envdb;");
            Environment.SetEnvironmentVariable(enableWritesEnv, "true");

            var settings = PluginConfigurationLoader.Load(pluginDirectory);

            Assert.Equal(2500, settings.Import.BatchSize);
            Assert.Equal(25, settings.Import.MaxSyncRowsPerRun);
            Assert.Equal("Host=env-host;Database=envdb;", settings.Postgres.ConnectionString);
            Assert.True(settings.SqlServer.EnableWrites);
        }
        finally
        {
            Environment.SetEnvironmentVariable(batchSizeEnv, previousBatchSize);
            Environment.SetEnvironmentVariable(maxSyncRowsEnv, previousMaxSyncRows);
            Environment.SetEnvironmentVariable(postgresEnv, previousPostgres);
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
            Assert.NotNull(settings.Postgres);
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

    private static string CreateTempPluginDirectory(string pluginSettingsJson)
    {
        var pluginDirectory = Path.Combine(Path.GetTempPath(), $"plugin-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(pluginDirectory);
        File.WriteAllText(Path.Combine(pluginDirectory, "plugin.settings.json"), pluginSettingsJson);
        return pluginDirectory;
    }
}
