namespace GlassManagement.Calc.Engines;

/// <summary>建筑类别（热工节能计算三类：公共/居住/工业）。菜单 id 125/126 公共、128/129 居住、131/132 工业。</summary>
public enum BuildingType
{
    公共建筑 = 0,
    居住建筑 = 1,
    工业建筑 = 2,
}

/// <summary>热工节能限值与室内温度（按建筑类别，b7_probe28_residential.json 取证直读 + t_i 精确验证）。</summary>
public static class ThermalLimits
{
    /// <summary>玻璃幕墙（CW26）K 限值：公共 2.2 / 居住 2.5 / 工业 3.4（工业为「建筑整体K值」）。</summary>
    public static double GlassKLimb(BuildingType b) => b switch
    {
        BuildingType.居住建筑 => 2.5,
        BuildingType.工业建筑 => 3.4,
        _ => 2.2,
    };

    /// <summary>玻璃幕墙（CW26）SHGC 夏季限值：公共 0.35 / 居住 0.4 / 工业 0.6。</summary>
    public static double GlassShgcLimit(BuildingType b) => b switch
    {
        BuildingType.居住建筑 => 0.4,
        BuildingType.工业建筑 => 0.6,
        _ => 0.35,
    };

    /// <summary>玻璃幕墙（CW26）室内计算温度 t_i：公共/居住 20 / 工业 16。</summary>
    public static double GlassTIndoor(BuildingType b) => b == BuildingType.工业建筑 ? 16 : 20;

    /// <summary>非透明幕墙（CW27）K 限值：公共 0.8 / 居住 1.0 / 工业 1.1（工业为「建筑整体K值」）。</summary>
    public static double WallKLimb(BuildingType b) => b switch
    {
        BuildingType.居住建筑 => 1.0,
        BuildingType.工业建筑 => 1.1,
        _ => 0.8,
    };

    /// <summary>非透明幕墙（CW27）室内计算温度 t_i（采暖房间）：公共/居住 18 / 工业 16。</summary>
    public static double WallTIndoor(BuildingType b) => b == BuildingType.工业建筑 ? 16 : 18;
}
