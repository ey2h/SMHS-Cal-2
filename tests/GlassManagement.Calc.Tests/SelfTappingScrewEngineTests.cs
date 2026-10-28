using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>自攻螺钉及自攻自钻螺钉计算引擎测试（V31 实机对拍）。</summary>
public class SelfTappingScrewEngineTests
{
    [Fact]
    public void 自攻螺钉_默认基线_逐位一致()
    {
        var r = SelfTappingScrewEngine.Compute(new SelfTappingScrewEngine.Input());

        Assert.Equal(1, r.Segment);       // 2P≤Le<4P
        Assert.Equal(1132.6, r.Rn);
        Assert.True(r.RnOk);
        Assert.Equal(4.948, r.EffectiveAreaMm2);
        Assert.Equal(841.16, r.Ns);
        Assert.False(r.NsOk);             // 841.16 < 950
    }

    [Fact]
    public void 自攻螺钉_短咬合_分段0()
    {
        var d = new SelfTappingScrewEngine.Input { EngageLengthMm = 2.5 }; // 2mm≤Le<2.6
        var r = SelfTappingScrewEngine.Compute(d);

        Assert.Equal(0, r.Segment);
        // Rn = 0.56×3.5×2.5×110 = 539
        Assert.Equal(539.0, r.Rn);
    }

    [Fact]
    public void 自攻螺钉_长咬合_分段2()
    {
        var d = new SelfTappingScrewEngine.Input { EngageLengthMm = 6.0 }; // 4P=5.2≤Le≤8
        var r = SelfTappingScrewEngine.Compute(d);

        Assert.Equal(2, r.Segment);
        // Rn = 0.76×3.5×6×160 = 2553.6
        Assert.Equal(2553.6, r.Rn);
    }
}
