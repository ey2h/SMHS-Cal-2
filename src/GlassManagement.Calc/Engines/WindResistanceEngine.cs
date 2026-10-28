using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 物理性能·幕墙抗风压性能（菜单 id=139，CW19「抗风压性能计算」帧）。
/// 公式：风荷载标准值 wk → 查《建筑幕墙、门窗通用技术条件》GB/T31433-2015 表2 分级。
/// 分级表（b7_probe29_kangfengya 输出逐字直读，程序内置文案）：
///   1级 1.0≤P3&lt;1.5 / 2级 1.5≤P3&lt;2.0 / 3级 2.0≤P3&lt;2.5 / 4级 2.5≤P3&lt;3.0 /
///   5级 3.0≤P3&lt;3.5 / 6级 3.5≤P3&lt;4.0 / 7级 4.0≤P3&lt;4.5 / 8级 4.5≤P3&lt;5.0 /
///   9级 5.0≤P3（9 级须注明具体指标值）。
/// 指标值不应低于 wk，且不应小于 1.0KPa。
/// wk（风荷载标准值）来源：城市联动（b7_probe29 实测北京默认 2.5KPa；城市切换联动待专测）。
/// </summary>
public sealed class WindResistanceEngine
{
    /// <summary>GB/T31433-2015 表2 分级下限（9 档）。</summary>
    private static readonly double[] Lower = { 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0, 4.5, 5.0 };

    /// <summary>抗风压性能等级（1~9）；wk&lt;1.0 返回 0（低于 1 级，规范要求 ≥1.0）。</summary>
    public static int Grade(double wk)
    {
        if (wk < 1.0) return 0;
        for (int i = 0; i < Lower.Length; i++)
            if (wk < Lower[i]) return i;              // 落在 [Lower[i-1], Lower[i])
        return 9;
    }

    /// <summary>分级结论（b7_probe29 逐字：`3.0KPa&gt;wk = 2.5KPa≥2.5KPa      所以抗风压性能为：4级`）。</summary>
    public static string Conclude(double wk)
    {
        int g = Grade(wk);
        if (g == 0)
            return $"wk = {wk:0.###}KPa<1.0KPa      低于最低分级（规范要求 wk 不应小于 1.0KPa）";
        double lower = Lower[g - 1];
        double upper = g < 9 ? Lower[g] : double.PositiveInfinity;
        string upperText = g < 9 ? $"{upper:0.###}KPa>wk = {wk:0.###}KPa" : $"wk = {wk:0.###}KPa";
        string gradeNote = g == 9 ? "（第9级应在分级后同时注明具体分级指标值）" : "";
        return $"{upperText}≥{lower:0.###}KPa      所以抗风压性能为：{g}级{gradeNote}";
    }
}
