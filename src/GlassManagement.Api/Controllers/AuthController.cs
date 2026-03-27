using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GlassManagement.Api.Models;
using GlassManagement.Api.Services;

namespace GlassManagement.Api.Controllers;

/// <summary>认证API</summary>
[ApiController]
[Route("api/[controller]")]
[Tags("认证")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth) => _auth = auth;

    /// <summary>登录</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var (result, error) = await _auth.LoginAsync(req, ip);
        if (result == null)
            return Unauthorized(new { error = error ?? "用户名或密码错误" });
        return Ok(result);
    }

    /// <summary>Token登录</summary>
    [HttpPost("token-login")]
    [AllowAnonymous]
    public async Task<IActionResult> TokenLogin([FromBody] TokenLoginRequest req)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var (result, error) = await _auth.LoginByTokenAsync(req.ApiToken, ip);
        if (result == null)
            return Unauthorized(new { error = error ?? "无效的 API Token" });
        return Ok(result);
    }

    /// <summary>登出</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _auth.RecordLogout(userId);
        return Ok(new { message = "已登出" });
    }

    /// <summary>当前用户使用统计</summary>
    [HttpGet("usage")]
    [Authorize]
    public async Task<IActionResult> GetUsage([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var stats = await _auth.GetUsageStats(userId, from, to);
        return Ok(stats);
    }

    /// <summary>注册新用户</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        var (success, error, user) = await _auth.RegisterAsync(req);
        if (!success)
            return BadRequest(new { error });
        return Ok(new { message = "注册成功", user });
    }

    /// <summary>获取当前用户信息</summary>
    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var profile = await _auth.GetProfileAsync(userId);
        if (profile == null) return NotFound();
        return Ok(profile);
    }

    /// <summary>脉冲检测</summary>
    [HttpGet("pulse")]
    [Authorize]
    public async Task<IActionResult> Pulse([FromQuery] string? lastPulseId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var expClaim = User.FindFirstValue("exp");
        var tokenExpiry = expClaim != null
            ? DateTimeOffset.FromUnixTimeSeconds(long.Parse(expClaim)).UtcDateTime
            : DateTime.UtcNow;

        var result = await _auth.PulseAsync(userId, tokenExpiry);
        if (result == null)
            return Unauthorized(new { error = "登录已过期或用户已被禁用" });

        return Ok(result);
    }

    /// <summary>修改密码</summary>
    [HttpPut("password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (success, error) = await _auth.ChangePasswordAsync(userId, req);
        if (!success) return BadRequest(new { error });
        return Ok(new { message = "密码修改成功" });
    }
}
