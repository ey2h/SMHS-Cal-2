using Avalonia.Controls;

namespace GlassManagement.Desktop.Views.Calc;

/// <summary>
/// 「多跨数据输入」MVVM View：只负责布局与绑定（V35 权威几何），
/// 全部业务判断（2N 段生成 / 基线匹配 / 预演实算）在 MultiSpanViewModel。
/// </summary>
public partial class MultiSpanInputView : UserControl
{
    public MultiSpanInputView() => InitializeComponent();
}
