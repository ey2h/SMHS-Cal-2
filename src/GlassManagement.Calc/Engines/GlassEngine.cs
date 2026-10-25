using System;

namespace GlassManagement.Calc.Engines;

/// <summary>四边简支玻璃面板计算结果（单元/构件通用，玻璃屏输出 4 个数字）。</summary>
public readonly record struct GlassResult(
    double Wk,            // 玻璃部分风荷载标准值 N/mm²
    double SelfWeight,    // 自重面荷载 t·γ (N/mm²)，round6
    double Quake,         // 地震作用 5αmax·qg (N/mm²)，round6
    double WStrength,     // 强度组合 1.5wk + 0.7Q (N/mm²)，round6
    double WDeflection,   // 标准组合 wk + 0.5Q (N/mm²)，round6（只用于 θ_σ）
    double ThetaSigma,    // θ_σ = W标准·a⁴/(E·t⁴)，round3
    double EtaSigma,      // 折减系数（强度），3 位
    double ThetaDefl,     // θ_δ = wk·a⁴/(E·t⁴)，round3
    double EtaDefl,       // 折减系数（挠度），3 位
    double BendingCoef,   // 弯矩系数 m（按短/长边比插值）
    double DeflCoef,      // 挠度系数 μ（按短/长边比插值）
    double Sigma,         // 玻璃强度计算值 MPa，round3
    double Stiffness,     // 截面刚度 D = E·t³/[12(1−ν²)] N·mm
    double Deflection,    // 玻璃挠度计算值 mm，round3
    double Limit);        // 挠度容许值 a/60 mm，round3

/// <summary>
/// 玻璃面板（四边简支）计算 —— V31「幕墙计算-玻璃计算」屏。
///
/// <para><b>函数区</b>：<c>0xA1EBF5–0xA21F80</c>（9 个 rtcRound 定稿点），
/// 槽数组 <c>ds:[0x1C911BC]</c>（与横梁 0x1C911A0、立柱 0x1C91168 各自独立）。
/// 系数表方法：η <c>0xA78E85</c>、m <c>0xA795D7</c>、μ <c>0xA79B07</c>。</para>
///
/// <para><b>取证方式</b>：Frida 挂 __vbaVarMul/Div/Pow/Add（<c>v31_frida_ops.py</c>）抓出完整运算 DAG，
/// 每个中间量的数值都能对上；系数表用 capstone 静态读出断点与端点，再用运行期值双点校验。</para>
/// </summary>
public static class GlassEngine
{
    /// <summary>玻璃密度：25.6 kN/m³ → 2.56e-5 N/mm³（常量池 <c>0x402818</c>）。</summary>
    public const double DensityNmm3 = 2.56e-5;

    /// <summary>玻璃弹性模量 N/mm²（代码里以 VT_I4 立即数 0x11940 = 72000 传入，0xA21266）。</summary>
    public const double E = 72_000.0;

    /// <summary>12(1−ν²)，ν=0.2 → 11.52（常量 0xA21D5E 的除数）。</summary>
    public const double PlateFactor = 11.52;

    /// <summary>地震作用系数 5αmax 的 5（<c>0xA20D0A</c> 实测 ×5）。</summary>
    public const double SeismicFactor = 5.0;

    /// <summary>挠度容许值 = 短边/60（<c>0xA21C1F</c> 1100/60=18.333）。</summary>
    public const double DeflectionDivisor = 60.0;

    /// <summary>
    /// 折减系数 η 表（θ 断点 → η）。静态读出 <c>0xA78E85–0xA795D7</c>，
    /// 端点常量分别在 0x402928(0.96) / 0x402920(0.92) / 0x402918(0.84) / 0x402908(0.78)
    /// / 0x4028f8(0.73) / 0x4028f0(0.68) / 0x4028e0(0.65) / 0x4028d8(0.61) / 0x4028c8(0.57)
    /// / 0x4028c0(0.54) / 0x4028b8(0.52) / 0x4028a8(0.51) / 0x401928(0.50)。
    /// </summary>
    public static readonly (double Theta, double Eta)[] EtaTable =
    {
        (5.0, 1.00), (10.0, 0.96), (20.0, 0.92), (40.0, 0.84), (60.0, 0.78),
        (80.0, 0.73), (100.0, 0.68), (120.0, 0.65), (150.0, 0.61), (200.0, 0.57),
        (250.0, 0.54), (300.0, 0.52), (350.0, 0.51), (400.0, 0.50),
    };

    /// <summary>
    /// 弯矩系数 m 表（短/长边比 a/b → m），静态读出 0xA795D7–0xA79B07。
    /// <para>★ 端点归属：磁盘上每段打印的是「上端值、下端值、下端断点、上端断点」，
    /// 首段从 a/b→0 起（0.125 = 单向板弯矩系数）。早前按段内首个断点当端点会整体错一行。</para>
    /// </summary>
    public static readonly (double Ratio, double M)[] BendingTable =
    {
        (0.00, 0.1250), (0.25, 0.1230), (0.33, 0.1180), (0.40, 0.1115), (0.50, 0.1000),
        (0.55, 0.0934), (0.60, 0.0868), (0.65, 0.0804), (0.70, 0.0742), (0.75, 0.0683),
        (0.80, 0.0628), (0.85, 0.0576), (0.90, 0.0528), (0.95, 0.0483), (1.00, 0.0442),
    };

