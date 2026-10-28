using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassManagement.Calc.Engines.SectionCalc;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>
/// 截面特性计算（菜单 id=159）视图模型。
/// 输入外轮廓多边形点集（每行 x,y，顺时针/逆时针均可），Green 定理算截面特性。
/// 引擎下沉自 GlassManagement.SectionTool（SectionCalculationService）。
/// </summary>
public sealed partial class SectionCalcViewModel : ObservableObject
{
    [ObservableProperty] private string _pointsText =
        "0,0\n100,0\n100,50\n0,50";
    [ObservableProperty] private string _resultText = "";

    [RelayCommand]
    private void Calc()
    {
        var outer = new List<PointD>();
        foreach (var line in (PointsText ?? "").Split('\n'))
        {
            var parts = line.Trim().Split(',', '，');
            if (parts.Length >= 2 &&
                double.TryParse(parts[0].Trim(), out var x) &&
                double.TryParse(parts[1].Trim(), out var y))
            {
                outer.Add(new PointD { X = x, Y = y });
            }
        }
        if (outer.Count < 3) { ResultText = "请至少输入 3 个顶点（每行 x,y）。"; return; }

        try
        {
            var r = new SectionCalculationService().CalculateSection(outer, null);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"面积 A = {r.Area:0.###} mm²");
            sb.AppendLine($"形心 Cx = {r.Cx:0.###} mm，Cy = {r.Cy:0.###} mm");
            sb.AppendLine($"惯性矩 Ix = {r.Ix:0.###} mm⁴，Iy = {r.Iy:0.###} mm⁴");
            sb.AppendLine($"抵抗矩 Wx1 = {r.Wx1:0.###}，Wx2 = {r.Wx2:0.###} mm³");
            sb.AppendLine($"抵抗矩 Wy1 = {r.Wy1:0.###}，Wy2 = {r.Wy2:0.###} mm³");
            sb.AppendLine($"静矩 Sx = {r.Sx:0.###}，Sy = {r.Sy:0.###} mm³");
            ResultText = sb.ToString();
        }
        catch (System.Exception e)
        {
            ResultText = "计算失败：" + e.Message;
        }
    }
}
