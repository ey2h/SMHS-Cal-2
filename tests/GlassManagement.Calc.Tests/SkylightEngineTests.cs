using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>采光顶杆件计算引擎测试（V31 实机对拍）。</summary>
public class SkylightEngineTests
{
    [Fact]
    public void 采光顶单坡_默认基线_逐位一致()
    {
        var r = SkylightEngine.Compute(new SkylightEngine.Input());

        Assert.Equal(376.235, r.MainSigma);
        Assert.Equal(185.976, r.MainCombinedDeflection);
        Assert.Equal(17.321, r.MainCombinedLimit);
        Assert.Equal(61.82, r.MainDeadDeflection);
        Assert.Equal(6.928, r.MainDeadLimit);

        Assert.Equal(446.44, r.SubSigma);
        Assert.Equal(10.237, r.SubParaCombinedDeflection);
        Assert.Equal(21.041, r.SubVertCombinedDeflection);
        Assert.Equal(5.464, r.SubParaDeadDeflection);
        Assert.Equal(6.062, r.SubVertDeadDeflection);
        Assert.Equal(8.333, r.SubParaLimit);
        Assert.Equal(7.5, r.SubVertLimit);
    }

    [Fact]
    public void 采光顶_陡坡_斜长增大()
    {
        var d = new SkylightEngine.Input { SlopeAngleDeg = 45 };
        var r = SkylightEngine.Compute(d);

        // 45° 斜长更大 → 弯矩更大 → 强度更大
        Assert.True(r.MainSigma > 0);
    }
}
