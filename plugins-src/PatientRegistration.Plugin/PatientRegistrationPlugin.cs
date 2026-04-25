using Avalonia.Controls;
using DigitalIntelligenceBridge.Plugin.Abstractions;
using PatientRegistration.Plugin.Configuration;
using PatientRegistration.Plugin.Services;
using PatientRegistration.Plugin.ViewModels;
using PatientRegistration.Plugin.Views;

namespace PatientRegistration.Plugin;

public class PatientRegistrationPlugin : IPluginModule
{
    private const string PluginId = "patient-registration";
    private const string HomeMenuId = "patient-registration.home";

    private IPluginHostContext? _hostContext;

    public void Initialize(IPluginHostContext context)
    {
        _hostContext = context;
        _hostContext.LogInformation("就诊登记插件已初始化");
    }

    public PluginManifest GetManifest()
    {
        return new PluginManifest
        {
            Id = PluginId,
            Name = "就诊登记",
            Version = "0.1.0",
            EntryAssembly = "PatientRegistration.Plugin.dll",
            EntryType = typeof(PatientRegistrationPlugin).FullName ?? string.Empty,
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
                Name = "就诊登记",
                Icon = "🧾",
                Order = 120
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
        if (settings.DevelopmentMode.Enabled)
        {
            _hostContext?.LogInformation("就诊登记插件当前处于开发模式，本地敏感配置已启用，不适用于正式生产环境");
        }

        var connectionString = RuntimeResourceResolver.ResolveRegistrationDbConnectionString(pluginDirectory, settings, _hostContext);
        var repository = new PatientRegistrationRepository(connectionString);
        var printService = new QrPrintService(settings.Registration.PrintTemplate, pluginDirectory);
        var viewModel = new PatientRegistrationViewModel(repository, printService);
        return new PatientRegistrationHomeView(viewModel);
    }
}
