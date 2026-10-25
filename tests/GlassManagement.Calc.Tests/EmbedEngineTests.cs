using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 埋件与焊缝计算屏逐位对拍（土建预埋 + L 型转接件 + 三面围焊）。
///
/// 期望值全部来自 V31 计算书原文（<c>v31runs/book_embed.txt</c>）与槽数组直读，
/// 算式来自 <c>v31runs/embed_trace_vartrace.json</c>（234 条 __vbaVar* 实数轨迹）。
/// 判据：F3 逐位相等。
/// </summary>
public class EmbedEngineTests
{
    private static EmbedResult Run() => EmbedEngine.Compute(EmbedEngine.DefaultInputs());

    private static void Same(string label, double actual, double expected)
        => Assert.Equal(expected.ToString("F3"), actual.ToString("F3"));

    [Fact]
    public void 荷载与内力_逐位一致()
    {
        var r = Run();
        Same("风荷载 kPa", r.WkKpa, 1.285);
        Same("剪力设计值", r.ShearDesign, 1716);
        Same("轴力设计值", r.AxialDesign, 7098.3);
        Same("弯矩设计值", r.MomentDesign, 137280);
        Same("地震作用 qe", r.QuakeLoad, 0.00032);
        Same("组合荷载", r.CombinedLoad, 0.002151);
    }

    [Fact]
    public void 锚筋面积_逐位一致()
    {
        var r = Run();
        Same("αr", r.AlphaR, 0.638);
        Same("αb", r.AlphaB, 0.767);
        Same("锚筋总截面积计算值", r.RebarAreaRequired, 57.911);
        Same("实际锚筋总截面积", r.RebarAreaActual, 452.16);
        Same("0.5fcA", r.ConcreteBearing, 357000);
    }

    [Fact]
    public void 锚固长度_逐位一致()
    {
        var r = Run();
        Same("基本锚固长度 lab", r.AnchorLab, 449.008);
        Same("折减后锚固长度 la", r.AnchorLa, 57.507);
    }

    [Fact]
    public void 转接件与焊缝_逐位一致()
    {
        var r = Run();
        Same("转接件强度", r.TransferStress, 4.43);
        Same("转接件强度设计值", r.TransferStressLimit, 215);
        Same("转接件挠度", r.TransferDeflection, 0.000276);
        Same("转接件挠度设计值", r.TransferDeflLimit, 0.64);
        Same("焊缝强度", r.WeldStress, 12.452);
        Same("焊缝强度设计值", r.WeldStressLimit, 160);
        Same("焊缝计算面积", r.WeldArea, 529.2);
        Same("焊缝截面模量", r.WeldSectionModulus, 8214.426);
    }

    /// <summary>弯矩项两分支取大：1.3 分支得 57.443，0.4 分支得 57.911，实机取后者。</summary>
    [Fact]
    public void 弯矩项取大分支()
    {
        var r = Run();
        Assert.True(r.RebarAreaRequired > 57.443,
            $"应为 0.4 分支的 57.911，实际 {r.RebarAreaRequired}");
        Same("取大分支结果", r.RebarAreaRequired, 57.911);
    }

    /// <summary>锚筋直径变化时 la 必须跟着变（防止把 57.507 写成常量）。</summary>
    [Fact]
    public void 锚筋直径变化_la随之变化()
    {
        var a = EmbedEngine.Compute(EmbedEngine.DefaultInputs());
        var b = EmbedEngine.Compute(EmbedEngine.DefaultInputs() with { RebarDiameter = 14 });
        Assert.NotEqual(a.AnchorLa.ToString("F3"), b.AnchorLa.ToString("F3"));
        Assert.NotEqual(a.AnchorLab.ToString("F3"), b.AnchorLab.ToString("F3"));
    }
}
