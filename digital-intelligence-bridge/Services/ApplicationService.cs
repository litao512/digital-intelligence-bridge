using DigitalIntelligenceBridge.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalIntelligenceBridge.Services;

/// <summary>
/// åº”ç”¨ç”Ÿå‘½å‘¨æœŸæœåŠ¡å®žçŽ°
/// </summary>
public class ApplicationService : IApplicationService
{
    private readonly ILogger<ApplicationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IOptions<AppSettings> _appSettings;
    private readonly ITrayService _trayService;
    private readonly ISupabaseService _supabaseService;
    private bool _isInitialized = false;

    public bool IsInitialized => _isInitialized;

    public ApplicationService(
        ILogger<ApplicationService> logger,
        IConfiguration configuration,
        IOptions<AppSettings> appSettings,
        ITrayService trayService,
        ISupabaseService supabaseService)
    {
        _logger = logger;
        _configuration = configuration;
        _appSettings = appSettings;
        _trayService = trayService;
        _supabaseService = supabaseService;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            _logger.LogWarning("åº”ç”¨ç¨‹åºå·²ç»åˆå§‹åŒ–");
            return;
        }

        _logger.LogInformation("æ­£åœ¨åˆå§‹åŒ–åº”ç”¨ç¨‹åº...");

        try
        {
            // ç¡®ä¿é…ç½®ç›®å½•å­˜åœ¨
            EnsureDirectories();

            // é…ç½®æ—¥å¿—ç³»ç»Ÿ
            LoggingConfiguration.ConfigureSerilog(_appSettings.Value);

            _logger.LogInformation("åº”ç”¨ç¨‹åºé…ç½®:");
            _logger.LogInformation("  - åº”ç”¨åç§°: {Name}", _appSettings.Value.Application.Name);
            _logger.LogInformation("  - åº”ç”¨ç‰ˆæœ¬: {Version}", _appSettings.Value.Application.Version);
            _logger.LogInformation("  - æ—¥å¿—è·¯å¾„: {LogPath}", _appSettings.Value.Logging.LogPath);
            _logger.LogInformation("  - æ’ä»¶ç›®å½•: {PluginDir}", _appSettings.Value.Plugin.PluginDirectory);
            _logger.LogInformation("åº”ç”¨ç¨‹åºåˆå§‹åŒ–å®Œæˆ");

            _isInitialized = true;

            _logger.LogInformation("åº”ç”¨ç¨‹åºåˆå§‹åŒ–å®Œæˆ");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "åº”ç”¨ç¨‹åºåˆå§‹åŒ–å¤±è´¥");
            throw;
        }

        await Task.CompletedTask;
    }

    public async Task OnStartedAsync()
    {
        _logger.LogInformation("应用程序已启动");

        if (!_supabaseService.IsConfigured)
        {
            _logger.LogWarning("Supabase 未配置，已跳过启动连通性检测。");
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var connectionResult = await _supabaseService.CheckConnectionAsync(cts.Token).ConfigureAwait(false);

        if (!connectionResult.IsReachable)
        {
            _logger.LogWarning("Supabase 不可达：{Message}", connectionResult.Message);
            return;
        }

        if (!connectionResult.IsSuccess)
        {
            _logger.LogWarning(
                "Supabase 已连接但认证/权限异常。Status={StatusCode}, Message={Message}",
                connectionResult.StatusCode,
                connectionResult.Message);
            return;
        }

        var tableResult = await _supabaseService.CheckTableAccessAsync("todos", cts.Token).ConfigureAwait(false);
        var schema = string.IsNullOrWhiteSpace(_appSettings.Value.Supabase.Schema)
            ? "public"
            : _appSettings.Value.Supabase.Schema;

        if (tableResult.IsSuccess)
        {
            _logger.LogInformation("Supabase 表访问正常：{Schema}.todos", schema);
        }
        else
        {
            _logger.LogWarning(
                "Supabase 表访问失败：{Schema}.todos, Status={StatusCode}, Message={Message}",
                schema,
                tableResult.StatusCode,
                tableResult.Message);
        }
    }
public async Task OnShutdownAsync()
    {
        _logger.LogInformation("åº”ç”¨ç¨‹åºæ­£åœ¨å…³é—­...");

        try
        {
            // ä¿å­˜é…ç½®
            SaveConfiguration();

            // å…³é—­æ—¥å¿—
            LoggingConfiguration.CloseAndFlush();

            _logger.LogInformation("åº”ç”¨ç¨‹åºå·²å…³é—­");
        }
        catch (Exception ex)
        {
            // ä½¿ç”¨ Console è¾“å‡ºï¼Œå› ä¸ºæ—¥å¿—å¯èƒ½å·²ç»å…³é—­
            Console.WriteLine($"å…³é—­æ—¶å‘ç”Ÿé”™è¯¯: {ex.Message}");
        }

        await Task.CompletedTask;
    }

    public string GetVersion()
    {
        // ä¼˜å…ˆä½¿ç”¨é…ç½®æ–‡ä»¶ä¸­çš„ç‰ˆæœ¬
        var configVersion = _appSettings.Value.Application.Version;
        if (!string.IsNullOrEmpty(configVersion) && configVersion != "1.0.0")
        {
            return configVersion;
        }

        // å¦åˆ™ä½¿ç”¨ç¨‹åºé›†ç‰ˆæœ¬
        var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
        return assemblyVersion?.ToString(3) ?? "1.0.0";
    }

    public string GetApplicationName()
    {
        return _appSettings.Value.Application.Name;
    }

    /// <summary>
    /// ç¡®ä¿å¿…è¦çš„ç›®å½•å­˜åœ¨
    /// </summary>
    private void EnsureDirectories()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appDataPath, "UniversalTrayTool");

        // åˆ›å»ºåº”ç”¨ä¸»ç›®å½•
        if (!Directory.Exists(appFolder))
        {
            Directory.CreateDirectory(appFolder);
            _logger.LogDebug("åˆ›å»ºåº”ç”¨ç›®å½•: {Path}", appFolder);
        }

        // åˆ›å»ºæ—¥å¿—ç›®å½•
        var logPath = Path.Combine(appFolder, _appSettings.Value.Logging.LogPath);
        if (!Directory.Exists(logPath))
        {
            Directory.CreateDirectory(logPath);
            _logger.LogDebug("åˆ›å»ºæ—¥å¿—ç›®å½•: {Path}", logPath);
        }

        // åˆ›å»ºæ’ä»¶ç›®å½•
        var pluginPath = Path.Combine(appFolder, _appSettings.Value.Plugin.PluginDirectory);
        if (!Directory.Exists(pluginPath))
        {
            Directory.CreateDirectory(pluginPath);
            _logger.LogDebug("åˆ›å»ºæ’ä»¶ç›®å½•: {Path}", pluginPath);
        }
    }

    /// <summary>
    /// ä¿å­˜å½“å‰é…ç½®
    /// </summary>
    private void SaveConfiguration()
    {
        try
        {
            _configuration.SaveAppSettings(_appSettings.Value);
            _logger.LogDebug("é…ç½®å·²ä¿å­˜");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ä¿å­˜é…ç½®å¤±è´¥");
        }
    }
}

