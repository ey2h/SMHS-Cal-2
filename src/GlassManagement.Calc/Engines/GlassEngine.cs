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

/// <summary>中空玻璃两片各自的结果。</summary>
public readonly record struct GlassPlyResult(
    double Thickness,      // 厚度 mm
    double WkShare,        // 该片分到的风荷载标准值（外片含 1.1），round6
    double WStrength,      // 该片强度组合 1.5·w + 0.7Q，round6
    double WDeflection,    // 该片标准组合 w + 0.5Q，round6（只用于 θ）
    double Theta,          // θ = W标准·a⁴/(E·t⁴)，round3
    double Eta,            // 折减系数，3 位
    double Sigma);         // 该片强度计算值 MPa，round3

/// <summary>中空玻璃整片结果（2 片 σ + 整体挠度）。</summary>
public readonly record struct InsulatedGlassResult(
    GlassPlyResult Outer,
    GlassPlyResult Inner,
    double EquivalentThickness,   // t_e = 0.95·(t₁³+t₂³)^(1/3)，不取整
    double ThetaDefl,             // θ_δ = wk·a⁴/(E·t_e⁴)，round3
    double EtaDefl,               // 3 位
    double Stiffness,             // D = E·t_e³/11.52，round3
    double Deflection,            // df = η·μ·wk·a⁴/D，round3
    double Limit);                // a/60，round3

/// <summary>夹层玻璃整片结果（只出一组 σ + 整体挠度）。</summary>
public readonly record struct LaminatedGlassResult(
    GlassPlyResult Ply,           // 按单片厚度算出的那一片（两片均分，无 1.1）
    double EquivalentThickness,   // t_e = (t₁³+t₂³)^(1/3)，round3（**无 0.95**）
    double ThetaDefl,
    double EtaDefl,
    double Stiffness,
    double Deflection,
    double Limit);

/// <summary>胶片类型（V31 玻璃屏「夹层配置」下拉，ctrl id=5，项序 0=PVB / 1=SGP）。</summary>
public enum InterlayerKind
{
    /// <summary>PVB（聚乙烯醇缩丁醛）：**不参与受力**，胶片厚度下拉 en=0（不可输入）。</summary>
    Pvb = 0,

    /// <summary>SGP（离子型间层）：**参与受力**，胶片厚度下拉 en=1（必须输入）。</summary>
    Sgp = 1,
}

