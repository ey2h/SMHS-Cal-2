using Microsoft.AspNetCore.Mvc;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

/// <summary>UHPC板面板计算 (JGJ336-2016, 通长挂件连接通槽式)</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("UHPC板面板计算")]
public class UhpcPanelCalculationController : ControllerBase
{
    /// <summary>UHPC板面板强度、挠度及通槽式连接计算</summary>
    /// <remarks>
    /// 面板强度: σ1=6mqb0²/te² ≤ f (简支板, m=0.125)
    /// 面板挠度: df=μwkb⁴/D ≤ L/250
    /// 槽口抗弯: σ2=2qLhβ/tv² ≤ f
    /// 槽口抗剪: τ=2qL/tv ≤ fv
    /// 挂件抗剪: τp=qL/2tp ≤ fv,p
    /// </remarks>
    [HttpPost("Calculate")]
    [ProducesResponseType(typeof(UhpcPanelCalcResult), StatusCodes.Status200OK)]
    public ActionResult<UhpcPanelCalcResult> Calculate(
        [FromBody] UhpcPanelCalcRequest req)
    {
        var steps = new List<CalculationStep>();
        double a = req.ShortSide;
        double b = req.LongSide;
        double L = req.Span;
        double te = req.EffectiveThickness;
        double q = req.DesignLoad;
        double wk = req.WindLoad * 0.001; // kN/m² → MPa
        double f = req.AllowableBendingStress;
        double fv = req.AllowableShearStress;
        double E = req.ElasticModulus;
        double v = req.PoissonRatio;

        steps.Add(new() { StepNo = 1, Name = "基本参数",
            Result = $"板块: {a}×{b}mm, L={L}mm, te={te}mm, f={f}MPa, fv={fv}MPa" });

        // ===== 1. 面板抗弯强度 (简支板, m=0.125) =====
        double m = 0.125;
        double sigma1 = 6.0 * m * q * L * L / (te * te);
        bool strengthOk = sigma1 <= f;

        steps.Add(new() { StepNo = 2, Name = "面板抗弯强度",
            Formula = "σ1 = 6mqb0²/te² ≤ f",
            Process = $"σ1 = 6×{m}×{q}×{L}²/{te}²",
            Result = $"σ1 = {sigma1:F3} MPa ≤ f = {f} MPa → {(strengthOk ? "满足" : "不满足")}" });

        // ===== 2. 面板挠度 =====
        double mu = 0.013; // 简支板μ系数
        double D = E * Math.Pow(te, 3) / (12.0 * (1.0 - v * v));
        double df = mu * wk * Math.Pow(L, 4) / D;
        double dfLim = L / 250.0;
        bool deflOk = df <= dfLim;

        steps.Add(new() { StepNo = 3, Name = "面板挠度",
            Formula = "df = μwkb⁴/D, D=Ete³/(12(1-υ²)), 限值L/250",
            Process = $"D = {D:F3} N·mm",
            Result = $"df = {df:F3} mm ≤ {dfLim:F3} mm → {(deflOk ? "满足" : "不满足")}" });

        // ===== 3. 槽口抗弯 =====
        double h_slot = req.SlotDepth;
        double tv = req.SlotThickness;
        double beta = req.StressConcentrationFactor;
        double sigma2 = 2.0 * q * L * h_slot * beta / (tv * tv);
        bool slotBendOk = sigma2 <= f;

        var slotBendCheck = new CheckResult
        {
            Passed = slotBendOk,
            ActualValue = Math.Round(sigma2, 3),
            AllowableValue = f,
            Description = $"σ2 = 2qLhβ/tv² = {sigma2:F3} MPa ≤ f = {f} MPa"
        };
        steps.Add(new() { StepNo = 4, Name = "槽口抗弯",
            Formula = "σ2 = 2qLhβ/tv²",
            Process = $"σ2 = 2×{q}×{L}×{h_slot}×{beta}/{tv}²",
            Result = $"σ2 = {sigma2:F3} MPa → {(slotBendOk ? "满足" : "不满足")}" });

        // ===== 4. 槽口抗剪 =====
        double tau_slot = 2.0 * q * L / tv;
        bool slotShearOk = tau_slot <= fv;

        var slotShearCheck = new CheckResult
        {
            Passed = slotShearOk,
            ActualValue = Math.Round(tau_slot, 3),
            AllowableValue = fv,
            Description = $"τ = 2qL/tv = {tau_slot:F3} MPa ≤ fv = {fv} MPa"
        };
        steps.Add(new() { StepNo = 5, Name = "槽口抗剪",
            Formula = "τ = 2qL/tv",
            Result = $"τ = {tau_slot:F3} MPa → {(slotShearOk ? "满足" : "不满足")}" });

        // ===== 5. 挂件抗剪 =====
        double tp = req.HangerThickness;
        double fvp = req.HangerShearStrength;
        double tau_p = q * L / (2.0 * tp);
        bool hangerOk = tau_p <= fvp;

        var hangerCheck = new CheckResult
        {
            Passed = hangerOk,
            ActualValue = Math.Round(tau_p, 3),
            AllowableValue = fvp,
            Description = $"τp = qL/(2tp) = {tau_p:F3} MPa ≤ fv,p = {fvp} MPa"
        };
        steps.Add(new() { StepNo = 6, Name = "挂件抗剪",
            Formula = "τp = qL/(2tp)",
            Result = $"τp = {tau_p:F3} MPa → {(hangerOk ? "满足" : "不满足")}" });

        bool allPassed = strengthOk && deflOk && slotBendOk && slotShearOk && hangerOk;

        return Ok(new UhpcPanelCalcResult
        {
            Passed = allPassed,
            PanelStrengthCheck = new CheckResult
            {
                Passed = strengthOk,
                ActualValue = Math.Round(sigma1, 3),
                AllowableValue = f,
                Description = $"σ1 = {sigma1:F3} MPa ≤ f = {f} MPa"
            },
            PanelDeflectionCheck = new CheckResult
            {
                Passed = deflOk,
                ActualValue = Math.Round(df, 3),
                AllowableValue = Math.Round(dfLim, 3),
                Description = $"df = {df:F3} mm ≤ L/250 = {dfLim:F3} mm"
            },
            SlotBendingCheck = slotBendCheck,
            SlotShearCheck = slotShearCheck,
            HangerShearCheck = hangerCheck,
            Steps = steps
        });
    }
}
