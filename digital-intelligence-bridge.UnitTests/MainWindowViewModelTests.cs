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
