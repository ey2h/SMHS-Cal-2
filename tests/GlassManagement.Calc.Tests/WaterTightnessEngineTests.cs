using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 物理性能·幕墙水密性能（菜单 id=140）对拍：b7_probe30 id=140 输出逐字
/// （C类 100m、w0=1.5、非台风 → P=2696.9 → 0.75P=2022.7 → 5级）。
/// </summary>
public class WaterTightnessEngineTests
{
    [Fact]
    public void 水密性能_设计取值_probe30逐位()
    {
        // μz=0.544×(100/10)^0.44=1.4983 → P=1000×1.4983×1.2×1.5=2696.94→2696.9
        double p = WaterTightnessEngine.DesignPressure(TerrainCategory.C, 100, 1.5, typhoon: false);
        Assert.Equal(2022.7, p);   // 0.75×2696.94=2022.705 → 2022.7
        Assert.Equal(5, WaterTightnessEngine.Grade(p));   // ≥1500 → 5级
    }

    [Fact]
    public void 水密性能_台风区不折减()
    {
        // 台风区：P = 1000×1.4983×1.2×1.5 = 2696.9（不×0.75）
        double p = WaterTightnessEngine.DesignPressure(TerrainCategory.C, 100, 1.5, typhoon: true);
        Assert.Equal(2696.9, p);
        Assert.Equal(5, WaterTightnessEngine.Grade(p));
    }

    [Theory]
    [InlineData(400, 1)]
    [InlineData(500, 2)]
    [InlineData(700, 3)]
    [InlineData(1000, 4)]
    [InlineData(1500, 5)]
    [InlineData(2500, 5)]
    public void 水密性能_分级_表17固定部分(double p, int grade)
    {
        Assert.Equal(grade, WaterTightnessEngine.Grade(p));
    }
}
