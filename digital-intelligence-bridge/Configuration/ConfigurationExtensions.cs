using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.IO;

namespace DigitalIntelligenceBridge.Configuration;

/// <summary>
/// 配置服务扩展方法
/// </summary>
public static class ConfigurationExtensions
{
    /// <summary>
    /// 获取应用程序配置文件的完整路径
    /// </summary>
    public static string GetConfigFilePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appDataPath, "UniversalTrayTool");

        if (!Directory.Exists(appFolder))
        {
            Directory.CreateDirectory(appFolder);
        }

        return Path.Combine(appFolder, "appsettings.json");
    }

    /// <summary>
    /// 将配置系统添加到服务容器
    /// </summary>
    public static IServiceCollection AddAppConfiguration(this IServiceCollection services)
    {
        // 获取用户配置路径
        var userConfigPath = GetConfigFilePath();

        // 如果用户配置文件不存在，从程序目录复制默认配置
        if (!File.Exists(userConfigPath))
        {
            var defaultConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(defaultConfigPath))
            {
                File.Copy(defaultConfigPath, userConfigPath);
            }
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile(userConfigPath, optional: true, reloadOnChange: true)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // 绑定配置到强类型对象
        services.Configure<AppSettings>(options =>
        {
            configuration.Bind(options);
        });

        return services;
    }

    /// <summary>
    /// 保存当前配置到用户配置文件
    /// </summary>
    public static void SaveAppSettings(this IConfiguration configuration, AppSettings settings)
    {
        var configPath = GetConfigFilePath();
        var json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(configPath, json);
    }

    /// <summary>
    /// 获取配置项的快速访问方法
    /// </summary>
    public static T? GetValue<T>(this IConfiguration configuration, string key, T? defaultValue = default)
    {
        return configuration.GetValue(key, defaultValue);
    }
}
