using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 结构胶计算引擎（JGJ102-2003 第5.6节，原程序式实现）。
/// 宽度：c_s = a·ψ·w·a·b / (f1·(a+b))  （5.6.1，隐框/半隐框）
/// 厚度：t_s = ψ·w·a·b / (2000·(1+α_s/δ)·β·f2) · ... （5.6.3 变体，按工况）
/// 注：ψ 变位受力系数、f1/f2 设计值、变位 allowance 等随板块尺寸/胶型号变化，属主程序分支，待 IDA 核对。
/// </summary>
public sealed class StructuralSealantEngine
{
    /// <summary>
    /// 结构胶宽度计算（JGJ102 5.6.1-1 隐框幕墙，单组件）。
    /// c_s[mm] = a·ψ·w·a_g·b_g / (f1·(a_g+b_g))
    /// a=应力调整系数(=1)，ψ=变位受力系数，w[MPa]，a_g,b_g 板块短/长边[mm]，f1 结构胶强度设计值[MPa]。
    /// </summary>
    public double SealantWidth(double psi, double wMPa, double agMm, double bgMm, double f1MPa, double aFactor = 1.0)
        => aFactor * psi * wMPa * agMm * bgMm / (f1MPa * (agMm + bgMm));

    /// <summary>
    /// 结构胶厚度计算（JGJ102 5.6.3-1）。
    /// t_s[mm] = ψ·w·a_g·b_g / (2000·(2+α_s/δ)·β·f2·δ?) —— 此处按标准显式式；δ 变位[mm]，β 补偿系数，α_s 温差，f2 设计值。
    /// 具体系数组合以主程序为准，本式保留标准结构并标注待核对项。
    /// </summary>
    public double SealantThickness(double psi, double wMPa, double agMm, double bgMm,
        double deltaMm, double beta, double alphaTemp, double f2MPa)
    {
        double denom = 2000.0 * (2.0 + alphaTemp / deltaMm) * beta * f2MPa;
        return psi * wMPa * agMm * bgMm / denom;
    }

    /// <summary>结构胶粘结宽度最小值校验（JGJ102 5.6.2：c_s ≥ 6mm 且 ≥ h/2）。</summary>
    public bool WidthOk(double cSmm, double glassThicknessMm)
        => cSmm >= 6.0 && cSmm >= glassThicknessMm / 2.0;
}
