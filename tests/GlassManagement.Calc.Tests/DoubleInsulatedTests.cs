using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 双中空玻璃的等效厚度 —— V31 函数区 0xA5Cxxx–0xA5F2xx。
///
/// 四点实机（1100×1380、wk=0.00137、αmax=0.16）：
///   6/6/6 → 7.952    8/6/6 → 9.112    6/8/6 → 8.964    6/6/8 → 8.964
/// 拟合：t_e = [ (0.95·t₁)³ + (0.95²·t₂)³ + (0.95²·t₃)³ ]^(1/3)（逐层衰减）
/// </summary>
public class DoubleInsulatedTests
{
    [Theory]
    [InlineData(6.0, 6.0, 6.0, 7.952)]
    [InlineData(8.0, 6.0, 6.0, 9.112)]
    [InlineData(6.0, 8.0, 6.0, 8.964)]
    [InlineData(6.0, 6.0, 8.0, 8.964)]
    public void 等效厚度_四点逐位(double t1, double t2, double t3, double expect)
    {
        Assert.Equal(expect, GlassEngine.DoubleInsulatedThickness(t1, t2, t3));
    }

    [Fact]
    public void 外片权重是内侧两倍的平方关系()
    {
        // 外片只过一层腔（×0.95），中/内片过两层（×0.95²）
        double onlyOuter = GlassEngine.DoubleInsulatedThickness(8, 0.0001, 0.0001);
        double onlyMid = GlassEngine.DoubleInsulatedThickness(0.0001, 8, 0.0001);
        Assert.True(onlyOuter > onlyMid, "外片折一次，中片折两次 → 外片贡献更大");
        // 比值应约等于 1/0.95
        Assert.InRange(onlyOuter / onlyMid, 1.04, 1.06);
    }

    [Fact]
    public void 与中空加夹层的口径不同_不可混用()
    {
        // 双中空 6/6/6 → 7.952；中空+夹层 6/6/6 → 8.221（0.95·(Σt³)^(1/3)）
        var insullam = GlassEngine.ComputeComposite(0.00137, 1100, 1380, new[] { 6.0, 6.0, 6.0 },
                                                    InterlayerKind.Pvb, 0.16);
        Assert.Equal(8.221, insullam.EquivalentThickness);
        Assert.Equal(7.952, GlassEngine.DoubleInsulatedThickness(6, 6, 6));
        Assert.NotEqual(insullam.EquivalentThickness, GlassEngine.DoubleInsulatedThickness(6, 6, 6));
    }
}
