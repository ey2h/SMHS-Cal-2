using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GlassManagement.Api.Models;

/// <summary>使用记录</summary>
public class UsageLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int UserId { get; set; }

    public DateTime LoginAt { get; set; } = DateTime.UtcNow;

    public DateTime? LogoutAt { get; set; }

    [MaxLength(100)]
    public string IpAddress { get; set; } = "";

    /// <summary>在线时长（分钟）</summary>
    public int DurationMinutes { get; set; }

    [ForeignKey(nameof(UserId))]
    public AuthUser? User { get; set; }
}
