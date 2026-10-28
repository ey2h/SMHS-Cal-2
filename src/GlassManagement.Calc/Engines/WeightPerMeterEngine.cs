using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 型材米重计算引擎。米重 = 截面积 × 密度 / 1e6（kg/m）。
/// </summary>
public sealed class WeightPerMeterEngine
{
    /// <summary>材料候选+密度（kg/m³）：钢材 7850 / 铝材 2700。
    /// 来源=V31 实机直读（2026-10-27 b8_mizhong_probe/density 两 probe）：
    /// 「型材米重计算」屏材料下拉 CB_GETLBTEXT=[钢材,铝材]（无参数库表）；
    /// 切换+计算 → 输出框 7.85 / 2.7（截面积 1000 固定）、扰截面积 500 → 1.35/3.925，
    /// 反证 ρ=7850/2700 为程序按下拉档位内置。参数库 61 表无材料密度列（已 grep 0 命中）。</summary>
    public static readonly (string Name, double Density)[] Materials =
    {
        ("钢材", 7850), ("铝材", 2700),
    };

    public sealed class Input
    {
        public double AreaMm2 = 1000;        // 截面积 mm²
        public double DensityKgM3 = 2700;    // 密度 kg/m³（铝材）
    }

    public sealed class Result
    {
        public double WeightPerMeter;   // 2.7 kg/m
    }

    public static Result Compute(Input it)
    {
        return new Result
        {
            WeightPerMeter = WindLoadEngine.VbaRound(it.AreaMm2 * it.DensityKgM3 / 1e6, 3),
        };
    }
}
