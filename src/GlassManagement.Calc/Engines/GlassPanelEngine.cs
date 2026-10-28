using System;
using System.Collections.Generic;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 玻璃面板计算引擎（JGJ102-2003 第6.1节，原程序式实现）。
/// 系数表 m/μ 取自原程序计算书打印值（= JGJ102 附录原值），公式分支按 JGJ102 完整变体实现。
/// 注意：不同玻璃类型（单片/夹层/中空）、支承条件（四边/三边/两边/点支承）选用不同公式与系数表，
/// 这部分分支判定与原程序主程序一致性的最终闭合，需经 IDA 提取主程序代码后逐条核对。
/// </summary>
public sealed class GlassPanelEngine
{
    #region 支撑类型枚举
    public enum SupportType
    {
        FourEdge,      // 四边支承（JGJ102 6.1.2-1 / 6.1.3）
        ThreeEdge,     // 三边支承（6.1.2-2）
        TwoEdge,       // 两边支承（6.1.2-3）
        PointSupported // 点支承（6.1.2-4）
    }

    public enum GlassType
    {
        Monolithic,    // 单片玻璃
        Laminated,     // 夹层玻璃（需 η 折减，6.1.4）
        Insulated      // 中空玻璃（需荷载分配 θ，6.1.5）
    }
    #endregion

    #region 原程序打印的精确系数表（四边支承，a/b 降序）
    // ⚠ 来源未取证：以下 4 张表按 JGJ102-2003 表6.1.2-1/-2/-3/-4 规范值录入，
    //   未在 V31 常量池或运行期轨迹中找到对应实证（四边支承主链实际走 GlassEngine 的
    //   m/μ/η 表）。本引擎现仅 Calc.Demo 使用，未接线计算线；系数待 V31 取证后再逐位对拍。
    private static readonly double[] AbRatio_4e = { 1.0, 0.9, 0.8, 0.7, 0.6, 0.5 };
    private static readonly double[] M_4e       = { 0.0468, 0.0451, 0.0440, 0.0400, 0.0352, 0.0283 };
    private static readonly double[] Mu_4e      = { 0.00328, 0.00304, 0.00281, 0.00249, 0.00215, 0.00179 };

    // 三边支承（JGJ102 表6.1.2-2）a/b 降序（b 为长边，a 为短边）—— 标准值，待主程序核对
    private static readonly double[] AbRatio_3e = { 1.0, 0.9, 0.8, 0.7, 0.6, 0.5 };
    private static readonly double[] M_3e       = { 0.0650, 0.0624, 0.0592, 0.0550, 0.0494, 0.0425 };
    private static readonly double[] Mu_3e      = { 0.00555, 0.00514, 0.00465, 0.00409, 0.00347, 0.00281 };

    // 两边支承（JGJ102 表6.1.2-3）
    private static readonly double[] AbRatio_2e = { 1.0, 0.9, 0.8, 0.7, 0.6, 0.5 };
    private static readonly double[] M_2e       = { 0.1000, 0.0934, 0.0856, 0.0762, 0.0651, 0.0517 };
    private static readonly double[] Mu_2e      = { 0.01130, 0.01037, 0.00929, 0.00800, 0.00657, 0.00496 };

    // 点支承（JGJ102 表6.1.2-4）按 c/t 取值，此处 c/t 降序
    private static readonly double[] CtRatio_pt = { 2.0, 3.0, 4.0, 5.0, 6.0, 7.0 };
    private static readonly double[] M_pt       = { 0.159, 0.138, 0.121, 0.107, 0.096, 0.087 };
    #endregion

    /// <summary>按 a/b 在 (key 降序, val) 表上线性插值。</summary>
    private static double Interp(double ab, double[] keys, double[] vals)
    {
        if (ab >= keys[0]) return vals[0];
        if (ab <= keys[^1]) return vals[^1];
        for (int i = 0; i < keys.Length - 1; i++)
        {
            if (ab <= keys[i] && ab >= keys[i + 1])
            {
                double t = (keys[i] - ab) / (keys[i] - keys[i + 1]);
                return vals[i] - t * (vals[i] - vals[i + 1]);
            }
        }
        return vals[^1];
    }

    /// <summary>
    /// 玻璃面板薄膜效应折减系数 η（JGJ102-2003 6.1.2-2 / 计算书 表6-1.2-2）。
    /// θ = q·a⁴ / (E·t⁴)；当 θ≤1 时 η=1.0，当 θ&gt;1 时 η=θ⁻⁰·⁵。
    /// 该系数描述大挠度下薄膜效应对应力的折减，对单片/夹层/中空玻璃均适用，
    /// 且同时作用于应力与挠度两个公式（计算书 表6-2.3 / 表6-3 明确使用同一 η）。
    /// 单位：q[N/mm²=MPa], a,t[mm], E[MPa]，θ 无量纲。
    /// </summary>
    public static double MembraneEta(double qMPa, double a, double eMPa, double t)
    {
        double theta = qMPa * Math.Pow(a, 4) / (eMPa * Math.Pow(t, 4));
        return theta <= 1.0 ? 1.0 : Math.Pow(theta, -0.5);
    }

