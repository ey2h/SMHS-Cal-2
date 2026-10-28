using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GlassManagement.Desktop.Views.Calc;

/// <summary>
/// 背栓式点挂幕墙线「板块计算」屏（V31 客户区 782×536）。
/// <para>界面点「预演」→ <see cref="ViewModels.Calc.BackboltPlateCalcViewModel.PreviewCommand"/>
/// → <c>BackboltPlateEngine.Compute</c>；算式与四面对拍见 <c>docs/V31_BACKBOLT_PLATE_UI.md</c>。</para>
/// </summary>
public partial class BackboltPlateCalcView : UserControl
{
    public BackboltPlateCalcView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
