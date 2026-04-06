using System.Text.Json;
using DigitalIntelligenceBridge.Plugin.Host;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class PluginCatalogServiceTests : IDisposable
{
    private readonly string _rootDirectory;

    public PluginCatalogServiceTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), "dib-plugin-catalog-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDirectory);
    }

    [Fact]
    public void DiscoverManifests_ShouldReturnOnlyValidPluginJsonFiles()
    {
        CreatePluginDirectory(
            "medical-drug-import",
            new
            {
                id = "medical-drug-import",
                name = "医保药品导入",
                version = "0.1.0",
                entryAssembly = "MedicalDrugImport.Plugin.dll",
                entryType = "MedicalDrugImport.Plugin.MedicalDrugImportPlugin",
                minHostVersion = "1.0.0"
            });
        CreatePluginDirectory("missing-manifest", null);
        CreatePluginDirectory(
            "invalid-manifest",
            new
            {
                id = "",
                name = "坏插件",
                version = "0.1.0",
                entryAssembly = "",
                entryType = "",
                minHostVersion = "1.0.0"
            });

        var service = new PluginCatalogService();

        var manifests = service.DiscoverManifests(_rootDirectory);

        var manifest = Assert.Single(manifests);
        Assert.Equal("medical-drug-import", manifest.Manifest.Id);
        Assert.Equal("医保药品导入", manifest.Manifest.Name);
        Assert.Equal(Path.Combine(_rootDirectory, "medical-drug-import"), manifest.PluginDirectory);
        Assert.Equal(Path.Combine(_rootDirectory, "medical-drug-import", "plugin.json"), manifest.ManifestPath);
    }

    [Fact]
    public void DiscoverManifests_ShouldReturnEmpty_WhenPluginRootDoesNotExist()
    {
        var service = new PluginCatalogService();

        var manifests = service.DiscoverManifests(Path.Combine(_rootDirectory, "plugins"));

        Assert.Empty(manifests);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private void CreatePluginDirectory(string directoryName, object? manifest)
    {
        var directory = Path.Combine(_rootDirectory, directoryName);
        Directory.CreateDirectory(directory);

        if (manifest is null)
        {
            return;
        }

        var json = JsonSerializer.Serialize(manifest);
        File.WriteAllText(Path.Combine(directory, "plugin.json"), json);
    }
}

