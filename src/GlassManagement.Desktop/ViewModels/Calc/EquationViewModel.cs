using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>方程求解（菜单 id=191）视图模型：一元二次 / 一元三次。</summary>
public sealed partial class EquationViewModel : ObservableObject
{
    [ObservableProperty] private string _a2Text = "5";
    [ObservableProperty] private string _b2Text = "2";
    [ObservableProperty] private string _c2Text = "-15";
    [ObservableProperty] private string _quadResult = "";

    [ObservableProperty] private string _a3Text = "500";
    [ObservableProperty] private string _b3Text = "45";
    [ObservableProperty] private string _c3Text = "25";
    [ObservableProperty] private string _d3Text = "15";
    [ObservableProperty] private string _cubicResult = "";

    private static double Parse(string s, double def)
        => double.TryParse(s?.Trim(), out var v) ? v : def;

    [RelayCommand]
    private void SolveQuadratic()
    {
        var r = EquationEngine.SolveQuadratic(Parse(A2Text, 5), Parse(B2Text, 2), Parse(C2Text, -15));
        QuadResult = r.HasRealRoots
            ? $"x1 = {r.X1:0.#####}；x2 = {r.X2:0.#####}"
            : "无实根（判别式 < 0）";
    }

    [RelayCommand]
    private void SolveCubic()
    {
        var roots = EquationEngine.SolveCubic(Parse(A3Text, 500), Parse(B3Text, 45), Parse(C3Text, 25), Parse(D3Text, 15));
        CubicResult = roots.Length == 0
            ? "无实根"
            : string.Join("；", System.Linq.Enumerable.Select(roots, (r, i) => $"x{i + 1} = {r:0.###############}"));
    }
}
