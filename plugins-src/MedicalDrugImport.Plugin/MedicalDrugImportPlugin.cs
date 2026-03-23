using System.Collections.Generic;
using Avalonia.Controls;
using DigitalIntelligenceBridge.Plugin.Abstractions;
using MedicalDrugImport.Plugin.Configuration;
using MedicalDrugImport.Plugin.Services;
using MedicalDrugImport.Plugin.ViewModels;
using MedicalDrugImport.Plugin.Views;

namespace MedicalDrugImport.Plugin;

public class MedicalDrugImportPlugin : IPluginModule
{
    private const string PluginId = "medical-drug-import";
    private const string HomeMenuId = "medical-drug-import.home";

    private IPluginHostContext? _hostContext;

    public void Initialize(IPluginHostContext context)
    {
        _hostContext = context;
        _hostContext.LogInformation("医保药品导入插件已初始化");
    }

    public PluginManifest GetManifest()
    {
        return new PluginManifest
        {
            Id = PluginId,
            Name = "医保药品导入",
            Version = "0.1.0",
            EntryAssembly = "MedicalDrugImport.Plugin.dll",
            EntryType = typeof(MedicalDrugImportPlugin).FullName ?? string.Empty,
            MinHostVersion = "1.0.0"
        };
    }

    public IReadOnlyList<PluginMenuItem> CreateMenuItems()
    {
        return
        [
            new PluginMenuItem
            {
                Id = HomeMenuId,
                Name = "医保药品导入",
                Icon = "💊",
                Order = 100
            }
        ];
    }

    public Control CreateContent(string menuId)
    {
        return menuId switch
        {
            HomeMenuId => CreateHomeView(),
            _ => new TextBlock { Text = $"未知插件菜单: {menuId}" }
        };
    }

    private Control CreateHomeView()
    {
        var pluginDirectory = _hostContext?.PluginDirectory ?? AppContext.BaseDirectory;
        var settings = PluginConfigurationLoader.Load(pluginDirectory);
        var excelService = new DrugExcelImportService();
        var repository = new DrugImportRepository(settings);
        var pipeline = new DrugImportPipelineService(excelService, repository);
        var syncService = new SqlServerDrugSyncService(settings, repository);
        var viewModel = new DrugImportPluginViewModel(excelService, pipeline, syncService, settings);

        return new MedicalDrugImportHomeView(viewModel);
    }
}
