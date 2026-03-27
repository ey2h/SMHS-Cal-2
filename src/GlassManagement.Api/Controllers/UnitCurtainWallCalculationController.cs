using Microsoft.AspNetCore.Mvc;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

#region ===== 内部模型类 =====

/// <summary>单元幕墙立柱计算请求（公母框组合）</summary>
public class UnitMullionCalculationRequest
{
    /// <summary>计算点标高 (m)</summary>
    public double Height { get; set; } = 100;
    /// <summary>力学模型：SimpleBeam=简支梁, DoubleSpan=双跨梁</summary>
    public string MechanicalModel { get; set; } = "SimpleBeam";
    /// <summary>立柱跨度 L (mm)</summary>
    public double Span { get; set; } = 3000;
    /// <summary>立柱计算间距 B (mm)</summary>
    public double Spacing { get; set; } = 1100;
    /// <summary>安装方式：EccentricTension=偏心受拉, EccentricCompression=偏心受压</summary>
    public string InstallMode { get; set; } = "EccentricTension";
    /// <summary>板块配置：InsulatingGlass=中空玻璃</summary>
    public string PanelType { get; set; } = "InsulatingGlass";
    /// <summary>风荷载标准值 wk (MPa) - 支承结构</summary>
    public double WindLoad { get; set; } = 0.001285;
    /// <summary>幕墙单位面积自重 Gk/A (MPa)</summary>
    public double GravityLoad { get; set; } = 0.0005;
    /// <summary>动力放大系数 βE</summary>
    public double BetaE { get; set; } = 5.0;
    /// <summary>水平地震影响系数最大值 αmax</summary>
    public double AlphaMax { get; set; } = 0.16;

    // ===== 左单元框（公框）截面参数 =====
    /// <summary>左单元框型材号</summary>
    public string LeftProfileModel { get; set; } = "60/80系列";
    /// <summary>左单元框材质</summary>
    public string LeftAluminumGrade { get; set; } = "6063-T5";
    /// <summary>左单元框绕X轴惯性矩 I1x (mm⁴)</summary>
    public double I1x { get; set; } = 885950;
    /// <summary>左单元框绕X轴净截面抵抗矩（较小值）W1nx (mm³)</summary>
    public double W1nx { get; set; } = 21452;
    /// <summary>左单元框净截面面积 A1n (mm²)</summary>
    public double A1n { get; set; } = 991.5;
    /// <summary>左单元框截面垂直于X轴腹板总宽度 t1x (mm)</summary>
    public double T1x { get; set; } = 6;
    /// <summary>左单元框受力面对中性轴的面积矩 S1x (mm³)</summary>
    public double S1x { get; set; } = 13606;
    /// <summary>左单元框塑性发展系数 γ</summary>
    public double Gamma1 { get; set; } = 1.0;

    // ===== 右单元框（母框）截面参数 =====
    /// <summary>右单元框型材号</summary>
    public string RightProfileModel { get; set; } = "60/80系列";
    /// <summary>右单元框材质</summary>
    public string RightAluminumGrade { get; set; } = "6063-T5";
    /// <summary>右单元框绕X轴惯性矩 I2x (mm⁴)</summary>
    public double I2x { get; set; } = 885950;
    /// <summary>右单元框绕X轴净截面抵抗矩（较小值）W2nx (mm³)</summary>
    public double W2nx { get; set; } = 21452;
    /// <summary>右单元框净截面面积 A2n (mm²)</summary>
    public double A2n { get; set; } = 991.5;
    /// <summary>右单元框截面垂直于X轴腹板总宽度 t2x (mm)</summary>
    public double T2x { get; set; } = 6;
    /// <summary>右单元框受力面对中性轴的面积矩 S2x (mm³)</summary>
    public double S2x { get; set; } = 13606;
    /// <summary>右单元框塑性发展系数 γ</summary>
    public double Gamma2 { get; set; } = 1.0;
}

