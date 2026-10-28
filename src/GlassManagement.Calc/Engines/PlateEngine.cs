using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 板材（铝单板）板块计算引擎。
///
/// <para><b>算式来源</b>：V31 实机 <c>铝单板幕墙计算-板块计算</c> 屏的 Variant 实值轨迹
/// （Frida 钩 <c>msvbvm60</c> 的 <c>__vbaVarMul/VarDiv/VarAdd/VarPow</c> 与 <c>rtcRound</c>）
/// ＋ 脱壳镜像常量池逐值读出。取证地址与四面对拍见 <c>docs/V31_PLATE_UI.md</c>。</para>
///
/// <para><b>严禁反推</b>：本文件所有系数表均来自常量池原文
/// （m <c>0x407A60–0x407AA8</c>、α <c>0x4029E0–0x402A30</c>、K 见 <c>0xF4B69A</c>、
/// 加肋板 <c>0xF4D300</c> / <c>0xF4C5B6</c>），不存在任何由实测结果拟合出来的数值。</para>
/// </summary>
public sealed class PlateEngine
{
    // ───────────────────────── 系数表（常量池原文） ─────────────────────────

    /// <summary>共用的比值断点（11 点，0.50→1.00，步长 0.05）。</summary>
    private static readonly double[] RatioX =
        { 0.50, 0.55, 0.60, 0.65, 0.70, 0.75, 0.80, 0.85, 0.90, 0.95, 1.00 };

    /// <summary>无肋板弯矩系数 m（<c>0x407A60</c>=0.0489 … <c>0x407AA8</c>=0.1022，此处按 x 升序）。</summary>
    private static readonly double[] MPlain =
        { 0.1022, 0.0961, 0.09, 0.0839, 0.0781, 0.0725, 0.0671, 0.0621, 0.0574, 0.053, 0.0489 };

    /// <summary>无肋板挠度系数 α（<c>0x4029E0</c>=0.00406 … <c>0x402A30</c>=0.01013，按 x 升序）。</summary>
    private static readonly double[] APlain =
        { 0.01013, 0.0094, 0.00867, 0.00796, 0.00727, 0.00663, 0.00603, 0.00547, 0.00496, 0.00449, 0.00406 };

    /// <summary>
    /// 加肋板弯矩系数 m（<c>0xF4D300</c> 第一张表，按 x 升序）。
    /// 第二张表首值 0.0843 用于 x&lt;0.5 的兜底平均。
    /// </summary>
    private static readonly double[] MRib =
        { 0.1212, 0.1187, 0.1158, 0.1124, 0.1087, 0.1048, 0.1007, 0.0965, 0.0922, 0.088, 0.0839 };

    /// <summary>加肋板 m 的第二张表首值（x=0.5 处，<c>0xF4D9EE</c> 链）。</summary>
    private const double MRibTable2At05 = 0.0843;

    /// <summary>加肋板挠度系数 α（<c>0xF4C5B6</c> 第一张表，按 x 升序）。</summary>
    private static readonly double[] ARib =
        { 0.00504, 0.00492, 0.00472, 0.00448, 0.00422, 0.00399, 0.00376, 0.00352, 0.00329, 0.00306, 0.00285 };

    /// <summary>刚度折减系数 K 的断点 λ（<c>0xF4B69A</c>）。</summary>
    private static readonly double[] KX =
        { 5, 10, 20, 40, 60, 80, 100, 120, 150, 200, 250, 300, 350, 400 };

    /// <summary>K 的对应值（λ≤5 → 1.0；λ≥400 → 0.4）。</summary>
    private static readonly double[] KV =
        { 1.0, 0.95, 0.9, 0.81, 0.74, 0.69, 0.64, 0.61, 0.54, 0.5, 0.46, 0.43, 0.41, 0.4 };

    // ───────────────────────── 输入 ─────────────────────────

