using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GlassManagement.Desktop.Views.Calc;

/// <summary>
/// 石材线「板块计算」屏（V31 客户区 936×448）。
/// <para>界面点「预演」→ <see cref="ViewModels.Calc.StonePlateCalcViewModel.PreviewCommand"/>
/// → <c>StonePlateEngine.Compute</c>；算式与四面对拍见 <c>docs/V31_STONE_PLATE_UI.md</c>。</para>
/// </summary>
public partial class StonePlateCalcView : UserControl
{
    public StonePlateCalcView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
