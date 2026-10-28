using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 点式玻璃幕墙「点式肋（无肋）驳接幕墙设计计算」屏引擎（V31 客户区 903×438）。
///
/// <para><b>算式来源</b>：V31 实机「点式玻璃幕墙 → 点式肋（无肋）驳接」屏的 Variant 实值轨迹
/// （Frida 钩，54 var + 17 定稿点，<c>dianshi_var.json</c>）。</para>
///
/// <para><b>与全玻璃屏（FullGlassEngine）的关键差异</b>：</para>
/// <list type="bullet">
/// <item>玻璃按<b>四点驳接模型</b>计算，m/μ 按「肋间距比 r=肋间距1/肋间距2」查表
///   （m 随 r 增大而<b>增大</b>，与四边支承板相反），玻璃计算跨度 a=肋间距2；</item>
/// <item>玻璃挠度多一个<b>ηδ 折减</b>（θδ=wk·a⁴/(E·t⁴) 查 η 表），且容许值 = 肋间距2/60；</item>
/// <item>玻璃肋挠度用 <b>wD=wk+0.5·qd'</b>（全玻璃用 wk）；</item>
/// <item>输出标签「玻璃肋实际选用宽度/计算宽度」（全玻璃是「截面高度」）；</item>
/// <item><b>无</b>结构胶校核。</item>
/// </list>
/// </summary>
public sealed class DianshiGlassEngine
{
    // ───────────────────────── 四点驳接模型系数表 ─────────────────────────

    /// <summary>四点驳接弯矩系数 m 表（r=肋间距1/肋间距2，m 随 r 增大而增大）。
    /// 来源=V31 常量池原文（0x404B18=0.136 … 0x404AB8=0.154，docs/V31_DIANSHI_UI.md 四节）；
    /// 插值实测 0.1424（dianshi_var.json 运行期直读）命中该表。</summary>
    private static readonly (double R, double M)[] BendingTable =
    {
        (0.00, 0.125), (0.20, 0.126), (0.30, 0.127), (0.40, 0.129), (0.50, 0.130),
        (0.55, 0.132), (0.60, 0.134), (0.65, 0.136), (0.70, 0.138), (0.75, 0.140),
        (0.80, 0.142), (0.85, 0.145), (0.90, 0.148), (0.95, 0.151), (1.00, 0.154),
    };

    /// <summary>四点驳接挠度系数 μ 表。</summary>
    private static readonly (double R, double Mu)[] DeflectionTable =
    {
        (0.00, 0.01302), (0.20, 0.01317), (0.30, 0.01335), (0.40, 0.01367), (0.50, 0.01417),
        (0.55, 0.01451), (0.60, 0.01496), (0.65, 0.01555), (0.70, 0.01630), (0.75, 0.01725),
        (0.80, 0.01842), (0.85, 0.01984), (0.90, 0.02157), (0.95, 0.02363), (1.00, 0.02603),
    };

    /// <summary>玻璃肋强度设计值 [σ]肋（MPa），随肋厚折减（与全玻璃屏同表）。
    /// 9 档全直读：6/8/10/12→19.5、15/19→17.0、20/22/25→14.0（2026-10-28 b7_probe27_fullglass_sigma.json，
    /// [σ]肋 本体在 ra=0xFA0361 以 Mul(4,σ) 出现）。点式肋屏实机肋厚下拉仅 7 档（无 22/25），
    /// 本表按共用折减规则保留 22/25→14.0 兜底。</summary>
    private static readonly (double Thickness, double Sigma)[] RibAllowableTable =
    {
        (6, 19.5), (8, 19.5), (10, 19.5), (12, 19.5),
        (15, 17.0), (19, 17.0),
        (20, 14.0), (22, 14.0), (25, 14.0),
    };

    /// <summary>玻璃肋厚度候选（ComboBox，默认 12）。</summary>
    public static readonly double[] RibThicknessOptions = { 6, 8, 10, 12, 15, 19, 20, 22, 25 };

    public sealed class Input
    {
        /// <summary>玻璃板短边 a（mm，默认 1500，界面 (74,102)）。</summary>
        public double GlassShortSideMm = 1500;

