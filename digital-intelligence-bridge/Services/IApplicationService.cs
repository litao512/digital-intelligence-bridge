using System.Threading.Tasks;

namespace DigitalIntelligenceBridge.Services;

/// <summary>
/// 应用生命周期服务接口
/// 统一管理应用启动和关闭流程
/// </summary>
public interface IApplicationService
{
    /// <summary>
    /// 应用程序初始化
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// 应用程序启动完成
    /// </summary>
    Task OnStartedAsync();

    /// <summary>
    /// 应用程序正在关闭
    /// </summary>
    Task OnShutdownAsync();

    /// <summary>
    /// 获取应用程序版本
    /// </summary>
    string GetVersion();

    /// <summary>
    /// 获取应用程序名称
    /// </summary>
    string GetApplicationName();

    /// <summary>
    /// 应用程序是否已初始化
    /// </summary>
    bool IsInitialized { get; }
}
