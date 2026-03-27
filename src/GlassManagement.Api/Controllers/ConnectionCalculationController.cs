using Microsoft.AspNetCore.Mvc;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

/// <summary>连接件计算 - 按JGJ102-2003第7章, GB50429-2007</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("连接件计算")]
public class ConnectionCalculationController : ControllerBase
{
    /// <summary>连接件计算 (横梁-角码-立柱-主结构)</summary>
    [HttpPost("Calculate")]
    [ProducesResponseType(typeof(ConnectionCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<ConnectionCalculationResult> Calculate(
        [FromBody] ConnectionCalculationRequest req)
    {
        var steps = new List<CalculationStep>();
        int sn = 1;

        double B = req.TransomWidth;
        double H = req.TransomHeight;
        double L = req.MullionSpan;
        double L1 = req.ShortSpan;
        double L2 = req.LongSpan;
        double B1 = req.MullionSpacing;
        double wk = req.WindLoad * 0.001; // kN/m² → MPa

        // ===== 1. 横梁与角码连接 =====
        steps.Add(new() { StepNo = sn++, Name = "=== 横梁与角码连接 ===" });

        // 风荷载剪力 (三角分布) Vw = 1.5×wk×B²/4
        double Vw = 1.5 * wk * B * B / 4;
        // 地震剪力
        double qEAk_panel = req.BetaE * req.AlphaMax * req.PanelGravityLoad;
        double VEk = qEAk_panel * B * B / 4;
        double VE = 1.4 * VEk;
        // 总剪力 N1 = Vw + 0.5VE
        double N1 = Vw + 0.5 * VE;

        steps.Add(new() { StepNo = sn++, Name = "横梁连接处总剪力",
            Formula = "N1 = 1.5wk×B²/4 + 0.5×1.4×VEk (三角分布)",
            Process = $"Vw={Vw:F1}N, VEk={VEk:F1}N, VE={VE:F1}N",
            Result = $"N1 = {N1:F1} N" });

        // 螺栓受剪 Nv1b = nv×π×de²×fvb/4
        double de1 = GetEffectiveDiameter(req.TransomBoltDiameter);
        double Nv1b = 1 * Math.PI * de1 * de1 * req.BoltShearStrength / 4;
        int boltCount1 = Math.Max((int)Math.Ceiling(N1 / Nv1b), 2);

        steps.Add(new() { StepNo = sn++, Name = "横梁连接螺栓",
            Formula = "Nv1b = nv×π×de²×fvb/4",
            Process = $"de={de1:F3}mm, fvb={req.BoltShearStrength}MPa",
            Result = $"Nv1b = {Nv1b:F1}N, 需{N1 / Nv1b:F3}个, 取{boltCount1}个" });

        // 横梁壁承压 Nc1 = n×d×t1×fc
        double Nc1 = boltCount1 * req.TransomBoltDiameter * req.TransomWallThickness * req.AluminumBearingStrength;
        bool transom_ok = Nc1 >= N1;

        steps.Add(new() { StepNo = sn++, Name = "横梁壁承压",
            Formula = "Nc1 = n×d×t1×fc",
            Result = $"Nc1 = {Nc1:F1}N {(transom_ok ? "≥" : "<")} {N1:F1}N → {(transom_ok ? "✅" : "❌")}" });

        // ===== 2. 角码与立柱连接 =====
        steps.Add(new() { StepNo = sn++, Name = "=== 角码与立柱连接 ===" });

        // 自重
        double N2k = req.PanelGravityLoad * B * H / 2;
        double N2 = 1.3 * N2k;

        // 组合 N = √(N1² + N2²)
        double N_angle = Math.Sqrt(N1 * N1 + N2 * N2);

        steps.Add(new() { StepNo = sn++, Name = "角码连接组合荷载",
            Formula = "N = √(N1²+N2²), N2 = 1.3×qG×B×H/2",
            Process = $"N1={N1:F1}N, N2={N2:F1}N",
            Result = $"N = {N_angle:F1} N" });

        double de2 = GetEffectiveDiameter(req.AngleBoltDiameter);
        double Nv2b = 1 * Math.PI * de2 * de2 * req.BoltShearStrength / 4;
        int boltCount2 = Math.Max((int)Math.Ceiling(N_angle / Nv2b), 2);

        // 立柱壁承压
        double Nc2 = boltCount2 * req.AngleBoltDiameter * req.MullionWallThickness * req.AluminumBearingStrength;
        // 角码壁承压
        double Nc3 = boltCount2 * req.AngleBoltDiameter * req.AngleThickness * req.AluminumBearingStrength;
        bool angle_ok = Nc2 >= N_angle && Nc3 >= N_angle;

        steps.Add(new() { StepNo = sn++, Name = "角码连接螺栓和承压",
            Formula = "Nc = n×d×t×fc",
            Result = $"螺栓{boltCount2}个, 立柱壁Nc2={Nc2:F0}N, 角码Nc3={Nc3:F0}N ≥ {N_angle:F1}N → {(angle_ok ? "✅" : "❌")}" });

        // ===== 3. 立柱与主结构连接 =====
        steps.Add(new() { StepNo = sn++, Name = "=== 立柱与主结构连接 ===" });

        // 双跨梁中支座反力 R1
        double qw_m = 1.5 * wk * B1;
        double qEAk_m = req.BetaE * req.AlphaMax * req.GravityLoad;
        double qE_m = 1.4 * qEAk_m * B1;
        double q_m = qw_m + 0.5 * qE_m;

        // R1 = q×L×(L1²+3L1L2+L2²)/(8L1L2)
        double R1 = q_m * L * (L1 * L1 + 3 * L1 * L2 + L2 * L2) / (8 * L1 * L2);

        steps.Add(new() { StepNo = sn++, Name = "双跨梁中支座反力R1",
            Formula = "R1 = q×L×(L1²+3L1L2+L2²)/(8L1L2)",
            Process = $"q={q_m:F3}N/mm, L={L}mm, L1={L1}mm, L2={L2}mm",
            Result = $"R1 = {R1:F1} N" });

        // 自重
        double NGk = req.GravityLoad * B1 * L;
        double NG = 1.3 * NGk;

        // 总剪力
        double N_total = Math.Sqrt(R1 * R1 + NG * NG);

        steps.Add(new() { StepNo = sn++, Name = "连接处总剪力",
            Formula = "N = √(R1²+NG²)",
            Process = $"R1={R1:F1}N, NG={NG:F1}N",
            Result = $"N = {N_total:F1} N" });

        // 螺栓 (双剪)
        double de3 = GetEffectiveDiameter(req.MullionBoltDiameter);
        int nv3 = 2; // 双剪
        double Nv3b = nv3 * Math.PI * de3 * de3 * req.BoltShearStrength / 4;
        int boltCount3 = Math.Max((int)Math.Ceiling(N_total / Nv3b), 2);

        // 立柱壁承压 (双剪)
        double Nc4 = nv3 * boltCount3 * req.MullionBoltDiameter * req.MullionWallThickness * req.AluminumBearingStrength;
        // 钢角码承压
        double Nc5 = nv3 * boltCount3 * req.MullionBoltDiameter * req.SteelAngleThickness * req.SteelBearingStrength;

        bool mullion_ok = Nc4 >= N_total && Nc5 >= N_total;

        steps.Add(new() { StepNo = sn++, Name = "主结构连接螺栓和承压",
            Formula = "双剪: Nc = nv×n×d×t×fc",
            Result = $"螺栓{boltCount3}个(双剪), Nc4={Nc4:F0}N, Nc5={Nc5:F0}N ≥ {N_total:F1}N → {(mullion_ok ? "✅" : "❌")}" });

        bool allPassed = transom_ok && angle_ok && mullion_ok;

        return Ok(new ConnectionCalculationResult
        {
            Passed = allPassed,
            TransomAngleCheck = new CheckResult
            {
                Passed = transom_ok, ActualValue = Math.Round(N1, 1), AllowableValue = Math.Round(Nc1, 1),
                Description = $"N1={N1:F1}N ≤ Nc1={Nc1:F0}N, {boltCount1}个M{req.TransomBoltDiameter}螺栓"
            },
            AngleMullionCheck = new CheckResult
            {
                Passed = angle_ok, ActualValue = Math.Round(N_angle, 1), AllowableValue = Math.Round(Math.Min(Nc2, Nc3), 1),
                Description = $"N={N_angle:F1}N ≤ min(Nc2={Nc2:F0}, Nc3={Nc3:F0})N"
            },
            MullionStructureCheck = new CheckResult
            {
                Passed = mullion_ok, ActualValue = Math.Round(N_total, 1), AllowableValue = Math.Round(Math.Min(Nc4, Nc5), 1),
                Description = $"N={N_total:F1}N ≤ min(Nc4={Nc4:F0}, Nc5={Nc5:F0})N"
            },
            Standard = "JGJ 102-2003, GB50429-2007",
            Steps = steps
        });
    }

    /// <summary>螺栓螺纹有效直径 (简化)</summary>
    private static double GetEffectiveDiameter(double nominalDiameter) => nominalDiameter switch
    {
        5 => 4.249467,
        6 => 5.061833,
        8 => 6.827,
        10 => 8.593,
        12 => 10.358,
        16 => 13.835,
        20 => 17.294,
        _ => nominalDiameter * 0.84
    };
}
