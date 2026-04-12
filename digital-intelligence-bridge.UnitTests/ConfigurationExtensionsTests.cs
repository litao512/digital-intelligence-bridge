using System;
using System.IO;
using DigitalIntelligenceBridge.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class ConfigurationExtensionsTests
{
    [Fact]
    public void AddAppConfiguration_ShouldBindProgramAndUserConfig_WhenFilesPresent()
    {
        var originalSupabaseUrl = Environment.GetEnvironmentVariable("Supabase__Url");
        var originalSupabaseAnonKey = Environment.GetEnvironmentVariable("Supabase__AnonKey");
        var originalSupabaseSchema = Environment.GetEnvironmentVariable("Supabase__Schema");
        var originalMssqlServer = Environment.GetEnvironmentVariable("MSSQL_DB_SERVER");
        var originalMssqlPort = Environment.GetEnvironmentVariable("MSSQL_DB_PORT");
        var originalMssqlName = Environment.GetEnvironmentVariable("MSSQL_DB_NAME");
        var originalMssqlUser = Environment.GetEnvironmentVariable("MSSQL_DB_USER");
        var originalMssqlPassword = Environment.GetEnvironmentVariable("MSSQL_DB_PASSWORD");
        var originalMssqlEncrypt = Environment.GetEnvironmentVariable("MSSQL_DB_ENCRYPT");
        var originalMssqlTrust = Environment.GetEnvironmentVariable("MSSQL_DB_TRUST_SERVER_CERTIFICATE");
        using var sandbox = new TestConfigSandbox();
        var tempRoot = sandbox.RootDirectory;

        try
        {
            Environment.SetEnvironmentVariable("Supabase__Url", null);
            Environment.SetEnvironmentVariable("Supabase__AnonKey", null);
            Environment.SetEnvironmentVariable("Supabase__Schema", null);
            Environment.SetEnvironmentVariable("MSSQL_DB_SERVER", null);
            Environment.SetEnvironmentVariable("MSSQL_DB_PORT", null);
            Environment.SetEnvironmentVariable("MSSQL_DB_NAME", null);
            Environment.SetEnvironmentVariable("MSSQL_DB_USER", null);
            Environment.SetEnvironmentVariable("MSSQL_DB_PASSWORD", null);
            Environment.SetEnvironmentVariable("MSSQL_DB_ENCRYPT", null);
            Environment.SetEnvironmentVariable("MSSQL_DB_TRUST_SERVER_CERTIFICATE", null);

            var userConfigPath = ConfigurationExtensions.GetConfigFilePath();
            File.WriteAllText(
                userConfigPath,
                """
                {
                  "Supabase": { "Url": "https://user-config.test", "AnonKey": "user-anon", "Schema": "dib" },
                  "MedicalDrugImport": {
                    "Enabled": true,
                    "PostgresSchema": "etl",
                    "SqlServer": {
                      "Host": "sqlserver.local",
                      "Port": 1433,
                      "Database": "MedicalCatalog",
                      "Username": "sa",
                      "Password": "secret",
                      "Encrypt": true,
                      "TrustServerCertificate": true
                    }
                  },
                  "Logging": { "LogLevel": { "Default": "Information", "Microsoft": "Warning" }, "LogPath": "logs" }
                }
                """);

            var services = new ServiceCollection();
            services.AddAppConfiguration();
            using var provider = services.BuildServiceProvider();

            var settings = provider.GetRequiredService<IOptions<AppSettings>>().Value;
            Assert.Equal("https://user-config.test", settings.Supabase.Url);
            Assert.Equal("user-anon", settings.Supabase.AnonKey);
            Assert.Equal("dib", settings.Supabase.Schema);
            Assert.True(settings.MedicalDrugImport.Enabled);
            Assert.Equal("etl", settings.MedicalDrugImport.PostgresSchema);
            Assert.Equal("sqlserver.local", settings.MedicalDrugImport.SqlServer.Host);
            Assert.Equal(1433, settings.MedicalDrugImport.SqlServer.Port);
            Assert.Equal("MedicalCatalog", settings.MedicalDrugImport.SqlServer.Database);
            Assert.Equal("sa", settings.MedicalDrugImport.SqlServer.Username);
            Assert.Equal("secret", settings.MedicalDrugImport.SqlServer.Password);
            Assert.True(settings.MedicalDrugImport.SqlServer.Encrypt);
            Assert.True(settings.MedicalDrugImport.SqlServer.TrustServerCertificate);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Supabase__Url", originalSupabaseUrl);
            Environment.SetEnvironmentVariable("Supabase__AnonKey", originalSupabaseAnonKey);
            Environment.SetEnvironmentVariable("Supabase__Schema", originalSupabaseSchema);
            Environment.SetEnvironmentVariable("MSSQL_DB_SERVER", originalMssqlServer);
            Environment.SetEnvironmentVariable("MSSQL_DB_PORT", originalMssqlPort);
            Environment.SetEnvironmentVariable("MSSQL_DB_NAME", originalMssqlName);
            Environment.SetEnvironmentVariable("MSSQL_DB_USER", originalMssqlUser);
            Environment.SetEnvironmentVariable("MSSQL_DB_PASSWORD", originalMssqlPassword);
            Environment.SetEnvironmentVariable("MSSQL_DB_ENCRYPT", originalMssqlEncrypt);
            Environment.SetEnvironmentVariable("MSSQL_DB_TRUST_SERVER_CERTIFICATE", originalMssqlTrust);
        }
    }

    [Fact]
    public void AddAppConfiguration_ShouldCopyDefaultConfig_WhenUserConfigMissing()
    {
        var originalSupabaseUrl = Environment.GetEnvironmentVariable("Supabase__Url");
        var originalSupabaseAnonKey = Environment.GetEnvironmentVariable("Supabase__AnonKey");
        var originalSupabaseSchema = Environment.GetEnvironmentVariable("Supabase__Schema");
        var originalMssqlServer = Environment.GetEnvironmentVariable("MSSQL_DB_SERVER");
        var originalMssqlPort = Environment.GetEnvironmentVariable("MSSQL_DB_PORT");
        var originalMssqlName = Environment.GetEnvironmentVariable("MSSQL_DB_NAME");
        var originalMssqlUser = Environment.GetEnvironmentVariable("MSSQL_DB_USER");
        var originalMssqlPassword = Environment.GetEnvironmentVariable("MSSQL_DB_PASSWORD");
        var originalMssqlEncrypt = Environment.GetEnvironmentVariable("MSSQL_DB_ENCRYPT");
        var originalMssqlTrust = Environment.GetEnvironmentVariable("MSSQL_DB_TRUST_SERVER_CERTIFICATE");
        using var sandbox = new TestConfigSandbox();
        var tempRoot = sandbox.RootDirectory;
        var userConfigPath = Path.Combine(tempRoot, "appsettings.json");
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }

        try
        {
            Environment.SetEnvironmentVariable("Supabase__Url", null);
            Environment.SetEnvironmentVariable("Supabase__AnonKey", null);
            Environment.SetEnvironmentVariable("Supabase__Schema", null);
            Environment.SetEnvironmentVariable("MSSQL_DB_SERVER", null);
            Environment.SetEnvironmentVariable("MSSQL_DB_PORT", null);
            Environment.SetEnvironmentVariable("MSSQL_DB_NAME", null);
            Environment.SetEnvironmentVariable("MSSQL_DB_USER", null);
            Environment.SetEnvironmentVariable("MSSQL_DB_PASSWORD", null);
            Environment.SetEnvironmentVariable("MSSQL_DB_ENCRYPT", null);
            Environment.SetEnvironmentVariable("MSSQL_DB_TRUST_SERVER_CERTIFICATE", null);

            var services = new ServiceCollection();
            services.AddAppConfiguration();
            using var provider = services.BuildServiceProvider();

            Assert.True(File.Exists(userConfigPath));

            var settings = provider.GetRequiredService<IOptions<AppSettings>>().Value;
            Assert.Equal("通用工具箱", settings.Application.Name);
            Assert.Equal("logs", settings.Logging.LogPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Supabase__Url", originalSupabaseUrl);
            Environment.SetEnvironmentVariable("Supabase__AnonKey", originalSupabaseAnonKey);
            Environment.SetEnvironmentVariable("Supabase__Schema", originalSupabaseSchema);
            Environment.SetEnvironmentVariable("MSSQL_DB_SERVER", originalMssqlServer);
            Environment.SetEnvironmentVariable("MSSQL_DB_PORT", originalMssqlPort);
            Environment.SetEnvironmentVariable("MSSQL_DB_NAME", originalMssqlName);
            Environment.SetEnvironmentVariable("MSSQL_DB_USER", originalMssqlUser);
            Environment.SetEnvironmentVariable("MSSQL_DB_PASSWORD", originalMssqlPassword);
            Environment.SetEnvironmentVariable("MSSQL_DB_ENCRYPT", originalMssqlEncrypt);
            Environment.SetEnvironmentVariable("MSSQL_DB_TRUST_SERVER_CERTIFICATE", originalMssqlTrust);
        }
    }

    [Fact]
    public void RepairReleaseCenterSettings_ShouldBackfillMissingReleaseCenterFields_FromValidSourceConfig()
    {
        using var sandbox = new TestConfigSandbox();
        var tempRoot = sandbox.RootDirectory;
        var userConfigPath = Path.Combine(tempRoot, "appsettings.json");
        var sourceConfigPath = Path.Combine(tempRoot, "source.appsettings.json");

        File.WriteAllText(
            userConfigPath,
            """
            {
              "Application": { "Name": "通用工具箱", "Version": "1.0.0" },
              "Plugin": { "PluginDirectory": "plugins", "AutoLoad": true, "AllowUnsigned": false },
              "ReleaseCenter": {
                "Enabled": false,
                "BaseUrl": "http://101.42.19.26:8000",
                "Channel": "stable",
                "AnonKey": ""
              },
              "Logging": { "LogLevel": { "Default": "Information", "Microsoft": "Warning" }, "LogPath": "logs" }
            }
            """);

        File.WriteAllText(
            sourceConfigPath,
            """
            {
              "ReleaseCenter": {
                "Enabled": true,
                "BaseUrl": "http://101.42.19.26:8000",
                "Channel": "stable",
                "AnonKey": "valid-anon-key"
              }
            }
            """);

        ConfigurationExtensions.RepairReleaseCenterSettings(userConfigPath, sourceConfigPath);

        var repaired = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(userConfigPath));
        Assert.NotNull(repaired);
        Assert.True(repaired!.ReleaseCenter.Enabled);
        Assert.Equal("http://101.42.19.26:8000", repaired.ReleaseCenter.BaseUrl);
        Assert.Equal("stable", repaired.ReleaseCenter.Channel);
        Assert.Equal("valid-anon-key", repaired.ReleaseCenter.AnonKey);
    }

    [Fact]
    public void EnsureSafeUserConfiguration_ShouldThrow_WhenTestSettingsLeakIntoUserConfig()
    {
        var previousAllowUnsafeConfig = Environment.GetEnvironmentVariable("DIB_ALLOW_UNSAFE_CONFIG");
        Environment.SetEnvironmentVariable("DIB_ALLOW_UNSAFE_CONFIG", null);

        try
        {
            var settings = new AppSettings
            {
                Application = new ApplicationConfig { Name = "TestApp", Version = "1.0.0" },
                Plugin = new PluginConfig { PluginDirectory = "plugins-tests" },
                ReleaseCenter = new ReleaseCenterConfig
                {
                    Enabled = true,
                    BaseUrl = "http://release-center.local",
                    Channel = "stable"
                }
            };

            var ex = Assert.Throws<InvalidOperationException>(() =>
                ConfigurationSafetyValidator.EnsureSafeUserConfiguration(settings, "C:\\Users\\Administrator\\AppData\\Local\\UniversalTrayTool\\appsettings.json"));

            Assert.Contains("测试配置污染", ex.Message);
            Assert.Contains("TestApp", ex.Message);
            Assert.Contains("plugins-tests", ex.Message);
            Assert.Contains("release-center.local", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DIB_ALLOW_UNSAFE_CONFIG", previousAllowUnsafeConfig);
        }
    }

    [Fact]
    public void AddAppConfiguration_ShouldMapLegacyMssqlEnvironmentVariables()
    {
        var originalServer = Environment.GetEnvironmentVariable("MSSQL_DB_SERVER");
        var originalPort = Environment.GetEnvironmentVariable("MSSQL_DB_PORT");
        var originalName = Environment.GetEnvironmentVariable("MSSQL_DB_NAME");
        var originalUser = Environment.GetEnvironmentVariable("MSSQL_DB_USER");
        var originalPassword = Environment.GetEnvironmentVariable("MSSQL_DB_PASSWORD");
        var originalEncrypt = Environment.GetEnvironmentVariable("MSSQL_DB_ENCRYPT");
        var originalTrust = Environment.GetEnvironmentVariable("MSSQL_DB_TRUST_SERVER_CERTIFICATE");
        using var sandbox = new TestConfigSandbox();

        try
        {
            Environment.SetEnvironmentVariable("MSSQL_DB_SERVER", "legacy-sql-host");
            Environment.SetEnvironmentVariable("MSSQL_DB_PORT", "22433");
            Environment.SetEnvironmentVariable("MSSQL_DB_NAME", "ChisDict");
            Environment.SetEnvironmentVariable("MSSQL_DB_USER", "pluginUser");
            Environment.SetEnvironmentVariable("MSSQL_DB_PASSWORD", "pluginPassword");
            Environment.SetEnvironmentVariable("MSSQL_DB_ENCRYPT", "true");
            Environment.SetEnvironmentVariable("MSSQL_DB_TRUST_SERVER_CERTIFICATE", "false");

            var services = new ServiceCollection();
            services.AddAppConfiguration();
            using var provider = services.BuildServiceProvider();

            var settings = provider.GetRequiredService<IOptions<AppSettings>>().Value;
            Assert.Equal("legacy-sql-host", settings.MedicalDrugImport.SqlServer.Host);
            Assert.Equal(22433, settings.MedicalDrugImport.SqlServer.Port);
            Assert.Equal("ChisDict", settings.MedicalDrugImport.SqlServer.Database);
            Assert.Equal("pluginUser", settings.MedicalDrugImport.SqlServer.Username);
            Assert.Equal("pluginPassword", settings.MedicalDrugImport.SqlServer.Password);
            Assert.True(settings.MedicalDrugImport.SqlServer.Encrypt);
            Assert.False(settings.MedicalDrugImport.SqlServer.TrustServerCertificate);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSSQL_DB_SERVER", originalServer);
            Environment.SetEnvironmentVariable("MSSQL_DB_PORT", originalPort);
            Environment.SetEnvironmentVariable("MSSQL_DB_NAME", originalName);
            Environment.SetEnvironmentVariable("MSSQL_DB_USER", originalUser);
            Environment.SetEnvironmentVariable("MSSQL_DB_PASSWORD", originalPassword);
            Environment.SetEnvironmentVariable("MSSQL_DB_ENCRYPT", originalEncrypt);
            Environment.SetEnvironmentVariable("MSSQL_DB_TRUST_SERVER_CERTIFICATE", originalTrust);
        }
    }

    [Fact]
    public void ConfigurationExtensions_ShouldResolveRuntimePaths_UnderConfigRootDirectory()
    {
        using var sandbox = new TestConfigSandbox();

        var root = ConfigurationExtensions.GetConfigRootDirectory();
        var logs = ConfigurationExtensions.GetLogsDirectory();
        var plugins = ConfigurationExtensions.GetRuntimePluginsDirectory();
        var backups = ConfigurationExtensions.GetReleaseBackupsDirectory();

        Assert.Equal(sandbox.RootDirectory, root);
        Assert.Equal(Path.Combine(sandbox.RootDirectory, "logs"), logs);
        Assert.Equal(Path.Combine(sandbox.RootDirectory, "plugins"), plugins);
        Assert.Equal(Path.Combine(sandbox.RootDirectory, "release-backups", "plugins"), backups);
    }
}


