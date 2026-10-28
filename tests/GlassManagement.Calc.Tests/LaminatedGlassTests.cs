using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 夹层玻璃（两片 + 胶片）—— V31「玻璃计算」屏的夹层分支（函数区 0xA2DB00–0xA2FA00）。
///
/// 实机：1100×1380、两片均 6mm、wk=0.00137、αmax=0.16
/// → 屏上 <c>1.37 / 13.51 / 84 / 4.375 / 18.333</c>（**只出一组 σ**）。
/// </summary>
public class LaminatedGlassTests
{
    private const double Wk = 0.00137;
    private const double A = 1100.0;
    private const double B = 1380.0;
    private const double T = 6.0;
    private const double Alpha = 0.16;

    private static LaminatedGlassResult Run() =>
        GlassEngine.ComputeLaminated(Wk, A, B, T, T, Alpha);

    [Fact]
    public void 自重取两片之和_与中空不同()
    {
        var lam = Run();
        var insul = GlassEngine.ComputeInsulated(Wk, A, B, T, T, Alpha);
        // 夹层：round6((6+6)×2.56e-5) = 0.000307；中空：round6(6×2.56e-5) = 0.000154
        Assert.Equal(0.000307, 12.0 * GlassEngine.DensityNmm3, 6);
        Assert.Equal(0.001114, lam.Ply.WStrength);          // 1.5×0.000685+0.7×0.000246
        Assert.True(insul.Outer.WStrength > lam.Ply.WStrength, "中空外片乘了 1.1，应更大");
    }

    [Fact]
    public void 两片均分没有1点1放大()
    {
        var r = Run();
        Assert.Equal(0.000685, r.Ply.WkShare);      // wk×216/432 = wk/2（0xA2DEEE 段）
        Assert.Equal(0.000746, r.Ply.WDeflection);  // +0.5×0.000246
    }

    [Fact]
    public void 强度只出一组_用单片厚度()
    {
        var r = Run();
        Assert.Equal(11.705, r.Ply.Theta);   // 0xA2E683
        Assert.Equal(0.953, r.Ply.Eta);      // 0xA79568 → 0.95318
        Assert.Equal(13.51, r.Ply.Sigma);    // 屏：玻璃强度计算值 13.51MPa（0xA2EA28）
        Assert.Equal(6.0, r.Ply.Thickness);
    }

    [Fact]
    public void 等效厚度无0点95系数()
    {
        var r = Run();
        // 夹层 t_e = (216+216)^(1/3) = 7.559526 → round3 = 7.56（0xA2F3FC）
        // 中空 t_e = 0.95×7.559526 = 7.1815 → round3 = 7.182
        Assert.Equal(7.56, r.EquivalentThickness);
        var insul = GlassEngine.ComputeInsulated(Wk, A, B, T, T, Alpha);
        Assert.Equal(7.182, insul.EquivalentThickness);
        Assert.True(r.EquivalentThickness > insul.EquivalentThickness);
    }

    [Fact]
    public void 挠度_屏上数值逐位()
    {
        var r = Run();
        Assert.Equal(8.528, r.ThetaDefl);              // 0xA2F709
        Assert.Equal(0.972, r.EtaDefl);                // 0.96−0.004×(−1.472)…
        Assert.Equal(2_700_507.600, r.Stiffness, 3);   // 72000×7.56³/11.52（0xA2F553）
        Assert.Equal(4.375, r.Deflection);             // 屏：玻璃挠度计算值 4.375mm（0xA2F9CE）
        Assert.Equal(18.333, r.Limit);                 // 1100/60（0xA2F2A8）
    }

    [Fact]
    public void 夹层挠度小于中空_因为等效厚度更大()
    {
        var lam = Run();
        var insul = GlassEngine.ComputeInsulated(Wk, A, B, T, T, Alpha);
        Assert.True(lam.Deflection < insul.Deflection);
    }
}
