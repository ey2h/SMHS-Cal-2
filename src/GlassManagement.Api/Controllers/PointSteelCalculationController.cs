using Microsoft.AspNetCore.Mvc;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

/// <summary>点式钢管结构计算</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("点式钢管结构计算")]
public class PointSteelCalculationController : ControllerBase
{
    /// <summary>点式钢管龙骨强度与挠度计算（多点集中力简支梁）</summary>
    /// <remarks>
    /// 力学模型：多点集中力作用简支梁
    /// 强度: N/A + M/(γW) ≤ f
    /// 挠度: 有限元叠加法, df ≤ L/250
    /// 单集中力弯矩: AC段 Mx=Pbx/L, CB段 Mx=Pa(1-ξ)
    /// 单集中力挠度: AC段 fx=PbL²(ωDξ-β²ξ)/6EI
    /// </remarks>
    [HttpPost("Calculate")]
    [ProducesResponseType(typeof(PointSteelCalcResult), StatusCodes.Status200OK)]
    public ActionResult<PointSteelCalcResult> Calculate(
        [FromBody] PointSteelCalcRequest req)
    {
        var steps = new List<CalculationStep>();
        double L = req.Span;
        double B = req.GridWidth;
        double wk = req.WindLoad * 0.001; // kN/m² → MPa
        double A_section = req.SectionArea;
        double I = req.MomentOfInertia;
        double W = req.SectionModulus;
        double gamma = req.Gamma;
        double E_mod = req.ElasticModulus;
        double Gk_area = req.GravityLoad;

        // 材料强度
        double f = req.SteelGrade switch
        {
            "Q235" => 215.0,
            "Q355" => 315.0,
            _ => 215.0
        };

        // 地震作用
        double qEAk = req.BetaE * req.AlphaMax * Gk_area;

        // 荷载组合
        double S_design = 1.5 * wk + 0.5 * 1.4 * qEAk; // 强度用设计值
        double S_standard = wk; // 挠度用标准值

        steps.Add(new() { StepNo = 1, Name = "荷载组合",
            Process = $"qEAk = {req.BetaE}×{req.AlphaMax}×{Gk_area} = {qEAk} MPa",
            Result = $"设计值S = {S_design} MPa, 标准值Sk = {S_standard} MPa" });

        // 计算各集中力
        var segments = req.SegmentLengths;
        var positions = req.LoadPositions;
        int nForces = positions.Count;
        var Pi = new List<double>();   // 设计值
        var Pki = new List<double>();  // 标准值

        for (int i = 0; i < nForces; i++)
        {
            // 每个集中力 = S × B × (左右分格长度之和)/2
            double leftSeg = (i < segments.Count) ? segments[i] : 0;
            double rightSeg = (i + 1 < segments.Count) ? segments[i + 1] : 0;
            double tributary = (leftSeg + rightSeg) / 2.0;
            double pk = S_standard * B * tributary;
            double p = S_design * B * tributary;
            Pki.Add(pk);
            Pi.Add(p);
        }

        steps.Add(new() { StepNo = 2, Name = "集中力计算",
            Result = $"共{nForces}个集中力, Pi(设计) = [{string.Join(", ", Pi.Select(x => $"{x:F2}"))}] N" });

        // ===== 强度计算：叠加各集中力在各作用点产生的弯矩 =====
        double maxM = 0;
        double maxM_x = 0;

        // 在每个集中力作用点处计算弯矩叠加
        int calcPoints = Math.Max(100, (int)(L / 10));
        for (int j = 0; j <= calcPoints; j++)
        {
            double x = L * j / calcPoints;
            double M_total = 0;
            for (int i = 0; i < nForces; i++)
            {
                double ai = positions[i]; // 力到A支座的距离
                double bi = L - ai;       // 力到B支座的距离
                double Mx;
                if (x <= ai)
                {
                    // x在AC段: Mx = P×b×x/L
                    Mx = Pi[i] * bi * x / L;
                }
                else
                {
                    // x在CB段: Mx = P×a×(L-x)/L
                    Mx = Pi[i] * ai * (L - x) / L;
                }
                M_total += Mx;
            }
            if (M_total > maxM)
            {
                maxM = M_total;
                maxM_x = x;
            }
        }

        // 轴力 (自重)
        double N_axial = 1.3 * Gk_area * B * L;
        double sigmaCalc = N_axial / A_section + maxM / (gamma * W);
        bool strengthOk = sigmaCalc <= f;

        steps.Add(new() { StepNo = 3, Name = "强度校核",
            Formula = "N/A + M/(γW) ≤ f",
            Process = $"N = 1.3×Gk×B×L = {N_axial:F2} N, M_max = {maxM:F2} N·mm (x={maxM_x:F0}mm)",
            Result = $"σ = {sigmaCalc:F3} MPa ≤ f = {f} MPa → {(strengthOk ? "满足" : "不满足")}" });

        // ===== 挠度计算：叠加各集中力在各点产生的挠度 =====
        double maxDefl = 0;
        double maxDefl_x = 0;

        for (int j = 0; j <= calcPoints; j++)
        {
            double x = L * j / calcPoints;
            double defl_total = 0;
            for (int i = 0; i < nForces; i++)
            {
                double ai = positions[i];
                double bi = L - ai;
                double alpha = ai / L;
                double beta = bi / L;

                double fx;
                if (x <= ai)
                {
                    // AC段: fx = PbL²(ωDξ-β²ξ)/6EI
                    double xi = x / L;
                    double omegaDxi = xi - xi * xi * xi;
                    fx = Pki[i] * bi * L * L * (omegaDxi - beta * beta * xi) / (6.0 * E_mod * I);
                }
                else
                {
                    // CB段: fx = PaL²(ωDζ-α²ζ)/6EI
                    double zeta = (L - x) / L;
                    double omegaDzeta = zeta - zeta * zeta * zeta;
                    fx = Pki[i] * ai * L * L * (omegaDzeta - alpha * alpha * zeta) / (6.0 * E_mod * I);
                }
                defl_total += fx;
            }
            if (defl_total > maxDefl)
            {
                maxDefl = defl_total;
                maxDefl_x = x;
            }
        }

        double deflLim = L / 250.0;
        bool deflOk = maxDefl <= deflLim;

        steps.Add(new() { StepNo = 4, Name = "挠度校核",
            Formula = "叠加法计算最大挠度, df ≤ L/250",
            Process = $"df_max = {maxDefl:F3} mm (x={maxDefl_x:F0}mm)",
            Result = $"df = {maxDefl:F3} mm ≤ L/250 = {deflLim:F3} mm → {(deflOk ? "满足" : "不满足")}" });

        return Ok(new PointSteelCalcResult
        {
            Passed = strengthOk && deflOk,
            MaxBendingMoment = Math.Round(maxM, 2),
            MaxMomentPosition = Math.Round(maxM_x, 0),
            MaxDeflection = Math.Round(maxDefl, 3),
            MaxDeflectionPosition = Math.Round(maxDefl_x, 0),
            DesignForces = Pi.Select(x => Math.Round(x, 2)).ToList(),
            StandardForces = Pki.Select(x => Math.Round(x, 2)).ToList(),
            StrengthCheck = new CheckResult
            {
                Passed = strengthOk,
                ActualValue = Math.Round(sigmaCalc, 3),
                AllowableValue = f,
                Description = $"σ = N/A + M/(γW) = {sigmaCalc:F3} MPa ≤ f = {f} MPa"
            },
            DeflectionCheck = new CheckResult
            {
                Passed = deflOk,
                ActualValue = Math.Round(maxDefl, 3),
                AllowableValue = Math.Round(deflLim, 3),
                Description = $"df = {maxDefl:F3} mm ≤ L/250 = {deflLim:F3} mm"
            },
            Steps = steps
        });
    }
}
