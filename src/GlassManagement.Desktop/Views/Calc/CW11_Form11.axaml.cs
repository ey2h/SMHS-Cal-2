using Avalonia.Controls;
using GlassManagement.Desktop.ViewModels.Calc;

namespace GlassManagement.Desktop.Views.Calc;

/// <summary>
/// VB6 form reconstruction: 幕墙计算-立柱计算（静态互斥视图集合，供对照/其他菜单路径）。
/// 其中「多跨数据输入」Tab 内嵌的是 MVVM 面板，DataContext 接向导共享 MultiSpanViewModel。
/// </summary>
public partial class CW11_Form11 : UserControl
{
    public CW11_Form11()
    {
        InitializeComponent();
        MultiPanel.DataContext = CalcWizardViewModel.Default.GetOrCreateMulti();
    }
}
