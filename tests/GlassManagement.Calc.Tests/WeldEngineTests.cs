using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>焊缝强度计算引擎测试（GB50017-2017 角焊缝，V31 实机对拍）。</summary>
public class WeldEngineTests
{
    [Fact]
    public void 焊缝截面特性_逐位一致()
    {
        var r = WeldEngine.Compute(new WeldEngine.Input());

        Assert.Equal(4.2, r.He);           // he = 0.7×6
        Assert.Equal(30.097, r.NeutralAxisD);
        Assert.Equal(475639.887, r.InertiaI);
        Assert.Equal(8214.426, r.SectionW);
        Assert.Equal(579.6, r.AreaA);
    }
}
