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

    // ============================================================
    // 多跨静定梁**解析公式**（V31 逐指令还原；recon/verify_multispan.py 44 项 0 差异）
    // 取代此前的 3 个「实测拟合系数」（1.0743 / 1.1463 / 1.0364）与 5 个「双样本比例」。
    // 几何：A = 跨长L_i（支座段），L = 跨长i（计算跨）。
    // ============================================================

    /// <summary>有效跨 x = (L²−A²)/L（V31 0xD5E 段逐指令还原）。</summary>
    public static double EffectiveSpan(double aMm, double lMm)
        => Math.Abs(lMm) > 1e-9 ? (lMm * lMm - aMm * aMm) / lMm : 0.0;

    /// <summary>
    /// 线荷载：标准值 qk = round3(round3(wk,3)/1000·B)；设计值 qd = round3(qk×1.5)。
    /// （V31 先 round3 风荷载，再 round3 线荷载，再 ×1.5 再 round3。）
    /// </summary>
    public static (double Qk, double Qd) LineLoads(double windKpa, double bayWidthMm)
    {
        double wk = WindLoadEngine.VbaRound(windKpa, 3);
        double qk = WindLoadEngine.VbaRound(wk / 1000.0 * bayWidthMm, 3);
        double qd = WindLoadEngine.VbaRound(qk * 1.5, 3);
        return (qk, qd);
    }

    /// <summary>多跨静定梁内力解（q 为设计线荷载 qd）。S3=V1−D1 为第3跨剪力源，D1=qd·A·t。</summary>
    public readonly record struct Forces(
        double Span1Moment, double Span1Shear,
        double SupportMoment, double SupportShear,
        double Span3Moment, double Span3Shear,
        double S3, double D1);

    /// <summary>
    /// 多跨静定梁内力（V31 逐指令还原）：
    /// x=(L²−A²)/L；M1=qd·x²/8；V1=qd·x/2；M2=qd·A(A+x)/2；
    /// t=(1−(A/L)²)/2；D=qd·A·t；S3=V1−D；M3=M1−S3·A·t；V3=round0(S3·A/L)；V2=V1+qd(A/2)(2+A/L)。
    /// </summary>
    public static Forces InternalForces(double qd, double aMm, double lMm)
    {
        double x = EffectiveSpan(aMm, lMm);
        double v1 = qd * x / 2.0;
        double m1 = qd * x * x / 8.0;
        double m2 = qd * aMm * (aMm + x) / 2.0;
        double r = Math.Abs(lMm) > 1e-9 ? aMm / lMm : 0.0;
        double t3 = (1.0 - r * r) / 2.0;
        double d1 = qd * aMm * t3;
        double s3 = v1 - d1;
        double m3 = m1 - s3 * aMm * t3;
        double v3 = WindLoadEngine.VbaRound(s3 * aMm / (Math.Abs(lMm) > 1e-9 ? lMm : 1.0), 0);
        double v2 = v1 + qd * (aMm / 2.0) * (2.0 + r);
        return new Forces(m1, v1, m2, v2, m3, v3, s3, d1);
    }

    /// <summary>
    /// 两处挠度（V31 0xD66109–0xD662D2 逐指令还原）：
    /// r=A/L；n2=A²·V1·L/3；c1=1−4r²−3r³；b1=((1+r)n2−c1·A·qk·L³/24)/(EI)；
    /// df2c=5qk·L⁴/(384EI)·(1−2.4r²)；df1=round3(b1)/2+round3(df2c)；
    /// S4=V1−D(1−r)；n2b=A²·S4·L/3；b2=((1+r)n2b−c1·A·qk·L³/24)/(EI)；x2=S3·A·L²/16；
    /// df2=round3(b2)/2+round3(df2c−x2/EI)。
    /// </summary>
    public static (double Df1, double Df2) Deflections(
        double qk, double qd, double aMm, double lMm, double eMPa, double iMm4)
    {
        double ei = eMPa * iMm4;
        if (Math.Abs(ei) < 1e-12) return (0.0, 0.0);

        var f = InternalForces(qd, aMm, lMm);
        double r = Math.Abs(lMm) > 1e-9 ? aMm / lMm : 0.0;
        double l3 = Math.Pow(lMm, 3);
        double l4 = Math.Pow(lMm, 4);
        double c1 = 1.0 - 4.0 * r * r - 3.0 * r * r * r;

        double n2 = aMm * aMm * f.Span1Shear * lMm / 3.0;
        double b1 = ((1.0 + r) * n2 - c1 * aMm * qk * l3 / 24.0) / ei;
        double df2c = 5.0 * qk * l4 / (384.0 * ei) * (1.0 - 2.4 * r * r);
        double df1 = WindLoadEngine.VbaRound(b1, 3) / 2.0 + WindLoadEngine.VbaRound(df2c, 3);

        double s4 = f.Span1Shear - f.D1 * (1.0 - r);
        double n2b = aMm * aMm * s4 * lMm / 3.0;
        double b2 = ((1.0 + r) * n2b - c1 * aMm * qk * l3 / 24.0) / ei;
        double x2 = f.S3 * aMm * lMm * lMm / 16.0;
        double df2 = WindLoadEngine.VbaRound(b2, 3) / 2.0
                     + WindLoadEngine.VbaRound(df2c - x2 / ei, 3);

        return (df1, df2);
    }

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
        "支座弯矩/跨中弯矩之比不是常数：样本1 67.841/62.316=1.0888，样本2 131.832/98.123=1.3436 —— **已解释**：由解析式 M2=qd·A(A+x)/2 与 M1=qd·x²/8 直接决定，非插值。",
        "第1跨与第3跨弯矩之比不是常数：样本1 0.6735，样本2 0.6330 —— **已解释**：由 M3=M1−S3·A·t 决定。",
        "6063-T5 与 6063A-T5 的许用值是否相同（本样本 AT5 = 135/75/148.5，T5 那轮未抓到）。",
        "A002.CAC 材料性能表（f=A002(19)/fv=A002(20)）在 EXE 内，安装目录无此文件 → 全材质许用值表待续挖。",
    };

    // ============================================================
    // ★ 2026-10-28 P16：原「双样本比例模型」（SupportBendingRatio / Span3BendingRatio /
    //   SupportShearRatio / Span3ShearRatio / SecondSpanDeflectionRatio + Lerp）已**全部删除**。
    //   原因：这些是「V31 输出比值反推」，不是程序公式。
    //   现已用 V31 逐指令还原的解析公式（EffectiveSpan / InternalForces / Deflections）
    //   直接实算，recon/verify_multispan.py 对 4 组样本 44 项 0 差异。
    // ============================================================

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
        var (qk, qd) = LineLoads(windKpa, bayWidthMm);
        var allow = new MultiSpanStaticEngine();   // 默认许用值 135/75/148.5（6063-T5 系）
        var e = new MultiSpanEstimate
        {
            WindKpa = windKpa,
            TotalSpans = totalSpans,
            CalcSpanMm = calcSpan,
            LineLoadNPerMm = qd,
            DeflectionLimitMm = DeflectionLimit(spanMm, supportMm),
            AllowBendingMPa = allow.AllowBending,
            AllowShearMPa = allow.AllowShear,
            AllowEquivalentMPa = allow.AllowEquivalent,
        };
        // 内力：x=(L²−A²)/L；M1=qd·x²/8；V1=qd·x/2；M2=qd·A(A+x)/2；
        //       t=(1−(A/L)²)/2；D1=qd·A·t；S3=V1−D1；M3=M1−S3·A·t；V3=round0(S3·A/L)；V2=V1+qd(A/2)(2+A/L)
        // 挠度：见 Deflections()（V31 0xD66109–0xD662D2 逐指令还原）。
        var f = InternalForces(qd, supportMm, spanMm);
        if (section is not null)
        {
            // 参数库「立柱」表单位：I [cm⁴] → mm⁴ 乘 10⁴；W [cm³] → mm³ 乘 10³；S [cm³] → mm³ 乘 10³
            double i = section.Ix * 1e4;
            double w = section.Wx1 * 1e3;
            double s = section.StaticMoment * 1e3;
            double tw = section.ShearWebWidth;

            // 强度（σ=M/W，τ=V·S/(I·t)）：与 V31 一致先 round3，再进 σ_eq
            double b1 = WindLoadEngine.VbaRound(f.Span1Moment / w, 3);
            double b2 = WindLoadEngine.VbaRound(f.SupportMoment / w, 3);
            double b3 = WindLoadEngine.VbaRound(f.Span3Moment / w, 3);
            double v1 = WindLoadEngine.VbaRound(s * f.Span1Shear / (i * tw), 3);
            double v2 = WindLoadEngine.VbaRound(s * f.SupportShear / (i * tw), 3);
            double v3 = WindLoadEngine.VbaRound(s * f.Span3Shear / (i * tw), 3);
            e.BendingSpan1MPa = b1;
            e.BendingSupportMPa = b2;
            e.BendingSpan3MPa = b3;
            e.ShearSpan1MPa = v1;
            e.ShearSupportMPa = v2;
            e.ShearSpan3MPa = v3;

            // 折算强度 = round3(√(round3(σ)² + round3(τ)²))（V31 实测顺序）
            e.EquivalentSpan3MPa = WindLoadEngine.VbaRound(EquivalentStress(b3, v3), 3);
            e.EquivalentSupportMPa = WindLoadEngine.VbaRound(EquivalentStress(b2, v2), 3);

            // 挠度：用标准值 qk
            var (df1, df2) = Deflections(qk, qd, supportMm, spanMm, elasticModulusMPa, i);
            e.DeflectionSpan1Mm = df1;
            e.DeflectionSpan2AfterMm = df2;

            e.SectionSummary = $"{section.Code}（Ix={section.Ix}cm⁴ Wx1={section.Wx1}cm³ S={section.StaticMoment}cm³ t={section.ShearWebWidth}mm）";
        }
        e.Note = section is null
            ? "未选型材（请在立柱计算屏从参数库「立柱」95 行中选一行）：仅输出挠度限值与计算跨度"
            : "内力/挠度按 V31 逐指令还原的解析公式实算（x=(L²−A²)/L、M1=qd·x²/8、M2=qd·A(A+x)/2、M3=M1−S3·A·t、df1/df2 见 Deflections）；挠度限值三段、σ_eq、许用值为 V35 程序确证值";
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