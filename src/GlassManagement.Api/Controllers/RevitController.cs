using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GlassManagement.Api.Data;
using GlassManagement.Api.Models;

namespace GlassManagement.Api.Controllers;

/// <summary>Revit 模型数据批量提交API</summary>
[ApiController]
[Route("api/revit")]
[Tags("Revit数据")]
[Authorize]
public class RevitController : ControllerBase
{
    private readonly UserDbContext _db;

    public RevitController(UserDbContext db) => _db = db;

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    /// <summary>批量提交模型数据</summary>
    [HttpPost("submit")]
    public async Task<IActionResult> Submit([FromBody] RevitBatchSubmitRequest req)
    {
        var userId = GetUserId();
        var submission = new RevitSubmission
        {
            UserId = userId,
            ProjectName = req.ProjectName,
            RevitVersion = req.RevitVersion,
            FileName = req.FileName,
            ExtractedAt = req.ExtractedAt,
            SubmittedAt = DateTime.UtcNow,
            Status = "received",
            PanelCount = req.Panels.Count,
            MullionCount = req.Mullions.Count,
            ConnectionCount = req.Connections.Count,
            EmbedCount = req.Embeds.Count,
            DataJson = JsonSerializer.Serialize(req)
        };

        _db.RevitSubmissions.Add(submission);
        await _db.SaveChangesAsync();

        return Ok(new RevitBatchSubmitResponse
        {
            SubmissionId = submission.Id,
            PanelCount = submission.PanelCount,
            MullionCount = submission.MullionCount,
            ConnectionCount = submission.ConnectionCount,
            EmbedCount = submission.EmbedCount,
            TotalElements = submission.PanelCount + submission.MullionCount + submission.ConnectionCount + submission.EmbedCount,
            Status = submission.Status,
            ReceivedAt = submission.SubmittedAt
        });
    }

    /// <summary>当前用户的提交历史</summary>
    [HttpGet("submissions")]
    public async Task<IActionResult> GetSubmissions([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId();
        var query = _db.RevitSubmissions.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.SubmittedAt);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id, s.ProjectName, s.RevitVersion, s.FileName,
                s.ExtractedAt, s.SubmittedAt, s.Status,
                s.PanelCount, s.MullionCount, s.ConnectionCount, s.EmbedCount,
                TotalElements = s.PanelCount + s.MullionCount + s.ConnectionCount + s.EmbedCount
            })
            .ToListAsync();

        return Ok(new { items, total, page, pageSize });
    }

    /// <summary>获取某次提交详情</summary>
    [HttpGet("submissions/{id:int}")]
    public async Task<IActionResult> GetSubmission(int id)
    {
        var userId = GetUserId();
        var s = await _db.RevitSubmissions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (s == null) return NotFound(new { error = "提交记录不存在" });

        return Ok(new
        {
            s.Id, s.ProjectName, s.RevitVersion, s.FileName,
            s.ExtractedAt, s.SubmittedAt, s.Status,
            s.PanelCount, s.MullionCount, s.ConnectionCount, s.EmbedCount,
            TotalElements = s.PanelCount + s.MullionCount + s.ConnectionCount + s.EmbedCount,
            Data = JsonSerializer.Deserialize<RevitBatchSubmitRequest>(s.DataJson)
        });
    }

    /// <summary>删除某次提交</summary>
    [HttpDelete("submissions/{id:int}")]
    public async Task<IActionResult> DeleteSubmission(int id)
    {
        var userId = GetUserId();
        var s = await _db.RevitSubmissions.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (s == null) return NotFound(new { error = "提交记录不存在" });

        _db.RevitSubmissions.Remove(s);
        await _db.SaveChangesAsync();
        return Ok(new { message = "已删除" });
    }

    /// <summary>触发计算（mock）</summary>
    [HttpPost("submissions/{id:int}/calculate")]
    public async Task<IActionResult> Calculate(int id)
    {
        var userId = GetUserId();
        var s = await _db.RevitSubmissions.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (s == null) return NotFound(new { error = "提交记录不存在" });

        s.Status = "processing";
        await _db.SaveChangesAsync();

        // Mock: 直接标记为completed
        s.Status = "completed";
        await _db.SaveChangesAsync();

        return Ok(new { message = "计算完成", status = s.Status, submissionId = s.Id });
    }

    /// <summary>获取计算结果（mock）</summary>
    [HttpGet("submissions/{id:int}/results")]
    public async Task<IActionResult> GetResults(int id)
    {
        var userId = GetUserId();
        var s = await _db.RevitSubmissions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (s == null) return NotFound(new { error = "提交记录不存在" });

        if (s.Status != "completed")
            return BadRequest(new { error = "计算尚未完成", status = s.Status });

        // Mock 计算结果
        return Ok(new
        {
            submissionId = s.Id,
            status = s.Status,
            results = new
            {
                panelResults = new { total = s.PanelCount, passed = s.PanelCount, failed = 0 },
                mullionResults = new { total = s.MullionCount, passed = s.MullionCount, failed = 0 },
                connectionResults = new { total = s.ConnectionCount, passed = s.ConnectionCount, failed = 0 },
                embedResults = new { total = s.EmbedCount, passed = s.EmbedCount, failed = 0 },
                summary = "所有构件验算通过（mock结果）"
            }
        });
    }
}
