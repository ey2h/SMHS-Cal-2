using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GlassManagement.Core.Data;
using GlassManagement.Api.Models;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

/// <summary>项目管理API</summary>
[ApiController]
[Route("api/[controller]")]
[Tags("项目管理")]
public class ProjectController : ControllerBase
{
    private readonly GlassDbContext _db;

    public ProjectController(GlassDbContext db) => _db = db;

    /// <summary>获取用户的所有项目</summary>
    [HttpGet]
    public async Task<ActionResult<List<Project>>> GetAll([FromHeader(Name = "X-User-Id")] int userId)
    {
        return await _db.Projects
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync();
    }

    /// <summary>根据ID获取项目详情</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Project>> GetById(int id, [FromHeader(Name = "X-User-Id")] int userId)
    {
        var project = await _db.Projects
            .Include(p => p.CalculationRecords)
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (project == null) return NotFound(new { error = "项目不存在或无权访问" });
        return project;
    }

    /// <summary>创建项目</summary>
    [HttpPost]
    public async Task<ActionResult<Project>> Create(
        [FromHeader(Name = "X-User-Id")] int userId,
        [FromBody] CreateProjectRequest req)
    {
        var project = new Project
        {
            UserId = userId,
            Name = req.Name,
            Code = req.Code,
            Description = req.Description,
            City = req.City,
            BuildingHeight = req.BuildingHeight,
            TerrainCategory = req.TerrainCategory,
            SeismicIntensity = req.SeismicIntensity,
            BasicWindPressure = req.BasicWindPressure
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = project.Id }, project);
    }

    /// <summary>更新项目</summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<Project>> Update(
        int id,
        [FromHeader(Name = "X-User-Id")] int userId,
        [FromBody] UpdateProjectRequest req)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (project == null) return NotFound(new { error = "项目不存在或无权访问" });

        if (req.Name != null) project.Name = req.Name;
        if (req.Code != null) project.Code = req.Code;
        if (req.Description != null) project.Description = req.Description;
        if (req.City != null) project.City = req.City;
        if (req.BuildingHeight.HasValue) project.BuildingHeight = req.BuildingHeight.Value;
        if (req.TerrainCategory != null) project.TerrainCategory = req.TerrainCategory;
        if (req.SeismicIntensity.HasValue) project.SeismicIntensity = req.SeismicIntensity.Value;
        if (req.BasicWindPressure.HasValue) project.BasicWindPressure = req.BasicWindPressure.Value;
        if (req.Status != null) project.Status = req.Status;
        project.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return project;
    }

    /// <summary>删除项目</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, [FromHeader(Name = "X-User-Id")] int userId)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (project == null) return NotFound();
        _db.Projects.Remove(project);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>获取项目的计算记录</summary>
    [HttpGet("{id}/calculations")]
    public async Task<ActionResult<List<CalculationRecord>>> GetCalculations(int id, [FromHeader(Name = "X-User-Id")] int userId)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
        if (project == null) return NotFound(new { error = "项目不存在或无权访问" });

        return await _db.CalculationRecords
            .Where(c => c.ProjectId == id)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }
}
