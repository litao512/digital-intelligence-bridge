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
    public async Task DownloadLatestClientPackageAsync_ShouldReturnCancelled_AndDeletePartialFile_WhenCancellationRequested()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"release-client-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var packageBytes = Encoding.UTF8.GetBytes(new string('B', 256 * 1024));
        var sha256 = Convert.ToHexString(SHA256.HashData(packageBytes)).ToLowerInvariant();
        var cts = new CancellationTokenSource();
        var observedDownloadProgress = false;

        try
        {
            var progress = new Progress<ReleaseCenterDownloadProgress>(item =>
            {
                if (!observedDownloadProgress && item.Stage == "downloading" && item.BytesReceived > 0)
                {
                    observedDownloadProgress = true;
                    cts.Cancel();
                }
            });

            var service = CreateClientDownloadService(tempDir, packageBytes, sha256, simulateStreamingCancellation: true);

            var result = await service.DownloadLatestClientPackageAsync(progress, cts.Token);

            Assert.False(result.IsSuccess);
            Assert.Equal("客户端下载已取消", result.Summary);
            Assert.Contains("已取消", result.Detail);
            Assert.Equal(tempDir, result.CacheDirectory);
            Assert.Equal(string.Empty, result.PackagePath);
            Assert.Empty(Directory.GetFiles(tempDir, "*.zip", SearchOption.AllDirectories));
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
    public async Task DownloadAvailablePluginPackagesAsync_ShouldSkipPluginPackage_WhenRuntimeVersionIsCurrent()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"release-cache-{Guid.NewGuid():N}");
        var runtimeDir = Path.Combine(Path.GetTempPath(), $"release-runtime-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(runtimeDir, "patient-registration"));
        await File.WriteAllTextAsync(
            Path.Combine(runtimeDir, "patient-registration", "plugin.json"),
            """
            {
              "id": "patient-registration",
              "name": "就诊登记",
              "version": "1.0.1"
            }
            """);
        var packageBytes = Encoding.UTF8.GetBytes("plugin-package-content");
        var sha256 = Convert.ToHexString(SHA256.HashData(packageBytes)).ToLowerInvariant();

        try
        {
            var service = CreatePluginDownloadService(tempDir, packageBytes, sha256, runtimePluginRoot: runtimeDir);

            var result = await service.DownloadAvailablePluginPackagesAsync();

            Assert.True(result.IsSuccess);
            Assert.Equal(0, result.DownloadedCount);
            Assert.Empty(Directory.GetFiles(tempDir, "*.zip", SearchOption.AllDirectories));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }

            if (Directory.Exists(runtimeDir))
            {
                Directory.Delete(runtimeDir, recursive: true);
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

    private static ReleaseCenterService CreateClientDownloadService(string cacheDirectory, byte[] packageBytes, string sha256, bool simulateStreamingCancellation = false)
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

            HttpContent content = simulateStreamingCancellation
                ? new SlowCancelableByteArrayContent(packageBytes)
                : new ByteArrayContent(packageBytes);

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
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

    private static ReleaseCenterService CreatePluginDownloadService(
        string cacheDirectory,
        byte[] packageBytes,
        string sha256,
        bool useRelativePackageUrl = false,
        string runtimePluginRoot = "",
        bool simulateStreaming = false)
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

            HttpContent content = simulateStreaming
                ? new SlowCancelableByteArrayContent(packageBytes)
                : new ByteArrayContent(packageBytes);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
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
                    AnonKey = string.Empty,
                    CacheDirectory = cacheDirectory,
                    RuntimePluginRoot = runtimePluginRoot
                }
            }));
    }

    private static ReleaseCenterService CreateMultiPluginDownloadService(
        string cacheDirectory,
        byte[] requestedBytes,
        string requestedSha256,
        byte[] otherBytes,
        string otherSha256,
        List<string> downloadedUrls)
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            if (url.Contains("plugin-manifest", StringComparison.OrdinalIgnoreCase))
            {
                var manifest = $$"""
{
  "plugins": [
    {
      "pluginId": "patient-registration",
      "name": "就诊登记",
      "version": "1.0.2",
      "packageUrl": "http://release-center.local/packages/patient-registration-1.0.2.zip",
      "sha256": "{{requestedSha256}}"
    },
    {
      "pluginId": "other-plugin",
      "name": "其他插件",
      "version": "2.0.0",
      "packageUrl": "http://release-center.local/packages/other-plugin-2.0.0.zip",
      "sha256": "{{otherSha256}}"
    }
  ]
}
""";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(manifest, Encoding.UTF8, "application/json")
                };
            }

            downloadedUrls.Add(url);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(url.Contains("other-plugin", StringComparison.OrdinalIgnoreCase) ? otherBytes : requestedBytes)
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
                    AnonKey = string.Empty,
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

    private sealed class SlowCancelableByteArrayContent(byte[] content) : HttpContent
    {
        protected override bool TryComputeLength(out long length)
        {
            length = content.Length;
            return true;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            return stream.WriteAsync(content, 0, content.Length);
        }

        protected override Task<Stream> CreateContentReadStreamAsync()
        {
            return Task.FromResult<Stream>(new SlowCancelableReadStream(content));
        }

        private sealed class SlowCancelableReadStream(byte[] data) : MemoryStream(data, writable: false)
        {
            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                await Task.Delay(5, cancellationToken);
                var count = Math.Min(4096, buffer.Length);
                return await base.ReadAsync(buffer[..count], cancellationToken);
            }
        }
    }

    [Fact]
    public async Task DownloadPluginPackageAsync_ShouldDownloadOnlyRequestedPlugin_WhenManifestContainsMultiplePlugins()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"release-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var requestedBytes = Encoding.UTF8.GetBytes("requested-plugin-content");
        var otherBytes = Encoding.UTF8.GetBytes("other-plugin-content");
        var requestedSha256 = Convert.ToHexString(SHA256.HashData(requestedBytes)).ToLowerInvariant();
        var otherSha256 = Convert.ToHexString(SHA256.HashData(otherBytes)).ToLowerInvariant();
        var downloadedUrls = new List<string>();

        try
        {
            var service = CreateMultiPluginDownloadService(tempDir, requestedBytes, requestedSha256, otherBytes, otherSha256, downloadedUrls);

            var result = await service.DownloadPluginPackageAsync("patient-registration", "1.0.2");

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.DownloadedCount);
            var downloadedFile = Assert.Single(Directory.GetFiles(tempDir, "*.zip", SearchOption.TopDirectoryOnly));
            Assert.Equal(requestedBytes, await File.ReadAllBytesAsync(downloadedFile));
            Assert.Single(downloadedUrls);
            Assert.Contains("patient-registration-1.0.2.zip", downloadedUrls[0]);
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
    public async Task DownloadPluginPackageAsync_ShouldReportProgress_WhenDownloadingRequestedPlugin()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"release-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var packageBytes = Encoding.UTF8.GetBytes(new string('P', 192 * 1024));
        var sha256 = Convert.ToHexString(SHA256.HashData(packageBytes)).ToLowerInvariant();
        var progressEvents = new List<ReleaseCenterDownloadProgress>();
        var progress = new Progress<ReleaseCenterDownloadProgress>(item => progressEvents.Add(item));

        try
        {
            var service = CreatePluginDownloadService(tempDir, packageBytes, sha256, simulateStreaming: true);

            var result = await service.DownloadPluginPackageAsync("patient-registration", "1.0.1", progress);
            await WaitForAsync(() => progressEvents.Count >= 2);

            Assert.True(result.IsSuccess);
            Assert.Contains(progressEvents, item => item.Stage == "downloading" && item.BytesReceived > 0);
            Assert.Contains(progressEvents, item => item.Stage == "verifying");
            Assert.Contains(progressEvents, item => item.Stage == "completed");
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
    public async Task DownloadPluginPackageAsync_ShouldReturnCancelled_AndDeletePartialFile_WhenCancellationRequested()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"release-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var packageBytes = Encoding.UTF8.GetBytes(new string('C', 256 * 1024));
        var sha256 = Convert.ToHexString(SHA256.HashData(packageBytes)).ToLowerInvariant();
        var cts = new CancellationTokenSource();
        var observedDownloadProgress = false;

        try
        {
            var progress = new Progress<ReleaseCenterDownloadProgress>(item =>
            {
                if (!observedDownloadProgress && item.Stage == "downloading" && item.BytesReceived > 0)
                {
                    observedDownloadProgress = true;
                    cts.Cancel();
                }
            });
            var service = CreatePluginDownloadService(tempDir, packageBytes, sha256, simulateStreaming: true);

            var result = await service.DownloadPluginPackageAsync("patient-registration", "1.0.1", progress, cts.Token);

            Assert.False(result.IsSuccess);
            Assert.Equal("插件下载已取消", result.Summary);
            Assert.Empty(Directory.GetFiles(tempDir, "*.zip", SearchOption.AllDirectories));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}

