using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 百叶窗通风面积计算引擎（V31「其它计算>百叶窗通风面积」，945×542）。
/// 依据 GB/T39968-2021 附录 A：Af = L·(A+B+n·C)，βf = Af/(W·H)。
/// 取证参照（671 字计算书输出）：Af=0.96，βf=0.356。
/// </summary>
public static class LouverVentEngine
{
    public sealed class Input
    {
        /// <summary>边框之间的最小距离 L（mm），默认 1200。</summary>
        public double FrameSpanMm = 1200.0;

        /// <summary>上框和最上面叶片最小距离 A（mm），默认 25。</summary>
        public double TopGapMm = 25.0;

        /// <summary>下框和最下面叶片最小距离 B（mm），默认 25。</summary>
        public double BottomGapMm = 25.0;

        /// <summary>中间隔数量 n，默认 10。</summary>
        public double BladeCount = 10.0;

        /// <summary>相邻叶片最小间距 C（mm），默认 75。</summary>
        public double BladeGapMm = 75.0;

        /// <summary>百叶窗实际宽度 W（mm），默认 1500。</summary>
        public double WidthMm = 1500.0;

        /// <summary>百叶窗实际高度 H（mm），默认 1800。</summary>
        public double HeightMm = 1800.0;
    }

    public sealed class Result
    {
        public double VentAreaM2;   // Af = 0.96
        public double VentRatio;    // βf = 0.356
    }

    /// <summary>计算百叶窗通风面积。Af/βf 均 round3。</summary>
    public static Result Compute(Input it)
    {
        if (it is null) throw new ArgumentNullException(nameof(it));

        double L = it.FrameSpanMm / 1000.0;
        double A = it.TopGapMm / 1000.0;
        double B = it.BottomGapMm / 1000.0;
        double C = it.BladeGapMm / 1000.0;
        double W = it.WidthMm / 1000.0;
        double H = it.HeightMm / 1000.0;

        // Af = L·(A+B+n·C)
        double af = L * (A + B + it.BladeCount * C);
        double ratio = af / (W * H);

        return new Result
        {
            VentAreaM2 = WindLoadEngine.VbaRound(af, 3),
            VentRatio = WindLoadEngine.VbaRound(ratio, 3),
        };
    }
}
