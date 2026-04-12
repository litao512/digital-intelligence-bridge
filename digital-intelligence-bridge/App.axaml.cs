using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using DigitalIntelligenceBridge.Plugin.Host;
using DigitalIntelligenceBridge.ViewModels;
using DigitalIntelligenceBridge.Views;
using DigitalIntelligenceBridge.Services;
using DigitalIntelligenceBridge.Configuration;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using Serilog;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace DigitalIntelligenceBridge;

public partial class App : PrismApplication
{
    private Window? _mainWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        base.Initialize();
    }

    protected override AvaloniaObject CreateShell()
    {
        var appSettings = Container.Resolve<IOptions<AppSettings>>();
        var appLogger = Container.Resolve<ILoggerService<App>>();
        var releaseCenterService = Container.Resolve<IReleaseCenterService>();
        var activationResult = releaseCenterService.ActivatePreparedPluginPackagesAsync().GetAwaiter().GetResult();
        if (!activationResult.IsSuccess)
        {
            appLogger.LogWarning("启动时插件激活失败: {Detail}", activationResult.Detail);
        }
        else if (activationResult.ActivatedCount > 0)
        {
            appLogger.LogInformation("启动时已激活 {Count} 个插件目录", activationResult.ActivatedCount);
        }
        var runtimePlugins = LoadRuntimePlugins(
            DigitalIntelligenceBridge.Configuration.ConfigurationExtensions.GetConfigRootDirectory(),
            appSettings,
            Container.Resolve<PluginCatalogService>(),
            Container.Resolve<PluginLoaderService>(),
            Container.Resolve<ILoggerService<App>>());
        var externalMenus = runtimePlugins
            .Where(plugin => plugin.Module is not null)
            .SelectMany(plugin => plugin.Module!.CreateMenuItems())
            .OrderBy(menu => menu.Order)
            .ToList();

        _mainWindow = Container.Resolve<MainWindow>();
        _mainWindow.DataContext = new MainWindowViewModel(
            Container.Resolve<ILoggerService<MainWindowViewModel>>(),
            appSettings,
            Container.Resolve<ITodoRepository>(),
            Container.Resolve<DrugImportViewModel>(),
            externalMenus,
            runtimePlugins,
            Container.Resolve<IApplicationService>(),
            releaseCenterService);
        return _mainWindow;
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // 配置日志（首先配置，以便其他服务可以使用）
        ConfigureLogging();

        // 注册 Microsoft.Extensions.Logging 服务
        var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog(dispose: true));
        containerRegistry.RegisterInstance<ILoggerFactory>(loggerFactory);
        containerRegistry.RegisterSingleton(typeof(ILogger<>), typeof(Logger<>));

        // 注册配置
        RegisterConfiguration(containerRegistry);

        // 注册应用程序服务
        containerRegistry.RegisterSingleton<ITrayService, TrayService>();
        containerRegistry.RegisterSingleton<IApplicationService, ApplicationService>();
        containerRegistry.RegisterInstance(new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        });
        containerRegistry.RegisterSingleton<ISupabaseService, SupabaseService>();
        containerRegistry.RegisterSingleton<IReleaseCenterService, ReleaseCenterService>();
        containerRegistry.RegisterSingleton<ITodoRepository, SupabaseTodoRepository>();
        containerRegistry.RegisterSingleton<PluginCatalogService>();
        containerRegistry.RegisterSingleton<PluginLoaderService>();
        containerRegistry.RegisterSingleton<IDrugExcelImportService, DrugExcelImportService>();
        containerRegistry.RegisterSingleton<IDrugImportRepository, DrugImportRepository>();
        containerRegistry.RegisterSingleton<IDrugCatalogSyncRepository, DrugImportRepository>();
        containerRegistry.RegisterSingleton<IDrugImportPipelineService, DrugImportPipelineService>();
        containerRegistry.RegisterSingleton<ISqlServerDrugSyncService, SqlServerDrugSyncService>();
        containerRegistry.RegisterSingleton<DrugImportViewModel>();

        // 注：WebView 服务已移除，将作为可选插件在后续版本提供

        containerRegistry.RegisterSingleton(typeof(ILoggerService<>), typeof(LoggerService<>));

        // 注册 Views 和 ViewModels
        containerRegistry.RegisterForNavigation<MainWindow, MainWindowViewModel>();
        containerRegistry.RegisterForNavigation<SettingsView, SettingsViewModel>();
        containerRegistry.RegisterForNavigation<DrugImportView, DrugImportViewModel>();
    }

    private void RegisterConfiguration(IContainerRegistry containerRegistry)
    {
        // 获取用户配置路径
        var userConfigPath = DigitalIntelligenceBridge.Configuration.ConfigurationExtensions.GetConfigFilePath();
        var defaultConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        // 如果用户配置文件不存在，从程序目录复制默认配置
        if (!File.Exists(userConfigPath))
        {
            if (File.Exists(defaultConfigPath))
            {
                File.Copy(defaultConfigPath, userConfigPath);
            }
        }
        else
        {
            DigitalIntelligenceBridge.Configuration.ConfigurationExtensions.RepairReleaseCenterSettings(userConfigPath, defaultConfigPath);
        }

        // 构建配置
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile(userConfigPath, optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        // 注册 IConfiguration
        containerRegistry.RegisterInstance<IConfiguration>(configuration);

        // 绑定并注册 AppSettings
        var appSettings = new AppSettings();
        configuration.Bind(appSettings);
        ConfigurationSafetyValidator.EnsureSafeUserConfiguration(appSettings, userConfigPath);
        containerRegistry.RegisterInstance<IOptions<AppSettings>>(Options.Create(appSettings));
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        // 后续注册插件模块
        // moduleCatalog.AddModule<TodoModule>();
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                // 获取服务实例
                var trayService = Container.Resolve<ITrayService>();
                var appService = Container.Resolve<IApplicationService>();
                var logger = Container.Resolve<ILoggerService<App>>();

                logger.LogInformation("应用程序框架初始化开始");

                // 避免重复验证
                DisableAvaloniaDataAnnotationValidation();

                // 设置关机模式
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                if (_mainWindow != null)
                {
                    desktop.MainWindow = _mainWindow;
                    _mainWindow.Show();
                    _mainWindow.WindowState = WindowState.Normal;
                    _mainWindow.Activate();
                }

                // 初始化托盘服务
                trayService.Initialize(_mainWindow!);

                // 启动后台异步初始化，避免在 UI 线程上阻塞。
                _ = InitializeApplicationAsync(appService, logger);

                logger.LogInformation("应用程序框架初始化完成");

                // 处理应用程序退出
                desktop.Exit += (s, e) =>
                {
                    try
                    {
                        logger.LogInformation("应用程序正在退出...");
                        appService.OnShutdownAsync().GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"退出时发生错误: {ex.Message}");
                    }
                };
            }
            catch (Exception ex)
            {
                var logger = Container.Resolve<ILoggerService<App>>();
                logger.LogError($"应用程序初始化失败: {ex}");
                throw;
            }
        }
    }

    private void ConfigureLogging()
    {
        var logRoot = DigitalIntelligenceBridge.Configuration.ConfigurationExtensions.GetLogsDirectory();
        Directory.CreateDirectory(logRoot);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(logRoot, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();
    }

    private static async Task InitializeApplicationAsync(IApplicationService appService, ILoggerService<App> logger)
    {
        try
        {
            await appService.InitializeAsync().ConfigureAwait(false);
            await appService.OnStartedAsync().ConfigureAwait(false);
        }
        catch (Exception startupEx)
        {
            logger.LogError($"应用程序启动后处理失败: {startupEx}");
        }
    }

    /// <summary>
    /// 托盘图标点击事件 - 切换窗口显示/隐藏
    /// </summary>
    public void TrayIcon_Clicked(object? sender, EventArgs e)
    {
        var trayService = Container.Resolve<ITrayService>();
        trayService?.ToggleWindow();
    }

    /// <summary>
    /// 显示窗口菜单项点击
    /// </summary>
    public void ShowWindow_Click(object? sender, EventArgs e)
    {
        var trayService = Container.Resolve<ITrayService>();
        trayService?.ShowWindow();
    }

    /// <summary>
    /// 退出菜单项点击 - 完全退出应用
    /// </summary>
    public void Exit_Click(object? sender, EventArgs e)
    {
        var trayService = Container.Resolve<ITrayService>();
        trayService?.ExitApplication();
    }

    /// <summary>
    /// 完全退出应用程序
    /// </summary>
    public void ExitApplication()
    {
        var trayService = Container.Resolve<ITrayService>();
        trayService?.ExitApplication();
    }

    /// <summary>
    /// 公开主窗口引用供外部使用
    /// </summary>
    public new Window? MainWindow => _mainWindow;

    /// <summary>
    /// 获取当前 App 实例
    /// </summary>
    public static new App? Current => Application.Current as App;

    /// <summary>
    /// 获取 Prism 容器（用于从视图访问服务）
    /// </summary>
    public static IContainerProvider? ServiceContainer => Current?.Container;

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    public static IReadOnlyList<LoadedPlugin> LoadRuntimePlugins(
        string appBaseDirectory,
        IOptions<AppSettings> appSettings,
        PluginCatalogService catalogService,
        PluginLoaderService loaderService,
        ILoggerService<App> logger)
    {
        var pluginRoot = DigitalIntelligenceBridge.Configuration.ConfigurationExtensions.GetRuntimePluginsDirectory(appSettings.Value.Plugin.PluginDirectory);
        var discoveredPlugins = catalogService.DiscoverManifests(pluginRoot);
        var hostVersion = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        var loadedPlugins = new List<LoadedPlugin>();

        foreach (var plugin in discoveredPlugins)
        {
            var loadedPlugin = loaderService.LoadPlugin(plugin, hostVersion);
            if (loadedPlugin.Module is null)
            {
                logger.LogWarning("插件加载失败: {PluginId} - {Error}", loadedPlugin.Manifest.Id, loadedPlugin.ErrorMessage);
                loadedPlugins.Add(loadedPlugin);
                continue;
            }

            try
            {
                loadedPlugin.Module.Initialize(new PluginHostContext(
                    hostVersion,
                    loadedPlugin.PluginDirectory,
                    message => logger.LogInformation("[{PluginId}] {Message}", loadedPlugin.Manifest.Id, message)));
            }
            catch (Exception ex)
            {
                loadedPlugin.ErrorMessage = ex.Message;
                loadedPlugin.Module = null;
                logger.LogError(ex, "插件初始化失败: {PluginId}", loadedPlugin.Manifest.Id);
            }

            loadedPlugins.Add(loadedPlugin);
        }

        return loadedPlugins;
    }
}



