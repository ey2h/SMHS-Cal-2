using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GlassManagement.Core.Models;

/// <summary>用户实体</summary>
public class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>用户名（唯一）</summary>
    [Required, MaxLength(50)]
    public string Username { get; set; } = "";

    /// <summary>显示名称</summary>
    [MaxLength(100)]
    public string? DisplayName { get; set; }

    /// <summary>邮箱</summary>
    [MaxLength(200)]
    public string? Email { get; set; }

    /// <summary>密码哈希</summary>
    [MaxLength(256)]
    public string? PasswordHash { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>关联项目列表</summary>
    public List<Project> Projects { get; set; } = new();
}

/// <summary>项目实体</summary>
public class Project
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>所属用户ID</summary>
    public int UserId { get; set; }

    /// <summary>项目名称</summary>
    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    /// <summary>项目编号</summary>
    [MaxLength(50)]
    public string? Code { get; set; }

    /// <summary>项目描述</summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>所在城市</summary>
    [MaxLength(50)]
    public string? City { get; set; }

    /// <summary>建筑高度(m)</summary>
    public double BuildingHeight { get; set; }

    /// <summary>地面粗糙度类别</summary>
    [MaxLength(10)]
    public string? TerrainCategory { get; set; }

    /// <summary>抗震设防烈度</summary>
    public int SeismicIntensity { get; set; }

    /// <summary>基本风压(Pa)</summary>
    public double BasicWindPressure { get; set; }

    /// <summary>项目状态: Draft/InProgress/Completed</summary>
    [MaxLength(20)]
    public string Status { get; set; } = "Draft";

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>更新时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>关联用户</summary>
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    /// <summary>关联计算记录</summary>
    public List<CalculationRecord> CalculationRecords { get; set; } = new();
}

/// <summary>计算类型枚举</summary>
public enum CalcType
{
    WindLoad = 1,
    Seismic = 2,
    GlassStrength = 3,
    GlassDeflection = 4,
    Column = 5,
    Beam = 6,
    Connection = 7,
    Silicone = 8,
    Report = 9
}

/// <summary>计算记录实体</summary>
public class CalculationRecord
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>所属项目ID</summary>
    public int ProjectId { get; set; }

    /// <summary>计算类型</summary>
    public CalcType CalcType { get; set; }

    /// <summary>输入参数JSON</summary>
    public string? InputJson { get; set; }

    /// <summary>输出结果JSON</summary>
    public string? OutputJson { get; set; }

    /// <summary>计算状态: Pending/Success/Failed</summary>
    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>更新时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>关联项目</summary>
    [ForeignKey(nameof(ProjectId))]
    public Project? Project { get; set; }
}
