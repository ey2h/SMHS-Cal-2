using Microsoft.AspNetCore.Mvc;
using GlassManagement.Api.Services;

namespace GlassManagement.Api.Controllers;

/// <summary>璁＄畻涔DF鐢熸垚API</summary>
[ApiController]
[Route("api/[controller]")]
[Tags("璁＄畻涔DF")]
public class CalcBookPdfController : ControllerBase
{
    private readonly CalcBookPdfService _pdfSvc;
    private readonly CalcBookWordService _wordSvc;
    private readonly CalcBookWordV2Service _wordV2Svc;

    public CalcBookPdfController(CalcBookPdfService pdfSvc, CalcBookWordService wordSvc, CalcBookWordV2Service wordV2Svc)
    {
        _pdfSvc = pdfSvc;
        _wordSvc = wordSvc;
        _wordV2Svc = wordV2Svc;
    }

    /// <summary>鐢熸垚鐜荤拑骞曞璁＄畻涔DF</summary>
    [HttpPost("glass-curtainwall")]
    public IActionResult GenerateGlassCurtainWall([FromBody] GlassCalcBookInput input)
    {
        try
        {
            var pdf = _pdfSvc.GenerateGlassCurtainWallReport(input);
            var filename = $"鐜荤拑骞曞璁＄畻涔{input.ProjectName ?? "椤圭洰"}_{DateTime.Now:yyyyMMdd}.pdf";
            return File(pdf, "application/pdf", filename);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>鐢熸垚鐜荤拑骞曞璁＄畻涔ord</summary>
    [HttpPost("glass-curtainwall/word")]
    public IActionResult GenerateGlassCurtainWallWord([FromBody] GlassCalcBookInput input)
    {
        try
        {
            var word = _wordV2Svc.GenerateGlassCurtainWallReport(input);
            var filename = $"鐜荤拑骞曞璁＄畻涔{input.ProjectName ?? "椤圭洰"}_{DateTime.Now:yyyyMMdd}.docx";
            return File(word, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", filename);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }


    /// <summary>生成玻璃幕墙计算书 Word（V2：样板章节+梁图插入）</summary>
    [HttpPost("glass-curtainwall/word-v2")]
    public IActionResult GenerateGlassCurtainWallWordV2([FromBody] GlassCalcBookInput input)
    {
        try
        {
            var word = _wordV2Svc.GenerateGlassCurtainWallReport(input);
            var filename = $"计算书_{input.ProjectName ?? "项目"}_{DateTime.Now:yyyyMMdd}_V2.docx";
            return File(word, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", filename);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
    /// <summary>鐢熸垚鐜荤拑骞曞璁＄畻涔ord锛堜娇鐢ㄧず渚嬫暟鎹級</summary>
    [HttpGet("glass-curtainwall/word/demo")]
    public IActionResult GenerateWordDemo()
    {
        var input = CreateDemoInput();
        var word = _wordV2Svc.GenerateGlassCurtainWallReport(input);
        return File(word, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "鐜荤拑骞曞璁＄畻涔绀轰緥.docx");
    }

    /// <summary>鐢熸垚鐜荤拑骞曞璁＄畻涔DF锛堜娇鐢ㄧず渚嬫暟鎹級</summary>
    [HttpGet("glass-curtainwall/demo")]
    public IActionResult GenerateDemo()
    {
        var input = CreateDemoInput();
        var pdf = _pdfSvc.GenerateGlassCurtainWallReport(input);
        return File(pdf, "application/pdf", "鐜荤拑骞曞璁＄畻涔绀轰緥.pdf");
    }

    private static GlassCalcBookInput CreateDemoInput() => new()
    {
        ProjectName = "Demo Curtain Wall Project",
        CompanyName = "Demo Engineering Co., Ltd.",
        Designer = "Designer A",
        Reviewer = "Reviewer B",
        Approver = "Chief Engineer C",
        Supervisor = "Manager D",
        ReportDate = DateTime.Now.ToString("yyyy-MM-dd"),
        City = "Shanghai",
        BuildingHeight = 60,
        InstallHeight = 55,
        PanelWidth = 1200,
        PanelHeight = 3600,
        BasicWindPressure = 450,
        TerrainCategory = "B",
        SeismicIntensity = 8,
        GlassDescription = "6T + 12A + 6T IGU",
        GustFactor = 1.92,
        ShapeFactor = -1.4,
        HeightFactor = 1.65,
        WindPressureDesign = 2000,
        GlassCalc = new GlassCalcResult
        {
            ShortSide = 1200,
            LongSide = 3600,
            EquivalentThickness = 8.49,
            LoadDesign = 2000,
            StressCoeff = 0.0624,
            DesignStrength = 84,
            MaxStress = 24.6,
            DeflectionCoeff = 0.0083,
            ElasticModulus = 72000,
            MaxDeflection = 6.8,
            DeflectionLimit = 20
        },
        Column = new MemberCalcResult
        {
            ProfileCode = "60/140",
            SupportType = "Simple",
            Span = 3600,
            Ix = 351.63,
            Wx = 48.35,
            Area = 1351,
            LoadWidth = 1200,
            MaxStress = 67.2,
            AllowableStress = 100,
            MaxDeflection = 8.5,
            DeflectionLimit = 20
        },
        Beam = new MemberCalcResult
        {
            ProfileCode = "60/80",
            SupportType = "Simple",
            Span = 1200,
            Ix = 88.6,
            Wx = 21.45,
            Area = 991,
            LoadWidth = 1800,
            MaxStress = 45.8,
            AllowableStress = 100,
            MaxDeflection = 3.2,
            DeflectionLimit = 8
        }
    };
}
