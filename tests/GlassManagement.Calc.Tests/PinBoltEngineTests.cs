using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>扭剪钢销/螺栓/螺钉计算引擎测试（V31 实机对拍）。</summary>
public class PinBoltEngineTests
{
    [Fact]
    public void 扭剪钢销_默认基线_逐位一致()
    {
        var r = PinBoltEngine.Compute(new PinBoltEngine.Input());

        Assert.Equal(874.142, r.MaxShearN);
        Assert.Equal(3728.75, r.ShearAllowableN);
        Assert.True(r.ShearOk);
        Assert.Equal(3150.0, r.BearingAllowableN);
        Assert.True(r.BearingOk);
    }

    [Fact]
    public void 扭剪钢销_无弯矩_纯剪力均分()
    {
        var d = new PinBoltEngine.Input { MomentNmm = 0 };
        var r = PinBoltEngine.Compute(d);

        // fx=N/n=575, fy=V/n=625, max=√(575²+625²)=√721250=849.264
        Assert.Equal(849.264, r.MaxShearN);
    }
}
