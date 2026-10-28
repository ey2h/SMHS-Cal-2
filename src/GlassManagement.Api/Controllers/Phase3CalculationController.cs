using Microsoft.AspNetCore.Mvc;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

/// <summary>Phase 3 玻璃计算 - 多面板批量计算与云图展示</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("Phase 3 玻璃计算")]
public class Phase3CalculationController : ControllerBase
{
    /// <summary>Phase 3 多面板计算</summary>
    /// <remarks>接收多个玻璃面板输入，返回每个面板的计算结果和云图数据</remarks>
    [HttpPost("phase3")]
    [ProducesResponseType(typeof(Phase3GlassOutput), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<Phase3GlassOutput> Phase3([FromBody] Phase3GlassInput input)
    {
        if (input == null)
        {
            return BadRequest(new ProblemDetails { Title = "请求体为空" });
        }

        if (string.IsNullOrWhiteSpace(input.RegionId))
        {
            return BadRequest(new ProblemDetails { Title = "区域未选择" });
        }

        if (input.Panels == null || input.Panels.Count == 0)
        {
            return BadRequest(new ProblemDetails { Title = "Panel 为空" });
        }

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var panel in input.Panels)
        {
            if (string.IsNullOrWhiteSpace(panel.PanelId))
            {
                return BadRequest(new ProblemDetails { Title = "PanelId 不能为空" });
            }
            if (!seenIds.Add(panel.PanelId))
            {
                return BadRequest(new ProblemDetails { Title = $"PanelId 重复: {panel.PanelId}" });
            }
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var output = new Phase3GlassOutput();
        var cloudVizPoints = new List<Phase3CloudVizPoint>();

        int idx = 0;
        foreach (var panel in input.Panels)
        {
            var result = CalculatePanel(panel, idx);
            output.PanelsResults.Add(result);

            cloudVizPoints.Add(new Phase3CloudVizPoint
            {
                X = idx * 60,
                Y = Math.Max(0, result.Metrics.Deflection * 100),
                Value = result.Metrics.MaxStress
            });

            idx++;
        }

        sw.Stop();

        output.ResultSummary = new Phase3Summary
        {
            Passed = output.PanelsResults.All(p => p.Status == "Pass"),
            TotalTimeMs = sw.ElapsedMilliseconds
        };

        output.CloudViz = new Phase3CloudVizData { Points = cloudVizPoints };

        return Ok(output);
    }

    /// <summary>验证输入但不执行计算</summary>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult<bool> Validate([FromBody] Phase3GlassInput input)
    {
        if (input == null || input.Panels == null || input.Panels.Count == 0)
        {
            return BadRequest(false);
        }

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var panel in input.Panels)
        {
            if (string.IsNullOrWhiteSpace(panel.PanelId))
            {
                return BadRequest(false);
            }
            if (!seenIds.Add(panel.PanelId))
            {
                return BadRequest(false);
            }
        }

        return Ok(true);
    }

    private static Phase3PanelResult CalculatePanel(Phase3GlassPanelInput panel, int index)
    {
        double thickness = panel.Thickness;
        double width = panel.Width;
        double height = panel.Height;

        double aspectRatio = Math.Max(width, height) / Math.Min(width, height);
        double area = (width / 1000.0) * (height / 1000.0);

        double baseStress = 20.0 + (thickness - 6) * -1.5 + area * 2.0;
        double deflection = 0.8 + (thickness - 6) * -0.05 + area * 0.1;
        double edgeEffect = aspectRatio > 2.0 ? 1.15 : 1.0;
        double maxStress = baseStress * edgeEffect;
        double allowableStress = thickness <= 12 ? 28.0 : 36.0;
        double safetyFactor = allowableStress / maxStress;

        string status = safetyFactor >= 1.5 ? "Pass" : "Warning";

        return new Phase3PanelResult
        {
            PanelId = panel.PanelId,
            Type = panel.Type.ToString(),
            Status = status,
            Metrics = new Phase3Metrics
            {
                Thickness = thickness,
                Deflection = Math.Round(deflection, 4),
                MaxStress = Math.Round(maxStress, 4),
                EdgeEffect = Math.Round(edgeEffect, 4),
                SafetyFactor = Math.Round(safetyFactor, 4)
            },
            Warnings = safetyFactor < 1.5 ? $"安全系数 {safetyFactor:F2} 低于推荐值 1.5" : null
        };
    }
}
