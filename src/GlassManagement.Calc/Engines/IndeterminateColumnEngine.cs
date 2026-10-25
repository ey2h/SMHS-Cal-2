using System;
using System.Collections.Generic;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 多跨铰接连续【一次超静定】梁 —— 立柱「多跨铰接连续一次超静定梁」模型。
///
/// <para>
/// **全部公式来自 V31 脱壳镜像 <c>0xD73EEF–0xD8EF31</c>（约 110KB）的指令级 + Frida 运行期实值取证**，
/// 不含任何计算书 / 手册 / 规范推测。取证链：
/// <list type="number">
/// <item>挂 <c>__vbaVarMul(0x413714)</c> 统计返回地址 → 热点集中在 <c>0xD7Bxxx–0xD7Cxxx</c>
///       （每组 ×10 = 按跨数循环）→ 反查 <c>55 8B EC</c> 得函数首 <c>0xD73EEF</c>。</item>
/// <item>挂 <c>__vbaVar*</c> 12 个 helper 读「实参 → 出参」（并记录实参的 <c>ebp</c> 偏移），
///       逐个定位算式所在局部量。</item>
/// <item>直读 <c>Form+0xF4</c>(数组弯矩 M')、<c>+0xA0</c>(铰接力 P)、<c>+0x110</c>(L)、
///       <c>+0x4C</c>(A₁)、<c>+0x12C</c>(A₂)、<c>+0xD8</c>(−M) 六个 Double 数组。</item>
/// </list>
/// </para>
///
/// <para>
/// **两个悬挑长度**（实机确认，A₁=400/A₂=700 时两数组分别读出 400/700，互不影响）：
/// <c>A₁ = Form+0x4C</c>（多跨页「跨长A₁」），<c>A₂ = Form+0x12C</c>（超静定专属的第二个 A 输入，默认 600）。
/// 支座弯矩用 A₁；迭代与挠度的其它项用 A₂ —— 这是 V31 的实际行为。
/// </para>
///
/// <para>
/// **对拍 30/30 逐位一致**（60/80 系列、n=10、第一页默认风荷载）：
/// L=2700/2750/2800（A₁=A₂=600）、A₁=800/A₂=600/L=2700、A₁=700/A₂=800/L=2600、
/// L=3000（跨中控制分支）。复现脚本 <c>local-data/recon/verify_cj.py</c>。
/// </para>
/// </summary>
public static class IndeterminateColumnEngine
{
    /// <summary>挠度限值分母（同单跨/静定：180）。</summary>
    public const double LimitDivisor = 180.0;

    /// <summary>挠度限值绝对上限 20mm（V31 常量 <c>dbl_4019E0</c>）。</summary>
    public const double LimitCapMm = 20.0;

    /// <summary>递归里 <c>(L³+A₂³)·6·qd/24</c> 的常数 6（<c>0xD7C369</c> 处 Variant 常量 6）。</summary>
    private const double RecursionFactor = 6.0;

    /// <summary>一次迭代的三元组（逐跨）。</summary>
    private readonly record struct Step(double Moment, double ArrayMoment, double HingeForce);

    public sealed class Result
    {
        public double Moment { get; init; }         // 收敛支座弯矩 M = qd·A₁²/2 + P·A₁
        public double ArrayMoment { get; init; }    // 收敛数组弯矩 M'（负）
        public double HingeForce { get; init; }     // 收敛铰接力 P（round0 后的整数）
        public double MaxArrayMoment { get; init; } // 迭代中 |M'| 的最大值
        public double MaxMoment { get; init; }      // 迭代中 M 的最大值
        public bool SpanControl { get; init; }      // true = 跨中控制（max|M'| > max M）
        public double DesignMoment { get; init; }   // 参与 σ 的弯矩
        public double CriticalMoment { get; init; } // 控制截面的 M
        public double CriticalArrayMoment { get; init; }
        public double ShearCandidate1 { get; init; }// q1 = qd·A₁/2 + |(−M) − M'|/A₂
        public double ShearCandidate2 { get; init; }// 支座控制 P+qd·A₁ / 跨中控制 |qd·L/2 + M'/L|
        public double Shear { get; init; }          // V = max(q1, q2)
        public double Sigma { get; init; }          // σ = M_design / W
        public double Tau { get; init; }            // τ = Sx·V/(I·t)
        public double SigmaEq { get; init; }        // σ_eq = √(σ² + 3τ²)
        public double Deflection { get; init; }     // df（两个分项各 round3 后相减）
        public double DeflectionTerm1 { get; init; }
        public double DeflectionTerm2 { get; init; }
        public double Limit { get; init; }          // min((A₁+L)/180, 20)
        public int Iterations { get; init; }
    }

