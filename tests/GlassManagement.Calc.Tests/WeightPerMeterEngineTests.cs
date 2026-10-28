using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>型材米重引擎测试（V31 实机对拍）。</summary>
public class WeightPerMeterEngineTests
{
    [Fact]
    public void 米重_铝材_逐位一致()
    {
        var r = WeightPerMeterEngine.Compute(new WeightPerMeterEngine.Input());
        Assert.Equal(2.7, r.WeightPerMeter);
    }

    [Fact]
    public void 米重_钢材_逐位一致()
    {
        var d = new WeightPerMeterEngine.Input { DensityKgM3 = 7850 };
        var r = WeightPerMeterEngine.Compute(d);
        Assert.Equal(7.85, r.WeightPerMeter);
    }
}