/// <summary>复合玻璃（中空 + 单侧夹胶）整片结果。</summary>
public readonly record struct CompositeGlassResult(
    GlassPlyResult[] Plies,       // 按「室外 → 室内」顺序，逐片 σ
    double TotalStiffness,        // 全部玻璃片 Σt³（分配分母）
    double StrengthThickness,     // 强度用等效厚度 t_s（SGP 专用；PVB 逐片算故为 0）
    double EquivalentThickness,   // t_e，round3
    double ThetaDefl,
    double EtaDefl,
    double Stiffness,             // D
    double Deflection,
    double Limit);

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
    /// 中空玻璃等效厚度系数 0.95（<c>0xA28B80</c>：0.95 × (t₁³+t₂³)^(1/3) = 7.1815）。
    /// </summary>
    public const double InsulatedThicknessFactor = 0.95;

    /// <summary>
    /// 中空玻璃**外片**荷载分配放大系数 1.1（<c>0xA26972</c>：1.1 × wk = 0.001507）。
    /// 内片不乘（<c>0xA26A0B</c> 直接用 wk 分配 → 0.000685 = wk/2）。
    /// </summary>
    public const double InsulatedOuterFactor = 1.1;

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

    /// <summary>
    /// 中空玻璃（两片）计算 —— 走**另一段函数区** <c>0xA26000–0xA29200</c>（17 个定稿点）。
    ///
    /// <para><b>与单片的差别（实测）</b>：</para>
    /// <list type="number">
    /// <item>荷载按 t³ 分到两片，**外片再乘 1.1**、内片不乘（0xA26972 / 0xA26A0B）。</item>
    /// <item>两片各自算 标准组合 / 强度组合 / θ / η / σ（θ 用各自 t⁴）。</item>
    /// <item>挠度不分片，用等效厚度 <c>t_e = 0.95·(t₁³+t₂³)^(1/3)</c>（0xA28B80）。</item>
    /// </list>
    ///
    /// <para><b>实测工况</b>（1100×1380、两片均 6mm 钢化、wk=0.00137、αmax=0.16）：
    /// 外片 σ=14.697 / η=0.949 / θ=12.803 / W₁=0.001217；内片 σ=13.51 / η=0.953 / θ=11.705 / W₁=0.001114；
    /// t_e=7.1815 / θ_δ=10.471 / η=0.958 / D=2315347.704 / df=5.029 / lim=18.333。
    /// 中间量 0.000754(外片 wk 份额)、0.000816(外片标准组合)、0.000685(内片 wk 份额)。</para>
    /// </summary>
    public static InsulatedGlassResult ComputeInsulated(
        double wkNmm2, double aMm, double bMm, double tOuter, double tInner, double alphaMax)
    {
        double a = Math.Min(aMm, bMm);
        double b = Math.Max(aMm, bMm);

        double qg = R(tOuter * DensityNmm3, 6);                 // 0xA26538（实测两片同厚，取外片）
        double qe = R(qg * (SeismicFactor * alphaMax), 6);       // 0xA266B1

        double t3o = Math.Pow(tOuter, 3.0);
        double t3i = Math.Pow(tInner, 3.0);
        double sum3 = t3o + t3i;                                 // 0xA269FE

        double ratio = a / b;
        double m = R(Coef(ratio, BendingTable), BendingCoefDigits);
        double mu = R(Coef(ratio, DeflectionTable), DeflectionCoefDigits);

        GlassPlyResult Ply(double t, double t3, double wkFactor)
        {
            double wShare = R(R(wkFactor * wkNmm2, 6) * t3 / sum3, 6);   // 0xA26A0B / 0xA26A2D
            double wStr = R(1.5 * wShare + 0.7 * qe, 6);                  // 0xA26C8C
            double wDef = R(wShare + 0.5 * qe, 6);                        // 0xA26B43
            double theta = R3(wDef * Math.Pow(a, 4.0) / (E * Math.Pow(t, 4.0)));
            double eta = Eta(theta);
            double sigma = R3(6.0 * m * wStr * a * a * eta / (t * t));
            return new GlassPlyResult(t, wShare, wStr, wDef, theta, eta, sigma);
        }

        var outer = Ply(tOuter, t3o, InsulatedOuterFactor);   // 外片 ×1.1
        var inner = Ply(tInner, t3i, 1.0);                     // 内片不乘

        // ★ t_e 先取 3 位再参与运算：0xA28BA2 定稿点 in=7.1815499844 → out=7.182，
        //   后续 Pow(t_e,3)/(t_e,4) 用的都是 7.182（日志 pre=[7.182, 3/4, ...]）。
        double te = R3(InsulatedThicknessFactor * Math.Pow(sum3, 1.0 / 3.0));
        double thetaD = R3(wkNmm2 * Math.Pow(a, 4.0) / (E * Math.Pow(te, 4.0)));   // 0xA28EB6
        double etaD = Eta(thetaD);
        double d = R(E * Math.Pow(te, 3.0) / PlateFactor, 3);              // 0xA28D00
        double df = R3(etaD * mu * wkNmm2 * Math.Pow(a, 4.0) / d);          // 0xA2917B
        double limit = R3(a / DeflectionDivisor);

        return new InsulatedGlassResult(outer, inner, te, thetaD, etaD, d, df, limit);
    }

    /// <summary>
    /// 夹层玻璃（两片 + 胶片）计算 —— 走**第三段函数区** <c>0xA2DB00–0xA2FA00</c>（17 个定稿点）。
    ///
    /// <para><b>与中空玻璃的三处关键差异（实测）</b>：</para>
    /// <list type="number">
    /// <item><b>外片不乘 1.1</b>：两片均分（0xA2DEEE 段 w=0.000685 = wk/2）。</item>
    /// <item><b>等效厚度无 0.95</b>：<c>t_e = (t₁³+t₂³)^(1/3)</c> = 7.5595 → round3 = 7.56（0xA2F3FC）。</item>
    /// <item><b>自重取两片之和</b>：qg = round6((t₁+t₂)·γ) = 0.0003072（0xA2DBB2），中空只用一片的 0.000154。</item>
    /// </list>
    ///
    /// <para>强度只出**一组**（不像中空出两片各一组）：σ = 6·m·W₁·a²·η/t²，t 用**单片厚度**（不是 t_e）。
    /// 实测：θ=11.705 → η=0.953 → σ=13.51（与中空内片数值相同，因为都均分且 t=6）。</para>
    ///
    /// <para>挠度：θ_δ=8.528 → η=0.972 → D=2700507.6 → df=4.375。</para>
    /// </summary>
    public static LaminatedGlassResult ComputeLaminated(
        double wkNmm2, double aMm, double bMm, double t1, double t2, double alphaMax)
    {
        double a = Math.Min(aMm, bMm);
        double b = Math.Max(aMm, bMm);

        double qg = R((t1 + t2) * DensityNmm3, 6);               // 0xA2DB90（两片之和 = 2×6×γ）
        double qe = R(qg * (SeismicFactor * alphaMax), 6);        // 0xA2DD0E

        double ratio = a / b;
        double m = R(Coef(ratio, BendingTable), BendingCoefDigits);
        double mu = R(Coef(ratio, DeflectionTable), DeflectionCoefDigits);

        // ★ 夹层**不做 t³ 分配**：V31 直接把「每片一半」写进系数（0xA2DEAE / 0xA2E1EA）：
        //   标准组合 = 0.5wk + 0.25Q、强度组合 = 0.75wk + 0.35Q（0.25 = 0.5×0.5、0.35 = 0.7×0.5）。
        double wShare = R(0.5 * wkNmm2, 6);                       // 0.000685
        double wDef = R(0.5 * wkNmm2 + 0.25 * qe, 6);             // 0xA2DEE1 = 0.0007465
        double wStr = R(0.75 * wkNmm2 + 0.35 * qe, 6);            // 0xA2E21D = 0.0011136
        double theta = R3(wDef * Math.Pow(a, 4.0) / (E * Math.Pow(t1, 4.0)));   // 0xA2E683
        double eta = Eta(theta);                                   // 0.953
        double sigma = R3(6.0 * m * wStr * a * a * eta / (t1 * t1));            // 0xA2EA28
        var ply = new GlassPlyResult(t1, wShare, wStr, wDef, theta, eta, sigma);

        // 挠度：等效厚度（无 0.95），先 round3
        double sum3 = Math.Pow(t1, 3.0) + Math.Pow(t2, 3.0);
        double te = R3(Math.Pow(sum3, 1.0 / 3.0));                 // 0xA2F3FC = 7.56
        double thetaD = R3(wkNmm2 * Math.Pow(a, 4.0) / (E * Math.Pow(te, 4.0)));  // 0xA2F709
        double etaD = Eta(thetaD);                                 // 0.972
        double d = R(E * Math.Pow(te, 3.0) / PlateFactor, 3);      // 0xA2F553
        double df = R3(etaD * mu * wkNmm2 * Math.Pow(a, 4.0) / d); // 0xA2F9CE
        double limit = R3(a / DeflectionDivisor);

        return new LaminatedGlassResult(ply, te, thetaD, etaD, d, df, limit);
    }

    /// <summary>
    /// SGP 复合玻璃（中空 + 单侧夹胶）—— V31 玻璃屏第五段函数区 <c>0xA4AA25–0xA54FFD</c>。
    ///
    /// <para><b>与 PVB 的三处结构差异（全部运行期校验过）</b></para>
    /// <list type="number">
    /// <item><b>强度用等效厚度</b>（不是逐片）：
    /// <c>Γ = 1/(1 + 9.6·E·kΓ₁·t_f / (kΓ₂·(t+t_f)²·a²))</c>（0xA5104D–0xA51118，取 4 位），
    /// <c>t_s = round3(sqrt(d³/(t + 2Γy)))</c>，<c>d=t₁+t₂</c>、<c>y=(t+t_f)/2</c>（0xA5162D–0xA516B2）。</item>
    /// <item><b>折减系数恒 1</b>（0xA534A9 乘 1，不查 η 表）。</item>
    /// <item><b>强度荷载用整片</b>：<c>W=round6(1.5·wk·d³/Σ + 0.7Q)</c>，<c>Σ=d³+t₃³</c>（0xA522A7）。</item>
    /// </list>
    ///
    /// <para><b>挠度</b>：<c>t_e = round3(0.95·Σ^(1/3))</c> = 11.856（Σ=1944），θ_δ=1.410 → η=1 → D=10415846.9 → df=1.167。</para>
    ///
    /// <para><b>实测工况</b>（三片均 6mm、胶片 0.76、wk=0.00137、αmax=0.16、1100×1380）：
    /// Γ=0.8967、t_s=11.969、W标准=0.001341、W强度=0.001999、σ=6.392。</para>
    /// </summary>
    /// <param name="kGamma1">Γ 公式里的 k₁₉ = 137.093（V31 slot[19]，物理含义待考）。</param>
    /// <param name="kGamma2">Γ 公式里的 k₁₄ = 11.3（V31 slot[14]，即夹层配置框的 11.3）。</param>
    public static CompositeGlassResult ComputeCompositeSgp(
        double wkNmm2, double aMm, double bMm,
        double t1, double t2, double t3, double filmThickness, double alphaMax,
        double kGamma1 = 137.093, double kGamma2 = 11.3)
    {
        double a = Math.Min(aMm, bMm);
        double b = Math.Max(aMm, bMm);

        double qg = R((t1 + t2) * DensityNmm3, 6);              // 0.000307（两片和）
        double qe = R(qg * (SeismicFactor * alphaMax), 6);       // 0.000246

        double d = t1 + t2;                                      // 12
        double y = (t1 + filmThickness) / 2.0;                   // 3.38
        double eSum = Math.Pow(d, 3.0) + Math.Pow(t3, 3.0);       // 1944
        double ratio = a / b;
        double m = R(Coef(ratio, BendingTable), BendingCoefDigits);
        double mu = R(Coef(ratio, DeflectionTable), DeflectionCoefDigits);

        // Γ 剪切传递系数（0xA51118），取 4 位
        double gamma = R(1.0 / (1.0 + 9.6 * E * kGamma1 * filmThickness
                                     / (kGamma2 * Math.Pow(t1 + filmThickness, 2.0) * a * a)), 4);

        // 强度等效厚度 t_s = sqrt(d³/(t+2Γy))（0xA516B2）
        double ts = R3(Math.Sqrt(Math.Pow(d, 3.0) / (t1 + 2.0 * gamma * y)));

        // 整片强度组合（0xA522A7）
        double wStr = R(1.5 * (wkNmm2 * Math.Pow(d, 3.0) / eSum) + 0.7 * qe, 6);
        double wStd = R(wkNmm2 * Math.Pow(d, 3.0) / eSum + 0.5 * qe, 6);

        double sigma = R3(6.0 * m * wStr * a * a * 1.0 / (ts * ts));   // η 恒 1（0xA534A9）

        double te = R3(InsulatedThicknessFactor * Math.Pow(eSum, 1.0 / 3.0));   // 11.856
        double thetaD = R3(wkNmm2 * Math.Pow(a, 4.0) / (E * Math.Pow(te, 4.0))); // 1.410
        double etaD = 1.0;                                        // SGP 亦为 1（槽[64]=1）
        double stiff = R(E * Math.Pow(te, 3.0) / PlateFactor, 3);  // 10415846.938
        double df = R3(etaD * mu * wkNmm2 * Math.Pow(a, 4.0) / stiff);   // 1.167
        double limit = R3(a / DeflectionDivisor);

        var plies = new[]
        {
            new GlassPlyResult(t1, R(wkNmm2 * Math.Pow(d, 3.0) / eSum, 6), wStr, wStd, 0, 1.0, sigma),
            new GlassPlyResult(t2, 0, 0, 0, 0, 1.0, sigma),
            new GlassPlyResult(t3, 0, 0, 0, 0, 1.0, 0),
        };
        return new CompositeGlassResult(plies, eSum, ts, te, thetaD, etaD, stiff, df, limit);
    }

    /// <summary>
    /// **双中空玻璃**（3 片 2 腔）—— V31 函数区 <c>0xA5Cxxx–0xA5F2xx</c>。
    ///
    /// <para><b>分配权重 = 衰减厚度的立方</b>（实测三点定案）：
    /// <c>D₁=(0.95t₁)³</c>、<c>D₂=D₃=(0.95²t₂)³</c>，
    /// <c>wᵢ = 1.1?(wk)·Dᵢ/ΣD</c>（只有外片乘 1.1）。</para>
    ///
    /// <para>实测 6/6/6（wk=0.00137、Q=0.0001232）：
    /// D=(185.19, 158.78, 158.78)、Σ=502.75；
    /// w=(0.000555, 0.000433, 0.000433) → 槽 [13]/[20]；
    /// W标准=(0.000617, 0.000495, 0.000495) → 槽 [14]/[21]；
    /// W强度=(0.000919, 0.000736, 0.000736) → 槽 [15]/[22]；
    /// σ=(11.262, 9.16, 9.16)、df=3.806。</para>
    ///
    /// <para><b>注意</b>：w 先不取整（直接进组合），只有 W标准/W强度 取 6 位 ——
    /// 若先把 w round6 再组合，中片 W标准 会变 0.000494（实测 0.000495）。</para>
    /// </summary>
    public static (GlassPlyResult[] Plies, double EquivalentThickness, double Deflection, double Limit)
        ComputeDoubleInsulated(double wkNmm2, double aMm, double bMm, double t1, double t2, double t3, double alphaMax)
    {
        double a = Math.Min(aMm, bMm);
        double b = Math.Max(aMm, bMm);

        double qg = R(t1 * DensityNmm3, 6);                     // 用首片厚度（槽[7]=0.000154）
        double qeRaw = qg * (SeismicFactor * alphaMax);          // 未取整
        double qe = R(qeRaw, 6);                                 // 0.000123（阈值显示用）

        double d1 = Math.Pow(InsulatedThicknessFactor * t1, 3.0);
        double d2 = Math.Pow(InsulatedThicknessFactor * InsulatedThicknessFactor * t2, 3.0);
        double d3 = Math.Pow(InsulatedThicknessFactor * InsulatedThicknessFactor * t3, 3.0);
        double sum = d1 + d2 + d3;

        double ratio = a / b;
        double m = R(Coef(ratio, BendingTable), BendingCoefDigits);
        double mu = R(Coef(ratio, DeflectionTable), DeflectionCoefDigits);

        var ts = new[] { t1, t2, t3 };
        var ds = new[] { d1, d2, d3 };
        var plies = new GlassPlyResult[3];
        for (int i = 0; i < 3; i++)
        {
            double f = i == 0 ? InsulatedOuterFactor : 1.0;
            double wRaw = f * wkNmm2 * ds[i] / sum;              // 不先取整
            double wStr = R(1.5 * wRaw + 0.7 * qeRaw, 6);
            double wStd = R(wRaw + 0.5 * qeRaw, 6);
            double theta = R3(wStd * Math.Pow(a, 4.0) / (E * Math.Pow(ts[i], 4.0)));
            double eta = Eta(theta);
            double sigma = R3(6.0 * m * wStr * a * a * eta / (ts[i] * ts[i]));
            plies[i] = new GlassPlyResult(ts[i], R(wRaw, 6), wStr, wStd, theta, eta, sigma);
        }

        double te = R3(Math.Pow(sum, 1.0 / 3.0));
        double thetaD = R3(wkNmm2 * Math.Pow(a, 4.0) / (E * Math.Pow(te, 4.0)));
        double stiff = R(E * Math.Pow(te, 3.0) / PlateFactor, 3);
        double df = R3(Eta(thetaD) * mu * wkNmm2 * Math.Pow(a, 4.0) / stiff);
        double limit = R3(a / DeflectionDivisor);
        _ = qe;
        return (plies, te, df, limit);
    }

    /// <summary>
    /// **双中空玻璃**的挠度等效厚度 —— 逐层衰减，不是简单乘一个 0.95。
    ///
    /// <para>V31 函数区 <c>0xA5Cxxx–0xA5F2xx</c>。四点实测定案（1100×1380、wk=0.00137）：</para>
    /// <list type="table">
    /// <item><term>6/6/6</term><description>t_e = 7.952</description></item>
    /// <item><term>8/6/6</term><description>t_e = 9.112</description></item>
    /// <item><term>6/8/6</term><description>t_e = 8.964</description></item>
    /// <item><term>6/6/8</term><description>t_e = 8.964</description></item>
    /// </list>
    ///
    /// <para>拟合结果：<c>t_e = [ (0.95·t₁)³ + (0.95²·t₂)³ + (0.95²·t₃)³ ]^(1/3)</c> ——
    /// 每穿过一层中空腔厚度打 0.95 折：外片 1 层（0.95），中/内片 2 层（0.95²）。四点全部落在 round3 内。</para>
    ///
    /// <para>⚠ 与「中空+夹层」不同：后者是 <c>0.95·(Σt³)^(1/3)</c>（全体折一次，实测 8.221）。
    /// V31 两种配置的等效厚度口径不一致，按实测各记各的。</para>
    /// </summary>
    public static double DoubleInsulatedThickness(double t1, double t2, double t3)
    {
        double e1 = Math.Pow(InsulatedThicknessFactor * t1, 3.0);
        double e2 = Math.Pow(InsulatedThicknessFactor * InsulatedThicknessFactor * t2, 3.0);
        double e3 = Math.Pow(InsulatedThicknessFactor * InsulatedThicknessFactor * t3, 3.0);
        return R3(Math.Pow(e1 + e2 + e3, 1.0 / 3.0));
    }

    /// <summary>
    /// 复合玻璃（中空 + 单侧夹胶，共 3 片）—— 走**第四段函数区** <c>0xA46000–0xA49B00</c>（17 个定稿点）。
    ///
    /// <para><b>三级受力流程（V31 的合并实现，与「先分侧、再分片」数学等价）</b></para>
    ///
    /// <list type="number">
    /// <item><b>第一级：总刚度</b> S = Σ tᵢ³（把全部玻璃片一起算，实测分母 648 = 3×216）。
    /// V31 直接算每片 wᵢ = 1.1?·wk·tᵢ³/S；而「先按侧刚度 S_side/S 分到侧、再按 tᵢ³/S_side 分到片」
    /// 乘起来恰等于 tᵢ³/S ——所以两种写法结果相同，V31 省了一层。</item>
    /// <item><b>第二级：外片放大</b> 只有**最外侧那片**乘 1.1（0xA46001：1.1×wk×216）。</item>
    /// <item><b>第三级：逐片组合与强度</b> W标准ᵢ=round6(wᵢ+0.5Q)、W强度ᵢ=round6(1.5wᵢ+0.7Q)、
    /// θᵢ=round3(W标准ᵢ·a⁴/(E·tᵢ⁴))、σᵢ=round3(6·m·W强度ᵢ·a²·ηᵢ/tᵢ²)。</item>
    /// </list>
    ///
    /// <para><b>挠度</b>不分片：<c>t_e = round3(0.95·(Σtᵢ³)^(1/3))</c>（实测 0.95×8.6532 = 8.2208 → 8.221），
    /// 再 θ_δ=6.099 → η=0.991 → D=3472593.612 → df=3.469。</para>
    ///
    /// <para><b>实测工况</b>（中空+夹层、三片均 6mm、wk=0.00137、αmax=0.16、1100×1380、PVB）：
    /// 外侧夹胶片 w=0.000502 → W标准 0.000564 / W强度 0.000839 / θ 8.849 / σ=10.345（两片同值）；
    /// 室内单片 w=0.000457 → 0.000518 / 0.000772 / θ 8.128 / σ=9.578。</para>
    ///
    /// <para>⚠ <b>SGP 未实现</b>：实测 SGP 走另一段（0xA52xxx–0xA54Axx），
    /// <c>t_e = 0.95×(12³+6³)^(1/3)</c>（把夹层侧两片合成 12mm）→ 11.856，
    /// 但 σ 内部用的 t = <b>11.969</b> 来源尚未定位，不得臆造，故此处显式抛异常。</para>
    /// </summary>
    /// <param name="plyThicknesses">按「室外 → 室内」顺序的玻璃厚度 mm（3 片）。</param>
    public static CompositeGlassResult ComputeComposite(
        double wkNmm2, double aMm, double bMm, double[] plyThicknesses,
        InterlayerKind interlayer, double alphaMax)
    {
        if (interlayer == InterlayerKind.Sgp)
        {
            throw new NotSupportedException(
                "SGP 分支未闭合：V31 函数区 0xA52xxx–0xA54Axx，t_e=0.95×(Σt³)^(1/3) 已实测，" +
                "但 σ 公式里的 t=11.969 来源未定位（见 docs/V31_GLASS_PAGE.md 9.6）。");
        }
        if (plyThicknesses.Length < 2)
        {
            throw new ArgumentException("复合玻璃至少需要 2 片", nameof(plyThicknesses));
        }

        double a = Math.Min(aMm, bMm);
        double b = Math.Max(aMm, bMm);

        double qg = R(plyThicknesses[0] * DensityNmm3, 6);        // 0xA46476（单份）
        double qe = R(qg * (SeismicFactor * alphaMax), 6);         // 0xA46599

        double sum3 = 0;
        foreach (var t in plyThicknesses) sum3 += Math.Pow(t, 3.0);   // 0xA4608D 648

        double ratio = a / b;
        double m = R(Coef(ratio, BendingTable), BendingCoefDigits);
        double mu = R(Coef(ratio, DeflectionTable), DeflectionCoefDigits);

        var plies = new GlassPlyResult[plyThicknesses.Length];
        for (int i = 0; i < plyThicknesses.Length; i++)
        {
            double t = plyThicknesses[i];
            double wkFactor = i == 0 ? InsulatedOuterFactor : 1.0;    // 只有最外片 ×1.1
            double wShare = R(R(wkFactor * wkNmm2, 6) * Math.Pow(t, 3.0) / sum3, 6);
            double wStr = R(1.5 * wShare + 0.7 * qe, 6);
            double wDef = R(wShare + 0.5 * qe, 6);
            double theta = R3(wDef * Math.Pow(a, 4.0) / (E * Math.Pow(t, 4.0)));
            double eta = Eta(theta);
            double sigma = R3(6.0 * m * wStr * a * a * eta / (t * t));
            plies[i] = new GlassPlyResult(t, wShare, wStr, wDef, theta, eta, sigma);
        }

        double te = R3(InsulatedThicknessFactor * Math.Pow(sum3, 1.0 / 3.0));   // 8.221
        double thetaD = R3(wkNmm2 * Math.Pow(a, 4.0) / (E * Math.Pow(te, 4.0)));
        double etaD = Eta(thetaD);
        double d = R(E * Math.Pow(te, 3.0) / PlateFactor, 3);
        double df = R3(etaD * mu * wkNmm2 * Math.Pow(a, 4.0) / d);
        double limit = R3(a / DeflectionDivisor);

        return new CompositeGlassResult(plies, sum3, 0, te, thetaD, etaD, d, df, limit);
    }
}
