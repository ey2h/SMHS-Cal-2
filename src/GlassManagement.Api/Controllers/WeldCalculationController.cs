using Microsoft.AspNetCore.Mvc;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

/// <summary>焊缝计算 - 按GB50017-2017</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("焊缝计算")]
public class WeldCalculationController : ControllerBase
{
    /// <summary>L型角焊缝计算</summary>
    [HttpPost("Calculate")]
    [ProducesResponseType(typeof(WeldCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<WeldCalculationResult> Calculate(
        [FromBody] WeldCalculationRequest req)
    {
        var steps = new List<CalculationStep>();
        int sn = 1;

        double Lv = req.VerticalWeldLength;
        double Lh = req.HorizontalWeldLength;
        double hf = req.FilletSize;
        double N = req.TensionForce;
        double V = req.ShearForce;
        double M = req.BendingMoment;

        // (1) 焊缝有效厚度 he = 0.7×hf
        double he = 0.7 * hf;

        steps.Add(new() { StepNo = sn++, Name = "焊缝有效厚度",
            Formula = "he = 0.7×hf",
            Process = $"he = 0.7×{hf}",
            Result = $"he = {he:F1} mm" });

        // (2) 焊缝总面积 A = he×((Lv-2hf)+(Lh-2hf))
        double Lv_eff = Lv - 2 * hf;
        double Lh_eff = Lh - 2 * hf;
        double A_weld = he * (Lv_eff + Lh_eff);

        steps.Add(new() { StepNo = sn++, Name = "焊缝总面积",
            Formula = "A = he×((Lv-2hf)+(Lh-2hf))",
            Process = $"A = {he}×(({Lv}-2×{hf})+({Lh}-2×{hf}))",
            Result = $"A = {A_weld:F1} mm²" });

        // (3) 中性轴位置、惯性矩、抵抗矩
        // d: 水平焊缝到中性轴距离
        double d_na = 0.5 * (Lv_eff * Lv_eff - Lh_eff * he) / (Lv_eff + Lh_eff);
        d_na = Math.Max(d_na, 0);

        // I = he×(Lv-2hf)³/12 + (Lh-2hf)×he³/12 + he×(Lv-2hf)×((Lv-2hf)/2-d)² + (Lh-2hf)×he×(d+he/2)²
        double I_weld = he * Math.Pow(Lv_eff, 3) / 12
                      + Lh_eff * Math.Pow(he, 3) / 12
                      + he * Lv_eff * Math.Pow(Lv_eff / 2 - d_na, 2)
                      + Lh_eff * he * Math.Pow(d_na + he / 2, 2);

        double W_weld = I_weld / (Lv_eff - d_na);

        steps.Add(new() { StepNo = sn++, Name = "截面参数",
            Formula = "I, W (L型角焊缝截面)",
            Process = $"d(中性轴) = {d_na:F3}mm",
            Result = $"I = {I_weld:F1} mm⁴, W = {W_weld:F1} mm³" });

        // (4) 焊缝校核 ((σf/βf)²+τf²)^0.5 / k ≤ ffw
        double betaF = req.BetaF;
        double ffw = req.WeldStrength;
        int k = req.IsDualTransfer ? 2 : 1; // 双转接件除以2

        double sigma_f = N / (betaF * A_weld) + M / (betaF * W_weld);
        double tau_f = V / A_weld;
        double combined = Math.Sqrt(Math.Pow(sigma_f, 2) + Math.Pow(tau_f, 2)) / k;

        bool passed = combined <= ffw;

        steps.Add(new() { StepNo = sn++, Name = "焊缝强度校核",
            Formula = $"((σf/βf)²+τf²)^0.5/{k} ≤ ffw  ……11.2.2-3[GB50017-2017]",
            Process = $"σf = N/(βf×A)+M/(βf×W) = {N:F1}/(1.22×{A_weld:F1})+{M:F1}/(1.22×{W_weld:F1})\nτf = V/A = {V:F1}/{A_weld:F1}",
            Result = $"= {combined:F3} MPa {(passed ? "≤" : ">")} {ffw} MPa → {(passed ? "✅ 通过" : "❌ 不通过")}" });

        return Ok(new WeldCalculationResult
        {
            Passed = passed,
            EffectiveThickness = Math.Round(he, 1),
            WeldArea = Math.Round(A_weld, 1),
            MomentOfInertia = Math.Round(I_weld, 1),
            SectionModulus = Math.Round(W_weld, 1),
            StrengthCheck = new CheckResult
            {
                Passed = passed,
                ActualValue = Math.Round(combined, 3),
                AllowableValue = ffw,
                Description = $"组合应力 = {combined:F3} MPa ≤ ffw = {ffw} MPa"
            },
            Standard = "GB50017-2017",
            Steps = steps
        });
    }
}
