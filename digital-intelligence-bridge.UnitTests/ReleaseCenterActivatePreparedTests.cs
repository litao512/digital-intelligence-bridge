using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class ReleaseCenterActivatePreparedTests
{
    [Fact]
    public async Task ActivatePreparedPluginPackagesAsync_ShouldCopyPreparedPluginIntoRuntimeDirectory_AndBackupExistingVersion()
    {
        var root = Path.Combine(Path.GetTempPath(), $"release-activate-{Guid.NewGuid():N}");
        var staging = Path.Combine(root, "staging");
        var runtime = Path.Combine(root, "runtime-plugins");
        var backup = Path.Combine(root, "backup");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(runtime);
        Directory.CreateDirectory(backup);

        var preparedDir = Path.Combine(staging, "patient-registration-1.0.1");
        Directory.CreateDirectory(preparedDir);
        await File.WriteAllTextAsync(Path.Combine(preparedDir, "plugin.json"), """
{
  "id": "patient-registration",
  "name": "就诊登记",
  "entryAssembly": "PatientRegistration.Plugin.dll",
  "entryType": "PatientRegistration.Plugin.PatientRegistrationPlugin, PatientRegistration.Plugin"
}
""");
        await File.WriteAllTextAsync(Path.Combine(preparedDir, "PatientRegistration.Plugin.dll"), "new-binary");

        var existingDir = Path.Combine(runtime, "patient-registration");
        Directory.CreateDirectory(existingDir);
        await File.WriteAllTextAsync(Path.Combine(existingDir, "PatientRegistration.Plugin.dll"), "old-binary");

        try
        {
            var service = CreateService(staging, runtime, backup);

            var result = await service.ActivatePreparedPluginPackagesAsync();

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.ActivatedCount);
            Assert.Equal("new-binary", await File.ReadAllTextAsync(Path.Combine(runtime, "patient-registration", "PatientRegistration.Plugin.dll")));
            var backupDll = Directory.GetFiles(backup, "PatientRegistration.Plugin.dll", SearchOption.AllDirectories);
            Assert.Single(backupDll);
            Assert.Equal("old-binary", await File.ReadAllTextAsync(backupDll[0]));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static ReleaseCenterService CreateService(string stagingDirectory, string runtimePluginRoot, string backupDirectory)
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
                Plugin = new PluginConfig { PluginDirectory = "plugins" },
                ReleaseCenter = new ReleaseCenterConfig
                {
                    Enabled = true,
                    BaseUrl = "http://release-center.local",
                    Channel = "stable",
                    StagingDirectory = stagingDirectory,
                    RuntimePluginRoot = runtimePluginRoot,
                    BackupDirectory = backupDirectory
                }
            }));
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
        }
    }
}

