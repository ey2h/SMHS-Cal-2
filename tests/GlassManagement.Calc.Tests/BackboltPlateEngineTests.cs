using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 背栓式点挂幕墙板块计算引擎逐位对拍。
///
/// <para><b>实机基线</b>（2026-10-26，V31「背栓式点挂幕墙&gt;花岗岩 → 板块计算」原文，
/// <c>local-data/recon/v31runs/backbolt_granite_var.json</c>）：</para>
/// <para>默认参数 短边 800×长边 900、大支承点 800、小支承点 650、t=25、钻孔深 6、拓孔 15、
/// 背栓数 4、N=8000、αmax=0.16、γ=28（石材），wk=1.37KPa 时，输出为</para>
/// <code>
/// 石材抗弯强度计算值:2.175MPa（设计 3.72）
/// 背栓在面板槽口中剪应力计算值:2.922MPa（设计 1.86，不满足 ×）
/// 背栓抗拉计算值:550.575N（设计 3720.93）
/// 背栓抗剪计算值:327.6N（设计 2976.744）
/// 挂件抗弯强度计算值:86.488MPa（设计 180）
/// 挂件挠度计算值:0.122mm（设计 0.24）
/// 螺栓抗拉计算值:327.6N（设计 6222）
/// 螺栓抗剪计算值:1101.15N（设计 5124）
/// 拉剪复合:0.048954
/// </code>
/// </summary>

public class BackboltPlateEngineTests
{
    private static BackboltPlateEngine.Input DefaultInput() => new()
    {
        Species = "石材",
        ThicknessMm = 25,
        CalcThicknessMm = 25,
        ShortSideMm = 800,
        LongSideMm = 900,
        SmallSupportMm = 650,
        LargeSupportMm = 800,
        DrillDepthMm = 6,
        HoleDiameterMm = 15,
        AllowableBendingMpa = 3.72,
        AllowableShearMpa = 1.86,
        BackboltTensionCapacityN = 8000,
        BackboltTotalCount = 4,
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
        AlphaMax = 0.16,
        Wk = 0.00137,
    };

    [Fact]
    public void 花岗岩_默认参数_逐位一致()
    {
        var r = BackboltPlateEngine.Compute(DefaultInput());

        Assert.Equal(1.37, r.WindStandardKpa);

        Assert.Equal(28.0, r.DensityKnM3);
        Assert.Equal(0.00056, r.SeismicMpa);
        Assert.Equal(0.002447, r.Qd);
        Assert.Equal(0.812, r.Ratio);
        Assert.Equal(0.1447, r.MCoef);

        Assert.Equal(2.175, r.StressComputed);
        Assert.Equal(3.72, r.StressAllowable);
        Assert.True(r.StressOk);

        Assert.Equal(2.922, r.SlotShearComputed);
        Assert.Equal(1.86, r.SlotShearAllowable);
        Assert.False(r.SlotShearOk);   // 2.922 > 1.86，不满足

        Assert.Equal(550.575, r.BackboltTensionComputed);
        Assert.Equal(3720.93, r.BackboltTensionAllowable);
        Assert.True(r.BackboltTensionOk);

        Assert.Equal(504, r.SelfWeightN);
        Assert.Equal(327.6, r.BackboltShearN);
        Assert.Equal(2976.744, r.BackboltShearAllowable);
        Assert.True(r.BackboltShearOk);

        Assert.Equal(86.488, r.HangerStressComputed);
        Assert.Equal(180, r.HangerStressAllowable);
        Assert.True(r.HangerStressOk);

        Assert.Equal(0.122, r.HangerDeflectionComputed);
        Assert.Equal(0.24, r.HangerDeflectionAllowable);
        Assert.True(r.HangerDeflectionOk);

        Assert.Equal(327.6, r.BoltTensionComputed);
        Assert.Equal(6222, r.BoltTensionAllowable);
        Assert.True(r.BoltTensionOk);

        Assert.Equal(1101.15, r.BoltShearComputed);
        Assert.Equal(5124, r.BoltShearAllowable);
        Assert.True(r.BoltShearOk);

        Assert.Equal(0.048954, r.BoltCombined);
        Assert.True(r.BoltCombinedOk);
    }

    [Fact]
    public void 背栓槽口剪应力_随拓孔直径增大而增大()
    {
        // 拓孔直径 15→19，有效剪切长度 25−D 减小，τ 增大
        var base_ = BackboltPlateEngine.Compute(DefaultInput());
        Assert.Equal(2.922, base_.SlotShearComputed);

        var d = DefaultInput();
        d.HoleDiameterMm = 19;
        var r = BackboltPlateEngine.Compute(d);
        Assert.Equal(4.871, r.SlotShearComputed);
    }

    [Fact]
    public void 背栓槽口剪应力_随钻孔深度增大而减小()
    {
        var d = DefaultInput();
        d.DrillDepthMm = 8;
        var r = BackboltPlateEngine.Compute(d);
        Assert.Equal(2.192, r.SlotShearComputed);
    }

    [Fact]
    public void 背栓抗拉抗剪_随背栓总数增大而减小()
    {
        var d = DefaultInput();
        d.BackboltTotalCount = 8;
        var r = BackboltPlateEngine.Compute(d);
        Assert.Equal(275.287, r.BackboltTensionComputed);
        Assert.Equal(1.461, r.SlotShearComputed);
    }

    [Fact]
    public void m表_十五断点_逐位一致()
    {
        // 直接验证 m 表插值（r 值 → m 值）
        // r=0.812（round3 后）→ m=0.1447
        var r = BackboltPlateEngine.Compute(DefaultInput());
        Assert.Equal(0.1447, r.MCoef);

        // 端点钳位
        var lo = DefaultInput();
        lo.SmallSupportMm = 100;
        lo.LargeSupportMm = 2000;   // r=0.05 < 0.1 → 钳位 0.125
        var rlo = BackboltPlateEngine.Compute(lo);
        Assert.Equal(0.125, rlo.MCoef);

        var hi = DefaultInput();
        hi.SmallSupportMm = 2000;
        hi.LargeSupportMm = 1500;   // r=1.333 > 1.0 → 钳位 0.156
        var rhi = BackboltPlateEngine.Compute(hi);
        Assert.Equal(0.156, rhi.MCoef);
    }

    [Fact]
    public void 材料重度_按面板种类()
    {
        Assert.Equal(28.0, BackboltPlateEngine.DensityOf("石材"));
        Assert.Equal(23.5, BackboltPlateEngine.DensityOf("瓷板"));
        Assert.Equal(24.0, BackboltPlateEngine.DensityOf("陶板"));
        Assert.Equal(15.0, BackboltPlateEngine.DensityOf("轻质陶瓷板"));
        Assert.Equal(27.0, BackboltPlateEngine.DensityOf("微晶玻璃板"));
        Assert.Equal(16.7, BackboltPlateEngine.DensityOf("纤维水泥板"));
        Assert.Equal(12.7, BackboltPlateEngine.DensityOf("木纤维板"));
        Assert.Equal(22.0, BackboltPlateEngine.DensityOf("玻璃纤维板"));
    }
}
