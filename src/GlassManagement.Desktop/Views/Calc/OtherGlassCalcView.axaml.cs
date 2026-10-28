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

    /// <summary>
    /// V31「退出」= 关掉本窗体。复刻版有两种承载：
    /// CalcBrowserWindow（窗体就是本屏）→ 关窗口；CalcMainWindow（同窗口换内容）→ 回到空白水印态。
    /// </summary>
    private void OnExitClick(object? sender, RoutedEventArgs e)
    {
        switch (TopLevel.GetTopLevel(this))
        {
            case CalcBrowserWindow browser:
                browser.Close();
                break;
            case CalcMainWindow main:
                main.CloseCurrentScreen();
                break;
        }
    }

    /// <summary>
    /// V31「下一步」——实机取证（v31runs/otherglass_next.txt）：
    ///   点 (671,458)「下一步」(en=1) 后打开
    ///   「生成计算书（支持微软office及金山wps office）」屏，client 600x322。
    ///   ⇒ 这屏不是流程终点，下一步落点是计算书面板。
    /// </summary>
    private void OnNextClick(object? sender, RoutedEventArgs e)
    {
        const string book = "生成计算书";
        switch (TopLevel.GetTopLevel(this))
        {
            case CalcBrowserWindow browser:
                browser.ShowByTitle(book);
                break;
            case CalcMainWindow main:
                main.ShowByTitle(book);
                break;
        }
    }

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
