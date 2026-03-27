using Microsoft.AspNetCore.Mvc;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

#region ===== 内部模型类 =====

/// <summary>门窗框梃计算请求</summary>
public class DoorWindowMullionRequest
{
    /// <summary>计算点标高 (m)</summary>
    public double Height { get; set; } = 15;
    /// <summary>力学模型：SimpleBeam=简支梁</summary>
    public string MechanicalModel { get; set; } = "SimpleBeam";
    /// <summary>框梃类型：VerticalMullion=竖中梃, HorizontalTransom=横中梃</summary>
    public string MullionType { get; set; } = "VerticalMullion";
    /// <summary>框梃跨度 H 或 W (mm)</summary>
    public double Span { get; set; } = 1600;
    /// <summary>左/上受荷单元宽/高 W1/H1 (mm)</summary>
    public double LoadWidth1 { get; set; } = 600;
    /// <summary>右/下受荷单元宽/高 W2/H2 (mm)</summary>
    public double LoadWidth2 { get; set; } = 1500;
    /// <summary>框梃材质</summary>
    public string MaterialGrade { get; set; } = "PU-GlassFiber";  // 玻纤增强聚氨酯
    /// <summary>风荷载标准值 wk (MPa)</summary>
    public double WindLoad { get; set; } = 0.001;
    /// <summary>幕墙单位面积自重 Gk/A (MPa)</summary>
    public double GravityLoad { get; set; } = 0.00065;
    /// <summary>动力放大系数 βE</summary>
    public double BetaE { get; set; } = 5.0;
    /// <summary>水平地震影响系数最大值 αmax</summary>
    public double AlphaMax { get; set; } = 0.16;

    // ===== 截面参数 =====
    /// <summary>型材抗弯强度设计值 f (MPa)</summary>
    public double BendingStrength { get; set; } = 500;
    /// <summary>型材抗剪强度设计值 τ (MPa)</summary>
    public double ShearStrength { get; set; } = 40;
    /// <summary>弹性模量 E (MPa)</summary>
    public double ElasticModulus { get; set; } = 40000;
    /// <summary>绕X轴惯性矩 Ix (mm⁴)</summary>
    public double Ix { get; set; } = 154440;
    /// <summary>绕Y轴惯性矩 Iy (mm⁴)</summary>
    public double Iy { get; set; } = 18930;
    /// <summary>绕X轴净截面抵抗矩（较小值）Wnx (mm³)</summary>
    public double Wnx { get; set; } = 4013;
    /// <summary>净截面面积 An (mm²)</summary>
    public double An { get; set; } = 385.8;
    /// <summary>截面垂直于X轴腹板总宽度 t (mm)</summary>
    public double WebThickness { get; set; } = 3;
    /// <summary>受力面对中性轴的面积矩 Sx (mm³)</summary>
    public double Sx { get; set; } = 3299;
    /// <summary>塑性发展系数 γ</summary>
    public double Gamma { get; set; } = 1.0;

    // ===== 横中梃额外参数（双向弯曲时用） =====
    /// <summary>绕Y轴净截面抵抗矩（较小值）Wny (mm³) - 横中梃用</summary>
    public double? Wny { get; set; }
    /// <summary>截面垂直于Y轴腹板总宽度 ty (mm) - 横中梃用</summary>
    public double? Ty { get; set; }
    /// <summary>受力面对中性轴面积矩(绕Y轴) Sy (mm³) - 横中梃用</summary>
    public double? Sy { get; set; }

    // ===== 横中梃自重三跨梁参数 =====
    /// <summary>玻璃垫块到左支座距离 a1 (mm)</summary>
    public double GlassBlockPosition { get; set; } = 250;
    /// <summary>三跨梁各跨长度 L1,L2,L3 (mm) - 逗号分隔，如 "1100,400,700"</summary>
    public string? ThreeSpanLengths { get; set; }
}

