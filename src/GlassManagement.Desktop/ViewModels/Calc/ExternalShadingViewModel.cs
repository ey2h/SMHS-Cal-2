using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>
/// 外遮阳系数计算（菜单 id=134）视图模型。
/// 输入：遮阳板形式（水平/垂直）、外挑长度 A、根部到窗对边距离 B。
/// 输出：外挑系数 PF + 外遮阳系数 SD（GB50189-2005 附录 A）。
/// </summary>
public sealed partial class ExternalShadingViewModel : ObservableObject
{
    public static readonly string[] TypeOptions = { "水平遮阳板", "垂直遮阳板" };

    [ObservableProperty] private int _typeIndex;
    [ObservableProperty] private string _overhangText = "800";   // A 外挑长度
    [ObservableProperty] private string _distanceText = "1800";  // B 根部到窗对边距离
    [ObservableProperty] private string _resultText = "";

    private static double Parse(string s, double def)
        => double.TryParse(s?.Trim(), out var v) ? v : def;

    [RelayCommand]
    private void Calc()
    {
        double a = Parse(OverhangText, 800);
        double b = Parse(DistanceText, 1800);
        var type = TypeIndex == 1 ? ExternalShadingEngine.ShadingType.垂直遮阳板
                                  : ExternalShadingEngine.ShadingType.水平遮阳板;
        ResultText = ExternalShadingEngine.FormatOutput(type, a, b);
    }
}
