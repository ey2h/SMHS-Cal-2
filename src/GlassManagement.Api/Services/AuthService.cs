using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using GlassManagement.Api.Data;
using GlassManagement.Api.Models;

namespace GlassManagement.Api.Services;

public class AuthService
{
    private readonly UserDbContext _db;
    private readonly IConfiguration _config;

    private const string DefaultJwtKey = "SMHS_GlassManagement_JwtSecret_2024_SuperLongKeyForSecurity!";

    public AuthService(UserDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public string JwtKey => _config["Jwt:Key"] ?? DefaultJwtKey;
    public string JwtIssuer => _config["Jwt:Issuer"] ?? "GlassManagement";

    public static string GenerateApiToken() => Guid.NewGuid().ToString("N");

    public async Task<(LoginResponse? Response, string? Error)> LoginAsync(LoginRequest req, string? ipAddress = null)
    {
        var user = await _db.AuthUsers.FirstOrDefaultAsync(u => u.Username == req.Username && u.IsActive);
        if (user == null) return (null, "用户名或密码错误");

        if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return (null, "用户名或密码错误");

        if (user.LicenseExpiry.HasValue && user.LicenseExpiry.Value < DateTime.UtcNow)
            return (null, "您的授权已过期，请联系管理员续期");

        user.LastLoginAt = DateTime.UtcNow;
        user.LastLoginIp = ipAddress;

        // 强制登出旧session
        await ForceLogoutActiveSessions(user.Id);
        await _db.SaveChangesAsync();

        // 记录登录
        await RecordLogin(user.Id, ipAddress ?? "unknown");

        return (new LoginResponse
        {
            Token = GenerateToken(user),
            User = ToDto(user)
        }, null);
    }

    public async Task<(LoginResponse? Response, string? Error)> LoginByTokenAsync(string apiToken, string? ipAddress = null)
    {
        var user = await _db.AuthUsers.FirstOrDefaultAsync(u => u.ApiToken == apiToken);
        if (user == null) return (null, "无效的 API Token");
        if (!user.IsActive) return (null, "用户已被禁用");
        if (user.LicenseExpiry.HasValue && user.LicenseExpiry.Value < DateTime.UtcNow)
            return (null, "您的授权已过期，请联系管理员续期");

        user.LastLoginAt = DateTime.UtcNow;
        user.LastLoginIp = ipAddress;

        // 强制登出旧session
        await ForceLogoutActiveSessions(user.Id);
        await _db.SaveChangesAsync();

        // 记录登录
        await RecordLogin(user.Id, ipAddress ?? "unknown");

        return (new LoginResponse
        {
            Token = GenerateToken(user),
            User = ToDto(user)
        }, null);
    }

    private async Task ForceLogoutActiveSessions(int userId)
    {
        var activeSessions = await _db.UsageLogs
            .Where(l => l.UserId == userId && l.LogoutAt == null)
            .ToListAsync();
        var now = DateTime.UtcNow;
        foreach (var s in activeSessions)
        {
            s.LogoutAt = now;
            s.DurationMinutes = (int)(now - s.LoginAt).TotalMinutes;
        }
    }

    public async Task RecordLogin(int userId, string ip)
    {
        _db.UsageLogs.Add(new UsageLog
        {
            UserId = userId,
            LoginAt = DateTime.UtcNow,
            IpAddress = ip
        });
        await _db.SaveChangesAsync();
    }

    public async Task RecordLogout(int userId)
    {
        var session = await _db.UsageLogs
            .Where(l => l.UserId == userId && l.LogoutAt == null)
            .OrderByDescending(l => l.LoginAt)
            .FirstOrDefaultAsync();
        if (session != null)
        {
            session.LogoutAt = DateTime.UtcNow;
            session.DurationMinutes = (int)(DateTime.UtcNow - session.LoginAt).TotalMinutes;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<UsageStatDto> GetUsageStats(int userId, DateTime? from, DateTime? to)
    {
        var user = await _db.AuthUsers.FindAsync(userId);
        if (user == null) return new UsageStatDto();

        var query = _db.UsageLogs.Where(l => l.UserId == userId);
        if (from.HasValue) query = query.Where(l => l.LoginAt >= from.Value);
        if (to.HasValue) query = query.Where(l => l.LoginAt <= to.Value);

        var logs = await query.OrderBy(l => l.LoginAt).ToListAsync();

        var dailyStats = logs
            .GroupBy(l => l.LoginAt.ToString("yyyy-MM-dd"))
            .Select(g => new DailyUsageDto
            {
                Date = g.Key,
                Minutes = g.Sum(l => l.DurationMinutes),
                SessionCount = g.Count()
            })
            .ToList();

        return new UsageStatDto
        {
            UserId = userId,
            Username = user.Username,
            DisplayName = user.DisplayName,
            TotalMinutes = logs.Sum(l => l.DurationMinutes),
            SessionCount = logs.Count,
            DailyStats = dailyStats
        };
    }

    public async Task<(bool Success, string Error, AuthUserDto? User)> RegisterAsync(RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || req.Username.Length < 3)
            return (false, "用户名至少3个字符", null);
        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
            return (false, "密码至少6个字符", null);

        if (await _db.AuthUsers.AnyAsync(u => u.Username == req.Username))
            return (false, "用户名已存在", null);

        var user = new AuthUser
        {
            Username = req.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Salt = "",
            DisplayName = req.DisplayName ?? req.Username,
            Email = req.Email,
            Phone = req.Phone,
            Role = "user",
            ApiToken = GenerateApiToken()
        };

        _db.AuthUsers.Add(user);
        await _db.SaveChangesAsync();
        return (true, "", ToDto(user));
    }

    public async Task<AuthUserDto?> GetProfileAsync(int userId)
    {
        var user = await _db.AuthUsers.FindAsync(userId);
        return user == null ? null : ToDto(user);
    }

    public async Task<(bool Success, string Error)> ChangePasswordAsync(int userId, ChangePasswordRequest req)
    {
        var user = await _db.AuthUsers.FindAsync(userId);
        if (user == null) return (false, "用户不存在");

        if (!BCrypt.Net.BCrypt.Verify(req.OldPassword, user.PasswordHash))
            return (false, "原密码错误");

        if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 6)
            return (false, "新密码至少6个字符");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        await _db.SaveChangesAsync();
        return (true, "");
    }

    public string GenerateToken(AuthUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("displayName", user.DisplayName ?? user.Username)
        };
        if (user.LicenseExpiry.HasValue)
            claims.Add(new Claim("licenseExpiry", user.LicenseExpiry.Value.ToString("o")));

        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtIssuer,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>初始化默认管理员账号 + 测试数据</summary>
    public async Task EnsureAdminAsync()
    {
        if (!await _db.AuthUsers.AnyAsync(u => u.Username == "admin"))
        {
            _db.AuthUsers.Add(new AuthUser
            {
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Salt = "",
                DisplayName = "管理员",
                Role = "admin",
                IsActive = true,
                ApiToken = GenerateApiToken()
            });
            await _db.SaveChangesAsync();
        }
        else
        {
            // 确保 admin 有 ApiToken
            var admin = await _db.AuthUsers.FirstAsync(u => u.Username == "admin");
            if (string.IsNullOrEmpty(admin.ApiToken))
            {
                admin.ApiToken = GenerateApiToken();
                await _db.SaveChangesAsync();
            }
        }

        // 种子测试用户
        await SeedTestDataAsync();
    }

    private async Task SeedTestDataAsync()
    {
        var testUsers = new[]
        {
            new { Username = "user1", DisplayName = "张三", Phone = "13800001111", Email = "zhangsan@test.com", LicenseExpiry = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true },
            new { Username = "user2", DisplayName = "李四", Phone = "13800002222", Email = "lisi@test.com", LicenseExpiry = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true },
            new { Username = "user3", DisplayName = "王五", Phone = "13800003333", Email = "wangwu@test.com", LicenseExpiry = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true },
            new { Username = "user4", DisplayName = "赵六", Phone = "13800004444", Email = "zhaoliu@test.com", LicenseExpiry = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc), IsActive = false },
        };

        foreach (var t in testUsers)
        {
            if (!await _db.AuthUsers.AnyAsync(u => u.Username == t.Username))
            {
                _db.AuthUsers.Add(new AuthUser
                {
                    Username = t.Username,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("test123"),
                    Salt = "",
                    DisplayName = t.DisplayName,
                    Phone = t.Phone,
                    Email = t.Email,
                    Role = "user",
                    IsActive = t.IsActive,
                    LicenseExpiry = t.LicenseExpiry,
                    LicenseStart = DateTime.UtcNow,
                    ApiToken = GenerateApiToken()
                });
            }
        }
        await _db.SaveChangesAsync();
    }

