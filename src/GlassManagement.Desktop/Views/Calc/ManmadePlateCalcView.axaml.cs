using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GlassManagement.Desktop.Views.Calc;

/// <summary>
/// 人造板材线「板块计算」屏（V31 客户区 782×536）。
/// <para>界面点「预演」→ <see cref="ViewModels.Calc.ManmadePlateCalcViewModel.PreviewCommand"/>
/// → <c>ManmadePlateEngine.Compute</c>；算式与四面对拍见 <c>docs/V31_MANMADE_PLATE_UI.md</c>。</para>
/// </summary>
public partial class ManmadePlateCalcView : UserControl
{
    public ManmadePlateCalcView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
