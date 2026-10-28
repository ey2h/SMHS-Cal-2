using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 多跨铰接连续【一次超静定】梁：期望值全部来自 V31 实机
/// （60/80 系列、n=10、第一页默认风荷载）。公式出处见
/// <see cref="IndeterminateColumnEngine"/> 注释与 docs/V31_INDETERMINATE_CODE.md。
/// </summary>
public class IndeterminateColumnTests
{
    private const double E = 70000.0;
    private const double I = 885950.0;
    private const double W = 21452.0;
    private const double Sx = 13606.0;
    private const double Tw = 6.0;

    private static IndeterminateColumnEngine.Result Run(double a1, double a2, double span,
                                                        double qk, double qd)
        => IndeterminateColumnEngine.Compute(a1, a2, span, qk, qd, E, I, W, Sx, Tw);

    [Fact]
    public void L2800_SupportControl_MatchesV31()
    {
        var r = Run(600, 600, 2800, 1.392, 2.088);
        Assert.False(r.SpanControl);
        Assert.Equal(2325.0, r.HingeForce, 9);                 // 数组 P 实测 2325
        Assert.Equal(1770840.0, r.Moment, 6);                   // 实测 1 770 840
        Assert.Equal(-1675678.2353, r.ArrayMoment, 3);
        Assert.Equal(3577.8, r.Shear, 9);
        Assert.Equal(4.724, r.DeflectionTerm1, 9);
        Assert.Equal(2.448, r.DeflectionTerm2, 9);
        Assert.Equal(82.549, WindLoadEngine.VbaRound(r.Sigma, 3), 9);
        Assert.Equal(9.158, WindLoadEngine.VbaRound(r.Tau, 3), 9);
        Assert.Equal(84.059, WindLoadEngine.VbaRound(r.SigmaEq, 3), 9);
        Assert.Equal(3.5, r.Deflection, 9);
        Assert.Equal(18.889, WindLoadEngine.VbaRound(r.Limit, 3), 9);
    }

    [Theory]
    // A₁=A₂=600，跨度扫描
    [InlineData(600, 2700, 1.394, 2.091, 80.448, 8.968, 81.934, 2.987, 18.333)]
    [InlineData(600, 2750, 1.393, 2.090, 81.503, 9.063, 83.001, 3.234, 18.611)]
    [InlineData(600, 2800, 1.392, 2.088, 82.549, 9.158, 84.059, 3.500, 18.889)]
    public void SupportControl_Sweep_MatchesV31(double a1, double span, double qk, double qd,
                                                double expSigma, double expTau, double expEq,
                                                double expDf, double expLim)
    {
        var r = Run(a1, 600, span, qk, qd);
        Assert.False(r.SpanControl);
        Assert.Equal(expSigma, WindLoadEngine.VbaRound(r.Sigma, 3), 9);
        Assert.Equal(expTau, WindLoadEngine.VbaRound(r.Tau, 3), 9);
        Assert.Equal(expEq, WindLoadEngine.VbaRound(r.SigmaEq, 3), 9);
        Assert.Equal(expDf, r.Deflection, 9);
        Assert.Equal(expLim, WindLoadEngine.VbaRound(r.Limit, 3), 9);
    }

    /// <summary>
    /// A₁ ≠ A₂ 两组（800/600 与 700/800）：验证 num2 里 A₁ 与 A₂ 的混用
    /// 以及限值取 (A₁+L)/180。
    /// </summary>
    [Theory]
    [InlineData(800, 600, 2700, 1.390, 2.085, 114.898, 10.021, 116.202, 1.6645, 19.444)]
    [InlineData(700, 800, 2600, 1.390, 2.085, 95.372, 9.349, 96.737, 2.2935, 18.333)]
    public void DifferentCantilevers_MatchV31(double a1, double a2, double span,
                                              double qk, double qd,
                                              double expSigma, double expTau, double expEq,
                                              double expDf, double expLim)
    {
        var r = Run(a1, a2, span, qk, qd);
        Assert.False(r.SpanControl);
        Assert.Equal(expSigma, WindLoadEngine.VbaRound(r.Sigma, 3), 9);
        Assert.Equal(expTau, WindLoadEngine.VbaRound(r.Tau, 3), 9);
        Assert.Equal(expEq, WindLoadEngine.VbaRound(r.SigmaEq, 3), 9);
        Assert.Equal(expDf, r.Deflection, 9);
        Assert.Equal(expLim, WindLoadEngine.VbaRound(r.Limit, 3), 9);
    }

    /// <summary>
    /// 跨中控制分支（A=600/L=3000）：σ=|M'|max/W、V 走 |qd·L/2 + M'/L| 那条候选。
    /// </summary>
    [Fact]
    public void SpanControl_MatchesV31()
    {
        var r = Run(600, 600, 3000, 1.387, 2.080);
        Assert.True(r.SpanControl);
        Assert.Equal(1960400.0, r.MaxArrayMoment, 0);          // 数组 M' 实测 −1 960 400
        Assert.Equal(3267.3333, r.Shear, 3);                   // max(3267.33, 2466.53)
        Assert.Equal(91.385, WindLoadEngine.VbaRound(r.Sigma, 3), 9);
        Assert.Equal(8.363, WindLoadEngine.VbaRound(r.Tau, 3), 9);
        Assert.Equal(92.526, WindLoadEngine.VbaRound(r.SigmaEq, 3), 9);
        Assert.Equal(4.684, r.Deflection, 9);
        Assert.Equal(20.0, WindLoadEngine.VbaRound(r.Limit, 3), 9);
    }

    /// <summary>总跨数 n 不影响结果（3 步收敛，n=4 与 n=10 输出相同）。</summary>
    [Fact]
    public void SpanCount_DoesNotChangeResult()
    {
        var a = Run(600, 600, 2700, 1.394, 2.091);
        var b = IndeterminateColumnEngine.Compute(600, 600, 2700, 1.394, 2.091,
                                                  E, I, W, Sx, Tw, spans: 4);
        Assert.Equal(a.Sigma, b.Sigma, 12);
        Assert.Equal(a.Deflection, b.Deflection, 12);
    }
}
