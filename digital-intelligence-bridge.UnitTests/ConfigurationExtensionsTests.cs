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
    public void AddAppConfiguration_ShouldBindRuntimeConfig_WhenFilesPresent()
    {
        var originalConfigDir = Environment.GetEnvironmentVariable("DIB_CONFIG_DIR");
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
        var tempRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dib-config-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            Environment.SetEnvironmentVariable("DIB_CONFIG_DIR", tempRoot);
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
            var runtimeConfigPath = ConfigurationExtensions.GetRuntimeConfigFilePath();

            File.WriteAllText(
                userConfigPath,
                """
                {
                  "Supabase": { "Url": "http://localhost:54321", "AnonKey": "", "Schema": "dib" },
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
                  "Logging": { "LogLevel": { "Default": "Information", "Microsoft": "Warning" }, "LogPath": "logs" },
                  "Navigation": []
                }
                """);

            File.WriteAllText(
                runtimeConfigPath,
                """
                {
                  "Supabase": { "Url": "https://example.test", "AnonKey": "runtime-anon", "Schema": "dib" }
                }
                """);

            var services = new ServiceCollection();
            services.AddAppConfiguration();
            using var provider = services.BuildServiceProvider();

            var settings = provider.GetRequiredService<IOptions<AppSettings>>().Value;
            Assert.Equal("https://example.test", settings.Supabase.Url);
            Assert.Equal("runtime-anon", settings.Supabase.AnonKey);
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
            Environment.SetEnvironmentVariable("DIB_CONFIG_DIR", originalConfigDir);
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
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void AddAppConfiguration_ShouldCopyDefaultConfig_WhenUserConfigMissing()
    {
        var originalConfigDir = Environment.GetEnvironmentVariable("DIB_CONFIG_DIR");
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
        var tempRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dib-config-fallback-{Guid.NewGuid():N}");
        var userConfigPath = System.IO.Path.Combine(tempRoot, "appsettings.json");
        var runtimeConfigPath = System.IO.Path.Combine(tempRoot, "appsettings.runtime.json");

        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }

        try
        {
            Environment.SetEnvironmentVariable("DIB_CONFIG_DIR", tempRoot);
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
            Assert.False(File.Exists(runtimeConfigPath));

            var settings = provider.GetRequiredService<IOptions<AppSettings>>().Value;
            Assert.Equal("通用工具箱", settings.Application.Name);
            Assert.Equal("logs", settings.Logging.LogPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DIB_CONFIG_DIR", originalConfigDir);
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
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
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
}
