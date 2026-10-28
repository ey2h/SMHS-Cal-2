using Avalonia.Controls;

namespace GlassManagement.Desktop.Views.Calc;

/// <summary>
/// 「独立工具」屏通用视图载体。所有 <see cref="ViewModels.Calc.SimpleToolViewModel"/>
/// 子类共用本视图（由 <see cref="ViewLocator"/> 统一映射），界面差异全部由 VM 的字段列表驱动。
/// </summary>
public partial class SimpleToolView : UserControl
{
    public SimpleToolView() => InitializeComponent();
}
