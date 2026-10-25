using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 钢铝结合立柱：期望值全部来自 V31 实机
/// （第一页 → 力学模型勾「钢铝结合」→ 下一步 → 选型材 → 预演）。
/// 公式出处见 <see cref="AlloyColumnEngine"/> 注释与 docs/V31_ALLOY_CODE.md。
/// </summary>
public class AlloyColumnTests
{
    // 参数库「钢铝结合铝立柱」6 条（cm 单位已换算成 mm）
    private static AlloyProfile Al(string code) => code switch
    {
        "60/80" => new(code, 885_950, 500_880, 21_452, 22_883, 3, 991.5, 13_606, 6),
        "60/100" => new(code, 1_532_970, 598_440, 29_510, 31_843, 3, 1111.5, 18_846, 6),
        "60/125" => new(code, 2_656_350, 720_390, 40_905, 44_216, 3, 1261.5, 26_247, 6),
        "60/140" => new(code, 3_516_290, 793_560, 48_355, 52_250, 3, 1351.15, 31_139, 6),
        "60/150" => new(code, 4_173_330, 842_340, 53_583, 57_857, 3, 1411.5, 34_589, 6),
        "60/200" => new(code, 8_384_470, 1_081_680, 85_746, 82_026, 3, 1686.9, 53_034, 6),
        _ => throw new System.ArgumentOutOfRangeException(nameof(code)),
    };

    // 参数库「钢铝结合钢立柱」抽查 4 条
    private static AlloyProfile St(string code) => code switch
    {
        "槽钢［5" => new(code, 260_000, 83_000, 10_400, 10_400, 4.5, 500, 6_400, 4.5),
        "槽钢［6.3" => new(code, 508_000, 119_000, 16_100, 16_100, 4.8, 845.1, 9_800, 4.8),
        "槽钢［8" => new(code, 1_010_000, 166_000, 25_300, 25_300, 5, 1024.8, 15_100, 5),
        "槽钢［12.6" => new(code, 3_910_000, 380_000, 62_100, 62_100, 5.5, 1569.2, 36_400, 5.5),
        _ => throw new System.ArgumentOutOfRangeException(nameof(code)),
    };

    private const double Bay = 1100;

    private static AlloyColumnResult Run(double wk, double L, string al, string st)
        => AlloyColumnEngine.Compute(wk, Bay, L, Al(al), St(st));

    [Theory]
    // wk 由第一页算得；改 L 会改 z，wk 随之变（实测值）
    [InlineData(1.285, 3000, "60/100", "槽钢［8", 28.897, 59.246, 4.963, 2.330, 6.275, 10)]
    [InlineData(1.285, 3000, "60/80", "槽钢［8", 26.798, 69.198, 5.799, 1.962, 7.329, 10)]
    [InlineData(1.285, 3000, "60/140", "槽钢［8", 28.081, 41.163, 3.449, 2.672, 4.360, 10)]
    [InlineData(1.285, 3000, "60/200", "槽钢［8", 22.548, 23.504, 1.970, 2.600, 2.489, 10)]
    [InlineData(1.285, 3000, "60/100", "槽钢［5", 56.650, 72.734, 9.730, 4.567, 5.793, 10)]
    [InlineData(1.285, 3000, "60/100", "槽钢［6.3", 42.964, 69.676, 7.391, 3.464, 6.312, 10)]
    [InlineData(1.285, 3000, "60/100", "槽钢［12.6", 9.988, 32.298, 1.720, 0.805, 4.753, 10)]
    [InlineData(1.298, 2500, "60/100", "槽钢［8", 20.253, 41.555, 2.417, 1.959, 5.281, 8.333)]
    [InlineData(1.274, 3500, "60/100", "槽钢［8", 38.969, 79.949, 9.123, 2.693, 7.258, 11.667)]
    [InlineData(1.216, 8000, "60/100", "槽钢［8", 194.375, 398.720, 237.572, 5.876, 15.836, 16)]
    public void MatchesV31(double wk, double L, string al, string st,
                           double sigAl, double sigSt, double df, double tauAl, double tauSt, double lim)
    {
        var r = Run(wk, L, al, st);
        Assert.Equal(sigAl, r.SigmaAl, 9);
        Assert.Equal(sigSt, r.SigmaSt, 9);
        Assert.Equal(df, r.Deflection, 9);
        Assert.Equal(tauAl, r.TauAl, 9);
        Assert.Equal(tauSt, r.TauSt, 9);
        Assert.Equal(lim, r.Limit, 9);
    }

    /// <summary>中间量也要钉住，否则只是「结果对上」而不是「公式对上」。</summary>
    [Fact]
    public void IntermediateTerms_60_100_x_8_L3000()
    {
        var r = Run(1.285, 3000, "60/100", "槽钢［8");
        Assert.Equal(1.414, r.Qk, 9);
        Assert.Equal(2.121, r.Qd, 9);
        Assert.Equal(0.5051878746378436, r.RatioAlStd, 9);   // Frida 0xCE2D69 实读
        Assert.Equal(0.7577818119567654, r.RatioAlDes, 9);   // 0xCE31BD
        Assert.Equal(1.399303036231652, r.RatioStDes, 9);    // 0xCE33D2
        Assert.Equal(852_750.0, r.MAl, 6);
        Assert.Equal(1_498_928.5714285714, r.MSt, 3);
        Assert.Equal(1137.0, r.VAl, 9);
        Assert.Equal(2098.5, r.VSt, 9);
    }

    /// <summary>L&gt;7500 走 L/500 分支。</summary>
    [Fact]
    public void Limit_SwitchAt7500()
    {
        Assert.Equal(15.0, AlloyColumnEngine.LimitMm(4500));    // min(15, 15)
        Assert.Equal(15.0, AlloyColumnEngine.LimitMm(7500));    // min(25, 15)
        Assert.Equal(16.0, AlloyColumnEngine.LimitMm(8000));    // 8000/500
    }
}
