using Microsoft.Extensions.Configuration;

namespace PatientRegistration.Plugin.Configuration;

internal static class DevelopmentResourceLoader
{
    public const string SettingsFileName = "plugin.development.json";

    public static DevelopmentResourceSettings Load(string pluginDirectory)
    {
        var candidatePath = Path.GetFullPath(string.IsNullOrWhiteSpace(pluginDirectory) ? AppContext.BaseDirectory : pluginDirectory);
        var basePath = Directory.Exists(candidatePath) ? candidatePath : AppContext.BaseDirectory;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile(SettingsFileName, optional: true, reloadOnChange: false)
            .Build();

        var settings = new DevelopmentResourceSettings();
        configuration.Bind(settings);
        return settings;
    }
}
