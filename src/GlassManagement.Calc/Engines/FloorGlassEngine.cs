using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 地板玻璃「地板玻璃结构计算」屏引擎（V31 客户区 945×542，按 JGJ113 规范）。
///
/// <para><b>模型</b>：四边支承板受活荷载（非风荷载）。双片夹层玻璃按等效厚度。
/// 算式来源：V31 实机 var 轨迹 <c>diban_var.json</c>。</para>
///
/// <list type="bullet">
/// <item>活荷载 qk（KPa，默认 2）；</item>
/// <item>等效厚度 te=(2·t³)^(1/3)（双片夹层）；</item>
/// <item>qd' = (γ/1e6)·2·t（双片自重）；</item>
/// <item>q标准 = qk + qd'；q设计 = 1.5·qk + 1.3·qd'（活荷载组合系数 1.3）；</item>
/// <item>σ = 6·m·(q设计/2)·a²/t²（每片分担一半，单片厚度）；</item>
/// <item>挠度 = μ·q标准·a⁴/D，D=E·te³/11.52；</item>
/// <item>挠度设计值 = a/200。</item>
/// </list>
/// </summary>
public sealed class FloorGlassEngine
{
    public sealed class Input
    {
        /// <summary>活荷载标准值 qk（KPa，默认 2）。</summary>
        public double LiveLoadKpa = 2;

        /// <summary>玻璃短边 a（mm，默认 1500）。</summary>
        public double ShortSideMm = 1500;

        /// <summary>玻璃长边 b（mm，默认 2000）。</summary>
        public double LongSideMm = 2000;

        /// <summary>单片玻璃厚度 t（mm，默认 8，双片夹层）。</summary>
        public double GlassThicknessMm = 8;

        /// <summary>玻璃强度设计值（MPa，钢化默认 42）。</summary>
        public double SigmaAllowable = 42;
    }

    public sealed class Result
    {
        public double LiveLoadKpa;          // 2
        public double EquivalentThickness;  // te = 10.079
        public double SeismicLoad;          // qd' = 0.00041
        public double StandardLoad;         // q标准 = 0.00241
        public double DesignLoad;           // q设计 = 0.003533
        public double Sigma;                // 25.443
        public double SigmaAllowable;       // 42
        public bool SigmaOk;
        public double Deflection;           // 12.64
        public double DeflectionAllowable;  // 7.5
        public bool DeflectionOk;
    }

    public static Result Compute(Input it)
    {
        if (it is null) throw new ArgumentNullException(nameof(it));
        var r = new Result { LiveLoadKpa = it.LiveLoadKpa };

        double E = GlassEngine.E;
        double dens = GlassEngine.DensityNmm3;   // γ/1e6
        double t = it.GlassThicknessMm;
        double a = it.ShortSideMm;
        double b = it.LongSideMm;
        double qk = it.LiveLoadKpa / 1000.0;     // KPa → N/mm²

        // 等效厚度 te = (2·t³)^(1/3)
        r.EquivalentThickness = WindLoadEngine.VbaRound(Math.Pow(2.0 * t * t * t, 1.0 / 3.0), 3);

        // qd' = (γ/1e6)·2·t
        r.SeismicLoad = WindLoadEngine.VbaRound(dens * 2.0 * t, 6);
        // q标准 = qk + qd'
        r.StandardLoad = WindLoadEngine.VbaRound(qk + r.SeismicLoad, 6);
        // q设计 = 1.5·qk + 1.3·qd'
        r.DesignLoad = WindLoadEngine.VbaRound(1.5 * qk + 1.3 * r.SeismicLoad, 6);

        // m、μ 查四边支承板表（r=a/b）
        double ratio = a / b;
        double m = WindLoadEngine.VbaRound(Interp(ratio, GlassEngine.BendingTable), GlassEngine.BendingCoefDigits);
        double mu = WindLoadEngine.VbaRound(Interp(ratio, GlassEngine.DeflectionTable), GlassEngine.DeflectionCoefDigits);

        // σ = 6·m·(q设计/2)·a²/t²（q设计/2 先 round6）
        double qHalf = WindLoadEngine.VbaRound(r.DesignLoad / 2.0, 6);
        r.Sigma = WindLoadEngine.VbaRound(
            6.0 * m * qHalf * a * a / (t * t), 3);
        r.SigmaAllowable = it.SigmaAllowable;
        r.SigmaOk = r.Sigma <= r.SigmaAllowable;

        // 挠度 = μ·q标准·a⁴/D，D = E·te³/11.52
        double D = WindLoadEngine.VbaRound(E * Math.Pow(r.EquivalentThickness, 3.0) / GlassEngine.PlateFactor, 3);
        r.Deflection = WindLoadEngine.VbaRound(mu * r.StandardLoad * Math.Pow(a, 4.0) / D, 3);
        r.DeflectionAllowable = WindLoadEngine.VbaRound(a / 200.0, 3);
        r.DeflectionOk = r.Deflection <= r.DeflectionAllowable;

        return r;
    }

    /// <summary>分段线性插值（同 GlassEngine 口径）。</summary>
    private static double Interp(double x, (double X, double Y)[] table)
    {
        if (x <= table[0].X) return table[0].Y;
        for (int i = 1; i < table.Length; i++)
        {
            if (x <= table[i].X)
            {
                double x0 = table[i - 1].X, y0 = table[i - 1].Y;
                double x1 = table[i].X, y1 = table[i].Y;
                return y1 - (y1 - y0) * (x1 - x) / (x1 - x0);
            }
        }
        return table[^1].Y;
    }
}
