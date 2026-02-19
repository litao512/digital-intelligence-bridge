using Avalonia.Controls;
using System;

namespace DigitalIntelligenceBridge.Services;

/// <summary>
/// 托盘服务接口
/// 负责管理系统托盘图标的显示和菜单管理
/// </summary>
public interface ITrayService
{
    /// <summary>
    /// 初始化托盘服务
    /// </summary>
    void Initialize(Window mainWindow);

    /// <summary>
    /// 显示主窗口
    /// </summary>
    void ShowWindow();

    /// <summary>
    /// 隐藏主窗口
    /// </summary>
    void HideWindow();

    /// <summary>
    /// 切换窗口显示/隐藏状态
    /// </summary>
    void ToggleWindow();

    /// <summary>
    /// 完全退出应用程序
    /// </summary>
    void ExitApplication();

    /// <summary>
    /// 添加动态菜单项
    /// </summary>
    /// <param name="header">菜单项文本</param>
    /// <param name="callback">点击回调</param>
    /// <param name="parentPath">父菜单路径，用于创建子菜单</param>
    void AddMenuItem(string header, Action callback, string? parentPath = null);

    /// <summary>
    /// 移除菜单项
    /// </summary>
    /// <param name="path">菜单项路径</param>
    void RemoveMenuItem(string path);

    /// <summary>
    /// 添加分隔线
    /// </summary>
    /// <param name="parentPath">父菜单路径</param>
    void AddSeparator(string? parentPath = null);

    /// <summary>
    /// 更新托盘提示文本
    /// </summary>
    void SetTooltip(string tooltip);

    /// <summary>
    /// 主窗口是否可见
    /// </summary>
    bool IsWindowVisible { get; }

    /// <summary>
    /// 应用程序是否正在退出
    /// </summary>
    bool IsExiting { get; }

    /// <summary>
    /// 设置是否显示托盘通知
    /// </summary>
    /// <param name="show">是否显示</param>
    void SetShowNotifications(bool show);

    /// <summary>
    /// 显示通知（如果启用）
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="message">消息内容</param>
    void ShowNotification(string title, string message);
}
