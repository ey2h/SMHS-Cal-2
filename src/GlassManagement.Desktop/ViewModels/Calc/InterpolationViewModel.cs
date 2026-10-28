using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>插值计算器（菜单 id=190）视图模型：两点线性插值。</summary>
public sealed partial class InterpolationViewModel : ObservableObject
{
    [ObservableProperty] private string _x1Text = "125";
    [ObservableProperty] private string _y1Text = "0.1235";
    [ObservableProperty] private string _x2Text = "280";
    [ObservableProperty] private string _y2Text = "0.1358";
    [ObservableProperty] private string _xText = "200";
    [ObservableProperty] private string _resultText = "";

    private static double Parse(string s, double def)
        => double.TryParse(s?.Trim(), out var v) ? v : def;

    [RelayCommand]
    private void Calc()
    {
        try
        {
            double y = InterpolationEngine.Linear(
                Parse(XText, 200), Parse(X1Text, 125), Parse(Y1Text, 0.1235),
                Parse(X2Text, 280), Parse(Y2Text, 0.1358));
            ResultText = $"插值结果 y = {y:0.#####}";
        }
        catch (System.Exception e) { ResultText = "计算失败：" + e.Message; }
    }
}
