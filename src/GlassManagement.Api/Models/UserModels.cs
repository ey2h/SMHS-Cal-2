using System.ComponentModel.DataAnnotations;

namespace GlassManagement.Api.Models;

// ===== DTO模型（实体类在 GlassManagement.Core.Models） =====

/// <summary>创建用户请求</summary>
public class CreateUserRequest
{
    [Required]
    public string Username { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
}

/// <summary>创建项目请求</summary>
public class CreateProjectRequest
{
    [Required]
    public string Name { get; set; } = "";
    public string? Code { get; set; }
    public string? Description { get; set; }
    public string? City { get; set; }
    public double BuildingHeight { get; set; }
    public string? TerrainCategory { get; set; }
    public int SeismicIntensity { get; set; }
    public double BasicWindPressure { get; set; }
}

/// <summary>更新项目请求</summary>
public class UpdateProjectRequest
{
    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
    public string? City { get; set; }
    public double? BuildingHeight { get; set; }
    public string? TerrainCategory { get; set; }
    public int? SeismicIntensity { get; set; }
    public double? BasicWindPressure { get; set; }
    public string? Status { get; set; }
}
