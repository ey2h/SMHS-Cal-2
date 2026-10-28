using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 物理性能·幕墙水密性能（菜单 id=140，CW19「水密性能计算」帧）。
/// 公式（b7_probe30 id=140 输出逐字直读，JGJ102-2003 4.2.5 / GB/T31433-2015 表17）：
///   ① μz = 风压高度变化系数（复用 WindLoadEngine.MuZ，A/B/C/D 四类场地 + 截断）
///   ② P = 1000·μz·μc·w0，μc（风力系数）=1.2，w0 基本风压(KPa)
///   ③ 非热带风暴/台风区：P = 0.75·P（且固定部分取值不应低于 700Pa）
///   ④ 固定部分分级（表17）：1级 &lt;500 / 2级 500~700 / 3级 700~1000 / 4级 1000~1500 / 5级 ≥1500 Pa
/// 实测：C类 100m、w0=1.5 → μz=1.4983 → P=2696.9 → 非台风 0.75P=2022.7 → 5级。
/// </summary>
public sealed class WaterTightnessEngine
{
    /// <summary>风力系数 μc（JGJ102 4.2.5，可取 1.2）。</summary>
    public const double MuC = 1.2;

    /// <summary>水密性设计取值 P(Pa)。typhoon=true 受台风袭击区；false 其它地区（×0.75）。</summary>
    public static double DesignPressure(TerrainCategory terrain, double zM, double w0Kpa, bool typhoon)
    {
        double muz = WindLoadEngine.MuZ(terrain, zM);
        double p = 1000.0 * muz * MuC * w0Kpa;
        if (!typhoon) p *= 0.75;
        return WindLoadEngine.VbaRound(p, 1);
    }

    /// <summary>固定部分水密性能等级（1~5；GB/T31433-2015 表17 固定部分）。</summary>
    public static int Grade(double p)
    {
        if (p < 500) return 1;
        if (p < 700) return 2;
        if (p < 1000) return 3;
        if (p < 1500) return 4;
        return 5;
    }
}
