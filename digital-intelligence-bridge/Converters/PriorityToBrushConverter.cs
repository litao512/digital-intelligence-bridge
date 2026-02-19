using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using DigitalIntelligenceBridge.Models;

namespace DigitalIntelligenceBridge.Converters;

/// <summary>
/// 值转换器 - 将优先级枚举转换为对应的 Brush
/// 类似于 WinForm 的 IValueConverter，用于数据绑定时的值转换
/// </summary>
public class PriorityToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TodoItem.PriorityLevel priority)
        {
            return priority switch
            {
                TodoItem.PriorityLevel.High => new SolidColorBrush(Color.Parse("#D13438")),
                TodoItem.PriorityLevel.Normal => new SolidColorBrush(Color.Parse("#0078D4")),
                TodoItem.PriorityLevel.Low => new SolidColorBrush(Color.Parse("#107C10")),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
