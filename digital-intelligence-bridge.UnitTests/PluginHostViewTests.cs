using Avalonia.Controls;
using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Plugin.Abstractions;
using DigitalIntelligenceBridge.ViewModels;
using Microsoft.Extensions.Options;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class PluginHostViewTests
{
    [Fact]
    public void NavigateCommand_ShouldCreateErrorHostViewModel_WhenPluginMenuHasNoLoadedPlugin()
    {
        var settings = new AppSettings();
        IReadOnlyList<PluginMenuItem> externalMenus =
        [
            new PluginMenuItem { Id = "medical-drug-import.home", Name = "医保导入插件", Icon = "🧩", Order = 30 }
        ];

        var vm = new MainWindowViewModel(new TestLogger(), Options.Create(settings), null, externalMenus);

        vm.NavigateCommand.Execute("plugin:medical-drug-import.home");

        Assert.NotNull(vm.SelectedTab);
        var tab = vm.SelectedTab!;
        var hostViewModel = Assert.IsType<PluginHostViewModel>(tab.Content);
        Assert.True(hostViewModel.HasError);
        Assert.IsType<Border>(hostViewModel.Content);
    }

    [Fact]
    public void ViewLocator_ShouldResolvePluginHostView_WhenGivenPluginHostViewModel()
    {
        var locator = new ViewLocator();
        var view = locator.Build(new PluginHostViewModel(new TextBlock { Text = "插件内容" }));

        Assert.NotNull(view);
        Assert.IsType<DigitalIntelligenceBridge.Views.PluginHostView>(view);
    }

    [Fact]
    public void PluginHostViewModel_ShouldProvideErrorPlaceholder_WhenContentCreationFails()
    {
        var viewModel = PluginHostViewModel.CreateError("插件页面创建失败");

        Assert.True(viewModel.HasError);
        var placeholder = Assert.IsType<Border>(viewModel.Content);
        var panel = Assert.IsType<StackPanel>(placeholder.Child);
        Assert.Contains(panel.Children.OfType<TextBlock>(), child => child.Text?.Contains("插件页面创建失败") == true);
    }

    [Fact]
    public void MainWindowAxaml_ShouldRenderPluginHostView_WhenPluginHostTabSelected()
    {
        var filePath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "digital-intelligence-bridge",
            "Views",
            "MainWindow.axaml");
        var fullPath = Path.GetFullPath(filePath);

        var xaml = File.ReadAllText(fullPath);

        Assert.Contains("ConverterParameter=PluginHost", xaml);
        Assert.Contains("<views:PluginHostView", xaml);
    }

    private sealed class TestLogger : Services.ILoggerService<MainWindowViewModel>
    {
        public void LogCritical(string message, params object[] args) { }
        public void LogDebug(string message, params object[] args) { }
        public void LogError(string message, params object[] args) { }
        public void LogError(Exception exception, string message, params object[] args) { }
        public void LogInformation(string message, params object[] args) { }
        public void LogWarning(string message, params object[] args) { }
    }
}

