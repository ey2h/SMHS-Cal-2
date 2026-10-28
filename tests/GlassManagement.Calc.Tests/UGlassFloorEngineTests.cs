using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>U型玻璃 + 地板玻璃引擎测试（V31 实机逐位对拍）。</summary>
public class UGlassFloorEngineTests
{
    [Fact]
    public void U型玻璃_默认参数_逐位一致()
    {
        var r = UGlassEngine.Compute(new UGlassEngine.Input());

        Assert.Equal(1.37, r.WindStandardKpa);
        Assert.Equal(0.3206, r.LineLoad);
        Assert.Equal(0.0371, r.SeismicLoad);
        Assert.Equal(0.507, r.DesignLoad);
        Assert.Equal(396093.75, r.Moment);
        Assert.Equal(72.212, r.Sigma);
        Assert.Equal(51, r.SigmaAllowable);
        Assert.False(r.SigmaOk);           // 72.212 > 51，不满足
        Assert.Equal(12.778, r.Deflection);
        Assert.Equal(16.667, r.DeflectionAllowable);
        Assert.True(r.DeflectionOk);
    }

    [Fact]
    public void 地板玻璃_默认参数_逐位一致()
    {
        var r = FloorGlassEngine.Compute(new FloorGlassEngine.Input());

        Assert.Equal(2, r.LiveLoadKpa);
        Assert.Equal(10.079, r.EquivalentThickness);
        Assert.Equal(0.00041, r.SeismicLoad);
        Assert.Equal(0.00241, r.StandardLoad);
        Assert.Equal(0.003533, r.DesignLoad);
        Assert.Equal(25.443, r.Sigma);
        Assert.Equal(42, r.SigmaAllowable);
        Assert.True(r.SigmaOk);
        Assert.Equal(12.64, r.Deflection);
        Assert.Equal(7.5, r.DeflectionAllowable);
        Assert.False(r.DeflectionOk);      // 12.64 > 7.5，不满足
    }
}
