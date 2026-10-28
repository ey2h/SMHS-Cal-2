using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 板材线「板块计算」屏（人造板材 · 短挂件连接）——逐位对拍。
///
/// <para><b>期望值来源</b>：V31 实机 RichText 原文（基线，瓷板·短挂件连接，
/// <c>v31runs/chain_manmade_ci.txt</c> 第 4 屏）＋ 15 组 r 值扰动直读 m 插值局部变量
/// <c>v31runs/manmade_mtable.json</c> ＋ Frida 抓的 92 条 Variant 实值轨迹
/// <c>v31runs/manmade_ci_var.json</c>（26 个 rtcRound 点）＋ 常量池原文。
/// 算式与地址见 <c>docs/V31_MANMADE_PLATE_UI.md</c>。</para>
///
/// <para>基线（瓷板）关键值：wk=1.37KPa、r=0.722、m=0.1389、σ=2.575、τ=3.193、
/// 水平挂件抗剪=4.47、自重=423N、自重挂件抗剪=1.146、挂件抗弯=73.149、挂件挠度=0.103、
/// 螺栓抗拉=274.716、螺栓抗剪=1072.8、复合=0.045784。</para>
/// </summary>
public class ManmadePlateEngineTests
{
    private const double Wk = 0.00137;      // 面板口径（μsl=1.0，面板部分风荷载标准值 1.37 KPa）
    private const double AlphaMax = 0.16;   // 参数库「地区参数」北京 8 度

    private static ManmadePlateEngine.Input Base() => new()
    {
        Species = "瓷板",
        ThicknessMm = 25,
        ShortSideMm = 800,
        LongSideMm = 900,
        SmallSupportMm = 650,
        LargeSupportMm = 900,
        AllowableBendingMpa = 15,
        AllowableShearMpa = 7.5,
        SlotMouthMm = 3,
        SlotShearLengthMm = 56,
        HangerTotalCount = 4,
        HangerAllowableShearMpa = 100,
        HangerShearAreaMm2 = 240,
        HangerWidthMm = 80,
        HangerThicknessMm = 3,
        HangerEccentricityMm = 30,
        HangerLengthMm = 60,
        HangerMaterial = "316不锈钢",
        HorizontalHangerCount = 2,
        LoadHangerCount = 2,
        BoltPerHanger = 1,
        BoltMaterial = "碳钢：4.6-C级",
        BoltDiameterMm = 8,
        AlphaMax = AlphaMax,
        Wk = Wk,
    };

    [Fact]
    public void Baseline_CiPlate_MatchesAllNineGroups()
    {
        var r = ManmadePlateEngine.Compute(Base());

        // 荷载
        Assert.Equal(1.37, r.WindStandardKpa);
        Assert.Equal(23.5, r.DensityKnM3);
        Assert.Equal(0.00047, r.SeismicMpa);
        Assert.Equal(0.002384, r.Qd);

        // 比值 + m
        Assert.Equal(0.722, r.Ratio);
        Assert.Equal(0.1389, r.MCoef);

        // 抗弯
        Assert.Equal(2.575, r.StressComputed);
        Assert.Equal(15, r.StressAllowable);
        Assert.True(r.StressOk);

        // 抗剪
        Assert.Equal(3.193, r.ShearComputed);
        Assert.Equal(7.5, r.ShearAllowable);
        Assert.True(r.ShearOk);

        // 水平挂件抗剪
        Assert.Equal(4.47, r.HangerShearHorizontal);
        Assert.Equal(100, r.HangerShearAllowable);
        Assert.True(r.HangerShearHorizontalOk);

        // 自重 + 自重挂件抗剪
        Assert.Equal(423, r.SelfWeightN);
        Assert.Equal(1.146, r.HangerShearVertical);
        Assert.True(r.HangerShearVerticalOk);

        // 挂件抗弯
        Assert.Equal(73.149, r.HangerStressComputed);
        Assert.Equal(180, r.HangerStressAllowable);
        Assert.True(r.HangerStressOk);

        // 挂件挠度
        Assert.Equal(0.103, r.HangerDeflectionComputed);
        Assert.Equal(0.24, r.HangerDeflectionAllowable);
        Assert.True(r.HangerDeflectionOk);

        // 螺栓
        Assert.Equal(274.716, r.BoltTensionComputed);
        Assert.Equal(6222, r.BoltTensionAllowable);
        Assert.True(r.BoltTensionOk);
        Assert.Equal(1072.8, r.BoltShearComputed);
        Assert.Equal(5124, r.BoltShearAllowable);
        Assert.True(r.BoltShearOk);
        Assert.Equal(0.045784, r.BoltCombined);
        Assert.True(r.BoltCombinedOk);
    }

    [Fact]
    public void MTable_SevenRValues_MatchPerturbation()
    {
        // 扰动直读 m 插值局部变量（manmade_mtable.json），逐点验证 m(r)
        Assert.Equal(0.125, ManmadePlateEngine.Compute(With(r => r.SmallSupportMm = 200, r => r.LargeSupportMm = 2000)).MCoef);  // r=0.1
        Assert.Equal(0.126, ManmadePlateEngine.Compute(With(r => r.SmallSupportMm = 400, r => r.LargeSupportMm = 2000)).MCoef);  // r=0.2
        Assert.Equal(0.13,  ManmadePlateEngine.Compute(With(r => r.SmallSupportMm = 1000, r => r.LargeSupportMm = 2000)).MCoef);  // r=0.5
        Assert.Equal(0.138, ManmadePlateEngine.Compute(With(r => r.SmallSupportMm = 1400, r => r.LargeSupportMm = 2000)).MCoef);  // r=0.7
        Assert.Equal(0.14,  ManmadePlateEngine.Compute(With(r => r.SmallSupportMm = 1500, r => r.LargeSupportMm = 2000)).MCoef);  // r=0.75
        Assert.Equal(0.147, ManmadePlateEngine.Compute(With(r => r.SmallSupportMm = 1800, r => r.LargeSupportMm = 2000)).MCoef);  // r=0.9
        Assert.Equal(0.152, ManmadePlateEngine.Compute(With(r => r.SmallSupportMm = 2000, r => r.LargeSupportMm = 2000)).MCoef);  // r=1.0
    }

    [Fact]
    public void Density_BySpecies()
    {
        Assert.Equal(23.5, ManmadePlateEngine.DensityOf("瓷板"));
        Assert.Equal(24.0, ManmadePlateEngine.DensityOf("陶板"));
        Assert.Equal(27.0, ManmadePlateEngine.DensityOf("微晶玻璃板"));
        Assert.Equal(23.5, ManmadePlateEngine.DensityOf("未知"));
    }

    [Fact]
    public void TaoPlate_Defaults_UseDensity24()
    {
        // 陶板：重度 24、抗弯 10、抗剪 2（实机 manmade_tao 取证）
        var it = Base();
        it.Species = "陶板";
        it.AllowableBendingMpa = 10;
        it.AllowableShearMpa = 2;

        var r = ManmadePlateEngine.Compute(it);
        Assert.Equal(24.0, r.DensityKnM3);

        // 陶板自重 G = 24·800·900·25/1e6 = 432
        Assert.Equal(432, r.SelfWeightN);
    }

    private static ManmadePlateEngine.Input With(params Action<ManmadePlateEngine.Input>[] mut)
    {
        var it = Base();
        foreach (var m in mut) m(it);
        return it;
    }
}
