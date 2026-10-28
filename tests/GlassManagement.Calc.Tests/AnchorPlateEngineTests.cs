using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>锚板厚度计算引擎测试（GB50010，V31 实机对拍）。</summary>
public class AnchorPlateEngineTests
{
    [Fact]
    public void 锚板厚度_默认参数_逐位一致()
    {
        var r = AnchorPlateEngine.Compute(new AnchorPlateEngine.Input());

        Assert.Equal(1024.975, r.AreaRequired);
        Assert.Equal(60000, r.AreaActual);
        Assert.True(r.AreaOk);
        Assert.Equal(7.2, r.MinThickness);
        Assert.Equal(8, r.ActualThickness);
        Assert.True(r.ThicknessOk);
    }
}