/// <summary>单元幕墙横梁计算请求（上下框组合）</summary>
public class UnitTransomCalculationRequest
{
    /// <summary>计算点标高 (m)</summary>
    public double Height { get; set; } = 100;
    /// <summary>横梁跨度 B (mm)</summary>
    public double Span { get; set; } = 1100;
    /// <summary>横梁计算分格高度 H (mm)</summary>
    public double PanelHeight { get; set; } = 1380;
    /// <summary>力学模型：TriangleSimpleBeam=三角荷载简支梁, UniformSimpleBeam=均布荷载简支梁</summary>
    public string MechanicalModel { get; set; } = "TriangleSimpleBeam";
    /// <summary>是否挂式结构（true=下框承重, false=上框承重）</summary>
    public bool IsHangingStructure { get; set; } = false;
    /// <summary>风荷载标准值 wk (MPa)</summary>
    public double WindLoad { get; set; } = 0.001285;
    /// <summary>幕墙单位面积自重 Gk/A (MPa)</summary>
    public double GravityLoad { get; set; } = 0.0004;
    /// <summary>动力放大系数 βE</summary>
    public double BetaE { get; set; } = 5.0;
    /// <summary>水平地震影响系数最大值 αmax</summary>
    public double AlphaMax { get; set; } = 0.16;
    /// <summary>横梁材质</summary>
    public string AluminumGrade { get; set; } = "6063-T5";

    // ===== 上框截面参数 =====
    /// <summary>上框绕X轴惯性矩 I1x (mm⁴)</summary>
    public double I1x { get; set; } = 115800;
    /// <summary>上框绕Y轴惯性矩 I1y (mm⁴)</summary>
    public double I1y { get; set; } = 123700;
    /// <summary>上框绕X轴净截面抵抗矩（较小值）W1nx (mm³)</summary>
    public double W1nx { get; set; } = 3660;
    /// <summary>上框绕Y轴净截面抵抗矩（较小值）W1ny (mm³)</summary>
    public double W1ny { get; set; } = 5760;
    /// <summary>上框截面垂直于X轴腹板总宽度 t1x (mm)</summary>
    public double T1x { get; set; } = 5;
    /// <summary>上框截面垂直于Y轴腹板总宽度 t1y (mm)</summary>
    public double T1y { get; set; } = 3;
    /// <summary>上框受力面对中性轴面积矩(绕X轴) S1x (mm³)</summary>
    public double S1x { get; set; } = 2994;
    /// <summary>上框受力面对中性轴面积矩(绕Y轴) S1y (mm³)</summary>
    public double S1y { get; set; } = 3546;
    /// <summary>上框塑性发展系数</summary>
    public double Gamma1 { get; set; } = 1.0;

    // ===== 下框截面参数 =====
    /// <summary>下框绕X轴惯性矩 I2x (mm⁴)</summary>
    public double I2x { get; set; } = 115800;
    /// <summary>下框绕Y轴惯性矩 I2y (mm⁴)</summary>
    public double I2y { get; set; } = 123700;
    /// <summary>下框绕X轴净截面抵抗矩（较小值）W2nx (mm³)</summary>
    public double W2nx { get; set; } = 3660;
    /// <summary>下框绕Y轴净截面抵抗矩（较小值）W2ny (mm³)</summary>
    public double W2ny { get; set; } = 5760;
    /// <summary>下框截面垂直于X轴腹板总宽度 t2x (mm)</summary>
    public double T2x { get; set; } = 5;
    /// <summary>下框截面垂直于Y轴腹板总宽度 t2y (mm)</summary>
    public double T2y { get; set; } = 3;
    /// <summary>下框受力面对中性轴面积矩(绕X轴) S2x (mm³)</summary>
    public double S2x { get; set; } = 2994;
    /// <summary>下框受力面对中性轴面积矩(绕Y轴) S2y (mm³)</summary>
    public double S2y { get; set; } = 3546;
    /// <summary>下框塑性发展系数</summary>
    public double Gamma2 { get; set; } = 1.0;
}

/// <summary>单元幕墙计算结果</summary>
public class UnitCurtainWallCalculationResult
{
    public bool Passed { get; set; }
    public string Standard { get; set; } = "";
    /// <summary>左/上框校核结果</summary>
    public FrameCheckResult? LeftFrame { get; set; }
    /// <summary>右/下框校核结果</summary>
    public FrameCheckResult? RightFrame { get; set; }
    /// <summary>组合挠度校核</summary>
    public CheckResult? DeflectionCheck { get; set; }
    public List<CalculationStep> Steps { get; set; } = new();
}

