using System;
using System.Collections.Generic;
using Prism.Mvvm;

namespace AvaloniaDemo.Models;

/// <summary>
/// 待办事项模型类
/// 使用 Prism 的 BindableBase 实现属性变更通知
/// </summary>
public class TodoItem : BindableBase
{
    private Guid _id = Guid.NewGuid();
    private string _title = string.Empty;
    private string _description = string.Empty;
    private bool _isCompleted;
    private DateTime _createdAt = DateTime.Now;
    private DateTime? _completedAt;
    private PriorityLevel _priority = PriorityLevel.Normal;
    private string _category = "默认";
    private List<string> _tags = new();
    private DateTime? _dueDate;
    private bool _isReminderSet;
    private DateTime? _reminderTime;

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        set
        {
            if (SetProperty(ref _isCompleted, value))
            {
                RaisePropertyChanged(nameof(StatusText));
            }
        }
    }

    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetProperty(ref _createdAt, value);
    }

    public PriorityLevel Priority
    {
        get => _priority;
        set => SetProperty(ref _priority, value);
    }

    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    public List<string> Tags
    {
        get => _tags;
        set => SetProperty(ref _tags, value);
    }

    public DateTime? DueDate
    {
        get => _dueDate;
        set
        {
            if (SetProperty(ref _dueDate, value))
            {
                RaisePropertyChanged(nameof(DueDateText));
                RaisePropertyChanged(nameof(IsOverdue));
            }
        }
    }

    public bool IsReminderSet
    {
        get => _isReminderSet;
        set => SetProperty(ref _isReminderSet, value);
    }

    public DateTime? ReminderTime
    {
        get => _reminderTime;
        set => SetProperty(ref _reminderTime, value);
    }

    public DateTime? CompletedAt
    {
        get => _completedAt;
        set => SetProperty(ref _completedAt, value);
    }

    // 计算属性 - 根据完成状态返回不同的显示文本
    public string StatusText => IsCompleted ? "已完成" : "进行中";

    // 截止日期显示文本
    public string DueDateText => DueDate?.ToString("MM-dd") ?? "无截止日期";

    // 是否已逾期
    public bool IsOverdue => !IsCompleted && DueDate.HasValue && DueDate.Value < DateTime.Now;

    // 标签显示文本
    public string TagsDisplayText => Tags.Count > 0 ? string.Join(", ", Tags) : "无标签";

    // 优先级枚举
    public enum PriorityLevel
    {
        Low,
        Normal,
        High
    }
}
