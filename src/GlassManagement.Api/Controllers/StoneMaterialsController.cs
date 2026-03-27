using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GlassManagement.Core.Data;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

/// <summary>石材材质管理</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("数据管理 - 石材")]
public class StoneMaterialsController : ControllerBase
{
    private readonly GlassDbContext _db;
    public StoneMaterialsController(GlassDbContext db) => _db = db;

    /// <summary>获取所有石材</summary>
    /// <param name="stoneType">石材种类: Granite / Marble / Limestone / Sandstone</param>
    [HttpGet]
    [ProducesResponseType(typeof(List<StoneMaterial>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<StoneMaterial>>> GetAll([FromQuery] string? stoneType = null)
    {
        var query = _db.StoneMaterials.Where(x => x.IsActive);
        if (!string.IsNullOrEmpty(stoneType)) query = query.Where(x => x.StoneType == stoneType);
        return Ok(await query.OrderBy(x => x.Name).ToListAsync());
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(StoneMaterial), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StoneMaterial>> GetById(int id)
    {
        var item = await _db.StoneMaterials.FindAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [ProducesResponseType(typeof(StoneMaterial), StatusCodes.Status201Created)]
    public async Task<ActionResult<StoneMaterial>> Create([FromBody] StoneMaterial entity)
    {
        entity.CreatedAt = entity.UpdatedAt = DateTime.UtcNow;
        _db.StoneMaterials.Add(entity);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] StoneMaterial entity)
    {
        var existing = await _db.StoneMaterials.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Name = entity.Name;
        existing.StoneType = entity.StoneType;
        existing.Origin = entity.Origin;
        existing.Density = entity.Density;
        existing.FlexuralStrength = entity.FlexuralStrength;
        existing.CompressiveStrength = entity.CompressiveStrength;
        existing.ElasticModulus = entity.ElasticModulus;
        existing.WaterAbsorption = entity.WaterAbsorption;
        existing.DrySaturationCoefficient = entity.DrySaturationCoefficient;
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
        var item = await _db.StoneMaterials.FindAsync(id);
        if (item is null) return NotFound();
        _db.StoneMaterials.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
