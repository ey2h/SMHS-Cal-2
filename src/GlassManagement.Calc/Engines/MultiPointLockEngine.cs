using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 多点锁扇框及锁点计算引擎（V31「其它计算>多点锁及开启扇扇框计算」，763×500）。
/// 2 点对称模型：扇框按悬臂+跨中梁，锁点按受剪。
/// 取证参照 duodiansuo_var.json（6 rounds + 40 var，0x168FEF0–0x169308D）。
/// </summary>
public static class MultiPointLockEngine
{
    public sealed class Input
    {
        /// <summary>扇高 H（mm），默认 750。</summary>
        public double HeightMm = 750.0;
        /// <summary>扇宽 B（mm），默认 1650。</summary>
        public double WidthMm = 1650.0;

        /// <summary>悬臂端长度 L1（mm），默认 280。</summary>
        public double CantileverMm = 280.0;
        /// <summary>锁点间距 L2（mm），默认 450。</summary>
        public double LockSpanMm = 450.0;

        /// <summary>锁点直径 d（mm），默认 5。</summary>
        public double PinDiameterMm = 5.0;
        /// <summary>锁点受剪承载力设计值 fv（MPa），默认 100。</summary>
        public double PinShearMpa = 100.0;
        /// <summary>实际设计锁点数 n，默认 2。</summary>
        public double PinCount = 2.0;

        /// <summary>风荷载标准值 wk（N/mm²），默认 0.00137。</summary>
        public double WkNmm2 = 0.00137;
        /// <summary>设计荷载 qd（N/mm²），默认 0.00212。</summary>
        public double DesignLoadNmm2 = 0.00212;
        /// <summary>荷载安全系数，默认 1.35。</summary>
        public double LoadFactor = 1.35;

        /// <summary>扇框截面抵抗矩 W（mm³），默认 5300。</summary>
        public double SectionModulus = 5300.0;
        /// <summary>扇框截面惯性矩 I（mm⁴），默认 150500。</summary>
        public double Inertia = 150500.0;
        /// <summary>弹性模量 E（MPa），默认 70000。</summary>
        public double ElasticModulus = 70000.0;
        /// <summary>型材抗弯强度设计值（MPa），默认 90（6063-T5）。</summary>
        public double AllowableSigma = 90.0;
    }

    public sealed class Result
    {
        public double FrameSigma;            // 12.936
        public bool FrameSigmaOk;
        public double CantileverDeflection;  // 0.233
        public double CantileverLimit;       // 3.111
        public bool CantileverOk;
        public double MidSpanDeflection;     // 0.049
        public double MidSpanLimit;          // 2.5
        public bool MidSpanOk;
        public double RequiredPins;          // 1.805
        public bool PinsOk;
        public double MaxPinShearN;          // 1766.49
        public double PinShearAllowableN;    // 1962.5
        public bool PinShearOk;
    }

    private static double R(double v, int d) => WindLoadEngine.VbaRound(v, d);

    public static Result Compute(Input it)
    {
        if (it is null) throw new ArgumentNullException(nameof(it));
        var r = new Result();

        double B = it.WidthMm;
        double L1 = it.CantileverMm;
        double L2 = it.LockSpanMm;

        // 线荷载：q标准 = wk·B/2，q设计 = qd·B/2
        double qStd = it.WkNmm2 * B / 2.0;      // 1.13025
        double qDes = it.DesignLoadNmm2 * B / 2.0;  // 1.749

        // ① 扇框强度 σ = q设计·L1²/2 / W（悬臂梁）
        double moment = qDes * L1 * L1 / 2.0;   // 68560.8
        r.FrameSigma = R(moment / it.SectionModulus, 3);   // 12.936
        r.FrameSigmaOk = r.FrameSigma <= it.AllowableSigma;

        // ② 悬臂端挠度 w = q标准·L1·L2³·(6a²−1+3a³)/(24EI)，a=L1/L2
        double a = L1 / L2;   // 0.6222
        double a2 = a * a;
        double a3 = a2 * a;
        double cantileverCoef = 6.0 * a2 - 1.0 + 3.0 * a3;   // 2.0457
        r.CantileverDeflection = R(
            qStd * L1 * L2 * L2 * L2 * cantileverCoef / (24.0 * it.ElasticModulus * it.Inertia), 3);
        r.CantileverLimit = R(L1 / 90.0, 3);   // 3.111
        r.CantileverOk = Math.Abs(r.CantileverDeflection) <= r.CantileverLimit;

        // ③ 跨中挠度 w = q标准·L2⁴·(5−24a²)/(384EI)
        r.MidSpanDeflection = R(
            Math.Abs(qStd * Math.Pow(L2, 4.0) * (5.0 - 24.0 * a2)
                     / (384.0 * it.ElasticModulus * it.Inertia)), 3);
        r.MidSpanLimit = R(L2 / 180.0, 3);   // 2.5
        r.MidSpanOk = r.MidSpanDeflection <= r.MidSpanLimit;

        // ④ 锁点受剪承载力 = π·d²/4·fv
        const double pi = 3.14;
        r.PinShearAllowableN = R(pi * it.PinDiameterMm * it.PinDiameterMm / 4.0 * it.PinShearMpa, 3);

        // ⑤ 所需锁点数 = qd·面积·1.35/(单锁点承载力)
        double totalLoad = it.DesignLoadNmm2 * it.HeightMm * it.WidthMm * it.LoadFactor;
        r.RequiredPins = R(totalLoad / r.PinShearAllowableN, 3);
        r.PinsOk = r.RequiredPins <= it.PinCount;

        // ⑥ 受力最大锁点剪切 = q设计·(2·L1+L2)
        r.MaxPinShearN = R(qDes * (2.0 * L1 + L2), 3);
        r.PinShearOk = r.MaxPinShearN <= r.PinShearAllowableN;

        return r;
    }
}
