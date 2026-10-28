using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>多点锁扇框及锁点计算引擎测试（V31 实机对拍）。</summary>
public class MultiPointLockEngineTests
{
    [Fact]
    public void 多点锁_默认基线_逐位一致()
    {
        var r = MultiPointLockEngine.Compute(new MultiPointLockEngine.Input());

        Assert.Equal(12.936, r.FrameSigma);
        Assert.True(r.FrameSigmaOk);
        Assert.Equal(0.233, r.CantileverDeflection);
        Assert.Equal(3.111, r.CantileverLimit);
        Assert.Equal(0.049, r.MidSpanDeflection);
        Assert.Equal(2.5, r.MidSpanLimit);
        Assert.Equal(1.805, r.RequiredPins);
        Assert.True(r.PinsOk);
        Assert.Equal(1766.49, r.MaxPinShearN);
        Assert.Equal(1962.5, r.PinShearAllowableN);
        Assert.True(r.PinShearOk);
    }

    [Fact]
    public void 多点锁_锁点不足_需要更多锁点()
    {
        var d = new MultiPointLockEngine.Input { PinCount = 1 };
        var r = MultiPointLockEngine.Compute(d);

        // 1.805 > 1 → 不满足
        Assert.False(r.PinsOk);
    }
}
