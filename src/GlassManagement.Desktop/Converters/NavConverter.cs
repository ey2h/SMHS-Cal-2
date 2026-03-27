using System.Globalization;
using Avalonia.Data.Converters;

namespace GlassManagement.Desktop;

/// <summary>导航索引 == 参数 时显示</summary>
public class NavConverter : IValueConverter
{
    public static readonly NavConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int index && parameter is string pStr && int.TryParse(pStr, out int target))
            return index == target;
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>索引 >= 14 时显示数据管理页</summary>
public class NavConverterGte4 : IValueConverter
{
    public static readonly NavConverterGte4 Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int index) return index >= 15;
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
