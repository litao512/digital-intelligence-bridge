using DigitalIntelligenceBridge.Services;
using DigitalIntelligenceBridge.ViewModels;
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
}

