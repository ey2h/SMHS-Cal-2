using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GlassManagement.Desktop.Views.Calc;

/// <summary>
/// 全玻璃幕墙线「设计计算」屏（V31 客户区 903×438）。
/// <para>界面点「预演」→ <see cref="ViewModels.Calc.FullGlassCalcViewModel.PreviewCommand"/>
/// → <c>FullGlassEngine.Compute</c>；算式与四面对拍见 <c>docs/V31_FULLGLASS_UI.md</c>。</para>
/// </summary>
public partial class FullGlassCalcView : UserControl
{
    public FullGlassCalcView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
