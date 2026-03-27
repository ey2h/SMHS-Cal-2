#pragma warning disable CS0618 // 兼容旧模型

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GlassManagement.Core.Data;
using GlassManagement.Api.Models;
using GlassManagement.Core.Models;
using GlassManagement.Api.Services;
using System.Text.Json;

namespace GlassManagement.Api.Controllers;

/// <summary>向导式操作流程API — 对齐汇宝8步向导</summary>
[ApiController]
[Route("api/[controller]")]
[Tags("向导流程")]
public class WizardController : ControllerBase
{
    private readonly WizardService _wizard;
    private readonly GlassDbContext _db;
    private readonly CalcBookPdfService _pdfSvc;
    private readonly CalcBookWordService _wordSvc;

    public WizardController(WizardService wizard, GlassDbContext db, CalcBookPdfService pdfSvc, CalcBookWordService wordSvc)
    {
        _wizard = wizard;
        _db = db;
        _pdfSvc = pdfSvc;
        _wordSvc = wordSvc;
    }

    /// <summary>创建向导会话</summary>
    [HttpPost("sessions")]
    public ActionResult<WizardSessionResponse> CreateSession([FromHeader(Name = "X-User-Id")] int userId = 0)
    {
        var session = _wizard.CreateSession(userId);
        return CreatedAtAction(nameof(GetSession), new { id = session.SessionId }, WizardService.ToResponse(session));
    }

    /// <summary>获取向导会话状态</summary>
    [HttpGet("sessions/{id}")]
    public ActionResult<WizardSessionResponse> GetSession(string id)
    {
        var session = _wizard.GetSession(id);
        if (session == null) return NotFound(new { error = "会话不存在" });
        return WizardService.ToResponse(session);
    }

    // ===== 新8步向导（对齐需求清单）=====

