using System;
using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// rtcRound 等价性测试（2026-10-25 实机定案）。
///
/// 期望值不是"算出来的近似"，而是 **Frida 直接调用 V31 的 0x41371A 读回的 VT_R8 位模式**
/// （见 local-data/recon/v31_frida_round.py / v31_frida_chain.py），逐位相等。
/// </summary>
public class VbaRoundTests
{
    [Theory]
    // 四个 qd 平局点：入参 = qk(double)×1.5，期望 = V31 实机返回值（位模式全等）
    [InlineData(3.0495000000000001, 3.0499999999999998)]   // B01 → 显示 3.050
    [InlineData(4.0514999999999999, 4.0519999999999996)]   // B08 → 显示 4.052
    [InlineData(4.1325000000000003, 4.1319999999999997)]   // B12 → 显示 4.132
    [InlineData(2.0474999999999999, 2.0470000000000002)]   // B10 → 显示 2.047
    public void QdTieCases_MatchV31BitForBit(double input, double expected)
    {
        double got = WindLoadEngine.VbaRound(input, 3);
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(got));
    }

    [Theory]
    // qk 链：rtcRound(raw,3) 的返回位模式（V31 实机）
    [InlineData(2.0329067, 2033)]
    [InlineData(2.7009134, 2701)]
    [InlineData(2.7550760, 2755)]
    [InlineData(1.36496, 1365)]
    public void QkRound_ReturnsSameBitsAsV31(double raw, long milliScaled)
    {
        double got = WindLoadEngine.VbaRound(raw, 3);
        double expect = milliScaled / 1000.0;   // V31 是 n/1000.0（除法），不是 n*0.001
        Assert.Equal(BitConverter.DoubleToInt64Bits(expect), BitConverter.DoubleToInt64Bits(got));
    }

    [Fact]
    public void WholeChain_ReproducesV31Qd()
    {
        // B01/B08/B12/B10 的 raw → qk → qd，全链复现
        var cases = new (double raw, double qd)[]
        {
            (2.0329067, 3.0499999999999998),
            (2.7009134, 4.0519999999999996),
            (2.7550760, 4.1319999999999997),
            (1.36496,   2.0470000000000002),
        };
        foreach (var (raw, qd) in cases)
        {
            double qk = WindLoadEngine.VbaRound(raw, 3);
            double got = WindLoadEngine.VbaRound(qk * 1.5, 3);
            Assert.Equal(BitConverter.DoubleToInt64Bits(qd), BitConverter.DoubleToInt64Bits(got));
        }
    }
}
