using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GlassManagement.Core.Data;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

/// <summary>单片玻璃配置管理</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("数据管理 - 单片玻璃")]
public class SingleGlassController : ControllerBase
{
    private readonly GlassDbContext _db;
    public SingleGlassController(GlassDbContext db) => _db = db;

    /// <summary>获取所有单片玻璃配置</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<SingleGlassConfig>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SingleGlassConfig>>> GetAll()
        => Ok(await _db.SingleGlassConfigs.Include(x => x.GlassMaterial).OrderBy(x => x.NominalThickness).ToListAsync());

    /// <summary>根据Id获取单片玻璃配置</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(SingleGlassConfig), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SingleGlassConfig>> GetById(int id)
    {
        var item = await _db.SingleGlassConfigs.Include(x => x.GlassMaterial).FirstOrDefaultAsync(x => x.Id == id);
        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>新增单片玻璃配置</summary>
    [HttpPost]
    [ProducesResponseType(typeof(SingleGlassConfig), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SingleGlassConfig>> Create([FromBody] SingleGlassConfig entity)
    {
        entity.CreatedAt = entity.UpdatedAt = DateTime.UtcNow;
        _db.SingleGlassConfigs.Add(entity);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }

    /// <summary>更新单片玻璃配置</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] SingleGlassConfig entity)
    {
        var existing = await _db.SingleGlassConfigs.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Name = entity.Name;
        existing.NominalThickness = entity.NominalThickness;
        existing.MinThickness = entity.MinThickness;
        existing.GlassMaterialId = entity.GlassMaterialId;
        existing.MaxArea = entity.MaxArea;
        existing.IsActive = entity.IsActive;
        existing.Remark = entity.Remark;
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>删除单片玻璃配置</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.SingleGlassConfigs.FindAsync(id);
        if (item is null) return NotFound();
        _db.SingleGlassConfigs.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
