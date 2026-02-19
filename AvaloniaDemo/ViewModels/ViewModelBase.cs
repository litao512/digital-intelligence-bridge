using Prism.Mvvm;

namespace AvaloniaDemo.ViewModels;

/// <summary>
/// 视图模型基类
/// 使用 Prism 的 BindableBase
/// </summary>
public abstract class ViewModelBase : BindableBase
{
    /// <summary>
    /// 当视图被激活时调用（由导航服务触发）
    /// </summary>
    public virtual void OnNavigatedTo(object? parameter = null)
    {
    }

    /// <summary>
    /// 当视图即将被导航离开时调用
    /// </summary>
    public virtual void OnNavigatedFrom()
    {
    }
}
