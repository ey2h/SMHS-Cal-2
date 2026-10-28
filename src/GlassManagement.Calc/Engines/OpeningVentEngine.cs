using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 开启扇通风面积计算引擎（V31「其它计算>开启扇通风面积」，945×542）。
/// 算法一 GB50352-2019；算法二 SJG44-2018。sin 值先 round3。
/// 取证参照 kaiqishan_var.json（2 rounds + 12 var，0x1B6AA87–0x1B6B2D1）。
/// </summary>
public static class OpeningVentEngine
{
    public sealed class Input
    {
        /// <summary>开启扇宽度 b（mm），默认 1500。</summary>
        public double WidthMm = 1500.0;

        /// <summary>开启扇高度 a/h（mm），默认 1800。</summary>
        public double HeightMm = 1800.0;

        /// <summary>开启角度 α（度），默认 30。</summary>
        public double AngleDeg = 30.0;
    }

    public sealed class Result
    {
        public double D;                 // 下缘框扇间距 0.9324
        public double Method1Fp;         // Fp = d(h+B) = 3.07692
        public double Method1Area;       // min(Fp, h·B) = 2.7
        public double Method2OpenArea;   // a·b = 2.7
        public double Method2S1;         // a²·sinα/2 = 0.81
        public double Method2S2;         // b·d = 1.3986
        public double Method2AirArea;    // 2S1+S2 = 3.0186
        public double Method2Area;       // min = 2.7
    }

    /// <summary>计算开启扇通风面积。</summary>
    public static Result Compute(Input it)
    {
        if (it is null) throw new ArgumentNullException(nameof(it));
        var r = new Result();

        double a = it.HeightMm / 1000.0;   // 1.8
        double b = it.WidthMm / 1000.0;    // 1.5
        double rad = Math.PI / 180.0;

        // sin 值 round3
        double sinAlpha = WindLoadEngine.VbaRound(Math.Sin(it.AngleDeg * rad), 3);        // 0.5
        double sinHalf = WindLoadEngine.VbaRound(Math.Sin(it.AngleDeg / 2.0 * rad), 3);   // 0.259

        // d = 2a·sin(α/2)
        r.D = WindLoadEngine.VbaRound(2.0 * a * sinHalf, 4);   // 0.9324

        // 算法一（GB50352-2019）：Fp = d·(h+B)
        r.Method1Fp = WindLoadEngine.VbaRound(r.D * (a + b), 5);   // 3.07692
        r.Method1Area = WindLoadEngine.VbaRound(Math.Min(r.Method1Fp, a * b), 3);   // 2.7

        // 算法二（SJG44-2018）
        r.Method2OpenArea = WindLoadEngine.VbaRound(a * b, 3);              // 2.7
        r.Method2S1 = WindLoadEngine.VbaRound(a * a * sinAlpha / 2.0, 3);   // 0.81
        r.Method2S2 = WindLoadEngine.VbaRound(b * r.D, 4);                  // 1.3986
        r.Method2AirArea = WindLoadEngine.VbaRound(2.0 * r.Method2S1 + r.Method2S2, 4);  // 3.0186
        r.Method2Area = WindLoadEngine.VbaRound(Math.Min(r.Method2OpenArea, r.Method2AirArea), 3);

        return r;
    }
}
