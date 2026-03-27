using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GlassManagement.Core.Models;

#region ===== 玻璃材质 (已有，补全) =====

/// <summary>玻璃材质</summary>
public class GlassMaterial
{
    [Key] public int Id { get; set; }

    /// <summary>材质名称</summary>
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;

    /// <summary>玻璃类型 (Float / SemiTempered / Tempered)</summary>
    [Required, MaxLength(50)] public string GlassType { get; set; } = string.Empty;

    /// <summary>强度等级 (Normal / High / ExtraHigh)</summary>
    [Required, MaxLength(50)] public string StrengthLevel { get; set; } = string.Empty;

    /// <summary>设计强度 fg (MPa)</summary>
    [Required] public double DesignStrength { get; set; }

    /// <summary>极限强度 (MPa)</summary>
    public double UltimateStrength { get; set; }

    /// <summary>弹性模量 E (MPa)，默认72000</summary>
    public double ElasticModulus { get; set; } = 72000;

    /// <summary>泊松比，默认0.2</summary>
    public double PoissonRatio { get; set; } = 0.2;

    /// <summary>密度 (kg/m³)，默认2500</summary>
    public double Density { get; set; } = 2500;

    /// <summary>备注</summary>
    [MaxLength(500)] public string? Remark { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

#endregion

#region ===== 单片玻璃配置 (已有，补全) =====

public class SingleGlassConfig
{
    [Key] public int Id { get; set; }

    /// <summary>名称 (如 "6mm钢化玻璃")</summary>
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;

    /// <summary>标称厚度 (mm)</summary>
    [Required] public double NominalThickness { get; set; }

    /// <summary>最小厚度 (mm)</summary>
    public double MinThickness { get; set; }

    /// <summary>玻璃材质Id</summary>
    public int? GlassMaterialId { get; set; }

    [ForeignKey(nameof(GlassMaterialId))]
    public GlassMaterial? GlassMaterial { get; set; }

    /// <summary>最大面积 (m²)</summary>
    public double? MaxArea { get; set; }

    /// <summary>是否可用</summary>
    public bool IsActive { get; set; } = true;

    [MaxLength(500)] public string? Remark { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

#endregion

#region ===== 夹胶玻璃配置 (已有，补全) =====

public class LaminatedGlassConfig
{
    [Key] public int Id { get; set; }

    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;

    /// <summary>玻璃层配置，如 "6+1.14PVB+6"</summary>
    [Required, MaxLength(200)] public string Configuration { get; set; } = string.Empty;

    /// <summary>胶片类型 (PVB / SGP / EVA)</summary>
    [Required, MaxLength(50)] public string InterlayerType { get; set; } = "PVB";

    /// <summary>各层玻璃厚度 JSON, 如 [6, 6]</summary>
    public string GlassThicknessesJson { get; set; } = "[]";

    /// <summary>各层胶片厚度 JSON, 如 [1.14]</summary>
    public string InterlayerThicknessesJson { get; set; } = "[]";

    /// <summary>等效厚度 (mm)</summary>
    public double? EquivalentThickness { get; set; }

    public int? GlassMaterialId { get; set; }

    [ForeignKey(nameof(GlassMaterialId))]
    public GlassMaterial? GlassMaterial { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(500)] public string? Remark { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

#endregion

#region ===== 中空玻璃配置 (已有，补全) =====

public class InsulatingGlassConfig
{
    [Key] public int Id { get; set; }

    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;

    /// <summary>配置，如 "6+12A+6"</summary>
    [Required, MaxLength(200)] public string Configuration { get; set; } = string.Empty;

    /// <summary>外片厚度 (mm)</summary>
    [Required] public double OuterThickness { get; set; }

    /// <summary>内片厚度 (mm)</summary>
    [Required] public double InnerThickness { get; set; }

    /// <summary>空气层厚度 (mm)</summary>
    [Required] public double AirSpaceThickness { get; set; }

    /// <summary>填充气体 (Air / Argon / Krypton)</summary>
    public string GasType { get; set; } = "Air";

    /// <summary>传热系数 K (W/m²·K)</summary>
    public double? ThermalTransmittance { get; set; }

    public int? OuterGlassMaterialId { get; set; }
    public int? InnerGlassMaterialId { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(500)] public string? Remark { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

#endregion

#region ===== 技术规范 (已有，补全) =====

public class TechnicalStandard
{
    [Key] public int Id { get; set; }

    /// <summary>规范编号 (如 "JGJ 102-2003")</summary>
    [Required, MaxLength(50)] public string Code { get; set; } = string.Empty;

    /// <summary>规范名称</summary>
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;

    /// <summary>分类 (Glass / Aluminum / Stone / Structure)</summary>
    [Required, MaxLength(50)] public string Category { get; set; } = string.Empty;

    /// <summary>发布年份</summary>
    public int? PublishYear { get; set; }

    /// <summary>是否现行有效</summary>
    public bool IsCurrent { get; set; } = true;

    /// <summary>替代规范编号</summary>
    [MaxLength(50)] public string? ReplacedBy { get; set; }

    [MaxLength(1000)] public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

#endregion

#region ===== 新增：铝型材规格 =====

public class AluminumProfile
{
    [Key] public int Id { get; set; }

    /// <summary>型材编号</summary>
    [Required, MaxLength(50)] public string ProfileCode { get; set; } = string.Empty;

    /// <summary>型材规格 (如 "100×50×3")</summary>
    [Required, MaxLength(100)] public string Specification { get; set; } = string.Empty;

    /// <summary>用途 (Mullion / Rafter / Frame)</summary>
    [Required, MaxLength(50)] public string Usage { get; set; } = string.Empty;

    /// <summary>铝合金牌号 (6063-T5 / 6061-T6)</summary>
    [Required, MaxLength(50)] public string AluminumGrade { get; set; } = "6063-T5";

    /// <summary>截面面积 (cm²)</summary>
    public double SectionArea { get; set; }

    /// <summary>惯性矩 Ix (cm⁴)</summary>
    public double MomentOfInertiaX { get; set; }

    /// <summary>惯性矩 Iy (cm⁴)</summary>
    public double MomentOfInertiaY { get; set; }

    /// <summary>截面模量 Wx (cm³)</summary>
    public double SectionModulusX { get; set; }

    /// <summary>截面模量 Wy (cm³)</summary>
    public double SectionModulusY { get; set; }

    /// <summary>回转半径 ix (cm)</summary>
    public double RadiusOfGyrationX { get; set; }

    /// <summary>回转半径 iy (cm)</summary>
    public double RadiusOfGyrationY { get; set; }

    /// <summary>线密度 (kg/m)</summary>
    public double LinearDensity { get; set; }

    /// <summary>容许应力 (MPa)</summary>
    public double AllowableStress { get; set; }

    /// <summary>弹性模量 E (MPa)，默认70000</summary>
    public double ElasticModulus { get; set; } = 70000;

    public bool IsActive { get; set; } = true;

    [MaxLength(500)] public string? Remark { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

#endregion

#region ===== 新增：石材材质 =====

public class StoneMaterial
{
    [Key] public int Id { get; set; }

    /// <summary>石材名称</summary>
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;

    /// <summary>石材种类 (Granite / Marble / Limestone / Sandstone)</summary>
    [Required, MaxLength(50)] public string StoneType { get; set; } = string.Empty;

    /// <summary>产地</summary>
    [MaxLength(100)] public string? Origin { get; set; }

    /// <summary>容重 (kN/m³)</summary>
    [Required] public double Density { get; set; }

    /// <summary>抗弯强度 (MPa)</summary>
    [Required] public double FlexuralStrength { get; set; }

    /// <summary>抗压强度 (MPa)</summary>
    public double CompressiveStrength { get; set; }

    /// <summary>弹性模量 (MPa)</summary>
    public double ElasticModulus { get; set; }

    /// <summary>吸水率 (%)</summary>
    public double WaterAbsorption { get; set; }

    /// <summary>干燥/饱和系数</summary>
    public double DrySaturationCoefficient { get; set; } = 0.8;

    public bool IsActive { get; set; } = true;

    [MaxLength(500)] public string? Remark { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

#endregion

#region ===== 新增：结构胶参数 =====

public class SiliconeParameter
{
    [Key] public int Id { get; set; }

    /// <summary>品牌</summary>
    [Required, MaxLength(100)] public string Brand { get; set; } = string.Empty;

    /// <summary>型号</summary>
    [Required, MaxLength(100)] public string Model { get; set; } = string.Empty;

    /// <summary>类型 (Structural / Weathering)</summary>
    [Required, MaxLength(50)] public string Type { get; set; } = "Structural";

    /// <summary>设计强度 σd (MPa)，结构胶默认0.14</summary>
    [Required] public double DesignStrength { get; set; } = 0.14;

    /// <summary>拉伸粘结强度 (MPa)</summary>
    public double TensileBondStrength { get; set; }

    /// <summary>剪切强度 (MPa)</summary>
    public double ShearStrength { get; set; }

    /// <summary>拉伸模量 (MPa)</summary>
    public double TensileModulus { get; set; }

    /// <summary>极限伸长率 (%)</summary>
    public double ElongationAtBreak { get; set; }

    /// <summary>适用温度范围</summary>
    [MaxLength(50)] public string? TemperatureRange { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(500)] public string? Remark { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

#endregion

#region ===== 新增：预埋件规格 =====

public class EmbedPartSpec
{
    [Key] public int Id { get; set; }

    /// <summary>预埋件编号</summary>
    [Required, MaxLength(50)] public string Code { get; set; } = string.Empty;

    /// <summary>类型 (Channel / Plate / Bolt)</summary>
    [Required, MaxLength(50)] public string EmbedType { get; set; } = "Plate";

    /// <summary>锚板尺寸描述 (如 "200×150×10")</summary>
    [Required, MaxLength(100)] public string PlateDimension { get; set; } = string.Empty;

    /// <summary>锚筋直径 (mm)</summary>
    public double AnchorDiameter { get; set; }

    /// <summary>锚筋数量</summary>
    public int AnchorCount { get; set; }

    /// <summary>锚筋长度 (mm)</summary>
    public double AnchorLength { get; set; }

    /// <summary>锚板材质 (Q235B / Q345B)</summary>
    [MaxLength(50)] public string PlateMaterial { get; set; } = "Q235B";

    /// <summary>锚筋材质 (HPB300 / HRB400)</summary>
    [MaxLength(50)] public string AnchorMaterial { get; set; } = "HRB400";

    /// <summary>适用混凝土等级 (最低)</summary>
    [MaxLength(20)] public string MinConcreteGrade { get; set; } = "C20";

    /// <summary>受拉承载力设计值 (kN)</summary>
    public double TensionCapacity { get; set; }

    /// <summary>受剪承载力设计值 (kN)</summary>
    public double ShearCapacity { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(500)] public string? Remark { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

#endregion
