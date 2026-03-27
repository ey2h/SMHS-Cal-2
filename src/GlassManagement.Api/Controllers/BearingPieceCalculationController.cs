using Microsoft.AspNetCore.Mvc;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

/// <summary>承托件计算 - 计算书第14章</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("承托件计算")]
public class BearingPieceCalculationController : ControllerBase
{
    /// <summary>承托件抗弯强度和挠度计算</summary>
    [HttpPost("Calculate")]
    [ProducesResponseType(typeof(BearingPieceCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<BearingPieceCalculationResult> Calculate(
        [FromBody] BearingPieceCalculationRequest req)
    {
        var steps = new List<CalculationStep>();
        int sn = 1;

        double B = req.PanelWidth;
        double H = req.PanelHeight;
        double t2 = req.TotalGlassThickness;
        double t = req.PieceThickness;
        double L = req.PieceLength;
        double e = req.Eccentricity;
        double w = req.CantileverLength;
        int n = req.PieceCount;
        double f = req.AllowableStress;
        double E = req.ElasticModulus;
        double rho = req.GlassDensity; // kN/m³

        // (1) 自重荷载设计值
        // G = 1.3 × ρ × B × H × t2
        double G = 1.3 * rho / 1e6 * B * H * t2;

        steps.Add(new() { StepNo = sn++, Name = "自重荷载设计值",
            Formula = "G = 1.3×ρ×B×H×t2",
            Process = $"G = 1.3×{rho}/10⁶×{B}×{H}×{t2}",
            Result = $"G = {G:F3} N" });

        // (2) 每个承托件根部弯矩
        // M = G×e/n
        double M = G * e / n;

        steps.Add(new() { StepNo = sn++, Name = "单个承托件弯矩",
            Formula = "M = G×e/n",
            Process = $"M = {G:F3}×{e}/{n}",
            Result = $"M = {M:F3} N·mm" });

        // (3) 截面抵抗矩
        // W = L×t²/6
        double W_sec = L * t * t / 6.0;

        steps.Add(new() { StepNo = sn++, Name = "截面抵抗矩",
            Formula = "W = L×t²/6",
            Process = $"W = {L}×{t}²/6",
            Result = $"W = {W_sec:F3} mm³" });

        // (4) 抗弯强度校核 M/W ≤ f
        double sigma = M / W_sec;
        bool bendingOk = sigma <= f;

        steps.Add(new() { StepNo = sn++, Name = "抗弯强度校核",
            Formula = "M/W ≤ f",
            Process = $"{M:F3}/{W_sec:F3}",
            Result = $"σ = {sigma:F3} MPa {(bendingOk ? "≤" : ">")} {f} MPa → {(bendingOk ? "✅ 通过" : "❌ 不通过")}" });

        // (5) 挠度校核
        // Gk = G/1.3/n (每个承托件标准值)
        double Gk = G / 1.3 / n;
        // I = L×t³/12
        double I_sec = L * t * t * t / 12.0;
        // df = Gk×e²×w×(3-e/w) / (6×E×I)
        double df = Gk * e * e * w * (3 - e / w) / (6 * E * I_sec);
        // 悬臂挠度限值: 2w/250
        double dfLimit = 2 * w / 250.0;
        bool deflectionOk = df <= dfLimit;

        steps.Add(new() { StepNo = sn++, Name = "挠度校核",
            Formula = "df = Gk×e²×w×(3-e/w)/(6×E×I) ≤ 2w/250",
            Process = $"Gk={Gk:F3}N, I={I_sec:F3}mm⁴",
            Result = $"df = {df:F4} mm {(deflectionOk ? "≤" : ">")} df,lim = {dfLimit:F3} mm → {(deflectionOk ? "✅ 通过" : "❌ 不通过")}" });

        return Ok(new BearingPieceCalculationResult
        {
            Passed = bendingOk && deflectionOk,
            BendingCheck = new CheckResult
            {
                Passed = bendingOk,
                ActualValue = Math.Round(sigma, 3),
                AllowableValue = f,
                Description = $"σ = {sigma:F3} MPa ≤ f = {f} MPa"
            },
            DeflectionCheck = new CheckResult
            {
                Passed = deflectionOk,
                ActualValue = Math.Round(df, 4),
                AllowableValue = Math.Round(dfLimit, 3),
                Description = $"df = {df:F4} mm ≤ {dfLimit:F3} mm"
            },
            Standard = "JGJ 102-2003",
            Steps = steps
        });
    }
}
