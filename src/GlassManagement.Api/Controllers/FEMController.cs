using Microsoft.AspNetCore.Mvc;
using GlassManagement.Api.Services;

namespace GlassManagement.Api.Controllers;

/// <summary>
/// 有限元分析接口 — 梁和板的FEM计算
/// </summary>
[ApiController]
[Route("api/[controller]")]
[ApiExplorerSettings(GroupName = "FEM")]
public class FEMController : ControllerBase
{
    private readonly FEMService _service;

    public FEMController(FEMService service)
    {
        _service = service;
    }

    /// <summary>有限元梁分析</summary>
    /// <remarks>
    /// 支持任意节点/单元/荷载组合，铰接/刚接/弹簧支座。
    /// 返回节点位移、单元内力和支座反力。
    /// </remarks>
    [HttpPost("beam")]
    public ActionResult<FEMBeamResult> AnalyzeBeam([FromBody] FEMBeamRequest request)
    {
        if (request.Nodes.Count < 2)
            return BadRequest("至少需要2个节点");
        if (request.Elements.Count < 1)
            return BadRequest("至少需要1个单元");

        var result = _service.AnalyzeBeam(request);
        return Ok(result);
    }

    /// <summary>有限元板分析（薄板弯曲）</summary>
    /// <remarks>
    /// 支持四边简支、三边简支一边自由、对边简支等。
    /// 返回挠度场、弯矩场和应力场。
    /// </remarks>
    [HttpPost("plate")]
    public ActionResult<FEMPlateResult> AnalyzePlate([FromBody] FEMPlateRequest request)
    {
        if (request.Width <= 0 || request.Height <= 0 || request.Thickness <= 0)
            return BadRequest("板尺寸和厚度必须大于0");

        var result = _service.AnalyzePlate(request);
        return Ok(result);
    }
}
