using System.IO;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class ThemeResourceTests
{
    [Fact]
    public void AppThemeResources_ShouldDefineLightAndDarkVariants()
    {
        var appAxamlPath = Path.Combine(ProjectRoot(), "digital-intelligence-bridge", "App.axaml");
        var text = File.ReadAllText(appAxamlPath);

        Assert.Contains("<ResourceDictionary.ThemeDictionaries>", text);
        Assert.Contains("x:Key=\"Light\"", text);
        Assert.Contains("x:Key=\"Dark\"", text);
        Assert.Contains("ColorPageBg", text);
        Assert.Contains("ColorCardBg", text);
        Assert.Contains("ColorTextMain", text);
        Assert.Contains("ColorTextMuted", text);
        Assert.Contains("ColorBorder", text);
    }

    [Fact]
    public void MainWindow_ShouldUseDynamicThemeBrushes_ForPageAndCards()
    {
        var mainWindowPath = Path.Combine(ProjectRoot(), "digital-intelligence-bridge", "Views", "MainWindow.axaml");
        var text = File.ReadAllText(mainWindowPath);

        Assert.Contains("Background=\"{DynamicResource PageBgBrush}\"", text);
        Assert.Contains("Background=\"{DynamicResource CardBgBrush}\"", text);
    }

    [Fact]
    public void MainWindow_LeftNavigation_ShouldUseThemeResources_InsteadOfHardcodedColors()
    {
        var mainWindowPath = Path.Combine(ProjectRoot(), "digital-intelligence-bridge", "Views", "MainWindow.axaml");
        var text = File.ReadAllText(mainWindowPath);

        Assert.DoesNotContain("Foreground=\"White\"", text);
        Assert.DoesNotContain("Foreground=\"#96A0AA\"", text);
        Assert.DoesNotContain("Background=\"#171A1F\"", text);
        Assert.DoesNotContain("BorderBrush=\"#171A1F\"", text);
        Assert.DoesNotContain("Background=\"#3B414A\"", text);
        Assert.Contains("{DynamicResource NavBgBrush}", text);
        Assert.Contains("{DynamicResource NavHoverBrush}", text);
        Assert.Contains("{DynamicResource TextMainBrush}", text);
        Assert.Contains("{DynamicResource TextMutedBrush}", text);
        Assert.Contains("{DynamicResource BorderBrush}", text);
    }

    [Fact]
    public void HomeOverview_ShouldUseThemeResources_ForSurfaceAndText()
    {
        var homeViewPath = Path.Combine(ProjectRoot(), "digital-intelligence-bridge", "Views", "HomeView.axaml");
        var text = File.ReadAllText(homeViewPath);

        Assert.DoesNotContain("Background=\"#F7FAFF\"", text);
        Assert.DoesNotContain("Background=\"#F4FBF4\"", text);
        Assert.DoesNotContain("Background=\"#FFF9F0\"", text);
        Assert.Contains("{DynamicResource CardBgBrush}", text);
        Assert.Contains("{DynamicResource BorderBrush}", text);
        Assert.Contains("{DynamicResource TextMainBrush}", text);
        Assert.Contains("{DynamicResource TextMutedBrush}", text);
    }

    [Fact]
    public void MainWindow_ShouldBindNavigationWidthAndLabels_ToMenuCollapsedState()
    {
        var mainWindowPath = Path.Combine(ProjectRoot(), "digital-intelligence-bridge", "Views", "MainWindow.axaml");
        var text = File.ReadAllText(mainWindowPath);

        Assert.Contains("<ColumnDefinition Width=\"{Binding SidebarWidth}\" />", text);
        Assert.Contains("Command=\"{Binding ToggleMenuCollapseCommand}\"", text);
        Assert.Contains("Text=\"{Binding MenuToggleGlyph}\"", text);
        Assert.Contains("IsVisible=\"{Binding IsMenuExpanded}\"", text);
    }

    [Fact]
    public void MainWindowCodeBehind_ShouldSynchronizeSidebarWidthAtRuntime()
    {
        var codeBehindPath = Path.Combine(ProjectRoot(), "digital-intelligence-bridge", "Views", "MainWindow.axaml.cs");
        var text = File.ReadAllText(codeBehindPath);

        Assert.Contains("PropertyChanged", text);
        Assert.Contains("SidebarWidth", text);
        Assert.Contains("ColumnDefinitions[0]", text);
        Assert.Contains("new GridLength", text);
    }

    [Fact]
    public void MainWindow_CollapsedNavigation_ShouldKeepLeftAlignment()
    {
        var mainWindowPath = Path.Combine(ProjectRoot(), "digital-intelligence-bridge", "Views", "MainWindow.axaml");
        var text = File.ReadAllText(mainWindowPath);

        Assert.DoesNotContain("HorizontalContentAlignment=\"Center\"", text);
        Assert.Contains("HorizontalContentAlignment=\"Left\"", text);
        Assert.Contains("HorizontalAlignment=\"Left\"", text);
    }

    private static string ProjectRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }
}