/// <summary>门窗五金件承载力计算请求</summary>
public class HardwareCalculationRequest
{
    /// <summary>整樘门窗洞口宽 B (mm)</summary>
    public double WindowWidth { get; set; } = 2500;
    /// <summary>整樘门窗洞口高 H (mm)</summary>
    public double WindowHeight { get; set; } = 2000;
    /// <summary>风荷载标准值 wk (MPa)</summary>
    public double WindLoad { get; set; } = 0.001;
    /// <summary>幕墙单位面积自重 Gk/A (MPa)</summary>
    public double GravityLoad { get; set; } = 0.00065;
    /// <summary>动力放大系数 βE</summary>
    public double BetaE { get; set; } = 5.0;
    /// <summary>αmax</summary>
    public double AlphaMax { get; set; } = 0.16;
    /// <summary>连接片总数</summary>
    public int ConnectorCount { get; set; } = 24;
    /// <summary>受力不均匀系数 β</summary>
    public double UnevenCoefficient { get; set; } = 1.25;
    /// <summary>连接片钢板厚度 (mm)</summary>
    public double ConnectorThickness { get; set; } = 1.5;

    // ===== 连接片与窗框连接 =====
    /// <summary>紧固件类型：Rivet=抽芯铆钉, SelfTapping=自攻螺钉</summary>
    public string FastenerType { get; set; } = "Rivet";
    /// <summary>紧固件公称直径 d (mm)</summary>
    public double FastenerDiameter { get; set; } = 3;
    /// <summary>每个连接片上紧固件数量</summary>
    public int FastenersPerConnector { get; set; } = 1;
    /// <summary>窗边框壁厚 t1 (mm)</summary>
    public double FrameWallThickness { get; set; } = 1.5;
    /// <summary>窗边框材质</summary>
    public string FrameMaterial { get; set; } = "6061-T4";

    // ===== 连接片与结构连接 =====
    /// <summary>结构连接紧固件类型：ShootingNail=射钉, ExpansionBolt=膨胀螺钉</summary>
    public string StructureFastenerType { get; set; } = "ShootingNail";
    /// <summary>结构连接紧固件直径 d (mm)</summary>
    public double StructureFastenerDiameter { get; set; } = 4;
    /// <summary>每个连接片上结构连接紧固件数量</summary>
    public int StructureFastenersPerConnector { get; set; } = 1;
    /// <summary>被固定钢板抗拉强度设计值 f (MPa) - 射钉用</summary>
    public double SteelPlateStrength { get; set; } = 205;
}

/// <summary>开启扇校核请求</summary>
public class OpeningSashCheckRequest
{
    /// <summary>玻璃长边 B (mm)</summary>
    public double GlassLongSide { get; set; } = 1280;
    /// <summary>玻璃短边 D (mm)</summary>
    public double GlassShortSide { get; set; } = 1000;
    /// <summary>槽口端部到作用力结合点距离 a (mm)</summary>
    public double SlotDistance { get; set; } = 24;
    /// <summary>槽口处型材厚度 t (mm)</summary>
    public double SlotThickness { get; set; } = 1.5;
    /// <summary>水平荷载组合设计值 q (MPa)</summary>
    public double DesignLoad { get; set; } = 0.001758;
    /// <summary>型材横向抗弯强度设计值 f (MPa)</summary>
    public double TransverseBendingStrength { get; set; } = 40;
}

/// <summary>门窗计算结果</summary>
public class DoorWindowCalculationResult
{
    public bool Passed { get; set; }
    public string Standard { get; set; } = "";
    public CheckResult? StrengthCheck { get; set; }
    public CheckResult? DeflectionCheck { get; set; }
    public CheckResult? ShearCheck { get; set; }
    public List<CalculationStep> Steps { get; set; } = new();
}

/// <summary>五金件计算结果</summary>
public class HardwareCalculationResult
{
    public bool Passed { get; set; }
    public string Standard { get; set; } = "";
    /// <summary>每个连接片承受的荷载 (N)</summary>
    public double LoadPerConnector { get; set; }
    /// <summary>连接片与窗框连接校核</summary>
    public CheckResult? FrameConnectionCheck { get; set; }
    /// <summary>连接片与结构连接校核</summary>
    public CheckResult? StructureConnectionCheck { get; set; }
    public List<CalculationStep> Steps { get; set; } = new();
}

#endregion

