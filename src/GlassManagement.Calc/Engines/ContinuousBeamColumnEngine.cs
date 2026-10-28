using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 连续梁（四点三跨）—— 公式全部来自 V31 脱壳镜像 <c>sub_D42762</c>
/// （0xD42762–0xD4DB91，46,127B）的运行期实值取证（Frida 挂 <c>__vbaVar*</c> 读实参 → 出参）。
///
/// <para>实机样本（L=3000、wk=1.285、bay=1100、6063A-T5、60/80系列）：</para>
/// <code>
/// 0xD475B6  Pow(2, 3000)      → 9 000 000      L²
/// 0xD475C3  Mul(9e6, 2.121)   → 19 089 000     qd·L²
/// 0xD475D7  Mul(0.1, ...)     → 1 908 900      ★ ×0.1  → M = 0.1·qd·L²
/// 0xD4B8B9  Mul(1100, 0.001285) → 1.4135       qk_raw = bay × wk(N/mm²)
/// 0xD4B8D8  Mul(3000, 1.4135) → 4240.5         qk_raw·L
/// 0xD4B8EC  Mul(0.6, ...)     → 2544.3         ★ ×0.6  → V0 = 0.6·qk_raw·L
/// 0xD4B9D1  Mul(2544.3, 1.5)  → 3816.45        ★ ×1.5  → V
/// 0xD4C777  Mul(1.414, 0.00677) → 0.00957278   ★ 挠度系数 0.00677（用取整后的 qk）
/// </code>
/// </summary>
public static class ContinuousBeamColumnEngine
{
    /// <summary>挠度系数（V31 代码里的字面量，三跨连续梁边跨跨中）。</summary>
    public const double DeflectionCoefficient = 0.00677;

    /// <summary>弯矩系数 0.1（V31 <c>Mul(0.1, qd·L²)</c>）。</summary>
    public const double MomentCoefficient = 0.1;

    /// <summary>剪力系数 0.6（V31 <c>Mul(0.6, qk_raw·L)</c>，随后再 ×1.5）。</summary>
    public const double ShearCoefficient = 0.6;

    /// <summary>设计值系数（标准值 → 设计值）。</summary>
    public const double PartialFactor = 1.5;

    public sealed class Result
    {
        public double Moment { get; init; }         // 0.1·qd·L²（round3 后）
        public double Shear { get; init; }          // 0.6·qk_raw·L·1.5（不取整）
        public double Deflection { get; init; }     // qk·0.00677·L⁴/(E·I)
        public double Limit { get; init; }          // min(L/180, 20)
        public double RequiredW { get; init; }      // M/[σ]
        public double RequiredI { get; init; }      // qk·0.00677·L⁴/(E·round3(lim))
    }

    /// <param name="qkRaw">未取整的标准值线荷载 N/mm（= bay × wk(N/mm²)）—— 剪力用它</param>
    /// <param name="qk">取整后的 qk（round3）—— 挠度用它</param>
    /// <param name="qd">取整后的设计值（round3(qk×1.5)）—— 弯矩用它</param>
    public static Result Compute(double lMm, double qkRaw, double qk, double qd,
                                 double eMpa, double iMm4, double sigmaAllow)
    {
        double m = WindLoadEngine.VbaRound(MomentCoefficient * qd * lMm * lMm, 3);
        double lim = Math.Min(lMm / DoubleSpanColumnEngine.LimitDivisor, DoubleSpanColumnEngine.LimitCapMm);
        double limR = WindLoadEngine.VbaRound(lim, 3);

        double coeff = qk * DeflectionCoefficient * Math.Pow(lMm, 4);
        double df = coeff / eMpa / iMm4;
        double iReq = coeff / eMpa / limR;

        double v = ShearCoefficient * qkRaw * lMm * PartialFactor;

        return new Result
        {
            Moment = m,
            Shear = v,
            Deflection = df,
            Limit = lim,
            RequiredW = m / sigmaAllow,
            RequiredI = iReq,
        };
    }
}
