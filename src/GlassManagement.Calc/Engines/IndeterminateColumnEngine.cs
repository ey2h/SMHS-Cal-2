using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 多跨铰接连续【一次超静定】梁 —— 立柱「多跨铰接连续一次超静定梁」模型。
///
/// <para>
/// **全部公式来自 V31 脱壳镜像 <c>0xD73EEF–0xD8EF31</c>（约 110KB）的指令级 + Frida 运行期实值取证**，
/// 不含任何计算书/手册/规范推测。取证链：
/// <list type="number">
/// <item>挂 <c>__vbaVarMul(0x413714)</c> 统计返回地址 → 热点集中在 <c>0xD7Bxxx–0xD7Cxxx</c>
///       （每组 ×10 = 按跨数循环）→ 反查 <c>55 8B EC</c> 得函数首 <c>0xD73EEF</c>。</item>
/// <item>挂 <c>__vbaVar*</c> 12 个 helper 读「实参 → 出参」还原算式；挂 x87 段的
///       <c>fstp qword ptr [ebp-X]</c> 读中间量。</item>
/// <item>直读 <c>Form+0xF4</c>(数组弯矩 M')、<c>+0xA0</c>(铰接力 P)、<c>+0x110</c>(L)、
///       <c>+0x4C</c>(A₁)、<c>+0x12C</c>(A₂) 五个 Double 数组，确认递归收敛序列。</item>
/// </list>
/// </para>
///
/// <para>
/// **两个悬挑长度**（实机确认，A₁=400/A₂=700 时两个数组分别为 400/700，互不影响）：
/// <c>A₁ = Form+0x4C</c>（多跨页「跨长A₁」输入），<c>A₂ = Form+0x12C</c>（超静定专属的第二个 A 输入，默认 600）。
/// 支座弯矩用 A₁，递归与挠度用 A₂ —— 这是 V31 的实际行为，不是笔误。
/// </para>
///
/// <para>
/// **已逐位验证**（60/80 系列、n=10、第一页默认风荷载）：<br/>
/// A₁=A₂=600 且 L=2700/2750/2800 → σ/τ/σ_eq/df/lim 全对（15/15）；<br/>
/// A₁=800/A₂=600/L=2700 → σ/τ/σ_eq 全对（3/3）。<br/>
/// 复现脚本：<c>local-data/recon/verify_cj.py</c>。
/// </para>
///
/// <para>
/// ⚠ **未闭合项（如实标注，未做任何凑数）**：
/// <list type="bullet">
/// <item>A₁ ≠ A₂ 时的 <b>df</b>：A₁=800/A₂=600 实测 1.6645，按本文公式得 3.06，差在挠度第二项的 A 归属。</item>
/// <item><b>跨中控制分支</b>（max|M'| &gt; M，如 A=600/L=3000）：σ=|M'|/W 已对上，
///       但 τ 的 V 与 df2 的另一路径未解（实测 τ=8.363 对应 V≈3267.3，而 P+qd·A₁=3721）。</item>
/// </list>
/// </para>
/// </summary>
public static class IndeterminateColumnEngine
{
    /// <summary>挠度限值分母（同单跨/静定：180）。</summary>
    public const double LimitDivisor = 180.0;

    /// <summary>挠度限值绝对上限 20mm（V31 常量 <c>dbl_4019E0</c>）。</summary>
    public const double LimitCapMm = 20.0;

    /// <summary>P 递归里 <c>(L³+A₂³)·6·qd/24</c> 的常数 6（<c>0xD7C369</c> 处 Variant 常量 6）。</summary>
    private const double RecursionFactor = 6.0;

    /// <summary>V31 <c>rtcRound(v, 0)</c> 的等价：逐次 ×1 → 半偶 → ÷1。</summary>
    private static double Round0(double v) => WindLoadEngine.VbaRound(v, 0);

    public sealed class Result
    {
        public double Moment { get; init; }        // 支座弯矩 M = qd·A₁²/2 + P·A₁（未取整）
        public double ArrayMoment { get; init; }   // 数组弯矩 M'（负值，收敛后）
        public double MaxArrayMoment { get; init; }// 迭代过程中 |M'| 的最大值
        public double HingeForce { get; init; }    // 铰接力 P（round0 后的整数）
        public double Shear { get; init; }         // V = P + qd·A₁（支座控制分支）
        public bool SpanControl { get; init; }     // true = max|M'| > M（跨中控制分支）
        public double DesignMoment { get; init; }  // 参与 σ 的弯矩
        public double Sigma { get; init; }         // σ = M_design / W
        public double Tau { get; init; }           // τ = Sx·V/(I·t)
        public double SigmaEq { get; init; }       // σ_eq = √(σ² + 3τ²)
        public double Deflection { get; init; }    // df（已按 V31 分项各取 3 位后相加）
        public double Limit { get; init; }         // min((A₁+L)/180, 20)
        public int Iterations { get; init; }
    }

