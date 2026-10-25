using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 幕墙横梁：期望值全部来自 V31 实机
/// （第一页改分格尺寸 → 下一步 → 立柱 → 下一步 → 横梁 → 选 40/60 系列 → 预演）。
/// 公式与地址见 <see cref="HorizontalBeamEngine"/> 注释与 docs/V31_BEAM_UI.md。
/// </summary>
public class HorizontalBeamTests
{
    /// <summary>参数库「横框」40/60系列（cm 单位已换算成 mm）。</summary>
    private static readonly BeamProfile P4060 = new(
        "40/60系列",
        Ix: 115_810, Iy: 123_650,
        Wx1: 4_100, Wx2: 3_662,
        Wy1: 5_763, Wy2: 6_639,
        Sx: 2_994, Sy: 3_546,
        Tx: 5, Ty: 3,
        Area: 449.3);

    /// <summary>参数库「幕墙自重参数」第 1 行「单玻璃幕墙」计算横梁时自重 300 N/m²。</summary>
    private static readonly double G = HorizontalBeamEngine.GravityFromLibrary(300);

    private const double F = 90.0;   // 抗弯强度设计值 MPa

    private static BeamResult Run(double wk, double B, double H)
        => HorizontalBeamEngine.Compute(wk, B, H, G, F, P4060);

    [Fact]
    public void 三角形分布_基线_1100x1380()
    {
        // r = min(1100,1380) = 1100 = B → u = 0.5 → 弯矩系数退化为 1/12、挠度 1/120
        var r = Run(1.285, 1100, 1380);

        Assert.Equal(904.139, R3(r.WxRequired));
        Assert.Equal(2583.574, R3(r.WyRequired));
        Assert.Equal(25624.721, R3(r.IxRequired));
        Assert.Equal(40330.033, R3(r.IyRequired));
        Assert.Equal(0.974, R3(r.DeflectionV));
        Assert.Equal(1.993, R3(r.DeflectionH));
        Assert.Equal(4.4, R3(r.LimitV));
        Assert.Equal(6.111, r.LimitH);
        Assert.Equal(62.568, r.Sigma);
        Assert.Equal(6.061, r.TauH);      // V31 打印标签 tx
        Assert.Equal(1.531, r.TauV);      // V31 打印标签 ty
        Assert.Equal(0.5, r.U, 12);
    }

    [Fact]
    public void 梯形分布_1500x1000()
    {
        var r = Run(1.264, 1500, 1000);

        Assert.Equal(1.264, r.Qk);
        Assert.Equal(2.064, r.Qy);        // 1.896 + 0.168
        Assert.Equal(494500.0, r.My, 0);  // 2.064×1500²×(3−4/9)/24
        Assert.Equal(1218.75, R3(r.WxRequired));
        Assert.Equal(5494.444, R3(r.WyRequired));
        Assert.Equal(47084.263, R3(r.IxRequired));
        Assert.Equal(118575.279, R3(r.IyRequired));
        Assert.Equal(2.439, R3(r.DeflectionV));
        Assert.Equal(7.991, R3(r.DeflectionH));
        Assert.Equal(6.0, R3(r.LimitV));
        Assert.Equal(8.333, r.LimitH);
        Assert.Equal(115.759, r.Sigma);
        Assert.Equal(9.865, r.TauH);
        Assert.Equal(1.512, r.TauV);
        Assert.Equal(1.0 / 3.0, r.U, 12);
    }

    [Fact]
    public void 梯形分布_2000x800_qy需要二次取整()
    {
        // 1.491 + 0.1345 = 1.6255 → round3 → 1.626（用 1.6255 会让 Wy 预选差 2.6）
        var r = Run(1.243, 2000, 800);

        Assert.Equal(0.994, r.Qk);
        Assert.Equal(1.626, r.Qy);
        Assert.Equal(1733.333, R3(r.WxRequired));
        Assert.Equal(8551.556, R3(r.WyRequired));
        Assert.Equal(89285.714, R3(r.IxRequired));
        Assert.Equal(249485.135, R3(r.IyRequired));
        Assert.Equal(6.168, R3(r.DeflectionV));
        Assert.Equal(22.418, R3(r.DeflectionH));
        Assert.Equal(8.0, R3(r.LimitV));
        Assert.Equal(11.111, r.LimitH);
        Assert.Equal(176.148, r.Sigma);
    }

    [Fact]
    public void 中间比_1200x1000()
    {
        // 插值点，验证 (3−r²)/24 与 (3.125−5u²+2u⁴)/240 不是只在两端吻合
        var r = Run(1.28, 1200, 1000);

        Assert.Equal(780.0, R3(r.WxRequired));
        Assert.Equal(3209.333, R3(r.WyRequired));
        Assert.Equal(24107.143, R3(r.IxRequired));
        Assert.Equal(54911.54, R3(r.IyRequired));
        Assert.Equal(0.999, R3(r.DeflectionV));
        Assert.Equal(2.961, R3(r.DeflectionH));
        Assert.Equal(4.8, R3(r.LimitV));
        Assert.Equal(6.667, r.LimitH);
        Assert.Equal(69.29, r.Sigma);
        Assert.Equal(6.986, r.TauH);
        Assert.Equal(1.21, r.TauV);
    }

    [Fact]
    public void 临界_1000x1000_B等于H退化为简支系数()
    {
        var r = Run(1.292, 1000, 1000);

        Assert.Equal(0.5, r.U, 12);
        Assert.Equal(541.667, R3(r.WxRequired));
        Assert.Equal(1950.0, R3(r.WyRequired));
        Assert.Equal(13950.893, R3(r.IxRequired));
        Assert.Equal(27683.5, R3(r.IyRequired));
        Assert.Equal(0.482, R3(r.DeflectionV));
        Assert.Equal(1.244, R3(r.DeflectionH));
        Assert.Equal(4.0, R3(r.LimitV));
        Assert.Equal(5.556, r.LimitH);
        Assert.Equal(43.765, r.Sigma);
        Assert.Equal(5.033, r.TauH);
        Assert.Equal(1.008, r.TauV);
    }

    [Fact]
    public void 抵抗矩取min_双向各自()
    {
        // 40/60：Wx min(4100,3662)=3662、Wy min(5763,6639)=5763
        Assert.Equal(3_662, P4060.WxBending);
        Assert.Equal(5_763, P4060.WyBending);
    }

    [Fact]
    public void 弯矩系数_两端极限()
    {
        // r=B（三角形）→ 1/12；r→0（均布）→ 1/8
        var tri = Run(1.285, 1100, 1380);
        Assert.Equal(1.0 / 12.0, tri.My / (tri.Qy * 1100 * 1100), 9);

        var flat = Run(1.243, 2000, 40);   // H 极小，接近均布（仅验证系数趋势，不比对 V31）
        Assert.True(flat.My / (flat.Qy * 2000 * 2000) > 0.124);
    }

    private static double R3(double v) => WindLoadEngine.VbaRound(v, 3);
}
