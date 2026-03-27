using System.ComponentModel.DataAnnotations;

namespace GlassManagement.Api.Services.RegionalStandard;

#region ===== 枚举 =====

/// <summary>建筑开洞类型 — 用于确定内压系数</summary>
public enum BuildingOpeningType
{
    /// <summary>封闭式建筑（开洞面积≤0.5%）</summary>
    Closed = 0,

    /// <summary>部分封闭（有一面开洞面积大于其他面之和）</summary>
    PartiallyClosed = 1,

    /// <summary>开敞式建筑（开洞面积≥80%）</summary>
    Open = 2,

    /// <summary>有主导开口（迎风面有大面积开口）</summary>
    DominantOpening = 3
}

/// <summary>围护结构位置区域（用于局部体型系数）</summary>
public enum ZoneType
{
    /// <summary>中部区域</summary>
    Center = 0,

    /// <summary>边缘区域</summary>
    Edge = 1,

    /// <summary>角部区域</summary>
    Corner = 2
}

/// <summary>围护结构表面类型</summary>
public enum SurfaceType
{
    /// <summary>墙面</summary>
    Wall = 0,

    /// <summary>平屋面</summary>
    FlatRoof = 1,

    /// <summary>坡屋面</summary>
    SlopedRoof = 2,

    /// <summary>女儿墙</summary>
    Parapet = 3,

    /// <summary>阳台</summary>
    Balcony = 4
}

#endregion

#region ===== 输入/输出模型 =====

/// <summary>局部体型系数计算输入</summary>
public class LocalShapeCoeffInput
{
    /// <summary>建筑高度 H (m)</summary>
    [Required]
    public double BuildingHeight { get; set; }

    /// <summary>建筑迎风面宽度 d (m)（广东规范用）</summary>
    public double BuildingWidth { get; set; } = 30;

    /// <summary>从属面积 A (m²)</summary>
    public double TributaryArea { get; set; } = 1.0;

    /// <summary>围护结构表面类型</summary>
    public SurfaceType SurfaceType { get; set; } = SurfaceType.Wall;

    /// <summary>围护结构位置区域</summary>
    public ZoneType ZoneType { get; set; } = ZoneType.Center;

    /// <summary>建筑开洞类型</summary>
    public BuildingOpeningType OpeningType { get; set; } = BuildingOpeningType.Closed;

    /// <summary>是否有女儿墙</summary>
    public bool HasParapet { get; set; }

    /// <summary>女儿墙高度 hp (m)</summary>
    public double ParapetHeight { get; set; }

    /// <summary>屋面坡度 (°)（坡屋面时使用）</summary>
    public double RoofSlope { get; set; }
}

/// <summary>局部体型系数计算结果</summary>
public class LocalShapeCoeffResult
{
    /// <summary>最终局部体型系数 μsl</summary>
    public double Musl { get; set; }

    /// <summary>外压局部体型系数 μse（广东规范使用）</summary>
    public double Muse { get; set; }

    /// <summary>内压系数 μsi（广东规范使用）</summary>
    public double Musi { get; set; }

    /// <summary>从属面积折减后的系数（国标使用）</summary>
    public double AreaReducedCoeff { get; set; }

    /// <summary>原始查表值 μs1(1)（A=1m²时）</summary>
    public double BaseCoeff { get; set; }

    /// <summary>计算说明</summary>
    public string Description { get; set; } = "";

    /// <summary>规范条文出处</summary>
    public string StandardClause { get; set; } = "";
}

/// <summary>风荷载计算统一输入</summary>
public class WindLoadInput
{
    /// <summary>计算点标高 z (m)</summary>
    [Required]
    public double Height { get; set; }

    /// <summary>地面粗糙度类别 (A/B/C/D)</summary>
    [Required]
    public string TerrainCategory { get; set; } = "C";

    /// <summary>城市名称（用于查询基本风压）</summary>
    [Required]
    public string City { get; set; } = "";

    /// <summary>基本风压 w0 (kN/m²)，若为0则按城市自动查询</summary>
    public double BasicWindPressure { get; set; }

    /// <summary>重现期（年），默认50年</summary>
    public int ReturnPeriod { get; set; } = 50;

    /// <summary>局部体型系数输入参数</summary>
    public LocalShapeCoeffInput? ShapeCoeffInput { get; set; }

    /// <summary>直接指定局部体型系数 μs1(1)（不使用ShapeCoeffInput时）</summary>
    public double? DirectShapeCoeff { get; set; }

    /// <summary>支承结构从属面积 (m²)</summary>
    public double StructureArea { get; set; } = 2.0;

    /// <summary>地形修正系数 K1 (GB55001-2021)</summary>
    public double K1 { get; set; } = 1.0;

    /// <summary>风向影响系数 K2 (GB55001-2021)</summary>
    public double K2 { get; set; } = 1.0;

    /// <summary>建筑开洞类型</summary>
    public BuildingOpeningType OpeningType { get; set; } = BuildingOpeningType.Closed;
}

/// <summary>风荷载计算统一结果</summary>
public class WindLoadResult
{
    /// <summary>基本风压 w0 (kN/m²)</summary>
    public double W0 { get; set; }

    /// <summary>风压高度变化系数 μz</summary>
    public double MuZ { get; set; }

    /// <summary>阵风系数 βgz</summary>
    public double BetaGz { get; set; }

    /// <summary>面板局部体型系数 μsl（面板）</summary>
    public double PanelShapeCoeff { get; set; }

    /// <summary>支承结构局部体型系数 μsl（结构）</summary>
    public double StructureShapeCoeff { get; set; }

    /// <summary>内压系数</summary>
    public double InternalPressureCoeff { get; set; }

    /// <summary>面板风荷载标准值 wk (kN/m²)</summary>
    public double PanelWindLoad { get; set; }

    /// <summary>支承结构风荷载标准值 wk (kN/m²)</summary>
    public double StructureWindLoad { get; set; }

    /// <summary>计算依据规范编号</summary>
    public string Standard { get; set; } = "";

    /// <summary>详细计算步骤</summary>
    public List<WindLoadCalcStep> Steps { get; set; } = new();
}

/// <summary>计算步骤详情</summary>
public class WindLoadCalcStep
{
    public int StepNo { get; set; }
    public string Name { get; set; } = "";
    public string Formula { get; set; } = "";
    public string Process { get; set; } = "";
    public string Result { get; set; } = "";
    public string Clause { get; set; } = "";
}

/// <summary>规范信息</summary>
public class StandardInfo
{
    /// <summary>规范代号</summary>
    public string Code { get; set; } = "";

    /// <summary>规范名称</summary>
    public string Name { get; set; } = "";

    /// <summary>适用地区描述</summary>
    public string Region { get; set; } = "";

    /// <summary>规范发布年份</summary>
    public int Year { get; set; }

    /// <summary>是否为完整实现（否则为框架）</summary>
    public bool IsFullyImplemented { get; set; }
}

/// <summary>城市风压信息</summary>
public class CityWindPressureInfo
{
    /// <summary>城市/区县名称</summary>
    public string City { get; set; } = "";

    /// <summary>上级地级市（区/县时有值）</summary>
    public string? Prefecture { get; set; }

    /// <summary>50年重现期基本风压 (kN/m²)</summary>
    public double W0_50 { get; set; }

    /// <summary>100年重现期基本风压 (kN/m²)，0表示无数据</summary>
    public double W0_100 { get; set; }
}

#endregion
