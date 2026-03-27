using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace GlassManagement.Desktop;

/// <summary>
/// 将 int 值与 ConverterParameter 比较，相等返回 true (用于 RadioButton 绑定)
/// </summary>
public class IntEqualConverter : IValueConverter
{
    public static readonly IntEqualConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intVal && parameter is string pStr && int.TryParse(pStr, out int target))
            return intVal == target;
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter is string pStr && int.TryParse(pStr, out int target))
            return target;
        return Avalonia.Data.BindingOperations.DoNothing;
    }
}