/// <summary>门窗计算 - 框梃强度挠度、五金件、开启扇</summary>
/// <remarks>
/// 基于计算书：门窗设计计算书
/// 规范依据：JGJ214-2010, JGJ102-2003, GB/T8478-2020, GB50018-2002, T/CECS591-2019
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("门窗计算")]
public class DoorWindowCalculationController : ControllerBase
{
    /// <summary>门窗竖中梃/横中梃计算（梯形荷载简支梁）</summary>
    /// <remarks>
    /// 当受荷单元宽 W &lt; 框梃跨度 H 时，荷载为梯形分布。
    /// 梯形荷载弯矩：M = q×H²/24×(3-(W/H)²)
    /// 梯形荷载剪力：V = q×H/2×(1-W/(2H))
    /// 梯形荷载挠度：df = qk×H⁴/(240EI)×(25/8-5(W/2H)²+2(W/2H)⁴)
    /// </remarks>
    [HttpPost("Mullion")]
    [ProducesResponseType(typeof(DoorWindowCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<DoorWindowCalculationResult> CalculateMullion(
        [FromBody] DoorWindowMullionRequest req)
    {
        var steps = new List<CalculationStep>();
        int sn = 1;

        double H = req.Span;
        double W1 = req.LoadWidth1;
        double W2 = req.LoadWidth2;
        double wk = req.WindLoad * 0.001; // kN/m² → MPa
        double f = req.BendingStrength;
        double tau = req.ShearStrength;
        double E = req.ElasticModulus;
        double Ix = req.Ix;
        double gamma = req.Gamma;

        steps.Add(new() { StepNo = sn++, Name = "基本参数",
            Result = $"类型={req.MullionType}, 跨度H={H}mm, W1={W1}mm, W2={W2}mm, f={f}MPa, E={E}MPa" });

        // ===== 左/上受荷单元荷载 =====
        double qwk1 = wk * W1 / 2.0;
        double qw1 = 1.5 * qwk1;
        double qEAk = req.BetaE * req.AlphaMax * req.GravityLoad;
        double qEk1 = qEAk * W1 / 2.0;
        double qE1 = 1.4 * qEk1;
        double q1 = qw1 + 0.5 * qE1;
        double qk1 = qwk1;

        steps.Add(new() { StepNo = sn++, Name = "左/上受荷单元荷载",
            Formula = "qwk1=wk×W1/2, q1=1.5qwk1+0.5×1.4qEk1",
            Result = $"qwk1={qwk1:F3}, q1={q1:F3} N/mm" });

        // ===== 右/下受荷单元荷载 =====
        double qwk2 = wk * W2 / 2.0;
        double qw2 = 1.5 * qwk2;
        double qEk2 = qEAk * W2 / 2.0;
        double qE2 = 1.4 * qEk2;
        double q2 = qw2 + 0.5 * qE2;
        double qk2 = qwk2;

        steps.Add(new() { StepNo = sn++, Name = "右/下受荷单元荷载",
            Formula = "qwk2=wk×W2/2, q2=1.5qwk2+0.5×1.4qEk2",
            Result = $"qwk2={qwk2:F3}, q2={q2:F3} N/mm" });

        // ===== 梯形荷载弯矩 M = q×H²/24×(3-(W/H)²) =====
        double M1 = q1 * H * H / 24.0 * (3.0 - Math.Pow(W1 / H, 2));
        double M2 = q2 * H * H / 24.0 * (3.0 - Math.Pow(W2 / H, 2));
        double Mtotal = M1 + M2;

        steps.Add(new() { StepNo = sn++, Name = "弯矩计算（梯形荷载）",
            Formula = "M = q×H²/24×(3-(W/H)²)",
            Process = $"M1={M1:F1}, M2={M2:F1}",
            Result = $"M总 = {Mtotal:F1} N·mm" });

        // ===== 梯形荷载剪力 V = q×H/2×(1-W/(2H)) =====
        double V1 = q1 * H / 2.0 * (1.0 - W1 / (2.0 * H));
        double V2 = q2 * H / 2.0 * (1.0 - W2 / (2.0 * H));
        double Vtotal = V1 + V2;

        steps.Add(new() { StepNo = sn++, Name = "剪力计算",
            Formula = "V = q×H/2×(1-W/(2H))",
            Result = $"V1={V1:F1}, V2={V2:F1}, V总={Vtotal:F1} N" });

        // ===== 抗弯强度校核 =====
        double Wnx = req.Wnx;
        double sigma = Mtotal / (gamma * Wnx);
        bool strengthOk = sigma <= f;

        steps.Add(new() { StepNo = sn++, Name = "抗弯强度校核",
            Formula = "(M1+M2)/(γ×Wnx) ≤ f",
            Process = $"({M1:F1}+{M2:F1})/({gamma}×{Wnx})",
            Result = $"σ = {sigma:F3} MPa {(strengthOk ? "≤" : ">")} {f} MPa → {(strengthOk ? "✅ 通过" : "❌ 不通过")}" });

        // ===== 挠度计算（梯形荷载） =====
        // df = qk×H⁴/(240EI)×(25/8-5(W/2H)²+2(W/2H)⁴)
        double c1 = W1 / (2.0 * H);
        double df1 = qk1 * Math.Pow(H, 4) / (240.0 * E * Ix) * (25.0 / 8.0 - 5.0 * c1 * c1 + 2.0 * Math.Pow(c1, 4));

        double c2 = W2 / (2.0 * H);
        double df2 = qk2 * Math.Pow(H, 4) / (240.0 * E * Ix) * (25.0 / 8.0 - 5.0 * c2 * c2 + 2.0 * Math.Pow(c2, 4));

        double dfTotal = df1 + df2;
        double dfLimit = H / 150.0;
        double dfAbsLimit = 20.0;
        bool deflectionOk = dfTotal <= dfLimit && dfTotal <= dfAbsLimit;

        steps.Add(new() { StepNo = sn++, Name = "挠度校核（梯形荷载）",
            Formula = "df = qk×H⁴/(240EI)×(25/8-5(W/2H)²+2(W/2H)⁴)",
            Process = $"df1={df1:F3}mm, df2={df2:F3}mm",
            Result = $"df = {dfTotal:F3} mm, 限值 = min(H/150={dfLimit:F3}, {dfAbsLimit})mm → {(deflectionOk ? "✅ 通过" : "❌ 不通过")}" });

        // ===== 抗剪校核 =====
        double tauMax = Vtotal * req.Sx / (Ix * req.WebThickness);
        bool shearOk = tauMax <= tau;

        steps.Add(new() { StepNo = sn++, Name = "抗剪校核",
            Formula = "τ = (V1+V2)×Sx/(Ix×t) ≤ τ",
            Process = $"({V1:F1}+{V2:F1})×{req.Sx}/({Ix}×{req.WebThickness})",
            Result = $"τ = {tauMax:F3} MPa {(shearOk ? "≤" : ">")} {tau} MPa → {(shearOk ? "✅ 通过" : "❌ 不通过")}" });

        return Ok(new DoorWindowCalculationResult
        {
            Passed = strengthOk && deflectionOk && shearOk,
            Standard = "JGJ 102-2003, JGJ214-2010, GB/T8478-2020",
            StrengthCheck = new CheckResult { Passed = strengthOk, ActualValue = Math.Round(sigma, 3), AllowableValue = f },
            DeflectionCheck = new CheckResult { Passed = deflectionOk, ActualValue = Math.Round(dfTotal, 3),
                AllowableValue = Math.Round(Math.Min(dfLimit, dfAbsLimit), 3),
                Description = $"df={dfTotal:F3}mm ≤ H/150={dfLimit:F3}mm" },
            ShearCheck = new CheckResult { Passed = shearOk, ActualValue = Math.Round(tauMax, 3), AllowableValue = tau },
            Steps = steps
        });
    }

    /// <summary>门窗五金件（连接片）承载力计算</summary>
    /// <remarks>
    /// 整樘结构总荷载按 Vw+0.5VE 组合
    /// 连接片与窗框：按GB50018-2002 6.1.7条冷弯薄壁钢规范公式
    /// 连接片与结构：射钉按 fvf=3.7tdf，膨胀螺钉按 Nvb=nv×π×d²×fvb/4
    /// </remarks>
    [HttpPost("Hardware")]
    [ProducesResponseType(typeof(HardwareCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<HardwareCalculationResult> CalculateHardware(
        [FromBody] HardwareCalculationRequest req)
    {
        var steps = new List<CalculationStep>();
        int sn = 1;

        double B = req.WindowWidth;
        double H = req.WindowHeight;
        double wk = req.WindLoad * 0.001; // kN/m² → MPa

        // ===== Step 1: 整樘结构总荷载 =====
        double Vw = 1.5 * wk * B * H;
        double VE = 1.4 * req.BetaE * req.AlphaMax * req.GravityLoad * B * H;
        double V = Vw + 0.5 * VE;

        steps.Add(new() { StepNo = sn++, Name = "整樘结构总荷载",
            Formula = "V = 1.5×wk×B×H + 0.5×1.4×βE×αmax×Gk/A×B×H",
            Process = $"Vw={Vw:F1}N, VE={VE:F1}N",
            Result = $"V = {V:F1} N" });

        // ===== Step 2: 每个连接片荷载 =====
        double V1 = req.UnevenCoefficient * V / req.ConnectorCount;
        double v1 = V1 / req.FastenersPerConnector;

        steps.Add(new() { StepNo = sn++, Name = "每个连接片/紧固件荷载",
            Formula = "V1 = β×V/n, v1 = V1/n_fastener",
            Result = $"V1 = {V1:F1}N, v1 = {v1:F1}N" });

        // ===== Step 3: 连接片与窗框连接（GB50018-2002 6.1.7） =====
        double t = Math.Min(req.ConnectorThickness, req.FrameWallThickness);
        double t1 = Math.Max(req.ConnectorThickness, req.FrameWallThickness);
        double d = req.FastenerDiameter;
        double f_frame = GetMaterialStrength(req.FrameMaterial);

        // Nvf = 3.7×(t³×d)^0.5×f  ……6.1.7-4[GB50018-2002]
        double Nvf1 = 3.7 * Math.Sqrt(t * t * t * d) * f_frame;
        // Nvf ≤ 2.4×t×d×f  ……6.1.7-5[GB50018-2002]
        double Nvf2 = 2.4 * t * d * f_frame;
        double Nvf = Math.Min(Nvf1, Nvf2);

        // t1/t 插值
        double ratio = t1 / t;
        if (ratio >= 2.5) Nvf = Nvf2;
        else if (ratio > 1.0)
        {
            double Nvf_25 = Nvf2;
            double frac = (ratio - 1.0) / 1.5;
            Nvf = Nvf1 + frac * (Nvf_25 - Nvf1);
            Nvf = Math.Min(Nvf, Nvf2);
        }

        bool frameOk = Nvf >= v1;

        steps.Add(new() { StepNo = sn++, Name = "连接片与窗框连接校核",
            Formula = "Nvf=min(3.7(t³d)^0.5f, 2.4tdf)  ……6.1.7[GB50018-2002]",
            Process = $"t={t}mm, t1={t1}mm, d={d}mm, f={f_frame}MPa, t1/t={ratio:F2}",
            Result = $"Nvf = {Nvf:F1}N {(frameOk ? "≥" : "<")} v1={v1:F1}N → {(frameOk ? "✅ 通过" : "❌ 不通过")}" });

        // ===== Step 4: 连接片与结构连接 =====
        double v2 = V1 / req.StructureFastenersPerConnector;
        double structureCapacity;
        string structureFormula;

        if (req.StructureFastenerType == "ShootingNail")
        {
            // 射钉: fvf = 3.7×t×d×f  ……6.1.7-7[GB50018-2002]
            structureCapacity = 3.7 * req.ConnectorThickness * req.StructureFastenerDiameter * req.SteelPlateStrength;
            structureFormula = "fvf = 3.7×t×d×f  ……6.1.7-7[GB50018-2002]";
        }
        else
        {
            // 膨胀螺钉: Nvb = nv×π×d²×fvb/4
            double fvb = 265; // 8.8级
            double de = req.StructureFastenerDiameter * 0.85;
            structureCapacity = Math.PI * de * de * fvb / 4.0;
            structureFormula = "Nvb = nv×π×de²×fvb/4";
        }

        bool structureOk = structureCapacity >= v2;

        steps.Add(new() { StepNo = sn++, Name = "连接片与结构连接校核",
            Formula = structureFormula,
            Result = $"承载力 = {structureCapacity:F1}N {(structureOk ? "≥" : "<")} v2={v2:F1}N → {(structureOk ? "✅ 通过" : "❌ 不通过")}" });

        return Ok(new HardwareCalculationResult
        {
            Passed = frameOk && structureOk,
            Standard = "GB50018-2002, JGJ102-2003",
            LoadPerConnector = Math.Round(V1, 1),
            FrameConnectionCheck = new CheckResult { Passed = frameOk, ActualValue = Math.Round(v1, 1), AllowableValue = Math.Round(Nvf, 1),
                Description = $"v1={v1:F1}N ≤ Nvf={Nvf:F1}N" },
            StructureConnectionCheck = new CheckResult { Passed = structureOk, ActualValue = Math.Round(v2, 1), AllowableValue = Math.Round(structureCapacity, 1),
                Description = $"v2={v2:F1}N ≤ {structureCapacity:F1}N" },
            Steps = steps
        });
    }

    /// <summary>开启扇槽口验算（聚氨酯型材）</summary>
    /// <remarks>
    /// 按T/CECS591-2019 4.2.5条，玻纤增强聚氨酯型材为各向异性，
    /// 槽口处抗弯强度较弱，需按悬臂梁验算。
    /// M = q×B×D×a/2, W = B×t²/6, σ = M/W ≤ f
    /// </remarks>
    [HttpPost("OpeningSash")]
    [ProducesResponseType(typeof(DoorWindowCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<DoorWindowCalculationResult> CalculateOpeningSash(
        [FromBody] OpeningSashCheckRequest req)
    {
        var steps = new List<CalculationStep>();
        int sn = 1;

        double B = req.GlassLongSide;
        double D = req.GlassShortSide;
        double a = req.SlotDistance;
        double t = req.SlotThickness;
        double q = req.DesignLoad;
        double f = req.TransverseBendingStrength;

        // ===== Step 1: 槽口弯矩 =====
        double M = q * B * D * a / 2.0;
        steps.Add(new() { StepNo = sn++, Name = "槽口弯矩计算",
            Formula = "M = q×B×D×a/2",
            Process = $"M = {q}×{B}×{D}×{a}/2",
            Result = $"M = {M:F3} N·mm" });

        // ===== Step 2: 截面模量 =====
        double W = B * t * t / 6.0;
        steps.Add(new() { StepNo = sn++, Name = "截面模量计算",
            Formula = "W = B×t²/6",
            Result = $"W = {W:F3} mm³" });

        // ===== Step 3: 强度校核 =====
        double sigma = M / W;
        bool ok = sigma <= f;
        steps.Add(new() { StepNo = sn++, Name = "槽口抗弯强度校核",
            Formula = "σ = M/W ≤ f  ……T/CECS591-2019 4.2.5",
            Result = $"σ = {sigma:F3} MPa {(ok ? "≤" : ">")} f = {f} MPa → {(ok ? "✅ 通过" : "❌ 不通过")}" });

        return Ok(new DoorWindowCalculationResult
        {
            Passed = ok,
            Standard = "T/CECS591-2019, JGJ214-2010",
            StrengthCheck = new CheckResult { Passed = ok, ActualValue = Math.Round(sigma, 3), AllowableValue = f,
                Description = $"σ={sigma:F3}MPa ≤ f={f}MPa" },
            Steps = steps
        });
    }

    #region ===== 辅助方法 =====

    /// <summary>获取材料抗弯/抗拉强度设计值</summary>
    private static double GetMaterialStrength(string grade) => grade switch
    {
        "6063-T5" => 90.0,
        "6063-T6" => 150.0,
        "6061-T4" => 90.0,
        "6061-T6" => 200.0,
        "Q235" => 205.0,
        "Q355" => 310.0,
        "PU-GlassFiber" => 500.0,
        _ => 90.0
    };

    #endregion
}
