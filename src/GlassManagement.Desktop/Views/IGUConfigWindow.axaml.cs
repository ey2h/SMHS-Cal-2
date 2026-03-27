using Avalonia.Controls;
using Avalonia.Interactivity;
using GlassManagement.Desktop.ViewModels;

namespace GlassManagement.Desktop.Views;

public partial class IGUConfigWindow : Window
{
    private IGUConfigViewModel VM => (IGUConfigViewModel)DataContext!;

    public IGUConfigWindow()
    {
        InitializeComponent();
    }

    /// <summary>打开单片/夹胶配置子窗口</summary>
    private async void OnConfigureSheet(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not IGUSheetItemVM sheet) return;

        var configWin = new GlassConfigWindow();
        if (configWin.DataContext is GlassConfigViewModel configVm)
        {
            // 初始化为单片模式 + 当前厚度
            configVm.SelectedBasicCategory = "单片";
            var thkOpts = configVm.GlassThicknessOptions;
            configVm.SingleThicknessIndex = sheet.ThicknessIndex < thkOpts.Count ? sheet.ThicknessIndex : 1;
        }

        var result = await configWin.ShowDialog<bool?>(this);
        if (result == true && configWin.DataContext is GlassConfigViewModel vm2)
        {
            sheet.GlassConfigResult = vm2.ConfigResult;
            if (vm2.IsSingleMode)
            {
                sheet.ThicknessIndex = vm2.SingleThicknessIndex;
            }
        }
        VM.UpdateResult();
    }

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        VM.IsConfirmed = true;
        Close(true);
    }
}
