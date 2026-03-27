using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GlassManagement.Core.Data;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

/// <summary>预埋件规格管理</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("数据管理 - 预埋件")]
public class EmbedPartSpecsController : ControllerBase
{
    private readonly GlassDbContext _db;
    public EmbedPartSpecsController(GlassDbContext db) => _db = db;

    /// <summary>获取所有预埋件规格</summary>
    /// <param name="embedType">类型: Channel / Plate / Bolt</param>
    [HttpGet]
    [ProducesResponseType(typeof(List<EmbedPartSpec>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EmbedPartSpec>>> GetAll([FromQuery] string? embedType = null)
    {
        var query = _db.EmbedPartSpecs.Where(x => x.IsActive);
        if (!string.IsNullOrEmpty(embedType)) query = query.Where(x => x.EmbedType == embedType);
        return Ok(await query.OrderBy(x => x.Code).ToListAsync());
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EmbedPartSpec), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmbedPartSpec>> GetById(int id)
    {
        var item = await _db.EmbedPartSpecs.FindAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [ProducesResponseType(typeof(EmbedPartSpec), StatusCodes.Status201Created)]
    public async Task<ActionResult<EmbedPartSpec>> Create([FromBody] EmbedPartSpec entity)
    {
        entity.CreatedAt = entity.UpdatedAt = DateTime.UtcNow;
        _db.EmbedPartSpecs.Add(entity);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] EmbedPartSpec entity)
    {
        var existing = await _db.EmbedPartSpecs.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Code = entity.Code;
        existing.EmbedType = entity.EmbedType;
        existing.PlateDimension = entity.PlateDimension;
        existing.AnchorDiameter = entity.AnchorDiameter;
        existing.AnchorCount = entity.AnchorCount;
        existing.AnchorLength = entity.AnchorLength;
        existing.PlateMaterial = entity.PlateMaterial;
        existing.AnchorMaterial = entity.AnchorMaterial;
        existing.MinConcreteGrade = entity.MinConcreteGrade;
        existing.TensionCapacity = entity.TensionCapacity;
        existing.ShearCapacity = entity.ShearCapacity;
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
        var item = await _db.EmbedPartSpecs.FindAsync(id);
        if (item is null) return NotFound();
        _db.EmbedPartSpecs.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
