using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>转角立柱计算引擎测试（双向受弯，V31 实机对拍）。</summary>
public class CornerColumnEngineTests
{
    [Fact]
    public void 转角立柱_默认参数_逐位一致()
    {
        var r = CornerColumnEngine.Compute(new CornerColumnEngine.Input());

        Assert.Equal(0.895, r.QStandard);
        Assert.Equal(2264760, r.Mx);
        Assert.Equal(1092187.5, r.My);
        Assert.Equal(2632.5, r.AxialN);
        Assert.Equal(75.814, r.SigmaCase1);
        Assert.Equal(72.224, r.SigmaCase2);
        Assert.True(r.SigmaOk);
        Assert.Equal(8.133, r.DeflectionY);
        Assert.Equal(8.024, r.DeflectionX);
        Assert.Equal(20, r.DeflectionAllowable);
    }
}
