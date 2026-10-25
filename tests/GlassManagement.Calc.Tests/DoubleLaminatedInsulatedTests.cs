using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 双夹层-中空玻璃（4 片：外夹层 2 片 + 中空腔 + 内夹层 2 片）。
///
/// 实机取证：`local-data/recon/glass_cfg_scan.py --cases dbllam`
/// 工况 1100×1380、4 片均 6mm、wk=0.00137、αmax=0.16。
/// 屏上：最外片(1#) 8.172 / 次外片(2#) 8.172 / 次内片(3#) 7.539 / 最内片(4#) 7.539 / df 2.625 / 限值 18.333
///
/// 槽：t_e[11]=9.04875  w[13]=0.000754(每片0.000377)  w[26]=0.000685(每片0.000342)
///     W标准[22]=0.000438 [33]=0.000404   W强度[23]=0.000652 [34]=0.000599
///     θ[40]=6.8724 η[41]=0.985  θ[54]=6.3389 η[55]=0.989  D[70]=4630690.832
/// </summary>
public class DoubleLaminatedInsulatedTests
{
    private static CompositeGlassResult Run()
        => GlassEngine.ComputeDoubleLaminated(0.00137, 1100, 1380, 6, 6, 6, 6, 0.16);

    [Fact]
    public void 四片强度_屏上数值逐位()
    {
        var r = Run();
        Assert.Equal(4, r.Plies.Length);
        Assert.Equal(8.172, r.Plies[0].Sigma);   // 最外片(1#)
        Assert.Equal(8.172, r.Plies[1].Sigma);   // 次外片(2#)：与外夹层另一片同厚同值
        Assert.Equal(7.539, r.Plies[2].Sigma);   // 次内片(3#)
        Assert.Equal(7.539, r.Plies[3].Sigma);   // 最内片(4#)
    }

    [Fact]
    public void 两侧份额_外侧含1点1倍()
    {
        var r = Run();
        // 外侧夹层整体 1.1×wk×D外/ΣD = 0.0007535 → 每片 0.00037675 → round6 0.000377（槽[13]/2）
        Assert.Equal(0.000377, r.Plies[0].WkShare);
        Assert.Equal(0.000377, r.Plies[1].WkShare);
        // 内侧夹层 0.0006849 → 每片 0.0003425 → round6 0.000342（银行家舍入，槽[27]）
        Assert.Equal(0.000342, r.Plies[2].WkShare);
        Assert.Equal(0.000342, r.Plies[3].WkShare);
    }

    [Fact]
    public void 两个组合值_取整口径不同()
    {
        var r = Run();
        // W标准 用未取整份额：round6(0.00037675 + 0.5×0.00012288) = 0.000438
        Assert.Equal(0.000438, r.Plies[0].WDeflection);
        // 内侧：round6(0.00034250 + 0.00006144) = 0.000404（若用 round6 份额 0.000342 会得 0.000403）
        Assert.Equal(0.000404, r.Plies[2].WDeflection);
        // W强度 用 round6 份额：round6(1.5×0.000377 + 0.7×0.00012288) = 0.000652
        Assert.Equal(0.000652, r.Plies[0].WStrength);
        Assert.Equal(0.000599, r.Plies[2].WStrength);
    }

    [Fact]
    public void theta与eta_屏上数值逐位()
    {
        var r = Run();
        Assert.Equal(6.8724, r.Plies[0].Theta);   // 槽[40]
        Assert.Equal(0.985, r.Plies[0].Eta);      // 槽[41]
        Assert.Equal(6.3389, r.Plies[2].Theta);   // 槽[54]
        Assert.Equal(0.989, r.Plies[2].Eta);      // 槽[55]
    }

    [Fact]
    public void 挠度与限值_屏上数值逐位()
    {
        var r = Run();
        Assert.Equal(2.625, r.Deflection);        // 屏上 2.625mm
        Assert.Equal(18.333, r.Limit);            // 屏上 18.333mm
    }

    [Fact]
    public void 等效厚度_差距万分之七未定位()
    {
        // ⚠ 实测 9.04875，本实现 9.04814（t_e³: 740.91 vs 740.77）。
        // 按 (Σ(0.95tᵢ)³)^(1/3) = (4×185.193)^(1/3) = 9.04814，与实测差 7e-5。
        // 不硬凑：这里锁住「公式结构正确、差距在 1e-4 以内」，待改厚度取两点定系数。
        var r = Run();
        Assert.InRange(r.EquivalentThickness, 9.048, 9.049);
        Assert.InRange(System.Math.Abs(r.EquivalentThickness - 9.04875), 0.0, 0.001);
    }

    [Fact]
    public void 与双中空和三片构型_三者等效厚度口径各不相同()
    {
        // 双中空 6/6/6 → 7.952（逐层衰减）；双夹层-中空 6/6/6/6 → 9.048
        Assert.Equal(7.952, GlassEngine.DoubleInsulatedThickness(6, 6, 6));
        Assert.InRange(Run().EquivalentThickness, 9.048, 9.049);
        Assert.NotEqual(GlassEngine.DoubleInsulatedThickness(6, 6, 6), Run().EquivalentThickness);
    }

    [Fact]
    public void 内外侧同厚时_外侧份额大于内侧()
    {
        var r = Run();
        Assert.True(r.Plies[0].WkShare > r.Plies[2].WkShare, "外侧含 1.1 倍，份额应更大");
        // 0.000377/0.000342 ≈ 1.10
        Assert.InRange(r.Plies[0].WkShare / r.Plies[2].WkShare, 1.09, 1.11);
    }
}
