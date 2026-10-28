using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// SGP 胶片复合玻璃 —— V31 玻璃屏第五段函数区 0xA4AA25–0xA54FFD。
///
/// 实机：三片均 6mm、胶片 0.76、wk=0.00137、αmax=0.16、1100×1380
/// 屏上：1#玻璃 6.392MPa、挠度 1.167mm、容许 18.333mm。
/// </summary>
public class SgpCompositeGlassTests
{
    private const double Wk = 0.00137;
    private const double A = 1100.0;
    private const double B = 1380.0;
    private const double T = 6.0;
    private const double Film = 0.76;
    private const double Alpha = 0.16;

    private static CompositeGlassResult Run() =>
        GlassEngine.ComputeCompositeSgp(Wk, A, B, T, T, T, Film, Alpha);

    [Fact]
    public void 剪切传递系数取4位()
    {
        // Γ = 1/(1 + 9.6×72000×137.093×0.76 / (11.3×6.76²×1100²))
        double num = 9.6 * 72000 * 137.093 * Film;
        double den = 11.3 * System.Math.Pow(T + Film, 2.0) * A * A;
        Assert.Equal(72_016_598.016, num, 3);          // 0xA51079
        Assert.Equal(624_823_284.8, den, 3);           // 0xA510F1
        Assert.Equal(0.11525914569437315, num / den, 14);   // 0xA510FE
        // 槽值 slot[22] = 0.8967
        Assert.Equal(0.8967, 1.0 / (1.0 + num / den), 4);
    }

    [Fact]
    public void 强度等效厚度_开方形式()
    {
        var r = Run();
        // sqrt(12³ / (6 + 2×0.8967×3.38)) = sqrt(143.2634824) = 11.9692724 → 11.969
        double tsRaw = System.Math.Sqrt(1728.0 / (6 + 2 * 0.8967 * 3.38));
        Assert.Equal(11.969272427210955, tsRaw, 12);
        Assert.Equal(11.969, r.StrengthThickness);   // round3 后（槽 slot[24]）
    }

    [Fact]
    public void 整片强度组合()
    {
        var r = Run();
        // Σ = 12³+6³ = 1944；wk·d³/Σ = 0.00137×1728/1944 = 0.001218
        Assert.Equal(1944.0, r.TotalStiffness);
        Assert.Equal(0.001218, 0.00137 * 1728.0 / 1944.0, 6);        // slot[28]
        // 强度组合 = 1.5×0.001218 + 0.7×0.000246 = 0.0019992 → 0.001999（0xA522A7）
        Assert.Equal(0.001999, 1.5 * (0.00137 * 1728.0 / 1944.0) + 0.7 * 0.000246, 6);
    }

    [Fact]
    public void 强度与挠度_屏上数值逐位()
    {
        var r = Run();
        Assert.Equal(6.392, r.Plies[0].Sigma);     // 屏：1#玻璃(夹层偏室外侧)强度计算值（0xA534DC）
        Assert.Equal(11.856, r.EquivalentThickness);  // 0.95×(12³+6³)^(1/3)（0xA54415）
        Assert.Equal(1.410, r.ThetaDefl);             // 0xA5472F
        Assert.Equal(10_415_846.938, r.Stiffness, 3); // 0xA54576
        Assert.Equal(1.167, r.Deflection);            // 屏：挠度（0xA54A00 段）
        Assert.Equal(18.333, r.Limit);
    }

    [Fact]
    public void 强度用η恒1_与PVB不同()
    {
        var sgp = Run();
        var pvb = GlassEngine.ComputeComposite(Wk, A, B, new[] { T, T, T }, InterlayerKind.Pvb, Alpha);
        Assert.Equal(1.0, sgp.EtaDefl);
        Assert.Equal(0.991, pvb.EtaDefl);
        // SGP 把夹层合成 12mm，等效厚度显著更大 → 挠度更小
        Assert.True(sgp.EquivalentThickness > pvb.EquivalentThickness);
        Assert.True(sgp.Deflection < pvb.Deflection);
        // 但强度用 t_s=11.969（比 t_e 小）且荷载更集中 → σ 反而更小（6.392 < 10.345）
        Assert.True(sgp.Plies[0].Sigma < pvb.Plies[0].Sigma);
    }

    [Fact]
    public void 换胶片厚度_Gamma与ts随动()
    {
        var a = GlassEngine.ComputeCompositeSgp(Wk, A, B, T, T, T, 0.76, Alpha);
        var b = GlassEngine.ComputeCompositeSgp(Wk, A, B, T, T, T, 2.28, Alpha);
        // 胶片越厚 → Γ 下降且 y=(t+t_f)/2 变大使 2Γy 增长更快 → 分母更大 → t_s 变小
        // （物理含义：胶片越厚，剪切滑移占比越大，等效厚度越靠近单片）
        Assert.True(b.StrengthThickness < a.StrengthThickness,
                    "SGP 胶片越厚 → t_s 变小（滑移更大）");
        Assert.Equal(11.969, a.StrengthThickness);
        // 挠度 t_e 只由 Σt³ 决定，与胶片厚度无关
        Assert.Equal(a.EquivalentThickness, b.EquivalentThickness);
    }
}
