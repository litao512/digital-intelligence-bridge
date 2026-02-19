using DigitalIntelligenceBridge.Services;
using DigitalIntelligenceBridge.ViewModels;

namespace DigitalIntelligenceBridge.Tests;

internal static class Program
{
    private static int Main()
    {
        var failures = new List<string>();

        Run("Constructor initializes home tab and menu selection", failures, () =>
        {
            var vm = CreateVm();
            Assert(vm.SelectedTab != null, "SelectedTab should not be null.");
            Assert(vm.OpenTabs.Count == 1, "OpenTabs should contain one default tab.");
            Assert(vm.SelectedTab!.TabType == TabItemType.Home, "Default selected tab should be Home.");
            Assert(vm.CurrentView == MainViewType.Home, "Default view should be Home.");
            Assert(vm.MenuItems.Any(x => x.ViewType == MainViewType.Home && x.IsSelected), "Home menu should be selected.");
        });

        Run("Navigate command opens Todo tab", failures, () =>
        {
            var vm = CreateVm();
            vm.NavigateCommand.Execute(MainViewType.Todo);
            Assert(vm.CurrentView == MainViewType.Todo, "CurrentView should be Todo.");
            Assert(vm.SelectedTab != null && vm.SelectedTab.TabType == TabItemType.Todo, "Selected tab should be Todo.");
            Assert(vm.OpenTabs.Any(x => x.TabType == TabItemType.Todo), "OpenTabs should contain Todo tab.");
        });

        Run("Cannot close the only Home tab", failures, () =>
        {
            var vm = CreateVm();
            vm.CloseTabCommand.Execute(vm.SelectedTab);
            Assert(vm.OpenTabs.Count == 1, "Home tab should not be closed when it is the only tab.");
            Assert(vm.OpenTabs[0].TabType == TabItemType.Home, "Remaining tab should be Home.");
        });

        Run("Filter empty state is set when search has no matches", failures, () =>
        {
            var vm = CreateVm();
            vm.SearchText = "__no_match_keyword__";
            Assert(vm.IsTodoEmpty, "IsTodoEmpty should be true.");
            Assert(vm.IsTodoFilterEmpty, "IsTodoFilterEmpty should be true.");
            Assert(!vm.IsTodoDataEmpty, "IsTodoDataEmpty should be false.");
        });

        Run("Deleting all tasks sets data empty state", failures, () =>
        {
            var vm = CreateVm();
            foreach (var item in vm.TodoItems.ToList())
            {
                vm.DeleteTodoCommand.Execute(item);
            }

            Assert(vm.TodoItems.Count == 0, "TodoItems should be empty.");
            Assert(vm.FilteredTodoItems.Count == 0, "FilteredTodoItems should be empty.");
            Assert(vm.IsTodoDataEmpty, "IsTodoDataEmpty should be true.");
            Assert(vm.IsTodoEmpty, "IsTodoEmpty should be true.");
        });

        if (failures.Count == 0)
        {
            Console.WriteLine("All tests passed.");
            return 0;
        }

        Console.WriteLine("Test failures:");
        foreach (var failure in failures)
        {
            Console.WriteLine($"- {failure}");
        }
        return 1;
    }

    private static MainWindowViewModel CreateVm() => new(new NullLoggerService<MainWindowViewModel>());

    private static void Run(string name, List<string> failures, Action test)
    {
        try
        {
            test();
            Console.WriteLine($"[PASS] {name}");
        }
        catch (Exception ex)
        {
            failures.Add($"{name}: {ex.Message}");
            Console.WriteLine($"[FAIL] {name}");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
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
