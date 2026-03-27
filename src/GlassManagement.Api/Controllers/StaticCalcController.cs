using Microsoft.AspNetCore.Mvc;
using GlassManagement.Api.Services.StaticCalcEngine;

namespace GlassManagement.Api.Controllers;

/// <summary>
/// 静力计算引擎 — 基于《建筑结构静力计算手册》
/// 提供各类梁的弯矩、剪力、挠度计算及图表数据
/// </summary>
[ApiController]
[Route("api/[controller]")]
[ApiExplorerSettings(GroupName = "StaticCalc")]
public class StaticCalcController : ControllerBase
{
    private readonly StaticCalcService _service;

    public StaticCalcController(StaticCalcService service)
    {
        _service = service;
    }

    /// <summary>通用梁力学计算</summary>
    /// <remarks>
    /// 根据支承条件和荷载类型，返回完整的弯矩、剪力、挠度图数据。
    /// 支持：简支梁、悬臂梁、双跨梁、三跨梁、多跨铰接梁、带悬臂简支梁。
    /// </remarks>
    [HttpPost("beam")]
    public ActionResult<BeamCalcResult> CalculateBeam([FromBody] BeamCalcRequest request)
    {
        if (request.L <= 0)
            return BadRequest("跨度L必须大于0");
        if (request.NumPoints < 10) request.NumPoints = 100;
        if (request.NumPoints > 1000) request.NumPoints = 1000;

        var result = _service.Calculate(request);
        return Ok(result);
    }

    /// <summary>获取支持的力学模型列表</summary>
    [HttpGet("models")]
    public ActionResult<List<MechanicalModel>> GetModels()
    {
        return Ok(_service.GetModels());
    }

    /// <summary>获取弯矩/剪力/挠度图数据（用于SVG可视化）</summary>
    /// <remarks>与beam接口相同，但专门用于图表绘制，确保返回等间距数据点</remarks>
    [HttpPost("beam/diagram")]
    public ActionResult<BeamCalcResult> GetDiagram([FromBody] BeamCalcRequest request)
    {
        if (request.L <= 0)
            return BadRequest("跨度L必须大于0");
        if (request.NumPoints < 10) request.NumPoints = 100;
        if (request.NumPoints > 1000) request.NumPoints = 1000;

        var result = _service.Calculate(request);
        return Ok(result);
    }
}
