using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class ReleaseCenterPrepareInstallTests
{
    [Fact]
    public async Task PrepareCachedPluginPackagesAsync_ShouldExtractPluginIntoStagingDirectory_WhenZipContainsPluginJson()
    {
        var root = Path.Combine(Path.GetTempPath(), $"release-stage-{Guid.NewGuid():N}");
        var cache = Path.Combine(root, "cache");
        var staging = Path.Combine(root, "staging");
        Directory.CreateDirectory(cache);
        var packagePath = Path.Combine(cache, "patient-registration-1.0.1.zip");
        CreatePluginZip(packagePath, "patient-registration", "PatientRegistration.Plugin.dll");

        try
        {
            var service = CreateService(cache, staging);

            var result = await service.PrepareCachedPluginPackagesAsync();

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.PreparedCount);
            var pluginJson = Directory.GetFiles(staging, "plugin.json", SearchOption.AllDirectories);
            Assert.Single(pluginJson);
            Assert.Contains("patient-registration", await File.ReadAllTextAsync(pluginJson[0]));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PrepareCachedPluginPackagesAsync_ShouldFail_WhenZipMissingPluginJson()
    {
        var root = Path.Combine(Path.GetTempPath(), $"release-stage-{Guid.NewGuid():N}");
        var cache = Path.Combine(root, "cache");
        var staging = Path.Combine(root, "staging");
        Directory.CreateDirectory(cache);
        var packagePath = Path.Combine(cache, "broken.zip");
        using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("readme.txt");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write("invalid plugin package");
        }

        try
        {
            var service = CreateService(cache, staging);

            var result = await service.PrepareCachedPluginPackagesAsync();

            Assert.False(result.IsSuccess);
            Assert.Contains("plugin.json", result.Detail);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static ReleaseCenterService CreateService(string cacheDirectory, string stagingDirectory)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        });

        return new ReleaseCenterService(
            new HttpClient(handler),
            NullLogger<ReleaseCenterService>.Instance,
            Options.Create(new AppSettings
            {
                Application = new ApplicationConfig { Version = "1.0.0" },
                ReleaseCenter = new ReleaseCenterConfig
                {
                    Enabled = true,
                    BaseUrl = "http://release-center.local",
                    Channel = "stable",
                    CacheDirectory = cacheDirectory,
                    StagingDirectory = stagingDirectory
                }
            }));
    }

    private static void CreatePluginZip(string filePath, string pluginId, string entryAssembly)
    {
        using var archive = ZipFile.Open(filePath, ZipArchiveMode.Create);
        WriteEntry(archive, "plugin.json", $$"""
{
  "id": "{{pluginId}}",
  "name": "测试插件",
  "entryAssembly": "{{entryAssembly}}",
  "entryType": "Test.Plugin.Entry, Test.Plugin"
}
""");
        WriteEntry(archive, entryAssembly, "binary-content");
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
        }
    }
}