        /// <summary>玻璃板长边（mm，默认 1800，界面 (194,102)）。</summary>
        public double GlassLongSideMm = 1800;

        /// <summary>肋间距 1（mm，默认 1250，界面 (160,123)）。</summary>
        public double RibSpacing1Mm = 1250;

        /// <summary>肋间距 2（mm，默认 1550，界面 (160,143)）。玻璃计算跨度 a=此值。</summary>
        public double RibSpacing2Mm = 1550;

        /// <summary>大面玻璃厚度 t（mm，默认 10，界面 (93,295)）。</summary>
        public double GlassThicknessMm = 10;

        /// <summary>玻璃肋厚度 t肋（mm，默认 12，界面 (483,191)）。</summary>
        public double RibThicknessMm = 12;

        /// <summary>玻璃肋实际截面高度/宽度（mm，默认 200，界面 (486,383)）。</summary>
        public double RibActualHeightMm = 200;

        /// <summary>玻璃肋跨度 L（mm，默认 4000，界面 (486,404)）。</summary>
        public double RibSpanMm = 4000;

        /// <summary>地震影响系数最大值 αmax（默认 0.16）。</summary>
        public double AlphaMax = 0.16;

        /// <summary>风荷载标准值 wk（N/mm²，默认 0.00137）。</summary>
        public double Wk = 0.00137;
    }

    public sealed class Result
    {
        public double WindStandardKpa;         // 1.37
        public double GlassSigma;              // 42.998
        public double GlassSigmaAllowable;     // 84（钢化）
        public bool GlassSigmaOk;
        public double GlassDeflection;         // 22.498
        public double GlassDeflectionAllowable; // 25.833
        public bool GlassDeflectionOk;
        public double RibRequiredWidth;        // 411.19
        public double RibActualWidth;          // 200
        public bool RibWidthOk;
        public double RibDeflection;           // 12.778
        public double RibDeflectionAllowable;  // 20
        public bool RibDeflectionOk;

        // 过程量
        public double SeismicMpa;   // qd' = 0.000205
        public double Qd;           // 0.002198
        public double WD;           // 0.001472
        public double Ratio;        // 0.80645
        public double BendingCoef;  // 0.1424
        public double DeflectionCoef; // 0.0186
        public double ThetaSigma;   // 11.801
        public double EtaSigma;     // 0.953
        public double ThetaDefl;    // 10.983
        public double EtaDefl;      // 0.956
        public double RibAllowableMpa; // 19.5
    }

