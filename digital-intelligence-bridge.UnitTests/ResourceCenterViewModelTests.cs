using DigitalIntelligenceBridge.Models;
using DigitalIntelligenceBridge.Services;
using DigitalIntelligenceBridge.ViewModels;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class ResourceCenterViewModelTests
{
    [Fact]
    public async Task RefreshAsync_ShouldLoadAvailableAuthorizedAndPendingResources()
    {
        var service = new StubReleaseCenterService
        {
            IncludePendingApplication = true
        };
        var vm = new ResourceCenterViewModel(
            new NullLoggerService<ResourceCenterViewModel>(),
            service);

        await vm.RefreshAsync();

        var available = Assert.Single(vm.AvailableResources);
        Assert.Equal("门诊业务 PostgreSQL", available.Name);
        Assert.Equal("Shared", available.Scope);
        Assert.Equal("审批中", available.ApplyStateText);
        Assert.False(available.CanApply);

        var authorized = Assert.Single(vm.AuthorizedResources);
        Assert.Equal("OCR 网关", authorized.Name);
        Assert.Equal("patient-registration", authorized.Target);

        var pending = Assert.Single(vm.PendingApplications);
        Assert.Equal("UseResource", pending.Type);
        Assert.Equal("UnderReview", pending.Status);
        Assert.Equal("资源中心已同步", vm.Summary);
    }

    [Fact]
    public async Task RefreshAsync_ShouldExposeUnavailableState_WhenServiceMissing()
    {
        var vm = new ResourceCenterViewModel(
            new NullLoggerService<ResourceCenterViewModel>(),
            null);

        await vm.RefreshAsync();

        Assert.Equal("资源中心不可用", vm.Summary);
        Assert.Empty(vm.AvailableResources);
        Assert.Empty(vm.AuthorizedResources);
        Assert.Empty(vm.PendingApplications);
    }

    [Fact]
    public async Task ApplyResourceCommand_ShouldSubmitApplication_AndRefreshPendingList()
    {
        var service = new StubReleaseCenterService();
        var dialogService = new StubResourceApplicationDialogService
        {
            Result = new ResourceApplicationDialogResult(true, "需要访问业务库")
        };
        var vm = new ResourceCenterViewModel(
            new NullLoggerService<ResourceCenterViewModel>(),
            service,
            dialogService);
        await vm.RefreshAsync();

        var available = Assert.Single(vm.AvailableResources);
        vm.ApplyResourceCommand.Execute(available);

        await WaitForIdleAsync(vm);

        Assert.Equal("1", service.ApplyCallCount.ToString());
        Assert.Equal("1", service.LastAppliedResourceId);
        Assert.Equal("medical-drug-import", service.LastAppliedPluginCode);
        Assert.Equal("申请已提交，当前状态：已提交，申请单：3", vm.Summary);
        Assert.Contains(vm.PendingApplications, item => item.Id == "3");
        Assert.Equal("审批中", Assert.Single(vm.AvailableResources).ApplyStateText);
    }

    [Fact]
    public async Task ApplyResourceCommand_ShouldUseUserProvidedReason_WhenDialogConfirmed()
    {
        var service = new StubReleaseCenterService();
        var dialogService = new StubResourceApplicationDialogService
        {
            Result = new ResourceApplicationDialogResult(true, "需要为门诊登记插件接入 OCR 资源")
        };
        var vm = new ResourceCenterViewModel(
            new NullLoggerService<ResourceCenterViewModel>(),
            service,
            dialogService);
        await vm.RefreshAsync();

        var available = Assert.Single(vm.AvailableResources);
        vm.ApplyResourceCommand.Execute(available);

        await WaitForIdleAsync(vm);

        Assert.Equal("需要为门诊登记插件接入 OCR 资源", service.LastAppliedReason);
        Assert.Equal("申请已提交，当前状态：已提交，申请单：3", vm.Summary);
    }

    [Fact]
    public async Task ApplyResourceCommand_ShouldNotSubmit_WhenDialogCancelled()
    {
        var service = new StubReleaseCenterService();
        var dialogService = new StubResourceApplicationDialogService
        {
            Result = new ResourceApplicationDialogResult(false, string.Empty)
        };
        var vm = new ResourceCenterViewModel(
            new NullLoggerService<ResourceCenterViewModel>(),
            service,
            dialogService);
        await vm.RefreshAsync();

        var available = Assert.Single(vm.AvailableResources);
        vm.ApplyResourceCommand.Execute(available);

        await WaitForIdleAsync(vm);

        Assert.Equal(0, service.ApplyCallCount);
        Assert.Equal("已取消资源申请", vm.Summary);
    }

    private static async Task WaitForIdleAsync(ResourceCenterViewModel vm)
    {
        for (var i = 0; i < 50; i++)
        {
            if (!vm.IsLoading && !vm.IsApplying)
            {
                return;
            }

            await Task.Delay(20);
        }
    }

    private sealed class StubReleaseCenterService : IReleaseCenterService
    {
        public bool IncludePendingApplication { get; set; }
        public int ApplyCallCount { get; private set; }
        public string? LastAppliedResourceId { get; private set; }
        public string? LastAppliedPluginCode { get; private set; }
        public string? LastAppliedReason { get; private set; }

        public bool IsConfigured => true;
        public Task<ReleaseCenterCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ReleaseCenterCheckResult(true, "ok", "client", "plugin", "detail", "site", "authorized", "authorized-detail"));
        public Task<ResourceDiscoverySnapshot> DiscoverResourcesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ResourceDiscoverySnapshot
            {
                AvailableToApply =
                [
                    new DiscoverableResourceDescriptor
                    {
                        ResourceId = "1",
                        ResourceCode = "postgres-outpatient-01",
                        ResourceName = "门诊业务 PostgreSQL",
                        ResourceType = "PostgreSQL",
                        VisibilityScope = "Shared",
                        MatchedPlugins = ["medical-drug-import"]
                    }
                ],
                Authorized =
                [
                    new ResourceDescriptor
                    {
                        ResourceId = "2",
                        ResourceCode = "ocr-gateway",
                        ResourceName = "OCR 网关",
                        ResourceType = "HttpService",
                        PluginCode = "patient-registration",
                        BindingScope = "PluginAtSite"
                    }
                ],
                PendingApplications = IncludePendingApplication || ApplyCallCount > 0
                    ?
                    [
                        new PendingResourceApplication
                        {
                            ApplicationId = "3",
                            ApplicationType = "UseResource",
                            ResourceId = "1",
                            Status = "UnderReview"
                        }
                    ]
                    : []
            });
        public Task<AuthorizedResourceSnapshot> GetAuthorizedResourcesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new AuthorizedResourceSnapshot());
        public Task<ResourceApplicationSubmitResult> ApplyResourceAsync(string resourceId, string pluginCode, string reason, CancellationToken cancellationToken = default)
        {
            ApplyCallCount++;
            LastAppliedResourceId = resourceId;
            LastAppliedPluginCode = pluginCode;
            LastAppliedReason = reason;
            return Task.FromResult(new ResourceApplicationSubmitResult(true, "申请已提交", "3", "Submitted"));
        }
        public Task<ReleaseCenterClientDownloadResult> DownloadLatestClientPackageAsync(IProgress<ReleaseCenterDownloadProgress>? progress = null, CancellationToken cancellationToken = default) => Task.FromResult(new ReleaseCenterClientDownloadResult(true, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty));
        public Task<ReleaseCenterPluginDownloadResult> DownloadAvailablePluginPackagesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ReleaseCenterPluginDownloadResult(true, string.Empty, string.Empty, 0, string.Empty));
        public Task<ReleaseCenterPluginPrepareResult> PrepareCachedPluginPackagesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ReleaseCenterPluginPrepareResult(true, string.Empty, string.Empty, 0, string.Empty));
        public Task<ReleaseCenterPluginActivateResult> ActivatePreparedPluginPackagesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ReleaseCenterPluginActivateResult(true, string.Empty, string.Empty, 0, string.Empty));
        public Task<ReleaseCenterPluginRollbackResult> RestoreLatestPluginBackupAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ReleaseCenterPluginRollbackResult(true, string.Empty, string.Empty, 0, string.Empty));
    }

    private sealed class StubResourceApplicationDialogService : IResourceApplicationDialogService
    {
        public ResourceApplicationDialogResult Result { get; set; } = new(false, string.Empty);

        public Task<ResourceApplicationDialogResult> ShowAsync(ResourceApplicationRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(Result);
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
