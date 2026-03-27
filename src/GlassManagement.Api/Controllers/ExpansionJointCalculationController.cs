using Microsoft.AspNetCore.Mvc;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

/// <summary>伸缩缝及耐候胶计算 - 计算书第11.4、11.5</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("伸缩缝及耐候胶计算")]
public class ExpansionJointCalculationController : ControllerBase
{
    /// <summary>伸缩缝及耐候胶缝宽度计算</summary>
    [HttpPost("Calculate")]
    [ProducesResponseType(typeof(ExpansionJointCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<ExpansionJointCalculationResult> Calculate(
        [FromBody] ExpansionJointCalculationRequest req)
    {
        var steps = new List<CalculationStep>();
        int sn = 1;

        double alphaMullion = req.MullionExpansionCoeff;
        double alphaPanel = req.PanelExpansionCoeff;
        double dt = req.TemperatureDifference;
        double L = req.MullionSpan;
        double b = req.PanelLongSide;
        double d1 = req.ConstructionTolerance;
        double d2 = req.OtherAllowance;
        double delta = req.WeatherSealantCapacity;
        double dc = req.SealantConstructionTolerance;
        double dE = req.SealantOtherAllowance;

        // (1) 伸缩缝计算: d ≥ α×Δt×L + d1 + d2
        double d_calc = alphaMullion * dt * L + d1 + d2;

        steps.Add(new() { StepNo = sn++, Name = "伸缩缝计算",
            Formula = "d = α×Δt×L + d1 + d2",
            Process = $"d = {alphaMullion}×{dt}×{L} + {d1} + {d2}",
            Result = $"d = {d_calc:F3} mm" });

        bool jointOk = req.ActualExpansionJoint >= d_calc;

        steps.Add(new() { StepNo = sn++, Name = "伸缩缝校核",
            Formula = "d实际 ≥ d计算",
            Result = $"d实际 = {req.ActualExpansionJoint} mm {(jointOk ? "≥" : "<")} d计算 = {d_calc:F3} mm → {(jointOk ? "✅ 满足" : "❌ 不满足")}" });

        // (2) 耐候胶缝宽度: ws = α×Δt×b/δ + dc + dE
        double ws_calc = alphaPanel * dt * b / delta + dc + dE;

        steps.Add(new() { StepNo = sn++, Name = "耐候胶缝宽度计算",
            Formula = "ws = α×Δt×b/δ + dc + dE",
            Process = $"ws = {alphaPanel}×{dt}×{b}/{delta} + {dc} + {dE}",
            Result = $"ws = {ws_calc:F3} mm" });

        bool sealantOk = req.ActualSealantWidth >= ws_calc;

        steps.Add(new() { StepNo = sn++, Name = "耐候胶缝校核",
            Formula = "ws实际 ≥ ws计算",
            Result = $"ws实际 = {req.ActualSealantWidth} mm {(sealantOk ? "≥" : "<")} ws计算 = {ws_calc:F3} mm → {(sealantOk ? "✅ 满足" : "❌ 不满足")}" });

        return Ok(new ExpansionJointCalculationResult
        {
            Passed = jointOk && sealantOk,
            RequiredExpansionJoint = Math.Round(d_calc, 3),
            RequiredSealantWidth = Math.Round(ws_calc, 3),
            ExpansionJointCheck = new CheckResult
            {
                Passed = jointOk,
                ActualValue = req.ActualExpansionJoint,
                AllowableValue = Math.Round(d_calc, 3),
                Description = $"实际{req.ActualExpansionJoint}mm ≥ 计算{d_calc:F3}mm"
            },
            SealantWidthCheck = new CheckResult
            {
                Passed = sealantOk,
                ActualValue = req.ActualSealantWidth,
                AllowableValue = Math.Round(ws_calc, 3),
                Description = $"实际{req.ActualSealantWidth}mm ≥ 计算{ws_calc:F3}mm"
            },
            Standard = "JGJ 102-2003",
            Steps = steps
        });
    }
}
