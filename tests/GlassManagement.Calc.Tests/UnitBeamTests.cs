using GlassManagement.Calc.Data;
using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 单元式横梁（上横框 + 下横框）：期望值全部来自 V31 实机
/// （第一页「幕墙结构」选单元式 → 下一步 → 立柱 → 下一步 → 横梁 → 预演，无中横）。
/// 公式与定稿点地址见 <see cref="UnitBeamEngine"/> 注释与 docs/V31_BEAM_UI.md 第九节。
/// </summary>
public class UnitBeamTests
{
    /// <summary>参数库「单元横框一/二」40/60系列（2 位小数口径：Ix 11.58 → 115800）。</summary>
    private static readonly BeamProfile Top = new(
        "40/60系列", Ix: 115_800, Iy: 123_700,
        Wx1: 4_100, Wx2: 3_660, Wy1: 5_760, Wy2: 6_640,
        Sx: 2_994, Sy: 3_546, Tx: 5, Ty: 3, Area: 449.3);

    private static readonly BeamProfile Bottom = Top;

    /// <summary>参数库「幕墙自重参数」单玻璃幕墙：计算横梁时 300 N/m²。</summary>
    private static readonly double G = HorizontalBeamEngine.GravityFromLibrary(300);

    private const double AlphaBeijing = 0.16;
    private const double F = 90.0;
    private const double Fv = 55.0;

    [Fact]
    public void 参数库单元横框表_列位与横框表不同_不得错位()
    {
        // 「横框.csv」14 列（含连接面壁厚）；「单元横框一/二/三」13 列（无该列）。
        // 曾因按 14 列布局解析 13 列表，导致 Sx/Sy/tx/ty 全体错位、ty 读到备注(0) → τx 除零得 ∞。
        var top = ParamLibrary.UnitBeamTop;
        Assert.True(top.Count > 1, "未读到 单元横框一.csv（退化成内置快照），无法验证解析");
        var p = top[0];
        Assert.Equal("40/60系列", p.Code);
        Assert.Equal(115_800, p.Ix);      // 11.58 cm⁴（2 位小数，不是 115810）
        Assert.Equal(123_700, p.Iy, 6);   // 12.37×1e4 有浮点尾差
        Assert.Equal(3_660, p.WxBending, 6); // min(4100, 3660)
        Assert.Equal(2_994, p.Sx, 6);     // ★ 错位时会读到 Sy
        Assert.Equal(3_546, p.Sy, 6);
        Assert.Equal(5, p.Tx, 6);
        Assert.Equal(3, p.Ty, 6);         // ★ 错位时会读到备注 → 0 → 除零
    }

    [Fact]
    public void 参数库横框表_14列口径不变()
    {
        var p = ParamLibrary.FindBeamProfile("40/60系列")!;
        Assert.Equal(115_810, p.Ix, 6);   // 横框表 3 位小数
        Assert.Equal(123_650, p.Iy, 6);
        Assert.Equal(2_994, p.Sx, 6);
        Assert.Equal(3_546, p.Sy, 6);
        Assert.Equal(5, p.Tx, 6);
        Assert.Equal(3, p.Ty, 6);
    }

    [Fact]
    public void 单元式上下框_B1100_H1380_逐位()
    {
        // 实机（无中横）：上框 42.417 / 下框 20.184 / 水平挠度 0.996 / 垂直挠度 0.974
        //                上 τx 3.03 / 上 τy 1.53 / 下 τx 3.03 / 下 τy 0
        var r = UnitBeamEngine.Compute(1.285, 1100, 1380, G, AlphaBeijing, Top, Bottom);

        Assert.Equal(1.414, r.Qk);
        Assert.Equal(2.306, r.Qy);
        Assert.Equal(81372.5, r.Mx, 6);
        Assert.Equal(232521.667, r.My, 3);

        Assert.Equal(42.417, r.SigmaTop);
        Assert.Equal(20.184, r.SigmaBottom);
        Assert.Equal(317.075, r.Vh2, 6);
        Assert.Equal(295.9, r.Vv, 6);
        Assert.Equal(3.030, r.TauTopX);
        Assert.Equal(1.530, r.TauTopY);
        Assert.Equal(3.030, r.TauBottomX);
        Assert.Equal(0.0, r.TauBottomY);

        Assert.Equal(0.974, r.DeflectionV);
        Assert.Equal(0.996, r.DeflectionH);
        Assert.Equal(4.4, r.LimitV);
        Assert.Equal(6.111, r.LimitH);
        Assert.Equal(0.5, r.U, 12);
    }

    [Fact]
    public void 中横框_有中横时追加5个量_逐位()
    {
        // 实机（有中横，上分格 1380）：中横抗弯 62.147 / 水平挠度 1.992 / 垂直挠度 0.952
        //                             τx 6.059 / τy 1.497
        var mid = Top;   // 参数库「单元横框三（中横框）」40/60，字段与上框同值
        var r = UnitBeamEngine.Compute(1.285, 1100, 1380, G, AlphaBeijing, Top, Bottom, false, mid);

        Assert.Equal(62.147, r.MidSigma!.Value);
        Assert.Equal(1.992, r.MidDeflectionH!.Value);
        Assert.Equal(0.952, r.MidDeflectionV!.Value);
        Assert.Equal(6.059, r.MidTauX!.Value);
        Assert.Equal(1.497, r.MidTauY!.Value);
        Assert.Equal(79708.75, r.MidMx!.Value, 6);   // q_x(1350·g·1.3)=0.527 × B²/8
    }

