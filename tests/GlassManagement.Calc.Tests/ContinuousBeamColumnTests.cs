using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 连续梁（四点三跨）引擎回归 —— 期望值全部来自 V31 实机
/// （函数 0xD42762，Frida 读 Variant 运算实参 + rtcRound 定稿点）。
///
/// <para>公共参数：wk=1.285KPa（→ qk_raw=1.4135、qk=1.414、qd=2.121）、bay=1100、
/// E=70000、I=885950、W=21452、S=13606、t=6、[σ]=135。</para>
/// </summary>
public class ContinuousBeamColumnTests
{
    private const double QkRaw = 1.4135;
    private const double Qk = 1.414;
    private const double Qd = 2.121;
    private const double E = 70000.0;
    private const double I = 885950.0;
    private const double W = 21452.0;
    private const double SigmaAllow = 135.0;

    [Fact]
    public void L3000_MatchesV31()
    {
        var r = ContinuousBeamColumnEngine.Compute(3000.0, QkRaw, Qk, Qd, E, I, SigmaAllow);

        Assert.Equal(1908900.0, r.Moment, 6);                 // 0xD475F9：0.1·qd·L²
        Assert.Equal(14140.0, r.RequiredW, 6);                // 0xD47762
        Assert.Equal(664611.147777, r.RequiredI, 6);          // 0xD49F39
        Assert.Equal(12.50304644731644, r.Deflection, 10);    // 0xD4C80A
        Assert.Equal(16.666666666666668, r.Limit, 12);        // 0xD48151

        // σ = M/W；τ = S·V/(I·t)，V = 0.6·qk_raw·L·1.5 = 3816.45（0xD4B9D1）
        Assert.Equal(3816.45, r.Shear, 6);
        Assert.Equal(88.985, WindLoadEngine.VbaRound(r.Moment / W, 3), 9);          // 0xD4B3F1
        Assert.Equal(9.769, WindLoadEngine.VbaRound(13606.0 * r.Shear / I / 6.0, 3), 9); // 0xD4BF00
    }

    [Fact]
    public void L4000_LimitCappedAt20()
    {
        // 4000/180 = 22.222 → 被 20 上限压住（V31 实机 lim=20）
        double qk = WindLoadEngine.VbaRound(1.392, 3);
        var r = ContinuousBeamColumnEngine.Compute(4000.0, 1.3915, qk, 2.088, E, I, SigmaAllow);
        Assert.Equal(20.0, r.Limit, 12);
        Assert.Equal(24746.667, WindLoadEngine.VbaRound(r.RequiredW, 3), 9);   // V31 mat_m3.csv（显示值）
        Assert.Equal(38.901, WindLoadEngine.VbaRound(r.Deflection, 3), 9);
        Assert.Equal(12.822, WindLoadEngine.VbaRound(13606.0 * r.Shear / I / 6.0, 3), 9);
    }
}
