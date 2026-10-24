using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using GlassManagement.Desktop.ViewModels.Calc;

namespace GlassManagement.Desktop.Views.Calc;

/// <summary>
/// 独立宿主：「多跨数据输入」Frame 从立柱计算窗体抽出、可直接从菜单打开。
/// MVVM：面板 DataContext = 向导共享的 MultiSpanViewModel（与向导链同一会话、同一实例）。
/// </summary>
public partial class CW136_MultiSpanInput : UserControl
{
    public CW136_MultiSpanInput()
    {
        InitializeComponent();
        var vm = CalcWizardViewModel.Default.GetOrCreateMulti();
        Panel.DataContext = vm;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MultiSpanViewModel.StatusText))
                StatusText.Text = vm.StatusText;
        };
        StatusText.Text = vm.StatusText;
    }
}