    public sealed class Input
    {
        /// <summary>短边尺寸 a（mm）。</summary>
        public double ShortSideMm = 1100;
        /// <summary>长边尺寸 b（mm）。</summary>
        public double LongSideMm = 1380;
        /// <summary>长边等分数 n（1..4）。n=1 走 A 板，n≥2 走 B 板 + 加强肋。</summary>
        public int LongSideDivision = 1;
        /// <summary>板厚 t（mm）。</summary>
        public double ThicknessMm = 2.5;
        /// <summary>A/B 板抗弯强度设计值 [σ]（MPa），取自参数库「铝单板」表。</summary>
        public double AllowableStressMpa = 54;
        /// <summary>面板口径风荷载标准值 wk（N/mm²）。</summary>
        public double Wk = 0.00137;
        /// <summary>地震影响系数最大值 αmax（参数库「地区参数」）。</summary>
        public double AlphaMax = 0.16;
        /// <summary>弹性模量 E（MPa），铝 70000。</summary>
        public double ElasticModulusMpa = 70000;
        /// <summary>泊松比 ν。</summary>
        public double PoissonRatio = 0.3;
        /// <summary>板自重密度 γ（kN/m³），铝 27。</summary>
        public double DensityKnM3 = 27;
        /// <summary>是否计算加强肋（界面「计算肋」）。</summary>
        public bool UseRib = false;
        /// <summary>加强肋算法二（界面「算法二」）。</summary>
        public bool RibAlgorithmTwo = false;
        /// <summary>板挠度限值分母：60 或 90（界面「短边/60」「短边/90」）。</summary>
        public double DeflectionDivisor = 60;
        /// <summary>肋挠度限值分母：250 / 180 / 120（界面 Ly/250、Ly/180、Ly/120）。</summary>
        public double RibDeflectionDivisor = 180;
        /// <summary>肋截面惯性矩 I（mm⁴），界面「肋参数」默认 25340。</summary>
        public double RibInertiaMm4 = 25340;
        /// <summary>肋截面抵抗矩 W（mm³），界面「肋参数」默认 2028。</summary>
        public double RibSectionModulusMm3 = 2028;
        /// <summary>肋抗弯强度设计值 [σ]（MPa），6063-T5 → 90。</summary>
        public double RibAllowableStressMpa = 90;
        /// <summary>
        /// 肋材质代号（界面「肋材质」8 个单选，顺序与窗体资源 `0x52E6xx` 一致：
        /// 6063-T5 / 6063-T6 / Q235 / Q355 / 6061-T4 / 6061-T6 / 6063A-T5 / 6063A-T6）。
        /// </summary>
        public string RibMaterialCode = "6063-T5";
        /// <summary>界面「冷弯薄壁型钢」复选框。只对钢材质生效（见 <see cref="RibProps"/>）。</summary>
        public bool RibColdFormed = false;
    }

    /// <summary>肋材质的三个派生量。</summary>
    public readonly record struct RibMaterialProps(double AllowableStressMpa,
                                          double ElasticModulusMpa,
                                          double PlasticFactor);

    /// <summary>
    /// 肋材质 → [σ] / E / 塑性发展系数 γ。
    ///
    /// <para><b>取证</b>（全部为实机「加强肋强度设计值」与「挠度计算值」直读，不是反推）：
    /// 同一算例（n=2、Lx=690、B=1100、t=2.5、I=25340、W=2028）逐一切换 8 个单选，读屏上输出。</para>
    ///
    /// <list type="table">
    /// <item><term>铝 6 种</term><description>[σ]=90/150/90/200/135/160；E=70000；γ=1.00（σ 恒 93.581）</description></item>
    /// <item><term>Q235 / Q355</term><description>[σ]=215/315；E=206000；γ=1.05 → σ=89.125=93.581/1.05</description></item>
    /// <item><term>+ 冷弯薄壁型钢</term><description>[σ] 降为 205/310，且 γ 回 1.00（σ 回到 93.581）；铝不受影响</description></item>
    /// </list>
    ///
    /// <para>E 的静态证据：常量池相邻一对 <c>0x4038B8=206000.0</c>、<c>0x4038C0=70000.0</c>；
    /// 轨迹 <c>0xF410F2</c> 直接 <c>Div … ∘ I4:70000</c>。γ 的静态证据：池中唯一 <c>1.05 @ 0x4020E0</c>，
    /// 轨迹 <c>0xF3F774</c> 除的 BSTR 就是 <c>"1.00"</c>（铝），钢时为 <c>"1.05"</c>。</para>
    /// </summary>
    public static RibMaterialProps RibProps(string code, bool coldFormed)
    {
        bool isSteel = code is "Q235" or "Q355";
        if (!isSteel)                      // 铝：冷弯薄壁开关无影响（实机 6063-T5 勾选前后同为 90）
        {
            return code switch
            {
                "6063-T6" => new(150, 70000, 1.0),
                "6061-T4" => new(90, 70000, 1.0),
                "6061-T6" => new(200, 70000, 1.0),
                "6063A-T5" => new(135, 70000, 1.0),
                "6063A-T6" => new(160, 70000, 1.0),
                _ => new(90, 70000, 1.0),   // 6063-T5（界面默认）
            };
        }
        return (code, coldFormed) switch
        {
            ("Q235", false) => new(215, 206000, 1.05),
            ("Q235", true) => new(205, 206000, 1.0),
            ("Q355", false) => new(315, 206000, 1.05),
            _ => new(310, 206000, 1.0),     // Q355 + 冷弯薄壁
        };
    }


