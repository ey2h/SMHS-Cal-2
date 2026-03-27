using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GlassManagement.Core.Data;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

/// <summary>夹胶玻璃配置管理</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("数据管理 - 夹胶玻璃")]
public class LaminatedGlassController : ControllerBase
{
    private readonly GlassDbContext _db;
    public LaminatedGlassController(GlassDbContext db) => _db = db;

    [HttpGet]
    [ProducesResponseType(typeof(List<LaminatedGlassConfig>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<LaminatedGlassConfig>>> GetAll()
        => Ok(await _db.LaminatedGlassConfigs.Include(x => x.GlassMaterial).ToListAsync());

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(LaminatedGlassConfig), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LaminatedGlassConfig>> GetById(int id)
    {
        var item = await _db.LaminatedGlassConfigs.Include(x => x.GlassMaterial).FirstOrDefaultAsync(x => x.Id == id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [ProducesResponseType(typeof(LaminatedGlassConfig), StatusCodes.Status201Created)]
    public async Task<ActionResult<LaminatedGlassConfig>> Create([FromBody] LaminatedGlassConfig entity)
    {
        entity.CreatedAt = entity.UpdatedAt = DateTime.UtcNow;
        _db.LaminatedGlassConfigs.Add(entity);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] LaminatedGlassConfig entity)
    {
        var existing = await _db.LaminatedGlassConfigs.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Name = entity.Name;
        existing.Configuration = entity.Configuration;
        existing.InterlayerType = entity.InterlayerType;
        existing.GlassThicknessesJson = entity.GlassThicknessesJson;
        existing.InterlayerThicknessesJson = entity.InterlayerThicknessesJson;
        existing.EquivalentThickness = entity.EquivalentThickness;
        existing.GlassMaterialId = entity.GlassMaterialId;
        existing.IsActive = entity.IsActive;
        existing.Remark = entity.Remark;
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.LaminatedGlassConfigs.FindAsync(id);
        if (item is null) return NotFound();
        _db.LaminatedGlassConfigs.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
