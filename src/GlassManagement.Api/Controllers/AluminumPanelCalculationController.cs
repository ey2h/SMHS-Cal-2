using Microsoft.AspNetCore.Mvc;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

/// <summary>铝单板面板计算 (JGJ133-2001)</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("铝单板面板计算")]
public class AluminumPanelCalculationController : ControllerBase
{
    /// <summary>铝单板面板强度与挠度计算（含加劲肋）</summary>
    /// <remarks>
    /// 面板强度: σ=6mqa²η/t² ≤ f (JGJ133-2001 5.4.3)
    /// θ参数: θ=qka⁴/Et⁴, 查表得η折减系数
    /// 面板挠度: df=ημwka⁴/D, D=Et³/(12(1-υ²)), 限值a/60
    /// 加劲肋强度: M=qL²/24×(3-(Lx/L)²) (梯形荷载)
    /// 加劲肋挠度: df=qkL⁴/240EI×(25/8-5×(Lx/2/L)²+2×(Lx/2/L)⁴)
    /// </remarks>
    [HttpPost("Calculate")]
    [ProducesResponseType(typeof(AluminumPanelCalcResult), StatusCodes.Status200OK)]
    public ActionResult<AluminumPanelCalcResult> Calculate(
        [FromBody] AluminumPanelCalcRequest req)
    {
        var steps = new List<CalculationStep>();
        double a = Math.Min(req.ShortSide, req.LongSide);
        double b = Math.Max(req.ShortSide, req.LongSide);
        double t = req.Thickness;
        double E = req.ElasticModulus;
        double v = req.PoissonRatio;
        double q = req.DesignLoad;
        double wk = req.WindLoad * 0.001; // kN/m² → MPa
        double qk = req.StandardLoad ?? wk;

        // 材料强度
        double f = req.AllowableBendingStress ?? (req.MaterialGrade switch
        {
            "3003-H24" => 54,
            "3003-H14" => 56,
            "5005-H14" => 54,
            "1100-H24" => 74,
            _ => 54
        });

        steps.Add(new() { StepNo = 1, Name = "基本参数",
            Result = $"a={a}mm, b={b}mm, t={t}mm, E={E}MPa, υ={v}, f={f}MPa" });

        // ===== 1. θ参数计算 =====
        double theta = qk * Math.Pow(a, 4) / (E * Math.Pow(t, 4));
        steps.Add(new() { StepNo = 2, Name = "θ参数计算",
            Formula = "θ = qk×a⁴/(E×t⁴)",
            Process = $"θ = {qk}×{a}⁴/({E}×{t}⁴)",
            Result = $"θ = {theta:F3}" });

        // ===== 2. η折减系数（查表近似插值） =====
        double eta = InterpolateEta(theta);
        steps.Add(new() { StepNo = 3, Name = "η折减系数",
            Formula = "按θ查表5.4.3[JGJ133-2001]",
            Result = $"η = {eta:F4}" });

        // ===== 3. 弯矩系数m =====
        double ratio = a / b;
        double m = InterpolateMomentCoeff(ratio);
        steps.Add(new() { StepNo = 4, Name = "弯矩系数m",
            Formula = "按边长比a/b查表",
            Process = $"a/b = {ratio:F4}",
            Result = $"m = {m:F5}" });

        // ===== 4. 面板强度校核 =====
        double sigma = 6.0 * m * q * a * a * eta / (t * t);
        bool strengthOk = sigma <= f;
        steps.Add(new() { StepNo = 5, Name = "面板强度校核",
            Formula = "σ = 6mqa²η/t² ≤ f",
            Process = $"σ = 6×{m:F5}×{q}×{a}²×{eta:F4}/{t}²",
            Result = $"σ = {sigma:F3} MPa, f = {f} MPa → {(strengthOk ? "满足" : "不满足")}" });

        // ===== 5. 面板挠度校核 =====
        // 挠度η (用wk重算θ)
        double thetaDefl = wk * Math.Pow(a, 4) / (E * Math.Pow(t, 4));
        double etaDefl = InterpolateEta(thetaDefl);
        double mu = InterpolateDeflCoeff(ratio);
        double D = E * Math.Pow(t, 3) / (12.0 * (1.0 - v * v));
        double df = etaDefl * mu * wk * Math.Pow(a, 4) / D;
        double dfLim = a / 60.0;
        bool deflOk = df <= dfLim;
        steps.Add(new() { StepNo = 6, Name = "面板挠度校核",
            Formula = "df = ημwka⁴/D, D=Et³/(12(1-υ²)), 限值a/60",
            Process = $"D = {D:F3} N·mm, df = {etaDefl:F4}×{mu:F5}×{wk}×{a}⁴/{D:F3}",
            Result = $"df = {df:F3} mm ≤ {dfLim:F3} mm → {(deflOk ? "满足" : "不满足")}" });

        // ===== 6. 加劲肋计算（如有） =====
        CheckResult? stiffStrCheck = null;
        CheckResult? stiffDeflCheck = null;
        double Lx = req.StiffenerSpacing;
        double L = b; // 加劲肋跨度取长边

        if (req.StiffenerW > 0 && Lx > 0 && Lx < L)
        {
            // 梯形荷载弯矩: M = qL²/24 × (3-(Lx/L)²)
            double qStiff = q * Lx; // 作用在肋上的线荷载设计值 (N/mm)
            // 注意：这里q是面压(MPa=N/mm²), 乘以Lx(mm)得线荷载(N/mm)
            double M_stiff = qStiff * L * L / 24.0 * (3.0 - Math.Pow(Lx / L, 2));
            double sigmaStiff = M_stiff / (req.StiffenerGamma * req.StiffenerW);
            bool stiffStrOk = sigmaStiff <= req.StiffenerAllowableStress;

            stiffStrCheck = new CheckResult
            {
                Passed = stiffStrOk,
                ActualValue = Math.Round(sigmaStiff, 3),
                AllowableValue = req.StiffenerAllowableStress,
                Description = $"σ = M/(γW) = {sigmaStiff:F3} MPa ≤ {req.StiffenerAllowableStress} MPa"
            };

            steps.Add(new() { StepNo = 7, Name = "加劲肋强度校核",
                Formula = "M = qL²/24×(3-(Lx/L)²), σ=M/(γW)",
                Process = $"M = {M_stiff:F2} N·mm",
                Result = $"σ = {sigmaStiff:F3} MPa → {(stiffStrOk ? "满足" : "不满足")}" });

            // 梯形荷载挠度: df=qkL⁴/240EI×(25/8-5×(Lx/2/L)²+2×(Lx/2/L)⁴)
            double qkStiff = wk * Lx; // 标准值线荷载
            double halfRatio = Lx / 2.0 / L;
            double deflFactor = 25.0 / 8.0 - 5.0 * halfRatio * halfRatio + 2.0 * Math.Pow(halfRatio, 4);
            double dfStiff = qkStiff * Math.Pow(L, 4) / (240.0 * req.StiffenerE * req.StiffenerI) * deflFactor;
            double dfStiffLim = L / 180.0;
            bool stiffDeflOk = dfStiff <= dfStiffLim;

            stiffDeflCheck = new CheckResult
            {
                Passed = stiffDeflOk,
                ActualValue = Math.Round(dfStiff, 3),
                AllowableValue = Math.Round(dfStiffLim, 3),
                Description = $"df = {dfStiff:F3} mm ≤ L/180 = {dfStiffLim:F3} mm"
            };

            steps.Add(new() { StepNo = 8, Name = "加劲肋挠度校核",
                Formula = "df = qkL⁴/240EI×(25/8-5×(Lx/2/L)²+2×(Lx/2/L)⁴)",
                Result = $"df = {dfStiff:F3} mm ≤ {dfStiffLim:F3} mm → {(stiffDeflOk ? "满足" : "不满足")}" });
        }

        bool allPassed = strengthOk && deflOk
            && (stiffStrCheck?.Passed ?? true)
            && (stiffDeflCheck?.Passed ?? true);

        return Ok(new AluminumPanelCalcResult
        {
            Passed = allPassed,
            Theta = Math.Round(theta, 3),
            Eta = Math.Round(eta, 4),
            PanelStrengthCheck = new CheckResult
            {
                Passed = strengthOk,
                ActualValue = Math.Round(sigma, 3),
                AllowableValue = f,
                Description = $"σ = {sigma:F3} MPa ≤ f = {f} MPa"
            },
            PanelDeflectionCheck = new CheckResult
            {
                Passed = deflOk,
                ActualValue = Math.Round(df, 3),
                AllowableValue = Math.Round(dfLim, 3),
                Description = $"df = {df:F3} mm ≤ a/60 = {dfLim:F3} mm"
            },
            StiffenerStrengthCheck = stiffStrCheck,
            StiffenerDeflectionCheck = stiffDeflCheck,
            Steps = steps
        });
    }

