using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 型材热应力计算引擎。σT = α·E·ΔT。
/// </summary>
public sealed class ThermalStressEngine
{
    public sealed class Input
    {
        public double Alpha = 0.000023;   // 线胀系数
        public double EMpa = 70000;       // 弹性模量
        public double DeltaT = 80;        // 温差（缺资料取 80℃）
    }

    public sealed class Result
    {
        public double SigmaT;   // 128.8
    }

    public static Result Compute(Input it)
    {
        var r = new Result
        {
            SigmaT = WindLoadEngine.VbaRound(it.Alpha * it.EMpa * it.DeltaT, 3),
        };
        return r;
    }
}

/// <summary>
/// 排烟面积计算引擎（DGJ08-88-2006 4.3.2）。Fp = Fc·sinα。
/// </summary>
public sealed class SmokeVentEngine
{
    public sealed class Input
    {
        public double WidthM = 1.5;       // 开启扇宽 m
        public double HeightM = 1.8;      // 开启扇高 m
        public double AngleDeg = 30;      // 开启角度
    }

    public sealed class Result
    {
        public double VentArea;   // 1.35
    }

    public static Result Compute(Input it)
    {
        double fc = it.WidthM * it.HeightM;   // 窗面积
        double area;
        if (it.AngleDeg > 70)
            area = fc;
        else
            area = fc * Math.Sin(it.AngleDeg * Math.PI / 180.0);
        return new Result { VentArea = WindLoadEngine.VbaRound(area, 3) };
    }
}
