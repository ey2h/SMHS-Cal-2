using Microsoft.AspNetCore.Mvc;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

/// <summary>风荷载计算 - 按GB50009-2012, GB55001-2021</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("风荷载计算")]
public class WindLoadCalculationController : ControllerBase
{
    /// <summary>风荷载标准值计算</summary>
    /// <remarks>
    /// wk = β×K1×K2×μz×μs1×w0 (GB55001-2021 4.6.1)
    /// </remarks>
    [HttpPost("Calculate")]
    [ProducesResponseType(typeof(WindLoadCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<WindLoadCalculationResult> Calculate(
        [FromBody] WindLoadCalculationRequest req)
    {
        var steps = new List<CalculationStep>();
        int sn = 1;

        double z = req.Height;
        string cat = req.TerrainCategory.ToUpper();
        double w0 = req.BasicWindPressure;

        // (1) 风压高度变化系数 μz
        // 截断高度和梯度高度
        var (zMin, zMax, coeff, exp) = cat switch
        {
            "A" => (5.0, 300.0, 1.284, 0.24),
            "B" => (10.0, 350.0, 1.000, 0.30),
            "C" => (15.0, 450.0, 0.544, 0.44),
            "D" => (30.0, 550.0, 0.262, 0.60),
            _ => (15.0, 450.0, 0.544, 0.44)
        };

        double zCalc = Math.Max(Math.Min(z, zMax), zMin);
        double muZ = coeff * Math.Pow(zCalc / 10.0, exp);

        steps.Add(new() { StepNo = sn++, Name = "风压高度变化系数μz",
            Formula = $"μz = {coeff}×(z/10)^{exp} ({cat}类场地)",
            Process = $"z = {z}m (截断: [{zMin},{zMax}]), z_calc = {zCalc}m",
            Result = $"μz = {muZ:F4}" });

        // (2) 阵风系数 βgz = 1 + 2g×I10×(z/10)^(-α)
        double g = 2.5; // 峰值因子
        var (I10, alpha) = cat switch
        {
            "A" => (0.12, 0.12),
            "B" => (0.14, 0.15),
            "C" => (0.23, 0.22),
            "D" => (0.39, 0.30),
            _ => (0.23, 0.22)
        };

        double betaGz = 1 + 2 * g * I10 * Math.Pow(zCalc / 10.0, -alpha);

        // β = max(βgz, 1+0.7/μz^0.5)
        double betaAlt = 1 + 0.7 / Math.Sqrt(muZ);
        double beta = Math.Max(betaGz, betaAlt);

        steps.Add(new() { StepNo = sn++, Name = "阵风系数β",
            Formula = "βgz = 1+2g×I10×(z/10)^(-α), β = max(βgz, 1+0.7/μz^0.5)",
            Process = $"g={g}, I10={I10}, α={alpha}, βgz={betaGz:F4}, 1+0.7/√μz={betaAlt:F4}",
            Result = $"β = {beta:F4}" });

        // (3) 局部体型系数折减 (面板和支承结构不同)
        double mus1_1 = req.ShapeCoefficient; // μs1(1)

        // 支承结构: 按从属面积折减
        double A_struct = req.StructureArea;
        double mus1_struct;
        if (A_struct <= 1.0)
        {
            mus1_struct = mus1_1;
        }
        else if (A_struct >= 25.0)
        {
            mus1_struct = 0.8 * mus1_1; // 墙面折减0.8
        }
        else
        {
            // 对数插值 μs1(A) = μs1(1) + [μs1(25)-μs1(1)]×logA/1.4
            double mus1_25 = 0.8 * mus1_1;
            double logA = Math.Log10(A_struct);
            mus1_struct = mus1_1 + (mus1_25 - mus1_1) * logA / 1.4;
        }

        // 加内压系数
        double internalCoeff = req.InternalPressureCoeff;
        double mus1_structure_final = mus1_struct + internalCoeff;
        double mus1_panel_final = mus1_1 + internalCoeff;

        steps.Add(new() { StepNo = sn++, Name = "局部风压体型系数",
            Formula = "μs1(A) = μs1(1)+[μs1(25)-μs1(1)]×logA/1.4 + 内压系数",
            Process = $"从属面积A={A_struct:F2}m², μs1(1)={mus1_1}, 内压={internalCoeff}",
            Result = $"支承结构 μs1={mus1_structure_final:F3}, 面板 μs1={mus1_panel_final:F3}" });

        // (4) 风荷载标准值 wk = β×K1×K2×μz×μs1×w0
        double wk_struct = beta * req.K1 * req.K2 * muZ * mus1_structure_final * w0;
        double wk_panel = beta * req.K1 * req.K2 * muZ * mus1_panel_final * w0;

        steps.Add(new() { StepNo = sn++, Name = "支承结构风荷载标准值",
            Formula = "wk = β×K1×K2×μz×μs1×w0  ……4.6.1[GB55001-2021]",
            Process = $"wk = {beta:F4}×{req.K1}×{req.K2}×{muZ:F4}×{mus1_structure_final:F3}×{w0}",
            Result = $"wk = {wk_struct:F6} MPa" });

        steps.Add(new() { StepNo = sn++, Name = "面板风荷载标准值",
            Formula = "wk = β×K1×K2×μz×μs1×w0",
            Process = $"wk = {beta:F4}×{req.K1}×{req.K2}×{muZ:F4}×{mus1_panel_final:F3}×{w0}",
            Result = $"wk = {wk_panel:F6} MPa" });

        return Ok(new WindLoadCalculationResult
        {
            Passed = true,
            MuZ = Math.Round(muZ, 4),
            BetaGz = Math.Round(betaGz, 4),
            Beta = Math.Round(beta, 4),
            StructureShapeCoeff = Math.Round(mus1_structure_final, 3),
            PanelShapeCoeff = Math.Round(mus1_panel_final, 3),
            StructureWindLoad = Math.Round(wk_struct, 6),
            PanelWindLoad = Math.Round(wk_panel, 6),
            Standard = "GB50009-2012, GB55001-2021",
            Steps = steps
        });
    }
}