    /// <summary>
    /// 迭代求解。V31 在打印循环里逐跨迭代（循环体 <c>0xD7C165–0xD7C9A0</c>）：
    /// 用上一跨的 P 算支座弯矩 M，再反算数组弯矩 M' 与新的 P。实测 3 步收敛，
    /// 且 <c>n</c>（总跨数）不影响结果（n=4 与 n=10 输出相同）。
    /// </summary>
    public static Result Compute(double a1, double a2, double span,
                                 double qk, double qd,
                                 double eMpa, double iMm4,
                                 double wMm3, double sxMm3, double twMm,
                                 int spans = 10)
    {
        int n = Math.Max(1, spans);
        var steps = new List<Step>(n);

        double p = 0.0;
        for (int k = 0; k < n; k++)
        {
            // ① 支座弯矩：悬挑自重项 + 上一跨铰接力在本层悬挑处产生的弯矩
            double m = qd * a1 * a1 / 2.0 + p * a1;

            // ② (L³ + A₂³)·6·qd/24
            //    （0xD7C165 算 L³、0xD7C192 算 A₂³ 后 fadd；0xD7C369 乘 6·qd、0xD7C391 除 24）
            double t = (Math.Pow(span, 3) + Math.Pow(a2, 3)) * RecursionFactor * qd / 24.0;

            // ③ 数组弯矩 M' = −(T − M·A₂/6) / 2 / (A₂ + L)
            //    （0xD7C3A5 = Add(T, −M·A₂/6)；0xD7C3C6 = ÷2；0xD7C3DA = ÷(A₂+L)）
            double mp = -(t - m * a2 / 6.0) / 2.0 / (a2 + span);

            // ④ 铰接力 P = rtcRound(qd·L/2 + M'/L, 0) —— ★ 0 位整数取整
            p = WindLoadEngine.VbaRound(qd * span / 2.0 + mp / span, 0);

            steps.Add(new Step(m, mp, p));
        }

        var last = steps[^1];

        // ⑤ 控制截面：max|M'| > max M ⇒ 跨中控制，否则支座控制
        double maxM = double.MinValue, maxAbsMp = 0.0;
        foreach (var s in steps)
        {
            if (s.Moment > maxM) maxM = s.Moment;
            if (Math.Abs(s.ArrayMoment) > maxAbsMp) maxAbsMp = Math.Abs(s.ArrayMoment);
        }
        bool spanControl = maxAbsMp > maxM;

        Step crit = steps[0];
        if (spanControl)
        {
            foreach (var s in steps)
                if (Math.Abs(s.ArrayMoment) >= Math.Abs(crit.ArrayMoment)) crit = s;
        }
        else
        {
            foreach (var s in steps)
                if (s.Moment >= crit.Moment) crit = s;
        }

        double designM = spanControl ? Math.Abs(crit.ArrayMoment) : crit.Moment;

        // ⑥ 剪力：两条支路各有两候选取大
        //    q1 = qd·A₁/2 + |(−M) − M'|/A₂  （0xD7DA21 fld −M → fsub M' → fabs → fdiv A₂）
        double q1 = qd * a1 / 2.0 + Math.Abs(-crit.Moment - crit.ArrayMoment) / a2;
        //    支座控制：P + qd·A₁；跨中控制：|qd·L/2 + M'/L|
        double q2 = spanControl
            ? Math.Abs(qd * span / 2.0 + crit.ArrayMoment / span)
            : crit.HingeForce + qd * a1;
        double v = Math.Max(q1, q2);

        double sigma = designM / wMm3;
        double tau = sxMm3 * v / iMm4 / twMm;

        // ⑦ 折算应力 √(σ² + 3τ²)
        //    （0xD802F4/0xD80316 Pow(2)、0xD80344 Pow(0.5)；实测 L=2800 时
        //      √(σ²+τ²)=83.055 而 V31 打印 84.059，必须加 3τ²）
        double seq = Math.Sqrt(sigma * sigma + 3.0 * tau * tau);

        // ⑧ 挠度：两个分项各自 round3 后相减（0xD877CE / 0xD873F8 两处 rtcRound(3)）
        //    ★ num2 里 A₁ 与 A₂ 是混用的（实机如此，L=2800 时两者相等所以看不出来；
        //      A₁=800/A₂=600 的实参 4.096e11=A₁⁴、1.296e11=A₂⁴、−492.96e6=M·A₂/3 是铁证）
        double ei = eMpa * iMm4;
        double num1 = 5.0 * qk * Math.Pow(span, 4) / 384.0 + last.ArrayMoment * span * span / 16.0;
        double num2 = last.HingeForce * Math.Pow(a1, 3) / 3.0
                      - Math.Pow(a1, 4) * qk / 8.0
                      + Math.Pow(a2, 4) * qk / 24.0
                      - last.Moment * a2 / 3.0
                      + last.ArrayMoment * a2 / 6.0;   // M' 本身为负，代码此处是「加」
        double df1 = WindLoadEngine.VbaRound(num1 / ei, 3);
        double df2 = WindLoadEngine.VbaRound(num2 / ei, 3);
        double df = df1 - df2 / 2.0;

        // ⑨ 挠度限值 min((A₁+L)/180, 20)
        double lim = Math.Min((a1 + span) / LimitDivisor, LimitCapMm);

        return new Result
        {
            Moment = last.Moment,
            ArrayMoment = last.ArrayMoment,
            HingeForce = last.HingeForce,
            MaxArrayMoment = maxAbsMp,
            MaxMoment = maxM,
            SpanControl = spanControl,
            DesignMoment = designM,
            CriticalMoment = crit.Moment,
            CriticalArrayMoment = crit.ArrayMoment,
            ShearCandidate1 = q1,
            ShearCandidate2 = q2,
            Shear = v,
            Sigma = sigma,
            Tau = tau,
            SigmaEq = seq,
            Deflection = df,
            DeflectionTerm1 = df1,
            DeflectionTerm2 = df2,
            Limit = lim,
            Iterations = n,
        };
    }
}
