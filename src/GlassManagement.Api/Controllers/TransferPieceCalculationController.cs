using Microsoft.AspNetCore.Mvc;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

/// <summary>转接件计算 - 计算书第9章</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("转接件计算")]
public class TransferPieceCalculationController : ControllerBase
{
    /// <summary>转接件强度和挠度计算</summary>
    [HttpPost("Calculate")]
    [ProducesResponseType(typeof(TransferPieceCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<TransferPieceCalculationResult> Calculate(
        [FromBody] TransferPieceCalculationRequest req)
    {
        var steps = new List<CalculationStep>();
        int sn = 1;

        double N = req.TensionForce;
        double V = req.ShearForce;
        double M = req.BendingMoment;
        double A = req.SectionArea;
        double W = req.SectionModulus;
        double I = req.MomentOfInertia;
        double gamma = req.Gamma;
        double f = req.AllowableStress;
        double E = req.ElasticModulus;
        double e = req.Eccentricity;
        int k = req.IsDualTransfer ? 2 : 1;

        steps.Add(new() { StepNo = sn++, Name = "受力分析",
            Result = $"N={N:F1}N, V={V:F1}N, M={M:F1}N·mm, {(req.IsDualTransfer ? "双" : "单")}转接件" });

        steps.Add(new() { StepNo = sn++, Name = "截面特性",
            Result = $"A={A}mm², W={W}mm³, I={I}mm⁴, 规格: {req.Specification}" });

        // 强度校核: σ = N/A/k + M/(γ×W)/k ≤ f
        double sigma = N / A / k + M / (gamma * W) / k;
        bool strengthOk = sigma <= f;

        steps.Add(new() { StepNo = sn++, Name = "强度校核",
            Formula = $"σ = N/A/{k} + M/(γ×W)/{k} ≤ f",
            Process = $"σ = {N:F1}/{A}/{k} + {M:F1}/({gamma}×{W})/{k}",
            Result = $"σ = {sigma:F3} MPa {(strengthOk ? "≤" : ">")} f = {f} MPa → {(strengthOk ? "✅ 通过" : "❌ 不通过")}" });

        // 挠度校核: df = Vk×e³/(3×E×I)/k ≤ df,lim
        double Vk = V / 1.3;
        double df = Vk * Math.Pow(e, 3) / (3 * E * I) / k;
        double dfLimit = e / 125.0;
        bool deflectionOk = df <= dfLimit;

        steps.Add(new() { StepNo = sn++, Name = "挠度校核",
            Formula = $"df = Vk×e³/(3×E×I)/{k} ≤ df,lim = e/125",
            Process = $"Vk = {V:F1}/1.3 = {Vk:F1}N, e = {e}mm",
            Result = $"df = {df:F6} mm {(deflectionOk ? "≤" : ">")} df,lim = {dfLimit:F3} mm → {(deflectionOk ? "✅ 通过" : "❌ 不通过")}" });

        return Ok(new TransferPieceCalculationResult
        {
            Passed = strengthOk && deflectionOk,
            StrengthCheck = new CheckResult
            {
                Passed = strengthOk,
                ActualValue = Math.Round(sigma, 3),
                AllowableValue = f,
                Description = $"σ = {sigma:F3} MPa ≤ f = {f} MPa"
            },
            DeflectionCheck = new CheckResult
            {
                Passed = deflectionOk,
                ActualValue = Math.Round(df, 6),
                AllowableValue = Math.Round(dfLimit, 3),
                Description = $"df = {df:F6} mm ≤ {dfLimit:F3} mm"
            },
            Standard = "GB50017-2017",
            Steps = steps
        });
    }
}
