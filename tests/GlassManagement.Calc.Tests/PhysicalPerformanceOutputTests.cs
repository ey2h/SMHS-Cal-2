using GlassManagement.Calc.Data;
using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// CW19 物理性能计算页输出文案逐字对拍（golden = V31 实机抓取，2026-10-28 T12b）。
/// 139 幕墙抗风压 / 149 门窗抗风压：同一模板，仅「幕墙/门窗」替换。
/// </summary>
public class PhysicalPerformanceOutputTests
{
    [Fact]
    public void CW19_139_幕墙抗风压_逐字复刻()
    {
        var actual = PhysicalPerformanceOutput.WindResistance(false, "北京", 2.5, 4);
        Assert.Equal(Cw19WindResistanceGolden.M139, actual);
    }

    [Fact]
    public void CW19_149_门窗抗风压_逐字复刻()
    {
        var actual = PhysicalPerformanceOutput.WindResistance(true, "北京", 2.5, 4);
        Assert.Equal(Cw19WindResistanceGolden.M149, actual);
    }

    [Theory]
    [InlineData(1.2, 1)]
    [InlineData(2.2, 3)]
    [InlineData(3.2, 5)]
    [InlineData(4.2, 7)]
    [InlineData(5.2, 9)]
    public void CW19_抗风压_分级与比较行(double wk, int grade)
    {
        Assert.Equal(grade, WindResistanceEngine.Grade(wk));
        var s = PhysicalPerformanceOutput.WindResistance(false, "北京", wk, grade);
        Assert.Contains($"所以抗风压性能为：{grade}级", s);
        // 9 级无上限形态：不出现 "X.XKPa>wk"
        if (grade >= 9)
            Assert.DoesNotContain("KPa>wk", s);
        else
            Assert.Contains("KPa>wk", s);
    }

    [Fact]
    public void CW19_抗风压_低于一级_走兜底()
    {
        var s = PhysicalPerformanceOutput.WindResistance(false, "北京", 0.8, 0);
        Assert.Contains("低于最低分级", s);
        Assert.DoesNotContain("所以抗风压性能为", s);
    }

    [Fact]
    public void CW19_140_幕墙水密_逐字复刻()
    {
        // 实机：C类 100m，w0=1.5KPa，非台风 → μz=1.4983, P=2696.9, 0.75P=2022.7, 5级
        var actual = PhysicalPerformanceOutput.WallWaterTightness("北京", 100, TerrainCategory.C, false, 1.5);
        Assert.Equal(Cw19WaterTightnessGolden.M140, actual);
    }

    [Fact]
    public void CW19_150_门窗水密_逐字复刻()
    {
        // 实机：C类 100m，w0=1500Pa，非台风 → C=0.4, μz=1.4983, P=899, 6级
        var actual = PhysicalPerformanceOutput.DoorWaterTightness("北京", 100, TerrainCategory.C, false, 1500);
        Assert.Equal(Cw19WaterTightnessGolden.M150, actual);
    }

    [Fact]
    public void CW19_137_空气声隔声_逐字复刻()
    {
        // 实机（单片 4mm 默认）：R=26.639, 组合墙 31.41, 3/4/2 级
        var s = SoundInsulationEngine.GlassStructure.单片玻璃;
        double r = SoundInsulationEngine.ComputeDefault(s);
        double rWall = SoundInsulationEngine.ComputeWall(r, 50, 25);
        var actual = PhysicalPerformanceOutput.Insulation(
            r, rWall,
            SoundInsulationEngine.Grade8485(r),
            SoundInsulationEngine.Grade50121(r),
            SoundInsulationEngine.Grade21086(r));
        Assert.Equal(Cw19InsulationGolden.M137, actual);
    }
}
