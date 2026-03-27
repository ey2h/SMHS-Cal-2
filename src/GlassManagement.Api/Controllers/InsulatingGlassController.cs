using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GlassManagement.Core.Data;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

/// <summary>中空玻璃配置管理</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("数据管理 - 中空玻璃")]
public class InsulatingGlassController : ControllerBase
{
    private readonly GlassDbContext _db;
    public InsulatingGlassController(GlassDbContext db) => _db = db;

    [HttpGet]
    [ProducesResponseType(typeof(List<InsulatingGlassConfig>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<InsulatingGlassConfig>>> GetAll()
        => Ok(await _db.InsulatingGlassConfigs.ToListAsync());

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(InsulatingGlassConfig), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InsulatingGlassConfig>> GetById(int id)
    {
        var item = await _db.InsulatingGlassConfigs.FindAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [ProducesResponseType(typeof(InsulatingGlassConfig), StatusCodes.Status201Created)]
    public async Task<ActionResult<InsulatingGlassConfig>> Create([FromBody] InsulatingGlassConfig entity)
    {
        entity.CreatedAt = entity.UpdatedAt = DateTime.UtcNow;
        _db.InsulatingGlassConfigs.Add(entity);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] InsulatingGlassConfig entity)
    {
        var existing = await _db.InsulatingGlassConfigs.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Name = entity.Name;
        existing.Configuration = entity.Configuration;
        existing.OuterThickness = entity.OuterThickness;
        existing.InnerThickness = entity.InnerThickness;
        existing.AirSpaceThickness = entity.AirSpaceThickness;
        existing.GasType = entity.GasType;
        existing.ThermalTransmittance = entity.ThermalTransmittance;
        existing.OuterGlassMaterialId = entity.OuterGlassMaterialId;
        existing.InnerGlassMaterialId = entity.InnerGlassMaterialId;
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
        var item = await _db.InsulatingGlassConfigs.FindAsync(id);
        if (item is null) return NotFound();
        _db.InsulatingGlassConfigs.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
