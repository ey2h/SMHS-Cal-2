using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>
/// 牛腿结构计算（菜单 id=170）视图模型。
/// 公式由 V31 运行期链逐条还原（CorbelEngine），φ 表 31 点运行期直读。
/// </summary>
public sealed partial class CorbelViewModel : ObservableObject
{
    // 尺寸参数
    [ObservableProperty] private string _lText = "2000";
    [ObservableProperty] private string _hText = "1600";
    [ObservableProperty] private string _angleText = "30";
    // 荷载参数（需自行求得）
    [ObservableProperty] private string _axialText = "2000";
    [ObservableProperty] private string _pText = "3500";
    [ObservableProperty] private string _qText = "1428";
    [ObservableProperty] private string _m0Text = "2916";
    // 截面参数（主梁/斜梁同值默认，与 V31 默认算例一致）
    [ObservableProperty] private string _sectionCode = "L50*5";
    [ObservableProperty] private string _ixText = "11.21";     // cm⁴
    [ObservableProperty] private string _wxText = "3.13";      // cm³
    [ObservableProperty] private string _areaText = "480.3";   // mm²
    // 材料
    [ObservableProperty] private string _eText = "206000";
    [ObservableProperty] private string _gammaText = "1.05";
    [ObservableProperty] private string _allowBendingText = "215";

    [ObservableProperty] private string _resultText = "";

    private static double Parse(string s, double def)
        => double.TryParse(s?.Trim(), out var v) ? v : def;

    [RelayCommand]
    private void Calc()
    {
        var it = new CorbelEngine.Input
        {
            L = Parse(LText, 2000),
            H = Parse(HText, 1600),
            AngleDeg = Parse(AngleText, 30),
            AxialN = Parse(AxialText, 2000),
            P = Parse(PText, 3500),
            Q = Parse(QText, 1428),
            M0 = Parse(M0Text, 2916),
            Ix = Parse(IxText, 11.21) * 1e4,     // cm⁴ → mm⁴
            Wx = Parse(WxText, 3.13) * 1e3,      // cm³ → mm³
            Area = Parse(AreaText, 480.3),
            E = Parse(EText, 206000),
            Gamma = Parse(GammaText, 1.05),
            AllowBending = Parse(AllowBendingText, 215),
        };
        var o = CorbelEngine.Compute(it);
        ResultText = CorbelEngine.FormatOutput(it, o);
    }

    [RelayCommand]
    private void Clear()
    {
        ResultText = "";
    }
}
