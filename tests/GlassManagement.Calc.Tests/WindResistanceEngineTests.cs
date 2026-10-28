using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 物理性能·幕墙抗风压性能（菜单 id=139）对拍：b7_probe29_kangfengya.json 输出逐字
/// （北京默认 wk=2.5KPa → 4级；分级表 GB/T31433-2015 表2）。
/// </summary>
public class WindResistanceEngineTests
{
    [Theory]
    [InlineData(1.0, 1)]
    [InlineData(1.5, 2)]
    [InlineData(2.0, 3)]
    [InlineData(2.5, 4)]
    [InlineData(3.0, 5)]
    [InlineData(3.5, 6)]
    [InlineData(4.0, 7)]
    [InlineData(4.5, 8)]
    [InlineData(5.0, 9)]
    [InlineData(6.0, 9)]
    [InlineData(0.9, 0)]
    public void 抗风压性能_分级_GB31433表2(double wk, int grade)
    {
        Assert.Equal(grade, WindResistanceEngine.Grade(wk));
    }

    [Fact]
    public void 抗风压性能_结论文案_probe29逐字()
    {
        // b7_probe29：wk=2.5 → "3.0KPa>wk = 2.5KPa≥2.5KPa      所以抗风压性能为：4级"
        string s = WindResistanceEngine.Conclude(2.5);
        Assert.Contains("3KPa>wk = 2.5KPa≥2.5KPa", s);
        Assert.Contains("所以抗风压性能为：4级", s);
    }
}
