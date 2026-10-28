using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GlassManagement.Desktop.Views.Calc;

/// <summary>
/// 板材线「板块计算」屏（V31 客户区 795×530）。
/// <para>界面点「预演」→ <see cref="ViewModels.Calc.MetalPlateCalcViewModel.PreviewCommand"/>
/// → <c>PlateEngine.Compute</c>；算式与四面对拍见 <c>docs/V31_PLATE_UI.md</c>。</para>
/// </summary>
public partial class MetalPlateCalcView : UserControl
{
    public MetalPlateCalcView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
