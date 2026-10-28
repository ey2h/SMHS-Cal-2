using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassManagement.Calc.Data;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>
/// 建筑预计雷击次数计算（菜单 id=181）视图模型。
/// 公式 N = k·Ng·Ae（GB50057-2010 附录 A），雷暴日 Td 来自 leiji 城市表或手填。
/// </summary>
public sealed partial class LightningViewModel : ObservableObject
{
    public static readonly string[] KFactorOptions =
        { "一般情况", "河边湖边/土山顶部/山谷风口/特别潮湿", "金属屋面未接地的砖木结构", "山顶或旷野的孤立建筑物" };
    public static readonly string[] CityOptions = LightningCityTable.Td.Keys.OrderBy(x => x).ToArray();

    [ObservableProperty] private int _kFactorIndex;
    [ObservableProperty] private int _cityIndex;
    [ObservableProperty] private string _lengthText = "100";  // L (m)
    [ObservableProperty] private string _widthText = "50";    // W (m)
    [ObservableProperty] private string _heightText = "50";   // H (m)
    [ObservableProperty] private string _tdText = "36.7";     // 雷暴日
    [ObservableProperty] private string _resultText = "";

    private static double Parse(string s, double def)
        => double.TryParse(s?.Trim(), out var v) ? v : def;

    partial void OnCityIndexChanged(int value)
    {
        if (value >= 0 && value < CityOptions.Length)
            TdText = LightningCityTable.Get(CityOptions[value]).ToString("0.#");
    }

    [RelayCommand]
    private void Calc()
    {
        double l = Parse(LengthText, 100), w = Parse(WidthText, 50);
        double h = Parse(HeightText, 50), td = Parse(TdText, 36.7);
        var kf = (LightningEngine.KFactor)KFactorIndex;
        ResultText = LightningEngine.FormatOutput(l, w, h, td, kf);
    }
}
