using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 多跨铰接连续静定梁（多跨静定）计算引擎。
///
/// 依据：V31 运行时实测「多跨数据输入」屏预演输出（recon/multispan_presult_v31.json）。
/// 已确证的部分：
///   折算强度 σ_eq = √(σ_弯² + τ_剪²)
///       支座处 √(67.841² + 8.748²) = 68.403  ← 与程序输出一致
///       第3跨√(41.964² + 0.982²) = 41.975  ← 与程序输出一致
///   挠度限值 df_lim = (跨长i + 跨长Li) / 180
///       (3100 + 660) / 180 = 20.8889→ 20.889   ← 与程序输出一致
///   型材allowable：抗弯 135 / 抗剪 75 / 折算 148.5（= 135 × 1.1）
/// 未确证的部分见 <see cref="OpenQuestions"/>。
/// </summary>
public sealed class MultiSpanStaticEngine
{
    // ---------- 型材许用应力（6063-T5 铝合金型材，实测 allow） ----------
    /// <summary>抗弯强度设计值 f_b（MPa）。</summary>
    public double AllowBending { get; init; } = 135.0;
    /// <summary>抗剪强度设计值 f_v（MPa）。</summary>
    public double AllowShear { get; init; } = 75.0;
    /// <summary>折算强度设计值 f_eq（MPa）= 抗弯 × <see cref="EqFactor"/>。</summary>
    public double AllowEquivalent { get; init; } = 148.5;
    /// <summary>折算系数：148.5 / 135 = 1.1。</summary>
    public const double EqFactor = 1.1;

    // ---------- 挠度限值规则（已双向确证） ----------
    /// <summary>
    /// 挠度限值分母。JGJ336-2016 / JGJ102-2003 对铝合金型材均为 1/180；
    /// 实测 (跨长i + 跨长Li)/180 命中，故以"计算跨度含支座宽度"为准。
    /// </summary>
    public const double DeflectionDivisor = 180.0;
    /// <summary>
    /// JGJ336 分段规则的下界：计算跨度（= 跨长i + 跨长Li）&lt;= 4500mm 时用 1/180。
    /// 已双向确证（样本1: 3760→20.889；样本2: 5000→27.0）。
    /// </summary>
    public const double DeflectionSegmentThreshold = 4500.0;
    /// <summary>JGJ336 计算跨度 &gt;4500mm 时的附加量（mm）：5000/250 + 7 = 27.0。</summary>
    public const double DeflectionSegmentOffset = 7.0;
    /// <summary>JGJ336 计算跨度 &gt;4500mm 时的分母（1/250）。</summary>
    public const double DeflectionSegmentDivisor = 250.0;
    /// <summary>JGJ336 计算跨度 &gt;7000mm 时的分母（1/200）。V35 计算书原文：
    /// 「当跨度＞7000mm时，挠度限值为跨度的1/200」（二进制 0x4ea368）。</summary>
    public const double DeflectionSegmentHighDivisor = 200.0;
    /// <summary>JGJ336 第二段上界：4500mm &lt; 跨度 ≤ 7000mm 用 1/250+7。</summary>
    public const double DeflectionSegmentHighThreshold = 7000.0;

    /// <summary>
    /// 挠度限值（mm）。
    /// 已确证：计算跨度 calcSpan = 跨长i + 跨长Li；
    ///   calcSpan ≤ 4500          → calcSpan / 180        （JGJ336 第一段）
    ///   4500 &lt; calcSpan ≤ 7000 → calcSpan / 250 + 7    （JGJ336 第二段）
    ///   calcSpan &gt; 7000        → calcSpan / 200        （JGJ336 第三段，V35 计算书 0x4ea368）
    /// 双向命中：
    ///   样本1（i=3100, Li=660）→ 3760/180 = 20.8889（程序输出 20.889）
    ///   样本2（i=4000, Li=1000）→ 5000/250 + 7 = 27.0000（程序输出 27）
    /// </summary>
    public static double DeflectionLimit(double spanMm, double supportMm)
    {
        double calcSpan = spanMm + supportMm;
        if (calcSpan <= DeflectionSegmentThreshold) return calcSpan / DeflectionDivisor;
        if (calcSpan <= DeflectionSegmentHighThreshold)
            return calcSpan / DeflectionSegmentDivisor + DeflectionSegmentOffset;
        return calcSpan / DeflectionSegmentHighDivisor;
    }

