using Microsoft.AspNetCore.Mvc;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

/// <summary>压板计算 - 计算书第12章</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("压板计算")]
public class PressurePlateCalculationController : ControllerBase
{
    /// <summary>压板强度及螺栓抗拉计算</summary>
    [HttpPost("Calculate")]
    [ProducesResponseType(typeof(PressurePlateCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<PressurePlateCalculationResult> Calculate(
        [FromBody] PressurePlateCalculationRequest req)
    {
        var steps = new List<CalculationStep>();
        int sn = 1;

        double a = req.PanelShortSide;
        double Byb = req.PlateWidth;
        double Lyb = req.PlateLength;
        double tyb = req.PlateThickness;
        double Syb = req.PlateSpacing;
        double d = req.BoltHoleDiameter;
        double qyb = req.DesignLoad;

        // (1) 压板单侧受力
        // Pyb = 1.25 × qyb × Syb × a / 2
        double Pyb = 1.25 * qyb * Syb * a / 2;

        steps.Add(new() { StepNo = sn++, Name = "压板单侧集中力",
            Formula = "Pyb = 1.25×qyb×Syb×a/2",
            Process = $"Pyb = 1.25×{qyb}×{Syb}×{a}/2",
            Result = $"Pyb = {Pyb:F3} N" });

        // (2) 弯矩设计值
        double Myb = Pyb * 0.5 * Byb;

        steps.Add(new() { StepNo = sn++, Name = "弯矩设计值",
            Formula = "Myb = Pyb×(0.5×Byb)",
            Process = $"Myb = {Pyb:F3}×0.5×{Byb}",
            Result = $"Myb = {Myb:F2} N·mm" });

        // (3) 压板弯曲应力
        // σ = 6Myb / ((Lyb-d)×tyb²)
        double sigma = 6 * Myb / ((Lyb - d) * tyb * tyb);
        bool bendingOk = sigma <= req.AllowableBendingStress;

        steps.Add(new() { StepNo = sn++, Name = "压板弯曲应力校核",
            Formula = "σ = 6Myb/((Lyb-d)×tyb²) ≤ fa",
            Process = $"σ = 6×{Myb:F2}/({Lyb}-{d})×{tyb}²)",
            Result = $"σ = {sigma:F3} MPa {(bendingOk ? "≤" : ">")} {req.AllowableBendingStress} MPa → {(bendingOk ? "✅ 通过" : "❌ 不通过")}" });

        // (4) 压板剪应力
        // τ = 1.5Pyb / ((Lyb-d)×tyb)
        double tau = 1.5 * Pyb / ((Lyb - d) * tyb);
        bool shearOk = tau <= req.AllowableShearStress;

        steps.Add(new() { StepNo = sn++, Name = "压板剪应力校核",
            Formula = "τ = 1.5Pyb/((Lyb-d)×tyb) ≤ fv",
            Process = $"τ = 1.5×{Pyb:F3}/({Lyb}-{d})×{tyb})",
            Result = $"τ = {tau:F3} MPa {(shearOk ? "≤" : ">")} {req.AllowableShearStress} MPa → {(shearOk ? "✅ 通过" : "❌ 不通过")}" });

        // (5) 螺栓抗拉
        // Ntb = π×de²×ftb/4
        double de = req.BoltEffectiveDiameter;
        double ftb = req.BoltTensileStrength;
        double Ntb = Math.PI * de * de * ftb / 4;
        double load2Pyb = 2 * Pyb;
        bool boltOk = load2Pyb <= Ntb;

        steps.Add(new() { StepNo = sn++, Name = "螺栓抗拉强度验算",
            Formula = "Ntb = π×de²×ftb/4 ≥ 2Pyb",
            Process = $"Ntb = π×{de:F4}²×{ftb}/4 = {Ntb:F3}N, 2Pyb = {load2Pyb:F3}N",
            Result = $"{load2Pyb:F3}N {(boltOk ? "≤" : ">")} {Ntb:F3}N → {(boltOk ? "✅ 通过" : "❌ 不通过")}" });

        return Ok(new PressurePlateCalculationResult
        {
            Passed = bendingOk && shearOk && boltOk,
            BendingCheck = new CheckResult
            {
                Passed = bendingOk,
                ActualValue = Math.Round(sigma, 3),
                AllowableValue = req.AllowableBendingStress,
                Description = $"σ = {sigma:F3} MPa ≤ {req.AllowableBendingStress} MPa"
            },
            ShearCheck = new CheckResult
            {
                Passed = shearOk,
                ActualValue = Math.Round(tau, 3),
                AllowableValue = req.AllowableShearStress,
                Description = $"τ = {tau:F3} MPa ≤ {req.AllowableShearStress} MPa"
            },
            BoltCheck = new CheckResult
            {
                Passed = boltOk,
                ActualValue = Math.Round(load2Pyb, 3),
                AllowableValue = Math.Round(Ntb, 3),
                Description = $"2Pyb = {load2Pyb:F3}N ≤ Ntb = {Ntb:F3}N"
            },
            Standard = "JGJ 102-2003",
            Steps = steps
        });
    }
}
