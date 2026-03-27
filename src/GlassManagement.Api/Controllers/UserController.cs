using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GlassManagement.Core.Data;
using GlassManagement.Api.Models;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

/// <summary>用户管理API</summary>
[ApiController]
[Route("api/[controller]")]
[Tags("用户管理")]
public class UserController : ControllerBase
{
    private readonly GlassDbContext _db;

    public UserController(GlassDbContext db) => _db = db;

    /// <summary>获取所有用户</summary>
    [HttpGet]
    public async Task<ActionResult<List<User>>> GetAll()
    {
        return await _db.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();
    }

    /// <summary>根据ID获取用户</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<User>> GetById(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound(new { error = "用户不存在" });
        return user;
    }

    /// <summary>创建用户</summary>
    [HttpPost]
    public async Task<ActionResult<User>> Create([FromBody] CreateUserRequest req)
    {
        // 检查用户名唯一
        if (await _db.Users.AnyAsync(u => u.Username == req.Username))
            return Conflict(new { error = "用户名已存在" });

        var user = new User
        {
            Username = req.Username,
            DisplayName = req.DisplayName,
            Email = req.Email,
            PasswordHash = req.Password // 简单存储，后续可改为哈希
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }

    /// <summary>删除用户</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
