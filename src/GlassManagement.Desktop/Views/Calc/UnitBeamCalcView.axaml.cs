using Avalonia.Controls;

namespace GlassManagement.Desktop.Views.Calc;

/// <summary>
/// 单元式横梁屏 View（严格 MVVM）。
/// 代码后置只有 InitializeComponent —— 无业务逻辑、无数值、无事件。
/// 行为在 <see cref="ViewModels.Calc.UnitBeamCalcViewModel"/>，算式在
/// <see cref="Calc.Engines.UnitBeamEngine"/>。
/// </summary>
public partial class UnitBeamCalcView : UserControl
{
    public UnitBeamCalcView()
    {
        InitializeComponent();
    }
}
