using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 物理性能·门窗水密性能（菜单 id=150）对拍：b7_probe33 id=150 输出逐字
/// （C类 100m、W0=1500Pa、非台风 → P=0.4×1.4983×1500=899 → 6级）。
/// </summary>
public class DoorWaterTightnessEngineTests
{
    [Fact]
    public void 门窗水密_设计取值_probe33逐位()
    {
        double p = DoorWaterTightnessEngine.DesignPressure(TerrainCategory.C, 100, 1500, typhoon: false);
        Assert.Equal(899, p);
        Assert.Equal(6, DoorWaterTightnessEngine.Grade(p));   // ≥700 → 6级
    }

    [Fact]
    public void 门窗水密_台风区系数05()
    {
        // 台风区 C=0.5：P = 0.5×1.4983×1500 = 1124 → 6级
        double p = DoorWaterTightnessEngine.DesignPressure(TerrainCategory.C, 100, 1500, typhoon: true);
        Assert.Equal(1124, p);
    }

    [Theory]
    [InlineData(100, 2)]
    [InlineData(150, 3)]
    [InlineData(250, 4)]
    [InlineData(350, 5)]
    [InlineData(500, 6)]
    [InlineData(700, 6)]
    [InlineData(1200, 6)]
    public void 门窗水密_分级_表16(double p, int grade)
    {
        Assert.Equal(grade, DoorWaterTightnessEngine.Grade(p));
    }
}
