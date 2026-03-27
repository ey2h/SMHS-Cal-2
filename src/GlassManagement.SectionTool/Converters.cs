using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace GlassManagement.SectionTool;

public static class Converters
{
    public static readonly IValueConverter StringEquals = new StringEqualsConverter();
}

public class StringEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true)
            return parameter?.ToString();
        return Avalonia.Data.BindingOperations.DoNothing;
    }
}