    // ───────────────────────── 输出 ─────────────────────────

    public sealed class Result
    {
        /// <summary>面板部分风荷载标准值（KPa，2 位）。</summary>
        public double WindStandardKpa;
        /// <summary>面荷载设计值 qd（N/mm²，已 round6）。</summary>
        public double Qd;
        /// <summary>面荷载标准值 qk（N/mm²，已 round6）。</summary>
        public double Qk;
        /// <summary>地震作用项（已 round6）。</summary>
        public double SeismicTerm;

        /// <summary>短边/长边比值 x = a/b（未取倒数前）。</summary>
        public double Ratio;
        /// <summary>计算跨度 a（mm）。</summary>
        public double SpanAMm;
        /// <summary>计算跨度 b（mm）。</summary>
        public double SpanBMm;
        public double M;
        public double Alpha;
        public double LambdaStrength;
        public double LambdaDeflection;
        public double KStrength;
        public double KDeflection;
        public double PlateStiffnessD;

        /// <summary>是否走 B 板（加肋）分支。</summary>
        public bool IsRibbedPanel;

        // A 板（n=1 或不算肋）
        public double A_StressComputed;
        public double A_StressAllowable;
        public double A_DeflectionComputed;
        public double A_DeflectionAllowable;

        // B 板（加肋）
        public double B_StressComputed;
        public double B_StressAllowable;
        public double B_DeflectionComputed;
        public double B_DeflectionAllowable;

        // 加强肋
        public double RibSpanMm;
        public double RibLoadWidthMm;
        public double RibMoment;
        public double RibStressComputed;
        public double RibStressAllowable;
        public double RibDeflectionComputed;
        public double RibDeflectionAllowable;

        public bool A_StressOk => A_StressComputed <= A_StressAllowable;
        public bool A_DeflectionOk => A_DeflectionComputed <= A_DeflectionAllowable;
        public bool B_StressOk => B_StressComputed <= B_StressAllowable;
        public bool B_DeflectionOk => B_DeflectionComputed <= B_DeflectionAllowable;
        public bool RibStressOk => RibStressComputed <= RibStressAllowable;
        public bool RibDeflectionOk => RibDeflectionComputed <= RibDeflectionAllowable;
    }

    // ───────────────────────── 主流程 ─────────────────────────

