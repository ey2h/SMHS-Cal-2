using Microsoft.AspNetCore.Mvc;
using GlassManagement.Api.Services.RegionalStandard;

namespace GlassManagement.Api.Controllers;

/// <summary>
/// 地方规范风荷载计算 API
/// 支持国标 GB50009-2012 及各省市地方规范的风荷载计算切换。
/// 
/// 已实现规范:
///   - GB50009-2012 (国家标准，完整)
///   - DBJ/T15-101-2022 (广东省标准，完整)
///   - DGJ08-56-2012 (上海市标准，框架)
///   - DB32/T4065-2021 (江苏省标准，框架)
///   - DBJ61/T161-2019 (陕西省标准，框架)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("地方规范风荷载")]
public class RegionalStandardController : ControllerBase
{
    private readonly WindLoadStandardFactory _factory;

    public RegionalStandardController(WindLoadStandardFactory factory)
    {
        _factory = factory;
    }

    /// <summary>获取所有支持的规范列表</summary>
    /// <remarks>
    /// 返回已注册的所有风荷载计算规范信息，包括是否为完整实现。
    /// </remarks>
    [HttpGet("list")]
    [ProducesResponseType(typeof(List<StandardInfo>), StatusCodes.Status200OK)]
    public ActionResult<List<StandardInfo>> GetStandardList()
    {
        return Ok(_factory.GetAvailableStandards());
    }

    /// <summary>获取指定规范下的城市列表及风压数据</summary>
    /// <param name="code">规范代号或别名，如 "GB50009-2012"、"广东" 等</param>
    /// <param name="keyword">城市名称关键字（可选，用于过滤）</param>
    /// <remarks>
    /// 国标返回全国主要城市，广东省标准返回精确到区/县的数据。
    /// </remarks>
    [HttpGet("{code}/cities")]
    [ProducesResponseType(typeof(List<CityWindPressureInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<List<CityWindPressureInfo>> GetCities(string code, [FromQuery] string? keyword = null)
    {
        var standard = _factory.GetStandard(code);
        if (standard == null)
            return NotFound(new { error = $"未找到规范: {code}", availableStandards = _factory.GetAvailableStandards().Select(s => s.Code) });

        var cities = standard.GetCityList();

        // 关键字过滤
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            cities = cities.Where(c =>
                c.City.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                (c.Prefecture?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();
        }

        return Ok(cities);
    }

    /// <summary>按指定规范计算风荷载</summary>
    /// <param name="code">规范代号或别名</param>
    /// <param name="input">风荷载计算参数</param>
    /// <remarks>
    /// 根据所选规范执行完整的风荷载计算，返回详细的分步计算过程。
    /// 
    /// 示例请求（广东省规范）:
    /// ```json
    /// {
    ///   "height": 50,
    ///   "terrainCategory": "B",
    ///   "city": "从化",
    ///   "returnPeriod": 50,
    ///   "structureArea": 3.6,
    ///   "shapeCoeffInput": {
    ///     "buildingHeight": 100,
    ///     "buildingWidth": 30,
    ///     "tributaryArea": 1.0,
    ///     "surfaceType": 0,
    ///     "zoneType": 2,
    ///     "openingType": 0
    ///   }
    /// }
    /// ```
    /// </remarks>
    [HttpPost("{code}/windload")]
    [ProducesResponseType(typeof(WindLoadResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<WindLoadResult> CalculateWindLoad(string code, [FromBody] WindLoadInput input)
    {
        var standard = _factory.GetStandard(code);
        if (standard == null)
            return NotFound(new { error = $"未找到规范: {code}" });

        if (input.Height <= 0)
            return BadRequest(new { error = "计算点标高必须大于0" });

        try
        {
            var result = standard.CalculateWindLoad(input);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"计算错误: {ex.Message}" });
        }
    }

    /// <summary>计算局部体型系数</summary>
    /// <param name="code">规范代号或别名</param>
    /// <param name="input">体型系数计算参数</param>
    /// <remarks>
    /// 国标: μs1直接查表8.3.1，按从属面积折减(8.3.4条)，加内压(8.3.5条)
    /// 广东: μsl = μse - μsi（7.5.2条），外压查表7.5.3，内压单独取值
    /// 
    /// SurfaceType 枚举: 0=墙面, 1=平屋面, 2=坡屋面, 3=女儿墙, 4=阳台
    /// ZoneType 枚举: 0=中部, 1=边缘, 2=角部
    /// </remarks>
    [HttpPost("{code}/shape-coefficient")]
    [ProducesResponseType(typeof(LocalShapeCoeffResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<LocalShapeCoeffResult> CalculateShapeCoefficient(string code, [FromBody] LocalShapeCoeffInput input)
    {
        var standard = _factory.GetStandard(code);
        if (standard == null)
            return NotFound(new { error = $"未找到规范: {code}" });

        try
        {
            var result = standard.GetLocalShapeCoefficient(input);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"计算错误: {ex.Message}" });
        }
    }

    /// <summary>查询指定城市在指定规范下的基本风压</summary>
    /// <param name="code">规范代号或别名</param>
    /// <param name="city">城市/区县名称</param>
    /// <param name="returnPeriod">重现期（年），默认50</param>
    [HttpGet("{code}/wind-pressure")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetWindPressure(string code, [FromQuery] string city, [FromQuery] int returnPeriod = 50)
    {
        var standard = _factory.GetStandard(code);
        if (standard == null)
            return NotFound(new { error = $"未找到规范: {code}" });

        double w0 = standard.GetBasicWindPressure(city, returnPeriod);
        if (w0 < 0)
            return NotFound(new { error = $"在 {standard.Name} 中未找到城市: {city}" });

        return Ok(new
        {
            standard = standard.Code,
            city,
            returnPeriod,
            w0,
            unit = "kN/m²"
        });
    }

    /// <summary>查询指定高度的阵风系数和风压高度变化系数</summary>
    /// <param name="code">规范代号</param>
    /// <param name="height">计算点标高 (m)</param>
    /// <param name="terrainCategory">地面粗糙度类别 A/B/C/D</param>
    [HttpGet("{code}/height-factors")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetHeightFactors(string code, [FromQuery] double height, [FromQuery] string terrainCategory = "B")
    {
        var standard = _factory.GetStandard(code);
        if (standard == null)
            return NotFound(new { error = $"未找到规范: {code}" });

        return Ok(new
        {
            standard = standard.Code,
            height,
            terrainCategory,
            muZ = standard.GetHeightFactor(height, terrainCategory),
            betaGz = standard.GetGustFactor(height, terrainCategory)
        });
    }
}
