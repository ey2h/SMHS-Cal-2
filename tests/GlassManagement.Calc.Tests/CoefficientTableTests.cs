using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 锁死原程序计算书打印的精确系数表值（m/μ/α），保证“表值小数点一致”。
/// </summary>
public class CoefficientTableTests
{
    private readonly GlassPanelEngine _panel = new();
    private readonly ColumnSpanEngine _col = new();

    [Theory]
    [InlineData(1.0, 0.0468, 0.00328)]
    [InlineData(0.9, 0.0451, 0.00304)]
    [InlineData(0.8, 0.0440, 0.00281)]
    [InlineData(0.7, 0.0400, 0.00249)]
    [InlineData(0.6, 0.0352, 0.00215)]
    [InlineData(0.5, 0.0283, 0.00179)]
    public void FourEdge_TableValues_MatchReference(double ab, double mExp, double muExp)
    {
        double a = ab * 1000, b = 1000, t = 6, w = 1, E = 72000;
        var s = _panel.ComputeStress(GlassPanelEngine.SupportType.FourEdge, GlassPanelEngine.GlassType.Monolithic, a, b, t, w, E);
        var d = _panel.ComputeDeflection(GlassPanelEngine.SupportType.FourEdge, a, b, t, w, E);
        Assert.Equal(mExp, s.M, 4);
        Assert.Equal(muExp, d.Mu, 5);
    }

    [Theory]
    [InlineData(0.5, 0.73)]
    [InlineData(0.6, 0.65)]
    [InlineData(0.7, 0.59)]
    [InlineData(0.8, 0.53)]
    [InlineData(0.9, 0.49)]
    [InlineData(1.0, 0.42)]
    public void DoubleSpanAlpha_TableValues_MatchReference(double lr, double alphaExp)
    {
        Assert.Equal(alphaExp, _col.DoubleSpanAlpha(lr), 2);
    }

    [Fact]
    public void FourEdge_Stress_Formula()
    {
        // σ = 6·m·w·a²/t² ；取 a/b=1, m=0.0468, w=0.001MPa, a=t=1000? 用 a=1000,b=1000,t=10,w=0.001
        var s = _panel.ComputeStress(GlassPanelEngine.SupportType.FourEdge, GlassPanelEngine.GlassType.Monolithic, 1000, 1000, 10, 0.001, 72000, etaOverride: 1.0);
        double expected = 6 * 0.0468 * 0.001 * 1000 * 1000 / (10 * 10);
        Assert.Equal(expected, s.SigmaMPa, 6);
    }
}
