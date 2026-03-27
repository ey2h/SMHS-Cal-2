using Microsoft.AspNetCore.Mvc;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

/// <summary>玻璃副框计算 - 计算书第13章</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("玻璃副框计算")]
public class SubFrameCalculationController : ControllerBase
{
    /// <summary>副框抗拉和抗剪强度计算</summary>
    [HttpPost("Calculate")]
    [ProducesResponseType(typeof(SubFrameCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<SubFrameCalculationResult> Calculate(
        [FromBody] SubFrameCalculationRequest req)
    {
        var steps = new List<CalculationStep>();
        int sn = 1;

        double B = req.PanelWidth;
        double H = req.PanelHeight;
        double t1 = req.TotalGlassThickness;
        double S = req.FramePerimeter;
        double a = req.FrameThickness;
        double q = req.DesignLoad;
        double f = req.AllowableBendingStress;
        double fv = req.AllowableShearStress;
        double rho = req.GlassDensity; // kN/m³

        // (1) 承受荷载的断面总面积
        double A_sec = S * a;

        steps.Add(new() { StepNo = sn++, Name = "副框断面面积",
            Formula = "A = S × a",
            Process = $"A = {S} × {a}",
            Result = $"A = {A_sec:F0} mm²" });

        // (2) 抗拉强度 N/A ≤ f
        double N = q * B * H;

        steps.Add(new() { StepNo = sn++, Name = "水平总荷载",
            Formula = "N = q × B × H",
            Process = $"N = {q} × {B} × {H}",
            Result = $"N = {N:F3} N" });

        double sigma_t = N / A_sec;
        bool tensionOk = sigma_t <= f;

        steps.Add(new() { StepNo = sn++, Name = "抗拉强度校核",
            Formula = "N/A ≤ f",
            Process = $"{N:F3}/{A_sec:F0}",
            Result = $"σ = {sigma_t:F3} MPa {(tensionOk ? "≤" : ">")} {f} MPa → {(tensionOk ? "✅ 通过" : "❌ 不通过")}" });

        // (3) 抗剪强度 V/A ≤ fv
        // V = 1.3 × ρ × B × H × t1 (玻璃自重)
        double V = 1.3 * rho / 1e6 * B * H * t1;

        steps.Add(new() { StepNo = sn++, Name = "玻璃自重剪力",
            Formula = "V = 1.3×ρ×B×H×t1",
            Process = $"V = 1.3×{rho}/10⁶×{B}×{H}×{t1}",
            Result = $"V = {V:F3} N" });

        double tau = V / A_sec;
        bool shearOk = tau <= fv;

        steps.Add(new() { StepNo = sn++, Name = "抗剪强度校核",
            Formula = "V/A ≤ fv",
            Process = $"{V:F3}/{A_sec:F0}",
            Result = $"τ = {tau:F3} MPa {(shearOk ? "≤" : ">")} {fv} MPa → {(shearOk ? "✅ 通过" : "❌ 不通过")}" });

        return Ok(new SubFrameCalculationResult
        {
            Passed = tensionOk && shearOk,
            TensionCheck = new CheckResult
            {
                Passed = tensionOk,
                ActualValue = Math.Round(sigma_t, 3),
                AllowableValue = f,
                Description = $"σ = {sigma_t:F3} MPa ≤ f = {f} MPa"
            },
            ShearCheck = new CheckResult
            {
                Passed = shearOk,
                ActualValue = Math.Round(tau, 3),
                AllowableValue = fv,
                Description = $"τ = {tau:F3} MPa ≤ fv = {fv} MPa"
            },
            Standard = "JGJ 102-2003",
            Steps = steps
        });
    }
}
