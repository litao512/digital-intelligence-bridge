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
        var tempRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dib-config-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            Environment.SetEnvironmentVariable("DIB_CONFIG_DIR", tempRoot);
            Environment.SetEnvironmentVariable("Supabase__Url", null);
            Environment.SetEnvironmentVariable("Supabase__AnonKey", null);
            Environment.SetEnvironmentVariable("Supabase__Schema", null);

            var userConfigPath = ConfigurationExtensions.GetConfigFilePath();
            var runtimeConfigPath = ConfigurationExtensions.GetRuntimeConfigFilePath();

            File.WriteAllText(
                userConfigPath,
                """
                {
                  "Supabase": { "Url": "http://localhost:54321", "AnonKey": "", "Schema": "dib" },
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
        }
        finally
        {
            Environment.SetEnvironmentVariable("DIB_CONFIG_DIR", originalConfigDir);
            Environment.SetEnvironmentVariable("Supabase__Url", originalSupabaseUrl);
            Environment.SetEnvironmentVariable("Supabase__AnonKey", originalSupabaseAnonKey);
            Environment.SetEnvironmentVariable("Supabase__Schema", originalSupabaseSchema);
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
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