    public static Result Compute(Input it)
    {
        if (it is null) throw new ArgumentNullException(nameof(it));

        int n = Math.Max(1, Math.Min(4, it.LongSideDivision));
        double t = it.ThicknessMm;
        double E = it.ElasticModulusMpa;
        double nu = it.PoissonRatio;

        var r = new Result
        {
            WindStandardKpa = WindLoadEngine.VbaRound(it.Wk * 1000.0, 2),
        };

        // 3.1 荷载：g_t = γ·t/1e6；eq = round6(5·αmax·g_t)
        double gt = it.DensityKnM3 * t / 1e6;
        double eq = WindLoadEngine.VbaRound(5.0 * it.AlphaMax * gt, 6);
        r.SeismicTerm = eq;
        double qd = WindLoadEngine.VbaRound(1.5 * it.Wk + 0.7 * eq, 6);
        double qk = WindLoadEngine.VbaRound(1.0 * it.Wk + 0.5 * eq, 6);
        r.Qd = qd;
        r.Qk = qk;

        bool ribbed = it.UseRib && n >= 2;
        r.IsRibbedPanel = ribbed;

        // 跨度与受荷宽度
        double lx = it.LongSideMm / n;
        double a, b;
        if (ribbed)
        {
            a = Math.Min(lx, it.ShortSideMm);
            b = Math.Max(lx, it.ShortSideMm);
        }
        else
        {
            a = it.ShortSideMm;
            b = it.LongSideMm;
        }
        r.SpanAMm = a;
        r.SpanBMm = b;
        r.Ratio = a / b;

        // 3.3 / 3.4 系数
        r.M = ribbed ? MRibCoef(r.Ratio) : MPlainCoef(r.Ratio);
        r.Alpha = ribbed ? ARibCoef(r.Ratio) : APlainCoef(r.Ratio);

        // 3.2 刚度参数 λ 与 K
        double a2 = a * a;
        double a4 = a2 * a2;
        double t2 = t * t;
        double t4 = t2 * t2;
        r.LambdaStrength = WindLoadEngine.VbaRound(qk * a4 / (E * t4), 3);
        r.LambdaDeflection = WindLoadEngine.VbaRound(it.Wk * a4 / (E * t4), 3);
        r.KStrength = KCoef(r.LambdaStrength);
        r.KDeflection = KCoef(r.LambdaDeflection);

        // 3.5 强度 / 挠度
        double d = E * t * t * t / (12.0 * (1.0 - nu * nu));
        r.PlateStiffnessD = d;

        double sigma = 6.0 * r.M * qd * a2 * r.KStrength / t2;
        double w = r.KDeflection * r.Alpha * it.Wk * a4 / d;
        double lim = a / it.DeflectionDivisor;

        if (ribbed)
        {
            r.B_StressComputed = WindLoadEngine.VbaRound(sigma, 3);
            r.B_StressAllowable = it.AllowableStressMpa;
            r.B_DeflectionComputed = WindLoadEngine.VbaRound(w, 3);
            r.B_DeflectionAllowable = WindLoadEngine.VbaRound(lim, 3);

            // 3.6 加强肋（L = 短边，Lx = 长边/n）
            double L = it.ShortSideMm;
            double lw = lx;
            double ratio2 = lw / L;
            double mom = qd * lw * L * L / 24.0 * (3.0 - ratio2 * ratio2);
            r.RibSpanMm = L;
            r.RibLoadWidthMm = lw;
            r.RibMoment = WindLoadEngine.VbaRound(mom, 3);
            // ★ 肋应力 = M / γ / W。γ 就是 0xF4A1F8 除的那个 BSTR：
            //   铝 = "1.00"（不折减），钢 = "1.05"（截面塑性发展系数，常量池 0x4020E0）。
            //   实机：铝 93.581 = 189781.99/2028；Q235 89.125 = 93.581/1.05。
            //   不是第一页的 γ0（默认 1.1）——写成 γ0 会把 93.581 压成 85.074。
            var rib = RibProps(it.RibMaterialCode, it.RibColdFormed);
            r.RibStressComputed = WindLoadEngine.VbaRound(
                mom / rib.PlasticFactor / it.RibSectionModulusMm3, 3);
            r.RibStressAllowable = rib.AllowableStressMpa;

            double u = (lw / 2.0) / L;
            double u2 = u * u;
            double u4 = u2 * u2;
            double kdf = 3.125 - 5.0 * u2 + 2.0 * u4;
            // 挠度用标准值 wk（实机 0xF49C25：0.00137×690=0.9453、0.00137×460=0.6302）
            // E 取肋材质自身的：铝 70000、钢 206000（实机 Q235 → 2.93 = 8.623/2.943）。
            double wr = it.Wk * lw * Math.Pow(L, 4) / 240.0 / rib.ElasticModulusMpa
                        / it.RibInertiaMm4 * kdf;
            r.RibDeflectionComputed = WindLoadEngine.VbaRound(wr, 3);
            r.RibDeflectionAllowable = WindLoadEngine.VbaRound(L / it.RibDeflectionDivisor, 3);
        }
        else
        {
            r.A_StressComputed = WindLoadEngine.VbaRound(sigma, 3);
            r.A_StressAllowable = it.AllowableStressMpa;
            r.A_DeflectionComputed = WindLoadEngine.VbaRound(w, 3);
            r.A_DeflectionAllowable = WindLoadEngine.VbaRound(lim, 3);
        }

        return r;
    }

