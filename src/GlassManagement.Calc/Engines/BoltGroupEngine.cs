using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 螺栓群计算引擎（GB50017 偏心受剪普通螺栓）。
///
/// <para><b>来源</b>：V31 实机「其它计算 → 螺栓群计算」屏预演输出。
/// 受力最大螺栓受剪 + 孔壁承压校核。</para>
///
/// <list type="bullet">
/// <item>Σr² = n·(x²+y²)（螺栓到形心距离平方和）；</item>
/// <item>竖向分量 = V/n + M·y/Σr²；水平分量 = N/n + M·x/Σr²；</item>
/// <item>最大螺栓剪力 = √(竖向²+水平²)；</item>
/// <item>受剪承载力 = π·d²/4·fvb；承压承载力 = d·Σt·fcb。</item>
/// </list>
/// </summary>
public sealed class BoltGroupEngine
{
    public sealed class Input
    {
        /// <summary>螺栓直径 d（mm，默认 5）。</summary>
        public double DiameterMm = 5;

        /// <summary>螺栓数量 n（默认 4 = 2×2）。</summary>
        public int Count = 4;

        /// <summary>螺栓 x 方向间距（mm，默认 60，半间距 30）。</summary>
        public double SpacingX = 60;

        /// <summary>螺栓 y 方向间距（mm，默认 80，半间距 40）。</summary>
        public double SpacingY = 80;

        /// <summary>水平力 N（N，默认 2300）。</summary>
        public double AxialN = 2300;

        /// <summary>竖向剪力 V（N，默认 2500）。</summary>
        public double ShearV = 2500;

        /// <summary>扭矩 M（N·mm，默认 5000）。</summary>
        public double TorqueM = 5000;

        /// <summary>螺栓抗剪强度设计值 fvb（MPa，C级碳钢 140）。</summary>
        public double Fvb = 140;

        /// <summary>连接板承压强度设计值 fcb（MPa，默认 185）。</summary>
        public double Fcb = 185;

        /// <summary>承压板总厚度 Σt（mm，默认 3）。</summary>
        public double SumThickness = 3;
    }

    public sealed class Result
    {
        public double X;                 // 半间距 30
        public double Y;                 // 半间距 40
        public double SumR2;             // 10000
        public double VerticalComponent; // 645
        public double HorizontalComponent; // 590
        public double MaxBoltShear;      // 874.142
        public double ShearAllowable;    // 2747.5
        public bool ShearOk;
        public double BearingStress;     // 874.142
        public double BearingAllowable;  // 2775
        public bool BearingOk;
    }

    public static Result Compute(Input it)
    {
        if (it is null) throw new ArgumentNullException(nameof(it));
        var r = new Result();

        r.X = it.SpacingX / 2.0;   // 30
        r.Y = it.SpacingY / 2.0;   // 40
        r.SumR2 = it.Count * (r.X * r.X + r.Y * r.Y);  // 4×(900+1600)=10000

        r.VerticalComponent = WindLoadEngine.VbaRound(
            it.ShearV / it.Count + it.TorqueM * r.Y / r.SumR2, 3);   // 645
        r.HorizontalComponent = WindLoadEngine.VbaRound(
            it.AxialN / it.Count + it.TorqueM * r.X / r.SumR2, 3);   // 590

        r.MaxBoltShear = WindLoadEngine.VbaRound(
            Math.Sqrt(r.VerticalComponent * r.VerticalComponent
                    + r.HorizontalComponent * r.HorizontalComponent), 3); // 874.142

        // V31 用 π=3.14（VB6 常量），非 Math.PI
        r.ShearAllowable = WindLoadEngine.VbaRound(
            3.14 * it.DiameterMm * it.DiameterMm / 4.0 * it.Fvb, 3);   // 2747.5
        r.ShearOk = r.MaxBoltShear <= r.ShearAllowable;

        r.BearingStress = r.MaxBoltShear;
        r.BearingAllowable = WindLoadEngine.VbaRound(
            it.DiameterMm * it.SumThickness * it.Fcb, 3);                  // 2775
        r.BearingOk = r.BearingStress <= r.BearingAllowable;
        return r;
    }
}
