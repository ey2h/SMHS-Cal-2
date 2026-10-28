using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>其它玻璃计算引擎测试（V31 实机对拍）。</summary>
public class OtherGlassEngineTests
{
    [Fact]
    public void 其它玻璃_四玻外夹层双中空_逐位一致()
    {
        var r = OtherGlassEngine.Compute(new OtherGlassEngine.Input());

        Assert.Equal(8.833, r.OuterSigma);
        Assert.Equal(8.833, r.Outer2Sigma);
        Assert.Equal(7.217, r.Inner2Sigma);
        Assert.Equal(7.217, r.InnerSigma);
        Assert.Equal(2.356, r.Deflection);
        Assert.Equal(16.667, r.DeflectionAllowable);
        Assert.True(r.DeflectionOk);
    }

    [Fact]
    public void 其它玻璃_外夹层厚内夹层薄_外片强度大()
    {
        var d = new OtherGlassEngine.Input
        {
            OuterThicknessMm = 8,
            Outer2ThicknessMm = 8,
            Inner2ThicknessMm = 6,
            InnerThicknessMm = 6,
        };
        var r = OtherGlassEngine.Compute(d);

        // 外夹层厚 → 分配更多荷载 → 外片强度 > 内片
        Assert.True(r.OuterSigma > r.InnerSigma);
    }
}
