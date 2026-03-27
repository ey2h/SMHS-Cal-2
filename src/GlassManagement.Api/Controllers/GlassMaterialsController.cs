using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GlassManagement.Core.Data;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

/// <summary>玻璃材质管理</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("数据管理 - 玻璃材质")]
public class GlassMaterialsController : ControllerBase
{
    private readonly GlassDbContext _db;
    public GlassMaterialsController(GlassDbContext db) => _db = db;

    /// <summary>获取所有玻璃材质</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<GlassMaterial>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<GlassMaterial>>> GetAll()
        => Ok(await _db.GlassMaterials.OrderBy(x => x.GlassType).ToListAsync());

    /// <summary>根据Id获取玻璃材质</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(GlassMaterial), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GlassMaterial>> GetById(int id)
    {
        var item = await _db.GlassMaterials.FindAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>新增玻璃材质</summary>
    [HttpPost]
    [ProducesResponseType(typeof(GlassMaterial), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GlassMaterial>> Create([FromBody] GlassMaterial entity)
    {
        entity.CreatedAt = entity.UpdatedAt = DateTime.UtcNow;
        _db.GlassMaterials.Add(entity);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }

    /// <summary>更新玻璃材质</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] GlassMaterial entity)
    {
        var existing = await _db.GlassMaterials.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Name = entity.Name;
        existing.GlassType = entity.GlassType;
        existing.StrengthLevel = entity.StrengthLevel;
        existing.DesignStrength = entity.DesignStrength;
        existing.UltimateStrength = entity.UltimateStrength;
        existing.ElasticModulus = entity.ElasticModulus;
        existing.PoissonRatio = entity.PoissonRatio;
        existing.Density = entity.Density;
        existing.Remark = entity.Remark;
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>删除玻璃材质</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.GlassMaterials.FindAsync(id);
        if (item is null) return NotFound();
        _db.GlassMaterials.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
