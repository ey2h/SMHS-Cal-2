using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 扭剪钢销/螺栓/螺钉计算引擎（V31「其它计算>钢销/螺栓(扭+剪)计算」，945×542）。
/// 偏心受剪（GB50017），钢销做法。
/// 取证参照 gangxiao_var.json（5 rounds + 22 var，0x1B764D3–0x1B7C2C5）。
/// </summary>
public static class PinBoltEngine
{
    public sealed class Input
    {
        /// <summary>钢销数量 n，默认 4。</summary>
        public double Count = 4.0;

        /// <summary>钢销公称直径 d（mm），默认 5。</summary>
        public double DiameterMm = 5.0;

        /// <summary>水平（x）方向间距（mm），默认 60。</summary>
        public double SpacingXMm = 60.0;

        /// <summary>垂直（y）方向间距（mm），默认 80。</summary>
        public double SpacingYMm = 80.0;

        /// <summary>型材壁厚 t（mm），默认 3。</summary>
        public double WallThicknessMm = 3.0;

        /// <summary>钢销抗剪强度设计值 fv（MPa），50级不锈钢 = 190。</summary>
        public double PinShearMpa = 190.0;

        /// <summary>型材承压强度设计值 fcb（MPa），6061-T4 = 210。</summary>
        public double BaseBearingMpa = 210.0;

        /// <summary>轴力 N（N），默认 2300。</summary>
        public double AxialForceN = 2300.0;

        /// <summary>剪力 V（N），默认 2500。</summary>
        public double ShearForceN = 2500.0;

        /// <summary>弯矩 M（N·mm），默认 5000。</summary>
        public double MomentNmm = 5000.0;
    }

    public sealed class Result
    {
        public double MaxShearN;          // 874.142
        public double ShearAllowableN;    // 3728.75
        public bool ShearOk;
        public double BearingAllowableN;  // 3150
        public bool BearingOk;
    }

    /// <summary>计算扭剪钢销。π 用 3.14（VB6 常量）。</summary>
    public static Result Compute(Input it)
    {
        if (it is null) throw new ArgumentNullException(nameof(it));
        var r = new Result();

        double xh = it.SpacingXMm / 2.0;   // 30
        double yh = it.SpacingYMm / 2.0;   // 40
        double sumR2 = it.Count * (xh * xh + yh * yh);   // 4×(900+1600)=10000

        // 水平分量（含轴力）：N/n + M·x_half/Σr² = 575+15 = 590
        double fx = it.AxialForceN / it.Count + it.MomentNmm * xh / sumR2;
        // 垂直分量（含剪力）：V/n + M·y_half/Σr² = 625+20 = 645
        double fy = it.ShearForceN / it.Count + it.MomentNmm * yh / sumR2;

        r.MaxShearN = WindLoadEngine.VbaRound(Math.Sqrt(fx * fx + fy * fy), 3);

        // 抗剪设计值：π·d²/4·fv = 3.14×25×190/4 = 3728.75
        const double pi = 3.14;
        r.ShearAllowableN = WindLoadEngine.VbaRound(pi * it.DiameterMm * it.DiameterMm / 4.0 * it.PinShearMpa, 3);
        r.ShearOk = r.MaxShearN <= r.ShearAllowableN;

        // 型材壁承压：d·t·fcb = 5×3×210 = 3150
        r.BearingAllowableN = WindLoadEngine.VbaRound(it.DiameterMm * it.WallThicknessMm * it.BaseBearingMpa, 3);
        r.BearingOk = r.MaxShearN <= r.BearingAllowableN;

        return r;
    }
}
