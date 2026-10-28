using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 全玻璃幕墙设计计算屏引擎逐位对拍。
///
/// <para><b>实机基线</b>（2026-10-26，V31「全玻璃(含吊挂) → 全玻璃幕墙设计计算」原文，
/// <c>v31runs/fullglass_design_var.json</c>）：玻璃 10mm 钢化、板 长4000×短1500、
/// 玻璃肋 t=12 h=200 钢化、αmax=0.16、wk=1.37KPa 时，输出</para>
/// <code>
/// 玻璃强度计算值:35.571MPa（设计 84 钢化）
/// 玻璃挠度计算值:13.892mm（容许 25）
/// 玻璃肋实际截面高度:200mm（不应小于 411.19mm，不满足 ×）
/// 玻璃肋挠度计算值:11.892mm（限值 20）
/// 硅酮结构胶强度校核:0.275MPa（设计 0.2，不满足 ×）
/// </code>
/// </summary>
public class FullGlassEngineTests
{
    private static FullGlassEngine.Input DefaultInput() => new()
    {
        GlassShortSideMm = 1500,
        RibSpanMm = 4000,
        GlassThicknessMm = 10,
        RibThicknessMm = 12,
        RibActualHeightMm = 200,
        AlphaMax = 0.16,
        Wk = 0.00137,
    };

    [Fact]
    public void 默认参数_逐位一致()
    {
        var r = FullGlassEngine.Compute(DefaultInput());

        Assert.Equal(1.37, r.WindStandardKpa);
        Assert.Equal(0.000205, r.SeismicMpa);
        Assert.Equal(0.002198, r.Qd);

        Assert.Equal(35.571, r.GlassSigma);
        Assert.Equal(84, r.GlassSigmaAllowable);
        Assert.True(r.GlassSigmaOk);

        Assert.Equal(13.892, r.GlassDeflection);
        Assert.Equal(25, r.GlassDeflectionAllowable);
        Assert.True(r.GlassDeflectionOk);

        Assert.Equal(411.19, r.RibRequiredHeight);
        Assert.Equal(200, r.RibActualHeight);
        Assert.False(r.RibHeightOk);   // 200 < 411.19

        Assert.Equal(11.892, r.RibDeflection);
        Assert.Equal(20, r.RibDeflectionAllowable);
        Assert.True(r.RibDeflectionOk);

        Assert.Equal(0.275, r.SealantStress);
        Assert.Equal(0.2, r.SealantAllowable);
        Assert.False(r.SealantOk);     // 0.275 > 0.2
    }

    [Fact]
    public void 玻璃肋挠度_随肋高度三次方反比()
    {
        // 肋高 200→300，w肋 = 11.892×(200/300)³ = 3.524
        var d = DefaultInput();
        d.RibActualHeightMm = 300;
        var r = FullGlassEngine.Compute(d);
        Assert.Equal(3.524, r.RibDeflection);
    }

    [Fact]
    public void 结构胶_随肋厚度反比()
    {
        // 肋厚 12→15，τ = 0.002198×1500/15 = 0.22
        var d = DefaultInput();
        d.RibThicknessMm = 15;
        var r = FullGlassEngine.Compute(d);
        Assert.Equal(0.22, r.SealantStress);
        Assert.Equal(393.895, r.RibRequiredHeight);
    }

    // ─────────── 中空玻璃（外片10 + 内片10 + 1 中空层，总厚度 32）───────────
    // 实机基线（fullglass_zhongkong_var.json / fullglass_zhongkong.txt）：
    //   外片 21.327 / 内片 19.682 / 挠度 8.413 / 肋高 424.445 / 肋挠度 11.892 / 结构胶 0.293

    private static FullGlassEngine.Input InsulatedInput() => new()
    {
        GlassShortSideMm = 1500,
        RibSpanMm = 4000,
        GlassThicknessMm = 10,     // 外片
        InnerThicknessMm = 10,     // 内片
        TotalThicknessMm = 32,     // 外片10 + 空气层12 + 内片10
        RibThicknessMm = 12,
        RibActualHeightMm = 200,
        AlphaMax = 0.16,
        Wk = 0.00137,
    };