    [Fact]
    public void 上分格高小于跨度_水平链仍按B_竖向链按H_逐位()
    {
        // ★ 2026-10-26 实机（单元式 + 有中横，上分格高 1000 < B=1100）：
        //   上框 36.301 / 下框 20.184 / df水平 0.996 / df垂直 0.706 / Mx 58987.5 / Vv 214.5
        //   τx 3.03 / τy上 1.109 / τy下 0 / 中横 62.147·1.992·0.952·6.059·1.497
        //   来源 local-data/recon/v31runs/py_u_mid.txt（RichText 原文）+ 槽数组 0x1C911A0 直读。
        //   关键：H<B 时 qk 仍是 1.414（B 口径），若按 min(B,H) 会得 1.285 → 全线偏移。
        var r = UnitBeamEngine.Compute(1.285, 1100, 1000, G, AlphaBeijing, Top, Bottom, false, Top);

        Assert.Equal(1.414, r.Qk);
        Assert.Equal(2.306, r.Qy);
        Assert.Equal(58_987.5, r.Mx, 6);
        Assert.Equal(232_521.667, r.My, 3);
        Assert.Equal(214.5, r.Vv, 6);
        Assert.Equal(317.075, r.Vh2, 6);

        Assert.Equal(36.301, r.SigmaTop);
        Assert.Equal(20.184, r.SigmaBottom);
        Assert.Equal(3.030, r.TauTopX);
        Assert.Equal(1.109, r.TauTopY);
        Assert.Equal(3.030, r.TauBottomX);
        Assert.Equal(0.0, r.TauBottomY);
        Assert.Equal(0.996, r.DeflectionH);
        Assert.Equal(0.706, r.DeflectionV);
        Assert.Equal(6.111, r.LimitH);
        Assert.Equal(4.4, r.LimitV);

        Assert.Equal(62.147, r.MidSigma!.Value);
        Assert.Equal(1.992, r.MidDeflectionH!.Value);
        Assert.Equal(0.952, r.MidDeflectionV!.Value);
        Assert.Equal(6.059, r.MidTauX!.Value);
        Assert.Equal(1.497, r.MidTauY!.Value);
    }

    [Fact]
    public void 中横自重高度是常量1350_不随界面尺寸变()
    {
        // 实测：上分格改 1500 / 1000，中横 5 个量全部不变；
        // 第一页第三组尺寸改 1500 也无效 ⇒ 常量 1350（0x96797A2 实参 1350×0.0003=0.405）
        var a = UnitBeamEngine.Compute(1.285, 1100, 1380, G, AlphaBeijing, Top, Bottom, false, Top);
        var b = UnitBeamEngine.Compute(1.285, 1100, 1500, G, AlphaBeijing, Top, Bottom, false, Top);
        var c = UnitBeamEngine.Compute(1.285, 1100, 1000, G, AlphaBeijing, Top, Bottom, false, Top);

        Assert.Equal(a.MidSigma, b.MidSigma);
        Assert.Equal(a.MidSigma, c.MidSigma);
        Assert.Equal(a.MidTauY, c.MidTauY);
        Assert.Equal(1350.0, UnitBeamEngine.MidGridHeightMm);
    }

    [Fact]
    public void 无中横时中横字段为空()
    {
        var r = UnitBeamEngine.Compute(1.285, 1100, 1380, G, AlphaBeijing, Top, Bottom);
        Assert.Null(r.MidSigma);
    }

    [Fact]
    public void 自重只进上框_下框不含竖向分量()
    {
        // 把上分格高翻倍：竖向弯矩/剪力/上框应力应随之变，下框应力完全不动
        var a = UnitBeamEngine.Compute(1.285, 1100, 1380, G, AlphaBeijing, Top, Bottom);
        var b = UnitBeamEngine.Compute(1.285, 1100, 2760, G, AlphaBeijing, Top, Bottom);

        Assert.NotEqual(a.SigmaTop, b.SigmaTop);
        Assert.Equal(a.SigmaBottom, b.SigmaBottom);   // 下框与 H 无关
        Assert.Equal(0.0, b.TauBottomY);
        Assert.True(b.Vv > a.Vv);
    }

    [Fact]
    public void 水平挠度用上下框惯性矩相加()
    {
        // df_h = qk·B⁴/(120·E·(Iy上+Iy下))；若只用单框会差一倍
        var r = UnitBeamEngine.Compute(1.285, 1100, 1380, G, AlphaBeijing, Top, Bottom);
        double manual = WindLoadEngine.VbaRound(
            1.414 * Math.Pow(1100, 4) / 120.0 / (70000.0 * (123_700 + 123_700)), 3);
        Assert.Equal(manual, r.DeflectionH);
        Assert.Equal(0.996, r.DeflectionH);
    }

    [Fact]
    public void 不考虑地震时水平弯矩减半项消失()
    {
        // 复刻版修正：V31 勾「计算中不考虑地震作用」横梁不变（bug），此处 kq=0
        var withQ = UnitBeamEngine.Compute(1.285, 1100, 1380, G, AlphaBeijing, Top, Bottom);
        var noQ = UnitBeamEngine.Compute(1.285, 1100, 1380, G, AlphaBeijing, Top, Bottom, ignoreSeismic: true);

        Assert.Equal(2.306, withQ.Qy);
        Assert.Equal(2.121, noQ.Qy);                 // 只剩风荷载 qd
        Assert.True(noQ.My < withQ.My);
        Assert.True(noQ.SigmaTop < withQ.SigmaTop);
        Assert.Equal(withQ.SigmaBottom - 0.0, withQ.SigmaBottom);   // 占位：确保字段可读
        Assert.True(noQ.SigmaBottom < withQ.SigmaBottom);
    }
}
