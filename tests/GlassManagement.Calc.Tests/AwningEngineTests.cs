using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>雨篷计算引擎测试（V31 实机对拍 yupeng_var.json）。</summary>
public class AwningEngineTests
{
    [Fact]
    public void 雨篷_默认基线_逐位一致()
    {
        var r = AwningEngine.Compute(new AwningEngine.Input());

        // 荷载组合
        Assert.Equal(0.571, r.PosWindKpa);
        Assert.Equal(2.283, r.NegWindKpa);
        Assert.Equal(1.883, r.StdCombineKpa);
        Assert.Equal(3.024, r.DsnCombineKpa);

        // 悬臂梁
        Assert.Equal(3000, r.CantileverSpanMm);
        Assert.Equal(20389320, r.TotalMoment);
        Assert.Equal(312.696, r.BeamSigma);
        Assert.Equal(38.185, r.BeamDeflection);
        Assert.Equal(24, r.BeamDeflLimit);
        Assert.Equal(13608, r.TotalShear);

        // 焊缝
        Assert.Equal(217.197, r.WeldSigma);

        // 玻璃
        Assert.Equal(45.052, r.GlassSigma);
        Assert.Equal(20.301, r.GlassDeflection);
        Assert.Equal(20.833, r.GlassDeflLimit);

        // 埋件
        Assert.Equal(2237.644, r.AnchorAreaReq);
        Assert.Equal(452.16, r.AnchorAreaActual);
        Assert.Equal(518.4, r.AnchorLab);
        Assert.Equal(2565.452, r.AnchorLa);
    }

    [Fact]
    public void 雨篷_负压风增大_强度增大()
    {
        var d = new AwningEngine.Input { BasicWindKpa = 0.6 };
        var r = AwningEngine.Compute(d);

        Assert.True(r.NegWindKpa > 2.283);
        Assert.True(r.BeamSigma > 312.696);
        Assert.True(r.WeldSigma > 217.197);
    }

    [Fact]
    public void 雨篷_玻璃加厚_应力下降()
    {
        var d = new AwningEngine.Input { GlassThicknessMm = 12 };
        var r = AwningEngine.Compute(d);

        Assert.True(r.GlassSigma < 45.052);
    }
}