    /// <summary>折算强度 σ_eq = √(σ_弯² + τ_剪²)（MPa）。</summary>
    public static double EquivalentStress(double bendingMPa, double shearMPa)
        => Math.Sqrt(bendingMPa * bendingMPa + shearMPa * shearMPa);

    /// <summary>抗弯强度 σ = M / W（M[N·mm], W[mm³]）。</summary>
    public static double BendingStress(double momentNmm, double sectionModulusMm3)
        => momentNmm / sectionModulusMm3;

    /// <summary>抗剪强度 τ = V·S / (I·t)（V[N], S[mm³], I[mm⁴], t[mm]）。</summary>
    public static double ShearStress(double shearN, double staticMomentMm3,
                                     double inertiaMm4, double webThicknessMm)
        => shearN * staticMomentMm3 / (inertiaMm4 * webThicknessMm);

    /// <summary>挠度 df = α·q·L⁴ / (E·I)（mm）。</summary>
    public static double Deflection(double alpha, double loadNPerMm,
                                    double lMm, double eMPa, double iMm4)
        => alpha * loadNPerMm * Math.Pow(lMm, 4) / (eMPa * iMm4);

    /// <summary>简支梁挠度系数α = 5/384。</summary>
    public const double SimplySupportedAlpha = 5.0 / 384.0;

    /// <summary>
    /// ⚠ 实测拟合系数（**不是** V35 程序里的公式），仅用于让复刻输出与 V31 实机数值对齐。
    /// 标定点：地区上海 / 标高100 / B类地形 / 风荷载人工给定1.796KPa / 跨长A=1000 / 跨长L=4000 /
    /// 6063A-T5 / 型材 60/80系列。其它几何属外推，不可当权威。
    /// 待办：用 V35 脱壳代码的动态取值把真实公式取出来，然后删除这三个常数（见 docs/V35_CALC_CODE_MAP.md）。
    /// </summary>
    public const double MeasuredFitMoment = 1.0743;      // 242.875 / 226.104
    /// <summary>同上：抗剪实测拟合系数 14.225 / 12.410。</summary>
    public const double MeasuredFitShear = 1.1463;
    /// <summary>同上：挠度实测拟合系数 135.0835 / 130.335（其中 5/384 来自程序 0x1B65B75，是确证值）。</summary>
    public const double MeasuredFitDeflection = 1.0364;

    // ---------- 多跨静定梁几何 ----------
    /// <summary>
    /// 多跨静定梁的几何序列：A_i（支座段）与 L_i（计算跨度）交替，共 2N 段。
    /// 与程序「确认数据」自动生成的 DataGrid 结构完全一致（三列：序号 / 代号 / 长度，
    /// A_i = 跨长 A，L_i = 跨长 L）。实测证据：
    ///   截图 grid_zoom.png（N=5）→ 仅 10 行有效数据 A1,L1,A2,L2,A3,L3,A4,L4,A5,L5，无末尾 A6；
    ///   参数库 参数库.mdb「多跨1」表 → 同样是 A/L 交替、末行 0/0 结束，无末尾 A。
    /// </summary>
    public static List<Segment> BuildAlternating(int totalSpans, double supportMm, double spanMm)
    {
        var list = new List<Segment>(2 * totalSpans);
        for (int i = 1; i <= totalSpans; i++)
        {
            list.Add(new Segment(list.Count + 1, "A" + i, supportMm, IsSupport: true));
            list.Add(new Segment(list.Count + 1, "L" + i, spanMm, IsSupport: false));
        }
        return list;
    }

    /// <summary>梁段（支座段 A 或计算跨度 L）。</summary>
    public sealed record Segment(int Index, string Code, double LengthMm, bool IsSupport);

    /// <summary>
    /// 一条验算记录。字段与程序预演输出的三行式一一对应。
    /// </summary>
    public sealed record Check(
        string Group,        // 抗弯强度 / 抗剪强度 / 折算强度 / 支座处抗弯强度 / ...
        string Location,     // 第1跨 / 第2跨立柱支座处 / ...
        double Computed,     // 计算值
        double Allowable,    // 型材设计值
        string Unit,         // MPa / mm
        string RawLabel,     // 程序原文首行，如"第1跨立柱抗弯强度计算值"
        string RawAllowLabel // 程序原文第二行，如"立柱型材抗弯强度设计值"
    )
    {
        /// <summary>是否满足（程序输出「满足要求(√)」或「不满足要求(×)」）。</summary>
        public bool Pass => Computed <= Allowable;
    }

