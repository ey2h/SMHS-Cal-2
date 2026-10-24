using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 立柱/横梁 跨度挠度系数引擎。
/// 双跨梁（连续梁）挠度系数 α 取自原程序计算书 表4-4（=《建筑结构静力计算手册》原值）；
/// 简支梁 α = 5/384。
/// 注：α 随 L₁/L₂ 变化，需插值；其余强度/抗剪按 JGJ102 第5/6章。
/// </summary>
public sealed class ColumnSpanEngine
{
    // 双跨梁挠度系数 α，L₁/L₂ 升序（计算书 表4-4）
    private static readonly double[] Lr = { 0.5, 0.6, 0.7, 0.8, 0.9, 1.0 };
    private static readonly double[] Alpha = { 0.73, 0.65, 0.59, 0.53, 0.49, 0.42 };

    /// <summary>双跨梁挠度系数 α（按 L₁/L₂ 线性插值）。</summary>
    public double DoubleSpanAlpha(double l1l2)
    {
        if (l1l2 <= Lr[0]) return Alpha[0];
        if (l1l2 >= Lr[^1]) return Alpha[^1];
        for (int i = 0; i < Lr.Length - 1; i++)
        {
            if (l1l2 >= Lr[i] && l1l2 <= Lr[i + 1])
            {
                double t = (l1l2 - Lr[i]) / (Lr[i + 1] - Lr[i]);
                return Alpha[i] + t * (Alpha[i + 1] - Alpha[i]);
            }
        }
        return Alpha[^1];
    }

    /// <summary>简支梁挠度系数（均布荷载）α = 5/384。</summary>
    public const double SimplySupportedAlpha = 5.0 / 384.0;

    /// <summary>
    /// 双跨梁跨中挠度 df = α·q·L⁴ / (E·I) （mm）。
    /// q[N/mm], L[mm], E[MPa], I[mm⁴]。
    /// </summary>
    public double DoubleSpanDeflection(double alpha, double qNperMm, double lMm, double eMPa, double iMm4)
        => alpha * qNperMm * Math.Pow(lMm, 4) / (eMPa * iMm4);

    /// <summary>简支梁跨中挠度 df = 5·q·L⁴ / (384·E·I)。</summary>
    public double SimplySupportedDeflection(double qNperMm, double lMm, double eMPa, double iMm4)
        => SimplySupportedAlpha * qNperMm * Math.Pow(lMm, 4) / (eMPa * iMm4);

    /// <summary>抗弯强度 σ = M / W （MPa）。M[N·mm], W[mm³]。</summary>
    public double BendingStress(double momentNmm, double sectionModulusMm3)
        => momentNmm / sectionModulusMm3;

    /// <summary>双跨梁（等跨）跨中弯矩 M = q·L²/8（近似，按静力手册双跨梁跨中）。</summary>
    public double DoubleSpanMidMoment(double qNperMm, double lMm)
        => qNperMm * lMm * lMm / 8.0;
}
