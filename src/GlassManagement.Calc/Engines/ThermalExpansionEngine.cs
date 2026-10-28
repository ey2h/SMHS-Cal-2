using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 型（板）材热应力伸缩量计算引擎。ΔL = α·L·ΔT（mm）。
/// </summary>
public sealed class ThermalExpansionEngine
{
    public sealed class Input
    {
        public double Alpha = 0.000023;   // 线胀系数
        public double LengthMm = 3000;    // 材料长度 mm
        public double DeltaT = 80;        // 温差（缺资料取 80℃）
    }

    public sealed class Result
    {
        public double ExpansionMm;   // 5.52
    }

    public static Result Compute(Input it)
    {
        return new Result
        {
            ExpansionMm = WindLoadEngine.VbaRound(it.Alpha * it.LengthMm * it.DeltaT, 3),
        };
    }
}
