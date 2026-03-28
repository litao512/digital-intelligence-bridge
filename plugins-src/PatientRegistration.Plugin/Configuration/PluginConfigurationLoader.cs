using Microsoft.Extensions.Configuration;

namespace PatientRegistration.Plugin.Configuration;

public static class PluginConfigurationLoader
{
    public const string SettingsFileName = "plugin.settings.json";
    public const string EnvironmentPrefix = "PATIENT_REGISTRATION__";

    public static PluginSettings Load(string pluginDirectory)
    {
        var candidatePath = Path.GetFullPath(string.IsNullOrWhiteSpace(pluginDirectory) ? AppContext.BaseDirectory : pluginDirectory);
        var basePath = Directory.Exists(candidatePath) ? candidatePath : AppContext.BaseDirectory;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile(SettingsFileName, optional: true, reloadOnChange: false)
            .AddEnvironmentVariables(EnvironmentPrefix)
            .Build();

        var settings = new PluginSettings();
        configuration.Bind(settings);
        return settings;
    }
}
