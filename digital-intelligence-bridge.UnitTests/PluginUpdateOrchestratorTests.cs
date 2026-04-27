using DigitalIntelligenceBridge.Models;
using DigitalIntelligenceBridge.Services;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class PluginUpdateOrchestratorTests
{
    [Fact]
    public async Task RunAsync_ShouldCheckDownloadAndPrepare_WhenPluginPackagesAvailable()
    {
        var releaseCenter = new StubReleaseCenterService
        {
            CheckResult = new ReleaseCenterCheckResult(
                true,
                "发现 1 类可用更新",
                "客户端更新：暂无",
                "插件更新：1 个可用插件（就诊登记 1.0.3）",
                "detail",
                "站点正常",
                "授权插件：1 个",
                "就诊登记 / patient-registration / 1.0.3"),
            DownloadResult = new ReleaseCenterPluginDownloadResult(true, "插件包已缓存 1 项", "download-detail", 1, "cache"),
            PrepareResult = new ReleaseCenterPluginPrepareResult(true, "已生成 1 个预安装目录", "prepare-detail", 1, "staging")
        };
        var orchestrator = new PluginUpdateOrchestrator(
            releaseCenter,
            new NullLoggerService<PluginUpdateOrchestrator>());

        var result = await orchestrator.RunAsync(PluginUpdateTrigger.Manual);

        Assert.True(result.IsSuccess);
        Assert.True(result.RestartRequired);
        Assert.Equal("已生成 1 个预安装目录", result.Summary);
        Assert.Equal(1, releaseCenter.CheckCallCount);
        Assert.Equal(1, releaseCenter.DownloadCallCount);
        Assert.Equal(1, releaseCenter.PrepareCallCount);
    }

    [Fact]
    public async Task RunAsync_ShouldReturnNotConfigured_WhenReleaseCenterIsMissing()
    {
        var orchestrator = new PluginUpdateOrchestrator(
            null,
            new NullLoggerService<PluginUpdateOrchestrator>());

        var result = await orchestrator.RunAsync(PluginUpdateTrigger.Manual);

        Assert.False(result.IsSuccess);
        Assert.False(result.RestartRequired);
        Assert.Equal("发布中心未配置", result.Summary);
    }

    [Fact]
    public async Task RunAsync_ShouldNotDownload_WhenCheckFails()
    {
        var releaseCenter = new StubReleaseCenterService
        {
            CheckResult = new ReleaseCenterCheckResult(false, "检查更新失败", "client", "plugin", "network", "site", "authorized", string.Empty)
        };
        var orchestrator = new PluginUpdateOrchestrator(
            releaseCenter,
            new NullLoggerService<PluginUpdateOrchestrator>());

        var result = await orchestrator.RunAsync(PluginUpdateTrigger.Manual);

        Assert.False(result.IsSuccess);
        Assert.Equal("network", result.Detail);
        Assert.Equal(1, releaseCenter.CheckCallCount);
        Assert.Equal(0, releaseCenter.DownloadCallCount);
        Assert.Equal(0, releaseCenter.PrepareCallCount);
    }

    [Fact]
    public async Task RunAsync_ShouldNotPrepare_WhenDownloadFails()
    {
        var releaseCenter = new StubReleaseCenterService
        {
            DownloadResult = new ReleaseCenterPluginDownloadResult(false, "插件包下载失败", "sha mismatch", 0, "cache")
        };
        var orchestrator = new PluginUpdateOrchestrator(
            releaseCenter,
            new NullLoggerService<PluginUpdateOrchestrator>());

        var result = await orchestrator.RunAsync(PluginUpdateTrigger.Manual);

        Assert.False(result.IsSuccess);
        Assert.Equal("sha mismatch", result.Detail);
        Assert.Equal(1, releaseCenter.CheckCallCount);
        Assert.Equal(1, releaseCenter.DownloadCallCount);
        Assert.Equal(0, releaseCenter.PrepareCallCount);
    }

    [Fact]
    public async Task RunAsync_ShouldNotRequireRestart_WhenNoPluginPackagesDownloaded()
    {
        var releaseCenter = new StubReleaseCenterService
        {
            DownloadResult = new ReleaseCenterPluginDownloadResult(true, "插件包已缓存 0 项", string.Empty, 0, "cache")
        };
        var orchestrator = new PluginUpdateOrchestrator(
            releaseCenter,
            new NullLoggerService<PluginUpdateOrchestrator>());

        var result = await orchestrator.RunAsync(PluginUpdateTrigger.Startup);

        Assert.True(result.IsSuccess);
        Assert.False(result.RestartRequired);
        Assert.Equal(1, releaseCenter.CheckCallCount);
        Assert.Equal(1, releaseCenter.DownloadCallCount);
        Assert.Equal(0, releaseCenter.PrepareCallCount);
    }

    private sealed class StubReleaseCenterService : IReleaseCenterService
    {
        public bool IsConfigured { get; set; } = true;
        public int CheckCallCount { get; private set; }
        public int DownloadCallCount { get; private set; }
        public int PrepareCallCount { get; private set; }
        public ReleaseCenterCheckResult CheckResult { get; set; } = new(true, "ok", "client", "plugin", "detail", "site", "authorized", "authorized-detail");
        public ReleaseCenterPluginDownloadResult DownloadResult { get; set; } = new(true, "插件包已缓存 0 项", string.Empty, 0, "cache");
        public ReleaseCenterPluginPrepareResult PrepareResult { get; set; } = new(true, "已生成 0 个预安装目录", string.Empty, 0, "staging");

        public Task<ReleaseCenterCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
        {
            CheckCallCount++;
            return Task.FromResult(CheckResult);
        }

        public Task<ReleaseCenterPluginDownloadResult> DownloadAvailablePluginPackagesAsync(CancellationToken cancellationToken = default)
        {
            DownloadCallCount++;
            return Task.FromResult(DownloadResult);
        }

        public Task<ReleaseCenterPluginPrepareResult> PrepareCachedPluginPackagesAsync(CancellationToken cancellationToken = default)
        {
            PrepareCallCount++;
            return Task.FromResult(PrepareResult);
        }

        public Task<ResourceDiscoverySnapshot> DiscoverResourcesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ResourceDiscoverySnapshot());

        public Task<AuthorizedResourceSnapshot> GetAuthorizedResourcesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new AuthorizedResourceSnapshot());

        public Task<ResourceApplicationSubmitResult> ApplyResourceAsync(string resourceId, string pluginCode, string reason, CancellationToken cancellationToken = default)
            => Task.FromResult(new ResourceApplicationSubmitResult(true, "ok", "apply-test", "Submitted"));

        public Task<ReleaseCenterClientDownloadResult> DownloadLatestClientPackageAsync(IProgress<ReleaseCenterDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new ReleaseCenterClientDownloadResult(true, "没有可下载的客户端更新包", string.Empty, string.Empty, "cache", string.Empty));

        public Task<ReleaseCenterPluginActivateResult> ActivatePreparedPluginPackagesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ReleaseCenterPluginActivateResult(true, "已激活 0 个插件目录", string.Empty, 0, "runtime"));

        public Task<ReleaseCenterPluginRollbackResult> RestoreLatestPluginBackupAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ReleaseCenterPluginRollbackResult(true, "已恢复最近一次插件备份", string.Empty, 0, "runtime"));
    }

    private sealed class NullLoggerService<T> : ILoggerService<T>
    {
        public void LogCritical(string message, params object[] args) { }
        public void LogDebug(string message, params object[] args) { }
        public void LogError(string message, params object[] args) { }
        public void LogError(Exception exception, string message, params object[] args) { }
        public void LogInformation(string message, params object[] args) { }
        public void LogWarning(string message, params object[] args) { }
    }
}
