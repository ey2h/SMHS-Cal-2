using Microsoft.AspNetCore.Mvc;
using GlassManagement.Core.Models;
using GlassManagement.Api.Services;

namespace GlassManagement.Api.Controllers;

/// <summary>计算书生成</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("计算书生成")]
public class ReportController : ControllerBase
{
    private readonly CalcBookWordService _calcBookWordService;
    private readonly AluminumPlateCalcBookService _aluminumPlateService;
    private readonly UnitCurtainWallCalcBookService _unitCurtainWallService;
    private readonly PanelCalcBookService _panelService;
    private readonly DoorWindowCalcBookService _doorWindowService;
    private readonly EnergySavingCalcBookService _energySavingService;
    private readonly PointSupportCalcBookService _pointSupportService;

    public ReportController(
        CalcBookWordService calcBookWordService,
        AluminumPlateCalcBookService aluminumPlateService,
        UnitCurtainWallCalcBookService unitCurtainWallService,
        PanelCalcBookService panelService,
        DoorWindowCalcBookService doorWindowService,
        EnergySavingCalcBookService energySavingService,
        PointSupportCalcBookService pointSupportService)
    {
        _calcBookWordService = calcBookWordService;
        _aluminumPlateService = aluminumPlateService;
        _unitCurtainWallService = unitCurtainWallService;
        _panelService = panelService;
        _doorWindowService = doorWindowService;
        _energySavingService = energySavingService;
        _pointSupportService = pointSupportService;
    }

    /// <summary>获取计算书模板列表</summary>
    [HttpGet("templates")]
    [ProducesResponseType(typeof(List<ReportTemplate>), StatusCodes.Status200OK)]
    public ActionResult<List<ReportTemplate>> GetTemplates()
    {
        return Ok(new List<ReportTemplate>
        {
            new ReportTemplate { Id = "glass", Name = "全隐框玻璃幕墙", Description = "包含玻璃选型、强度验算、挠度验算的完整计算书" },
            new ReportTemplate { Id = "aluminum-plate", Name = "铝单板幕墙", Description = "包含铝板面板、立柱、横梁等计算" },
            new ReportTemplate { Id = "unit-curtain-wall", Name = "单元幕墙", Description = "包含单元立柱、横梁、插芯、挂件等计算" },
            new ReportTemplate { Id = "grc-panel", Name = "GRC板幕墙", Description = "包含GRC板面板、立柱、横梁等计算" },
            new ReportTemplate { Id = "uhpc-panel", Name = "UHPC板幕墙", Description = "包含UHPC板面板、立柱、横梁等计算" },
            new ReportTemplate { Id = "point-support", Name = "点式钢管结构", Description = "包含钢管立柱、横梁、驳接爪等计算" },
            new ReportTemplate { Id = "door-window", Name = "门窗", Description = "包含窗框、玻璃、五金件等计算" },
            new ReportTemplate { Id = "energy-saving", Name = "节能计算书", Description = "包含传热系数、遮阳系数等计算" }
        });
    }

    /// <summary>生成玻璃幕墙计算书</summary>
    /// <remarks>包含玻璃选型、强度验算、挠度验算的完整计算书</remarks>
    [HttpPost("glass")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult GenerateGlassReport([FromBody] GlassCalcBookInput input)
    {
        try
        {
            var bytes = _calcBookWordService.GenerateGlassCurtainWallReport(input);
            var fileName = $"玻璃幕墙计算书_{input.ProjectName ?? "项目"}_{DateTime.Now:yyyyMMdd}.docx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>生成铝单板幕墙计算书</summary>
    [HttpPost("aluminum-plate")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult GenerateAluminumPlateReport([FromBody] AluminumPlateCalcBookInput input)
    {
        try
        {
            var bytes = _aluminumPlateService.GenerateReport(input);
            var fileName = $"铝单板幕墙计算书_{input.ProjectName ?? "项目"}_{DateTime.Now:yyyyMMdd}.docx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>生成单元幕墙计算书</summary>
    [HttpPost("unit-curtain-wall")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult GenerateUnitCurtainWallReport([FromBody] UnitCurtainWallCalcBookInput input)
    {
        try
        {
            var bytes = _unitCurtainWallService.GenerateReport(input);
            var fileName = $"单元幕墙计算书_{input.ProjectName ?? "项目"}_{DateTime.Now:yyyyMMdd}.docx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>生成GRC板幕墙计算书</summary>
    [HttpPost("grc-panel")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult GenerateGrcPanelReport([FromBody] PanelCalcBookInput input)
    {
        try
        {
            input.PanelType = "GRC";
            var bytes = _panelService.GenerateReport(input);
            var fileName = $"GRC板幕墙计算书_{input.ProjectName ?? "项目"}_{DateTime.Now:yyyyMMdd}.docx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>生成UHPC板幕墙计算书</summary>
    [HttpPost("uhpc-panel")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult GenerateUhpcPanelReport([FromBody] PanelCalcBookInput input)
    {
        try
        {
            input.PanelType = "UHPC";
            var bytes = _panelService.GenerateReport(input);
            var fileName = $"UHPC板幕墙计算书_{input.ProjectName ?? "项目"}_{DateTime.Now:yyyyMMdd}.docx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>生成点式钢管结构计算书</summary>
    [HttpPost("point-support")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult GeneratePointSupportReport([FromBody] PointSupportCalcBookInput input)
    {
        try
        {
            var bytes = _pointSupportService.GenerateReport(input);
            var fileName = $"点式钢管结构计算书_{input.ProjectName ?? "项目"}_{DateTime.Now:yyyyMMdd}.docx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>生成门窗计算书</summary>
    [HttpPost("door-window")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult GenerateDoorWindowReport([FromBody] DoorWindowCalcBookInput input)
    {
        try
        {
            var bytes = _doorWindowService.GenerateReport(input);
            var fileName = $"门窗计算书_{input.ProjectName ?? "项目"}_{DateTime.Now:yyyyMMdd}.docx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>生成节能计算书</summary>
    [HttpPost("energy-saving")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult GenerateEnergySavingReport([FromBody] EnergySavingCalcBookInput input)
    {
        try
        {
            var bytes = _energySavingService.GenerateReport(input);
            var fileName = $"节能计算书_{input.ProjectName ?? "项目"}_{DateTime.Now:yyyyMMdd}.docx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
