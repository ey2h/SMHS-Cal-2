using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GlassManagement.Core.Data;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

/// <summary>技术规范管理</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("数据管理 - 技术规范")]
public class StandardsController : ControllerBase
{
    private readonly GlassDbContext _db;
    public StandardsController(GlassDbContext db) => _db = db;

    [HttpGet]
    [ProducesResponseType(typeof(List<TechnicalStandard>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TechnicalStandard>>> GetAll(
        [FromQuery] string? category = null)
    {
        var query = _db.TechnicalStandards.AsQueryable();
        if (!string.IsNullOrEmpty(category))
            query = query.Where(x => x.Category == category);
        return Ok(await query.OrderBy(x => x.Code).ToListAsync());
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TechnicalStandard), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TechnicalStandard>> GetById(int id)
    {
        var item = await _db.TechnicalStandards.FindAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [ProducesResponseType(typeof(TechnicalStandard), StatusCodes.Status201Created)]
    public async Task<ActionResult<TechnicalStandard>> Create([FromBody] TechnicalStandard entity)
    {
        entity.CreatedAt = entity.UpdatedAt = DateTime.UtcNow;
        _db.TechnicalStandards.Add(entity);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] TechnicalStandard entity)
    {
        var existing = await _db.TechnicalStandards.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Code = entity.Code;
        existing.Name = entity.Name;
        existing.Category = entity.Category;
        existing.PublishYear = entity.PublishYear;
        existing.IsCurrent = entity.IsCurrent;
        existing.ReplacedBy = entity.ReplacedBy;
        existing.Description = entity.Description;
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.TechnicalStandards.FindAsync(id);
        if (item is null) return NotFound();
        _db.TechnicalStandards.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