/// <summary>单个框的校核结果</summary>
public class FrameCheckResult
{
    public string FrameName { get; set; } = "";
    public CheckResult? StrengthCheck { get; set; }
    public CheckResult? ShearCheck { get; set; }
}

#endregion

/// <summary>单元幕墙计算 - 公母框插接组合立柱/横梁</summary>
/// <remarks>
/// 基于计算书：单元结构幕墙设计计算书
/// 规范依据：JGJ102-2003, GB50429-2007, GB50009-2012
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("单元幕墙计算")]
public class UnitCurtainWallCalculationController : ControllerBase
{
    private const double E_ALU = 70000.0; // 铝合金弹性模量 MPa

    /// <summary>单元幕墙立柱计算（公母框组合截面，等挠度原则分配荷载）</summary>
    /// <remarks>
    /// 公母框插接立柱按等挠度原则分配荷载：
    /// q1 = q × I1x/(I1x+I2x), q2 = q × I2x/(I1x+I2x)
    /// 分别对两框进行强度、挠度、抗剪校核
    /// </remarks>
    [HttpPost("Mullion")]
    [ProducesResponseType(typeof(UnitCurtainWallCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<UnitCurtainWallCalculationResult> CalculateMullion(
        [FromBody] UnitMullionCalculationRequest req)
    {
        var steps = new List<CalculationStep>();
        int sn = 1;

        double L = req.Span;
        double B = req.Spacing;
        double wk = req.WindLoad * 0.001; // kN/m² → MPa
        var (fa, tau_a, _) = GetAluminumStrength(req.LeftAluminumGrade);

        steps.Add(new() { StepNo = sn++, Name = "基本参数",
            Result = $"标高={req.Height}m, L={L}mm, B={B}mm, 模型={req.MechanicalModel}, wk={wk}MPa" });

        // ===== Step 1: 风荷载线荷载 =====
        double qwk = wk * B;
        double qw = 1.5 * qwk;
        steps.Add(new() { StepNo = sn++, Name = "风荷载线荷载",
            Formula = "qwk = wk×B, qw = 1.5×qwk",
            Process = $"qwk = {wk}×{B} = {qwk:F3} N/mm",
            Result = $"qwk = {qwk:F3} N/mm, qw = {qw:F3} N/mm" });

        // ===== Step 2: 地震作用线荷载 =====
        double qEAk = req.BetaE * req.AlphaMax * req.GravityLoad;
        double qEk = qEAk * B;
        double qE = 1.4 * qEk;
        steps.Add(new() { StepNo = sn++, Name = "地震作用线荷载",
            Formula = "qEAk = βE×αmax×Gk/A  ……5.3.4[JGJ102-2003]",
            Process = $"qEAk = {req.BetaE}×{req.AlphaMax}×{req.GravityLoad} = {qEAk:F6} MPa",
            Result = $"qEk = {qEk:F3} N/mm, qE = {qE:F3} N/mm" });

        // ===== Step 3: 荷载组合 =====
        double q = qw + 0.5 * qE;   // Sw+0.5SE 设计值
        double qk = qwk;             // 挠度用标准值
        steps.Add(new() { StepNo = sn++, Name = "荷载组合 Sw+0.5SE  ……5.4.1[JGJ102-2003]",
            Formula = "q = qw + 0.5×qE, qk = qwk",
            Result = $"q = {q:F3} N/mm (强度), qk = {qk:F3} N/mm (挠度)" });

        // ===== Step 4: 等挠度荷载分配 =====
        double I1x = req.I1x;
        double I2x = req.I2x;
        double Itotal = I1x + I2x;

        double q1k = qk * I1x / Itotal;
        double q2k = qk * I2x / Itotal;
        double q1 = q * I1x / Itotal;
        double q2 = q * I2x / Itotal;

        steps.Add(new() { StepNo = sn++, Name = "等挠度荷载分配",
            Formula = "q1 = q×I1x/(I1x+I2x), q2 = q×I2x/(I1x+I2x)",
            Process = $"I1x={I1x}mm⁴, I2x={I2x}mm⁴, Itotal={Itotal}mm⁴",
            Result = $"q1={q1:F3} N/mm, q2={q2:F3} N/mm, q1k={q1k:F3} N/mm, q2k={q2k:F3} N/mm" });

        // ===== Step 5: 弯矩计算（简支梁 M=qL²/8） =====
        double M1x = q1 * L * L / 8.0;
        double M2x = q2 * L * L / 8.0;
        steps.Add(new() { StepNo = sn++, Name = "弯矩计算（简支梁）",
            Formula = "M = q×L²/8",
            Result = $"M1x = {M1x:F1} N·mm, M2x = {M2x:F1} N·mm" });

        // ===== Step 6: 轴力分配 =====
        double Nk = req.GravityLoad * B * L;
        double N = 1.3 * Nk;
        double N1 = N / 2.0;
        double N2 = N / 2.0;
        steps.Add(new() { StepNo = sn++, Name = "轴力分配",
            Formula = "N = 1.3×Gk/A×B×L, N1=N2=N/2",
            Process = $"Nk = {req.GravityLoad}×{B}×{L} = {Nk:F1}N",
            Result = $"N = {N:F1}N, N1=N2 = {N1:F1}N" });

        // ===== Step 7: 左单元框抗弯强度校核 =====
        // N1/A1n + M1x/(γ×W1nx) ≤ fa  ……6.3.7[JGJ102-2003]
        double sigma1 = N1 / req.A1n + M1x / (req.Gamma1 * req.W1nx);
        bool strength1Ok = sigma1 <= fa;
        steps.Add(new() { StepNo = sn++, Name = "左单元框抗弯强度校核",
            Formula = "N1/A1n + M1x/(γ×W1nx) ≤ fa  ……6.3.7[JGJ102-2003]",
            Process = $"{N1:F1}/{req.A1n} + {M1x:F1}/({req.Gamma1}×{req.W1nx})",
            Result = $"σ1 = {sigma1:F3} MPa {(strength1Ok ? "≤" : ">")} {fa} MPa → {(strength1Ok ? "✅ 通过" : "❌ 不通过")}" });

        // ===== Step 8: 右单元框抗弯强度校核 =====
        double sigma2 = N2 / req.A2n + M2x / (req.Gamma2 * req.W2nx);
        bool strength2Ok = sigma2 <= fa;
        steps.Add(new() { StepNo = sn++, Name = "右单元框抗弯强度校核",
            Formula = "N2/A2n + M2x/(γ×W2nx) ≤ fa  ……6.3.7[JGJ102-2003]",
            Process = $"{N2:F1}/{req.A2n} + {M2x:F1}/({req.Gamma2}×{req.W2nx})",
            Result = $"σ2 = {sigma2:F3} MPa {(strength2Ok ? "≤" : ">")} {fa} MPa → {(strength2Ok ? "✅ 通过" : "❌ 不通过")}" });

        // ===== Step 9: 挠度计算（等挠度，仅以左框为例） =====
        // df = 5×q1k×L⁴/(384×E×I1x) 简支梁均布荷载
        double dfLimit = L / 180.0;
        if (L <= 4500) dfLimit = Math.Min(dfLimit, 20.0);
        else if (L <= 7000) dfLimit = L / 250.0 + 7.0;
        else dfLimit = L / 200.0;

        double df = 5.0 * q1k * Math.Pow(L, 4) / (384.0 * E_ALU * I1x);
        bool deflectionOk = df <= dfLimit;
        steps.Add(new() { StepNo = sn++, Name = "组合挠度校核（等挠度原理，以左框为例）",
            Formula = "df = 5×q1k×L⁴/(384×E×I1x)  ……简支梁均布荷载",
            Process = $"df = 5×{q1k:F3}×{L}⁴/(384×{E_ALU}×{I1x})",
            Result = $"df = {df:F3} mm {(deflectionOk ? "≤" : ">")} {dfLimit:F3} mm → {(deflectionOk ? "✅ 通过" : "❌ 不通过")}" });

        // ===== Step 10: 左单元框抗剪计算 =====
        double V1 = q1 * L / 2.0;
        double tau1 = V1 * req.S1x / (I1x * req.T1x);
        bool shear1Ok = tau1 <= tau_a;
        steps.Add(new() { StepNo = sn++, Name = "左单元框抗剪校核",
            Formula = "τ1 = V1×S1x/(I1x×t1x) ≤ τa  ……6.2.5[JGJ102-2003]",
            Process = $"V1 = {q1:F3}×{L}/2 = {V1:F1}N",
            Result = $"τ1 = {tau1:F3} MPa {(shear1Ok ? "≤" : ">")} {tau_a} MPa → {(shear1Ok ? "✅ 通过" : "❌ 不通过")}" });

        // ===== Step 11: 右单元框抗剪计算 =====
        double V2 = q2 * L / 2.0;
        double tau2 = V2 * req.S2x / (I2x * req.T2x);
        bool shear2Ok = tau2 <= tau_a;
        steps.Add(new() { StepNo = sn++, Name = "右单元框抗剪校核",
            Formula = "τ2 = V2×S2x/(I2x×t2x) ≤ τa",
            Process = $"V2 = {q2:F3}×{L}/2 = {V2:F1}N",
            Result = $"τ2 = {tau2:F3} MPa {(shear2Ok ? "≤" : ">")} {tau_a} MPa → {(shear2Ok ? "✅ 通过" : "❌ 不通过")}" });

        bool allPassed = strength1Ok && strength2Ok && deflectionOk && shear1Ok && shear2Ok;

        return Ok(new UnitCurtainWallCalculationResult
        {
            Passed = allPassed,
            Standard = "JGJ 102-2003, GB50429-2007",
            LeftFrame = new FrameCheckResult
            {
                FrameName = "左单元框（公框）",
                StrengthCheck = new CheckResult { Passed = strength1Ok, ActualValue = Math.Round(sigma1, 3), AllowableValue = fa,
                    Description = $"σ1={sigma1:F3}MPa ≤ fa={fa}MPa" },
                ShearCheck = new CheckResult { Passed = shear1Ok, ActualValue = Math.Round(tau1, 3), AllowableValue = tau_a,
                    Description = $"τ1={tau1:F3}MPa ≤ τa={tau_a}MPa" }
            },
            RightFrame = new FrameCheckResult
            {
                FrameName = "右单元框（母框）",
                StrengthCheck = new CheckResult { Passed = strength2Ok, ActualValue = Math.Round(sigma2, 3), AllowableValue = fa,
                    Description = $"σ2={sigma2:F3}MPa ≤ fa={fa}MPa" },
                ShearCheck = new CheckResult { Passed = shear2Ok, ActualValue = Math.Round(tau2, 3), AllowableValue = tau_a,
                    Description = $"τ2={tau2:F3}MPa ≤ τa={tau_a}MPa" }
            },
            DeflectionCheck = new CheckResult { Passed = deflectionOk, ActualValue = Math.Round(df, 3), AllowableValue = Math.Round(dfLimit, 3),
                Description = $"df={df:F3}mm ≤ {dfLimit:F3}mm" },
            Steps = steps
        });
    }

    /// <summary>单元幕墙横梁计算（上下框组合截面，等挠度原则分配荷载）</summary>
    /// <remarks>
    /// 横梁承受双向弯矩：水平方向风荷载（三角形分布）+ 垂直方向自重
    /// 按等挠度原则将水平荷载在上下框间分配
    /// 强度校核：Mx/(γx×Wnx) + My/(γy×Wny) ≤ fa  ……6.2.4[JGJ102-2003]
    /// </remarks>
    [HttpPost("Transom")]
    [ProducesResponseType(typeof(UnitCurtainWallCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<UnitCurtainWallCalculationResult> CalculateTransom(
        [FromBody] UnitTransomCalculationRequest req)
    {
        var steps = new List<CalculationStep>();
        int sn = 1;

        double B = req.Span;          // 横梁跨度
        double H = req.PanelHeight;   // 分格高度
        double wk = req.WindLoad * 0.001; // kN/m² → MPa
        var (fa, tau_a, _) = GetAluminumStrength(req.AluminumGrade);

        steps.Add(new() { StepNo = sn++, Name = "基本参数",
            Result = $"B={B}mm, H={H}mm, 模型={req.MechanicalModel}, 挂式={req.IsHangingStructure}" });

        // ===== Step 1: 风荷载 =====
        double qwk = wk * B;
        double qw = 1.5 * qwk;

        // ===== Step 2: 地震荷载 =====
        double qEAk = req.BetaE * req.AlphaMax * req.GravityLoad;
        double qEk = qEAk * B;
        double qE = 1.4 * qEk;

        // ===== Step 3: 组合 =====
        double q = qw + 0.5 * qE;
        double qk = qwk;

        steps.Add(new() { StepNo = sn++, Name = "荷载计算",
            Formula = "q = 1.5×wk×B + 0.5×1.4×qEAk×B",
            Result = $"qwk={qwk:F3}, qw={qw:F3}, qEk={qEk:F3}, qE={qE:F3}, q={q:F3} N/mm" });

        // ===== Step 4: 等挠度水平荷载分配（三角形荷载 q×B⁴/120EI） =====
        double I1y = req.I1y, I2y = req.I2y;
        double Itotal_y = I1y + I2y;
        double q1k = qk * I1y / Itotal_y;
        double q2k = qk * I2y / Itotal_y;
        double q1 = q * I1y / Itotal_y;
        double q2 = q * I2y / Itotal_y;

        steps.Add(new() { StepNo = sn++, Name = "等挠度水平荷载分配",
            Formula = "q1 = q×I1y/(I1y+I2y)  (三角荷载等挠度)",
            Result = $"q1={q1:F3}, q2={q2:F3} N/mm" });

        // ===== Step 5: 自重荷载（仅一框承受） =====
        double Gk = req.GravityLoad * H;
        double G = 1.3 * Gk;
        // 非挂式：上框承重；挂式：下框承重
        double G1 = req.IsHangingStructure ? 0 : G;
        double G2 = req.IsHangingStructure ? G : 0;
        double G1k = req.IsHangingStructure ? 0 : Gk;
        double G2k = req.IsHangingStructure ? Gk : 0;

        steps.Add(new() { StepNo = sn++, Name = "自重荷载分配",
            Formula = req.IsHangingStructure ? "挂式：下框承重" : "非挂式：上框承重",
            Result = $"Gk={Gk:F3}N/mm, G1={G1:F3}, G2={G2:F3} N/mm" });

        // ===== Step 6: 弯矩计算 =====
        // 水平方向（三角荷载）: My = q×B²/12
        double M1y = q1 * B * B / 12.0;
        double M2y = q2 * B * B / 12.0;
        // 垂直方向（均布自重）: Mx = G×B²/8
        double M1x = G1 * B * B / 8.0;
        double M2x = G2 * B * B / 8.0;

        steps.Add(new() { StepNo = sn++, Name = "弯矩计算",
            Formula = "My=q×B²/12 (三角), Mx=G×B²/8 (均布)",
            Result = $"M1y={M1y:F1}, M2y={M2y:F1}, M1x={M1x:F1}, M2x={M2x:F1} N·mm" });

        // ===== Step 7: 上框双向弯曲强度校核 =====
        double sigma1 = M1x / (req.Gamma1 * req.W1nx) + M1y / (req.Gamma1 * req.W1ny);
        bool strength1Ok = sigma1 <= fa;
        steps.Add(new() { StepNo = sn++, Name = "上框抗弯强度校核",
            Formula = "Mx/(γx×Wnx) + My/(γy×Wny) ≤ fa  ……6.2.4[JGJ102-2003]",
            Process = $"{M1x:F1}/({req.Gamma1}×{req.W1nx}) + {M1y:F1}/({req.Gamma1}×{req.W1ny})",
            Result = $"σ1 = {sigma1:F3} MPa {(strength1Ok ? "≤" : ">")} {fa} MPa" });

        // ===== Step 8: 下框双向弯曲强度校核 =====
        double sigma2 = M2x / (req.Gamma2 * req.W2nx) + M2y / (req.Gamma2 * req.W2ny);
        bool strength2Ok = sigma2 <= fa;
        steps.Add(new() { StepNo = sn++, Name = "下框抗弯强度校核",
            Formula = "Mx/(γx×Wnx) + My/(γy×Wny) ≤ fa  ……6.2.4[JGJ102-2003]",
            Result = $"σ2 = {sigma2:F3} MPa {(strength2Ok ? "≤" : ">")} {fa} MPa" });

        // ===== Step 9: 水平挠度（三角荷载：df = q×B⁴/(120×E×Iy)） =====
        double dfyLimit = B / 180.0;
        double dfy = q1k * Math.Pow(B, 4) / (120.0 * E_ALU * I1y);
        bool dfyOk = dfy <= dfyLimit;
        steps.Add(new() { StepNo = sn++, Name = "水平挠度校核",
            Formula = "dfy = q1k×B⁴/(120×E×I1y)  (三角荷载)",
            Result = $"dfy = {dfy:F3} mm {(dfyOk ? "≤" : ">")} {dfyLimit:F3} mm" });

        // ===== Step 10: 自重挠度 =====
        double dfxLimit = B / 250.0;
        double Ix_load = req.IsHangingStructure ? req.I2x : req.I1x;
        double Gk_load = req.IsHangingStructure ? G2k : G1k;
        double dfx = 5.0 * Gk_load * Math.Pow(B, 4) / (384.0 * E_ALU * Ix_load);
        bool dfxOk = dfx <= dfxLimit;
        steps.Add(new() { StepNo = sn++, Name = "自重挠度校核",
            Formula = "dfx = 5×Gk×B⁴/(384×E×Ix)",
            Result = $"dfx = {dfx:F3} mm {(dfxOk ? "≤" : ">")} {dfxLimit:F3} mm" });

        // ===== Step 11: 抗剪校核 =====
        // 三角荷载剪力 V = q×B/4
        double V1x = q1 * B / 4.0;
        double V1y = G1 * B / 2.0;
        double tau1x = V1x * req.S1y / (I1y * req.T1y);
        double tau1y = V1y * req.S1x / (req.I1x * req.T1x);
        bool shear1Ok = tau1x <= tau_a && tau1y <= tau_a;

        double V2x = q2 * B / 4.0;
        double V2y = G2 * B / 2.0;
        double tau2x = V2x * req.S2y / (I2y * req.T2y);
        double tau2y = V2y * req.S2x / (req.I2x * req.T2x);
        bool shear2Ok = tau2x <= tau_a && tau2y <= tau_a;

        steps.Add(new() { StepNo = sn++, Name = "抗剪校核",
            Formula = "τ = V×S/(I×t) ≤ τa  ……6.2.5[JGJ102-2003]",
            Result = $"上框: τx={tau1x:F3}, τy={tau1y:F3}; 下框: τx={tau2x:F3}, τy={tau2y:F3} MPa ≤ {tau_a} MPa" });

        bool allPassed = strength1Ok && strength2Ok && dfyOk && dfxOk && shear1Ok && shear2Ok;

        return Ok(new UnitCurtainWallCalculationResult
        {
            Passed = allPassed,
            Standard = "JGJ 102-2003, GB50429-2007",
            LeftFrame = new FrameCheckResult
            {
                FrameName = "上框",
                StrengthCheck = new CheckResult { Passed = strength1Ok, ActualValue = Math.Round(sigma1, 3), AllowableValue = fa },
                ShearCheck = new CheckResult { Passed = shear1Ok, ActualValue = Math.Round(Math.Max(tau1x, tau1y), 3), AllowableValue = tau_a }
            },
            RightFrame = new FrameCheckResult
            {
                FrameName = "下框",
                StrengthCheck = new CheckResult { Passed = strength2Ok, ActualValue = Math.Round(sigma2, 3), AllowableValue = fa },
                ShearCheck = new CheckResult { Passed = shear2Ok, ActualValue = Math.Round(Math.Max(tau2x, tau2y), 3), AllowableValue = tau_a }
            },
            DeflectionCheck = new CheckResult
            {
                Passed = dfyOk && dfxOk,
                ActualValue = Math.Round(Math.Max(dfy, dfx), 3),
                AllowableValue = Math.Round(Math.Min(dfyLimit, dfxLimit), 3),
                Description = $"dfy={dfy:F3}mm≤{dfyLimit:F3}mm, dfx={dfx:F3}mm≤{dfxLimit:F3}mm"
            },
            Steps = steps
        });
    }

    #region ===== 辅助方法 =====

    /// <summary>铝合金强度设计值 (GB50429-2007 表4.3.4)</summary>
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

    #endregion
}
