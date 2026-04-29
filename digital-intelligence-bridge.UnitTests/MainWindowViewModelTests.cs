using DigitalIntelligenceBridge.Services;
using DigitalIntelligenceBridge.Plugin.Abstractions;
using DigitalIntelligenceBridge.ViewModels;
using Avalonia.Controls;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class MainWindowViewModelTests
{
    private static MainWindowViewModel CreateVm() => new(new NullLoggerService<MainWindowViewModel>());

    [Fact]
    public void Constructor_ShouldInitializeHomeTabAndSelection_WhenCreated()
    {
        var vm = CreateVm();

        Assert.NotNull(vm.SelectedTab);
        Assert.Single(vm.OpenTabs);
        Assert.Equal(TabItemType.Home, vm.SelectedTab!.TabType);
        Assert.Equal(MainViewType.Home, vm.CurrentView);
        Assert.Contains(vm.MenuItems, x => x.ViewType == MainViewType.Home && x.IsSelected);
    }

    [Fact]
    public void AppVersionText_ShouldUseApplicationServiceVersion_WhenProvided()
    {
        var settings = new Configuration.AppSettings();
        var vm = new MainWindowViewModel(
            new NullLoggerService<MainWindowViewModel>(),
            Microsoft.Extensions.Options.Options.Create(settings),
            applicationService: new StubApplicationService("1.0.3"));

        Assert.Equal("v1.0.3", vm.AppVersionText);
    }

    [Fact]
    public async Task RunStartupPluginUpdateAsync_ShouldCheckWithStartupTrigger()
    {
        var updateService = new StubPluginUpdateOrchestrator(new PluginUpdateRunResult(
            true,
            "插件包已缓存 0 项",
            string.Empty,
            false,
            DateTime.Now,
            null,
            null,
            null));
        var vm = new MainWindowViewModel(
            new NullLoggerService<MainWindowViewModel>(),
            Microsoft.Extensions.Options.Options.Create(new Configuration.AppSettings()),
            pluginUpdateOrchestrator: updateService);

        await vm.RunStartupPluginUpdateAsync(delay: TimeSpan.Zero);

        Assert.Equal(1, updateService.CheckCallCount);
        Assert.Equal(0, updateService.RunCallCount);
        Assert.Equal(PluginUpdateTrigger.Startup, updateService.LastTrigger);
    }

    [Fact]
    public async Task RunStartupPluginUpdateAsync_ShouldOnlyCheckPluginsAndExposeAvailablePlugins()
    {
        using var sandbox = new TestConfigSandbox();
        var updateService = new StubPluginUpdateOrchestrator(new PluginUpdateRunResult(
            true,
            "发现插件更新",
            string.Empty,
            false,
            DateTime.Now,
            new ReleaseCenterCheckResult(
                true,
                "发现 1 类可用更新",
                "客户端更新：暂无发布版本",
                "插件更新：1 个可用插件（就诊登记 1.0.3）",
                "detail",
                "site",
                "authorized",
                "就诊登记 / patient-registration / 1.0.3"),
            null,
            null));
        var trayService = new StubTrayService();
        var settings = new Configuration.AppSettings
        {
            Plugin = new Configuration.PluginConfig
            {
                PluginDirectory = Path.Combine(sandbox.RootDirectory, "runtime")
            },
            ReleaseCenter = new Configuration.ReleaseCenterConfig
            {
                RuntimePluginRoot = Path.Combine(sandbox.RootDirectory, "runtime"),
                StagingDirectory = Path.Combine(sandbox.RootDirectory, "staging")
            }
        };
        var vm = new MainWindowViewModel(
            new NullLoggerService<MainWindowViewModel>(),
            Microsoft.Extensions.Options.Options.Create(settings),
            pluginUpdateOrchestrator: updateService,
            trayService: trayService);

        await vm.RunStartupPluginUpdateAsync(delay: TimeSpan.Zero);

        Assert.Equal(1, updateService.CheckCallCount);
        Assert.Equal(0, updateService.RunCallCount);
        await vm.PluginCenter.RefreshAsync();
        var item = Assert.Single(vm.PluginCenter.PluginItems);
        Assert.Equal("安装", item.ActionText);
        Assert.Equal("未安装", item.Status);
        Assert.Contains(trayService.Notifications, item => item.Title == "插件可更新");
        Assert.Contains("1 个插件可更新", trayService.Tooltip);
    }

    [Fact]
    public void MenuSections_ShouldSeparateWorkspaceAndPluginMenus_WhenExternalPluginMenusExist()
    {
        var pluginMenu = new PluginMenuItem
        {
            Id = "patient-registration.entry",
            Name = "就诊登记",
            Icon = "🧾",
            Order = 10
        };
        var vm = new MainWindowViewModel(
            new NullLoggerService<MainWindowViewModel>(),
            Microsoft.Extensions.Options.Options.Create(new Configuration.AppSettings()),
            externalPluginMenus: [pluginMenu]);

        var workspace = Assert.Single(vm.MenuSections, section => section.Title == "工作台");
        Assert.Contains(workspace.Items, item => item.Title == "首页");
        Assert.Contains(workspace.Items, item => item.Title == "资源中心");
        Assert.Contains(workspace.Items, item => item.Title == "插件中心");

        var plugins = Assert.Single(vm.MenuSections, section => section.Title == "插件");
        var item = Assert.Single(plugins.Items);
        Assert.Equal("就诊登记", item.Title);
        Assert.True(item.IsExternalPlugin);
    }

    [Fact]
    public void NavigateCommand_ShouldOpenTodoTab_WhenNavigateToTodo()
    {
        var vm = CreateVm();

        vm.NavigateCommand.Execute(MainViewType.Todo);

        Assert.Equal(MainViewType.Todo, vm.CurrentView);
        Assert.NotNull(vm.SelectedTab);
        Assert.Equal(TabItemType.Todo, vm.SelectedTab!.TabType);
        Assert.Contains(vm.OpenTabs, x => x.TabType == TabItemType.Todo);
        Assert.DoesNotContain(vm.MenuItems, x => x.ViewType == MainViewType.Todo);
    }

    [Fact]
    public void NavigateCommand_ShouldOpenResourceCenterTab_WhenNavigateToResourceCenter()
    {
        var vm = CreateVm();

        vm.NavigateCommand.Execute(MainViewType.ResourceCenter);

        Assert.Equal(MainViewType.ResourceCenter, vm.CurrentView);
        Assert.NotNull(vm.SelectedTab);
        Assert.Equal(TabItemType.ResourceCenter, vm.SelectedTab!.TabType);
        Assert.Contains(vm.MenuItems, x => x.ViewType == MainViewType.ResourceCenter && x.IsSelected);
    }

    [Fact]
    public void NavigateCommand_ShouldOpenPluginCenterTab_WhenNavigateToPluginCenter()
    {
        var vm = CreateVm();

        vm.NavigateCommand.Execute(MainViewType.PluginCenter);

        Assert.Equal(MainViewType.PluginCenter, vm.CurrentView);
        Assert.NotNull(vm.SelectedTab);
        Assert.Equal(TabItemType.PluginCenter, vm.SelectedTab!.TabType);
    }

    [Fact]
    public void NavigateCommand_ShouldReuseExistingTab_WhenNavigatingToSameView()
    {
        var vm = CreateVm();

        vm.NavigateCommand.Execute(MainViewType.Todo);
        var tabCount = vm.OpenTabs.Count;

        vm.NavigateCommand.Execute(MainViewType.Todo);

        Assert.Equal(tabCount, vm.OpenTabs.Count);
        Assert.Equal(MainViewType.Todo, vm.CurrentView);
        Assert.Equal(TabItemType.Todo, vm.SelectedTab!.TabType);
    }

    [Fact]
    public void CloseTabCommand_ShouldSwitchToPreviousTabAndUpdateSelection()
    {
        var vm = CreateVm();

        vm.NavigateCommand.Execute(MainViewType.Todo);
        vm.NavigateCommand.Execute(MainViewType.Settings);

        var closingTab = vm.SelectedTab;
        vm.CloseTabCommand.Execute(closingTab);

        Assert.NotNull(vm.SelectedTab);
        Assert.Equal(TabItemType.Todo, vm.SelectedTab!.TabType);
        Assert.Equal(MainViewType.Todo, vm.CurrentView);
        Assert.DoesNotContain(vm.MenuItems, x => x.ViewType == MainViewType.Settings && x.IsSelected);
    }

    [Fact]
    public void SelectedTab_ShouldUpdatePageTitles_WhenChanged()
    {
        var vm = CreateVm();
        var customTab = new TabItemModel
        {
            Id = "custom",
            Title = "新的标题",
            Subtitle = "子标题",
            TabType = TabItemType.Schedule
        };
        vm.OpenTabs.Add(customTab);

        vm.SelectedTab = customTab;

        Assert.Equal("新的标题", vm.PageTitle);
        Assert.Equal("子标题", vm.PageSubtitle);
    }

    [Fact]
    public void CloseTabCommand_ShouldKeepHomeTab_WhenOnlyHomeExists()
    {
        var vm = CreateVm();
        var homeTab = vm.SelectedTab;

        vm.CloseTabCommand.Execute(homeTab);

        Assert.Single(vm.OpenTabs);
        Assert.Equal(TabItemType.Home, vm.OpenTabs[0].TabType);
    }

    [Fact]
    public void Filter_ShouldSetFilterEmptyState_WhenSearchHasNoMatch()
    {
        var vm = CreateVm();

        vm.SearchText = "__no_match_keyword__";

        Assert.True(vm.IsTodoEmpty);
        Assert.True(vm.IsTodoFilterEmpty);
        Assert.False(vm.IsTodoDataEmpty);
    }

    [Fact]
    public void DeleteTodoCommand_ShouldSetDataEmptyState_WhenAllItemsDeleted()
    {
        var vm = CreateVm();
        var all = vm.TodoItems.ToList();

        foreach (var item in all)
        {
            vm.DeleteTodoCommand.Execute(item);
        }

        Assert.True(vm.IsTodoDataEmpty);
        Assert.True(vm.IsTodoEmpty);
        Assert.Empty(vm.TodoItems);
        Assert.Empty(vm.FilteredTodoItems);
    }

    [Fact]
    public void Constructor_ShouldNotInjectTodoOrLegacyDrugImport_WhenUsingDefaultMenuFallback()
    {
        var vm = CreateVm();

        Assert.Contains(vm.MenuItems, x => x.ViewType == MainViewType.Home);
        Assert.DoesNotContain(vm.MenuItems, x => x.ViewType == MainViewType.Todo);
        Assert.DoesNotContain("DrugImport", Enum.GetNames(typeof(MainViewType)));
    }

    [Fact]
    public void MainWindowAxaml_ShouldBindSidebarToMenuSections()
    {
        var fullPath = FindRepositoryFile("digital-intelligence-bridge", "Views", "MainWindow.axaml");
        var xaml = File.ReadAllText(fullPath);

        Assert.Contains("ItemsSource=\"{Binding MenuSections}\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding Items}\"", xaml);
    }

    [Fact]
    public void ToggleMenuCollapseCommand_ShouldToggleCollapsedState()
    {
        var vm = CreateVm();

        Assert.False(vm.IsMenuCollapsed);
        Assert.Equal(220, vm.SidebarWidth);

        vm.ToggleMenuCollapseCommand.Execute();
        Assert.True(vm.IsMenuCollapsed);
        Assert.Equal("▶", vm.MenuToggleGlyph);
        Assert.Equal(48, vm.SidebarWidth);

        vm.ToggleMenuCollapseCommand.Execute();
        Assert.False(vm.IsMenuCollapsed);
        Assert.Equal("◀", vm.MenuToggleGlyph);
        Assert.Equal(220, vm.SidebarWidth);
    }

    private static string FindRepositoryFile(params string[] relativePathParts)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(new[] { current.FullName }.Concat(relativePathParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException($"未找到仓库文件：{Path.Combine(relativePathParts)}");
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

    private sealed class StubApplicationService(string version) : IApplicationService
    {
        public bool IsInitialized => true;
        public Task InitializeAsync() => Task.CompletedTask;
        public Task OnStartedAsync() => Task.CompletedTask;
        public Task OnShutdownAsync() => Task.CompletedTask;
        public string GetVersion() => version;
        public string GetApplicationName() => "DIB客户端";
        public void RestartApplication() { }
    }

    private sealed class StubPluginUpdateOrchestrator(PluginUpdateRunResult result) : IPluginUpdateOrchestrator
    {
        public int RunCallCount { get; private set; }
        public int CheckCallCount { get; private set; }
        public PluginUpdateTrigger LastTrigger { get; private set; }

        public Task<PluginUpdateRunResult> RunAsync(PluginUpdateTrigger trigger, CancellationToken cancellationToken = default)
        {
            RunCallCount++;
            LastTrigger = trigger;
            return Task.FromResult(result);
        }

        public Task<PluginUpdateRunResult> CheckAsync(PluginUpdateTrigger trigger, CancellationToken cancellationToken = default)
        {
            CheckCallCount++;
            LastTrigger = trigger;
            return Task.FromResult(result);
        }
    }

    private sealed class StubTrayService : ITrayService
    {
        public List<(string Title, string Message)> Notifications { get; } = new();
        public string Tooltip { get; private set; } = string.Empty;
        public bool IsWindowVisible => true;
        public bool IsExiting => false;
        public void Initialize(Window mainWindow) { }
        public void ShowWindow() { }
        public void HideWindow() { }
        public void ToggleWindow() { }
        public void ExitApplication() { }
        public void AddMenuItem(string header, Action callback, string? parentPath = null) { }
        public void RemoveMenuItem(string path) { }
        public void AddSeparator(string? parentPath = null) { }
        public void SetTooltip(string tooltip) => Tooltip = tooltip;
        public void SetShowNotifications(bool show) { }
        public void ShowNotification(string title, string message) => Notifications.Add((title, message));
    }
}
