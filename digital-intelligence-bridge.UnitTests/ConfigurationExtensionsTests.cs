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
        var tempRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dib-config-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            Environment.SetEnvironmentVariable("DIB_CONFIG_DIR", tempRoot);

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
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}

