using Avalonia.Data.Converters;
using DigitalIntelligenceBridge.ViewModels;
using System;
using System.Globalization;

namespace DigitalIntelligenceBridge.Converters;

/// <summary>
/// 将 TabItemType 转换为 Visibility 的转换器
/// </summary>
public class TabTypeToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TabItemType tabType && parameter is string targetTypeStr)
        {
            return tabType.ToString() == targetTypeStr;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