    // ───────────────────────── 系数函数 ─────────────────────────

    /// <summary>
    /// 刚度折减系数 K(λ)。<c>0xF4B69A</c>。
    /// λ≤5 → 1（原程序此处返回整型）；λ≥400 → 0.4；其余分段线性，round5。
    /// </summary>
    public static double KCoef(double lambda)
    {
        if (double.IsNaN(lambda)) return 0;
        if (lambda <= KX[0]) return 1.0;
        if (lambda >= KX[KX.Length - 1]) return 0.4;
        return Interp(KX, KV, lambda);
    }

    /// <summary>无肋板 m(x)。<c>0xF4BDEC</c>：x&gt;1 → 1/x；x&lt;0.5 → 0.1022。</summary>
    public static double MPlainCoef(double x)
    {
        x = NormalizeRatio(x);
        if (x < RatioX[0]) return MPlain[0];
        return Interp(RatioX, MPlain, x);
    }

    /// <summary>无肋板 α(x)。<c>0xF4C1D1</c>：x&lt;0.5 → 0.01013。</summary>
    public static double APlainCoef(double x)
    {
        x = NormalizeRatio(x);
        if (x < RatioX[0]) return APlain[0];
        return Interp(RatioX, APlain, x);
    }

    /// <summary>
    /// 加肋板 m(x)。<c>0xF4D300</c> 表一。
    /// x&lt;0.5 时原程序走兜底平均：<c>(0.1212 + 0.0843) / 2 = 0.10275</c>
    /// （<c>0xF3847A __vbaVarAdd</c> → <c>0xF384A3 ÷2</c>）。
    /// </summary>
    public static double MRibCoef(double x)
    {
        x = NormalizeRatio(x);
        if (x < RatioX[0]) return (MRib[0] + MRibTable2At05) / 2.0;
        return Interp(RatioX, MRib, x);
    }

    /// <summary>加肋板 α(x)。<c>0xF4C5B6</c> 表一：x&lt;0.5 → 0.00504（直接钳位，不平均）。</summary>
    public static double ARibCoef(double x)
    {
        x = NormalizeRatio(x);
        if (x < RatioX[0]) return ARib[0];
        return Interp(RatioX, ARib, x);
    }

    /// <summary>原程序在比值 &gt;1 时取倒数（<c>0xF4BE3E</c> 后的 <c>fld1; fdiv</c>）。</summary>
    private static double NormalizeRatio(double x)
    {
        if (x <= 0) return 0;
        return x > 1.0 ? 1.0 / x : x;
    }

    /// <summary>
    /// 分段线性插值：<c>round5( v1 − (v1−v2)·(r1−x)/(r1−r2) )</c>，
    /// 与 <c>0xF4BD2D–0xF4BD78</c> 的 x87 序列（fsub / fmulp / fdivp / fsubr → rtcRound 5）同构。
    /// </summary>
    private static double Interp(double[] xs, double[] vs, double x)
    {
        int i = 1;
        while (i < xs.Length - 1 && x > xs[i]) i++;
        double r1 = xs[i - 1], r2 = xs[i];
        double v1 = vs[i - 1], v2 = vs[i];
        double res = v1 - (v1 - v2) * (r1 - x) / (r1 - r2);
        return WindLoadEngine.VbaRound(res, 5);
    }
}
