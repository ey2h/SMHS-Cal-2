using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 双跨梁（立柱「双跨梁」力学模型）—— 公式全部来自 V31 脱壳镜像 <c>sub_D35D8C</c>
/// （0xD35D8C–0xD42762，51,670B）的逐指令 + Frida 运行期实值取证，不含任何计算书/手册数据。
///
/// <para>
/// **取证方式**（2026-10-25）：Frida 挂 <c>rtcRound(0x41371A)</c> 取返回地址锁定函数边界；
/// 再挂 <c>__vbaVar*</c> 系列（Div 0x413708 / Mul 0x413714 / Pow 0x41370E / Add 0x413552 /
/// Sub 0x41377A / Atn 0x4137FE / Cos 0x4137F2 / Sqr 0x4137F8）读每次运算的实参 → out，
/// 完整还原算式链。两组实机参数（L2=1500 等跨、L2=1200 不等跨）逐位复现 13/13。
/// </para>
///
/// <para>
/// **两跨的 ±1 规则**（用户实机观察 + 两组数据印证）：等跨（L2 == L1−L2）时 V31 把
/// a 自动改成 a−1、b 改成 b+1（L1=3000/L2=1500 → 1499 / 1501），否则原样（1200 / 1800）。
/// </para>
/// </summary>
public static class DoubleSpanColumnEngine
{
    /// <summary>挠度限值分母：V31 用 180（Lc/180），实测 1501/180=8.3389→8.339、1800/180=10。</summary>
    public const double LimitDivisor = 180.0;

    /// <summary>
    /// 挠度限值绝对上限 20mm（V31 常量 <c>dbl_4019E0 = 20</c>，在双跨函数内被引用
    /// <c>0xD3CBB3</c> / <c>0xD3DE6F</c>）。实测：Lc=2500 → 13.889（Lc/180）、
    /// Lc=4000 → **20**（4000/180=22.2 被压到 20）。
    /// </summary>
    public const double LimitCapMm = 20.0;

    /// <summary>V31 里两个不同的 π 常量：theta 用 3.14，角度转弧度用 3.1415926。</summary>
    private const double PiShort = 3.14;
    private const double PiLong = 3.1415926;

    public sealed class Result
    {
        public double A { get; init; }              // 左跨（等跨时已 −1）
        public double B { get; init; }              // 右跨
        public double Lc { get; init; }             // max(A,B)，挠度/限值用它
        public double Moment { get; init; }         // M = qd(a³+b³)/(8L1)，已 round3
        public double Shear { get; init; }          // V = max(a·qd/2 + M/a, b·qd/2 + M/b)，未取整
        public double Deflection { get; init; }     // df = qk·kf·Lc⁴/(24EI)
        public double Limit { get; init; }          // Lc/180（未取整；显示时 round3）
        public double RequiredW { get; init; }      // 抗弯矩预选 = M/[σ]
        public double RequiredI { get; init; }      // 惯性矩预选 = qk·kf·Lc⁴/(24E·round3(Limit))
        public double K1 { get; init; }             // 4M/(qd·Lc²) 的 round3
        public double K4 { get; init; }
        public double K6 { get; init; }
        public double Theta { get; init; }
        public double K11 { get; init; }
        public double Kf { get; init; }             // round4 的挠度系数（进 df 用这个）
    }

    /// <summary>两跨长度：等跨时 a−1 / b+1（V31 实测行为）。</summary>
    public static (double a, double b) Spans(double l1, double l2)
    {
        double a = l2;
        double b = l1 - a;
        if (Math.Abs(a - b) < 1e-9)
        {
            a -= 1.0;
            b += 1.0;
        }
        return (a, b);
    }