    public static Result Compute(Input it)
    {
        if (it is null) throw new ArgumentNullException(nameof(it));
        var r = new Result
        {
            WindStandardKpa = WindLoadEngine.VbaRound(it.Wk * 1000.0, 2),
        };

        double E = GlassEngine.E;
        double dens = GlassEngine.DensityNmm3;
        double t = it.GlassThicknessMm;
        double aGlass = it.RibSpacing2Mm;      // 玻璃计算跨度 = 肋间距2
        double aShort = it.GlassShortSideMm;   // 短边（肋公式用）
        double L = it.RibSpanMm;

        // ① qd' = 5·αmax·(γ/1e6)·t
        r.SeismicMpa = WindLoadEngine.VbaRound(5.0 * it.AlphaMax * dens * t, 6);  // 0.000205
        // ② qd = 1.5·wk + 0.7·qd'
        r.Qd = WindLoadEngine.VbaRound(1.5 * it.Wk + 0.7 * r.SeismicMpa, 6);      // 0.002198
        // ③ wD = wk + 0.5·qd'
        r.WD = WindLoadEngine.VbaRound(it.Wk + 0.5 * r.SeismicMpa, 6);            // 0.001472

        // ④ r = 肋间距1/肋间距2，查 m/μ 表
        r.Ratio = it.RibSpacing1Mm / it.RibSpacing2Mm;
        r.BendingCoef = WindLoadEngine.VbaRound(Interp(r.Ratio, BendingTable), 4);       // 0.1424
        r.DeflectionCoef = WindLoadEngine.VbaRound(Interp(r.Ratio, DeflectionTable), 5); // 0.0186

        // ⑤ 强度：θ = wD·a⁴/(E·t⁴) → η → σ = 6·m·qd·a²·η/t²
        r.ThetaSigma = WindLoadEngine.VbaRound(r.WD * Math.Pow(aGlass, 4.0) / (E * Math.Pow(t, 4.0)), 3); // 11.801
        r.EtaSigma = GlassEngine.Eta(r.ThetaSigma);                                   // 0.953
        r.GlassSigma = WindLoadEngine.VbaRound(
            6.0 * r.BendingCoef * r.Qd * aGlass * aGlass * r.EtaSigma / (t * t), 3);  // 42.998
        r.GlassSigmaAllowable = 84;
        r.GlassSigmaOk = r.GlassSigma <= r.GlassSigmaAllowable;

        // ⑥ 挠度：θδ = wk·a⁴/(E·t⁴) → ηδ → w = ηδ·μ·wk·a⁴/D
        r.GlassDeflectionAllowable = WindLoadEngine.VbaRound(aGlass / 60.0, 3);       // 25.833
        double D = WindLoadEngine.VbaRound(E * Math.Pow(t, 3.0) / GlassEngine.PlateFactor, 3); // 6250000
        r.ThetaDefl = WindLoadEngine.VbaRound(it.Wk * Math.Pow(aGlass, 4.0) / (E * Math.Pow(t, 4.0)), 3); // 10.983
        r.EtaDefl = GlassEngine.Eta(r.ThetaDefl);                                     // 0.956
        r.GlassDeflection = WindLoadEngine.VbaRound(
            r.EtaDefl * r.DeflectionCoef * it.Wk * Math.Pow(aGlass, 4.0) / D, 3);     // 22.498
        r.GlassDeflectionOk = r.GlassDeflection <= r.GlassDeflectionAllowable;

        // ⑦ 玻璃肋计算宽度 = √(3·qd·短边·L²/(4·[σ]肋·t肋))
        r.RibAllowableMpa = RibAllowableOf(it.RibThicknessMm);                        // 19.5
        r.RibRequiredWidth = WindLoadEngine.VbaRound(
            Math.Sqrt(3.0 * r.Qd * aShort * L * L / (4.0 * r.RibAllowableMpa * it.RibThicknessMm)), 3); // 411.19
        r.RibActualWidth = it.RibActualHeightMm;
        r.RibWidthOk = r.RibActualWidth >= r.RibRequiredWidth;

        // ⑧ 玻璃肋挠度 = 5·wD·短边·L⁴/(384·E·I)
        double h = it.RibActualHeightMm;
        double I = it.RibThicknessMm * Math.Pow(h, 3.0) / 12.0;
        r.RibDeflection = WindLoadEngine.VbaRound(
            5.0 * r.WD * aShort * Math.Pow(L, 4.0) / (384.0 * E * I), 3);            // 12.778
        r.RibDeflectionAllowable = WindLoadEngine.VbaRound(L / 200.0, 3);             // 20
        r.RibDeflectionOk = r.RibDeflection <= r.RibDeflectionAllowable;

        return r;
    }

    /// <summary>线性插值（r 落在相邻断点间，末段外推用首尾斜率）。</summary>
    private static double Interp(double ratio, (double R, double V)[] table)
    {
        if (ratio <= table[0].R) return table[0].V;
        if (ratio >= table[^1].R) return table[^1].V;
        for (int i = 0; i < table.Length - 1; i++)
        {
            var (r1, v1) = table[i];
            var (r2, v2) = table[i + 1];
            if (ratio >= r1 && ratio <= r2)
                return v1 + (v2 - v1) * (ratio - r1) / (r2 - r1);
        }
        return table[^1].V;
    }

    /// <summary>玻璃肋强度设计值 [σ]肋（MPa），按肋厚折减。未知厚度回落 19.5。</summary>
    private static double RibAllowableOf(double thickness)
    {
        foreach (var (tk, sg) in RibAllowableTable)
            if (Math.Abs(tk - thickness) < 1e-9)
                return sg;
        return 19.5;
    }
}
