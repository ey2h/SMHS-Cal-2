using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace GlassManagement.Desktop.Converters;

/// <summary>
/// 字符串相等转换器：用于「材质单选组」——9 个 RadioButton 共享一个
/// <c>Session.BeamMaterial</c> 字符串属性，用 ConverterParameter 各自比较。
/// 单向（IsChecked 由 VM 决定，点击走 Command），避免 TwoWay 到 string 的非法绑定。
/// </summary>
public sealed class StringEqualsConverter : IValueConverter
{
    public static readonly StringEqualsConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.Equals(value as string, parameter as string, StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
