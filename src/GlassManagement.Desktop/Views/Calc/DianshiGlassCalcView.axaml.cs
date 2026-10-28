using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GlassManagement.Desktop.Views.Calc;

/// <summary>
/// 点式玻璃幕墙线「点式肋（无肋）驳接幕墙设计计算」屏（V31 客户区 903×438）。
/// <para>界面点「预演」→ <see cref="ViewModels.Calc.DianshiGlassCalcViewModel.PreviewCommand"/>
/// → <c>DianshiGlassEngine.Compute</c>。</para>
/// </summary>
public partial class DianshiGlassCalcView : UserControl
{
    public DianshiGlassCalcView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
