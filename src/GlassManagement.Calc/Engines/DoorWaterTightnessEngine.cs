using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 物理性能·门窗水密性能（菜单 id=150，CW19「水密性能计算」帧）。
/// 公式（b7_probe33 id=150 输出逐字直读，JGJ103-2008 3.3.1-2 / JGJ214-2010 4.5.2 / GB/T31433-2015 表16）：
///   ① μz = 风压高度变化系数（复用 WindLoadEngine.MuZ，四类场地 + 截断）
///   ② P = C·μz·W0，C 水密性能设计计算系数：台风区 0.5 / 其它地区 0.4；W0 基本风压(Pa，非 KPa)
///   ③ 分级（表16 门窗水密，6 级）：1级 100≤△P&lt;150 / 2级 150~250 / 3级 250~350 /
///      4级 350~500 / 5级 500~700 / 6级 ≥700 Pa
/// 实测：C类 100m、W0=1500Pa、非台风 → μz=1.4983 → P=0.4×1.4983×1500=899 → 6级。
/// 注：与幕墙水密（WaterTightnessEngine，P=1000·μz·1.2·w0×0.75、表17）不同。
/// </summary>
public sealed class DoorWaterTightnessEngine
{
    /// <summary>水密性能设计计算系数 C：台风区 0.5 / 其它地区 0.4。</summary>
    public static double Coef(bool typhoon) => typhoon ? 0.5 : 0.4;

    /// <summary>水密性设计取值 P(Pa)。W0 单位 Pa。</summary>
    public static double DesignPressure(TerrainCategory terrain, double zM, double w0Pa, bool typhoon)
    {
        double muz = WindLoadEngine.MuZ(terrain, zM);
        double p = Coef(typhoon) * muz * w0Pa;
        return WindLoadEngine.VbaRound(p, 0);   // 899（整数）
    }

    /// <summary>门窗水密性能等级（1~6；GB/T31433-2015 表16）。</summary>
    public static int Grade(double p)
    {
        if (p < 100) return 1;
        if (p < 150) return 2;
        if (p < 250) return 3;
        if (p < 350) return 4;
        if (p < 500) return 5;
        return 6;
    }
}
