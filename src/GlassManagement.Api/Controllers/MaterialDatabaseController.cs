using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using GlassManagement.Api.Data;

namespace GlassManagement.Api.Controllers;

/// <summary>材料参数数据库API - 完整CRUD操作</summary>
[ApiController]
[Route("api/[controller]")]
public class MaterialDatabaseController : ControllerBase
{
    private readonly MaterialDataService _svc;
    private readonly IWebHostEnvironment _env;

    // 表名映射（URL路径 → 数据库表名）
    private static readonly Dictionary<string, string> TableMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["regions"] = "Regions",
        ["glass"] = "GlassTypes",
        ["columns"] = "ProfileColumns",
        ["beams"] = "ProfileBeams",
        ["steelpipes"] = "SteelPipes",
        ["bolts"] = "Bolts",
        ["panels"] = "PanelMaterials",
        ["weights"] = "CurtainWallWeights",
        ["steel-alum-steel-columns"] = "SteelAlumSteelColumns",
        ["steel-alum-alu-columns"] = "SteelAlumAluColumns",
        ["steel-alum-steel-beams"] = "SteelAlumSteelBeams",
        ["steel-alum-alu-beams"] = "SteelAlumAluBeams",
    };

    public MaterialDatabaseController(MaterialDataService svc, IWebHostEnvironment env)
    {
        _svc = svc;
        _env = env;
    }

    // ========== 数据库统计 ==========
    [HttpGet("stats")]
    public IActionResult GetStats() => Ok(_svc.GetStats());

    // ========== 通用查询 ==========

    /// <summary>查询指定表数据（支持搜索、分类筛选、分页）</summary>
    [HttpGet("{tableName}")]
    public IActionResult GetAll(string tableName, [FromQuery] string? search = null, [FromQuery] string? category = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 0)
    {
        object? data = tableName.ToLower() switch
        {
            "regions" => _svc.GetAllRegions(search),
            "glass" => _svc.GetAllGlassTypes(category, search),
            "columns" => _svc.GetAllProfileColumns(search),
            "beams" => _svc.GetAllProfileBeams(search),
            "steelpipes" => _svc.GetAllSteelPipes(search),
            "bolts" => _svc.GetAllBolts(search, category != null && int.TryParse(category, out var c) ? c : null),
            "panels" => _svc.GetAllPanelMaterials(category, search),
            "weights" => _svc.GetAllCurtainWallWeights(search),
            "steel-alum-steel-columns" => _svc.GetAllSteelAlumSteelColumns(search),
            "steel-alum-alu-columns" => _svc.GetAllSteelAlumAluColumns(search),
            "steel-alum-steel-beams" => _svc.GetAllSteelAlumSteelBeams(search),
            "steel-alum-alu-beams" => _svc.GetAllSteelAlumAluBeams(search),
            _ => null
        };
        if (data == null) return NotFound(new { error = $"未知的表名: {tableName}" });

        // 分页
        if (pageSize > 0 && data is System.Collections.IList list)
        {
            var total = list.Count;
            var items = list.Cast<object>().Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Ok(new { total, page, pageSize, items });
        }
        return Ok(data);
    }

    /// <summary>查询单条记录</summary>
    [HttpGet("{tableName}/{id:int}")]
    public IActionResult GetById(string tableName, int id)
    {
        object? data = tableName.ToLower() switch
        {
            "regions" => _svc.GetRegionById(id),
            "glass" => _svc.GetGlassTypeById(id),
            "columns" => _svc.GetProfileColumnById(id),
            "beams" => _svc.GetProfileBeamById(id),
            "steelpipes" => _svc.GetSteelPipeById(id),
            "bolts" => _svc.GetBoltById(id),
            "panels" => _svc.GetPanelMaterialById(id),
            "weights" => _svc.GetCurtainWallWeightById(id),
            _ => null
        };
        if (data == null) return NotFound(new { error = "记录未找到" });
        return Ok(data);
    }

    /// <summary>新增记录</summary>
    [HttpPost("{tableName}")]
    public IActionResult Add(string tableName, [FromBody] System.Text.Json.JsonElement body)
    {
        try
        {
            var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            int newId = tableName.ToLower() switch
            {
                "regions" => _svc.AddRegion(body.Deserialize<RegionData>(opts)!),
                "glass" => _svc.AddGlassType(body.Deserialize<GlassTypeData>(opts)!),
                "columns" => _svc.AddProfileColumn(body.Deserialize<ProfileColumnData>(opts)!),
                "beams" => _svc.AddProfileBeam(body.Deserialize<ProfileBeamData>(opts)!),
                "steelpipes" => _svc.AddSteelPipe(body.Deserialize<SteelPipeData>(opts)!),
                "bolts" => _svc.AddBolt(body.Deserialize<BoltData>(opts)!),
                "panels" => _svc.AddPanelMaterial(body.Deserialize<PanelMaterialData>(opts)!),
                "weights" => _svc.AddCurtainWallWeight(body.Deserialize<CurtainWallWeightData>(opts)!),
                _ => throw new ArgumentException($"未知的表名: {tableName}")
            };
            return Ok(new { id = newId });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>修改记录</summary>
    [HttpPut("{tableName}/{id:int}")]
    public IActionResult Update(string tableName, int id, [FromBody] System.Text.Json.JsonElement body)
    {
        try
        {
            var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            bool ok = tableName.ToLower() switch
            {
                "regions" => _svc.UpdateRegion(SetId(body.Deserialize<RegionData>(opts)!, id)),
                "glass" => _svc.UpdateGlassType(SetId(body.Deserialize<GlassTypeData>(opts)!, id)),
                "columns" => _svc.UpdateProfileColumn(SetId(body.Deserialize<ProfileColumnData>(opts)!, id)),
                "beams" => _svc.UpdateProfileBeam(SetId(body.Deserialize<ProfileBeamData>(opts)!, id)),
                "steelpipes" => _svc.UpdateSteelPipe(SetId(body.Deserialize<SteelPipeData>(opts)!, id)),
                "bolts" => _svc.UpdateBolt(SetId(body.Deserialize<BoltData>(opts)!, id)),
                "panels" => _svc.UpdatePanelMaterial(SetId(body.Deserialize<PanelMaterialData>(opts)!, id)),
                "weights" => _svc.UpdateCurtainWallWeight(SetId(body.Deserialize<CurtainWallWeightData>(opts)!, id)),
                _ => throw new ArgumentException($"未知的表名: {tableName}")
            };
            return ok ? Ok(new { success = true }) : NotFound(new { error = "记录未找到" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>删除记录</summary>
    [HttpDelete("{tableName}/{id:int}")]
    public IActionResult Delete(string tableName, int id)
    {
        bool ok = tableName.ToLower() switch
        {
            "regions" => _svc.DeleteRegion(id),
            "glass" => _svc.DeleteGlassType(id),
            "columns" => _svc.DeleteProfileColumn(id),
            "beams" => _svc.DeleteProfileBeam(id),
            "steelpipes" => _svc.DeleteSteelPipe(id),
            "bolts" => _svc.DeleteBolt(id),
            "panels" => _svc.DeletePanelMaterial(id),
            "weights" => _svc.DeleteCurtainWallWeight(id),
            _ => false
        };
        return ok ? Ok(new { success = true }) : NotFound(new { error = "记录未找到" });
    }

    /// <summary>CSV导入</summary>
    [HttpPost("{tableName}/import")]
    [Consumes("application/json")]
    public async Task<IActionResult> ImportCsv(string tableName, [FromBody] string? csvContent = null)
    {
        if (!TableMap.TryGetValue(tableName, out var dbTable))
            return NotFound(new { error = $"未知的表名: {tableName}" });

        string content;
        if (!string.IsNullOrEmpty(csvContent))
            content = csvContent;
        else
            return BadRequest(new { error = "请提供CSV内容" });

        try
        {
            var count = _svc.ImportFromCsv(dbTable, content);
            return Ok(new { imported = count });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>CSV导出</summary>
    [HttpGet("{tableName}/export")]
    public IActionResult ExportCsv(string tableName)
    {
        if (!TableMap.TryGetValue(tableName, out var dbTable))
            return NotFound(new { error = $"未知的表名: {tableName}" });

        try
        {
            var csv = _svc.ExportToCsv(dbTable);
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"{dbTable}.csv");
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>备份数据库</summary>
    [HttpPost("backup")]
    public IActionResult Backup()
    {
        try
        {
            var backupDir = Path.Combine(_env.ContentRootPath, "Data", "backups");
            Directory.CreateDirectory(backupDir);
            var backupPath = Path.Combine(backupDir, $"params_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");
            _svc.BackupDatabase(backupPath);
            return Ok(new { path = backupPath, time = DateTime.Now });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ========== 兼容旧API路由 ==========

    [HttpGet("glass/categories")]
    public IActionResult GetGlassCategories() => Ok(_svc.GetGlassCategories());

    [HttpGet("panels/categories")]
    public IActionResult GetPanelCategories() => Ok(_svc.GetPanelCategories());

    // ========== 辅助方法 ==========

    private static T SetId<T>(T obj, int id) where T : class
    {
        var prop = typeof(T).GetProperty("Id");
        prop?.SetValue(obj, id);
        return obj;
    }
}
