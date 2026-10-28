using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>热应力 + 排烟面积引擎测试（V31 实机对拍）。</summary>
public class ThermalSmokeEngineTests
{
    [Fact]
    public void 热应力_逐位一致()
    {
        var r = ThermalStressEngine.Compute(new ThermalStressEngine.Input());
        Assert.Equal(128.8, r.SigmaT);
    }

    [Fact]
    public void 排烟面积_逐位一致()
    {
        var r = SmokeVentEngine.Compute(new SmokeVentEngine.Input());
        Assert.Equal(1.35, r.VentArea);
    }

    [Fact]
    public void 排烟面积_角度大于70_取窗面积()
    {
        var d = new SmokeVentEngine.Input { AngleDeg = 80 };
        var r = SmokeVentEngine.Compute(d);
        Assert.Equal(2.7, r.VentArea);   // 1.5×1.8 = 2.7
    }
}

