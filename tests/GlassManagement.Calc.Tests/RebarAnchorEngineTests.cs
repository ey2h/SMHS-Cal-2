using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>建筑植筋计算引擎测试（GB50367，V31 实机对拍）。</summary>
public class RebarAnchorEngineTests
{
    [Fact]
    public void 植筋_默认参数_逐位一致()
    {
        var r = RebarAnchorEngine.Compute(new RebarAnchorEngine.Input());

        Assert.Equal(12100, r.SumR2);
        Assert.Equal(2159.091, r.MaxBoltTension);
        Assert.Equal(113.04, r.AnchorArea);
        Assert.Equal(33912, r.TensionAllowable);
        Assert.Equal(313.043, r.BaseAnchorage);
        Assert.Equal(568.173, r.FinalAnchorage);
    }
}
