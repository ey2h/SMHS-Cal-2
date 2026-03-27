using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GlassManagement.Api.Data;
using GlassManagement.Api.Models;
using GlassManagement.Api.Services;

namespace GlassManagement.Api.Controllers;

/// <summary>管理员用户管理API</summary>
[ApiController]
[Route("api/admin")]
[Tags("管理员")]
[Authorize(Roles = "admin")]
public class AdminController : ControllerBase
{
    private readonly UserDbContext _db;
    private readonly AuthService _auth;

    public AdminController(UserDbContext db, AuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    /// <summary>用户列表（分页）</summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = _db.AuthUsers.AsNoTracking().OrderByDescending(u => u.Id);
        var total = await query.CountAsync();
        var users = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = users.Select(u => _auth.ToDto(u)).ToList();

        return Ok(new UserListResponse { Items = items, Total = total, Page = page, PageSize = pageSize });
    }

    /// <summary>创建用户</summary>
    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] AdminCreateUserRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || req.Username.Length < 3)
            return BadRequest(new { error = "用户名至少3个字符" });
        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
            return BadRequest(new { error = "密码至少6个字符" });
        if (await _db.AuthUsers.AnyAsync(u => u.Username == req.Username))
            return BadRequest(new { error = "用户名已存在" });

        var user = new AuthUser
        {
            Username = req.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Salt = "",
            DisplayName = req.DisplayName ?? req.Username,
            Email = req.Email,
            Phone = req.Phone,
            Company = req.Company,
            Role = req.Role ?? "user",
            LicenseExpiry = req.LicenseExpiry,
            LicenseStart = req.LicenseStart ?? DateTime.UtcNow,
            MaxProjects = req.MaxProjects,
            IsActive = true,
            ApiToken = AuthService.GenerateApiToken()
        };

        _db.AuthUsers.Add(user);
        await _db.SaveChangesAsync();
        return Ok(new { message = "用户创建成功", userId = user.Id, apiToken = user.ApiToken });
    }

    /// <summary>编辑用户信息</summary>
    [HttpPut("users/{id}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] AdminUpdateUserRequest req)
    {
        var user = await _db.AuthUsers.FindAsync(id);
        if (user == null) return NotFound(new { error = "用户不存在" });

        user.DisplayName = req.DisplayName ?? user.DisplayName;
        user.Email = req.Email ?? user.Email;
        user.Phone = req.Phone ?? user.Phone;
        user.Company = req.Company ?? user.Company;
        user.Notes = req.Notes ?? user.Notes;
        user.MaxProjects = req.MaxProjects;

        await _db.SaveChangesAsync();
        return Ok(new { message = "用户信息已更新" });
    }

    /// <summary>设置授权时限</summary>
    [HttpPut("users/{id}/license")]
    public async Task<IActionResult> SetLicense(int id, [FromBody] SetLicenseRequest req)
    {
        var user = await _db.AuthUsers.FindAsync(id);
        if (user == null) return NotFound(new { error = "用户不存在" });

        user.LicenseExpiry = req.LicenseExpiry;
        await _db.SaveChangesAsync();
        return Ok(new { message = req.LicenseExpiry.HasValue ? $"授权到期时间已设置为 {req.LicenseExpiry.Value:yyyy-MM-dd}" : "已设置为永久授权" });
    }

    /// <summary>启用/禁用用户</summary>
    [HttpPut("users/{id}/toggle")]
    public async Task<IActionResult> ToggleUser(int id)
    {
        var user = await _db.AuthUsers.FindAsync(id);
        if (user == null) return NotFound(new { error = "用户不存在" });

        user.IsActive = !user.IsActive;
        await _db.SaveChangesAsync();
        return Ok(new { message = user.IsActive ? "用户已启用" : "用户已禁用", isActive = user.IsActive });
    }

    /// <summary>重新生成Token</summary>
    [HttpPut("users/{id}/regenerate-token")]
    public async Task<IActionResult> RegenerateToken(int id)
    {
        var user = await _db.AuthUsers.FindAsync(id);
        if (user == null) return NotFound(new { error = "用户不存在" });

        user.ApiToken = AuthService.GenerateApiToken();
        await _db.SaveChangesAsync();
        return Ok(new RegenerateTokenResponse { NewToken = user.ApiToken });
    }

    /// <summary>查看用户使用统计</summary>
    [HttpGet("users/{id}/usage")]
    public async Task<IActionResult> GetUserUsage(int id, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var user = await _db.AuthUsers.FindAsync(id);
        if (user == null) return NotFound(new { error = "用户不存在" });

        var stats = await _auth.GetUsageStats(id, from, to);
        return Ok(stats);
    }

    /// <summary>所有用户使用汇总</summary>
    [HttpGet("usage/summary")]
    public async Task<IActionResult> GetUsageSummary([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var users = await _db.AuthUsers.AsNoTracking().ToListAsync();
        var result = new UsageSummaryResponse();
        foreach (var u in users)
        {
            var stats = await _auth.GetUsageStats(u.Id, from, to);
            result.Users.Add(stats);
        }
        return Ok(result);
    }

    /// <summary>删除用户</summary>
    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _db.AuthUsers.FindAsync(id);
        if (user == null) return NotFound(new { error = "用户不存在" });
        if (user.Username == "admin") return BadRequest(new { error = "不能删除默认管理员" });

        _db.AuthUsers.Remove(user);
        await _db.SaveChangesAsync();
        return Ok(new { message = "用户已删除" });
    }

    /// <summary>所有用户的Revit提交记录</summary>
    [HttpGet("revit/submissions")]
    public async Task<IActionResult> GetRevitSubmissions([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = _db.RevitSubmissions.AsNoTracking().OrderByDescending(s => s.SubmittedAt);
        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id, s.UserId, s.ProjectName, s.RevitVersion, s.FileName,
                s.ExtractedAt, s.SubmittedAt, s.Status,
                s.PanelCount, s.MullionCount, s.ConnectionCount, s.EmbedCount,
                TotalElements = s.PanelCount + s.MullionCount + s.ConnectionCount + s.EmbedCount
            })
            .ToListAsync();

        return Ok(new { items, total, page, pageSize });
    }
}
