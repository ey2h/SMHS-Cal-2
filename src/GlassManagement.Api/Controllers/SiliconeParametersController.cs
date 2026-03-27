using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GlassManagement.Core.Data;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

/// <summary>结构胶参数管理</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("数据管理 - 结构胶")]
public class SiliconeParametersController : ControllerBase
{
    private readonly GlassDbContext _db;
    public SiliconeParametersController(GlassDbContext db) => _db = db;

    /// <summary>获取所有结构胶参数</summary>
    /// <param name="type">类型: Structural / Weathering</param>
    [HttpGet]
    [ProducesResponseType(typeof(List<SiliconeParameter>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SiliconeParameter>>> GetAll([FromQuery] string? type = null)
    {
        var query = _db.SiliconeParameters.Where(x => x.IsActive);
        if (!string.IsNullOrEmpty(type)) query = query.Where(x => x.Type == type);
        return Ok(await query.OrderBy(x => x.Brand).ThenBy(x => x.Model).ToListAsync());
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(SiliconeParameter), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SiliconeParameter>> GetById(int id)
    {
        var item = await _db.SiliconeParameters.FindAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [ProducesResponseType(typeof(SiliconeParameter), StatusCodes.Status201Created)]
    public async Task<ActionResult<SiliconeParameter>> Create([FromBody] SiliconeParameter entity)
    {
        entity.CreatedAt = entity.UpdatedAt = DateTime.UtcNow;
        _db.SiliconeParameters.Add(entity);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] SiliconeParameter entity)
    {
        var existing = await _db.SiliconeParameters.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Brand = entity.Brand;
        existing.Model = entity.Model;
        existing.Type = entity.Type;
        existing.DesignStrength = entity.DesignStrength;
        existing.TensileBondStrength = entity.TensileBondStrength;
        existing.ShearStrength = entity.ShearStrength;
        existing.TensileModulus = entity.TensileModulus;
        existing.ElongationAtBreak = entity.ElongationAtBreak;
        existing.TemperatureRange = entity.TemperatureRange;
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
        var item = await _db.SiliconeParameters.FindAsync(id);
        if (item is null) return NotFound();
        _db.SiliconeParameters.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
