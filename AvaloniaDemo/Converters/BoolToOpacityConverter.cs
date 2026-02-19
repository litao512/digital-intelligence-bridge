using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace AvaloniaDemo.Converters;

/// <summary>
/// 布尔值转透明度转换器
/// true = 1.0 (完全不透明)
/// false = 0.5 (半透明)
/// </summary>
public class BoolToOpacityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isInstalled)
        {
            return isInstalled ? 1.0 : 0.5;
        }
        return 1.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
