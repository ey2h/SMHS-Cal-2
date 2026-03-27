using System.Globalization;
using System.Text;
using GlassManagement.Api.Services.StaticCalcEngine;
using GlassManagement.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace GlassManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DiagramController : ControllerBase
{
    private readonly StaticCalcService _staticCalcService;

    public DiagramController(StaticCalcService staticCalcService)
    {
        _staticCalcService = staticCalcService;
    }

    [HttpPost("MullionBeam")]
    [Produces("image/svg+xml")]
    public IActionResult MullionBeam([FromBody] MullionDiagramRequest req)
    {
        var total = req.L > 0 ? req.L : Math.Max(req.L1 + req.L2, 1);
        var beamType = (req.BeamType ?? string.Empty).ToLowerInvariant();
        var mode = beamType.Contains("cantilever") || beamType.Contains("悬") ? SupportCondition.Cantilever
            : beamType.Contains("double") || beamType.Contains("双") ? SupportCondition.DoubleSpan
            : SupportCondition.SimpleBeam;

        var request = new BeamCalcRequest
        {
            Support = mode,
            Load = LoadType.Uniform,
            L = total / 1000.0,
            L2 = req.L2 / 1000.0,
            Q = req.Q
        };

        return Content(BeamDiagramGenerator.GenerateMechanicalDiagram(request), "image/svg+xml");
    }

    [HttpPost("TransomBeam")]
    [Produces("image/svg+xml")]
    public IActionResult TransomBeam([FromBody] TransomDiagramRequest req)
    {
        var load = (req.LoadType ?? string.Empty).ToLowerInvariant();
        var lt = load.Contains("triangle") || load.Contains("三角") ? LoadType.Triangular
            : load.Contains("trape") || load.Contains("梯") ? LoadType.Trapezoidal
            : LoadType.Uniform;

        var request = new BeamCalcRequest
        {
            Support = SupportCondition.SimpleBeam,
            Load = lt,
            L = Math.Max(req.B, 1) / 1000.0,
            Q = req.Q,
            Q1 = req.Q,
            Q2 = req.Q
        };

        return Content(BeamDiagramGenerator.GenerateMechanicalDiagram(request), "image/svg+xml");
    }

    [HttpPost("PlateSupport")]
    [Produces("image/svg+xml")]
    public IActionResult PlateSupport([FromBody] PlateSupportDiagramRequest req)
    {
        var w = 800;
        var h = 420;
        var side = req.SupportType ?? "Plate";
        var sb = new StringBuilder();
        sb.AppendLine($"<svg xmlns='http://www.w3.org/2000/svg' width='{w}' height='{h}' viewBox='0 0 {w} {h}'>");
        sb.AppendLine($"<rect width='{w}' height='{h}' fill='white' />");
        sb.AppendLine($"<text x='{w / 2}' y='28' text-anchor='middle' font-size='18' font-weight='700'>Plate Support ({Escape(side)})</text>");
        sb.AppendLine("<rect x='220' y='90' width='360' height='220' fill='#DBEAFE' stroke='#1D4ED8' stroke-width='2' />");
        sb.AppendLine($"<text x='{w / 2}' y='340' text-anchor='middle' font-size='13'>a={F(req.A)} mm, b={F(req.B)} mm, t={F(req.T)} mm</text>");
        sb.AppendLine("</svg>");
        return Content(sb.ToString(), "image/svg+xml");
    }

    [HttpPost("ConnectionDetail")]
    [Produces("image/svg+xml")]
    public IActionResult ConnectionDetail([FromBody] ConnectionDiagramRequest req)
    {
        var w = 820;
        var h = 420;
        var type = req.ConnectionType ?? "Connection";
        var sb = new StringBuilder();
        sb.AppendLine($"<svg xmlns='http://www.w3.org/2000/svg' width='{w}' height='{h}' viewBox='0 0 {w} {h}'>");
        sb.AppendLine($"<rect width='{w}' height='{h}' fill='white' />");
        sb.AppendLine($"<text x='{w / 2}' y='28' text-anchor='middle' font-size='18' font-weight='700'>Connection ({Escape(type)})</text>");
        sb.AppendLine("<rect x='180' y='110' width='110' height='190' fill='#E5E7EB' stroke='#374151' stroke-width='2' />");
        sb.AppendLine("<rect x='290' y='190' width='300' height='30' fill='#BFDBFE' stroke='#1D4ED8' stroke-width='2' />");
        sb.AppendLine($"<text x='{w / 2}' y='340' text-anchor='middle' font-size='13'>bolt d={F(req.BoltDiameter)} mm, plate t={F(req.PlateThickness)} mm</text>");
        sb.AppendLine("</svg>");
        return Content(sb.ToString(), "image/svg+xml");
    }

    [HttpPost("beam/all")]
    [Produces("application/json")]
    public ActionResult<object> BeamAll([FromBody] BeamCalcRequest request)
    {
        if (request.L <= 0)
        {
            return BadRequest("L must be greater than 0");
        }

        if (request.NumPoints < 10)
        {
            request.NumPoints = 100;
        }
        else if (request.NumPoints > 1000)
        {
            request.NumPoints = 1000;
        }

        var result = _staticCalcService.Calculate(request);

        return Ok(new
        {
            mechanicalDiagramSvg = result.MechanicalDiagramSvg,
            momentDiagramSvg = result.MomentDiagramSvg,
            shearDiagramSvg = result.ShearDiagramSvg,
            deflectionDiagramSvg = result.DeflectionDiagramSvg
        });
    }

    private static string Escape(string value)
    {
        return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }

    private static string F(double v)
    {
        return v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
