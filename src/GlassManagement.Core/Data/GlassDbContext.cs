using Microsoft.EntityFrameworkCore;
using GlassManagement.Core.Models;

namespace GlassManagement.Core.Data;

public class GlassDbContext : DbContext
{
    public GlassDbContext(DbContextOptions<GlassDbContext> options) : base(options) { }

    // ===== 用户与项目（多用户数据隔离） =====
    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<CalculationRecord> CalculationRecords => Set<CalculationRecord>();

    // ===== 玻璃相关 =====
    public DbSet<GlassMaterial> GlassMaterials => Set<GlassMaterial>();
    public DbSet<SingleGlassConfig> SingleGlassConfigs => Set<SingleGlassConfig>();
    public DbSet<LaminatedGlassConfig> LaminatedGlassConfigs => Set<LaminatedGlassConfig>();
    public DbSet<InsulatingGlassConfig> InsulatingGlassConfigs => Set<InsulatingGlassConfig>();

    // ===== 铝型材 =====
    public DbSet<AluminumProfile> AluminumProfiles => Set<AluminumProfile>();

    // ===== 石材 =====
    public DbSet<StoneMaterial> StoneMaterials => Set<StoneMaterial>();

    // ===== 结构胶 =====
    public DbSet<SiliconeParameter> SiliconeParameters => Set<SiliconeParameter>();

    // ===== 预埋件 =====
    public DbSet<EmbedPartSpec> EmbedPartSpecs => Set<EmbedPartSpec>();

    // ===== 技术规范 =====
    public DbSet<TechnicalStandard> TechnicalStandards => Set<TechnicalStandard>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 用户 - 用户名唯一
        modelBuilder.Entity<User>()
            .HasIndex(e => e.Username)
            .IsUnique();

        // 项目 - 用户+项目关系
        modelBuilder.Entity<Project>()
            .HasOne(p => p.User)
            .WithMany(u => u.Projects)
            .HasForeignKey(p => p.UserId);

        // 计算记录 - 项目关系
        modelBuilder.Entity<CalculationRecord>()
            .HasOne(c => c.Project)
            .WithMany(p => p.CalculationRecords)
            .HasForeignKey(c => c.ProjectId);

        // 玻璃材质 - 唯一索引
        modelBuilder.Entity<GlassMaterial>()
            .HasIndex(e => new { e.GlassType, e.StrengthLevel })
            .IsUnique();

        // 铝型材 - 编号唯一
        modelBuilder.Entity<AluminumProfile>()
            .HasIndex(e => e.ProfileCode)
            .IsUnique();

        // 石材 - 名称唯一
        modelBuilder.Entity<StoneMaterial>()
            .HasIndex(e => e.Name)
            .IsUnique();

        // 结构胶 - 品牌+型号唯一
        modelBuilder.Entity<SiliconeParameter>()
            .HasIndex(e => new { e.Brand, e.Model })
            .IsUnique();

        // 预埋件 - 编号唯一
        modelBuilder.Entity<EmbedPartSpec>()
            .HasIndex(e => e.Code)
            .IsUnique();

        // 规范 - 编号唯一
        modelBuilder.Entity<TechnicalStandard>()
            .HasIndex(e => e.Code)
            .IsUnique();

        // ===== 种子数据 =====
        modelBuilder.Entity<GlassMaterial>().HasData(
            new GlassMaterial { Id = 1, Name = "浮法玻璃(<12mm)", GlassType = "Float", StrengthLevel = "Normal", DesignStrength = 28, ElasticModulus = 72000, PoissonRatio = 0.2, Density = 2500 },
            new GlassMaterial { Id = 2, Name = "钢化玻璃(<12mm)", GlassType = "Tempered", StrengthLevel = "Normal", DesignStrength = 84, ElasticModulus = 72000, PoissonRatio = 0.2, Density = 2500 },
            new GlassMaterial { Id = 3, Name = "半钢化玻璃(<12mm)", GlassType = "SemiTempered", StrengthLevel = "Normal", DesignStrength = 41, ElasticModulus = 72000, PoissonRatio = 0.2, Density = 2500 },
            new GlassMaterial { Id = 4, Name = "浮法玻璃(≥12mm)", GlassType = "Float", StrengthLevel = "Thick", DesignStrength = 24, ElasticModulus = 72000, PoissonRatio = 0.2, Density = 2500 },
            new GlassMaterial { Id = 5, Name = "钢化玻璃(≥12mm)", GlassType = "Tempered", StrengthLevel = "Thick", DesignStrength = 72, ElasticModulus = 72000, PoissonRatio = 0.2, Density = 2500 },
            new GlassMaterial { Id = 6, Name = "半钢化玻璃(≥12mm)", GlassType = "SemiTempered", StrengthLevel = "Thick", DesignStrength = 38, ElasticModulus = 72000, PoissonRatio = 0.2, Density = 2500 }
        );

        modelBuilder.Entity<TechnicalStandard>().HasData(
            new TechnicalStandard { Id = 1, Code = "JGJ 102-2003", Name = "玻璃幕墙工程技术规范", Category = "Glass" },
            new TechnicalStandard { Id = 2, Code = "JGJ 133-2001", Name = "金属与石材幕墙工程技术规范", Category = "Stone" },
            new TechnicalStandard { Id = 3, Code = "GB 50009-2012", Name = "建筑结构荷载规范", Category = "Structure" },
            new TechnicalStandard { Id = 4, Code = "GB 55001-2021", Name = "工程结构通用规范", Category = "Structure" },
            new TechnicalStandard { Id = 5, Code = "GB 50011-2010", Name = "建筑抗震设计规范", Category = "Structure" },
            new TechnicalStandard { Id = 6, Code = "JGJ/T 151-2008", Name = "建筑门窗玻璃幕墙热工计算规程", Category = "Glass" }
        );

        modelBuilder.Entity<SiliconeParameter>().HasData(
            new SiliconeParameter { Id = 1, Brand = "白云", Model = "SS811", Type = "Structural", DesignStrength = 0.14, TensileBondStrength = 0.18, ShearStrength = 0.14 },
            new SiliconeParameter { Id = 2, Brand = "之江", Model = "JH8008", Type = "Structural", DesignStrength = 0.14, TensileBondStrength = 0.18, ShearStrength = 0.14 },
            new SiliconeParameter { Id = 3, Brand = "陶氏", Model = "DC993", Type = "Structural", DesignStrength = 0.14, TensileBondStrength = 0.20, ShearStrength = 0.16 }
        );

        modelBuilder.Entity<StoneMaterial>().HasData(
            new StoneMaterial { Id = 1, Name = "花岗岩", StoneType = "Granite", Density = 27, CompressiveStrength = 100, ElasticModulus = 40000, WaterAbsorption = 0.5 },
            new StoneMaterial { Id = 2, Name = "大理石", StoneType = "Marble", Density = 26, CompressiveStrength = 70, ElasticModulus = 30000, WaterAbsorption = 0.8 },
            new StoneMaterial { Id = 3, Name = "砂岩", StoneType = "Sandstone", Density = 23, CompressiveStrength = 40, ElasticModulus = 15000, WaterAbsorption = 3.0 },
            new StoneMaterial { Id = 4, Name = "石灰石", StoneType = "Limestone", Density = 24, CompressiveStrength = 50, ElasticModulus = 20000, WaterAbsorption = 1.5 }
        );
    }
}
