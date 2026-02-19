using AvaloniaDemo.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace AvaloniaDemo.Services;

/// <summary>
/// 应用生命周期服务实现
/// </summary>
public class ApplicationService : IApplicationService
{
    private readonly ILogger<ApplicationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IOptions<AppSettings> _appSettings;
    private readonly ITrayService _trayService;
    private bool _isInitialized = false;

    public bool IsInitialized => _isInitialized;

    public ApplicationService(
        ILogger<ApplicationService> logger,
        IConfiguration configuration,
        IOptions<AppSettings> appSettings,
        ITrayService trayService)
    {
        _logger = logger;
        _configuration = configuration;
        _appSettings = appSettings;
        _trayService = trayService;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            _logger.LogWarning("应用程序已经初始化");
            return;
        }

        _logger.LogInformation("正在初始化应用程序...");

        try
        {
            // 确保配置目录存在
            EnsureDirectories();

            // 配置日志系统
            LoggingConfiguration.ConfigureSerilog(_appSettings.Value);

            _logger.LogInformation("应用程序配置:");
            _logger.LogInformation("  - 应用名称: {Name}", _appSettings.Value.Application.Name);
            _logger.LogInformation("  - 应用版本: {Version}", _appSettings.Value.Application.Version);
            _logger.LogInformation("  - 日志路径: {LogPath}", _appSettings.Value.Logging.LogPath);
            _logger.LogInformation("  - 插件目录: {PluginDir}", _appSettings.Value.Plugin.PluginDirectory);
            _logger.LogInformation("应用程序初始化完成");

            _isInitialized = true;

            _logger.LogInformation("应用程序初始化完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "应用程序初始化失败");
            throw;
        }

        await Task.CompletedTask;
    }

    public async Task OnStartedAsync()
    {
        _logger.LogInformation("应用程序已启动");

        // 可以在这里加载插件、连接服务器等

        await Task.CompletedTask;
    }

    public async Task OnShutdownAsync()
    {
        _logger.LogInformation("应用程序正在关闭...");

        try
        {
            // 保存配置
            SaveConfiguration();

            // 关闭日志
            LoggingConfiguration.CloseAndFlush();

            _logger.LogInformation("应用程序已关闭");
        }
        catch (Exception ex)
        {
            // 使用 Console 输出，因为日志可能已经关闭
            Console.WriteLine($"关闭时发生错误: {ex.Message}");
        }

        await Task.CompletedTask;
    }

    public string GetVersion()
    {
        // 优先使用配置文件中的版本
        var configVersion = _appSettings.Value.Application.Version;
        if (!string.IsNullOrEmpty(configVersion) && configVersion != "1.0.0")
        {
            return configVersion;
        }

        // 否则使用程序集版本
        var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
        return assemblyVersion?.ToString(3) ?? "1.0.0";
    }

    public string GetApplicationName()
    {
        return _appSettings.Value.Application.Name;
    }

    /// <summary>
    /// 确保必要的目录存在
    /// </summary>
    private void EnsureDirectories()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appDataPath, "UniversalTrayTool");

        // 创建应用主目录
        if (!Directory.Exists(appFolder))
        {
            Directory.CreateDirectory(appFolder);
            _logger.LogDebug("创建应用目录: {Path}", appFolder);
        }

        // 创建日志目录
        var logPath = Path.Combine(appFolder, _appSettings.Value.Logging.LogPath);
        if (!Directory.Exists(logPath))
        {
            Directory.CreateDirectory(logPath);
            _logger.LogDebug("创建日志目录: {Path}", logPath);
        }

        // 创建插件目录
        var pluginPath = Path.Combine(appFolder, _appSettings.Value.Plugin.PluginDirectory);
        if (!Directory.Exists(pluginPath))
        {
            Directory.CreateDirectory(pluginPath);
            _logger.LogDebug("创建插件目录: {Path}", pluginPath);
        }
    }

    /// <summary>
    /// 保存当前配置
    /// </summary>
    private void SaveConfiguration()
    {
        try
        {
            _configuration.SaveAppSettings(_appSettings.Value);
            _logger.LogDebug("配置已保存");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "保存配置失败");
        }
    }
}
