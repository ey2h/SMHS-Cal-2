using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GlassManagement.Api.Models;
using GlassManagement.Api.Services;

namespace GlassManagement.Api.Controllers;

/// <summary>CAD截面特性计算工具</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("截面工具")]
[Authorize]
public class SectionController : ControllerBase
{
    private readonly SectionCalculationService _calcService;
    private static readonly string ProfileDbPath = Path.Combine(AppContext.BaseDirectory, "Data", "section_profiles.json");
    private static readonly ConcurrentDictionary<string, List<SectionProfileRecord>> _profileDb = new();
    private static readonly object _fileLock = new();

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    static SectionController()
    {
        LoadProfileDb();
    }

    public SectionController(SectionCalculationService calcService)
    {
        _calcService = calcService;
    }

    /// <summary>计算截面特性</summary>
    [HttpPost("calculate")]
    [ProducesResponseType(typeof(SectionResult), StatusCodes.Status200OK)]
    public ActionResult<SectionResult> Calculate([FromBody] SectionCalculateRequest req)
    {
        try
        {
            if (req.OuterBoundary == null || req.OuterBoundary.Count < 3)
                return BadRequest("外轮廓至少需要3个顶点");

            SectionResult result;
            if (req.IsBreakHeat && req.BreakHeatConfig != null)
            {
                var cfg = req.BreakHeatConfig;
                if (cfg.Side1Region.Count < 3 || cfg.Side2Region.Count < 3)
                    return BadRequest("断热截面两侧区域各需至少3个顶点");

                result = _calcService.CalculateBreakHeatSection(
                    cfg.Side1Region, cfg.Side2Region, req.OuterBoundary,
                    cfg.CompositeElasticity, cfg.SpanLength);
            }
            else
            {
                result = _calcService.CalculateSection(req.OuterBoundary, req.Holes);
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest($"计算错误: {ex.Message}");
        }
    }

    /// <summary>获取所有型材分类</summary>
    [HttpGet("categories")]
    public ActionResult<string[]> GetCategories()
    {
        return Ok(ProfileCategories.All);
    }

    /// <summary>获取某分类下所有型材</summary>
    [HttpGet("profiles/{category}")]
    public ActionResult<List<SectionProfileRecord>> GetProfiles(string category)
    {
        category = Uri.UnescapeDataString(category);
        if (_profileDb.TryGetValue(category, out var list))
            return Ok(list);
        return Ok(new List<SectionProfileRecord>());
    }

    /// <summary>导入截面计算结果到型材库</summary>
    [HttpPost("import")]
    public ActionResult<SectionProfileRecord> ImportProfile([FromBody] SectionImportRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ProfileName))
            return BadRequest("型材名称不能为空");
        if (string.IsNullOrWhiteSpace(req.Category))
            return BadRequest("分类不能为空");

        var record = new SectionProfileRecord
        {
            ProfileName = req.ProfileName,
            Category = req.Category,
            Area = req.Result.Area,
            Ix = req.Result.Ix,
            Iy = req.Result.Iy,
            Wx1 = req.Result.Wx1,
            Wx2 = req.Result.Wx2,
            Wy1 = req.Result.Wy1,
            Wy2 = req.Result.Wy2,
            Sx = req.Result.Sx,
            Sy = req.Result.Sy,
            Cx = req.Result.Cx,
            Cy = req.Result.Cy,
            WallThickness = req.WallThickness,
            ShearWebWidth = req.ShearWebWidth
        };

        var list = _profileDb.GetOrAdd(req.Category, _ => new List<SectionProfileRecord>());
        lock (list)
        {
            list.Add(record);
        }
        SaveProfileDb();
        return Ok(record);
    }

    /// <summary>更新型材参数</summary>
    [HttpPut("profiles/{category}/{id}")]
    public ActionResult<SectionProfileRecord> UpdateProfile(string category, string id, [FromBody] SectionImportRequest req)
    {
        category = Uri.UnescapeDataString(category);
        if (!_profileDb.TryGetValue(category, out var list))
            return NotFound("分类不存在");

        lock (list)
        {
            var record = list.FirstOrDefault(r => r.Id == id);
            if (record == null) return NotFound("型材不存在");

            record.ProfileName = req.ProfileName;
            record.Area = req.Result.Area;
            record.Ix = req.Result.Ix;
            record.Iy = req.Result.Iy;
            record.Wx1 = req.Result.Wx1;
            record.Wx2 = req.Result.Wx2;
            record.Wy1 = req.Result.Wy1;
            record.Wy2 = req.Result.Wy2;
            record.Sx = req.Result.Sx;
            record.Sy = req.Result.Sy;
            record.Cx = req.Result.Cx;
            record.Cy = req.Result.Cy;
            record.WallThickness = req.WallThickness;
            record.ShearWebWidth = req.ShearWebWidth;

            SaveProfileDb();
            return Ok(record);
        }
    }

    /// <summary>删除型材</summary>
    [HttpDelete("profiles/{category}/{id}")]
    public ActionResult DeleteProfile(string category, string id)
    {
        category = Uri.UnescapeDataString(category);
        if (!_profileDb.TryGetValue(category, out var list))
            return NotFound("分类不存在");

        lock (list)
        {
            var idx = list.FindIndex(r => r.Id == id);
            if (idx < 0) return NotFound("型材不存在");
            list.RemoveAt(idx);
        }
        SaveProfileDb();
        return Ok(new { message = "已删除" });
    }

    #region 持久化

    private static void LoadProfileDb()
    {
        try
        {
            if (System.IO.File.Exists(ProfileDbPath))
            {
                var json = System.IO.File.ReadAllText(ProfileDbPath);
                var data = JsonSerializer.Deserialize<Dictionary<string, List<SectionProfileRecord>>>(json, _jsonOpts);
                if (data != null)
                {
                    foreach (var kv in data)
                        _profileDb[kv.Key] = kv.Value;
                }
            }
        }
        catch { /* 首次运行无文件 */ }
    }

    private static void SaveProfileDb()
    {
        lock (_fileLock)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ProfileDbPath)!);
                var json = JsonSerializer.Serialize(_profileDb, _jsonOpts);
                System.IO.File.WriteAllText(ProfileDbPath, json);
            }
            catch { /* 静默失败 */ }
        }
    }

    #endregion
}
