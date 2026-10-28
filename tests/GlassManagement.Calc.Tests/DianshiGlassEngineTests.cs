using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>点式玻璃幕墙「点式肋（无肋）驳接」屏引擎测试（V31 实机逐位对拍）。</summary>
public class DianshiGlassEngineTests
{
    private static DianshiGlassEngine.Input DefaultInput() => new();

    [Fact]
    public void 点式肋_四点驳接_默认参数_逐位一致()
    {
        var r = DianshiGlassEngine.Compute(DefaultInput());

        Assert.Equal(1.37, r.WindStandardKpa);
        Assert.Equal(0.000205, r.SeismicMpa);
        Assert.Equal(0.002198, r.Qd);
        Assert.Equal(0.001472, r.WD);
        Assert.Equal(0.1424, r.BendingCoef);
        Assert.Equal(0.0186, r.DeflectionCoef);
        Assert.Equal(42.998, r.GlassSigma);
        Assert.Equal(22.498, r.GlassDeflection);
        Assert.Equal(25.833, r.GlassDeflectionAllowable);
        Assert.Equal(411.19, r.RibRequiredWidth);
        Assert.Equal(12.778, r.RibDeflection);
        Assert.Equal(20, r.RibDeflectionAllowable);
        Assert.False(r.RibWidthOk);   // 200 < 411.19，不满足
    }

    [Fact]
    public void 点式肋_肋挠度_用WD而非wk()
    {
        // 肋挠度 = 5·wD·a·L⁴/(384·E·I)，wD=0.001472（不是 wk=0.00137）
        // 若用 wk 会得到 11.892（全玻璃值），点式肋实机是 12.778
        var r = DianshiGlassEngine.Compute(DefaultInput());
        Assert.Equal(12.778, r.RibDeflection);
        Assert.NotEqual(11.892, r.RibDeflection);
    }

    [Fact]
    public void 点式肋_玻璃跨度用肋间距2_非短边()
    {
        // 玻璃强度用 a=肋间距2=1550（不是短边1500）
        // 验证：改短边不影响玻璃强度，改肋间距2影响
        var d1 = DefaultInput();
        d1.GlassShortSideMm = 1200;
        var r1 = DianshiGlassEngine.Compute(d1);
        Assert.Equal(42.998, r1.GlassSigma);   // 短边不影响玻璃强度

        var d2 = DefaultInput();
        d2.RibSpacing2Mm = 2000;
        var r2 = DianshiGlassEngine.Compute(d2);
        Assert.NotEqual(42.998, r2.GlassSigma); // 肋间距2影响玻璃强度
    }
}