    /// <summary>步骤1：基本参数（环境+荷载+分格）</summary>
    [HttpPut("sessions/{id}/step1-basic")]
    public async Task<ActionResult<WizardStepResult>> Step1Basic(string id, [FromHeader(Name = "X-User-Id")] int userId, [FromBody] Step1BasicInput input)
    {
        var session = _wizard.GetSession(id);
        if (session == null) return NotFound(new { error = "会话不存在" });

        // 创建/更新项目
        var project = new Project
        {
            UserId = userId,
            Name = input.ProjectName,
            Code = input.ProjectCode,
            City = input.City,
            BuildingHeight = input.BuildingHeight,
            TerrainCategory = input.TerrainCategory,
            SeismicIntensity = input.SeismicIntensity,
            BasicWindPressure = input.BasicWindPressure,
            Status = "InProgress"
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        _wizard.SetProjectId(id, project.Id);
        _wizard.UpdateStep(id, 1, "step1", input);

        // 自动计算
        var calcResult = CalcStep1(input);
        _wizard.UpdateStep(id, 1, "step1Result", calcResult);

        return new WizardStepResult
        {
            Success = true,
            CurrentStep = 1,
            Result = new { projectId = project.Id, calcResult },
            NextStepTemplate = new Step2GlassInput(),
            Message = "基本参数已保存，请进行步骤2：玻璃配置与验算"
        };
    }

    /// <summary>步骤2：玻璃配置与验算</summary>
    [HttpPut("sessions/{id}/step2-glass")]
    public async Task<ActionResult<WizardStepResult>> Step2Glass(string id, [FromBody] Step2GlassInput input)
    {
        var session = _wizard.GetSession(id);
        if (session == null) return NotFound(new { error = "会话不存在" });
        if (!session.CompletedSteps.Contains(1)) return BadRequest(new { error = "请先完成步骤1" });

        _wizard.UpdateStep(id, 2, "step2", input);
        await SaveCalcRecord(session.ProjectId, CalcType.GlassStrength, input, input);

        return new WizardStepResult
        {
            Success = true, CurrentStep = 2,
            Result = input,
            NextStepTemplate = new Step3MullionInput(),
            Message = "玻璃配置完成，请进行步骤3：立柱计算"
        };
    }

    /// <summary>步骤3：立柱计算</summary>
    [HttpPut("sessions/{id}/step3-mullion")]
    public async Task<ActionResult<WizardStepResult>> Step3Mullion(string id, [FromBody] Step3MullionInput input)
    {
        var session = _wizard.GetSession(id);
        if (session == null) return NotFound(new { error = "会话不存在" });
        if (!session.CompletedSteps.Contains(2)) return BadRequest(new { error = "请先完成步骤2" });

        _wizard.UpdateStep(id, 3, "step3", input);
        await SaveCalcRecord(session.ProjectId, CalcType.Column, input, input);

        return new WizardStepResult
        {
            Success = true, CurrentStep = 3,
            Result = input,
            NextStepTemplate = new Step4TransomInput(),
            Message = "立柱计算完成，请进行步骤4：横梁计算"
        };
    }

    /// <summary>步骤4：横梁计算</summary>
    [HttpPut("sessions/{id}/step4-transom")]
    public async Task<ActionResult<WizardStepResult>> Step4Transom(string id, [FromBody] Step4TransomInput input)
    {
        var session = _wizard.GetSession(id);
        if (session == null) return NotFound(new { error = "会话不存在" });
        if (!session.CompletedSteps.Contains(3)) return BadRequest(new { error = "请先完成步骤3" });

        _wizard.UpdateStep(id, 4, "step4", input);
        await SaveCalcRecord(session.ProjectId, CalcType.Beam, input, input);

        return new WizardStepResult
        {
            Success = true, CurrentStep = 4,
            Result = input,
            NextStepTemplate = new Step5ConnectionInput(),
            Message = "横梁计算完成，请进行步骤5：连接件计算"
        };
    }

    /// <summary>步骤5：连接件计算</summary>
    [HttpPut("sessions/{id}/step5-connection")]
    public async Task<ActionResult<WizardStepResult>> Step5Connection(string id, [FromBody] Step5ConnectionInput input)
    {
        var session = _wizard.GetSession(id);
        if (session == null) return NotFound(new { error = "会话不存在" });
        if (!session.CompletedSteps.Contains(4)) return BadRequest(new { error = "请先完成步骤4" });

        _wizard.UpdateStep(id, 5, "step5", input);
        await SaveCalcRecord(session.ProjectId, CalcType.Connection, input, input);

        return new WizardStepResult
        {
            Success = true, CurrentStep = 5,
            Result = input,
            NextStepTemplate = new Step6EmbeddedInput(),
            Message = "连接件计算完成，请进行步骤6：预埋件与其他验算"
        };
    }

    /// <summary>步骤6：预埋件与其他验算</summary>
    [HttpPut("sessions/{id}/step6-embedded")]
    public async Task<ActionResult<WizardStepResult>> Step6Embedded(string id, [FromBody] Step6EmbeddedInput input)
    {
        var session = _wizard.GetSession(id);
        if (session == null) return NotFound(new { error = "会话不存在" });
        if (!session.CompletedSteps.Contains(5)) return BadRequest(new { error = "请先完成步骤5" });

        _wizard.UpdateStep(id, 6, "step6", input);
        await SaveCalcRecord(session.ProjectId, CalcType.Silicone, input, input);

        return new WizardStepResult
        {
            Success = true, CurrentStep = 6,
            Result = input,
            Message = "预埋件验算完成，请进行步骤7：计算结果汇总"
        };
    }

    /// <summary>步骤7：计算结果汇总（只读，提交确认）</summary>
    [HttpPut("sessions/{id}/step7-summary")]
    public ActionResult<WizardStepResult> Step7Summary(string id)
    {
        var session = _wizard.GetSession(id);
        if (session == null) return NotFound(new { error = "会话不存在" });
        if (!session.CompletedSteps.Contains(6)) return BadRequest(new { error = "请先完成步骤6" });

        _wizard.UpdateStep(id, 7, "step7", new { confirmed = true });

        return new WizardStepResult
        {
            Success = true, CurrentStep = 7,
            Result = new { message = "结果汇总已确认" },
            NextStepTemplate = new Step8ReportInput(),
            Message = "请进行步骤8：计算书输出"
        };
    }

    /// <summary>步骤8：生成计算书</summary>
    [HttpPut("sessions/{id}/step8-report")]
    public async Task<ActionResult<WizardStepResult>> Step8Report(string id, [FromBody] Step8ReportInput input)
    {
        var session = _wizard.GetSession(id);
        if (session == null) return NotFound(new { error = "会话不存在" });
        if (!session.CompletedSteps.Contains(7)) return BadRequest(new { error = "请先完成步骤7" });

        _wizard.UpdateStep(id, 8, "step8", input);
        _wizard.CompleteSession(id);

        await SaveCalcRecord(session.ProjectId, CalcType.Report, input, new { formats = input.Formats });

        return new WizardStepResult
        {
            Success = true, CurrentStep = 8,
            Result = new { generatedFormats = input.Formats, message = "计算书生成完成" },
            Message = "向导流程已完成！"
        };
    }

    /// <summary>下载向导会话计算书</summary>
    [HttpGet("sessions/{id}/download/{format}")]
    public ActionResult DownloadReport(string id, string format)
    {
        var session = _wizard.GetSession(id);
        if (session == null) return NotFound(new { error = "会话不存在" });

        var bookInput = BuildCalcBookInput(session);

        if (format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
        {
            var pdf = _pdfSvc.GenerateGlassCurtainWallReport(bookInput);
            return File(pdf, "application/pdf", $"计算书_{bookInput.ProjectName}.pdf");
        }
        if (format.Equals("word", StringComparison.OrdinalIgnoreCase))
        {
            var word = _wordSvc.GenerateGlassCurtainWallReport(bookInput);
            return File(word, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"计算书_{bookInput.ProjectName}.docx");
        }

        return BadRequest(new { error = "不支持的格式，请使用 pdf 或 word" });
    }

    // ===== 旧步骤兼容端点（保留不删，标记过期）=====

    /// <summary>[旧] 步骤2：风荷载 — 已合并到step1</summary>
    [HttpPut("sessions/{id}/step2-wind")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public ActionResult Step2WindLegacy(string id, [FromBody] Step2WindInput input)
    {
        var session = _wizard.GetSession(id);
        if (session == null) return NotFound();
        _wizard.UpdateStep(id, 2, "step2-legacy-wind", input);
        return Ok(new { redirected = true, message = "此端点已弃用，请使用 step2-glass" });
    }

    /// <summary>[旧] 步骤3：地震 — 已合并到step1</summary>
    [HttpPut("sessions/{id}/step3-seismic")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public ActionResult Step3SeismicLegacy(string id, [FromBody] Step3SeismicInput input)
    {
        var session = _wizard.GetSession(id);
        if (session == null) return NotFound();
        _wizard.UpdateStep(id, 3, "step3-legacy-seismic", input);
        return Ok(new { redirected = true, message = "此端点已弃用，请使用 step3-mullion" });
    }

    /// <summary>[旧] 步骤4：玻璃 — 改为step2</summary>
    [HttpPut("sessions/{id}/step4-glass")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public ActionResult Step4GlassLegacy(string id, [FromBody] Step4GlassInput input)
    {
        var session = _wizard.GetSession(id);
        if (session == null) return NotFound();
        _wizard.UpdateStep(id, 4, "step4-legacy-glass", input);
        return Ok(new { redirected = true, message = "此端点已弃用，请使用 step2-glass" });
    }

    /// <summary>[旧] 步骤5：框架 — 拆分为step3+step4</summary>
    [HttpPut("sessions/{id}/step5-frame")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public ActionResult Step5FrameLegacy(string id, [FromBody] Step5FrameInput input)
    {
        var session = _wizard.GetSession(id);
        if (session == null) return NotFound();
        _wizard.UpdateStep(id, 5, "step5-legacy-frame", input);
        return Ok(new { redirected = true, message = "此端点已弃用，请使用 step3-mullion 和 step4-transom" });
    }

    /// <summary>[旧] 步骤6：连接件</summary>
    [HttpPut("sessions/{id}/step6-connection")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public ActionResult Step6ConnectionLegacy(string id, [FromBody] Step6ConnectionInput input)
    {
        var session = _wizard.GetSession(id);
        if (session == null) return NotFound();
        _wizard.UpdateStep(id, 6, "step6-legacy-connection", input);
        return Ok(new { redirected = true, message = "此端点已弃用，请使用 step5-connection" });
    }

    /// <summary>[旧] 步骤7：胶缝</summary>
    [HttpPut("sessions/{id}/step7-silicone")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public ActionResult Step7SiliconeLegacy(string id, [FromBody] Step7SiliconeInput input)
    {
        var session = _wizard.GetSession(id);
        if (session == null) return NotFound();
        _wizard.UpdateStep(id, 7, "step7-legacy-silicone", input);
        return Ok(new { redirected = true, message = "此端点已弃用，请使用 step6-embedded" });
    }

    #region 辅助方法

    /// <summary>Step1 自动计算</summary>
    private Step1CalcResult CalcStep1(Step1BasicInput input)
    {
        // 从属面积 = (左+右)/2 × (上分格高+下分格高)/2
        double bPanel = (input.LeftPanelWidth + input.RightPanelWidth) / 2.0;
        double hPanel = (input.UpperPanelHeight + input.LowerPanelHeight) / 2.0;
        double tributaryArea = bPanel * hPanel / 1e6; // m²

        // μz 风压高度变化系数（简化版，按B类地面）
        double h = input.CalcElevation > 0 ? input.CalcElevation : input.BuildingHeight;
        double muZ = input.TerrainCategory switch
        {
            "A" => Math.Pow(h / 10.0, 0.24) * 1.284,
            "B" => Math.Pow(h / 10.0, 0.30) * 1.000,
            "C" => Math.Pow(h / 10.0, 0.44) * 0.544,
            "D" => Math.Pow(h / 10.0, 0.60) * 0.262,
            _ => 1.0
        };
        muZ = Math.Max(muZ, input.TerrainCategory switch { "A" => 1.09, "B" => 1.00, "C" => 0.54, "D" => 0.26, _ => 1.0 });

        // βgz 阵风系数（简化查表插值）
        double betaGz = input.TerrainCategory switch
        {
            "A" => 1.65 + 0.35 * Math.Log10(Math.Max(h, 5) / 5),
            "B" => 1.80 + 0.40 * Math.Log10(Math.Max(h, 5) / 5),
            "C" => 2.05 + 0.50 * Math.Log10(Math.Max(h, 5) / 5),
            "D" => 2.40 + 0.60 * Math.Log10(Math.Max(h, 5) / 5),
            _ => 2.0
        };

        // μs1 局部体型系数
        double muSe = input.ExternalShapeMode switch
        {
            "wall_center" => -1.0,
            "wall_corner" => -1.8,
            "custom" => input.ExternalShapeValue,
            _ => -1.0
        };
        double muSi = input.InternalShapeMode switch
        {
            "sealed" => 0.2,
            "custom" => input.InternalShapeValue,
            _ => 0.2
        };
        double muS1 = Math.Abs(muSe) + Math.Abs(muSi);

        // wk = βgz × μs1 × μz × w₀
        double w0 = input.BasicWindPressure / 1000.0; // Pa → kPa
        double wkPanel = betaGz * muS1 * muZ * w0;

        // 构件风荷载（面积折减后）
        double areaReduction = tributaryArea <= 1 ? 1.0 :
            tributaryArea <= 25 ? 1.0 - 0.2 * Math.Log10(tributaryArea) : 0.72;
        double wkStructure = wkPanel * areaReduction;

        // 地震线荷载（简化）
        double alpha = input.AlphaMax > 0 ? input.AlphaMax : input.SeismicIntensity switch
        {
            6 => 0.04, 7 => 0.08, 8 => 0.16, 9 => 0.32, _ => 0.08
        };
        double qEAk = alpha * 0.5; // 简化：需要构件自重

        // 荷载组合 1.4wk + 0.7×1.3×qEAk
        double loadCombo = 1.4 * wkPanel + 0.7 * 1.3 * qEAk;

        return new Step1CalcResult
        {
            TributaryArea = tributaryArea,
            MuZ = Math.Round(muZ, 3),
            BetaGz = Math.Round(betaGz, 3),
            MuS1 = Math.Round(muS1, 3),
            WkPanel = Math.Round(wkPanel, 3),
            WkStructure = Math.Round(wkStructure, 3),
            SeismicLineLoad = Math.Round(qEAk, 3),
            LoadCombination = Math.Round(loadCombo, 3)
        };
    }

    /// <summary>从会话数据构建计算书输入模型</summary>
    private GlassCalcBookInput BuildCalcBookInput(WizardSession session)
    {
        var step1 = GetStepData<Step1BasicInput>(session, "step1");
        var step8 = GetStepData<Step8ReportInput>(session, "step8");

        return new GlassCalcBookInput
        {
            ProjectName = step8?.EngineeringName ?? step1?.ProjectName,
            CompanyName = step8?.DesignCompany ?? step1?.CompanyName,
            Designer = step8?.Author ?? step1?.Designer,
            Reviewer = step8?.Reviewer ?? step1?.Reviewer,
            Approver = step8?.Approver ?? step1?.Approver,
            Supervisor = step8?.Supervisor,
            ReportDate = step8?.ReportDate ?? DateTime.Now.ToString("yyyy年M月d日"),
            City = step1?.City,
            BuildingHeight = step1?.BuildingHeight ?? 0,
            InstallHeight = step1?.CalcElevation ?? step1?.InstallHeight ?? 0,
            BasicWindPressure = step1?.BasicWindPressure ?? 0,
            TerrainCategory = step1?.TerrainCategory,
            SeismicIntensity = step1?.SeismicIntensity ?? 0,
        };
    }

    private T? GetStepData<T>(WizardSession session, string key) where T : class
    {
        if (session.StepData.TryGetValue(key, out var data) && data != null)
        {
            if (data is T typed) return typed;
            if (data is JsonElement je)
                return JsonSerializer.Deserialize<T>(je.GetRawText());
        }
        return null;
    }

    private async Task SaveCalcRecord(int? projectId, CalcType calcType, object input, object result)
    {
        if (projectId == null) return;
        var record = new CalculationRecord
        {
            ProjectId = projectId.Value,
            CalcType = calcType,
            InputJson = JsonSerializer.Serialize(input),
            OutputJson = JsonSerializer.Serialize(result),
            Status = "Success"
        };
        _db.CalculationRecords.Add(record);
        await _db.SaveChangesAsync();
    }

    #endregion
}