    [Fact]
    public void 中空玻璃_逐位一致()
    {
        var r = FullGlassEngine.ComputeInsulated(InsulatedInput());

        Assert.Equal(1.37, r.WindStandardKpa);

        Assert.Equal(21.327, r.OuterSigma);
        Assert.Equal(84, r.OuterSigmaAllowable);
        Assert.True(r.OuterSigmaOk);

        Assert.Equal(19.682, r.InnerSigma);
        Assert.Equal(84, r.InnerSigmaAllowable);
        Assert.True(r.InnerSigmaOk);

        Assert.Equal(8.413, r.GlassDeflection);
        Assert.Equal(25, r.GlassDeflectionAllowable);
        Assert.True(r.GlassDeflectionOk);

        Assert.Equal(424.445, r.RibRequiredHeight);
        Assert.Equal(200, r.RibActualHeight);
        Assert.False(r.RibHeightOk);   // 200 < 424.445

        Assert.Equal(11.892, r.RibDeflection);
        Assert.True(r.RibDeflectionOk);

        Assert.Equal(0.293, r.SealantStress);
        Assert.Equal(0.2, r.SealantAllowable);
        Assert.False(r.SealantOk);     // 0.293 > 0.2
    }

    [Fact]
    public void 中空玻璃_总厚度校验()
    {
        // 总厚度 32 > 外片10+内片10=20 → 有效
        Assert.False(FullGlassEngine.TotalThicknessInvalid(32, 10, 10));
        // 总厚度 20 = 玻璃和 → 无效（空气层 0）
        Assert.True(FullGlassEngine.TotalThicknessInvalid(20, 10, 10));
        // 总厚度 10 < 玻璃和 → 无效（切中空后未更新的默认单片值）
        Assert.True(FullGlassEngine.TotalThicknessInvalid(10, 10, 10));
    }

    [Fact]
    public void 中空玻璃_内外片厚度不同_荷载按立方分配()
    {
        // 外片 12、内片 6：qd外 = 1.1·wk·12³/(12³+6³)，qd内 = wk·6³/(12³+6³)
        // 12³=1728, 6³=216, 和=1944
        // qd外 = 1.1×0.00137×1728/1944 = 0.001339, qd内 = 0.00137×216/1944 = 0.000152
        var d = InsulatedInput();
        d.GlassThicknessMm = 12;
        d.InnerThicknessMm = 6;
        d.TotalThicknessMm = 30;   // 12 + 12 + 6
        var r = FullGlassEngine.ComputeInsulated(d);

        Assert.True(r.OuterSigma > r.InnerSigma);   // 外片厚、分配荷载大，强度应更大
        Assert.False(FullGlassEngine.TotalThicknessInvalid(d.TotalThicknessMm, 12, 6));
    }

    // ─────────── 夹层玻璃（外片10 + 内片10，等片，PVB）───────────
    // 实机基线：内片 19.682 / 外片 19.682 / 挠度 7.213 / 肋高 424.445 / 结构胶 0.293

    [Fact]
    public void 夹层玻璃_逐位一致()
    {
        var d = InsulatedInput();
        var r = FullGlassEngine.ComputeLaminated(d);

        Assert.Equal(19.682, r.InnerSigma);
        Assert.Equal(19.682, r.OuterSigma);
        Assert.Equal(7.213, r.GlassDeflection);
        Assert.Equal(424.445, r.RibRequiredHeight);
        Assert.Equal(0.293, r.SealantStress);
    }

    // ─────────── 三玻双中空（外+中+内，2 中空层）───────────
    // 实机基线：外 16.47 / 中 13.382 / 内 13.382 / 挠度 6.197 / 肋高 437.299 / 结构胶 0.311

    [Fact]
    public void 三玻双中空_逐位一致()
    {
        var d = InsulatedInput();
        var r = FullGlassEngine.ComputeTripleInsul(d);

        Assert.Equal(16.47, r.OuterSigma);
        Assert.Equal(13.382, r.MidSigma);
        Assert.Equal(13.382, r.InnerSigma);
        Assert.Equal(6.197, r.GlassDeflection);
        Assert.Equal(437.299, r.RibRequiredHeight);
        Assert.Equal(0.311, r.SealantStress);
    }

