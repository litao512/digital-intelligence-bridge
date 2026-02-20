using System.IO;
using System.Reflection;
using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Services;
using DigitalIntelligenceBridge.ViewModels;
using Microsoft.Extensions.Options;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class SettingsViewModelTests
{
    [Fact]
    public async Task Constructor_ShouldRunSelfCheck_WhenCreated()
    {
        var vm = CreateVm();
        await WaitForSelfCheckAsync(vm);

        Assert.NotEmpty(vm.SelfCheckItems);
        Assert.NotNull(vm.LastSelfCheckAt);
        Assert.Contains("自检完成", vm.SelfCheckSummary);
        Assert.Contains(vm.SelfCheckItems, x => x.Name == "Supabase 表访问");
    }

    [Fact]
    public async Task ExportSelfCheckReportCommand_ShouldAppendExportFlag_WhenExecuted()
    {
        var vm = CreateVm();
        await WaitForSelfCheckAsync(vm);

        vm.ExportSelfCheckReportCommand.Execute();

        Assert.Contains("已导出报告", vm.SelfCheckSummary);
    }

    [Fact]
    public void IsTrayIconAvailable_ShouldReturnTrue_WhenFileExistsUnderBaseDirectory()
    {
        var relativePath = Path.Combine("test-icons", $"{Guid.NewGuid():N}.ico");
        var fullPath = Path.Combine(AppContext.BaseDirectory, relativePath);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(fullPath, "fake-icon");

        try
        {
            var result = InvokeIsTrayIconAvailable(relativePath, out var detail);

            Assert.True(result);
            Assert.Equal(fullPath, detail);
        }
        finally
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void IsTrayIconAvailable_ShouldReturnFalse_WhenFileAndAssetMissing()
    {
        var relativePath = Path.Combine("missing-icons", $"{Guid.NewGuid():N}.ico");

        var result = InvokeIsTrayIconAvailable(relativePath, out var detail);

        Assert.False(result);
        Assert.StartsWith(Path.Combine(AppContext.BaseDirectory, relativePath), detail);
        Assert.Contains("资源加载失败", detail);
    }

    private static bool InvokeIsTrayIconAvailable(string path, out string detail)
    {
        var method = typeof(SettingsViewModel).GetMethod(
            "IsTrayIconAvailable",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var args = new object?[] { path, null };
        var result = (bool)method!.Invoke(null, args)!;
        detail = (string)args[1]!;
        return result;
    }

    private static async Task WaitForSelfCheckAsync(SettingsViewModel vm)
    {
        for (var i = 0; i < 50; i++)
        {
            if (!vm.IsSelfCheckRunning && vm.SelfCheckItems.Count > 0)
            {
                return;
            }

            await Task.Delay(20);
        }
    }

    private static SettingsViewModel CreateVm()
    {
        var settings = new AppSettings
        {
            Application = new ApplicationConfig { Name = "TestApp", Version = "9.9.9" },
            Tray = new TrayConfig { IconPath = "Assets/avalonia-logo.ico", ShowNotifications = true },
            Plugin = new PluginConfig { PluginDirectory = "plugins-tests" },
            Logging = new LoggingConfig { LogPath = "logs" },
            Navigation = new List<NavigationMenuItemConfig>
            {
                new() { Id = "home", Name = "首页", Type = "Home", Order = 1 },
                new() { Id = "todo", Name = "待办", Type = "Todo", Order = 2 }
            }
        };

        return new SettingsViewModel(
            new StubApplicationService(),
            new StubTrayService(),
            new NullLoggerService<SettingsViewModel>(),
            Options.Create(settings),
            new StubSupabaseService());
    }

    private sealed class StubSupabaseService : ISupabaseService
    {
        public bool IsConfigured => true;

        public Task<SupabaseConnectionResult> CheckConnectionAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SupabaseConnectionResult(true, true, System.Net.HttpStatusCode.OK, "ok"));
        }

        public Task<SupabaseConnectionResult> CheckTableAccessAsync(string tableName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SupabaseConnectionResult(true, true, System.Net.HttpStatusCode.OK, "table ok"));
        }
    }

    private sealed class StubApplicationService : IApplicationService
    {
        public bool IsInitialized => true;
        public string GetApplicationName() => "TestApp";
        public string GetVersion() => "9.9.9";
        public Task InitializeAsync() => Task.CompletedTask;
        public Task OnShutdownAsync() => Task.CompletedTask;
        public Task OnStartedAsync() => Task.CompletedTask;
    }

    private sealed class StubTrayService : ITrayService
    {
        public bool IsWindowVisible => true;
        public bool IsExiting => false;
        public void AddMenuItem(string header, Action callback, string? parentPath = null) { }
        public void AddSeparator(string? parentPath = null) { }
        public void ExitApplication() { }
        public void HideWindow() { }
        public void Initialize(Avalonia.Controls.Window mainWindow) { }
        public void RemoveMenuItem(string path) { }
        public void SetShowNotifications(bool show) { }
        public void SetTooltip(string tooltip) { }
        public void ShowNotification(string title, string message) { }
        public void ShowWindow() { }
        public void ToggleWindow() { }
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
