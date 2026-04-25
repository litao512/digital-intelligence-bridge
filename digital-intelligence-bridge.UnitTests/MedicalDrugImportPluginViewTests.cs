using Avalonia.Controls;
using System.Collections.Generic;
using MedicalDrugImport.Plugin;
using MedicalDrugImport.Plugin.ViewModels;
using MedicalDrugImport.Plugin.Views;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class MedicalDrugImportPluginViewTests
{
    [Fact]
    public void CreateContent_ShouldReturnToolShellView_ForHomeMenu()
    {
        var plugin = new MedicalDrugImportPlugin();
        plugin.Initialize(new StubPluginHostContext());

        var content = plugin.CreateContent("medical-drug-import.home");

        var view = Assert.IsType<MedicalDrugImportHomeView>(content);
        var viewModel = Assert.IsType<DrugImportPluginViewModel>(view.DataContext);
        Assert.Contains("只读", viewModel.SyncGuardSummary, StringComparison.Ordinal);
        Assert.Contains("50", viewModel.SyncGuardSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void ToolShellViewXaml_ShouldContainImportWorkflowControls()
    {
        var filePath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "plugins-src",
            "MedicalDrugImport.Plugin",
            "Views",
            "MedicalDrugImportHomeView.axaml");
        var fullPath = Path.GetFullPath(filePath);
        var xaml = File.ReadAllText(fullPath);

        Assert.Contains("Text=\"文件路径\"", xaml);
        Assert.Contains("Content=\"预检\"", xaml);
        Assert.Contains("Content=\"导入入库\"", xaml);
        Assert.Contains("Content=\"同步预检\"", xaml);
        Assert.Contains("Content=\"同步 SQL Server\"", xaml);
        Assert.Contains("Content=\"重试同步\"", xaml);
        Assert.Contains("SyncGuardSummary", xaml);
        Assert.Contains("IsEnabled=\"{Binding CanSyncToSqlServer}\"", xaml);
    }

    [Fact]
    public void PluginViewModel_ShouldBePluginLocal_WithoutHostViewModelDependency()
    {
        var viewModel = new DrugImportPluginViewModel();

        Assert.False(viewModel.GetType().FullName!.Contains("DigitalIntelligenceBridge.ViewModels", StringComparison.Ordinal));
        Assert.Equal(string.Empty, viewModel.SelectedFilePath);
    }

    [Fact]
    public void PluginViewModel_ShouldExposeGuardSummary_WhenSettingsProvided()
    {
        var viewModel = new DrugImportPluginViewModel(
            excelImportService: null,
            importPipelineService: null,
            sqlServerDrugSyncService: null,
            settings: new MedicalDrugImport.Plugin.Configuration.PluginSettings
        {
            SqlServer = new MedicalDrugImport.Plugin.Configuration.SqlServerSettings
            {
                EnableWrites = true
            },
            Import = new MedicalDrugImport.Plugin.Configuration.ImportSettings
            {
                BatchSize = 20,
                MaxSyncRowsPerRun = 50
            }
        });

        Assert.Contains("已开启", viewModel.SyncGuardSummary, StringComparison.Ordinal);
        Assert.Contains("20", viewModel.SyncGuardSummary, StringComparison.Ordinal);
        Assert.Contains("50", viewModel.SyncGuardSummary, StringComparison.Ordinal);
        Assert.True(viewModel.CanSyncToSqlServer);
    }

    [Fact]
    public void PluginViewModel_ShouldDisableRealSync_WhenWritesAreDisabled()
    {
        var viewModel = new DrugImportPluginViewModel(
            excelImportService: null,
            importPipelineService: null,
            sqlServerDrugSyncService: null,
            settings: new MedicalDrugImport.Plugin.Configuration.PluginSettings
            {
                SqlServer = new MedicalDrugImport.Plugin.Configuration.SqlServerSettings
                {
                    EnableWrites = false
                }
            });

        Assert.False(viewModel.CanSyncToSqlServer);
        Assert.Contains("只读", viewModel.SyncGuardSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateContent_ShouldLogWarningStyleMessage_WhenDevelopmentModeEnabled()
    {
        var pluginDirectory = Path.Combine(Path.GetTempPath(), $"medical-drug-import-plugin-{Guid.NewGuid():N}");
        Directory.CreateDirectory(pluginDirectory);
        File.WriteAllText(
            Path.Combine(pluginDirectory, "plugin.settings.json"),
            """
            {
              "DevelopmentMode": {
                "Enabled": true
              }
            }
            """);

        try
        {
            var hostContext = new StubPluginHostContext(pluginDirectory);
            var plugin = new MedicalDrugImportPlugin();
            plugin.Initialize(hostContext);

            _ = plugin.CreateContent("medical-drug-import.home");

            Assert.Contains(hostContext.Logs, message => message.Contains("开发模式", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(pluginDirectory, true);
        }
    }

    private sealed class StubPluginHostContext : DigitalIntelligenceBridge.Plugin.Abstractions.IPluginHostContext
    {
        public StubPluginHostContext(string pluginDirectory = "plugins/MedicalDrugImport")
        {
            PluginDirectory = pluginDirectory;
        }

        public List<string> Logs { get; } = [];

        public string HostVersion => "1.0.0";
        public string PluginDirectory { get; }
        public void LogInformation(string message) { Logs.Add(message); }

        public IReadOnlyList<DigitalIntelligenceBridge.Plugin.Abstractions.AuthorizedRuntimeResource> GetAuthorizedResources() => [];

        public bool TryGetResource(string usageKey, out DigitalIntelligenceBridge.Plugin.Abstractions.AuthorizedRuntimeResource? resource)
        {
            resource = null;
            return false;
        }
    }
}

