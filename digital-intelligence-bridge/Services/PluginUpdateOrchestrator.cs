using System;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalIntelligenceBridge.Services;

public enum PluginUpdateTrigger
{
    Manual,
    Startup,
    SiteInitialized
}

public sealed record PluginUpdateRunResult(
    bool IsSuccess,
    string Summary,
    string Detail,
    bool RestartRequired,
    DateTime CheckedAt,
    ReleaseCenterCheckResult? CheckResult,
    ReleaseCenterPluginDownloadResult? DownloadResult,
    ReleaseCenterPluginPrepareResult? PrepareResult);

public interface IPluginUpdateOrchestrator
{
    Task<PluginUpdateRunResult> RunAsync(PluginUpdateTrigger trigger, CancellationToken cancellationToken = default);
    Task<PluginUpdateRunResult> CheckAsync(PluginUpdateTrigger trigger, CancellationToken cancellationToken = default)
        => RunAsync(trigger, cancellationToken);
    Task<PluginUpdateRunResult> InstallOrUpdateAsync(string pluginId, string? version = null, IProgress<ReleaseCenterDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
        => RunAsync(PluginUpdateTrigger.Manual, cancellationToken);
    Task<PluginUpdateRunResult> UninstallAsync(string pluginId, CancellationToken cancellationToken = default)
        => RunAsync(PluginUpdateTrigger.Manual, cancellationToken);
}

public sealed class PluginUpdateOrchestrator : IPluginUpdateOrchestrator
{
    private readonly IReleaseCenterService? _releaseCenterService;
    private readonly ILoggerService<PluginUpdateOrchestrator> _logger;

    public PluginUpdateOrchestrator(
        IReleaseCenterService? releaseCenterService,
        ILoggerService<PluginUpdateOrchestrator> logger)
    {
        _releaseCenterService = releaseCenterService;
        _logger = logger;
    }

    public async Task<PluginUpdateRunResult> RunAsync(PluginUpdateTrigger trigger, CancellationToken cancellationToken = default)
    {
        var checkedAt = DateTime.Now;
        if (_releaseCenterService is null || !_releaseCenterService.IsConfigured)
        {
            return new PluginUpdateRunResult(
                false,
                "发布中心未配置",
                "ReleaseCenter 未启用或缺少 BaseUrl/Channel。",
                false,
                checkedAt,
                null,
                null,
                null);
        }

        try
        {
            var checkResult = await _releaseCenterService.CheckForUpdatesAsync(cancellationToken).ConfigureAwait(false);
            if (!checkResult.IsSuccess)
            {
                return new PluginUpdateRunResult(
                    false,
                    checkResult.Summary,
                    checkResult.Detail,
                    false,
                    checkedAt,
                    checkResult,
                    null,
                    null);
            }

            var downloadResult = await _releaseCenterService.DownloadAvailablePluginPackagesAsync(cancellationToken).ConfigureAwait(false);
            if (!downloadResult.IsSuccess)
            {
                return new PluginUpdateRunResult(
                    false,
                    downloadResult.Summary,
                    downloadResult.Detail,
                    false,
                    checkedAt,
                    checkResult,
                    downloadResult,
                    null);
            }

            if (downloadResult.DownloadedCount <= 0)
            {
                return new PluginUpdateRunResult(
                    true,
                    downloadResult.Summary,
                    downloadResult.Detail,
                    false,
                    checkedAt,
                    checkResult,
                    downloadResult,
                    null);
            }

            var prepareResult = await _releaseCenterService.PrepareCachedPluginPackagesAsync(cancellationToken).ConfigureAwait(false);
            return new PluginUpdateRunResult(
                prepareResult.IsSuccess,
                prepareResult.Summary,
                prepareResult.Detail,
                prepareResult.IsSuccess && prepareResult.PreparedCount > 0,
                checkedAt,
                checkResult,
                downloadResult,
                prepareResult);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("插件更新编排失败: {Message}", ex.Message);
            return new PluginUpdateRunResult(
                false,
                "插件更新失败",
                ex.Message,
                false,
                checkedAt,
                null,
                null,
                null);
        }
    }

    public async Task<PluginUpdateRunResult> CheckAsync(PluginUpdateTrigger trigger, CancellationToken cancellationToken = default)
    {
        var checkedAt = DateTime.Now;
        if (_releaseCenterService is null || !_releaseCenterService.IsConfigured)
        {
            return new PluginUpdateRunResult(
                false,
                "发布中心未配置",
                "ReleaseCenter 未启用或缺少 BaseUrl/Channel。",
                false,
                checkedAt,
                null,
                null,
                null);
        }

        try
        {
            var checkResult = await _releaseCenterService.CheckForUpdatesAsync(cancellationToken).ConfigureAwait(false);
            return new PluginUpdateRunResult(
                checkResult.IsSuccess,
                checkResult.Summary,
                checkResult.Detail,
                false,
                checkedAt,
                checkResult,
                null,
                null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("插件更新检查失败: {Message}", ex.Message);
            return new PluginUpdateRunResult(
                false,
                "插件更新检查失败",
                ex.Message,
                false,
                checkedAt,
                null,
                null,
                null);
        }
    }

    public async Task<PluginUpdateRunResult> InstallOrUpdateAsync(string pluginId, string? version = null, IProgress<ReleaseCenterDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var checkedAt = DateTime.Now;
        if (_releaseCenterService is null || !_releaseCenterService.IsConfigured)
        {
            return new PluginUpdateRunResult(false, "发布中心未配置", "ReleaseCenter 未启用或缺少 BaseUrl/Channel。", false, checkedAt, null, null, null);
        }

        try
        {
            var downloadResult = await _releaseCenterService.DownloadPluginPackageAsync(pluginId, version, progress, cancellationToken).ConfigureAwait(false);
            if (!downloadResult.IsSuccess || downloadResult.DownloadedCount <= 0)
            {
                return new PluginUpdateRunResult(downloadResult.IsSuccess, downloadResult.Summary, downloadResult.Detail, false, checkedAt, null, downloadResult, null);
            }

            var prepareResult = await _releaseCenterService.PreparePluginPackageAsync(pluginId, version, cancellationToken).ConfigureAwait(false);
            return new PluginUpdateRunResult(prepareResult.IsSuccess, prepareResult.Summary, prepareResult.Detail, prepareResult.IsSuccess && prepareResult.PreparedCount > 0, checkedAt, null, downloadResult, prepareResult);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("插件安装或更新失败: {PluginId}; {Message}", pluginId, ex.Message);
            return new PluginUpdateRunResult(false, "插件安装或更新失败", ex.Message, false, checkedAt, null, null, null);
        }
    }

    public async Task<PluginUpdateRunResult> UninstallAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        var checkedAt = DateTime.Now;
        if (_releaseCenterService is null || !_releaseCenterService.IsConfigured)
        {
            return new PluginUpdateRunResult(false, "发布中心未配置", "ReleaseCenter 未启用或缺少 BaseUrl/Channel。", false, checkedAt, null, null, null);
        }

        try
        {
            var uninstallResult = await _releaseCenterService.MarkPluginForUninstallAsync(pluginId, cancellationToken).ConfigureAwait(false);
            return new PluginUpdateRunResult(uninstallResult.IsSuccess, uninstallResult.Summary, uninstallResult.Detail, uninstallResult.IsSuccess, checkedAt, null, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("插件卸载失败: {PluginId}; {Message}", pluginId, ex.Message);
            return new PluginUpdateRunResult(false, "插件卸载失败", ex.Message, false, checkedAt, null, null, null);
        }
    }
}
