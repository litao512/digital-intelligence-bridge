using System.Text.Json;
using System.Collections.Generic;
using Avalonia.Controls;
using DigitalIntelligenceBridge.Plugin.Abstractions;
using MedicalDrugImport.Plugin;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class MedicalDrugImportPluginContractTests
{
    [Fact]
    public void PluginJson_ShouldMatchEntryAssemblyAndEntryType()
    {
        var pluginJsonPath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "plugins-src",
            "MedicalDrugImport.Plugin",
            "plugin.json");
        var fullPath = Path.GetFullPath(pluginJsonPath);
        var json = File.ReadAllText(fullPath);
        var manifest = JsonSerializer.Deserialize<PluginManifest>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(manifest);
        Assert.Equal("medical-drug-import", manifest!.Id);
        Assert.Equal("MedicalDrugImport.Plugin.dll", manifest.EntryAssembly);
        Assert.Equal(typeof(MedicalDrugImportPlugin).FullName, manifest.EntryType);
    }

    [Fact]
    public void Plugin_ShouldImplementContract_AndExposeMenuAndView()
    {
        var plugin = new MedicalDrugImportPlugin();
        plugin.Initialize(new StubPluginHostContext());

        var manifest = plugin.GetManifest();
        var menuItems = plugin.CreateMenuItems();
        var content = plugin.CreateContent(menuItems[0].Id);

        Assert.Equal("medical-drug-import", manifest.Id);
        var menu = Assert.Single(menuItems);
        Assert.Equal("medical-drug-import.home", menu.Id);
        Assert.IsAssignableFrom<UserControl>(content);
    }

    private sealed class StubPluginHostContext : IPluginHostContext
    {
        public string HostVersion => "1.0.0";

        public string PluginDirectory => "plugins/MedicalDrugImport";

        public void LogInformation(string message)
        {
        }

        public IReadOnlyList<AuthorizedRuntimeResource> GetAuthorizedResources() => [];

        public bool TryGetResource(string usageKey, out AuthorizedRuntimeResource? resource)
        {
            resource = null;
            return false;
        }
    }
}

