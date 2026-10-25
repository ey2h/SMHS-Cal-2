using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 胶类、伸缩及紧固计算屏逐位对拍。
/// 期望值来自 V31 计算书原文（v31runs/book_sealant.txt），
/// 算式来自 v31runs/sealant_trace_vartrace.json（134 条 __vbaVar* 实数轨迹）。
/// </summary>
public class SealantEngineTests
{
    private static SealantResult Run() => SealantEngine.Compute(SealantEngine.DefaultInputs());

    private static void Same(string label, double actual, double expected)
        => Assert.Equal(expected.ToString("F3"), actual.ToString("F3"));

    [Fact]
    public void 面板风荷载用面板口径()
    {
        Same("面板部分风荷载标准值", Run().WkPanelKpa, 1.37);
    }

    [Fact]
    public void 结构胶宽度与厚度_逐位一致()
    {
        var r = Run();
        Same("结构胶宽度最小值", r.StructuralWidth, 6.121);
        Same("结构胶厚度最小值", r.StructuralThickness, 5.475);
    }

    [Fact]
    public void 耐候胶与伸缩缝_逐位一致()
    {
        var r = Run();
        Same("耐候胶胶缝计算宽度", r.WeatherWidth, 7.705);
        Same("立柱伸缩缝计算值", r.JointWidth, 8.381);
    }

    [Fact]
    public void 压板应力_逐位一致()
    {
        var r = Run();
        Same("压板弯曲应力", r.ClampBending, 43.907);
        Same("压板剪应力", r.ClampShear, 2.927);
        Same("压板抗弯设计值", r.ClampBendingLimit, 90);
        Same("压板抗剪设计值", r.ClampShearLimit, 55);
    }

    [Fact]
    public void 连接螺栓_逐位一致()
    {
        var r = Run();
        Same("抗拉承载力设计值", r.BoltCapacity, 2409.836);
        Same("实际受力", r.BoltForce, 1030.356);
    }

    /// <summary>结构胶厚度随层高变化（防止把 5.475 写成常量）。</summary>
    [Fact]
    public void 层高变化_厚度随之变化()
    {
        var a = SealantEngine.Compute(SealantEngine.DefaultInputs());
        var b = SealantEngine.Compute(SealantEngine.DefaultInputs() with { StoryHeight = 1600 });
        Assert.NotEqual(a.StructuralThickness.ToString("F3"),
                        b.StructuralThickness.ToString("F3"));
    }
}
