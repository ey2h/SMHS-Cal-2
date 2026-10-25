using Avalonia.Controls;
using Avalonia.Interactivity;
using GlassManagement.Desktop.ViewModels.Calc;

namespace GlassManagement.Desktop.Views.Calc;

/// <summary>
/// 「其它玻璃计算」视图。
/// 厚度下拉是 10 档静态列表（与 V31 一致），这里把选中索引换算成 mm 写回 VM。
/// </summary>
public partial class OtherGlassCalcView : UserControl
{
    public OtherGlassCalcView()
    {
        InitializeComponent();
    }

    private OtherGlassCalcViewModel? Vm => DataContext as OtherGlassCalcViewModel;

    private static double ThicknessAt(ComboBox? cb)
    {
        int i = cb?.SelectedIndex ?? -1;
        return i >= 0 && i < OtherGlassCalcViewModel.ThicknessOptions.Length
            ? OtherGlassCalcViewModel.ThicknessOptions[i]
            : 6;
    }

    private void OnOuterThicknessChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Vm is null) return;
        Vm.T1 = ThicknessAt(OuterThickness);
    }

    private void OnOuter2ThicknessChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Vm is null) return;
        Vm.T2 = ThicknessAt(Outer2Thickness);
    }

    private void OnInner2ThicknessChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Vm is null) return;
        Vm.T3 = ThicknessAt(Inner2Thickness);
    }

    private void OnInnerThicknessChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Vm is null) return;
        Vm.T4 = ThicknessAt(InnerThickness);
    }
}
