using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 中空玻璃（两片）—— V31「玻璃计算」屏的中空分支。
///
/// 期望值来自实机：屏上原文（<c>1.37 / 14.697 / 84 / 13.51 / 84 / 5.029 / 18.333</c>）
/// + Frida 抓的运算 DAG（<c>v31runs/glass_insul_ops.json</c>）
/// + 定稿点（<c>v31runs/glass_insul_rounds.json</c>，函数区 0xA26xxx–0xA291xx）。
/// </summary>
public class InsulatedGlassTests
{
    private const double Wk = 0.00137;
    private const double A = 1100.0;
    private const double B = 1380.0;
    private const double T = 6.0;
    private const double Alpha = 0.16;

    private static InsulatedGlassResult Run() =>
        GlassEngine.ComputeInsulated(Wk, A, B, T, T, Alpha);

    [Fact]
    public void 荷载按t三次方分配_外片再乘1点1()
    {
        var r = Run();
        Assert.Equal(0.000754, r.Outer.WkShare);   // 1.1×0.00137×216/432（0xA26A2D）
        Assert.Equal(0.000685, r.Inner.WkShare);   //     0.00137×216/432（0xA26A2B 段）
        // 外片确实被放大：0.001507 / 2 = 0.0007535 → round6
        Assert.Equal(0.001507, GlassEngine.InsulatedOuterFactor * Wk, 6);
    }

    [Fact]
    public void 两片的组合值各自独立()
    {
        var r = Run();
        Assert.Equal(0.001217, r.Outer.WStrength);      // 1.5×0.000754+0.7×0.000123（0xA26C8C）
        Assert.Equal(0.000816, r.Outer.WDeflection);    // 0.000754+0.5×0.000123（0xA26B43）
        Assert.Equal(0.001114, r.Inner.WStrength);      // 1.5×0.000685+0.7×0.000123
        Assert.Equal(0.000746, r.Inner.WDeflection);    // 0.000685+0.5×0.000123
    }

    [Fact]
    public void 两片的theta与折减系数()
    {
        var r = Run();
        Assert.Equal(12.803, r.Outer.Theta);   // 0.000816×1100⁴/(72000×6⁴)（0xA279E1）
        Assert.Equal(11.705, r.Inner.Theta);   // 0.000746×1100⁴/(72000×6⁴)（0xA282C7）
        Assert.Equal(0.949, r.Outer.Eta);      // 0.96−0.004×2.803（0xA79568）
        Assert.Equal(0.953, r.Inner.Eta);      // 0.96−0.004×1.705
    }

    [Fact]
    public void 两片强度_屏上数值逐位()
    {
        var r = Run();
        Assert.Equal(14.697, r.Outer.Sigma);   // 外片玻璃强度计算值（0xA27D86）
        Assert.Equal(13.51, r.Inner.Sigma);    // 内片玻璃强度计算值（0xA2866C）
        Assert.True(r.Outer.Sigma > r.Inner.Sigma, "外片乘了 1.1，必然更大");
    }

    [Fact]
    public void 等效厚度与挠度_屏上数值逐位()
    {
        var r = Run();
        // t_e 原值 0.95×(6³+6³)^(1/3) = 7.1815499844，但 0xA28BA2 定稿点已把它 round3 成 7.182，
        // 后续 Pow(t_e,3)/Pow(t_e,4) 用的都是 7.182 ⇒ 引擎返回取整后的值。
        Assert.Equal(7.182, r.EquivalentThickness);
        Assert.Equal(7.181549984400776, 0.95 * System.Math.Pow(432.0, 1.0 / 3.0), 12);
        Assert.Equal(10.471, r.ThetaDefl);                      // 0xA28EB6
        Assert.Equal(0.958, r.EtaDefl);
        Assert.Equal(2_315_347.704, r.Stiffness, 3);            // 72000×t_e³/11.52（0xA28D00）
        Assert.Equal(5.029, r.Deflection);                        // 屏：玻璃挠度计算值 5.029mm（0xA2917B）
        Assert.Equal(18.333, r.Limit);                            // 1100/60
    }

    [Fact]
    public void 挠度比单片小_因为等效厚度大于单片厚度()
    {
        var single = GlassEngine.Compute(Wk, A, B, T, Alpha);
        var insul = Run();
        Assert.True(insul.EquivalentThickness > T);
        Assert.True(insul.Deflection < single.Deflection);
    }

    [Fact]
    public void 内外片同厚时两片应力接近但不相等_因外片乘1点1()
    {
        var r = Run();
        // 同厚同 η 附近时，σ 比值应约等于 1.1（实测 14.697/13.51 = 1.0878，η 差异吃掉一点）
        double ratio = r.Outer.Sigma / r.Inner.Sigma;
        Assert.InRange(ratio, 1.05, 1.10);
    }
}
