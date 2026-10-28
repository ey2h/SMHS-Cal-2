using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>热应力伸缩量引擎测试（V31 实机对拍）。</summary>
public class ThermalExpansionEngineTests
{
    [Fact]
    public void 伸缩量_逐位一致()
    {
        var r = ThermalExpansionEngine.Compute(new ThermalExpansionEngine.Input());
        Assert.Equal(5.52, r.ExpansionMm);
    }
}