    /// <param name="l1">总跨（mm，界面第一个跨度框）</param>
    /// <param name="l2">短跨/支座位置（mm，界面第二个跨度框）</param>
    /// <param name="qk">标准值线荷载 N/mm</param>
    /// <param name="qd">设计值线荷载 N/mm（= round3(qk×1.5)）</param>
    public static Result Compute(double l1, double l2, double qk, double qd,
                                 double eMpa, double iMm4, double sigmaAllow)
    {
        var (a, b) = Spans(l1, l2);
        double lc = Math.Max(a, b);

        // ① 支座负弯矩 M = qd·(a³+b³)/(8·L1)，随后 round3（后续所有量都吃这个取整值）
        double mRaw = qd * (Math.Pow(a, 3) + Math.Pow(b, 3)) / 8.0 / l1;
        double m = WindLoadEngine.VbaRound(mRaw, 3);

        // ② 挠度限值 = min(Lc/180, 20)（未取整参与计算，显示时才 round3）
        double lim = Math.Min(lc / LimitDivisor, LimitCapMm);

        // ③ 挠度系数链 —— 每一步的取整位置都按 V31 实机定稿点还原
        double k1 = WindLoadEngine.VbaRound(4.0 * m / (qd * lc * lc), 3);        // slot[22] 0.499
        double k2 = k1 + 2.0;                                                    // 2.499（不取整）
        double k3 = k2 / 4.0;                                                    // 0.62475（不取整）
        double k4 = WindLoadEngine.VbaRound(Math.Pow(k3 * k3 - k1 / 2.0, 1.5), 3); // 0.053

        double e1 = 1.0 - 2.0 * k1;                                              // 等跨 0.002 / 不等跨 0.222
        double a1 = Math.Pow(k2, 3) - 12.0 * k1 * k2;
        double a2 = a1 - e1 * 8.0;
        double k5 = a2 / 64.0;
        double k6Raw = k5 / k4;
        double k6 = WindLoadEngine.VbaRound(k6Raw, 3);                           // 0.185
        double k7 = 1.0 - k6 * k6;
        double k7s = Math.Sqrt(k7);
        double k8 = -k6 / k7s;

        // theta = (180/3.14/3) × (2·Atn(1) + Atn(k8))，不取整参与后续
        double theta = (180.0 / PiShort / 3.0) * (2.0 * Math.Atan(1.0) + Math.Atan(k8));
        double thetaR = WindLoadEngine.VbaRound(theta, 3);                       // 显示/角度都用取整值
        double ang = PiLong * (240.0 + thetaR) / 180.0;
        double c1 = Math.Cos(ang);
        double c2 = 2.0 * Math.Pow(k4, 1.0 / 3.0);
        double k11Raw = k3 + c1 * c2;                                            // 0.5783633
        double k11 = WindLoadEngine.VbaRound(k11Raw, 3);                         // 0.578

        // ④ 挠度系数 kf = k12·k11，round4
        double p2 = k11 * k11;
        double p3 = k11 * p2;
        double u1 = 2.0 * p2;
        double u2 = k1 * p2;
        double t1 = k11 * WindLoadEngine.VbaRound(3.0 * k1, 3) + e1;
        double t2 = t1 - u1;
        double t3 = t2 - u2;
        double k12 = p3 + t3;
        double kfRaw = k12 * k11;
        double kf = WindLoadEngine.VbaRound(kfRaw, 4);                           // 0.1303 / 0.1692

        // ⑤ 挠度 / 预选
        double df = qk * kf * Math.Pow(lc, 4) / 24.0 / eMpa / iMm4;
        double limR = WindLoadEngine.VbaRound(lim, 3);
        double iReq = qk * kf * Math.Pow(lc, 4) / 24.0 / eMpa / limR;
        double wReq = m / sigmaAllow;

        // ⑥ 抗剪：两跨分别算 V = a·qd/2 + M/a，取绝对值大者，再向下取 3 位参与 τ
        double v1 = a * qd / 2.0 + m / a;
        double v2 = b * qd / 2.0 + m / b;
        double v = Math.Abs(v1) >= Math.Abs(v2) ? v1 : v2;

        return new Result
        {
            A = a, B = b, Lc = lc, Moment = m, Shear = v,
            Deflection = df, Limit = lim, RequiredW = wReq, RequiredI = iReq,
            K1 = k1, K4 = k4, K6 = k6, Theta = theta, K11 = k11, Kf = kf,
        };
    }
}
