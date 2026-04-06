using DigitalIntelligenceBridge.Plugin.Host;
using MedicalDrugImport.Plugin.Configuration;
using MedicalDrugImport.Plugin;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class PluginRuntimeDiscoveryTests
{
    [Fact]
    public void DiscoverManifests_ShouldFindMedicalDrugImportPlugin_InRuntimeDirectory()
    {
        var pluginRoot = GetRuntimePluginRoot();
        var catalog = new PluginCatalogService();

        var manifests = catalog.DiscoverManifests(pluginRoot);

        var plugin = Assert.Single(manifests, item => item.Manifest.Id == "medical-drug-import");
        Assert.Equal("plugins\\MedicalDrugImport\\plugin.json", Path.GetRelativePath(GetRepositoryRoot(), plugin.ManifestPath));
    }

    [Fact]
    public void LoadPlugin_ShouldLoadSamplePlugin_FromRuntimeDirectory()
    {
        var pluginRoot = GetRuntimePluginRoot();
        var catalog = new PluginCatalogService();
        var loader = new PluginLoaderService();
        var discovered = Assert.Single(catalog.DiscoverManifests(pluginRoot));

        var loaded = loader.LoadPlugin(discovered);

        Assert.NotNull(loaded.Module);
        Assert.Empty(loaded.ErrorMessage);
        Assert.Equal(typeof(MedicalDrugImportPlugin).FullName, loaded.Module!.GetType().FullName);
    }

    [Fact]
    public void LoadPlugin_ShouldReturnClearError_WhenEntryAssemblyIsMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), $"plugin-missing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var pluginDir = Path.Combine(root, "MissingPlugin");
        Directory.CreateDirectory(pluginDir);

        try
        {
            File.WriteAllText(Path.Combine(pluginDir, "plugin.json"), """
            {
              "id": "missing-plugin",
              "name": "缺失插件",
              "version": "0.1.0",
              "entryAssembly": "Missing.Plugin.dll",
              "entryType": "Missing.Plugin.Entry",
              "minHostVersion": "1.0.0"
            }
            """);

            var catalog = new PluginCatalogService();
            var loader = new PluginLoaderService();
            var plugin = Assert.Single(catalog.DiscoverManifests(root));

            var loaded = loader.LoadPlugin(plugin);

            Assert.Null(loaded.Module);
            Assert.Contains("Missing.Plugin.dll", loaded.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void RuntimePluginDirectory_ShouldContainPluginSettings()
    {
        var pluginSettingsPath = Path.Combine(GetRuntimePluginRoot(), "MedicalDrugImport", PluginConfigurationLoader.SettingsFileName);

        Assert.True(File.Exists(pluginSettingsPath));
    }

    [Fact]
    public void PluginConfigurationLoader_ShouldReturnDefaults_WhenSettingsFileIsMissing()
    {
        var pluginDir = Path.Combine(Path.GetTempPath(), $"plugin-runtime-{Guid.NewGuid():N}");
        Directory.CreateDirectory(pluginDir);

        try
        {
            var settings = PluginConfigurationLoader.Load(pluginDir);

            Assert.NotNull(settings);
            Assert.NotEmpty(settings.Excel.RequiredSheets);
            Assert.Equal(1000, settings.Import.BatchSize);
            Assert.Equal(50, settings.Import.MaxSyncRowsPerRun);
            Assert.False(settings.Import.AllowUnsafeFullSync);
        }
        finally
        {
            Directory.Delete(pluginDir, true);
        }
    }

    private static string GetRepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }

    private static string GetRuntimePluginRoot()
    {
        return Path.Combine(GetRepositoryRoot(), "plugins");
    }
}

