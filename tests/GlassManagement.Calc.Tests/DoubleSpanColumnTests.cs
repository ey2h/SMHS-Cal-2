using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 双跨梁引擎回归：期望值全部来自 V31 实机（Frida 定稿点 + 槽快照），不是计算书。
///
/// <para>实机参数：wk=1.285KPa（slot9=0.001285 N/mm²）、bay=1100mm → qk=1.414、qd=2.121；
/// E=70000MPa、I=885950mm⁴、W=21452mm³、[σ]=135MPa；L1=3000。</para>
/// </summary>
public class DoubleSpanColumnTests
{
    private const double Qk = 1.414;
    private const double Qd = 2.121;
    private const double E = 70000.0;
    private const double I = 885950.0;
    private const double W = 21452.0;
    private const double SigmaAllow = 135.0;

    [Fact]
    public void EqualSpans_AutoShiftedTo1499_1501()
    {
        // V31 实测：L2=1500（等跨）时程序自动改成 a=1499 / b=1501（用户目视确认 + 槽 slot3/slot4）
        var (a, b) = DoubleSpanColumnEngine.Spans(3000.0, 1500.0);
        Assert.Equal(1499.0, a, 9);
        Assert.Equal(1501.0, b, 9);
    }

    [Fact]
    public void UnequalSpans_Unchanged()
    {
        var (a, b) = DoubleSpanColumnEngine.Spans(3000.0, 1200.0);
        Assert.Equal(1200.0, a, 9);
        Assert.Equal(1800.0, b, 9);
    }

    [Fact]
    public void EqualCase_MatchesV31()
    {
        var r = DoubleSpanColumnEngine.Compute(3000.0, 1500.0, Qk, Qd, E, I, SigmaAllow);

        Assert.Equal(596532.045, r.Moment, 6);          // V31 定稿点 0xD3AE9C 入参 -596532.045375
        Assert.Equal(1501.0, r.Lc, 9);
        Assert.Equal(8.3388888888888886, r.Limit, 12);  // = 1501/180
        Assert.Equal(66756.46614604564, r.RequiredI, 6);   // V31 0xD3E95A
        Assert.Equal(4418.755888888889, r.RequiredW, 6);   // V31 0xD3EAC3
        Assert.Equal(0.6283449079427447, r.Deflection, 12); // V31 0xD413D0
        Assert.Equal(0.499, r.K1, 9);                    // slot[22]
        Assert.Equal(0.053, r.K4, 9);                    // slot[24]
        Assert.Equal(0.185, r.K6, 9);                    // slot[26]
        Assert.Equal(26.46, WindLoadEngine.VbaRound(r.Theta, 3), 9);  // slot[25]
        Assert.Equal(0.578, r.K11, 9);
        Assert.Equal(0.1303, r.Kf, 9);

        // 抗弯/抗剪：与 V31 打印值一致
        double sigma = WindLoadEngine.VbaRound(r.Moment / W, 3);
        Assert.Equal(27.808, sigma, 9);                  // V31 0xD3FFF6 → 27.808
        double tau = WindLoadEngine.VbaRound(13606.0 * WindLoadEngine.VbaRound(r.Shear, 3) / I / 6.0, 3);
        Assert.Equal(5.092, tau, 9);                     // V31 0xD40A76 → 5.092
    }

    [Fact]
    public void UnequalCase_MatchesV31()
    {
        var r = DoubleSpanColumnEngine.Compute(3000.0, 1200.0, Qk, Qd, E, I, SigmaAllow);

        Assert.Equal(668115.0, r.Moment, 6);             // V31 0xD3AE9C 入参 -668115（恰好整数）
        Assert.Equal(1800.0, r.Lc, 9);
        Assert.Equal(10.0, r.Limit, 12);                 // = 1800/180
        Assert.Equal(149496.3216, r.RequiredI, 6);       // V31 0xD3E95A
        Assert.Equal(4949.0, r.RequiredW, 6);            // V31 0xD3EAC3
        Assert.Equal(1.6874126259946949, r.Deflection, 12); // V31 0xD413D0
        Assert.Equal(0.389, r.K1, 9);
        Assert.Equal(0.065, r.K4, 9);
        Assert.Equal(0.17, r.K6, 9);                     // round3(0.1699369877)
        Assert.Equal(26.751, WindLoadEngine.VbaRound(r.Theta, 3), 9);
        Assert.Equal(0.552, r.K11, 9);
        Assert.Equal(0.1692, r.Kf, 9);

        double sigma = WindLoadEngine.VbaRound(r.Moment / W, 3);
        Assert.Equal(31.145, sigma, 9);                  // V31 0xD3FFF6 → 31.145
        double tau = WindLoadEngine.VbaRound(13606.0 * WindLoadEngine.VbaRound(r.Shear, 3) / I / 6.0, 3);
        Assert.Equal(5.836, tau, 9);                     // V31 0xD40A76 → 5.836
    }

    /// <summary>
    /// 换型材：I/W 走参数库（立柱.csv），预选量（w_pre/i_pre）与型材无关、强度/挠度随型材变。
    /// 期望值来自 V31 实机（双跨 3000/1500、wk=1.285）：60/100系列 df=0.363 σ=20.215 τ=4.076、
    /// 60/125系列 df=0.21 σ=14.583 τ=3.276。
    /// </summary>
    [Theory]
    // I[mm4], W[mm3], S[mm3], t[mm], 期望 df, 期望 sigma, 期望 tau
    [InlineData(1532970.0, 29510.0, 18846.0, 6.0, 0.363, 20.215, 4.076)]
    [InlineData(2656350.0, 40905.0, 26247.0, 6.0, 0.21, 14.583, 3.276)]
    public void DifferentProfiles_MatchV31(double iMm4, double wMm3, double sMm3, double tw,
                                           double expDf, double expSigma, double expTau)
    {
        var r = DoubleSpanColumnEngine.Compute(3000.0, 1500.0, Qk, Qd, E, iMm4, SigmaAllow);
        Assert.Equal(expDf, WindLoadEngine.VbaRound(r.Deflection, 3), 9);
        Assert.Equal(expSigma, WindLoadEngine.VbaRound(r.Moment / wMm3, 3), 9);
        double tau = WindLoadEngine.VbaRound(sMm3 * WindLoadEngine.VbaRound(r.Shear, 3) / iMm4 / tw, 3);
        Assert.Equal(expTau, tau, 9);
        // 预选量与型材无关（V31 实机同值）
        Assert.Equal(66756.46614604564, r.RequiredI, 6);
        Assert.Equal(4418.755888888889, r.RequiredW, 6);
    }
}
