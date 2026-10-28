using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GlassManagement.Desktop.Views.Calc;

/// <summary>U型玻璃「U型玻璃结构设计计算」屏（V31 客户区 797×521）。</summary>
public partial class UGlassCalcView : UserControl
{
    public UGlassCalcView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
