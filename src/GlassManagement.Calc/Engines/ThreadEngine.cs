using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 螺钉与内螺纹计算引擎（V31「其它计算>型材螺纹计算」，945×542）。
/// 螺钉直接旋进型材（6063-T5 等铝合金），校验抗拉/抗剪/拉剪复合/型材侧壁承压/表面挤压/牙根切出。
/// 取证参照 luowen_var.json（9 rounds + 25 var，0x1B6FFE0–0x1B714B5）。
/// </summary>
public static class ThreadEngine
{
    public sealed class Input
    {
        /// <summary>螺钉公称直径 d（mm），默认 6.0（M6）。</summary>
        public double DiameterMm = 6.0;

        /// <summary>螺距 P（mm），M6 普通螺纹 = 1.0。</summary>
        public double PitchMm = 1.0;

        /// <summary>螺钉有效（应力）截面积 As（mm²）。M6 普通螺钉 = 20.1。</summary>
        public double EffectiveAreaMm2 = 20.1;

        /// <summary>螺钉抗拉强度设计值 ft（MPa）。不锈钢 A1-50 = 230。</summary>
        public double ScrewTensileMpa = 230.0;

        /// <summary>螺钉抗剪强度设计值 fv（MPa）。不锈钢 A1-50 = 175。</summary>
        public double ScrewShearMpa = 175.0;

        /// <summary>型材（母材）承压强度设计值 fcb（MPa）。6063-T5 = 185。</summary>
        public double BaseBearingMpa = 185.0;

        /// <summary>型材（母材）牙根切出（剪切）强度设计值 fv_base（MPa）。6063-T5 = 55。</summary>
        public double BaseShearMpa = 55.0;

        /// <summary>旋入长度（=型材壁厚）L（mm），默认 4。</summary>
        public double EngageLengthMm = 4.0;

        /// <summary>拉力设计值 N（N），默认 3500。</summary>
        public double TensileForceN = 3500.0;

        /// <summary>剪力设计值 V（N），默认 3500。</summary>
        public double ShearForceN = 3500.0;
    }

    public sealed class Result
    {
        public double TensileAllowableN;   // 4623
        public bool TensileOk;
        public double ShearAllowableN;     // 3517.5
        public bool ShearOk;
        public double CombinedStress;      // 1.25
        public bool CombinedOk;
        public double WallBearingAllowableN;   // 4440
        public bool WallBearingOk;
        public double ThreadBearingAllowableN; // 4485.876
        public bool ThreadBearingOk;
        public double ThreadShearOutAllowableN; // 3626.7
        public bool ThreadShearOutOk;
    }

    /// <summary>计算螺钉与内螺纹。σ 值取整 round3。</summary>
    public static Result Compute(Input it)
    {
        if (it is null) throw new ArgumentNullException(nameof(it));
        var r = new Result();

        // ① 螺钉抗拉/抗剪（0x1B6FFE0 / 0x1B702CC）：As·ft、As·fv
        r.TensileAllowableN = WindLoadEngine.VbaRound(it.EffectiveAreaMm2 * it.ScrewTensileMpa, 3);
        r.ShearAllowableN = WindLoadEngine.VbaRound(it.EffectiveAreaMm2 * it.ScrewShearMpa, 3);
        r.TensileOk = it.TensileForceN <= r.TensileAllowableN;
        r.ShearOk = it.ShearForceN <= r.ShearAllowableN;

        // ② 拉剪复合（0x1B70644–0x1B706B2）：√((N/Nt)²+(V/Nv)²)
        double a = it.TensileForceN / r.TensileAllowableN;
        double b = it.ShearForceN / r.ShearAllowableN;
        r.CombinedStress = WindLoadEngine.VbaRound(Math.Sqrt(a * a + b * b), 3);
        r.CombinedOk = r.CombinedStress <= 1.0;

        // ③ 型材侧壁承压（0x1B709BB/0x1B709D7）：d·L·fcb
        r.WallBearingAllowableN = WindLoadEngine.VbaRound(it.DiameterMm * it.EngageLengthMm * it.BaseBearingMpa, 3);
        r.WallBearingOk = it.TensileForceN <= r.WallBearingAllowableN;

        // ④ 表面挤压（0x1B70CB9–0x1B7109F）：π·d2·(5P/8)·L/(2·cos30°)·fcb
        //     d2 = round3(d − 0.6495·P)
        double d2 = WindLoadEngine.VbaRound(it.DiameterMm - 0.6495 * it.PitchMm, 3);
        double threadDepth = 5.0 * it.PitchMm / 8.0;      // 0.625·P
        const double pi = 3.14;                           // ★ VB6 常量，非 Math.PI
        const double cos30 = 0.866;                       // ★ VB6 常量
        double bearArea = pi * d2 * threadDepth * it.EngageLengthMm / (2.0 * cos30);
        r.ThreadBearingAllowableN = WindLoadEngine.VbaRound(bearArea * it.BaseBearingMpa, 3);
        r.ThreadBearingOk = it.TensileForceN <= r.ThreadBearingAllowableN;

        // ⑤ 牙根切出（0x1B71431–0x1B714B5）：π·d·(7/8)·L·fv_base
        double shearOutArea = pi * it.DiameterMm * (7.0 / 8.0) * it.EngageLengthMm;
        r.ThreadShearOutAllowableN = WindLoadEngine.VbaRound(shearOutArea * it.BaseShearMpa, 3);
        r.ThreadShearOutOk = it.TensileForceN <= r.ThreadShearOutAllowableN;

        return r;
    }
}
