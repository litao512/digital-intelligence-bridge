using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Services;
using DigitalIntelligenceBridge.ViewModels;
using Microsoft.Extensions.Options;
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
    public void NavigateCommand_ShouldOpenTodoTab_WhenNavigateToTodo()
    {
        var vm = CreateVm();

        vm.NavigateCommand.Execute(MainViewType.Todo);

        Assert.Equal(MainViewType.Todo, vm.CurrentView);
        Assert.NotNull(vm.SelectedTab);
        Assert.Equal(TabItemType.Todo, vm.SelectedTab!.TabType);
        Assert.Contains(vm.OpenTabs, x => x.TabType == TabItemType.Todo);
        Assert.Contains(vm.MenuItems, x => x.ViewType == MainViewType.Todo && x.IsSelected);
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
        Assert.Contains(vm.MenuItems, x => x.ViewType == MainViewType.Todo && x.IsSelected);
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
    public void Constructor_ShouldUseConfiguredMenuAndDetectInstalledPlugin_WhenPluginDirectoryExists()
    {
        var pluginRootName = $"plugins-test-{Guid.NewGuid():N}";
        var appFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UniversalTrayTool");
        var pluginRoot = Path.Combine(appFolder, pluginRootName);
        var patientPluginDir = Path.Combine(pluginRoot, "patient");
        Directory.CreateDirectory(patientPluginDir);

        try
        {
            var settings = new AppSettings
            {
                Plugin = new PluginConfig { PluginDirectory = pluginRootName },
                Navigation = new List<NavigationMenuItemConfig>
                {
                    new() { Id = "todo", Name = "待办", Type = "Todo", Order = 1, IsInstalled = true },
                    new() { Id = "patient", Name = "患者", Type = "PatientMgmt", Order = 2, IsInstalled = false }
                }
            };

            var vm = new MainWindowViewModel(
                new NullLoggerService<MainWindowViewModel>(),
                Options.Create(settings));

            Assert.Equal(2, vm.MenuItems.Count);
            Assert.Equal("todo", vm.MenuItems[0].Id);
            Assert.Equal("patient", vm.MenuItems[1].Id);
            Assert.True(vm.MenuItems[1].IsInstalled);
            Assert.False(vm.MenuItems[1].IsPlaceholder);
        }
        finally
        {
            if (Directory.Exists(pluginRoot))
            {
                Directory.Delete(pluginRoot, true);
            }
        }
    }

    [Fact]
    public void Constructor_ShouldFallbackToDefaultMenu_WhenConfiguredMenuHasNoValidType()
    {
        var settings = new AppSettings
        {
            Navigation = new List<NavigationMenuItemConfig>
            {
                new() { Id = "invalid", Name = "Invalid", Type = "NotAViewType", Order = 1 }
            }
        };

        var vm = new MainWindowViewModel(
            new NullLoggerService<MainWindowViewModel>(),
            Options.Create(settings));

        Assert.Contains(vm.MenuItems, x => x.ViewType == MainViewType.Home);
        Assert.Contains(vm.MenuItems, x => x.ViewType == MainViewType.Todo);
        Assert.True(vm.MenuItems.Count >= 2);
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