    /// <summary>η折减系数近似插值 (JGJ133-2001 表5.4.3)</summary>
    private static double InterpolateEta(double theta)
    {
        // θ-η 查表数据点（近似）
        double[] thetas = { 0, 5, 10, 15, 20, 30, 40, 50, 60, 80, 100, 150, 200, 300 };
        double[] etas =   { 1.0, 0.996, 0.984, 0.966, 0.944, 0.896, 0.848, 0.805, 0.766,
                            0.700, 0.646, 0.546, 0.474, 0.375 };

        if (theta <= thetas[0]) return etas[0];
        if (theta >= thetas[^1]) return etas[^1];

        for (int i = 0; i < thetas.Length - 1; i++)
        {
            if (theta >= thetas[i] && theta <= thetas[i + 1])
            {
                double t1 = (theta - thetas[i]) / (thetas[i + 1] - thetas[i]);
                return etas[i] + t1 * (etas[i + 1] - etas[i]);
            }
        }
        return 1.0;
    }

    /// <summary>弯矩系数m近似插值</summary>
    private static double InterpolateMomentCoeff(double ratio)
    {
        // a/b → m 查表 (四边简支)
        double[] ratios = { 0.0, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0 };
        double[] ms =     { 0.125, 0.125, 0.124, 0.1214, 0.1174, 0.1150, 0.1140, 0.1134, 0.1130, 0.1128 };

        if (ratio <= ratios[0]) return ms[0];
        if (ratio >= ratios[^1]) return ms[^1];

        for (int i = 0; i < ratios.Length - 1; i++)
        {
            if (ratio >= ratios[i] && ratio <= ratios[i + 1])
            {
                double t1 = (ratio - ratios[i]) / (ratios[i + 1] - ratios[i]);
                return ms[i] + t1 * (ms[i + 1] - ms[i]);
            }
        }
        return 0.125;
    }

    /// <summary>挠度系数μ近似插值</summary>
    private static double InterpolateDeflCoeff(double ratio)
    {
        double[] ratios = { 0.0, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0 };
        double[] mus =    { 0.01302, 0.01282, 0.01202, 0.01062, 0.00922, 0.00751, 0.00602, 0.00459, 0.00388, 0.00340 };

        if (ratio <= ratios[0]) return mus[0];
        if (ratio >= ratios[^1]) return mus[^1];

        for (int i = 0; i < ratios.Length - 1; i++)
        {
            if (ratio >= ratios[i] && ratio <= ratios[i + 1])
            {
                double t1 = (ratio - ratios[i]) / (ratios[i + 1] - ratios[i]);
                return mus[i] + t1 * (mus[i + 1] - mus[i]);
            }
        }
        return 0.01302;
    }
}
