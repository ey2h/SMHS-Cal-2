using Microsoft.EntityFrameworkCore;
using GlassManagement.Api.Models;

namespace GlassManagement.Api.Data;

/// <summary>独立用户数据库上下文 — SMHS_User.db</summary>
public class UserDbContext : DbContext
{
    public UserDbContext(DbContextOptions<UserDbContext> options) : base(options) { }

    public DbSet<AuthUser> AuthUsers => Set<AuthUser>();
    public DbSet<UsageLog> UsageLogs => Set<UsageLog>();
    public DbSet<RevitSubmission> RevitSubmissions => Set<RevitSubmission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuthUser>(e =>
        {
            e.ToTable("Users");
            e.HasIndex(u => u.Username).IsUnique();
            e.HasIndex(u => u.ApiToken).IsUnique();
        });

        modelBuilder.Entity<UsageLog>(e =>
        {
            e.ToTable("UsageLogs");
            e.HasIndex(l => l.UserId);
            e.HasIndex(l => l.LoginAt);
        });

        modelBuilder.Entity<RevitSubmission>(e =>
        {
            e.ToTable("RevitSubmissions");
            e.HasIndex(s => s.UserId);
        });
    }
}
