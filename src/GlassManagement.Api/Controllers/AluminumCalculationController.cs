using Microsoft.AspNetCore.Mvc;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

/// <summary>铝合金计算 - 按JGJ102-2003, GB50429-2007</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("铝合金计算")]
public class AluminumCalculationController : ControllerBase
{
    private const double E_ALU = 70000.0; // MPa

    /// <summary>铝合金横梁计算 (三角荷载简支梁模型)</summary>
    [HttpPost("Rafter")]
    [ProducesResponseType(typeof(AluminumCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<AluminumCalculationResult> CalculateRafter(
        [FromBody] AluminumRafterCalculationRequest req)
    {
        var steps = new List<CalculationStep>();
        int sn = 1;

        double B = req.Span;     // 横梁跨度 mm
        double H = req.Spacing;  // 横梁计算间距 mm
        double wk = req.WindLoad * 0.001; // kN/m² → MPa
        var (fa, tau_a, fc) = GetAluminumStrength(req.AluminumGrade);
        double gamma = req.Gamma;

        // (1) 风荷载线荷载
        double qwk = wk * B; // N/mm
        double qw = 1.5 * qwk;

        steps.Add(new() { StepNo = sn++, Name = "风荷载线荷载",
            Formula = "qwk = wk×B, qw = 1.5×qwk",
            Process = $"qwk = {wk}×{B}",
            Result = $"qwk = {qwk:F3} N/mm, qw = {qw:F3} N/mm" });

        // (2) 地震作用线荷载
        double qEAk = req.BetaE * req.AlphaMax * req.GravityLoad; // MPa
        double qEk = qEAk * B; // N/mm
        double qE = 1.4 * qEk;

        steps.Add(new() { StepNo = sn++, Name = "地震作用线荷载",
            Formula = "qEAk = βE×αmax×Gk/A, qEk = qEAk×B",
            Process = $"qEAk = {req.BetaE}×{req.AlphaMax}×{req.GravityLoad} = {qEAk:F6} MPa",
            Result = $"qEk = {qEk:F3} N/mm, qE = {qE:F3} N/mm" });

        // (3) 荷载组合
        double q = qw + 0.5 * qE;  // 强度设计值 Sw+0.5SE
        double qk_defl = qwk;       // 挠度用标准值

        steps.Add(new() { StepNo = sn++, Name = "荷载组合 (Sw+0.5SE)",
            Formula = "q = qw + 0.5×qE",
            Result = $"q = {q:F3} N/mm (强度), qk = {qk_defl:F3} N/mm (挠度)" });

        // (4) 三角荷载简支梁弯矩 M = qB²/12
        double My = q * B * B / 12.0;

        steps.Add(new() { StepNo = sn++, Name = "风荷载+地震弯矩 (三角形分布)",
            Formula = "My = q×B²/12",
            Process = $"My = {q:F3}×{B}²/12",
            Result = $"My = {My:F3} N·mm" });

        // (5) 自重弯矩 (矩形分布)
        double H1 = req.GravityHeight > 0 ? req.GravityHeight : H;
        double Gk = req.GravityLoad * H1; // N/mm
        double G = 1.3 * Gk;
        double Mx = G * B * B / 8.0;

        steps.Add(new() { StepNo = sn++, Name = "自重弯矩",
            Formula = "Mx = G×B²/8",
            Process = $"Gk = {req.GravityLoad}×{H1} = {Gk:F3} N/mm, G = {G:F3} N/mm",
            Result = $"Mx = {Mx:F3} N·mm" });

        // 截面特性
        double Ix = req.Ix ?? 115810;
        double Iy = req.Iy ?? 123650;
        double Wnx = req.Wnx ?? 3662;
        double Wny = req.Wny ?? 5763;
        double An = req.An ?? 449.3;
        double Sx = req.Sx ?? 2994;
        double Sy = req.Sy ?? 3546;
        double tx = req.Tx;
        double ty = req.Ty;

        steps.Add(new() { StepNo = sn++, Name = "截面特性",
            Result = $"Ix={Ix}mm⁴, Iy={Iy}mm⁴, Wnx={Wnx}mm³, Wny={Wny}mm³" });

        // (6) 双向弯曲强度校核 Mx/γxWnx + My/γyWny ≤ fa (6.2.4[JGJ102-2003])
        double sigma = Mx / (gamma * Wnx) + My / (gamma * Wny);
        bool strengthOk = sigma <= fa;

        steps.Add(new() { StepNo = sn++, Name = "抗弯强度校核",
            Formula = "Mx/(γ×Wnx) + My/(γ×Wny) ≤ fa  ……6.2.4[JGJ102-2003]",
            Process = $"{Mx:F1}/({gamma}×{Wnx}) + {My:F1}/({gamma}×{Wny})",
            Result = $"σ = {sigma:F3} MPa {(strengthOk ? "≤" : ">")} {fa} MPa → {(strengthOk ? "✅ 通过" : "❌ 不通过")}" });

        // (7) 挠度
        double df1Limit = B / 180.0;
        double df2Limit = B / 180.0; // 也可按GB/T21086取B/500
        double df1 = qk_defl * Math.Pow(B, 4) / (120.0 * E_ALU * Iy);
        double df2 = 5.0 * Gk * Math.Pow(B, 4) / (384.0 * E_ALU * Ix);

        bool deflectionOk = df1 <= df1Limit && df2 <= df2Limit;

        steps.Add(new() { StepNo = sn++, Name = "挠度校核 (风荷载方向)",
            Formula = "df1 = qk×B⁴/(120×E×Iy)  (三角荷载)",
            Process = $"df1 = {qk_defl:F3}×{B}⁴/(120×{E_ALU}×{Iy})",
            Result = $"df1 = {df1:F3} mm ≤ B/180 = {df1Limit:F3} mm" });
        steps.Add(new() { StepNo = sn++, Name = "挠度校核 (自重方向)",
            Formula = "df2 = 5Gk×B⁴/(384×E×Ix)",
            Result = $"df2 = {df2:F3} mm ≤ B/180 = {df2Limit:F3} mm" });

        // (8) 抗剪计算
        // 三角荷载剪力 V = qB/4
        double Vwk = qwk * B / 4;
        double Vw = 1.5 * Vwk;
        double VEk = qEk * B / 4;
        double VE = 1.4 * VEk;
        double Vx = Vw + 0.5 * VE;
        double Vy = 1.3 * req.GravityLoad * B * H1 / 2;

        double tau_x = Vx * Sy / (Iy * ty);
        double tau_y = Vy * Sx / (Ix * tx);
        bool shearOk = tau_x <= tau_a && tau_y <= tau_a;

        steps.Add(new() { StepNo = sn++, Name = "抗剪校核",
            Formula = "τ = V×S/(I×t) ≤ τa  ……6.2.5[JGJ102-2003]",
            Process = $"τx = {Vx:F1}×{Sy}/{Iy}/{ty}, τy = {Vy:F1}×{Sx}/{Ix}/{tx}",
            Result = $"τx = {tau_x:F3} MPa, τy = {tau_y:F3} MPa ≤ τa = {tau_a} MPa" });

        return Ok(new AluminumCalculationResult
        {
            Passed = strengthOk && deflectionOk && shearOk,
            MaxBendingMoment = Math.Round(My, 1),
            MaxDeflection = Math.Round(Math.Max(df1, df2), 3),
            DeflectionLimit = Math.Round(df1Limit, 3),
            Standard = "JGJ 102-2003, GB50429-2007",
            StrengthCheck = new CheckResult
            {
                Passed = strengthOk,
                ActualValue = Math.Round(sigma, 3),
                AllowableValue = fa,
                Description = $"σ = {sigma:F3} MPa ≤ fa = {fa} MPa"
            },
            DeflectionCheck = new CheckResult
            {
                Passed = deflectionOk,
                ActualValue = Math.Round(Math.Max(df1, df2), 3),
                AllowableValue = Math.Round(df1Limit, 3),
                Description = $"df1={df1:F3}mm, df2={df2:F3}mm ≤ {df1Limit:F3}mm"
            },
            ShearCheck = new CheckResult
            {
                Passed = shearOk,
                ActualValue = Math.Round(Math.Max(tau_x, tau_y), 3),
                AllowableValue = tau_a,
                Description = $"τx={tau_x:F3}MPa, τy={tau_y:F3}MPa ≤ {tau_a}MPa"
            },
            Steps = steps
        });
    }

    /// <summary>铝合金立柱计算 (双跨梁模型)</summary>
    [HttpPost("Mullion")]
    [ProducesResponseType(typeof(AluminumCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<AluminumCalculationResult> CalculateMullion(
        [FromBody] AluminumMullionCalculationRequest req)
    {
        var steps = new List<CalculationStep>();
        int sn = 1;

        double L = req.Height;     // 立柱总跨度 mm
        double L1 = req.ShortSpan > 0 ? req.ShortSpan : L * 0.15;
        double L2 = req.LongSpan > 0 ? req.LongSpan : L - L1;
        double B = req.Spacing;     // 立柱计算间距 mm
        double wk = req.WindLoad * 0.001; // kN/m² → MPa
        var (fa, tau_a, fc) = GetAluminumStrength(req.AluminumGrade);
        double gamma = req.Gamma;

        steps.Add(new() { StepNo = sn++, Name = "基本参数",
            Result = $"L={L}mm(L1={L1}mm, L2={L2}mm), B={B}mm, fa={fa}MPa" });

        // (1) 风荷载线荷载
        double qwk = wk * B;
        double qw = 1.5 * qwk;

        // (2) 地震线荷载
        double qEAk = req.BetaE * req.AlphaMax * req.SeismicGravityLoad;
        double qEk = qEAk * B;
        double qE = 1.4 * qEk;

        // (3) 组合
        double q = qw + 0.5 * qE;
        double qk_defl = qwk;

        steps.Add(new() { StepNo = sn++, Name = "荷载",
            Formula = "q = 1.5×wk×B + 0.5×1.4×qEAk×B",
            Result = $"qwk={qwk:F3}, qw={qw:F3}, qEk={qEk:F3}, qE={qE:F3}, q={q:F3} N/mm" });

        // (4) 双跨梁: 中支座弯矩 M1 和反力 R1
        // M1 = -q×(L1³+L2³)/(8L)
        double M1 = q * (Math.Pow(L1, 3) + Math.Pow(L2, 3)) / (8 * L);
        // R1 = q×L1/2 + M1/L1 + q×L2/2 + M1/L2 (M1取绝对值，符号已处理)
        double R1 = q * L1 / 2 + M1 / L1 + q * L2 / 2 + M1 / L2;

        steps.Add(new() { StepNo = sn++, Name = "双跨梁中支座弯矩和反力",
            Formula = "M1 = q×(L1³+L2³)/(8L), R1 = qL1/2+M1/L1+qL2/2+M1/L2",
            Process = $"M1 = {q:F3}×({L1}³+{L2}³)/(8×{L})",
            Result = $"|M1| = {M1:F1} N·mm, R1 = {R1:F1} N" });

        // (5) 截面惯性矩预选(挠度)
        // 查《建筑结构静力计算手册》双跨梁挠度系数
        double k1_param = 4 * M1 / (q * L2 * L2);
        double k2_param = 0;
        double A_param = 2 + k1_param - k2_param;
        double R_param = Math.Pow(Math.Pow(A_param / 4, 2) - k1_param / 2, 1.5);
        R_param = Math.Max(R_param, 1e-10);
        double theta_param = (1.0 / 3.0) * Math.Acos(Math.Max(-1, Math.Min(1,
            (Math.Pow(A_param, 3) - 12 * k1_param * A_param - 8 * (1 - 2 * k1_param - k2_param)) / (64 * R_param))));
        double x0 = A_param / 4 + 2 * Math.Pow(R_param, 1.0 / 3.0) * Math.Cos(theta_param + 240 * Math.PI / 180);
        double lambda = x0 * (1 - 2 * k1_param + 3 * k1_param * x0 - 2 * x0 * x0 - k1_param * x0 * x0 + Math.Pow(x0, 3));
        lambda = Math.Abs(lambda);

        // 挠度限值 L2/180
        double dfLimit = L2 / 180.0;
        if (L2 > 4500) dfLimit = Math.Min(dfLimit, 30.0);
        else dfLimit = Math.Min(dfLimit, 20.0);

        steps.Add(new() { StepNo = sn++, Name = "双跨梁挠度系数",
            Formula = "λ（查《建筑结构静力计算手册》表3-9）",
            Process = $"k1={k1_param:F3}, A={A_param:F3}, x0={x0:F4}",
            Result = $"λ = {lambda:F4}, df,lim = {dfLimit:F3} mm" });

        // 截面特性
        double Ix = req.Ix ?? 2656350;
        double Wnx = req.Wnx ?? 40905;
        double An = req.An ?? 1261.5;
        double t_web = req.WebThickness;
        double Sx_val = req.Sx ?? 26247;

        steps.Add(new() { StepNo = sn++, Name = "选用截面特性",
            Result = $"Ix={Ix}mm⁴, Wnx={Wnx}mm³, An={An}mm², t={t_web}mm, Sx={Sx_val}mm³" });

        // 实际挠度
        double df = lambda * qk_defl * Math.Pow(L2, 4) / (24 * E_ALU * Ix);

        steps.Add(new() { StepNo = sn++, Name = "挠度计算",
            Formula = "df = λ×qk×L2⁴/(24×E×Ix)",
            Process = $"df = {lambda:F4}×{qk_defl:F3}×{L2}⁴/(24×{E_ALU}×{Ix})",
            Result = $"df = {df:F3} mm {(df <= dfLimit ? "≤" : ">")} {dfLimit:F3} mm" });

        bool deflectionOk = df <= dfLimit;

        // (6) 轴向拉力
        double Nk = req.GravityLoad * B * L;
        double N = 1.3 * Nk;

        // (7) 抗弯强度校核 N/An + Mx/(γ×Wnx) ≤ fa  (6.3.7[JGJ102-2003])
        double sigma = N / An + M1 / (gamma * Wnx);
        bool strengthOk = sigma <= fa;

        steps.Add(new() { StepNo = sn++, Name = "轴力+弯矩组合强度校核",
            Formula = "N/An + Mx/(γ×Wnx) ≤ fa  ……6.3.7[JGJ102-2003]",
            Process = $"N={N:F1}N, An={An}mm², M1={M1:F1}N·mm, Wnx={Wnx}mm³",
            Result = $"σ = {N:F1}/{An} + {M1:F1}/({gamma}×{Wnx}) = {sigma:F3} MPa {(strengthOk ? "≤" : ">")} {fa} MPa" });

        // (8) 抗剪计算 τ = V×Sx/(Ix×t)
        // 中支座处剪力
        double V1_left = Math.Abs(q * L1 / 2 + M1 / L1);
        double V1_right = Math.Abs(q * L2 / 2 + M1 / L2);
        double V = Math.Max(V1_left, V1_right);
        double tau = V * Sx_val / (Ix * t_web);
        bool shearOk = tau <= tau_a;

        steps.Add(new() { StepNo = sn++, Name = "抗剪校核",
            Formula = "τmax = V×Sx/(Ix×t) ≤ τa",
            Process = $"V = {V:F1}N, Sx = {Sx_val}mm³",
            Result = $"τmax = {tau:F3} MPa {(shearOk ? "≤" : ">")} {tau_a} MPa → {(shearOk ? "✅ 通过" : "❌ 不通过")}" });

        return Ok(new AluminumCalculationResult
        {
            Passed = strengthOk && deflectionOk && shearOk,
            MaxBendingMoment = Math.Round(M1, 1),
            MaxDeflection = Math.Round(df, 3),
            DeflectionLimit = Math.Round(dfLimit, 3),
            Standard = "JGJ 102-2003, GB50429-2007",
            StrengthCheck = new CheckResult
            {
                Passed = strengthOk, ActualValue = Math.Round(sigma, 3), AllowableValue = fa,
                Description = $"N/An + M/(γW) = {sigma:F3} MPa ≤ fa = {fa} MPa"
            },
            DeflectionCheck = new CheckResult
            {
                Passed = deflectionOk, ActualValue = Math.Round(df, 3), AllowableValue = Math.Round(dfLimit, 3),
                Description = $"df = {df:F3} mm ≤ L2/180 = {dfLimit:F3} mm"
            },
            ShearCheck = new CheckResult
            {
                Passed = shearOk, ActualValue = Math.Round(tau, 3), AllowableValue = tau_a,
                Description = $"τ = {tau:F3} MPa ≤ τa = {tau_a} MPa"
            },
            Steps = steps
        });
    }

    /// <summary>铝板幕墙面板计算</summary>
    [HttpPost("Plate")]
    [ProducesResponseType(typeof(AluminumCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<AluminumCalculationResult> CalculatePlate(
        [FromBody] AluminumPlateCalculationRequest req)
    {
        double a = Math.Min(req.ShortSide, req.LongSide);
        double b = Math.Max(req.ShortSide, req.LongSide);
        double t = req.Thickness;
        double ratio = b / a;

        double f = req.MaterialGrade switch
        {
            "3003-H24" => 97.0, "3003-H14" => 97.0,
            "5052-H32" => 130.0, "5052-O" => 50.0,
            _ => 97.0
        };

        double E = 70000.0;
        var (k1, k2) = GetPlateCoefficients(ratio);

        // 风荷载: kN/m² → N/mm² (除以1000000)
        double W = req.WindLoad / 1e6; // N/mm²
        double sigma = k1 * W * a * a / (t * t);
        double delta = k2 * W * Math.Pow(a, 4) / (E * Math.Pow(t, 3));
        double deltaLimit = a / 180.0;

        bool strengthOk = sigma <= f;
        bool deflectionOk = delta <= deltaLimit;

        return Ok(new AluminumCalculationResult
        {
            Passed = strengthOk && deflectionOk,
            MaxBendingMoment = Math.Round(k1 * W * 1e-3 * a * a, 1),
            MaxDeflection = Math.Round(delta, 3),
            DeflectionLimit = Math.Round(deltaLimit, 3),
            Standard = "JGJ 102-2003",
            StrengthCheck = new CheckResult
            {
                Passed = strengthOk, ActualValue = Math.Round(sigma, 3), AllowableValue = f,
                Description = $"σ = {sigma:F3} MPa ≤ f = {f} MPa"
            },
            DeflectionCheck = new CheckResult
            {
                Passed = deflectionOk, ActualValue = Math.Round(delta, 3), AllowableValue = Math.Round(deltaLimit, 3)
            },
            Steps = new List<CalculationStep>
            {
                new() { StepNo = 1, Name = "板参数", Result = $"{a}×{b}×{t}mm, {req.MaterialGrade}" },
                new() { StepNo = 2, Name = "最大应力", Formula = "σ = k1×W×a²/t²", Result = $"σ = {sigma:F3} MPa" },
                new() { StepNo = 3, Name = "最大挠度", Formula = "δ = k2×Wk×a⁴/(E×t³)", Result = $"δ = {delta:F3} mm" }
            }
        });
    }

    #region ===== 辅助方法 =====

    /// <summary>铝合金强度设计值 (GB50429-2007 表4.3.4)</summary>
    /// <returns>(抗弯fa, 抗剪τa, 承压fc)</returns>
    private static (double fa, double tau_a, double fc) GetAluminumStrength(string grade) => grade switch
    {
        "6063-T5" => (90.0, 55.0, 185.0),
        "6063-T6" => (150.0, 85.0, 240.0),
        "6063A-T5" => (135.0, 75.0, 220.0),
        "6063A-T6" => (160.0, 90.0, 255.0),
        "6061-T4" => (90.0, 55.0, 210.0),
        "6061-T6" => (200.0, 115.0, 305.0),
        _ => (90.0, 55.0, 185.0)
    };

    private static (double k1, double k2) GetPlateCoefficients(double r)
    {
        (double ratio, double k1, double k2)[] table = {
            (1.0, 0.0479, 0.0138), (1.1, 0.0554, 0.0164), (1.2, 0.0627, 0.0188),
            (1.3, 0.0694, 0.0209), (1.4, 0.0755, 0.0228), (1.5, 0.0812, 0.0245),
            (1.6, 0.0862, 0.0258), (1.7, 0.0908, 0.0270), (1.8, 0.0948, 0.0280),
            (1.9, 0.0985, 0.0288), (2.0, 0.1017, 0.0295), (2.5, 0.1139, 0.0321),
            (3.0, 0.1189, 0.0330), (999, 0.1250, 0.0340),
        };
        if (r <= 1.0) return (table[0].k1, table[0].k2);
        for (int i = 1; i < table.Length; i++)
        {
            if (r <= table[i].ratio)
            {
                double frac = (r - table[i - 1].ratio) / (table[i].ratio - table[i - 1].ratio);
                return (table[i - 1].k1 + frac * (table[i].k1 - table[i - 1].k1),
                        table[i - 1].k2 + frac * (table[i].k2 - table[i - 1].k2));
            }
        }
        return (0.1250, 0.0340);
    }

    #endregion
}
