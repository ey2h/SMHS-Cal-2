using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 自攻螺钉及自攻自钻螺钉计算引擎（V31「其它计算>自攻钉及自攻自钻螺钉计算」，917×492）。
/// 依据上海幕墙规范 DG/TJ08-56-2019 第 10.7.2，总体安全系数 3.0。
/// 取证参照 zigong_var.json（4 rounds + 20 var，0x1C57922–0x1C5B2D2）。
/// </summary>
public static class SelfTappingScrewEngine
{
    public sealed class Input
    {
        /// <summary>螺钉公称直径 D（mm），默认 3.5（ST3.5）。</summary>
        public double DiameterMm = 3.5;

        /// <summary>螺纹咬合深度 Le（mm），默认 3.5。</summary>
        public double EngageLengthMm = 3.5;

        /// <summary>螺纹间距 P（mm），ST3.5 = 1.3。</summary>
        public double PitchMm = 1.3;

        /// <summary>铝型材拉伸屈服强度 Fty（MPa），6063-T5 = 110。</summary>
        public double BaseYieldMpa = 110.0;

        /// <summary>铝型材拉伸极限强度 Ftu（MPa），6063-T5 = 160。</summary>
        public double BaseUltimateMpa = 160.0;

        /// <summary>螺钉螺纹内径 d1（mm），ST3.5 = 2.51（有效截面 As=π·(d1/2)²）。</summary>
        public double InnerDiameterMm = 2.51;

        /// <summary>螺钉抗拉强度设计值 f（MPa），4.6/4.8 C级碳钢 = 170。</summary>
        public double ScrewTensileMpa = 170.0;

        /// <summary>实际承受拉力 N（N），默认 950。</summary>
        public double TensileForceN = 950.0;
    }

    public sealed class Result
    {
        public double Rn;              // 与铝材咬合抗拉承载力 1132.6
        public bool RnOk;
        public double EffectiveAreaMm2; // 4.948
        public double Ns;              // 螺钉自身抗拉 841.16
        public bool NsOk;
        /// <summary>分段状态：0=2mm≤Le&lt;2P，1=2P≤Le&lt;4P，2=4P≤Le≤8mm。</summary>
        public int Segment;
    }

    /// <summary>计算自攻螺钉抗拉承载力。</summary>
    public static Result Compute(Input it)
    {
        if (it is null) throw new ArgumentNullException(nameof(it));
        var r = new Result();

        double D = it.DiameterMm;
        double Le = it.EngageLengthMm;
        double P = it.PitchMm;

        // Rn 三段分段函数（DG/TJ08-56-2019 10.7.2）
        if (Le >= 2.0 && Le < 2.0 * P)
        {
            r.Segment = 0;
            r.Rn = WindLoadEngine.VbaRound(0.56 * D * Le * it.BaseYieldMpa, 3);
        }
        else if (Le >= 2.0 * P && Le < 4.0 * P)
        {
            r.Segment = 1;
            r.Rn = WindLoadEngine.VbaRound(
                0.56 * D * it.BaseYieldMpa * (4.0 * P - Le)
                + 1.52 * D * it.BaseUltimateMpa * (Le - 2.0 * P), 3);
        }
        else // 4P ≤ Le ≤ 8
        {
            r.Segment = 2;
            r.Rn = WindLoadEngine.VbaRound(0.76 * D * Le * it.BaseUltimateMpa, 3);
        }
        r.RnOk = r.Rn >= it.TensileForceN;

        // 螺钉自身抗拉 NS = As·f，As = π·(d1/2)²（0x1C5B2B7–0x1C5AA11，π=Math.PI）
        r.EffectiveAreaMm2 = WindLoadEngine.VbaRound(
            Math.PI * Math.Pow(it.InnerDiameterMm / 2.0, 2.0), 3);
        r.Ns = WindLoadEngine.VbaRound(r.EffectiveAreaMm2 * it.ScrewTensileMpa, 3);
        r.NsOk = r.Ns >= it.TensileForceN;

        return r;
    }
}
