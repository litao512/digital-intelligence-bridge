using DigitalIntelligenceBridge.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System;
using System.IO;

namespace DigitalIntelligenceBridge.Services;

/// <summary>
/// 日志配置类
/// </summary>
public static class LoggingConfiguration
{
    /// <summary>
    /// 配置 Serilog 日志
    /// </summary>
    public static void ConfigureSerilog(AppSettings settings)
    {
        var logPath = GetLogPath(settings.Logging.LogPath);

        // 确保日志目录存在
        if (!Directory.Exists(logPath))
        {
            Directory.CreateDirectory(logPath);
        }

        var logLevel = ParseLogLevel(settings.Logging.LogLevel.Default);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(logLevel)
            .MinimumLevel.Override("Microsoft", ParseLogLevel(settings.Logging.LogLevel.Microsoft))
            .Enrich.FromLogContext()
            .WriteTo.Console(
                restrictedToMinimumLevel: LogEventLevel.Information,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                Path.Combine(logPath, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                restrictedToMinimumLevel: LogEventLevel.Debug,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    /// <summary>
    /// 获取日志存储路径
    /// </summary>
    private static string GetLogPath(string configLogPath)
    {
        // 如果是绝对路径，直接使用
        if (Path.IsPathRooted(configLogPath))
        {
            return configLogPath;
        }

        // 否则使用 LocalAppData 目录
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appDataPath, "UniversalTrayTool", configLogPath);
    }

    /// <summary>
    /// 解析日志级别字符串
    /// </summary>
    private static LogEventLevel ParseLogLevel(string level)
    {
        return level.ToLower() switch
        {
            "verbose" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "information" => LogEventLevel.Information,
            "warning" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }

    /// <summary>
    /// 关闭并刷新日志
    /// </summary>
    public static void CloseAndFlush()
    {
        Log.CloseAndFlush();
    }
}

/// <summary>
/// 日志服务接口
/// </summary>
public interface ILoggerService<T>
{
    void LogDebug(string message, params object[] args);
    void LogInformation(string message, params object[] args);
    void LogWarning(string message, params object[] args);
    void LogError(string message, params object[] args);
    void LogError(Exception exception, string message, params object[] args);
    void LogCritical(string message, params object[] args);
}

/// <summary>
/// 日志服务实现
/// </summary>
public class LoggerService<T> : ILoggerService<T>
{
    private readonly ILogger<T> _logger;

    public LoggerService(ILogger<T> logger)
    {
        _logger = logger;
    }

    public void LogDebug(string message, params object[] args)
    {
        _logger.LogDebug(message, args);
    }

    public void LogInformation(string message, params object[] args)
    {
        _logger.LogInformation(message, args);
    }

    public void LogWarning(string message, params object[] args)
    {
        _logger.LogWarning(message, args);
    }

    public void LogError(string message, params object[] args)
    {
        _logger.LogError(message, args);
    }

    public void LogError(Exception exception, string message, params object[] args)
    {
        _logger.LogError(exception, message, args);
    }

    public void LogCritical(string message, params object[] args)
    {
        _logger.LogCritical(message, args);
    }
}