    // ─────────── 三玻双夹层（外+中+内，双夹层）───────────
    // 实机基线：外/中/内 13.989 / 挠度 4.809 / 肋高 437.299 / 结构胶 0.311

    [Fact]
    public void 三玻双夹层_逐位一致()
    {
        var d = InsulatedInput();
        var r = FullGlassEngine.ComputeTripleLam(d);

        Assert.Equal(13.989, r.OuterSigma);
        Assert.Equal(13.989, r.MidSigma);
        Assert.Equal(13.989, r.InnerSigma);
        Assert.Equal(4.809, r.GlassDeflection);
        Assert.Equal(437.299, r.RibRequiredHeight);
        Assert.Equal(0.311, r.SealantStress);
    }

    // ─────────── 双夹层-中空（4片：最外1#/次外2#/次内3#/最内4#）───────────
    // 实机基线：1# 11.964 / 2# 11.964 / 3# 11.07 / 4# 11.07 / 挠度 4.213 / 肋高 449.701 / 结构胶 0.329

    [Fact]
    public void 双夹层中空_逐位一致()
    {
        var d = InsulatedInput();
        var r = FullGlassEngine.ComputeDoubleLamInsul(d);

        Assert.Equal(11.964, r.OuterSigma);
        Assert.Equal(11.964, r.Outer2Sigma);
        Assert.Equal(11.07, r.InnerSigma);
        Assert.Equal(11.07, r.Inner2Sigma);
        Assert.Equal(4.213, r.GlassDeflection);
        Assert.Equal(449.701, r.RibRequiredHeight);
        Assert.Equal(0.329, r.SealantStress);
    }

    // ─────────── 中空+夹层外（外夹层2片 + 中空 + 内单片）───────────
    // 实机基线：外 15.12 / 中 15.12 / 内 13.989 / 挠度 5.609 / 肋高 437.299 / 结构胶 0.311

    [Fact]
    public void 中空夹层外_逐位一致()
    {
        var d = InsulatedInput();
        var r = FullGlassEngine.ComputeInsulLamOuter(d);

        Assert.Equal(15.12, r.OuterSigma);
        Assert.Equal(15.12, r.MidSigma);
        Assert.Equal(13.989, r.InnerSigma);
        Assert.Equal(5.609, r.GlassDeflection);
        Assert.Equal(437.299, r.RibRequiredHeight);
        Assert.Equal(0.311, r.SealantStress);
    }

    // ─────────── 中空+夹层内（外单片 + 中空 + 内夹层2片）───────────
    // 实机基线：外 15.12 / 中 13.989 / 内 13.989 / 挠度 5.609 / 肋高 437.299 / 结构胶 0.311

    [Fact]
    public void 中空夹层内_逐位一致()
    {
        var d = InsulatedInput();
        var r = FullGlassEngine.ComputeInsulLamInner(d);

        Assert.Equal(15.12, r.OuterSigma);
        Assert.Equal(13.989, r.MidSigma);
        Assert.Equal(13.989, r.InnerSigma);
        Assert.Equal(5.609, r.GlassDeflection);
        Assert.Equal(437.299, r.RibRequiredHeight);
        Assert.Equal(0.311, r.SealantStress);
    }

    // P2 · 玻璃肋强度设计值 [σ]肋 折减表 9 档全直读（2026-10-28 b7_probe27_fullglass_sigma.json）
    // [σ]肋 本体在 ra=0xFA0361 以 Mul(4,σ) 出现：t12→78、t15/19→68、t20/22/25→56
    [Theory]
    [InlineData(6, 19.5)]
    [InlineData(8, 19.5)]
    [InlineData(10, 19.5)]
    [InlineData(12, 19.5)]
    [InlineData(15, 17.0)]
    [InlineData(19, 17.0)]
    [InlineData(20, 14.0)]
    [InlineData(22, 14.0)]
    [InlineData(25, 14.0)]
    public void 玻璃肋强度设计值_折减表9档_直读(double ribThk, double sigma)
    {
        var r = FullGlassEngine.Compute(new FullGlassEngine.Input { RibThicknessMm = ribThk });
        Assert.Equal(sigma, r.RibAllowableMpa);
    }
}
