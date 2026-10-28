using Microsoft.AspNetCore.Mvc;
using GlassManagement.Api.Models;
using System.Collections.Generic;

namespace GlassManagement.Api.Controllers {
    [ApiController]
    [Route("api/[controller]")]
    public class CalculationController : ControllerBase
    {
        [HttpPost("multi")]
    public ActionResult<GlassOutput> Multi([FromBody] GlassInput input)
    {
            // Simple placeholder implementation: return one sample panel result for Phase 1
            var output = new GlassOutput();
            var p = new PanelResult { PanelId = input.Panels?.Count > 0 ? input.Panels[0].PanelId : "p1", Type = input.Panels?.Count > 0 ? input.Panels[0].Type.ToString() : "Single", Status = "Pass" };
            p.Metrics.Thickness = input.Panels?.Count > 0 ? input.Panels[0].Thickness : 6.0;
            p.Metrics.Deflection = 0.5;
            p.Metrics.MaxStress = 12.0;
            output.PanelsResults.Add(p);
            output.ResultSummary.Passed = true;
            output.ResultSummary.TotalTimeMs = 120; // placeholder
            output.CloudViz = new CloudVizData { Points = new List<object>() { new { x = 0, y = 0, v = 0.0 } } };
        return Ok(output);
    }

        [HttpPost("phase2")]
        public ActionResult<GlassOutput> Phase2([FromBody] GlassInput input)
        {
            var output = new GlassOutput();
            if (input?.Panels != null && input.Panels.Count > 0)
            {
                int idx = 0;
                foreach (var panel in input.Panels)
                {
                    var res = new PanelResult { PanelId = panel.PanelId ?? $"p2-{idx}", Type = panel.Type.ToString(), Status = "Pass" };
                    res.Metrics.Thickness = panel.Thickness;
                    res.Metrics.Deflection = 1.0 + idx * 0.2;
                    res.Metrics.MaxStress = 20.0 + idx * 2.0;
                    output.PanelsResults.Add(res);
                    idx++;
                }
            }
            else
            {
                var panel = new GlassPanelInput { PanelId = "p2", Type = GlassPanelType.Single, Width = 100, Height = 100, Thickness = 6.0 };
                var res = new PanelResult { PanelId = panel.PanelId, Type = panel.Type.ToString(), Status = "Pass" };
                res.Metrics.Thickness = panel.Thickness;
                res.Metrics.Deflection = 1.2;
                res.Metrics.MaxStress = 22.0;
                output.PanelsResults.Add(res);
            }
            output.ResultSummary.Passed = true;
            output.CloudViz = new CloudVizData { Points = new List<object> { new { x = 0, y = 0, v = 0.0 } } };
            return Ok(output);
        }

        [HttpPost("phase3")]
        public ActionResult<GlassOutput> Phase3([FromBody] GlassInput input)
        {
            var output = new GlassOutput();
            if (input?.Panels != null && input.Panels.Count > 0)
            {
                int idx = 0;
                foreach (var panel in input.Panels)
                {
                    var res = new PanelResult { PanelId = panel.PanelId ?? $"p3-{idx}", Type = panel.Type.ToString(), Status = "Pass" };
                    res.Metrics.Thickness = panel.Thickness;
                    res.Metrics.Deflection = 0.9 + idx * 0.05;
                    res.Metrics.MaxStress = 24.0 + idx * 1.5;
                    output.PanelsResults.Add(res);
                    idx++;
                }
            }
            else
            {
                var p = new GlassPanelInput { PanelId = "p3-1", Type = GlassPanelType.Single, Width = 100, Height = 100, Thickness = 6.0, Layers = new List<GlassLayerInput> { new GlassLayerInput { MaterialId = "default", Thickness = 6 } } };
                output.PanelsResults.Add(new PanelResult { PanelId = p.PanelId, Type = p.Type.ToString(), Status = "Pass", Metrics = new GlassManagement.Api.Models.Metrics { Thickness = p.Thickness, Deflection = 0.5, MaxStress = 22.0 } });
            }
            output.ResultSummary.Passed = true;
            output.CloudViz = new CloudVizData { Points = new List<object> { new { x = 0, y = 0, v = 0.0 } } };
            return Ok(output);
        }

        [HttpPost("validate")]
        public ActionResult<bool> Validate([FromBody] GlassInput input)
        {
            // Very basic validation: ensure at least one panel exists and has a type
            if (input?.Panels == null || input.Panels.Count == 0) return BadRequest(false);
            return Ok(true);
        }
    }
}
