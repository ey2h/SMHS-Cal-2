using Avalonia.Controls;

namespace GlassManagement.Desktop.Views.Calc;

/// <summary>
/// 横梁计算屏 View（严格 MVVM）。
/// 代码后置**只有** InitializeComponent —— 无任何业务逻辑、无数值计算、无事件处理。
/// 所有行为在 <see cref="ViewModels.Calc.BeamCalcViewModel"/>，所有算式在
/// <see cref="Calc.Engines.HorizontalBeamEngine"/>。
/// </summary>
public partial class BeamCalcView : UserControl
{
    public BeamCalcView()
    {
        InitializeComponent();
    }
}
