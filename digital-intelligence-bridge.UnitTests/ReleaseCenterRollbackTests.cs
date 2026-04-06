using System.Net;
using System.Net.Http;
using System.Text;
using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class ReleaseCenterRollbackTests
{
    [Fact]
    public async Task RestoreLatestPluginBackupAsync_ShouldRestoreLatestBackupIntoRuntimeDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"release-rollback-{Guid.NewGuid():N}");
        var backupRoot = Path.Combine(root, "backup");
        var runtimeRoot = Path.Combine(root, "runtime");
        Directory.CreateDirectory(backupRoot);
        Directory.CreateDirectory(runtimeRoot);

        var oldSession = Path.Combine(backupRoot, "20260405-100000", "patient-registration");
        Directory.CreateDirectory(oldSession);
        await File.WriteAllTextAsync(Path.Combine(oldSession, "PatientRegistration.Plugin.dll"), "old-backup");

        var latestSession = Path.Combine(backupRoot, "20260405-120000", "patient-registration");
        Directory.CreateDirectory(latestSession);
        await File.WriteAllTextAsync(Path.Combine(latestSession, "PatientRegistration.Plugin.dll"), "latest-backup");

        var runtimePlugin = Path.Combine(runtimeRoot, "patient-registration");
        Directory.CreateDirectory(runtimePlugin);
        await File.WriteAllTextAsync(Path.Combine(runtimePlugin, "PatientRegistration.Plugin.dll"), "current-runtime");

        try
        {
            var service = CreateService(runtimeRoot, backupRoot);

            var result = await service.RestoreLatestPluginBackupAsync();

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.RestoredCount);
            Assert.Equal("latest-backup", await File.ReadAllTextAsync(Path.Combine(runtimeRoot, "patient-registration", "PatientRegistration.Plugin.dll")));
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
    public async Task RestoreLatestPluginBackupAsync_ShouldReturnNoBackup_WhenBackupDirectoryMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), $"release-rollback-{Guid.NewGuid():N}");
        var runtimeRoot = Path.Combine(root, "runtime");
        Directory.CreateDirectory(runtimeRoot);

        try
        {
            var service = CreateService(runtimeRoot, Path.Combine(root, "backup"));

            var result = await service.RestoreLatestPluginBackupAsync();

            Assert.True(result.IsSuccess);
            Assert.Equal("没有可回滚的插件备份", result.Summary);
            Assert.Equal(0, result.RestoredCount);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static ReleaseCenterService CreateService(string runtimePluginRoot, string backupDirectory)
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

