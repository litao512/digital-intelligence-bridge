using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DigitalIntelligenceBridge.Models;
using DigitalIntelligenceBridge.Services;
using Prism.Commands;

namespace DigitalIntelligenceBridge.ViewModels;

public sealed class ResourceCenterViewModel : ViewModelBase
{
    private readonly ILoggerService<ResourceCenterViewModel> _logger;
    private readonly IReleaseCenterService? _releaseCenterService;
    private readonly IResourceApplicationDialogService _resourceApplicationDialogService;
    private bool _isLoading;
    private string _summary = "资源中心未加载";
    private bool _isApplying;

    public ResourceCenterViewModel()
        : this(new NullLoggerService(), null, new NullResourceApplicationDialogService())
    {
    }

    public ResourceCenterViewModel(
        ILoggerService<ResourceCenterViewModel> logger,
        IReleaseCenterService? releaseCenterService = null,
        IResourceApplicationDialogService? resourceApplicationDialogService = null)
    {
        _logger = logger;
        _releaseCenterService = releaseCenterService;
        _resourceApplicationDialogService = resourceApplicationDialogService ?? new NullResourceApplicationDialogService();
        RefreshCommand = new DelegateCommand(async () => await RefreshAsync(), () => !IsLoading)
            .ObservesProperty(() => IsLoading);
        ApplyResourceCommand = new DelegateCommand<ResourceCenterItem?>(async item => await ApplyResourceAsync(item), item => item is { CanApply: true } && !IsApplying)
            .ObservesProperty(() => IsApplying);
    }

    public ObservableCollection<ResourceCenterItem> AvailableResources { get; } = new();
    public ObservableCollection<ResourceCenterItem> AuthorizedResources { get; } = new();
    public ObservableCollection<ResourceCenterPendingItem> PendingApplications { get; } = new();

    public DelegateCommand RefreshCommand { get; }
    public DelegateCommand<ResourceCenterItem?> ApplyResourceCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public bool IsApplying
    {
        get => _isApplying;
        private set => SetProperty(ref _isApplying, value);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        AvailableResources.Clear();
        AuthorizedResources.Clear();
        PendingApplications.Clear();

        if (_releaseCenterService is null)
        {
            Summary = "资源中心不可用";
            return;
        }

        try
        {
            IsLoading = true;
            var snapshot = await _releaseCenterService.DiscoverResourcesAsync(cancellationToken);
            var pendingResourceIds = snapshot.PendingApplications
                .Select(static item => item.ResourceId)
                .ToHashSet();

            foreach (var resource in snapshot.AvailableToApply)
            {
                var hasPendingApplication = pendingResourceIds.Contains(resource.ResourceId);
                AvailableResources.Add(new ResourceCenterItem(
                    resource.ResourceId,
                    resource.ResourceCode,
                    resource.ResourceName,
                    resource.ResourceType,
                    resource.MatchedPlugins.FirstOrDefault() ?? string.Empty,
                    resource.VisibilityScope ?? string.Empty,
                    hasPendingApplication ? "审批中" : "可申请",
                    !hasPendingApplication));
            }

            foreach (var resource in snapshot.Authorized)
            {
                AuthorizedResources.Add(new ResourceCenterItem(
                    resource.ResourceId,
                    resource.ResourceCode,
                    resource.ResourceName,
                    resource.ResourceType,
                    resource.PluginCode ?? string.Empty,
                    resource.BindingScope ?? string.Empty,
                    "已授权",
                    false));
            }

            foreach (var pending in snapshot.PendingApplications)
            {
                PendingApplications.Add(new ResourceCenterPendingItem(
                    pending.ApplicationId,
                    pending.ApplicationType,
                    pending.ResourceId,
                    pending.Status));
            }

            Summary = "资源中心已同步";
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning("资源中心同步失败: {0}", ex.Message);
            Summary = $"资源中心同步失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ApplyResourceAsync(ResourceCenterItem? item, CancellationToken cancellationToken = default)
    {
        if (item is null || _releaseCenterService is null)
        {
            return;
        }

        try
        {
            IsApplying = true;
            var dialogResult = await _resourceApplicationDialogService.ShowAsync(
                new ResourceApplicationRequest(item.ResourceId, item.Name, item.Target, item.Type),
                cancellationToken);
            if (!dialogResult.IsConfirmed)
            {
                Summary = "已取消资源申请";
                return;
            }

            var result = await _releaseCenterService.ApplyResourceAsync(item.ResourceId, item.Target, dialogResult.Reason, cancellationToken);
            await RefreshAsync(cancellationToken);
            Summary = result.IsSuccess
                ? $"申请已提交，当前状态：{MapApplicationStatus(result.Status)}，申请单：{result.ApplicationId}"
                : $"资源申请提交失败：{result.Message}";
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning("提交资源申请失败: {0}", ex.Message);
            Summary = $"提交资源申请失败：{ex.Message}";
        }
        finally
        {
            IsApplying = false;
        }
    }

    private static string MapApplicationStatus(string? status)
        => status switch
        {
            "Submitted" => "已提交",
            "UnderReview" => "审批中",
            "Approved" => "已批准",
            "Rejected" => "已拒绝",
            _ => string.IsNullOrWhiteSpace(status) ? "未知" : status
        };

    private sealed class NullLoggerService : ILoggerService<ResourceCenterViewModel>
    {
        public void LogCritical(string message, params object[] args) { }
        public void LogDebug(string message, params object[] args) { }
        public void LogError(string message, params object[] args) { }
        public void LogError(System.Exception exception, string message, params object[] args) { }
        public void LogInformation(string message, params object[] args) { }
        public void LogWarning(string message, params object[] args) { }
    }

    private sealed class NullResourceApplicationDialogService : IResourceApplicationDialogService
    {
        public Task<ResourceApplicationDialogResult> ShowAsync(ResourceApplicationRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ResourceApplicationDialogResult(false, string.Empty));
    }
}

public sealed record ResourceCenterItem(
    string ResourceId,
    string Code,
    string Name,
    string Type,
    string Target,
    string Scope,
    string ApplyStateText,
    bool CanApply);
public sealed record ResourceCenterPendingItem(string Id, string Type, string ResourceId, string Status);