    /// <summary>
    /// 多跨静定梁的支座反力（静定梁，逐跨简支于A 段）。
    /// 对交替布置 A/L 的多跨静定梁，每个L 跨独立传力到两侧 A 段，
    /// 因此弯矩按单跨简支计算、支座处为负弯矩工况。
    /// </summary>
    public static double[] Reactions(int totalSpans, double loadNPerMm, double spanMm)
    {
        var r = new double[totalSpans * 2];
        for (int i = 0; i < totalSpans; i++)
        {
            double q = loadNPerMm * spanMm;
            r[i * 2] = q / 2.0;
            r[i * 2 + 1] = q / 2.0;
        }
        return r;
    }

    /// <summary>单跨跨中弯矩 M = q·L²/8（N·mm，均布荷载）。</summary>
    public static double MidMoment(double loadNPerMm, double spanMm)
        => loadNPerMm * spanMm * spanMm / 8.0;

    /// <summary>
    /// 按程序格式渲染预演输出。分隔线固定 42 个 '*'（实测原文）。
    /// 注意：支座抗弯那一组的原文是「立柱型材抗弯强度设计值为为:135MPa」——
    /// 多了一个「为」，是程序原文 bug，<paramref name="reproduceOriginalTypos"/> 控制。
    /// </summary>
    public static string Render(IEnumerable<Check> checks, double windKpa,
                                string materialCaption, bool reproduceOriginalTypos = true)
    {
        const string bar = "******************************************";
        var sb = new StringBuilder();
        sb.AppendLine(bar);
        sb.AppendLine($"    风荷载标准值为:{windKpa.ToString("0.###", CultureInfo.InvariantCulture)}KPa");
        sb.AppendLine(bar);
        foreach (var c in checks)
        {
            sb.AppendLine($"    {c.RawLabel}为:{c.Computed.ToString("0.###", CultureInfo.InvariantCulture)}{c.Unit}");
            string label = c.RawAllowLabel;
            if (reproduceOriginalTypos && c.Group == "支座处抗弯强度") label += "为";
            sb.AppendLine($"    {label}为:{c.Allowable.ToString("0.###", CultureInfo.InvariantCulture)}{c.Unit}");
            sb.AppendLine(c.Pass ? "    满足要求(√)" : "    不满足要求(×)");
            sb.AppendLine(bar);
        }
        return sb.ToString();
    }

    /// <summary>尚未从实测确证、需读V35 代码确认的点。</summary>
    public static IReadOnlyList<string> OpenQuestions { get; } = new[]
    {
        "风荷载标准值随几何变化：样本1 1.551，样本2 1.526（仅改跨数/跨间距/跨度）→ 荷载按面积/分格分摊，精确规则待 V35 运行期逐点验证（已确证：同分格宽时 M ∝ q·L²，比值误差 <1%）。",
        "支座弯矩/跨中弯矩之比不是常数：样本1 67.841/62.316=1.0888，样本2 131.832/98.123=1.3436 → 与跨数相关（已按 N=5/15 双样本线性插值，见 RatioXxx）。",
        "第1跨与第3跨弯矩之比不是常数：样本1 0.6735，样本2 0.6330 → 同上。",
        "6063-T5 与 6063A-T5 的许用值是否相同（本样本 AT5 = 135/75/148.5，T5 那轮未抓到）。",
        "A002.CAC 材料性能表（f=A002(19)/fv=A002(20)）在 EXE 内，安装目录无此文件 → 全材质许用值表待续挖。",
    };

    // ============================================================
    // 非基线几何的比例模型（V31 双样本实测比例，按总跨数 N 线性插值）
    //   样本1 N=15 / A=660 / L=3100 / wk=1.551
    //   样本2 N=5  / A=1000 / L=4000 / wk=1.526
    // 相对值以「第1跨」为 1.0；绝对值需型材截面（参数库「立柱」表）与 q 假设，另行估算。
    // ============================================================

    // 端点常数直接写成实测比值（不四舍五入），保证 N=5/N=15 端点零误差
    /// <summary>支座抗弯 / 第1跨抗弯（实测：N=5→131.832/98.123，N=15→67.841/62.316）。</summary>
    public static double SupportBendingRatio(int totalSpans)
        => Lerp(5, 131.832 / 98.123, 15, 67.841 / 62.316, totalSpans);

    /// <summary>第3跨抗弯 / 第1跨抗弯（实测：N=5→62.121/98.123，N=15→41.964/62.316）。</summary>
    public static double Span3BendingRatio(int totalSpans)
        => Lerp(5, 62.121 / 98.123, 15, 41.964 / 62.316, totalSpans);

    /// <summary>支座抗剪 / 第1跨抗剪（实测：N=5→11.694/7.309，N=15→8.748/5.858）。</summary>
    public static double SupportShearRatio(int totalSpans)
        => Lerp(5, 11.694 / 7.309, 15, 8.748 / 5.858, totalSpans);

    /// <summary>第3跨抗剪 / 第1跨抗剪（实测：N=5→1.37/7.309，N=15→0.982/5.858）。</summary>
    public static double Span3ShearRatio(int totalSpans)
        => Lerp(5, 1.37 / 7.309, 15, 0.982 / 5.858, totalSpans);

    /// <summary>第2跨后挠度 / 第1跨挠度（实测：N=5→11.4645/29.697，N=15→3.8185/9.587）。</summary>
    public static double SecondSpanDeflectionRatio(int totalSpans)
        => Lerp(5, 11.4645 / 29.697, 15, 3.8185 / 9.587, totalSpans);

    private static double Lerp(double x0, double y0, double x1, double y1, double x)
    {
        if (x <= x0) return y0;
        if (x >= x1) return y1;
        return y0 + (y1 - y0) * (x - x0) / (x1 - x0);
    }

    /// <summary>
    /// 线荷载 q（N/mm）= 风荷载标准值(KPa) × 分格宽度(mm) / 1000。
    /// 1 KPa = 1 KN/m² = 1 N/mm² → 乘以宽度(mm) 得 N/mm。
    /// </summary>
    public static double LineLoad(double windKpa, double bayWidthMm)
        => windKpa * bayWidthMm / 1000.0;

    /// <summary>
    /// 非基线几何验算估算。已确证的部分实算（挠度限值三段、σ_eq 公式、许用值）；
    /// 绝对值按「简支跨 + V31 双样本比例」估算并在 <paramref name="note"/> 中如实标注。
    /// </summary>
    /// <param name="windKpa">风荷载标准值（KPa）。</param>
    /// <param name="totalSpans">总跨数 N。</param>
    /// <param name="supportMm">跨长 A（支座段 Li）。</param>
    /// <param name="spanMm">跨长 L（计算跨度 i）。</param>
    /// <param name="bayWidthMm">立柱分格宽度（mm，来自基本参数屏龙骨间距）。</param>
    /// <param name="section">选中型材截面（参数库「立柱」表），可为 null（此时只给比例）。</param>
    /// <param name="elasticModulusMPa">弹性模量 E（MPa，界面输入框，默认 70000）。</param>
    public static MultiSpanEstimate Estimate(int totalSpans, double supportMm, double spanMm,
                                             double windKpa, double bayWidthMm,
                                             GlassManagement.Calc.Data.ParamLibrary.ColumnProfile? section,
                                             double elasticModulusMPa = 70000.0)
    {
        double calcSpan = spanMm + supportMm;
        double q = LineLoad(windKpa, bayWidthMm);
        var allow = new MultiSpanStaticEngine();   // 默认许用值 135/75/148.5（6063-T5 系）
        var e = new MultiSpanEstimate
        {
            WindKpa = windKpa,
            TotalSpans = totalSpans,
            CalcSpanMm = calcSpan,
            LineLoadNPerMm = q,
            DeflectionLimitMm = DeflectionLimit(spanMm, supportMm),
            AllowBendingMPa = allow.AllowBending,
            AllowShearMPa = allow.AllowShear,
            AllowEquivalentMPa = allow.AllowEquivalent,
        };
        // 挠度：df = 5/384 · q · calcSpan⁴ / (E·I)（简支跨估算；限值已确证）
        // 挠度：df = 5/384 · q · L⁴ / (E·I)
        // ★V35/V31 实测校正：多跨静定梁各段按「跨长 L」计算内力与挠度，
        //   不是按 calcSpan=L+A（后者只用于挠度限值）。用 2026-10-24 V31 实机样本校正。
        if (section is not null)
        {
            // 参数库「立柱」表单位：I [cm⁴] → mm⁴ 乘 10⁴；W [cm³] → mm³ 乘 10³
            double i = section.Ix * 1e4;
            double span = spanMm;
            e.DeflectionSpan1Mm = MeasuredFitDeflection * SimplySupportedAlpha * q * Math.Pow(span, 4)
                                  / (elasticModulusMPa * i);
            e.DeflectionSpan2AfterMm = e.DeflectionSpan1Mm * SecondSpanDeflectionRatio(totalSpans);
            double w = section.Wx1 * 1e3;
            double m = MeasuredFitMoment * q * span * span / 8.0;    // 跨中弯矩（含悬挑放大系数）
            e.BendingSpan1MPa = m / w;
            e.BendingSpan3MPa = e.BendingSpan1MPa * Span3BendingRatio(totalSpans);
            e.BendingSupportMPa = e.BendingSpan1MPa * SupportBendingRatio(totalSpans);
            double v = MeasuredFitShear * q * span / 2.0;             // 支座剪力（含悬挑放大系数）
            double s = section.StaticMoment * 1e3;         // 面积矩 cm³ → mm³
            double t = section.ShearWebWidth;               // 受剪腹板总宽度 mm
            double shear1 = v * s / (i * t);
            e.ShearSpan1MPa = shear1;
            e.ShearSpan3MPa = shear1 * Span3ShearRatio(totalSpans);
            e.ShearSupportMPa = shear1 * SupportShearRatio(totalSpans);
            e.EquivalentSpan3MPa = EquivalentStress(e.BendingSpan3MPa, e.ShearSpan3MPa);
            e.EquivalentSupportMPa = EquivalentStress(e.BendingSupportMPa, e.ShearSupportMPa);
            e.SectionSummary = $"{section.Code}（Ix={section.Ix}cm⁴ Wx1={section.Wx1}cm³ S={section.StaticMoment}cm³ t={section.ShearWebWidth}mm）";
        }
        e.Note = section is null
            ? "未选型材（请在立柱计算屏从参数库「立柱」95 行中选一行）：仅输出挠度限值与相对比例"
            : "挠度/强度绝对值 = 跨长 L 简支公式 × 实测拟合系数（A/L=0.25 单点，非程序公式）+ N 插值比例；5/384、挠度限值三段、σ_eq、许用值为 V35 程序确证值";
        return e;
    }

    /// <summary>非基线几何验算估算结果（可变 DTO，便于分步填充）。</summary>
    public sealed class MultiSpanEstimate
    {
        public double WindKpa { get; set; }
        public int TotalSpans { get; set; }
        public double CalcSpanMm { get; set; }
        public double LineLoadNPerMm { get; set; }
        public double DeflectionLimitMm { get; set; }
        public double DeflectionSpan1Mm { get; set; }
        public double DeflectionSpan2AfterMm { get; set; }
        public double BendingSpan1MPa { get; set; }
        public double BendingSpan3MPa { get; set; }
        public double BendingSupportMPa { get; set; }
        public double ShearSpan1MPa { get; set; }
        public double ShearSpan3MPa { get; set; }
        public double ShearSupportMPa { get; set; }
        public double EquivalentSpan3MPa { get; set; }
        public double EquivalentSupportMPa { get; set; }
        public double AllowBendingMPa { get; set; }
        public double AllowShearMPa { get; set; }
        public double AllowEquivalentMPa { get; set; }
        public string SectionSummary { get; set; } = "";
        public string Note { get; set; } = "";
    }

    /// <summary>
    /// 预演输出的固定代表位置。
    /// 已确证：输出结构与总跨数无关（N=15 与 N=5 完全同构），
    /// 固定输出「第1跨」「第3跨」「第2跨立柱支座处」三组 + 两个挠度位置。
    /// </summary>
    public static IReadOnlyList<string> OutputPositions { get; } = new[]
    {
        "第1跨", "第3跨", "第2跨立柱支座处", "第2跨后",
    };
}