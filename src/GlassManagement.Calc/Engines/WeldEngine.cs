using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 焊缝强度计算引擎（GB50017-2017 角焊缝，公式 11.2.2-3）。
///
/// <para><b>来源</b>：V31 实机「其它计算 → 焊缝强度计算」屏预演输出。
/// 角焊缝截面特性 + 强度校核 <c>((σf/βf)²+τf²)^0.5 ≤ ffw</c>。</para>
///
/// <list type="bullet">
/// <item>有效厚度 he = 0.7·hf；</item>
/// <item>中性轴位置 d = 0.5·((H-2hf)²-(B-2hf)·he)/(H+B-4hf)；</item>
/// <item>惯性矩 I = he(H-2hf)³/12 + (B-2hf)he³/12 + he(H-2hf)((H-2hf)/2-d)² + (B-2hf)he(d+he/2)²；</item>
/// <item>抵抗矩 W = I/(H-2hf-d)；</item>
/// <item>校核 = ((N/(βf·A) + M/(βf·W))² + (V/A)²)^0.5，βf=1.22。</item>
/// </list>
/// </summary>
public sealed class WeldEngine
{
    /// <summary>正面角焊缝强度设计值增大系数 βf。</summary>
    public const double BetaF = 1.22;

    public sealed class Input
    {
        /// <summary>焊脚尺寸 hf（mm，默认 6）。</summary>
        public double Hf = 6;

        /// <summary>竖向焊缝长度 H（mm，默认 100）。</summary>
        public double H = 100;

        /// <summary>横向焊缝长度 B（mm，默认 50）。</summary>
        public double B = 50;

        /// <summary>轴向力 N（N，默认 5000）。</summary>
        public double AxialN = 5000;

        /// <summary>弯矩 M（N·mm，默认 4000）。</summary>
        public double MomentM = 4000;

        /// <summary>剪力 V（N，默认 0）。</summary>
        public double ShearV = 0;

        /// <summary>角焊缝强度设计值 ffw（MPa，Q235 默认 160）。</summary>
        public double Ffw = 160;
    }

    public sealed class Result
    {
        public double He;             // 有效厚度 4.2
        public double NeutralAxisD;   // 30.097
        public double InertiaI;       // 475639.887
        public double SectionW;       // 8214.426
        public double AreaA;          // 有效面积
        public double StressSigmaF;   // N/(βf·A) + M/(βf·W)
        public double StressTauF;     // V/A
        public double CheckValue;     // 27.807
        public double Allowable;      // ffw
        public bool Ok;
    }

    public static Result Compute(Input it)
    {
        if (it is null) throw new ArgumentNullException(nameof(it));
        var r = new Result();

        r.He = WindLoadEngine.VbaRound(0.7 * it.Hf, 3);              // 4.2
        double hc = it.H - 2 * it.Hf;                                // 88
        double bc = it.B - 2 * it.Hf;                                // 38

        r.NeutralAxisD = WindLoadEngine.VbaRound(
            0.5 * (hc * hc - bc * r.He) / (it.H + it.B - 4 * it.Hf), 3);  // 30.097

        double I1 = r.He * Math.Pow(hc, 3.0) / 12.0;
        double I2 = bc * Math.Pow(r.He, 3.0) / 12.0;
        double I3 = r.He * hc * Math.Pow(hc / 2.0 - r.NeutralAxisD, 2.0);
        double I4 = bc * r.He * Math.Pow(r.NeutralAxisD + r.He / 2.0, 2.0);
        r.InertiaI = WindLoadEngine.VbaRound(I1 + I2 + I3 + I4, 3);   // 475639.887

        r.SectionW = WindLoadEngine.VbaRound(r.InertiaI / (hc - r.NeutralAxisD), 3); // 8214.426

        r.AreaA = WindLoadEngine.VbaRound(r.He * (it.H + it.B - 2 * it.Hf), 3);   // 579.6

        r.StressSigmaF = WindLoadEngine.VbaRound(
            it.AxialN / (BetaF * r.AreaA) + it.MomentM / (BetaF * r.SectionW), 3);
        r.StressTauF = WindLoadEngine.VbaRound(it.ShearV / r.AreaA, 3);
        r.CheckValue = WindLoadEngine.VbaRound(
            Math.Sqrt(r.StressSigmaF * r.StressSigmaF + r.StressTauF * r.StressTauF), 3);
        r.Allowable = it.Ffw;
        r.Ok = r.CheckValue <= r.Allowable;
        return r;
    }
}
