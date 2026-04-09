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

public class ReleaseCenterDownloadTests
{
    [Fact]
    public async Task DownloadLatestClientPackageAsync_ShouldReportProgress_WhenStreamingClientPackage()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"release-client-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var packageBytes = Encoding.UTF8.GetBytes(new string('A', 256 * 1024));
        var sha256 = Convert.ToHexString(SHA256.HashData(packageBytes)).ToLowerInvariant();
        var progressEvents = new List<ReleaseCenterDownloadProgress>();
        var progress = new Progress<ReleaseCenterDownloadProgress>(item => progressEvents.Add(item));

        try
        {
            var service = CreateClientDownloadService(tempDir, packageBytes, sha256);

            var result = await service.DownloadLatestClientPackageAsync(progress);
            await WaitForAsync(() => progressEvents.Count >= 2);

            Assert.True(result.IsSuccess);
            Assert.Equal("1.0.1", result.Version);
            Assert.StartsWith(tempDir, result.CacheDirectory);
            Assert.True(File.Exists(result.PackagePath));
            Assert.Equal(packageBytes, await File.ReadAllBytesAsync(result.PackagePath));
            Assert.Contains(progressEvents, item => item.Stage == "downloading");
            Assert.Contains(progressEvents, item => item.Stage == "verifying");
            Assert.Contains(progressEvents, item => item.Stage == "completed");
            Assert.Contains(progressEvents, item => item.BytesReceived > 0);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DownloadAvailablePluginPackagesAsync_ShouldDownloadPluginPackageToCache_WhenManifestContainsPackageUrl()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"release-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var packageBytes = Encoding.UTF8.GetBytes("plugin-package-content");
        var sha256 = Convert.ToHexString(SHA256.HashData(packageBytes)).ToLowerInvariant();

        try
        {
            var service = CreatePluginDownloadService(tempDir, packageBytes, sha256);

            var result = await service.DownloadAvailablePluginPackagesAsync();

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.DownloadedCount);
            Assert.StartsWith(tempDir, result.CacheDirectory);
            var downloadedFile = Directory.GetFiles(tempDir, "*.zip", SearchOption.AllDirectories);
            Assert.Single(downloadedFile);
            Assert.Equal(packageBytes, await File.ReadAllBytesAsync(downloadedFile[0]));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DownloadAvailablePluginPackagesAsync_ShouldResolveRelativePackageUrl_AgainstBaseUrl()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"release-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var packageBytes = Encoding.UTF8.GetBytes("plugin-package-content");
        var sha256 = Convert.ToHexString(SHA256.HashData(packageBytes)).ToLowerInvariant();

        try
        {
            var service = CreatePluginDownloadService(tempDir, packageBytes, sha256, useRelativePackageUrl: true);

            var result = await service.DownloadAvailablePluginPackagesAsync();

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.DownloadedCount);
            var downloadedFile = Directory.GetFiles(tempDir, "*.zip", SearchOption.AllDirectories);
            Assert.Single(downloadedFile);
            Assert.Equal(packageBytes, await File.ReadAllBytesAsync(downloadedFile[0]));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DownloadAvailablePluginPackagesAsync_ShouldFail_WhenSha256DoesNotMatch()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"release-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var packageBytes = Encoding.UTF8.GetBytes("plugin-package-content");

        try
        {
            var service = CreatePluginDownloadService(tempDir, packageBytes, new string('0', 64));

            var result = await service.DownloadAvailablePluginPackagesAsync();

            Assert.False(result.IsSuccess);
            Assert.Contains("SHA256", result.Detail);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static ReleaseCenterService CreateClientDownloadService(string cacheDirectory, byte[] packageBytes, string sha256)
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri.Contains("client-manifest", StringComparison.OrdinalIgnoreCase))
            {
                var manifest = $$"""
{
  "latestVersion": "1.0.1",
  "minUpgradeVersion": "1.0.0",
  "packageUrl": "http://release-center.local/downloads/dib-win-x64-portable-1.0.1.zip",
  "sha256": "{{sha256}}"
}
""";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(manifest, Encoding.UTF8, "application/json")
                };
            }

            if (request.RequestUri.AbsoluteUri.Contains("plugin-manifest", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"plugins\":[]}", Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(packageBytes)
            };
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
                    ClientCacheDirectory = cacheDirectory
                }
            }));
    }

    private static ReleaseCenterService CreatePluginDownloadService(string cacheDirectory, byte[] packageBytes, string sha256, bool useRelativePackageUrl = false)
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri.Contains("plugin-manifest", StringComparison.OrdinalIgnoreCase))
            {
                var packageUrl = useRelativePackageUrl
                    ? "/storage/v1/object/public/dib-releases/plugins/patient-registration/stable/1.0.1/patient-registration-1.0.1.zip"
                    : "http://release-center.local/packages/patient-registration-1.0.1.zip";
                var manifest = $$"""
{
  "plugins": [
    {
      "pluginId": "patient-registration",
      "name": "就诊登记",
      "version": "1.0.1",
      "packageUrl": "{{packageUrl}}",
      "sha256": "{{sha256}}"
    }
  ]
}
""";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(manifest, Encoding.UTF8, "application/json")
                };
            }

            if (request.RequestUri!.AbsoluteUri.Contains("client-manifest", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"latestVersion\":\"1.0.0\"}", Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(packageBytes)
            };
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
                    CacheDirectory = cacheDirectory
                }
            }));
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100; i++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
        }
    }
}