    /// <summary>
    /// 迭代求解。V31 在打印循环里逐跨迭代（循环体见 <c>0xD7C165–0xD7C9A0</c>），
    /// 每步用上一跨的 P 算支座弯矩、再反算数组弯矩 M' 与新的 P；实测 3 步即收敛。
    /// </summary>
    public static Result Compute(double a1, double a2, double span,
                                 double qk, double qd,
                                 double eMpa, double iMm4,
                                 double wMm3, double sxMm3, double twMm,
                                 int spans = 10)
    {
        double p = 0.0;
        double m = qd * a1 * a1 / 2.0;
        double mp = 0.0;
        double mpMax = 0.0;
        int used = 0;

        for (int k = 0; k < Math.Max(1, spans); k++)
        {
            // ① 支座弯矩：悬挑自重项 + 上一跨铰接力在本层悬挑处产生的弯矩
            m = qd * a1 * a1 / 2.0 + p * a1;

            // ② (L³ + A₂³)·6·qd/24（0xD7C165 算 L³、0xD7C192 算 A₂³ 后相加；
            //    0xD7C369 乘 6·qd、0xD7C391 除 24）
            double t = (Math.Pow(span, 3) + Math.Pow(a2, 3)) * RecursionFactor * qd / 24.0;

            // ③ 数组弯矩 M' = −(T − M·A₂/6) / 2 / (A₂ + L)
            //    （0xD7C3A5 = Add(T, −M·A₂/6)；0xD7C3DA = ÷(A₂+L)，÷2 在 0xD7C3C6）
            mp = -(t - m * a2 / 6.0) / 2.0 / (a2 + span);

            if (Math.Abs(mp) > mpMax) mpMax = Math.Abs(mp);

            // ④ 铰接力 P = round0(qd·L/2 + M'/L) —— ★ 0 位整数取整（0xD7C95D push 0 + rtcRound）
            p = Round0(qd * span / 2.0 + mp / span);
            used = k + 1;
        }

        bool spanControl = mpMax > m;
        double designM = spanControl ? mpMax : m;

        // ⑤ 剪力 V = P + qd·A₁（0xD7FAD4 之前由 qd·A₁ 与 P 相加）
        double v = p + qd * a1;

        double sigma = designM / wMm3;
        double tau = sxMm3 * v / iMm4 / twMm;

        // ⑥ 折算应力 √(σ² + 3τ²)（0xD802F4/0xD80316 Pow(2)、0xD80344 Pow(0.5)；
        //    系数 3 由实测反证：L=2800 时 √(σ²+τ²)=83.055 但 V31 打印 84.059，加 3τ² 才对）
        double seq = Math.Sqrt(sigma * sigma + 3.0 * tau * tau);

        // ⑦ 挠度：两个分项各自 round3 后相减（0xD877CE / 0xD873F8 两处 rtcRound(3)）
        double ei = eMpa * iMm4;
        double num1 = 5.0 * qk * Math.Pow(span, 4) / 384.0 + mp * span * span / 16.0;
        double num2 = p * Math.Pow(a2, 3) / 3.0
                      - Math.Pow(a2, 4) * qk / 12.0
                      - m * a2 / 3.0
                      + mp * a2 / 6.0;   // mp 本身为负，代码在 0xD87390 处直接 Add
        double df1 = WindLoadEngine.VbaRound(num1 / ei, 3);
        double df2 = WindLoadEngine.VbaRound(num2 / ei, 3);
        double df = df1 - df2 / 2.0;

        // ⑧ 挠度限值 min((A₁+L)/180, 20)
        double lim = Math.Min((a1 + span) / LimitDivisor, LimitCapMm);

        return new Result
        {
            Moment = m,
            ArrayMoment = mp,
            MaxArrayMoment = mpMax,
            HingeForce = p,
            Shear = v,
            SpanControl = spanControl,
            DesignMoment = designM,
            Sigma = sigma,
            Tau = tau,
            SigmaEq = seq,
            Deflection = df,
            Limit = lim,
            Iterations = used,
        };
    }

    /// <summary>该配置是否落在已逐位验证过的分支内（支座控制 + A₁==A₂）。</summary>
    public static bool IsFullyVerified(Result r, double a1, double a2)
        => !r.SpanControl && Math.Abs(a1 - a2) < 1e-9;
}