    /// <summary>玻璃板弯曲刚度 D = E·t³ / [12·(1-ν²)]（挠度公式分母，JGJ102 6.1.3）。</summary>
    public static double BendingStiffnessD(double eMPa, double t, double nu = 0.20)
        => eMPa * Math.Pow(t, 3) / (12.0 * (1.0 - nu * nu));

    /// <summary>
    /// 玻璃面板应力（N/mm² = MPa）。
    /// σ = 6·m·η·q·a² / t²  （JGJ102 6.1.2-1，η=薄膜效应折减系数）。
    /// 单位：q[N/mm²], a,t[mm]。η 默认按 θ 自动计算；可传 etaOverride 强制指定（如验证用）。
    /// 注：夹层/中空玻璃按各片单独验算时，t 取该片厚度、q 取作用该片的风荷载。
    /// </summary>
    public GlassStressResult ComputeStress(
        SupportType support, GlassType glass,
        double a, double b,           // 短边、长边 (mm)
        double t,                     // 受荷玻璃厚度 (mm)
        double wLoadMPa,              // 作用于该片玻璃的面荷载 (MPa = kPa/1000)
        double eGlassMPa,             // 玻璃弹性模量 (MPa)
        double? etaOverride = null)   // 不传则按 θ 自动算 η
    {
        double ab = a / b;
        double m = support switch
        {
            SupportType.FourEdge => Interp(ab, AbRatio_4e, M_4e),
            SupportType.ThreeEdge => Interp(ab, AbRatio_3e, M_3e),
            SupportType.TwoEdge => Interp(ab, AbRatio_2e, M_2e),
            SupportType.PointSupported => throw new InvalidOperationException("点支承请用 ComputeStressPoint"),
            _ => throw new ArgumentOutOfRangeException(nameof(support))
        };
        double eta = etaOverride ?? MembraneEta(wLoadMPa, a, eGlassMPa, t);
        double sigma = 6.0 * m * eta * wLoadMPa * a * a / (t * t);
        return new GlassStressResult(sigma, m, eta, wLoadMPa);
    }

    /// <summary>点支承玻璃应力（JGJ102 6.1.2-4）：σ = 6·m·w·a² / t²，m 按 c/t 查表。</summary>
    public GlassStressResult ComputeStressPoint(
        double a, double b, double t, double c, double wLoadMPa)
    {
        double ct = c / t;
        double m = Interp(ct, CtRatio_pt, M_pt);
        double sigma = 6.0 * m * wLoadMPa * a * a / (t * t);
        return new GlassStressResult(sigma, m, 1.0, wLoadMPa);
    }

    /// <summary>
    /// 玻璃面板挠度（mm）。
    /// df = η·μ·q·a⁴ / D  （JGJ102 6.1.3，D=E·t³/[12·(1-ν²)]）。
    /// η 为薄膜效应折减系数（与应力公式共用，同按 θ 计算），ν 默认 0.20。
    /// 单位：q[N/mm²], a,t[mm], E[MPa]。
    /// </summary>
    public GlassDeflectionResult ComputeDeflection(
        SupportType support, double a, double b, double t,
        double wLoadMPa, double eGlassMPa, double nu = 0.20,
        double? etaOverride = null)
    {
        double ab = a / b;
        double mu = support switch
        {
            SupportType.FourEdge => Interp(ab, AbRatio_4e, Mu_4e),
            SupportType.ThreeEdge => Interp(ab, AbRatio_3e, Mu_3e),
            SupportType.TwoEdge => Interp(ab, AbRatio_2e, Mu_2e),
            SupportType.PointSupported => throw new InvalidOperationException("点支承挠度另按规范"),
            _ => throw new ArgumentOutOfRangeException(nameof(support))
        };
        double eta = etaOverride ?? MembraneEta(wLoadMPa, a, eGlassMPa, t);
        double D = BendingStiffnessD(eGlassMPa, t, nu);
        double df = eta * mu * wLoadMPa * Math.Pow(a, 4) / D;
        return new GlassDeflectionResult(df, mu, eta);
    }
}

public readonly record struct GlassStressResult(double SigmaMPa, double M, double Eta, double WLoadMPa);
public readonly record struct GlassDeflectionResult(double DfMm, double Mu, double Eta);
