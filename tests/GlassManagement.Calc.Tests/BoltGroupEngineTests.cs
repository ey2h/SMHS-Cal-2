using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>螺栓群计算引擎测试（GB50017 偏心受剪，V31 实机对拍）。</summary>
public class BoltGroupEngineTests
{
    [Fact]
    public void 螺栓群_默认参数_逐位一致()
    {
        var r = BoltGroupEngine.Compute(new BoltGroupEngine.Input());

        Assert.Equal(10000, r.SumR2);
        Assert.Equal(645, r.VerticalComponent);
        Assert.Equal(590, r.HorizontalComponent);
        Assert.Equal(874.142, r.MaxBoltShear);
        Assert.Equal(2747.5, r.ShearAllowable);
        Assert.True(r.ShearOk);
        Assert.Equal(2775, r.BearingAllowable);
        Assert.True(r.BearingOk);
    }
}
