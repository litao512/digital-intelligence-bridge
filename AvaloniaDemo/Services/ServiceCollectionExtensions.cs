using AvaloniaDemo.Configuration;
using AvaloniaDemo.ViewModels;
using AvaloniaDemo.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace AvaloniaDemo.Services;

/// <summary>
/// 服务集合扩展方法
/// 用于注册应用程序依赖注入服务
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加应用程序核心服务
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // 注册配置系统
        services.AddAppConfiguration();

        // 注册日志服务
        services.AddLogging(builder =>
        {
            builder.AddSerilog(dispose: true);
        });

        // 注册泛型日志服务
        services.AddSingleton(typeof(ILoggerService<>), typeof(LoggerService<>));

        // 注册应用程序服务
        services.AddSingleton<IApplicationService, ApplicationService>();

        // 注册托盘服务
        services.AddSingleton<ITrayService, TrayService>();

        // 注：WebView 服务已移除，将作为可选插件在后续版本提供

        return services;
    }

    /// <summary>
    /// 添加视图和视图模型
    /// </summary>
    public static IServiceCollection AddViewsAndViewModels(this IServiceCollection services)
    {
        // 注册主窗口
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowViewModel>();

        return services;
    }
}