    /// <summary>脉冲检测</summary>
    public async Task<PulseResponse?> PulseAsync(int userId, DateTime tokenExpiry)
    {
        var user = await _db.AuthUsers.FindAsync(userId);
        if (user == null || !user.IsActive)
            return null;

        if (user.LicenseExpiry.HasValue && user.LicenseExpiry.Value < DateTime.UtcNow)
            return null;

        var remaining = (int)(tokenExpiry - DateTime.UtcNow).TotalSeconds;
        if (remaining < 0) return null;

        var response = new PulseResponse
        {
            Valid = true,
            RemainingSeconds = remaining,
            PulseId = Guid.NewGuid().ToString("N"),
            LicenseExpiry = user.LicenseExpiry,
            LicenseRemainingDays = user.LicenseExpiry.HasValue
                ? (int)Math.Ceiling((user.LicenseExpiry.Value - DateTime.UtcNow).TotalDays)
                : null
        };

        if (remaining < 7200)
        {
            response.RefreshToken = GenerateToken(user);
        }

        return response;
    }

    public AuthUserDto ToDto(AuthUser u)
    {
        var totalMinutes = _db.UsageLogs.Where(l => l.UserId == u.Id).Sum(l => l.DurationMinutes);
        return new AuthUserDto
        {
            Id = u.Id,
            Username = u.Username,
            DisplayName = u.DisplayName,
            Email = u.Email,
            Phone = u.Phone,
            Role = u.Role,
            LicenseExpiry = u.LicenseExpiry,
            Company = u.Company,
            Notes = u.Notes,
            MaxProjects = u.MaxProjects,
            IsActive = u.IsActive,
            ApiToken = u.ApiToken,
            LicenseStart = u.LicenseStart,
            LastLoginIp = u.LastLoginIp,
            TotalUsageMinutes = totalMinutes
        };
    }
}
