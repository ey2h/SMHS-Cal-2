using CommunityToolkit.Mvvm.ComponentModel;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>
/// 独立工具屏的一个输入项（标签 + 值 + 单位）。
/// 输入一律以字符串承载，避免 Avalonia 数字绑定在小数点上打断输入。
/// <para>
/// 当 <see cref="Options"/> 非空时该字段渲染为下拉框：此时 <see cref="Text"/> 保存
/// **选中项文本**（与 V31 一致，取值按文案匹配），其余情况渲染为文本框。
/// </para>
/// </summary>
public sealed partial class ToolField : ObservableObject
{
    public ToolField(string label, string text, string unit = "", string[]? options = null)
    {
        Label = label;
        Unit = unit;
        Options = options;
        _text = text;
    }

    /// <summary>字段标题（与 V31 界面文案一致）。</summary>
    public string Label { get; }

    /// <summary>单位后缀，如 "mm" / "MPa" / "N"。</summary>
    public string Unit { get; }

    /// <summary>下拉候选；null 表示普通文本框。</summary>
    public string[]? Options { get; }

    public bool HasOptions => Options is { Length: > 0 };

    [ObservableProperty] private string _text;

    public double AsDouble(double def)
        => double.TryParse(Text?.Trim(), out var v) ? v : def;

    public int AsInt(int def)
        => int.TryParse(Text?.Trim(), out var v) ? v : def;
}
