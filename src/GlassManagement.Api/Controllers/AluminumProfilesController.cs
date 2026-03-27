using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GlassManagement.Core.Data;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

/// <summary>铝型材规格管理</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("数据管理 - 铝型材")]
public class AluminumProfilesController : ControllerBase
{
    private readonly GlassDbContext _db;
    public AluminumProfilesController(GlassDbContext db) => _db = db;

    /// <summary>获取所有铝型材</summary>
    /// <param name="usage">用途筛选: Mullion / Rafter / Frame</param>
    /// <param name="grade">牌号筛选: 6063-T5 / 6061-T6</param>
    [HttpGet]
    [ProducesResponseType(typeof(List<AluminumProfile>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AluminumProfile>>> GetAll(
        [FromQuery] string? usage = null,
        [FromQuery] string? grade = null)
    {
        var query = _db.AluminumProfiles.Where(x => x.IsActive);
        if (!string.IsNullOrEmpty(usage)) query = query.Where(x => x.Usage == usage);
        if (!string.IsNullOrEmpty(grade)) query = query.Where(x => x.AluminumGrade == grade);
        return Ok(await query.OrderBy(x => x.ProfileCode).ToListAsync());
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AluminumProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AluminumProfile>> GetById(int id)
    {
        var item = await _db.AluminumProfiles.FindAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [ProducesResponseType(typeof(AluminumProfile), StatusCodes.Status201Created)]
    public async Task<ActionResult<AluminumProfile>> Create([FromBody] AluminumProfile entity)
    {
        entity.CreatedAt = entity.UpdatedAt = DateTime.UtcNow;
        _db.AluminumProfiles.Add(entity);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] AluminumProfile entity)
    {
        var existing = await _db.AluminumProfiles.FindAsync(id);
        if (existing is null) return NotFound();

        existing.ProfileCode = entity.ProfileCode;
        existing.Specification = entity.Specification;
        existing.Usage = entity.Usage;
        existing.AluminumGrade = entity.AluminumGrade;
        existing.SectionArea = entity.SectionArea;
        existing.MomentOfInertiaX = entity.MomentOfInertiaX;
        existing.MomentOfInertiaY = entity.MomentOfInertiaY;
        existing.SectionModulusX = entity.SectionModulusX;
        existing.SectionModulusY = entity.SectionModulusY;
        existing.RadiusOfGyrationX = entity.RadiusOfGyrationX;
        existing.RadiusOfGyrationY = entity.RadiusOfGyrationY;
        existing.LinearDensity = entity.LinearDensity;
        existing.AllowableStress = entity.AllowableStress;
        existing.ElasticModulus = entity.ElasticModulus;
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
        var item = await _db.AluminumProfiles.FindAsync(id);
        if (item is null) return NotFound();
        _db.AluminumProfiles.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
