using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassManagement.Calc.Engines.StaticCalc;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>
/// 常用建筑结构静力计算（菜单 id=188）视图模型。
/// 梁类型（悬臂/简支/带悬臂）× 荷载类型（均布/集中），按《建筑结构静力计算手册》第二版。
/// 引擎下沉自 Api 侧 StaticCalcEngine（纯数值计算，SVG 绘图留在 Api 侧）。
/// </summary>
public sealed partial class StaticCalcViewModel : ObservableObject
{
    public static readonly string[] BeamTypeOptions = { "悬臂梁", "简支梁", "带悬臂的梁" };
    public static readonly string[] LoadTypeOptions = { "均布荷载", "集中荷载" };

    [ObservableProperty] private int _beamTypeIndex = 1;   // 默认简支梁
    [ObservableProperty] private int _loadTypeIndex;        // 默认均布
    [ObservableProperty] private string _lengthText = "3000";   // 跨度 L (mm)
    [ObservableProperty] private string _overhangText = "1000"; // 悬臂长度 L2 (mm)
    [ObservableProperty] private string _loadText = "10";       // 均布 q (kN/m) 或集中 P (kN)
    [ObservableProperty] private string _elasticText = "70000"; // E (MPa)
    [ObservableProperty] private string _inertiaText = "1250000"; // I (mm^4)
    [ObservableProperty] private string _resultText = "";

    private static double Parse(string s, double def)
        => double.TryParse(s?.Trim(), out var v) ? v : def;

    [RelayCommand]
    private void Calc()
    {
        var req = new BeamCalcRequest
        {
            Support = BeamTypeIndex switch
            {
                0 => SupportCondition.Cantilever,
                2 => SupportCondition.OverhangBeam,
                _ => SupportCondition.SimpleBeam,
            },
            Load = LoadTypeIndex == 1 ? LoadType.Concentrated : LoadType.Uniform,
            L = Parse(LengthText, 3000) / 1000.0,       // mm -> m
            L2 = Parse(OverhangText, 1000) / 1000.0,    // 悬臂长
            Q = Parse(LoadText, 10),                    // q 或 P
            P = Parse(LoadText, 10),
            E = Parse(ElasticText, 70000),              // MPa
            I = Parse(InertiaText, 1250000),             // mm^4（引擎内部 EI×1e-6 约定）
        };
        var r = new StaticCalcService().Calculate(req);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"梁类型：{BeamTypeOptions[BeamTypeIndex]}；荷载：{LoadTypeOptions[LoadTypeIndex]}");
        sb.AppendLine($"依据：{r.Reference}（{r.Description}）");
        sb.AppendLine();
        foreach (var ra in r.Reactions)
            sb.AppendLine($"支座反力 {ra.Name} = {ra.Value:0.###} kN");
        sb.AppendLine();
        sb.AppendLine($"最大弯矩 Mmax = {r.MaxMoment:0.###} kN·m（位置 x={r.MaxMomentPosition:0.###} m）");
        sb.AppendLine($"最小弯矩 Mmin = {r.MinMoment:0.###} kN·m（位置 x={r.MinMomentPosition:0.###} m）");
        sb.AppendLine($"最大剪力 Vmax = {r.MaxShear:0.###} kN");
        sb.AppendLine($"最大挠度 δmax = {r.MaxDeflection:0.###} mm（位置 x={r.MaxDeflectionPosition:0.###} m）");
        ResultText = sb.ToString();
    }
}