    /// <summary>挠度系数 μ 表（短/长边比 → μ），静态读出 0xA79B07–0xA7A037。</summary>
    public static readonly (double Ratio, double Mu)[] DeflectionTable =
    {
        (0.00, 0.01302), (0.20, 0.01297), (0.25, 0.01282), (0.33, 0.01223), (0.50, 0.01013),
        (0.55, 0.00940), (0.60, 0.00867), (0.65, 0.00796), (0.70, 0.00727), (0.75, 0.00663),
        (0.80, 0.00603), (0.85, 0.00547), (0.90, 0.00496), (0.95, 0.00449), (1.00, 0.00406),
    };

    /// <summary>m 取 4 位小数（运行期乘数恰为 0.0631，而非插值原值 0.06311884）。</summary>
    public const int BendingCoefDigits = 4;

    /// <summary>μ 取 5 位小数（运行期乘数恰为 0.00606，而非插值原值 0.00606478）。</summary>
    public const int DeflectionCoefDigits = 5;

    private static double R(double v, int d) => WindLoadEngine.VbaRound(v, d);

    private static double R3(double v) => R(v, 3);

    /// <summary>分段线性插值（与 0xA79518 段一致的写法：η = 上端 − (上端−下端)·(θ−θ下)/(θ上−θ下)）。</summary>
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

    /// <summary>折减系数 η(θ)，结果取 3 位小数（0.910188→0.91、0.914017→0.914 双点判别）。</summary>
    public static double Eta(double theta)
    {
        var t = new (double, double)[EtaTable.Length];
        for (int i = 0; i < t.Length; i++) t[i] = (EtaTable[i].Theta, EtaTable[i].Eta);
        return R(Interp(theta, t), 3);
    }

    private static double Coef(double ratio, (double, double)[] src)
    {
        var t = new (double, double)[src.Length];
        for (int i = 0; i < t.Length; i++) t[i] = (src[i].Item1, src[i].Item2);
        return Interp(ratio, t);
    }

    /// <summary>
    /// 计算一片四边简支玻璃。
    /// </summary>
    /// <param name="wkNmm2">玻璃部分风荷载标准值 N/mm²（上游传入；0.00137 = 1.37 kPa 屏上显示）。</param>
    /// <param name="aMm">短边 mm（本例 1100）。</param>
    /// <param name="bMm">长边 mm（本例 1380）。</param>
    /// <param name="tMm">玻璃厚度 mm（本例 6）。</param>
    /// <param name="alphaMax">地震影响系数最大值 αmax（参数库「地区参数」，北京 0.16）。</param>
    public static GlassResult Compute(double wkNmm2, double aMm, double bMm, double tMm, double alphaMax)
    {
        double a = Math.Min(aMm, bMm);
        double b = Math.Max(aMm, bMm);

        // —— 荷载组合（0xA20BC2 → 0xA210C3 全链）——
        double qg = R(tMm * DensityNmm3, 6);                       // 0xA20BDB: 6×2.56e-5 = 0.0001536 → 0.000154
        double qe = R(qg * (SeismicFactor * alphaMax), 6);          // 0xA20D42: ×0.8 = 0.0001232 → 0.000123
        double wS = R(1.5 * wkNmm2 + 0.7 * qe, 6);                  // 0xA20FC7: 0.002055+8.61e-5 = 0.002141
        double wD = R(wkNmm2 + 0.5 * qe, 6);                        // 0xA210CD: 0.00137+6.15e-5 = 0.001431

        // —— θ（强度用标准组合、挠度用 wk 纯风，两条链不同源，实测如此）——
        double t4 = Math.Pow(tMm, 4.0);
        double thetaS = R3(wD * Math.Pow(a, 4.0) / (E * t4));       // 0xA21359: 22.4529 → 22.453
        double thetaD = R3(wkNmm2 * Math.Pow(a, 4.0) / (E * t4));   // 0xA21A95: 21.4958

        double etaS = Eta(thetaS);                                  // 0xA2143F: 0.910188 → 0.91
        double etaD = Eta(thetaD);                                  // 0xA21AE4: 0.914017 → 0.914

        // —— 系数（短/长边比）——
        double ratio = a / b;
        double m = R(Coef(ratio, BendingTable), BendingCoefDigits);        // 0xA214CF: 0.0631
        double mu = R(Coef(ratio, DeflectionTable), DeflectionCoefDigits);  // 0xA21B7A: 0.00606

        // —— 强度 σ = 6·m·W强度·a²·η / t²（0xA216F8: 24.7926 → 24.793）——
        double sigma = R3(6.0 * m * wS * a * a * etaS / (tMm * tMm));

        // —— 刚度 D = E·t³/11.52（0xA21D80: 1350000）——
        double d = R(E * Math.Pow(tMm, 3.0) / PlateFactor, 3);

        // —— 挠度 df = η·μ·wk·a⁴/D（0xA21F6C: 8.2296 → 8.23）——
        double df = R3(etaD * mu * wkNmm2 * Math.Pow(a, 4.0) / d);

        double limit = R3(a / DeflectionDivisor);                    // 0xA21C38: 18.333

        return new GlassResult(wkNmm2, qg, qe, wS, wD, thetaS, etaS, thetaD, etaD,
                               m, mu, sigma, d, df, limit);
    }
}
