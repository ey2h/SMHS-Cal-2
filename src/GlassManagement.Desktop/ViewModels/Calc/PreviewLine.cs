using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>
/// 预演输出的一行（立柱屏 / 其他屏共用）。
/// 属性名与 MultiSpanViewModel.ResultLine 保持一致，XAML 可直接绑定 Text / Color / Decoration。
/// </summary>
public partial class PreviewLine : ObservableObject
{
    [ObservableProperty] private string _text = "";
    [ObservableProperty] private IBrush _color = Brushes.Black;
    [ObservableProperty] private TextDecorationCollection? _decoration;

    public PreviewLine() { }

    public PreviewLine(string text, IBrush color, bool underline = false)
    {
        _text = text;
        _color = color;
        _decoration = underline ? Avalonia.Media.TextDecorations.Underline : null;
    }
}
