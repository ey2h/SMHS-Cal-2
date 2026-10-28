using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>百叶窗通风面积计算引擎测试（V31 实机对拍）。</summary>
public class LouverVentEngineTests
{
    [Fact]
    public void 百叶窗_默认基线_逐位一致()
    {
        var r = LouverVentEngine.Compute(new LouverVentEngine.Input());

        Assert.Equal(0.96, r.VentAreaM2);
        Assert.Equal(0.356, r.VentRatio);
    }

    [Fact]
    public void 百叶窗_无叶片_面积缩小()
    {
        var d = new LouverVentEngine.Input { BladeCount = 0 };
        var r = LouverVentEngine.Compute(d);

        // Af = 1.2×(0.025+0.025) = 1.2×0.05 = 0.06
        Assert.Equal(0.06, r.VentAreaM2);
    }
}
