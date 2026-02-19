using Avalonia;
using DigitalIntelligenceBridge;
using Serilog;
using System;

namespace DigitalIntelligenceBridge;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            // 启动 Avalonia 应用
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            // 在日志系统初始化前，使用 Console 输出错误
            Console.WriteLine($"应用程序启动失败: {ex}");
            throw;
        }
        finally
        {
            // 确保日志被刷新
            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
