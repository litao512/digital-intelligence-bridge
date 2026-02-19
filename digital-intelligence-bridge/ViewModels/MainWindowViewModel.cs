using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DigitalIntelligenceBridge.Models;
using DigitalIntelligenceBridge.Services;
using Prism.Commands;
using Prism.Mvvm;

namespace DigitalIntelligenceBridge.ViewModels;

/// <summary>
/// 当前显示的视图类型
/// </summary>
public enum MainViewType
{
    Home,           // 首页
    Todo,           // 待办事项
    PatientMgmt,    // 患者管理（占位）
    Schedule,       // 日程安排（占位）
    Settings        // 设置
}

/// <summary>
/// 导航菜单项
/// </summary>
public class MenuItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public MainViewType ViewType { get; set; }
    public bool IsInstalled { get; set; } = true;
    public bool IsPlaceholder { get; set; } = false;
}

/// <summary>
/// 标签页类型
/// </summary>
public enum TabItemType
{
    Home,       // 首页
    Todo,       // 待办事项
    Settings,   // 设置
    PatientMgmt,// 患者管理
    Schedule    // 日程安排
}

/// <summary>
/// 标签页数据模型
/// </summary>
public class TabItemModel : BindableBase
{
    private string _title = string.Empty;
    private string _subtitle = string.Empty;
    private bool _isModified;

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get => _title; set => SetProperty(ref _title, value); }
    public string Subtitle { get => _subtitle; set => SetProperty(ref _subtitle, value); }
    public TabItemType TabType { get; set; }
    public object? Content { get; set; }
    public bool IsModified { get => _isModified; set => SetProperty(ref _isModified, value); }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 主窗口视图模型
