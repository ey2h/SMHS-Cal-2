using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 建筑后锚固-植筋计算引擎（GB50367）。
///
/// <para><b>来源</b>：V31 实机「其它计算 → 建筑植筋计算」屏预演输出。</para>
///
/// <list type="bullet">
/// <item>受力最大锚筋拉力 Nmax = N/n + M·r/Σr²（r 锚筋到形心距离）；</item>
/// <item>锚筋截面积 As = π·d²/4；锚筋拉力设计值 = fy·As；</item>
/// <item>锚固长度 la = 0.2·d·fy/ft；修正后 = 1.65×1.1×la（抗震/系数调整）。</item>
/// </list>
/// </summary>
public sealed class RebarAnchorEngine
{
    public sealed class Input
    {
        /// <summary>锚筋直径 d（mm，默认 12）。</summary>
        public double DiameterD = 12;

        /// <summary>锚筋数量 n（默认 4）。</summary>
        public int Count = 4;

        /// <summary>锚筋间距（mm，默认 110，r=55）。</summary>
        public double Spacing = 110;

        /// <summary>轴向拉力 N（N，默认 5000）。</summary>
        public double AxialN = 5000;

        /// <summary>弯矩 M（N·mm，默认 200000）。</summary>
        public double MomentM = 200000;

        /// <summary>钢筋抗拉强度设计值 fy（MPa，默认 300）。</summary>
        public double Fy = 300;

        /// <summary>混凝土抗拉强度 ft（MPa，C25 默认 2.3）。</summary>
        public double Ft = 2.3;
    }

    public sealed class Result
    {
        public double RadiusR;         // 55
        public double SumR2;           // 12100
        public double MaxBoltTension;  // 2159.091
        public double AnchorArea;      // 113.04
        public double TensionAllowable; // 33912
        public double BaseAnchorage;   // 313.043
        public double FinalAnchorage;  // 568.173
    }

    public static Result Compute(Input it)
    {
        if (it is null) throw new ArgumentNullException(nameof(it));
        var r = new Result();

        r.RadiusR = it.Spacing / 2.0;   // 55
        r.SumR2 = it.Count * r.RadiusR * r.RadiusR;  // 4×3025=12100

        r.MaxBoltTension = WindLoadEngine.VbaRound(
            it.AxialN / it.Count + it.MomentM * r.RadiusR / r.SumR2, 3); // 2159.091

        r.AnchorArea = WindLoadEngine.VbaRound(
            3.14 * it.DiameterD * it.DiameterD / 4.0, 3);   // 113.04
        r.TensionAllowable = WindLoadEngine.VbaRound(it.Fy * r.AnchorArea, 3); // 33912

        r.BaseAnchorage = WindLoadEngine.VbaRound(
            0.2 * it.DiameterD * it.Fy / it.Ft, 3);          // 313.043
        r.FinalAnchorage = WindLoadEngine.VbaRound(
            1.65 * 1.1 * r.BaseAnchorage, 3);                // 568.173
        return r;
    }
}
