using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>开启扇通风面积计算引擎测试（V31 实机对拍）。</summary>
public class OpeningVentEngineTests
{
    [Fact]
    public void 开启扇_默认基线_逐位一致()
    {
        var r = OpeningVentEngine.Compute(new OpeningVentEngine.Input());

        Assert.Equal(0.9324, r.D);
        Assert.Equal(3.07692, r.Method1Fp);
        Assert.Equal(2.7, r.Method1Area);
        Assert.Equal(2.7, r.Method2OpenArea);
        Assert.Equal(0.81, r.Method2S1);
        Assert.Equal(1.3986, r.Method2S2);
        Assert.Equal(3.0186, r.Method2AirArea);
        Assert.Equal(2.7, r.Method2Area);
    }

    [Fact]
    public void 开启扇_大角度_70度以上按窗面积()
    {
        var d = new OpeningVentEngine.Input { AngleDeg = 90 };
        var r = OpeningVentEngine.Compute(d);

        // sin(90°)=1, sin(45°)=0.707
        // d = 2×1.8×0.707 = 2.545
        // Fp = 2.545×3.3 = 8.4, min(8.4, 2.7)=2.7
        Assert.Equal(2.7, r.Method1Area);
        Assert.Equal(2.7, r.Method2Area);
    }
}
