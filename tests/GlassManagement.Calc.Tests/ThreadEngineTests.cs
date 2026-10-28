using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>螺钉与内螺纹计算引擎测试（V31 实机对拍）。</summary>
public class ThreadEngineTests
{
    private static ThreadEngine.Input DefaultInput() => new();

    [Fact]
    public void 螺纹_默认基线_逐位一致()
    {
        var r = ThreadEngine.Compute(DefaultInput());

        Assert.Equal(4623.0, r.TensileAllowableN);
        Assert.Equal(3517.5, r.ShearAllowableN);
        Assert.Equal(1.25, r.CombinedStress);
        Assert.False(r.CombinedOk);   // 1.25 > 1.0
        Assert.Equal(4440.0, r.WallBearingAllowableN);
        Assert.Equal(4485.876, r.ThreadBearingAllowableN);
        Assert.Equal(3626.7, r.ThreadShearOutAllowableN);
    }

    [Fact]
    public void 螺纹_拉力减半_复合满足()
    {
        var d = DefaultInput();
        d.TensileForceN = 1750;
        d.ShearForceN = 1750;
        var r = ThreadEngine.Compute(d);

        // (1750/4623)² + (1750/3517.5)² = 0.1433 + 0.2475 = 0.3908 → √ = 0.625
        Assert.Equal(0.625, r.CombinedStress);
        Assert.True(r.CombinedOk);
        Assert.True(r.TensileOk);
        Assert.True(r.ShearOk);
    }
}
