#pragma warning disable CS0618 // 旧版API保持向后兼容，类型标记为Obsolete但仍需使用
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

/// <summary>玻璃计算 - 按JGJ102-2003及JGJ113-2015</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("玻璃计算")]
public class GlassCalculationController : ControllerBase
{
    // 玻璃弹性模量 E = 72000 MPa
    private const double E_GLASS = 72000.0;
    // 玻璃泊松比
    private const double NU_GLASS = 0.2;

    /// <summary>单片玻璃计算</summary>
    [HttpPost("SingleGlass")]
    [ProducesResponseType(typeof(GlassCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<GlassCalculationResult> CalculateSingleGlass(
        [FromBody] SingleGlassCalculationRequest req)
    {
        var steps = new List<CalculationStep>();
        int stepNo = 1;

        // 1. 确定玻璃强度设计值 fg (JGJ113-2015 表4.1.9)
        double fg = GetGlassDesignStrength(req.GlassType, req.Thickness);
        steps.Add(new() { StepNo = stepNo++, Name = "确定玻璃强度设计值",
            Formula = "按JGJ113-2015表4.1.9查取",
            Process = $"玻璃类型: {req.GlassType}, 厚度: {req.Thickness}mm",
            Result = $"fg = {fg} MPa" });

        // 2. 边长
        double a = Math.Min(req.ShortSide, req.LongSide);
        double b = Math.Max(req.ShortSide, req.LongSide);
        double ratio = a / b; // 注意: JGJ102用a/b (短/长)

        // 3. 查弯矩系数m和挠度系数μ (JGJ102-2003 表6.1.2-1, 表6.1.3)
        double m = GetBendingCoefficient(ratio, req.SupportCondition);
        double mu = GetDeflectionCoefficient(ratio, req.SupportCondition);

        steps.Add(new() { StepNo = stepNo++, Name = "查弯矩系数和挠度系数",
            Formula = "按边长比a/b查表6.1.2-1和表6.1.3 [JGJ102-2003]",
            Process = $"a = {a}mm, b = {b}mm, a/b = {ratio:F4}",
            Result = $"m = {m:F4}, μ = {mu:F6}" });

        // 4. 设计厚度
        double t = GetMinThickness(req.Thickness);
        steps.Add(new() { StepNo = stepNo++, Name = "确定计算厚度",
            Formula = "标称厚度→最小厚度",
            Process = $"标称: {req.Thickness}mm",
            Result = $"t = {t}mm" });

        // 5. 荷载 (用户输入kN/m², 转换为MPa: 1 kN/m² = 0.001 MPa = 0.001 N/mm²)
        double wk = req.WindLoad * 0.001; // 转为 MPa (N/mm²)
        double q = 1.5 * wk; // 设计值 (仅风荷载)

        // 6. 计算参数θ
        double theta = wk * Math.Pow(a, 4) / (E_GLASS * Math.Pow(t, 4));
        steps.Add(new() { StepNo = stepNo++, Name = "计算参数θ",
            Formula = "θ = wk×a⁴/(E×t⁴)",
            Process = $"θ = {wk}×{a}⁴/({E_GLASS}×{t}⁴)",
            Result = $"θ = {theta:F4}" });

        // 7. 折减系数η (查表6.1.2-2)
        double eta = GetReductionFactor(theta);
        steps.Add(new() { StepNo = stepNo++, Name = "查折减系数η",
            Formula = "按参数θ查表6.1.2-2 [JGJ102-2003]",
            Process = $"θ = {theta:F4}",
            Result = $"η = {eta}" });

        // 8. 强度计算 σ = 6mqa²η/t²
        double sigma = 6 * m * q * a * a * eta / (t * t);
        steps.Add(new() { StepNo = stepNo++, Name = "最大弯曲应力",
            Formula = "σ = 6m×q×a²×η/t²",
            Process = $"σ = 6×{m:F4}×{q}×{a}²×{eta}/{t}²",
            Result = $"σ = {sigma:F3} MPa" });

        // 9. 挠度计算 df = η×μ×wk×a⁴/D
        double D = E_GLASS * Math.Pow(t, 3) / (12 * (1 - NU_GLASS * NU_GLASS));
        double df = eta * mu * wk * Math.Pow(a, 4) / D;
        // 挠度限值: 玻璃板取a/60
        double dfLimit = a / 60.0;

        steps.Add(new() { StepNo = stepNo++, Name = "弯曲刚度D",
            Formula = "D = E×t³/(12×(1-ν²))",
            Process = $"D = {E_GLASS}×{t}³/(12×(1-{NU_GLASS}²))",
            Result = $"D = {D:F0} N·mm" });

        steps.Add(new() { StepNo = stepNo++, Name = "最大挠度",
            Formula = "df = η×μ×wk×a⁴/D",
            Process = $"df = {eta}×{mu:F6}×{wk}×{a}⁴/{D:F0}",
            Result = $"df = {df:F3} mm" });

        // 10. 验算
        bool strengthOk = sigma <= fg;
        bool deflectionOk = df <= dfLimit;

        steps.Add(new() { StepNo = stepNo++, Name = "强度验算",
            Formula = "σ ≤ fg",
            Process = $"{sigma:F3} MPa vs {fg} MPa",
            Result = strengthOk ? "✅ 通过" : "❌ 不通过" });
        steps.Add(new() { StepNo = stepNo++, Name = "挠度验算",
            Formula = "df ≤ a/60",
            Process = $"{df:F3} mm vs {dfLimit:F3} mm",
            Result = deflectionOk ? "✅ 通过" : "❌ 不通过" });

        return Ok(new GlassCalculationResult
        {
            Passed = strengthOk && deflectionOk,
            MaxStress = Math.Round(sigma, 3),
            MaxDeflection = Math.Round(df, 3),
            DeflectionLimit = Math.Round(dfLimit, 3),
            Standard = "JGJ 102-2003, JGJ 113-2015",
            StrengthCheck = new CheckResult
            {
                Passed = strengthOk,
                ActualValue = Math.Round(sigma, 3),
                AllowableValue = fg,
                Description = $"σ = {sigma:F3} MPa ≤ fg = {fg} MPa → {(strengthOk ? "通过" : "不通过")}"
            },
            DeflectionCheck = new CheckResult
            {
                Passed = deflectionOk,
                ActualValue = Math.Round(df, 3),
                AllowableValue = Math.Round(dfLimit, 3),
                Description = $"df = {df:F3} mm ≤ a/60 = {dfLimit:F3} mm → {(deflectionOk ? "通过" : "不通过")}"
            },
            Steps = steps
        });
    }

    /// <summary>中空玻璃计算</summary>
    [HttpPost("InsulatingGlass")]
    [ProducesResponseType(typeof(GlassCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<GlassCalculationResult> CalculateInsulatingGlass(
        [FromBody] InsulatingGlassCalculationRequest req)
    {
        var steps = new List<CalculationStep>();
        int stepNo = 1;

        double a = Math.Min(req.ShortSide, req.LongSide);
        double b = Math.Max(req.ShortSide, req.LongSide);
        double ratio = a / b;

        double t1 = GetMinThickness(req.OuterThickness);
        double t2 = GetMinThickness(req.InnerThickness);

        // 荷载分配系数 (JGJ 102-2003 5.3)
        double t1_3 = Math.Pow(t1, 3);
        double t2_3 = Math.Pow(t2, 3);
        double phi1 = t1_3 / (t1_3 + t2_3);
        double phi2 = t2_3 / (t1_3 + t2_3);

        steps.Add(new() { StepNo = stepNo++, Name = "荷载分配系数",
            Formula = "φ1 = t1³/(t1³+t2³), φ2 = t2³/(t1³+t2³)",
            Process = $"t1={t1}mm, t2={t2}mm",
            Result = $"φ1(外) = {phi1:F4}, φ2(内) = {phi2:F4}" });

        double wk = req.WindLoad * 0.001; // kN/m² → MPa
        double q = 1.5 * wk;

        // 气压差附加荷载
        double Wg = 0.0;
        if (req.AltitudeDifference > 0)
            Wg += 0.012 * req.AltitudeDifference * 1e-3; // MPa
        if (req.TemperatureDifference > 0)
            Wg += 0.0034 * req.TemperatureDifference * 1e-3;

        double fg1 = GetGlassDesignStrength(req.OuterGlassType, req.OuterThickness);
        double fg2 = GetGlassDesignStrength(req.InnerGlassType, req.InnerThickness);

        // 弯矩系数和挠度系数
        double m = GetBendingCoefficient(ratio, "FourSides");
        double mu = GetDeflectionCoefficient(ratio, "FourSides");

        // 外片
        double q1 = q * phi1 + 1.5 * Wg * phi2;
        double wk1 = wk * phi1 + Wg * phi2;
        double theta1 = wk1 * Math.Pow(a, 4) / (E_GLASS * Math.Pow(t1, 4));
        double eta1 = GetReductionFactor(theta1);
        double sigma1 = 6 * m * q1 * a * a * eta1 / (t1 * t1);
        double D1 = E_GLASS * t1_3 / (12 * (1 - NU_GLASS * NU_GLASS));
        double df1 = eta1 * mu * wk1 * Math.Pow(a, 4) / D1;

        // 内片
        double q2 = q * phi2 + 1.5 * Wg * phi1;
        double wk2 = wk * phi2 + Wg * phi1;
        double theta2 = wk2 * Math.Pow(a, 4) / (E_GLASS * Math.Pow(t2, 4));
        double eta2 = GetReductionFactor(theta2);
        double sigma2 = 6 * m * q2 * a * a * eta2 / (t2 * t2);
        double D2 = E_GLASS * t2_3 / (12 * (1 - NU_GLASS * NU_GLASS));
        double df2 = eta2 * mu * wk2 * Math.Pow(a, 4) / D2;

        double sigma_max = Math.Max(sigma1, sigma2);
        double df_max = Math.Max(df1, df2);
        double dfLimit = a / 60.0;

        steps.Add(new() { StepNo = stepNo++, Name = "外片应力", Result = $"σ1 = {sigma1:F3} MPa (≤ {fg1} MPa)" });
        steps.Add(new() { StepNo = stepNo++, Name = "内片应力", Result = $"σ2 = {sigma2:F3} MPa (≤ {fg2} MPa)" });
        steps.Add(new() { StepNo = stepNo++, Name = "最大挠度", Result = $"df = {df_max:F3} mm (≤ {dfLimit:F3} mm)" });

        bool strengthOk = sigma1 <= fg1 && sigma2 <= fg2;
        bool deflectionOk = df_max <= dfLimit;

        return Ok(new GlassCalculationResult
        {
            Passed = strengthOk && deflectionOk,
            MaxStress = Math.Round(sigma_max, 3),
            MaxDeflection = Math.Round(df_max, 3),
            DeflectionLimit = Math.Round(dfLimit, 3),
            LoadShareFactor = Math.Round(phi1, 4),
            Standard = "JGJ 102-2003",
            StrengthCheck = new CheckResult
            {
                Passed = strengthOk,
                ActualValue = Math.Round(sigma_max, 3),
                AllowableValue = Math.Min(fg1, fg2),
                Description = $"外片 σ={sigma1:F3} MPa≤{fg1}, 内片 σ={sigma2:F3} MPa≤{fg2}"
            },
            DeflectionCheck = new CheckResult
            {
                Passed = deflectionOk,
                ActualValue = Math.Round(df_max, 3),
                AllowableValue = Math.Round(dfLimit, 3),
                Description = $"df = {df_max:F3} mm ≤ a/60 = {dfLimit:F3} mm"
            },
            Steps = steps
        });
    }

    /// <summary>夹胶玻璃计算 (SGP/PVB)</summary>
    [HttpPost("LaminatedGlass")]
    [ProducesResponseType(typeof(GlassCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<GlassCalculationResult> CalculateLaminatedGlass(
        [FromBody] LaminatedGlassCalculationRequest req)
    {
        var steps = new List<CalculationStep>();
        int stepNo = 1;

        double a = Math.Min(req.ShortSide, req.LongSide);
        double b = Math.Max(req.ShortSide, req.LongSide);
        double ratio = a / b;

        // 仅支持双片夹层玻璃的完整SGP计算
        if (req.GlassThicknesses.Count < 2 || req.InterlayerThicknesses.Count < 1)
            return BadRequest("夹层玻璃至少需要2层玻璃和1层胶片");

        // 夹胶玻璃等效厚度公式用公称厚度 (计算书一致)
        double t1 = req.GlassThicknesses[0]; // 公称厚度，非最小厚度
        double t2 = req.GlassThicknesses[1];
        double tv = req.InterlayerThicknesses[0];

        // SGP等效厚度完整计算 (JGJ102报批稿6.2.4, prEN13474)
        double ts = 0.5 * (t1 + t2) + tv;
        double ts1 = ts * t1 / (t1 + t2);
        double ts2 = ts * t2 / (t1 + t2);
        double Is = t1 * ts2 * ts2 + t2 * ts1 * ts1;

        steps.Add(new() { StepNo = stepNo++, Name = "夹层参数",
            Formula = "ts=0.5(t1+t2)+tv, ts,1=ts×t1/(t1+t2), Is=t1×ts,2²+t2×ts,1²",
            Process = $"t1={t1}mm, t2={t2}mm, tv={tv}mm",
            Result = $"ts={ts:F2}mm, ts,1={ts1:F2}mm, ts,2={ts2:F2}mm, Is={Is:F3}mm³" });

        // 夹层胶片剪切模量 G
        double G = req.InterlayerShearModulus ?? (req.InterlayerType switch
        {
            "SGP" => 11.3,  // 50℃, 1min持荷
            "PVB" when req.LoadDuration <= 3 => 0.5,
            "PVB" => 0.05,
            "EVA" => 0.3,
            _ => 0.5
        });

        double L = Math.Min(a, b); // 最小平面尺寸

        // Γ = 1/(1+9.6×E×Is×tv/(G×ts²×L²))
        double Gamma = 1.0 / (1.0 + 9.6 * E_GLASS * Is * tv / (G * ts * ts * L * L));

        steps.Add(new() { StepNo = stepNo++, Name = "剪切传递系数Γ",
            Formula = "Γ = 1/(1+9.6×E×Is×tv/(G×ts²×L²))",
            Process = $"G={G}MPa, L={L}mm",
            Result = $"Γ = {Gamma:F4}" });

        // 等效厚度
        double te_w_raw = Math.Pow(Math.Pow(t1, 3) + Math.Pow(t2, 3) + 12 * Gamma * Is, 1.0 / 3.0);
        double te_w = Math.Min(te_w_raw, t1 + t2);

        double t1e_sigma_raw = Math.Pow(Math.Pow(te_w, 3) / (t1 + 2 * Gamma * ts2), 0.5);
        double t1e_sigma = Math.Min(t1e_sigma_raw, t1 + t2);
        double t2e_sigma_raw = Math.Pow(Math.Pow(te_w, 3) / (t2 + 2 * Gamma * ts1), 0.5);
        double t2e_sigma = Math.Min(t2e_sigma_raw, t1 + t2);

        steps.Add(new() { StepNo = stepNo++, Name = "等效厚度",
            Formula = "te,w=(t1³+t2³+12ΓIs)^(1/3), tie,σ=(te,w³/(ti+2Γts,j))^0.5",
            Process = $"te,w(原始)={te_w_raw:F3}mm",
            Result = $"te,w={te_w:F3}mm, t1e,σ={t1e_sigma:F3}mm, t2e,σ={t2e_sigma:F3}mm" });

        // 荷载组合
        double wk = (req.WindLoad > 0 ? req.WindLoad : req.PanelWindLoad) * 0.001; // kN/m² → MPa
        double qEAk = req.SeismicLoad;
        if (qEAk == 0 && req.BetaE > 0 && req.AlphaMax > 0)
        {
            // 玻璃自重估算地震
            double glassDensity = 0.0000256; // N/mm³
            double GAk = glassDensity * (req.GlassThicknesses[0] + req.GlassThicknesses[1]);
            qEAk = req.BetaE * req.AlphaMax * GAk;
        }

        double qk = wk + 0.5 * qEAk;  // 标准值组合
        double q = 1.5 * wk + 0.5 * 1.4 * qEAk; // 设计值组合

        steps.Add(new() { StepNo = stepNo++, Name = "荷载组合",
            Formula = "qk=wk+0.5×qEAk, q=1.5wk+0.5×1.4×qEAk",
            Process = $"wk={wk}MPa, qEAk={qEAk:F6}MPa",
            Result = $"qk={qk:F6}MPa, q={q:F6}MPa" });

        // 弯矩系数m和挠度系数μ
        double m = GetBendingCoefficient(ratio, "FourSides");
        double mu_coeff = GetDeflectionCoefficient(ratio, "FourSides");

        // 1# 玻璃强度计算
        double theta1 = qk * Math.Pow(a, 4) / (E_GLASS * Math.Pow(t1e_sigma, 4));
        double eta1 = GetReductionFactor(theta1);
        double sigma1 = 6 * m * q * a * a * eta1 / (t1e_sigma * t1e_sigma);

        steps.Add(new() { StepNo = stepNo++, Name = "1#玻璃强度",
            Formula = "σ1 = 6m×q×a²×η1/t1e,σ²",
            Process = $"θ1={theta1:F3}, η1={eta1}, m={m:F4}",
            Result = $"σ1 = {sigma1:F3} MPa" });

        // 2# 玻璃强度计算
        double theta2 = qk * Math.Pow(a, 4) / (E_GLASS * Math.Pow(t2e_sigma, 4));
        double eta2 = GetReductionFactor(theta2);
        double sigma2 = 6 * m * q * a * a * eta2 / (t2e_sigma * t2e_sigma);

        steps.Add(new() { StepNo = stepNo++, Name = "2#玻璃强度",
            Formula = "σ2 = 6m×q×a²×η2/t2e,σ²",
            Process = $"θ2={theta2:F3}, η2={eta2}, m={m:F4}",
            Result = $"σ2 = {sigma2:F3} MPa" });

        // 挠度计算 df = η×μ×wk×a⁴/D
        double D = E_GLASS * Math.Pow(te_w, 3) / (12 * (1 - NU_GLASS * NU_GLASS));
        double theta_d = wk * Math.Pow(a, 4) / (E_GLASS * Math.Pow(te_w, 4));
        double eta_d = GetReductionFactor(theta_d);
        double df = eta_d * mu_coeff * wk * Math.Pow(a, 4) / D;
        double dfLimit = a / 60.0;

        steps.Add(new() { StepNo = stepNo++, Name = "挠度计算",
            Formula = "df = η×μ×wk×a⁴/D, D=E×te,w³/(12(1-ν²))",
            Process = $"D={D:F0}, θ={theta_d:F3}, η={eta_d}, μ={mu_coeff:F6}",
            Result = $"df = {df:F3} mm ≤ a/60 = {dfLimit:F3} mm" });

        double fg = GetGlassDesignStrength(req.GlassType, req.GlassThicknesses[0]);
        bool strengthOk = sigma1 <= fg && sigma2 <= fg;
        bool deflectionOk = df <= dfLimit;

        return Ok(new GlassCalculationResult
        {
            Passed = strengthOk && deflectionOk,
            MaxStress = Math.Round(Math.Max(sigma1, sigma2), 3),
            MaxDeflection = Math.Round(df, 3),
            DeflectionLimit = Math.Round(dfLimit, 3),
            EquivalentThickness = Math.Round(te_w, 3),
            Standard = "JGJ 102-2003",
            StrengthCheck = new CheckResult
            {
                Passed = strengthOk,
                ActualValue = Math.Round(Math.Max(sigma1, sigma2), 3),
                AllowableValue = fg,
                Description = $"1#: σ1={sigma1:F3}MPa, 2#: σ2={sigma2:F3}MPa ≤ fg={fg}MPa"
            },
            DeflectionCheck = new CheckResult
            {
                Passed = deflectionOk,
                ActualValue = Math.Round(df, 3),
                AllowableValue = Math.Round(dfLimit, 3),
                Description = $"df = {df:F3} mm ≤ a/60 = {dfLimit:F3} mm"
            },
            Steps = steps
        });
    }

    /// <summary>玻璃肋计算</summary>
    [HttpPost("GlassRib")]
    [ProducesResponseType(typeof(GlassCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<GlassCalculationResult> CalculateGlassRib(
        [FromBody] GlassRibCalculationRequest req)
    {
        double h = req.RibHeight;
        double t = GetMinThickness(req.RibThickness);
        double L = req.Span;
        double q = req.LineLoad; // N/mm

        double I = t * Math.Pow(h, 3) / 12.0;
        double W = t * h * h / 6.0;

        double fg = GetGlassDesignStrength(req.GlassType, req.RibThickness);
        double qd = 1.5 * q;

        double M, delta;
        switch (req.SupportType)
        {
            case "Fixed":
                M = qd * L * L / 12.0;
                delta = q * Math.Pow(L, 4) / (384.0 * E_GLASS * I);
                break;
            case "Cantilever":
                M = qd * L * L / 2.0;
                delta = q * Math.Pow(L, 4) / (8.0 * E_GLASS * I);
                break;
            default:
                M = qd * L * L / 8.0;
                delta = 5.0 * q * Math.Pow(L, 4) / (384.0 * E_GLASS * I);
                break;
        }

        double sigma = M / W;
        double deltaLimit = L / 60.0;
        bool strengthOk = sigma <= fg;
        bool deflectionOk = delta <= deltaLimit;

        return Ok(new GlassCalculationResult
        {
            Passed = strengthOk && deflectionOk,
            MaxStress = Math.Round(sigma, 3),
            MaxDeflection = Math.Round(delta, 3),
            DeflectionLimit = Math.Round(deltaLimit, 3),
            Standard = "JGJ 102-2003",
            StrengthCheck = new CheckResult
            {
                Passed = strengthOk, ActualValue = Math.Round(sigma, 3), AllowableValue = fg,
                Description = $"σ = {sigma:F3} MPa ≤ fg = {fg} MPa"
            },
            DeflectionCheck = new CheckResult
            {
                Passed = deflectionOk, ActualValue = Math.Round(delta, 3), AllowableValue = Math.Round(deltaLimit, 3)
            },
            Steps = new List<CalculationStep>
            {
                new() { StepNo = 1, Name = "截面特性", Result = $"I = {I:F0} mm⁴, W = {W:F0} mm³" },
                new() { StepNo = 2, Name = "最大弯矩", Formula = $"M ({req.SupportType})", Result = $"M = {M:F0} N·mm" },
                new() { StepNo = 3, Name = "最大应力", Result = $"σ = {sigma:F3} MPa" },
                new() { StepNo = 4, Name = "最大挠度", Result = $"df = {delta:F3} mm" }
            }
        });
    }

    /// <summary>通用中空/夹胶/中空夹胶玻璃计算 (支持任意N片组合)</summary>
    [HttpPost("CompositeGlass")]
    [ProducesResponseType(typeof(GlassCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<GlassCalculationResult> CalculateCompositeGlass(
        [FromBody] CompositeGlassCalculationRequest req)
    {
        var steps = new List<CalculationStep>();
        int stepNo = 1;

        if (req.Layers == null || req.Layers.Count == 0)
            return BadRequest("至少需要1个玻璃层");

        int N = req.Layers.Count;
        bool isInsulating = req.AirSpaceThicknesses != null && req.AirSpaceThicknesses.Count > 0;

        if (isInsulating && req.AirSpaceThicknesses!.Count != N - 1)
            return BadRequest($"中空腔数量应为 {N - 1}，实际为 {req.AirSpaceThicknesses!.Count}");

        double a = Math.Min(req.ShortSide, req.LongSide);
        double b = Math.Max(req.ShortSide, req.LongSide);
        double ratio = a / b;
        double wk = req.WindLoad * 0.001; // kN/m² → MPa
        double q = 1.5 * wk;

        double m = GetBendingCoefficient(ratio, "FourSides");
        double mu = GetDeflectionCoefficient(ratio, "FourSides");

        steps.Add(new() { StepNo = stepNo++, Name = "基本参数",
            Formula = "a/b查表",
            Process = $"a={a}mm, b={b}mm, a/b={ratio:F4}, wk={wk:F6}MPa",
            Result = $"m={m:F4}, μ={mu:F6}" });

        // === 计算每层的等效厚度 ===
        // deflEqThick[i]: 挠度等效厚度, stressEqThick[i]: 应力等效厚度(对控制片)
        // 对于单片层: deflEqThick = stressEqThick = GetMinThickness(t)
        // 对于夹胶层: 按JGJ102-2003 5.2计算

        double[] deflEqThick = new double[N];   // 挠度等效厚度 (用于荷载分配)
        double[][] stressEqThick = new double[N][]; // 夹胶每子片的应力等效厚度
        string[] layerDescs = new string[N];
        string[][] subGlassTypes = new string[N][];
        double[][] subThicknesses = new double[N][];

        for (int i = 0; i < N; i++)
        {
            var layer = req.Layers[i];
            if (layer.LayerType == "Laminated")
            {
                var ht = layer.LaminatedThicknesses;
                var it = layer.InterlayerThicknesses;
                if (ht == null || ht.Count < 2 || it == null || it.Count < 1 || it.Count != ht.Count - 1)
                    return BadRequest($"第{i + 1}层夹胶参数不完整");

                int nGlass = ht.Count;
                double[] hk = ht.ToArray(); // 各片公称厚度

                // 截面中心位置
                double totalHeight = 0;
                // 构建截面: glass0, interlayer0, glass1, interlayer1, glass2...
                List<(double thickness, bool isGlass, int idx)> sections = new();
                for (int k = 0; k < nGlass; k++)
                {
                    sections.Add((hk[k], true, k));
                    if (k < nGlass - 1)
                        sections.Add((it[k], false, k));
                }
                totalHeight = sections.Sum(s => s.thickness);
                // 中心到底面距离
                double centroid = totalHeight / 2.0;

                // dk = 各玻璃片中心到截面中心的距离
                double[] dk = new double[nGlass];
                double pos = 0;
                int gi = 0;
                foreach (var sec in sections)
                {
                    if (sec.isGlass)
                    {
                        double mid = pos + sec.thickness / 2.0;
                        dk[gi] = Math.Abs(mid - centroid);
                        gi++;
                    }
                    pos += sec.thickness;
                }

                // Is = Σ(hk×dk²)
                double Is = 0;
                for (int k = 0; k < nGlass; k++)
                    Is += hk[k] * dk[k] * dk[k];

                // Γ = 1/(1 + 9.6×E×Is,v/(G×ts²×a²))
                // Is,v 这里简化用 Is (即 Σhk×dk²) — 按任务说明
                // ts = 总截面厚度的一半... 实际 ts = 0.5×Σhk + Σtv (中心间距的概念)
                // 更严格: ts = Σ中心距 / (nGlass-1)... 
                // 按JGJ102: ts = 0.5(t1+t2)+tv (两片时), 推广为总截面高度
                double sumTv = it.Sum();
                double sumHk = hk.Sum();
                double ts = 0.5 * sumHk + sumTv; // 近似

                double G = GetInterlayerShearModulus(layer.InterlayerType, req.LoadDuration);

                double Gamma_denom = 1.0 + 9.6 * E_GLASS * Is * sumTv / (G * ts * ts * a * a);
                double Gamma = 1.0 / Gamma_denom;

                // 挠度等效厚度 te = (Σhk³ + 12×Γ×Is)^(1/3)
                double sumHk3 = hk.Sum(h => Math.Pow(h, 3));
                double te = Math.Pow(sumHk3 + 12 * Gamma * Is, 1.0 / 3.0);

                // 应力等效厚度 te,w = (Σhk² + 2×Γ×Σhk×dk)^(1/2)... 
                // 实际用 JGJ102: te,σ,i = (te³ / (hi + 2×Γ×Σ其它片到中心距...))^0.5
                // 更准确: 按双片公式推广
                // te,σ,i = te,w³ / (hi × te,w²) 不对... 
                // 用: t_ie_sigma = sqrt(te_w³ / (hi + 2*Gamma*dk_i_composite))
                // 简化方案: te,σ,i = te³ / (hi × (Σhk + 2*Gamma*Σdk_other)) ... 
                // 
                // 按经典公式: 对第i片, te,σ,i = (te_w³/(hi+2Γ×dsi))^0.5
                // 其中 dsi = 该片中心到截面中心距离... 但对于ts概念
                // 
                // 简化采用: te,σ,i 按 JGJ102 推广
                // te_w_sq = Σhk² + 2*Gamma*Σ(hk*dk)... 不对
                // 
                // 回到经典: 
                // te,w = (Σhk³ + 12*Γ*Is)^(1/3)  [挠度等效]
                // 对第i片应力等效: te,σ,i = te_w³ / (hi * te2_w)
                // 其中 te2_w = te_w² / (hi + 2*Γ*dk_i) ... 
                // 
                // 标准做法(双片推广):
                // te,σ,i = sqrt(te³ / (hi + 2*Gamma*dk_i))
                // 这等价于 JGJ102 双片公式

                double[] te_sigma = new double[nGlass];
                for (int k = 0; k < nGlass; k++)
                {
                    te_sigma[k] = Math.Sqrt(Math.Pow(te, 3) / (hk[k] + 2 * Gamma * dk[k]));
                }

                deflEqThick[i] = te;
                stressEqThick[i] = te_sigma;
                subThicknesses[i] = hk;
                subGlassTypes[i] = Enumerable.Repeat(layer.GlassType, nGlass).ToArray();

                string thickDesc = string.Join("+", ht.Zip(it.Append(0.0), (h, t2) => t2 > 0 ? $"{h}+{t2}{layer.InterlayerType}" : $"{h}").ToArray());
                layerDescs[i] = $"({thickDesc})夹胶";

                steps.Add(new() { StepNo = stepNo++, Name = $"第{i + 1}层夹胶等效厚度",
                    Formula = "te=(Σhk³+12ΓIs)^(1/3), Γ=1/(1+9.6EIs·tv/(G·ts²·a²))",
                    Process = $"G={G}MPa, Γ={Gamma:F4}, Is={Is:F2}",
                    Result = $"te={te:F3}mm, te,σ=[{string.Join(",", te_sigma.Select(x => x.ToString("F3")))}]mm" });
            }
            else
            {
                // 单片
                double t = GetMinThickness(layer.Thickness);
                deflEqThick[i] = t;
                stressEqThick[i] = new[] { t };
                subThicknesses[i] = new[] { layer.Thickness };
                subGlassTypes[i] = new[] { layer.GlassType };
                layerDescs[i] = $"{layer.Thickness}mm{layer.GlassType}";
            }
        }

        // === 荷载分配 ===
        double[] phi = new double[N];
        if (isInsulating && N > 1)
        {
            double sumT3 = deflEqThick.Sum(t => Math.Pow(t, 3));
            for (int i = 0; i < N; i++)
                phi[i] = Math.Pow(deflEqThick[i], 3) / sumT3;

            steps.Add(new() { StepNo = stepNo++, Name = "中空荷载分配",
                Formula = "φi = ti³/Σtj³",
                Process = $"te=[{string.Join(",", deflEqThick.Select(x => x.ToString("F3")))}]",
                Result = $"φ=[{string.Join(",", phi.Select(x => x.ToString("F4")))}]" });
        }
        else
        {
            // 非中空(纯夹胶或单片): 整体承载
            for (int i = 0; i < N; i++) phi[i] = 1.0;
        }

        // === 气压差附加荷载 (中空) ===
        double Wg = 0.0;
        if (isInsulating)
        {
            if (req.AltitudeDifference > 0) Wg += 0.012 * req.AltitudeDifference * 1e-3;
            if (req.TemperatureDifference > 0) Wg += 0.0034 * req.TemperatureDifference * 1e-3;
        }

        // === 逐层验算 ===
        var layerResults = new List<GlassLayerResult>();
        double maxStress = 0, maxDeflection = 0;
        bool allStrengthOk = true, allDeflectionOk = true;
        double dfLimit = a / 60.0;

        for (int i = 0; i < N; i++)
        {
            var layer = req.Layers[i];
            int nSub = stressEqThick[i].Length;

            // 该层分担的荷载
            // 简化: 外片承受 q*φi + Wg*(1-φi), 内片类似
            // 严格: 中空气压差对各片的分配... 这里用简化
            double qi_design = q * phi[i];
            double wki = wk * phi[i];
            if (isInsulating && Wg > 0)
            {
                qi_design += 1.5 * Wg * (1 - phi[i]);
                wki += Wg * (1 - phi[i]);
            }

            // 对夹胶层: 逐子片验算应力
            for (int k = 0; k < nSub; k++)
            {
                double te_sigma_k = stressEqThick[i][k];
                double hk = subThicknesses[i][k];
                string glassType = subGlassTypes[i][k];

                double theta_k = wki * Math.Pow(a, 4) / (E_GLASS * Math.Pow(te_sigma_k, 4));
                double eta_k = GetReductionFactor(theta_k);
                double sigma_k = 6 * m * qi_design * a * a * eta_k / (te_sigma_k * te_sigma_k);
                double fg_k = GetGlassDesignStrength(glassType, hk);

                // 挠度: 用该层的挠度等效厚度
                double D_i = E_GLASS * Math.Pow(deflEqThick[i], 3) / (12 * (1 - NU_GLASS * NU_GLASS));
                double theta_d = wki * Math.Pow(a, 4) / (E_GLASS * Math.Pow(deflEqThick[i], 4));
                double eta_d = GetReductionFactor(theta_d);
                double df_i = eta_d * mu * wki * Math.Pow(a, 4) / D_i;

                bool stressOk = sigma_k <= fg_k;
                bool deflOk = df_i <= dfLimit;

                string desc = nSub > 1
                    ? $"第{i + 1}层{layerDescs[i]}第{k + 1}子片{hk}mm"
                    : $"第{i + 1}层{layerDescs[i]}";

                layerResults.Add(new GlassLayerResult
                {
                    LayerIndex = i,
                    Description = desc,
                    EquivalentThickness = Math.Round(te_sigma_k, 3),
                    LoadShareFactor = Math.Round(phi[i], 4),
                    Stress = Math.Round(sigma_k, 3),
                    AllowableStress = fg_k,
                    Deflection = Math.Round(df_i, 3),
                    StressOk = stressOk,
                    DeflectionOk = deflOk
                });

                if (sigma_k > maxStress) maxStress = sigma_k;
                if (df_i > maxDeflection) maxDeflection = df_i;
                if (!stressOk) allStrengthOk = false;
                if (!deflOk) allDeflectionOk = false;

                steps.Add(new() { StepNo = stepNo++, Name = desc,
                    Formula = "σ=6m·q·a²·η/te,σ², df=η·μ·wk·a⁴/D",
                    Process = $"φ={phi[i]:F4}, θ={theta_k:F3}, η={eta_k:F4}",
                    Result = $"σ={sigma_k:F3}MPa(≤{fg_k}), df={df_i:F3}mm(≤{dfLimit:F3})" });
            }
        }

        return Ok(new GlassCalculationResult
        {
            Passed = allStrengthOk && allDeflectionOk,
            MaxStress = Math.Round(maxStress, 3),
            MaxDeflection = Math.Round(maxDeflection, 3),
            DeflectionLimit = Math.Round(dfLimit, 3),
            Standard = "JGJ 102-2003",
            StrengthCheck = new CheckResult
            {
                Passed = allStrengthOk,
                ActualValue = Math.Round(maxStress, 3),
                AllowableValue = layerResults.Min(r => r.AllowableStress),
                Description = string.Join("; ", layerResults.Select(r => $"{r.Description}: σ={r.Stress}MPa"))
            },
            DeflectionCheck = new CheckResult
            {
                Passed = allDeflectionOk,
                ActualValue = Math.Round(maxDeflection, 3),
                AllowableValue = Math.Round(dfLimit, 3),
                Description = $"max df={maxDeflection:F3}mm ≤ a/60={dfLimit:F3}mm"
            },
            LayerResults = layerResults,
            Steps = steps
        });
    }

    /// <summary>统一玻璃计算 (新API，支持任意组合)</summary>
    [HttpPost("UnifiedCalculate")]
    [ProducesResponseType(typeof(GlassCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<GlassCalculationResult> UnifiedCalculate(
        [FromBody] UnifiedGlassCalculationRequest req)
    {
        var cfg = req.GlassConfig;
        if (cfg == null) return BadRequest("GlassConfig 不能为空");

        // 根据 PanelCategory 分派
        switch (cfg.PanelCategory)
        {
            case PanelCategory.NonInsulating:
                return CalculateNonInsulating(req, cfg.OuterPane);

            case PanelCategory.SingleInsulating:
                return CalculateSingleInsulating(req, cfg);

            case PanelCategory.DoubleInsulating:
                return CalculateDoubleInsulating(req, cfg);

            case PanelCategory.TripleInsulating:
                return CalculateTripleInsulating(req, cfg);

            default:
                return BadRequest($"不支持的面板类别: {cfg.PanelCategory}");
        }
    }

    /// <summary>统一玻璃计算 V2 (递归树版本，支持 GlassBase 多态JSON)</summary>
    [HttpPost("UnifiedCalculateV2")]
    [ProducesResponseType(typeof(GlassCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<GlassCalculationResult> UnifiedCalculateV2(
        [FromBody] UnifiedGlassCalculationRequestV2 req)
    {
        // 将递归树转为扁平 DTO，复用 V1 逻辑
        var v1Req = req.ToV1();
        return UnifiedCalculate(v1Req);
    }

    #region ===== UnifiedCalculate 内部分派 =====

    /// <summary>将 GlassPaneDto 转为旧 GlassLayer 用于 CompositeGlass 计算</summary>
    private static GlassLayer PaneToLayer(GlassPaneDto pane)
    {
        string glassTypeStr(TemperedType t) => t switch
        {
            TemperedType.Float => "Float",
            TemperedType.HalfTempered => "SemiTempered",
            TemperedType.FullTempered => "Tempered",
            _ => "Tempered"
        };
        string filmTypeStr(FilmType f) => f switch
        {
            FilmType.PVB => "PVB",
            FilmType.SGP => "SGP",
            _ => "PVB"
        };

        if (!pane.IsLaminated)
        {
            return new GlassLayer
            {
                LayerType = "Single",
                Thickness = pane.Thickness,
                GlassType = glassTypeStr(pane.TemperedType)
            };
        }
        else
        {
            var sheets = pane.LaminatedSheets ?? new List<GlassSheetDto>();
            var layers = pane.LaminatedLayers ?? new List<LaminatedLayerDto>();
            return new GlassLayer
            {
                LayerType = "Laminated",
                LaminatedThicknesses = sheets.Select(s => (double)s.Thickness).ToList(),
                InterlayerThicknesses = layers.Select(l => l.FilmThickness).ToList(),
                GlassType = sheets.Count > 0 ? glassTypeStr(sheets[0].TemperedType) : "Tempered",
                InterlayerType = layers.Count > 0 ? filmTypeStr(layers[0].FilmType) : "PVB"
            };
        }
    }

    private ActionResult<GlassCalculationResult> CalculateNonInsulating(
        UnifiedGlassCalculationRequest req, GlassPaneDto pane)
    {
        if (!pane.IsLaminated)
        {
            // 单片 → 走 SingleGlass
            string gtStr = pane.TemperedType switch
            {
                TemperedType.Float => "Float",
                TemperedType.HalfTempered => "SemiTempered",
                _ => "Tempered"
            };
            var singleReq = new SingleGlassCalculationRequest
            {
                ShortSide = req.ShortSide,
                LongSide = req.LongSide,
                Thickness = pane.Thickness,
                GlassType = gtStr,
                WindLoad = req.WindLoad,
                SupportCondition = req.SupportCondition
            };
            return CalculateSingleGlass(singleReq);
        }
        else
        {
            // 夹胶 → 走 CompositeGlass (单层夹胶)
            var compReq = new CompositeGlassCalculationRequest
            {
                ShortSide = req.ShortSide,
                LongSide = req.LongSide,
                Layers = new List<GlassLayer> { PaneToLayer(pane) },
                WindLoad = req.WindLoad,
                LoadDuration = req.LoadDuration
            };
            return CalculateCompositeGlass(compReq);
        }
    }

    private ActionResult<GlassCalculationResult> CalculateSingleInsulating(
        UnifiedGlassCalculationRequest req, GlassConfigDto cfg)
    {
        var layers = new List<GlassLayer>();
        layers.Add(PaneToLayer(cfg.OuterPane));
        layers.Add(PaneToLayer(cfg.InnerPane ?? new GlassPaneDto()));

        var airSpaces = new List<double>();
        airSpaces.Add(cfg.FirstGap?.Thickness ?? 12);

        var compReq = new CompositeGlassCalculationRequest
        {
            ShortSide = req.ShortSide,
            LongSide = req.LongSide,
            Layers = layers,
            AirSpaceThicknesses = airSpaces,
            WindLoad = req.WindLoad,
            AltitudeDifference = req.AltitudeDifference,
            TemperatureDifference = req.TemperatureDifference,
            LoadDuration = req.LoadDuration
        };
        return CalculateCompositeGlass(compReq);
    }

    private ActionResult<GlassCalculationResult> CalculateDoubleInsulating(
        UnifiedGlassCalculationRequest req, GlassConfigDto cfg)
    {
        var layers = new List<GlassLayer>();
        layers.Add(PaneToLayer(cfg.OuterPane));
        layers.Add(PaneToLayer(cfg.MiddlePane ?? new GlassPaneDto()));
        layers.Add(PaneToLayer(cfg.InnerPane ?? new GlassPaneDto()));

        var airSpaces = new List<double>();
        airSpaces.Add(cfg.FirstGap?.Thickness ?? 12);
        airSpaces.Add(cfg.SecondGap?.Thickness ?? 12);

        var compReq = new CompositeGlassCalculationRequest
        {
            ShortSide = req.ShortSide,
            LongSide = req.LongSide,
            Layers = layers,
            AirSpaceThicknesses = airSpaces,
            WindLoad = req.WindLoad,
            AltitudeDifference = req.AltitudeDifference,
            TemperatureDifference = req.TemperatureDifference,
            LoadDuration = req.LoadDuration
        };
        return CalculateCompositeGlass(compReq);
    }

    /// <summary>三玻两腔玻璃计算</summary>
    private ActionResult<GlassCalculationResult> CalculateTripleInsulating(
        UnifiedGlassCalculationRequest req, GlassConfigDto cfg)
    {
        var layers = new List<GlassLayer>();
        layers.Add(PaneToLayer(cfg.OuterPane));
        layers.Add(PaneToLayer(cfg.MiddlePane ?? new GlassPaneDto()));
        layers.Add(PaneToLayer(cfg.InnerPane ?? new GlassPaneDto()));

        var airSpaces = new List<double>();
        airSpaces.Add(cfg.FirstGap?.Thickness ?? 12);
        airSpaces.Add(cfg.SecondGap?.Thickness ?? 12);

        var compReq = new CompositeGlassCalculationRequest
        {
            ShortSide = req.ShortSide,
            LongSide = req.LongSide,
            Layers = layers,
            AirSpaceThicknesses = airSpaces,
            WindLoad = req.WindLoad,
            AltitudeDifference = req.AltitudeDifference,
            TemperatureDifference = req.TemperatureDifference,
            LoadDuration = req.LoadDuration
        };
        return CalculateCompositeGlass(compReq);
    }

    #endregion

    #region ===== 辅助方法 =====

    /// <summary>玻璃强度设计值 (JGJ113-2015 表4.1.9) - 按厚度范围区分</summary>
    private static double GetGlassDesignStrength(string glassType, double thickness)
    {
        // 中部强度(短期荷载)
        return glassType switch
        {
            "Tempered" when thickness <= 12 => 84.0,
            "Tempered" when thickness <= 19 => 72.0,
            "Tempered" => 59.0,
            "SemiTempered" when thickness <= 12 => 56.0,
            "SemiTempered" when thickness <= 19 => 48.0,
            "SemiTempered" => 40.0,
            "Float" when thickness <= 12 => 28.0,
            "Float" when thickness <= 19 => 24.0,
            "Float" => 20.0,
            _ => 28.0
        };
    }

    /// <summary>标称厚度 → 最小厚度 (JGJ 102-2003 表5.1.3)</summary>
    private static double GetMinThickness(double nominal) => nominal switch
    {
        3 => 2.8, 4 => 3.8, 5 => 4.6, 6 => 5.6,
        8 => 7.5, 10 => 9.2, 12 => 11.1, 15 => 13.8, 19 => 17.5,
        _ => nominal * 0.92
    };

    /// <summary>弯矩系数 m (JGJ102-2003 表6.1.2-1, 四边简支均布荷载)</summary>
    /// <remarks>
    /// 定义: σ_max = 6×m×q×a²×η / t²
    /// 数据来源: JGJ102-2003规范表 + 计算书验证(a/b=0.7971→m=0.0631)
    /// </remarks>
    private static double GetBendingCoefficient(double ratioAB, string support)
    {
        if (support != "FourSides") return 0.0625;
        // JGJ102-2003 表6.1.2-1 (四边简支, 均布荷载)
        // 注: 计算书验证点 a/b=0.80 → m=0.0631
        (double r, double m)[] table = {
            (0.50, 0.0906), (0.55, 0.0847), (0.60, 0.0790), (0.65, 0.0736),
            (0.70, 0.0697), (0.75, 0.0663), (0.80, 0.0631), (0.85, 0.0601),
            (0.90, 0.0561), (0.95, 0.0514), (1.00, 0.0468)
        };
        if (ratioAB <= 0.50) return 0.0906;
        if (ratioAB >= 1.00) return 0.0468;
        return Interpolate(table, ratioAB);
    }

    /// <summary>挠度系数 μ (JGJ102-2003 表6.1.3, 四边简支均布荷载)</summary>
    /// <remarks>
    /// 定义: df = η×μ×wk×a⁴ / D
    /// 数据来源: JGJ102-2003规范表 + 计算书验证(a/b=0.7971→μ=0.00606)
    /// </remarks>
    private static double GetDeflectionCoefficient(double ratioAB, string support)
    {
        if (support != "FourSides") return 0.00260;
        // JGJ102-2003 表6.1.3 (四边简支, 均布荷载)
        // 注: 计算书验证点 a/b=0.80 → μ=0.00606
        (double r, double mu)[] table = {
            (0.50, 0.01013), (0.55, 0.00918), (0.60, 0.00830), (0.65, 0.00749),
            (0.70, 0.00697), (0.75, 0.00650), (0.80, 0.00606), (0.85, 0.00539),
            (0.90, 0.00479), (0.95, 0.00406), (1.00, 0.00340)
        };
        if (ratioAB <= 0.50) return 0.01013;
        if (ratioAB >= 1.00) return 0.00340;
        return Interpolate(table, ratioAB);
    }

    /// <summary>折减系数 η (JGJ102-2003 表6.1.2-2)</summary>
    private static double GetReductionFactor(double theta)
    {
        // 修正: 原5段粗略插值精度不足，替换为JGJ102-2003/JGJ133-2001精细插值表
        (double t, double e)[] table = {
            (0, 1.0), (0.5, 1.0), (1.0, 1.0), (1.5, 1.0), (2.0, 0.98),
            (2.5, 0.95), (3.0, 0.92), (4.0, 0.87), (5.0, 0.83),
            (7.5, 0.75), (10, 0.68), (15, 0.58), (20, 0.52),
            (25, 0.48), (30, 0.46), (50, 0.43), (100, 0.41)
        };
        if (theta <= 0) return 1.0;
        if (theta >= 100) return 0.41;
        for (int i = 1; i < table.Length; i++)
        {
            if (theta <= table[i].t)
            {
                double frac = (theta - table[i-1].t) / (table[i].t - table[i-1].t);
                return table[i-1].e + frac * (table[i].e - table[i-1].e);
            }
        }
        return 0.41;
    }

    /// <summary>夹层胶片剪切模量 G (MPa)</summary>
    private static double GetInterlayerShearModulus(string type, double loadDuration) => type switch
    {
        "SGP" => 11.3,   // 50℃
        "PVB" when loadDuration <= 3 => 0.4,
        "PVB" => 0.05,
        "EVA" => 0.3,
        _ => 0.4
    };

    private static double Interpolate((double r, double v)[] table, double x)
    {
        for (int i = 1; i < table.Length; i++)
        {
            if (x <= table[i].r)
            {
                double frac = (x - table[i - 1].r) / (table[i].r - table[i - 1].r);
                return table[i - 1].v + frac * (table[i].v - table[i - 1].v);
            }
        }
        return table[^1].v;
    }

    #endregion
}
