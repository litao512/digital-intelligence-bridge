using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Services;
using Microsoft.Extensions.Options;
using Prism.Commands;

namespace DigitalIntelligenceBridge.ViewModels;

public sealed class PluginCenterItem : ViewModelBase
{
    public string PluginId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}

public sealed class PluginCenterViewModel : ViewModelBase
{
    private readonly ILoggerService<PluginCenterViewModel> _logger;
    private readonly AppSettings _settings;
    private readonly IPluginUpdateOrchestrator? _pluginUpdateOrchestrator;
    private readonly IApplicationService? _applicationService;
    private string _installedCountText = "0 个";
    private string _pendingRestartCountText = "0 个";
    private string _failedCountText = "0 个";
    private string _lastUpdateStatus = "尚未检查";
    private string _lastPrepareStatus = "尚未预安装";
    private bool _isBusy;

    public PluginCenterViewModel()
        : this(
            new NullLoggerService(),
            Options.Create(new AppSettings()),
            null,
            null)
    {
    }

    public PluginCenterViewModel(
        ILoggerService<PluginCenterViewModel> logger,
        IOptions<AppSettings> settings,
        IPluginUpdateOrchestrator? pluginUpdateOrchestrator,
        IApplicationService? applicationService = null)
    {
        _logger = logger;
        _settings = settings.Value;
        _pluginUpdateOrchestrator = pluginUpdateOrchestrator;
        _applicationService = applicationService;
        RefreshCommand = new DelegateCommand(() => _ = RefreshAsync(), () => !IsBusy);
        CheckUpdatesCommand = new DelegateCommand(() => _ = CheckUpdatesAsync(), () => !IsBusy);
        RestartApplicationCommand = new DelegateCommand(() => _applicationService?.RestartApplication());
    }

    public ObservableCollection<PluginCenterItem> PluginItems { get; } = new();

    public string InstalledCountText { get => _installedCountText; private set => SetProperty(ref _installedCountText, value); }
    public string PendingRestartCountText { get => _pendingRestartCountText; private set => SetProperty(ref _pendingRestartCountText, value); }
    public string FailedCountText { get => _failedCountText; private set => SetProperty(ref _failedCountText, value); }
    public string LastUpdateStatus { get => _lastUpdateStatus; private set => SetProperty(ref _lastUpdateStatus, value); }
    public string LastPrepareStatus { get => _lastPrepareStatus; private set => SetProperty(ref _lastPrepareStatus, value); }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommand.RaiseCanExecuteChanged();
                CheckUpdatesCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public DelegateCommand RefreshCommand { get; }
    public DelegateCommand CheckUpdatesCommand { get; }
    public DelegateCommand RestartApplicationCommand { get; }

    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        PluginItems.Clear();
        var runtimePlugins = ReadLocalPluginStates(ResolveRuntimePluginRoot(), "已生效");
        var stagingPlugins = ReadLocalPluginStates(ResolveStagingDirectory(), "待重启生效");
        var merged = MergePluginStates(runtimePlugins, stagingPlugins);

        foreach (var item in merged)
        {
            PluginItems.Add(new PluginCenterItem
            {
                PluginId = item.PluginId,
                Name = item.Name,
                Version = item.Version,
                Status = item.Status
            });
        }

        InstalledCountText = $"{PluginItems.Count(item => string.Equals(item.Status, "已生效", StringComparison.Ordinal))} 个";
        PendingRestartCountText = $"{PluginItems.Count(item => string.Equals(item.Status, "待重启生效", StringComparison.Ordinal))} 个";
        FailedCountText = $"{PluginItems.Count(item => string.Equals(item.Status, "加载失败", StringComparison.Ordinal))} 个";
        return Task.CompletedTask;
    }

    private async Task CheckUpdatesAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            if (_pluginUpdateOrchestrator is null)
            {
                LastUpdateStatus = "插件更新不可用";
                return;
            }

            var result = await _pluginUpdateOrchestrator.RunAsync(PluginUpdateTrigger.Manual);
            LastUpdateStatus = $"{result.CheckedAt:yyyy-MM-dd HH:mm:ss} · {result.Summary}";
            if (result.RestartRequired)
            {
                LastPrepareStatus = LastUpdateStatus;
            }

            await RefreshAsync();
        }
        catch (Exception ex)
        {
            LastUpdateStatus = "插件更新失败";
            _logger.LogWarning("插件中心检查更新失败: {Message}", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string ResolveRuntimePluginRoot()
    {
        if (!string.IsNullOrWhiteSpace(_settings.ReleaseCenter.RuntimePluginRoot))
        {
            return _settings.ReleaseCenter.RuntimePluginRoot;
        }

        return ConfigurationExtensions.GetRuntimePluginsDirectory(_settings.Plugin.PluginDirectory);
    }

    private string ResolveStagingDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_settings.ReleaseCenter.StagingDirectory))
        {
            return _settings.ReleaseCenter.StagingDirectory;
        }

        return Path.Combine(
            ConfigurationExtensions.GetConfigRootDirectory(),
            "release-staging",
            "plugins",
            _settings.ReleaseCenter.Channel.Trim());
    }

    private static IReadOnlyList<PluginCenterItem> MergePluginStates(
        IReadOnlyList<PluginCenterItem> runtimePlugins,
        IReadOnlyList<PluginCenterItem> stagingPlugins)
    {
        var merged = new Dictionary<string, PluginCenterItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var plugin in runtimePlugins)
        {
            merged[plugin.PluginId] = plugin;
        }

        foreach (var plugin in stagingPlugins)
        {
            merged[plugin.PluginId] = plugin;
        }

        return merged.Values
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<PluginCenterItem> ReadLocalPluginStates(string rootDirectory, string status)
    {
        if (!Directory.Exists(rootDirectory))
        {
            return [];
        }

        var items = new List<PluginCenterItem>();
        foreach (var pluginJsonPath in Directory.GetFiles(rootDirectory, "plugin.json", SearchOption.AllDirectories))
        {
            try
            {
                using var stream = File.OpenRead(pluginJsonPath);
                using var document = JsonDocument.Parse(stream);
                var root = document.RootElement;
                var pluginId = root.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
                if (string.IsNullOrWhiteSpace(pluginId))
                {
                    continue;
                }

                var name = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : pluginId;
                var version = root.TryGetProperty("version", out var versionElement) ? versionElement.GetString() : "未识别";
                items.Add(new PluginCenterItem
                {
                    PluginId = pluginId!,
                    Name = name ?? pluginId!,
                    Version = version ?? "未识别",
                    Status = status
                });
            }
            catch
            {
                // 忽略损坏的插件描述文件，避免插件中心整体加载失败。
            }
        }

        return items
            .GroupBy(item => item.PluginId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private sealed class NullLoggerService : ILoggerService<PluginCenterViewModel>
    {
        public void LogCritical(string message, params object[] args) { }
        public void LogDebug(string message, params object[] args) { }
        public void LogError(string message, params object[] args) { }
        public void LogError(Exception exception, string message, params object[] args) { }
        public void LogInformation(string message, params object[] args) { }
        public void LogWarning(string message, params object[] args) { }
    }
}
