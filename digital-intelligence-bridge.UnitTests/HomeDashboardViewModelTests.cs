using System.Text.Json;
using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Models;
using DigitalIntelligenceBridge.Services;
using DigitalIntelligenceBridge.ViewModels;
using Microsoft.Extensions.Options;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class HomeDashboardViewModelTests
{
    [Fact]
    public async Task RefreshAsync_ShouldShowSiteProfileRequired_WhenSiteNameMissing()
    {
        using var sandbox = new TestConfigSandbox();
        var vm = CreateVm(siteOrganization: "第一人民医院", siteName: string.Empty, siteRemark: string.Empty);

        await vm.RefreshAsync();

        Assert.Equal("未配置站点名称", vm.SiteDisplayName);
        Assert.Equal("需要完善站点信息", vm.PendingActionTitle);
        Assert.Contains("设置", vm.PendingActionDetail);
    }

    [Fact]
    public async Task RefreshAsync_ShouldShowSiteProfileRequired_WhenSiteOrganizationMissing()
    {
        using var sandbox = new TestConfigSandbox();
        var vm = CreateVm(siteOrganization: string.Empty, siteName: "门诊一楼登记台", siteRemark: string.Empty);

        await vm.RefreshAsync();

        Assert.Equal("未配置使用单位", vm.SiteDisplayName);
        Assert.Equal("需要完善站点信息", vm.PendingActionTitle);
        Assert.Contains("使用单位", vm.PendingActionDetail);
    }

    [Fact]
    public async Task RefreshAsync_ShouldMergeAuthorizedAndRuntimePlugins_WhenPluginAlreadyActive()
    {
        using var sandbox = new TestConfigSandbox();
        var runtimeRoot = Path.Combine(sandbox.RootDirectory, "runtime-plugins");
        var stagingRoot = Path.Combine(sandbox.RootDirectory, "release-staging", "plugins", "stable");
        CreatePluginManifest(
            runtimeRoot,
            "patient-registration",
            "就诊登记",
            "1.0.1");

        var vm = CreateVm(
            new DashboardReleaseCenterService(),
            runtimePluginRoot: runtimeRoot,
            stagingDirectory: stagingRoot);
        await vm.RefreshAsync();

        Assert.Equal("第一人民医院 / 门诊一楼登记台", vm.SiteDisplayName);
        Assert.Equal("1 个", vm.AuthorizedPluginCountText);
        var plugin = Assert.Single(vm.PluginItems);
        Assert.Equal("就诊登记", plugin.Name);
        Assert.Equal("1.0.1", plugin.Version);
        Assert.Equal("已生效", plugin.Status);
        Assert.Equal("无需操作", vm.PendingActionTitle);
    }

    [Fact]
    public async Task RefreshAsync_ShouldMarkPluginAsPendingRestart_WhenStagingPluginExists()
    {
        using var sandbox = new TestConfigSandbox();
        var runtimeRoot = Path.Combine(sandbox.RootDirectory, "runtime-plugins");
        var stagingRoot = Path.Combine(sandbox.RootDirectory, "release-staging", "plugins", "stable");
        CreatePluginManifest(
            stagingRoot,
            "patient-registration",
            "就诊登记",
            "1.0.2",
            folderName: "patient-registration-1.0.2");

        var vm = CreateVm(
            new DashboardReleaseCenterService(),
            runtimePluginRoot: runtimeRoot,
            stagingDirectory: stagingRoot);
        await vm.RefreshAsync();

        var plugin = Assert.Single(vm.PluginItems);
        Assert.Equal("待重启生效", plugin.Status);
        Assert.Equal("需要重启", vm.PendingActionTitle);
    }

    [Fact]
    public async Task InitializeSitePluginsCommand_ShouldRunCheckDownloadPrepareSequence()
    {
        using var sandbox = new TestConfigSandbox();
        var service = new DashboardReleaseCenterService();
        var vm = CreateVm(service);

        vm.InitializeSitePluginsCommand.Execute();
        await WaitUntilAsync(() => !vm.IsBusy);

        Assert.Equal(1, service.CheckUpdateCallCount);
        Assert.Equal(1, service.PluginDownloadCallCount);
        Assert.Equal(1, service.PluginPrepareCallCount);
        Assert.Equal("初始化完成", vm.LastInitializationStatus);
        Assert.Equal("需要重启", vm.PendingActionTitle);
    }

    [Fact]
    public void Commands_ShouldInvokeSettingsNavigationAndRestart()
    {
        using var sandbox = new TestConfigSandbox();
        var appService = new StubApplicationService();
        var opened = false;
        var vm = CreateVm(appService: appService, openSettings: () => opened = true);

        vm.OpenSettingsCommand.Execute();
        vm.RestartApplicationCommand.Execute();

        Assert.True(opened);
        Assert.True(appService.RestartCalled);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100; i++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException("等待首页初始化命令执行完成超时。");
    }

    private static void CreatePluginManifest(string root, string id, string name, string version, string? folderName = null)
    {
        var pluginDirectory = Path.Combine(root, folderName ?? id);
        Directory.CreateDirectory(pluginDirectory);
        File.WriteAllText(
            Path.Combine(pluginDirectory, "plugin.json"),
            JsonSerializer.Serialize(new
            {
                id,
                name,
                version
            }));
    }

    private static HomeDashboardViewModel CreateVm(
        IReleaseCenterService? releaseCenterService = null,
        StubApplicationService? appService = null,
        Action? openSettings = null,
        string siteOrganization = "第一人民医院",
        string siteName = "门诊一楼登记台",
        string siteRemark = "收费处旁",
        string? runtimePluginRoot = null,
        string? stagingDirectory = null)
    {
        var settings = new AppSettings
        {
            Application = new ApplicationConfig
            {
                Name = "DIB客户端",
                Version = "1.0.1"
            },
            Plugin = new PluginConfig
            {
                PluginDirectory = runtimePluginRoot ?? "plugins"
            },
            ReleaseCenter = new ReleaseCenterConfig
            {
                Enabled = true,
                BaseUrl = "http://101.42.19.26:8000",
                Channel = "stable",
                SiteOrganization = siteOrganization,
                SiteName = siteName,
                SiteRemark = siteRemark,
                RuntimePluginRoot = runtimePluginRoot ?? string.Empty,
                StagingDirectory = stagingDirectory ?? string.Empty
            }
        };

        return new HomeDashboardViewModel(
            new NullLoggerService<HomeDashboardViewModel>(),
            appService ?? new StubApplicationService(),
            Options.Create(settings),
            releaseCenterService,
            openSettings ?? (() => { }));
    }

    private sealed class DashboardReleaseCenterService : IReleaseCenterService
    {
        public bool IsConfigured => true;
        public int CheckUpdateCallCount { get; private set; }
        public int PluginDownloadCallCount { get; private set; }
        public int PluginPrepareCallCount { get; private set; }

        public Task<ReleaseCenterCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
        {
            CheckUpdateCallCount++;
            return Task.FromResult(new ReleaseCenterCheckResult(
                true,
                "发现 1 类可用更新",
                "客户端更新：暂无发布版本",
                "插件更新：1 个可用插件（就诊登记 1.0.1）",
                "channel=stable",
                "当前站点：门诊一楼登记台 / 11111111-1111-1111-1111-111111111111",
                "授权插件：1 个",
                "就诊登记 / patient-registration / 1.0.1"));
        }

        public Task<ResourceDiscoverySnapshot> DiscoverResourcesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ResourceDiscoverySnapshot());

        public Task<AuthorizedResourceSnapshot> GetAuthorizedResourcesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new AuthorizedResourceSnapshot());

        public Task<ResourceApplicationSubmitResult> ApplyResourceAsync(string resourceId, string pluginCode, string reason, CancellationToken cancellationToken = default)
            => Task.FromResult(new ResourceApplicationSubmitResult(true, "ok", "apply-test", "Submitted"));

        public Task<ReleaseCenterClientDownloadResult> DownloadLatestClientPackageAsync(IProgress<ReleaseCenterDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new ReleaseCenterClientDownloadResult(true, "没有可下载的客户端更新包", string.Empty, string.Empty, "C:\\cache", string.Empty));

        public Task<ReleaseCenterPluginDownloadResult> DownloadAvailablePluginPackagesAsync(CancellationToken cancellationToken = default)
        {
            PluginDownloadCallCount++;
            return Task.FromResult(new ReleaseCenterPluginDownloadResult(true, "插件包已缓存 1 项", "detail", 1, "C:\\cache"));
        }

        public Task<ReleaseCenterPluginPrepareResult> PrepareCachedPluginPackagesAsync(CancellationToken cancellationToken = default)
        {
            PluginPrepareCallCount++;
            return Task.FromResult(new ReleaseCenterPluginPrepareResult(true, "已生成 1 个预安装目录", "detail", 1, "C:\\staging"));
        }

        public Task<ReleaseCenterPluginActivateResult> ActivatePreparedPluginPackagesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ReleaseCenterPluginActivateResult(true, "已激活 1 个插件目录", "detail", 1, "C:\\runtime"));

        public Task<ReleaseCenterPluginRollbackResult> RestoreLatestPluginBackupAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ReleaseCenterPluginRollbackResult(true, "已恢复最近一次插件备份", "detail", 1, "C:\\runtime"));
    }

    private sealed class StubApplicationService : IApplicationService
    {
        public bool RestartCalled { get; private set; }
        public bool IsInitialized => true;
        public string GetApplicationName() => "DIB客户端";
        public string GetVersion() => "1.0.1";
        public Task InitializeAsync() => Task.CompletedTask;
        public Task OnShutdownAsync() => Task.CompletedTask;
        public Task OnStartedAsync() => Task.CompletedTask;
        public void RestartApplication() => RestartCalled = true;
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
