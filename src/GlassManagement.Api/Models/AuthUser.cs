using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GlassManagement.Api.Models;

/// <summary>认证用户实体（独立用户数据库）</summary>
public class AuthUser
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Username { get; set; } = "";

    [Required, MaxLength(256)]
    public string PasswordHash { get; set; } = "";

    [MaxLength(256)]
    public string Salt { get; set; } = "";

    [MaxLength(100)]
    public string? DisplayName { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    /// <summary>角色：admin / user</summary>
    [Required, MaxLength(20)]
    public string Role { get; set; } = "user";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>授权到期时间，null=永久</summary>
    public DateTime? LicenseExpiry { get; set; }

    /// <summary>公司名称</summary>
    [MaxLength(200)]
    public string? Company { get; set; }

    /// <summary>备注</summary>
    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>最大项目数</summary>
    public int MaxProjects { get; set; } = 10;

    /// <summary>唯一 API Token（32位hex）</summary>
    [MaxLength(64)]
    public string? ApiToken { get; set; }

    /// <summary>授权开始时间</summary>
    public DateTime? LicenseStart { get; set; }

    /// <summary>最后登录 IP</summary>
    [MaxLength(100)]
    public string? LastLoginIp { get; set; }
}