/// 使用 Prism 的 BindableBase 和 DelegateCommand
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    private readonly ILoggerService<MainWindowViewModel> _logger;

    // 集合
    public ObservableCollection<TodoItem> TodoItems { get; } = new();
    public ObservableCollection<TodoItem> FilteredTodoItems { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();
    public ObservableCollection<MenuItem> MenuItems { get; } = new();
    public ObservableCollection<TabItemModel> OpenTabs { get; } = new();

    // 属性字段
    private string _newTodoTitle = string.Empty;
    private string _newTodoDescription = string.Empty;
    private TodoItem.PriorityLevel _selectedPriority = TodoItem.PriorityLevel.Normal;
    private string _selectedCategory = "默认";
    private string _newTags = string.Empty;
    private DateTime? _selectedDueDate;
    private TodoItem? _selectedTodoItem;
    private string _title = "通用工具箱";

    // 视图状态字段
    private MainViewType _currentView = MainViewType.Home;
    private string _pageTitle = "首页";
    private string _pageSubtitle = "欢迎使用";
    private TabItemModel? _selectedTab;

    // 搜索和筛选字段
    private string _searchText = string.Empty;
    private string _filterPriorityText = "全部";
    private string _filterStatusText = "全部";

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <summary>
    /// 当前视图类型
    /// </summary>
    public MainViewType CurrentView
    {
        get => _currentView;
        set
        {
            if (SetProperty(ref _currentView, value))
            {
                RaisePropertyChanged(nameof(IsHomeViewVisible));
                RaisePropertyChanged(nameof(IsTodoViewVisible));
                RaisePropertyChanged(nameof(IsSettingsViewVisible));
                RaisePropertyChanged(nameof(IsPatientViewVisible));
                RaisePropertyChanged(nameof(IsScheduleViewVisible));
            }
        }
    }

    /// <summary>
    /// 首页视图是否可见
    /// </summary>
    public bool IsHomeViewVisible => CurrentView == MainViewType.Home;

    /// <summary>
    /// 待办视图是否可见
    /// </summary>
    public bool IsTodoViewVisible => CurrentView == MainViewType.Todo;

    /// <summary>
    /// 设置视图是否可见
    /// </summary>
    public bool IsSettingsViewVisible => CurrentView == MainViewType.Settings;

    /// <summary>
    /// 患者管理视图是否可见
    /// </summary>
    public bool IsPatientViewVisible => CurrentView == MainViewType.PatientMgmt;

    /// <summary>
    /// 日程安排视图是否可见
    /// </summary>
    public bool IsScheduleViewVisible => CurrentView == MainViewType.Schedule;

    public string PageTitle
    {
        get => _pageTitle;
        set => SetProperty(ref _pageTitle, value);
    }

    public string PageSubtitle
    {
        get => _pageSubtitle;
        set => SetProperty(ref _pageSubtitle, value);
    }

    /// <summary>
    /// 当前选中的标签页
    /// </summary>
    public TabItemModel? SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (SetProperty(ref _selectedTab, value) && value != null)
            {
                PageTitle = value.Title;
                PageSubtitle = value.Subtitle;
            }
        }
    }

    public string NewTodoTitle
    {
        get => _newTodoTitle;
        set
        {
            if (SetProperty(ref _newTodoTitle, value))
            {
                AddTodoCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string NewTodoDescription
    {
        get => _newTodoDescription;
        set => SetProperty(ref _newTodoDescription, value);
    }

    public TodoItem.PriorityLevel SelectedPriority
    {
        get => _selectedPriority;
        set => SetProperty(ref _selectedPriority, value);
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
    }

    public string NewTags
    {
        get => _newTags;
        set => SetProperty(ref _newTags, value);
    }

    public DateTime? SelectedDueDate
    {
        get => _selectedDueDate;
        set => SetProperty(ref _selectedDueDate, value);
    }

    public TodoItem? SelectedTodoItem
    {
        get => _selectedTodoItem;
        set => SetProperty(ref _selectedTodoItem, value);
    }

    // 搜索和筛选属性
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    /// <summary>
    /// 优先级筛选文本（"全部"/"高"/"中"/"低"）
    /// </summary>
    public string FilterPriorityText
    {
        get => _filterPriorityText;
        set
        {
            if (SetProperty(ref _filterPriorityText, value))
            {
                ApplyFilter();
            }
        }
    }

    /// <summary>
    /// 状态筛选文本（"全部"/"进行中"/"已完成"）
    /// </summary>
    public string FilterStatusText
    {
        get => _filterStatusText;
        set
        {
            if (SetProperty(ref _filterStatusText, value))
            {
                ApplyFilter();
            }
        }
    }

    // 统计信息
    public int TotalCount => TodoItems.Count;
    public int CompletedCount => TodoItems.Count(x => x.IsCompleted);
    public int PendingCount => TodoItems.Count(x => !x.IsCompleted);
    public int OverdueCount => TodoItems.Count(x => x.IsOverdue);

    // Prism 命令
    public DelegateCommand AddTodoCommand { get; }
    public DelegateCommand<TodoItem?> DeleteTodoCommand { get; }
    public DelegateCommand<TodoItem?> ToggleCompleteCommand { get; }
    public DelegateCommand ClearCompletedCommand { get; }

    // 导航命令
    public DelegateCommand ShowHomeViewCommand { get; }
    public DelegateCommand ShowTodoViewCommand { get; }
    public DelegateCommand ShowSettingsViewCommand { get; }
    public DelegateCommand<MainViewType?> NavigateCommand { get; }

    // 筛选命令
    public DelegateCommand ClearFilterCommand { get; }

    // 标签页命令
    public DelegateCommand<TabItemModel?> CloseTabCommand { get; }

    public MainWindowViewModel(ILoggerService<MainWindowViewModel> logger)
    {
        _logger = logger;

        // 初始化命令
        AddTodoCommand = new DelegateCommand(OnAddTodo, CanAddTodo);
        DeleteTodoCommand = new DelegateCommand<TodoItem?>(OnDeleteTodo);
        ToggleCompleteCommand = new DelegateCommand<TodoItem?>(OnToggleComplete);
        ClearCompletedCommand = new DelegateCommand(OnClearCompleted);

        // 初始化导航命令
        ShowHomeViewCommand = new DelegateCommand(() => NavigateTo(MainViewType.Home));
        ShowTodoViewCommand = new DelegateCommand(() => NavigateTo(MainViewType.Todo));
        ShowSettingsViewCommand = new DelegateCommand(() => NavigateTo(MainViewType.Settings));
        NavigateCommand = new DelegateCommand<MainViewType?>(NavigateTo);

        // 初始化筛选命令
        ClearFilterCommand = new DelegateCommand(ClearFilter);

        // 初始化标签页命令
        CloseTabCommand = new DelegateCommand<TabItemModel?>(OnCloseTab);

        // 初始化分类
        InitializeCategories();

        // 初始化菜单项
        InitializeMenuItems();

        // 添加示例数据
        InitializeSampleData();

        // 初始筛选
        ApplyFilter();

        // 初始化首页标签页
        InitializeHomeTab();

        _logger.LogInformation("主窗口视图模型已初始化");
    }

    /// <summary>
    /// 初始化首页标签页
    /// </summary>
    private void InitializeHomeTab()
    {
        var homeTab = new TabItemModel
        {
            Id = "home",
            Title = "首页",
            Subtitle = "欢迎使用",
            TabType = TabItemType.Home
        };
        OpenTabs.Add(homeTab);
        SelectedTab = homeTab;
    }

    /// <summary>
    /// 导航到指定视图类型
    /// </summary>
    private void NavigateTo(MainViewType? viewType)
    {
        if (!viewType.HasValue) return;

        var type = viewType.Value;
        TabItemModel? existingTab = null;

        // 根据视图类型查找或创建标签页
        switch (type)
        {
            case MainViewType.Home:
                existingTab = OpenTabs.FirstOrDefault(t => t.TabType == TabItemType.Home);
                if (existingTab == null)
                {
                    existingTab = new TabItemModel
                    {
                        Id = "home",
                        Title = "首页",
                        Subtitle = "欢迎使用",
                        TabType = TabItemType.Home
                    };
                    OpenTabs.Add(existingTab);
                }
                break;

            case MainViewType.Todo:
                existingTab = OpenTabs.FirstOrDefault(t => t.TabType == TabItemType.Todo);
                if (existingTab == null)
                {
                    existingTab = new TabItemModel
                    {
                        Id = $"todo_{Guid.NewGuid():N}",
                        Title = "待办事项",
                        Subtitle = "管理您的日常任务",
                        TabType = TabItemType.Todo
                    };
                    OpenTabs.Add(existingTab);
                }
                break;

            case MainViewType.Settings:
                existingTab = OpenTabs.FirstOrDefault(t => t.TabType == TabItemType.Settings);
                if (existingTab == null)
                {
                    existingTab = new TabItemModel
                    {
                        Id = $"settings_{Guid.NewGuid():N}",
                        Title = "设置",
                        Subtitle = "配置应用程序选项",
                        TabType = TabItemType.Settings
                    };
                    OpenTabs.Add(existingTab);
                }
                break;

            case MainViewType.PatientMgmt:
                existingTab = OpenTabs.FirstOrDefault(t => t.TabType == TabItemType.PatientMgmt);
                if (existingTab == null)
                {
                    existingTab = new TabItemModel
                    {
                        Id = $"patient_{Guid.NewGuid():N}",
                        Title = "患者管理",
                        Subtitle = "功能开发中...",
                        TabType = TabItemType.PatientMgmt
                    };
                    OpenTabs.Add(existingTab);
                }
                break;

            case MainViewType.Schedule:
                existingTab = OpenTabs.FirstOrDefault(t => t.TabType == TabItemType.Schedule);
                if (existingTab == null)
                {
                    existingTab = new TabItemModel
                    {
                        Id = $"schedule_{Guid.NewGuid():N}",
                        Title = "日程安排",
                        Subtitle = "功能开发中...",
                        TabType = TabItemType.Schedule
                    };
                    OpenTabs.Add(existingTab);
                }
                break;
        }

        if (existingTab != null)
        {
            SelectedTab = existingTab;
            CurrentView = type;
            _logger.LogInformation("导航到视图: {View}, 标签页: {TabId}", type, existingTab.Id);
        }
    }

    /// <summary>
    /// 关闭标签页
    /// </summary>
    private void OnCloseTab(TabItemModel? tab)
    {
        if (tab == null) return;

        // 不允许关闭最后一个标签页（首页）
        if (tab.TabType == TabItemType.Home && OpenTabs.Count(t => t.TabType == TabItemType.Home) == 1)
        {
            return;
        }

        var index = OpenTabs.IndexOf(tab);
        OpenTabs.Remove(tab);

        // 如果关闭的是当前选中的标签页，切换到相邻的标签页
        if (SelectedTab == tab && OpenTabs.Count > 0)
        {
            var newIndex = Math.Min(index, OpenTabs.Count - 1);
            SelectedTab = OpenTabs[newIndex];
            UpdateCurrentViewFromTab(SelectedTab);
        }

        _logger.LogInformation("关闭标签页: {TabId}", tab.Id);
    }

    /// <summary>
    /// 根据标签页类型更新当前视图
    /// </summary>
    private void UpdateCurrentViewFromTab(TabItemModel tab)
    {
        CurrentView = tab.TabType switch
        {
            TabItemType.Home => MainViewType.Home,
            TabItemType.Todo => MainViewType.Todo,
            TabItemType.Settings => MainViewType.Settings,
            TabItemType.PatientMgmt => MainViewType.PatientMgmt,
            TabItemType.Schedule => MainViewType.Schedule,
            _ => MainViewType.Home
        };
    }

    private void InitializeCategories()
    {
        Categories.Add("默认");
        Categories.Add("工作");
        Categories.Add("个人");
        Categories.Add("学习");
        Categories.Add("购物");
        Categories.Add("健康");
    }

    private void InitializeMenuItems()
    {
        MenuItems.Add(new MenuItem
        {
            Id = "home",
            Name = "首页",
            Icon = "\uf015",
            ViewType = MainViewType.Home,
            IsInstalled = true
        });

        MenuItems.Add(new MenuItem
        {
            Id = "todo",
            Name = "待办事项",
            Icon = "\uf0ae",
            ViewType = MainViewType.Todo,
            IsInstalled = true
        });

        // 占位插件 - 患者管理
        MenuItems.Add(new MenuItem
        {
            Id = "patient",
            Name = "患者管理",
            Icon = "\uf0c0",
            ViewType = MainViewType.PatientMgmt,
            IsInstalled = false,
            IsPlaceholder = true
        });

        // 占位插件 - 日程安排
        MenuItems.Add(new MenuItem
        {
            Id = "schedule",
            Name = "日程安排",
            Icon = "\uf133",
            ViewType = MainViewType.Schedule,
            IsInstalled = false,
            IsPlaceholder = true
        });

        _logger.LogInformation("菜单项初始化完成，共 {Count} 项", MenuItems.Count);
    }

    private void InitializeSampleData()
    {
        TodoItems.Add(new TodoItem
        {
            Title = "学习 Avalonia 基础",
            Description = "了解 XAML 布局和数据绑定",
            Priority = TodoItem.PriorityLevel.High,
            Category = "学习",
            Tags = new List<string> { "技术", "UI" },
            DueDate = DateTime.Now.AddDays(3)
        });
        TodoItems.Add(new TodoItem
        {
            Title = "创建第一个 Avalonia 项目",
            Description = "使用 MVVM 模式开发",
            Priority = TodoItem.PriorityLevel.Normal,
            Category = "工作",
            Tags = new List<string> { "项目", "开发" }
        });
        TodoItems.Add(new TodoItem
        {
            Title = "购买 groceries",
            Description = "牛奶、面包、鸡蛋",
            Priority = TodoItem.PriorityLevel.Low,
            Category = "购物",
            Tags = new List<string> { "日常" },
            DueDate = DateTime.Now.AddDays(1)
        });
        TodoItems.Add(new TodoItem
        {
            Title = "晨跑 5 公里",
            Description = "保持健康",
            Priority = TodoItem.PriorityLevel.Normal,
            Category = "健康",
            Tags = new List<string> { "运动" },
            IsCompleted = true
        });
        TodoItems.Add(new TodoItem
        {
            Title = "准备周会报告",
            Description = "总结本周工作进展",
            Priority = TodoItem.PriorityLevel.High,
            Category = "工作",
            Tags = new List<string> { "会议", "汇报" },
            DueDate = DateTime.Now.AddDays(-1) // 已逾期
        });
    }

    private bool CanAddTodo()
    {
        return !string.IsNullOrWhiteSpace(NewTodoTitle);
    }

    private void OnAddTodo()
    {
        var tags = NewTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                          .Where(t => !string.IsNullOrWhiteSpace(t))
                          .ToList();

        var newItem = new TodoItem
        {
            Title = NewTodoTitle,
            Description = NewTodoDescription,
            Priority = SelectedPriority,
            Category = SelectedCategory,
            Tags = tags,
            DueDate = SelectedDueDate
        };

        TodoItems.Add(newItem);

        // 清空输入
        NewTodoTitle = string.Empty;
        NewTodoDescription = string.Empty;
        SelectedPriority = TodoItem.PriorityLevel.Normal;
        SelectedCategory = "默认";
        NewTags = string.Empty;
        SelectedDueDate = null;

        // 更新统计和筛选
        RaisePropertyChanged(nameof(TotalCount));
        RaisePropertyChanged(nameof(PendingCount));
        ApplyFilter();

        _logger.LogInformation($"添加新任务: {newItem.Title}");
    }

    private void OnDeleteTodo(TodoItem? item)
    {
        if (item != null)
        {
            TodoItems.Remove(item);
            RaisePropertyChanged(nameof(TotalCount));
            RaisePropertyChanged(nameof(CompletedCount));
            RaisePropertyChanged(nameof(PendingCount));
            ApplyFilter();
            _logger.LogInformation($"删除任务: {item.Title}");
        }
    }

    private void OnToggleComplete(TodoItem? item)
    {
        if (item != null)
        {
            item.IsCompleted = !item.IsCompleted;
            if (item.IsCompleted)
            {
                item.CompletedAt = DateTime.Now;
            }
            else
            {
                item.CompletedAt = null;
            }
            RaisePropertyChanged(nameof(CompletedCount));
            RaisePropertyChanged(nameof(PendingCount));
            ApplyFilter();
            _logger.LogInformation($"任务状态变更: {item.Title} -> {(item.IsCompleted ? "已完成" : "进行中")}");
        }
    }

    private void OnClearCompleted()
    {
        var completedItems = TodoItems.Where(x => x.IsCompleted).ToList();
        foreach (var item in completedItems)
        {
            TodoItems.Remove(item);
        }
        RaisePropertyChanged(nameof(TotalCount));
        RaisePropertyChanged(nameof(CompletedCount));
        ApplyFilter();
        _logger.LogInformation($"清除 {completedItems.Count} 个已完成任务");
    }

    /// <summary>
    /// 应用筛选条件
    /// </summary>
    private void ApplyFilter()
    {
        FilteredTodoItems.Clear();

        var query = TodoItems.AsEnumerable();

        // 搜索文本筛选
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.ToLowerInvariant();
            query = query.Where(x =>
                x.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                x.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                x.Tags.Any(t => t.Contains(SearchText, StringComparison.OrdinalIgnoreCase)));
        }

        // 优先级筛选
        if (FilterPriorityText != "全部")
        {
            var priority = FilterPriorityText switch
            {
                "高" => TodoItem.PriorityLevel.High,
                "中" => TodoItem.PriorityLevel.Normal,
                "低" => TodoItem.PriorityLevel.Low,
                _ => (TodoItem.PriorityLevel?)null
            };
            if (priority.HasValue)
            {
                query = query.Where(x => x.Priority == priority.Value);
            }
        }

        // 完成状态筛选
        if (FilterStatusText != "全部")
        {
            var isCompleted = FilterStatusText == "已完成";
            query = query.Where(x => x.IsCompleted == isCompleted);
        }

        foreach (var item in query)
        {
            FilteredTodoItems.Add(item);
        }
    }

    /// <summary>
    /// 清除所有筛选条件
    /// </summary>
    private void ClearFilter()
    {
        SearchText = string.Empty;
        FilterPriorityText = "全部";
        FilterStatusText = "全部";
        _logger.LogInformation("清除所有筛选条件");
    }

    /// <summary>
    /// 获取优先级显示名称
    /// </summary>
    public static string GetPriorityName(TodoItem.PriorityLevel priority)
    {
        return priority switch
        {
            TodoItem.PriorityLevel.High => "高",
            TodoItem.PriorityLevel.Normal => "中",
            TodoItem.PriorityLevel.Low => "低",
            _ => "未知"
        };
    }
}
