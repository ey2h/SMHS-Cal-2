using Microsoft.AspNetCore.Mvc;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

/// <summary>结构计算（预埋件 / 结构胶）- 按JGJ102-2003附录C</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("结构计算")]
public class StructureCalculationController : ControllerBase
{
    /// <summary>预埋件计算 (JGJ102-2003 附录C)</summary>
    [HttpPost("EmbedPart")]
    [ProducesResponseType(typeof(EmbedPartCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<EmbedPartCalculationResult> CalculateEmbedPart(
        [FromBody] EmbedPartCalculationRequest req)
    {
        var steps = new List<CalculationStep>();
        int sn = 1;

        double d = req.AnchorDiameter;
        int n = req.AnchorCount;
        double As = n * Math.PI * d * d / 4.0; // 锚筋总截面面积 mm²
        double t = req.PlateThickness;
        double z = req.AnchorSpacing; // 最外层锚筋中心线间距

        // 钢筋强度 (但不大于300MPa用于预埋件计算)
        double fy_raw = req.AnchorMaterial switch
        {
            "HPB300" => 270.0,
            "HRB400" => 360.0,
            "HRB500" => 435.0,
            _ => 270.0
        };
        double fy = Math.Min(fy_raw, 300.0); // 预埋件计算中fy不大于300

        // 混凝土参数
        double fc = GetConcreteCompressiveStrength(req.ConcreteGrade);
        double ft = GetConcreteTensileStrength(req.ConcreteGrade);

        steps.Add(new() { StepNo = sn++, Name = "材料参数",
            Result = $"{n}×φ{d}, As={As:F2}mm², fy={fy}MPa, fc={fc}MPa, ft={ft}MPa" });

        // 系数计算 (JGJ102-2003 附录C)
        // ar: 钢筋层数影响系数
        double ar = req.RebarLayers switch { 2 => 1.0, 3 => 0.9, 4 => 0.85, _ => 1.0 };

        // av: 钢筋受剪承载力系数 (C.0.1-5)
        double av = (4.0 - 0.08 * d) * Math.Sqrt(fc / fy);
        av = Math.Min(av, 0.7);

        // ab: 锚板弯曲变形折减系数 (C.0.1-6)
        double ab = 0.6 + 0.25 * t / d;

        steps.Add(new() { StepNo = sn++, Name = "系数计算",
            Formula = "ar按层数, av=(4-0.08d)×√(fc/fy)≤0.7, ab=0.6+0.25t/d",
            Process = $"av=(4-0.08×{d})×√({fc}/{fy}), ab=0.6+0.25×{t}/{d}",
            Result = $"ar={ar}, av={av:F3}, ab={ab:F3}" });

        double V = req.ShearForce;
        double N = req.TensionForce;
        double M = req.BendingMoment;

        // 校核条件1: AS ≥ V/(ar×av×fy) + N/(0.8×ab×fy) + M/(1.3×ar×ab×fy×z)  (C.0.1-1)
        double As_req1 = V / (ar * av * fy) + N / (0.8 * ab * fy) + M / (1.3 * ar * ab * fy * z);

        // 校核条件2: AS ≥ N/(0.8×ab×fy) + M/(0.4×ar×ab×fy×z)  (C.0.1-2)
        double As_req2 = N / (0.8 * ab * fy) + M / (0.4 * ar * ab * fy * z);

        double As_required = Math.Max(As_req1, As_req2);
        bool anchorOk = As >= As_required;

        steps.Add(new() { StepNo = sn++, Name = "锚筋面积校核条件1 (C.0.1-1)",
            Formula = "AS ≥ V/(ar×av×fy) + N/(0.8×ab×fy) + M/(1.3×ar×ab×fy×z)",
            Process = $"{V:F1}/({ar}×{av:F3}×{fy}) + {N:F1}/(0.8×{ab:F3}×{fy}) + {M:F1}/(1.3×{ar}×{ab:F3}×{fy}×{z})",
            Result = $"AS,req1 = {As_req1:F3} mm² ≤ AS = {As:F2} mm²" });

        steps.Add(new() { StepNo = sn++, Name = "锚筋面积校核条件2 (C.0.1-2)",
            Formula = "AS ≥ N/(0.8×ab×fy) + M/(0.4×ar×ab×fy×z)",
            Process = $"{N:F1}/(0.8×{ab:F3}×{fy}) + {M:F1}/(0.4×{ar}×{ab:F3}×{fy}×{z})",
            Result = $"AS,req2 = {As_req2:F3} mm² ≤ AS = {As:F2} mm²" });

        // 锚板面积校核: 0.5×fc×A ≥ N
        double plateArea = req.PlateLength * req.PlateWidth;
        double plateCapacity = 0.5 * fc * plateArea;
        bool plateOk = plateCapacity >= N;

        steps.Add(new() { StepNo = sn++, Name = "锚板面积校核",
            Formula = "0.5×fc×A ≥ N",
            Process = $"0.5×{fc}×{plateArea:F0} = {plateCapacity:F1}N",
            Result = $"{plateCapacity:F1} N {(plateOk ? "≥" : "<")} {N:F1} N → {(plateOk ? "✅ 满足" : "❌ 不满足")}" });

        // 锚筋长度计算: lab = k×α×(fy/ft)×d
        double k = req.HasSeismicDesign ? 1.1 : 1.0;
        double alpha_anchor = req.AnchorShape == "光圆筋" ? 0.16 : 0.14;
        double lab = k * alpha_anchor * (fy_raw / ft) * d;

        // 实际锚固长度修正: la = ζa × lab
        double zeta_a = As_required / As; // 设计面积/实际面积
        zeta_a = Math.Max(zeta_a, 0.25);
        double la = zeta_a * lab;
        la = Math.Max(la, 15 * d); // 不小于15d

        bool lengthOk = req.AnchorLength >= la;

        steps.Add(new() { StepNo = sn++, Name = "锚筋长度计算",
            Formula = "lab = k×α×(fy/ft)×d, la = ζa×lab ≥ 15d",
            Process = $"lab = {k}×{alpha_anchor}×({fy_raw}/{ft})×{d} = {lab:F1}mm, ζa = {zeta_a:F3}",
            Result = $"la = {la:F1} mm, 实际 = {req.AnchorLength} mm → {(lengthOk ? "✅ 满足" : "❌ 不满足")}" });

        bool allPassed = anchorOk && plateOk && lengthOk;

        return Ok(new EmbedPartCalculationResult
        {
            Passed = allPassed,
            Standard = "JGJ 102-2003 附录C",
            TensionCheck = new CheckResult
            {
                Passed = anchorOk,
                ActualValue = Math.Round(As_required, 3),
                AllowableValue = Math.Round(As, 2),
                Description = $"AS,req = {As_required:F3} mm² ≤ AS = {As:F2} mm²"
            },
            ShearCheck = new CheckResult
            {
                Passed = anchorOk,
                ActualValue = Math.Round(As_req1, 3),
                AllowableValue = Math.Round(As, 2),
                Description = $"条件1: {As_req1:F3} mm², 条件2: {As_req2:F3} mm²"
            },
            CombinedCheck = new CheckResult
            {
                Passed = anchorOk,
                ActualValue = Math.Round(As_required, 3),
                AllowableValue = Math.Round(As, 2),
                Description = $"max(条件1,条件2) = {As_required:F3} mm² ≤ {As:F2} mm²"
            },
            PlateBendingCheck = new CheckResult
            {
                Passed = plateOk,
                ActualValue = Math.Round(N, 1),
                AllowableValue = Math.Round(plateCapacity, 1),
                Description = $"N = {N:F1}N ≤ 0.5fcA = {plateCapacity:F1}N"
            },
            AnchorLengthCheck = new CheckResult
            {
                Passed = lengthOk,
                ActualValue = Math.Round(la, 1),
                AllowableValue = req.AnchorLength,
                Description = $"la = {la:F1}mm ≤ 实际 {req.AnchorLength}mm"
            },
            Steps = steps
        });
    }

    /// <summary>结构胶计算 (含宽度和厚度) - JGJ102-2003 5.6节</summary>
    [HttpPost("Silicone")]
    [ProducesResponseType(typeof(SiliconeCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<SiliconeCalculationResult> CalculateSilicone(
        [FromBody] SiliconeCalculationRequest req)
    {
        var steps = new List<CalculationStep>();
        int sn = 1;

        double a = req.BondLengthShort; // 短边
        double b = req.BondLengthLong;  // 长边
        double cs = req.SealantWidth;
        double ts = req.SealantThickness;
        double wk = req.WindLoad * 0.001; // kN/m² → MPa
        double qEAk = req.SeismicLoad;
        double f1 = req.ShortTermStrength;
        double f2 = req.LongTermStrength;

        // === 结构胶宽度计算 ===

        // (1) 水平荷载下宽度 Cs1 = (1.5wk + 0.5×1.4×qEAk)×a/(2×f1)  (5.6.3-2)
        double Cs1 = (1.5 * wk + 0.5 * 1.4 * qEAk) * a / (2 * f1);

        steps.Add(new() { StepNo = sn++, Name = "风荷载+地震作用下胶缝宽度",
            Formula = "Cs1 = (1.5wk+0.5×1.4×qEAk)×a/(2f1)  ……5.6.3-2[JGJ102-2003]",
            Process = $"Cs1 = (1.5×{wk}+0.5×1.4×{qEAk})×{a}/(2×{f1})",
            Result = $"Cs1 = {Cs1:F3} mm" });

        // (2) 自重下宽度 Cs2 = qG×a×b/(2(a+b)×f2)  (5.6.3-3)
        double qG = 1.3 * req.GlassWeight; // 自重设计值
        double Cs2 = qG * a * b / (2 * (a + b) * f2);

        steps.Add(new() { StepNo = sn++, Name = "自重作用下胶缝宽度",
            Formula = "Cs2 = qG×a×b/(2(a+b)×f2)  ……5.6.3-3[JGJ102-2003]",
            Process = $"qG = 1.3×{req.GlassWeight} = {qG:F6}MPa",
            Result = $"Cs2 = {Cs2:F3} mm" });

        double csRequired = Math.Max(Math.Max(Cs1, Cs2), 7.0);
        bool widthOk = cs >= csRequired;

        steps.Add(new() { StepNo = sn++, Name = "胶缝宽度汇总",
            Formula = "cs ≥ max(Cs1, Cs2, 7mm)",
            Result = $"cs,req = {csRequired:F3} mm, 实际 cs = {cs} mm → {(widthOk ? "✅" : "❌")}" });

        // === 结构胶厚度计算 ===

        // (1) 温度作用下厚度 (5.6.5)
        double us1 = b * req.TemperatureDifference * (req.AluminumExpansionCoeff - req.GlassExpansionCoeff);
        double delta1 = req.ThermalDeformCapacity;
        double ts1 = us1 / Math.Sqrt(delta1 * (2 + delta1));

        steps.Add(new() { StepNo = sn++, Name = "温度作用下厚度",
            Formula = "us1 = b×Δt×(α1-α2), ts1 = us1/√(δ1×(2+δ1))",
            Process = $"us1 = {b}×{req.TemperatureDifference}×({req.AluminumExpansionCoeff}-{req.GlassExpansionCoeff}) = {us1:F3}mm",
            Result = $"ts1 = {ts1:F3} mm" });

        // (2) 风荷载作用下厚度 (5.6.5-2)
        double theta = req.InterStoryDriftAngle;
        double hg = b; // 玻璃面板高度
        double us2 = theta * hg;
        double delta2 = req.WindDeformCapacity;
        double ts2 = us2 / Math.Sqrt(delta2 * (2 + delta2));

        steps.Add(new() { StepNo = sn++, Name = "风荷载作用下厚度",
            Formula = "us2 = θ×hg, ts2 = us2/√(δ2×(2+δ2))  ……5.6.5[JGJ102-2003]",
            Process = $"θ = {theta:F6}, hg = {hg}mm, us2 = {us2:F3}mm",
            Result = $"ts2 = {ts2:F3} mm" });

        double tsRequired = Math.Max(Math.Max(ts1, ts2), 6.0);
        tsRequired = Math.Min(tsRequired, 12.0); // 不大于12mm
        bool thicknessOk = ts >= tsRequired && ts >= 6 && ts <= 12;

        steps.Add(new() { StepNo = sn++, Name = "胶缝厚度汇总",
            Formula = "6mm ≤ ts ≤ 12mm",
            Result = $"ts,req = {tsRequired:F3} mm, 实际 ts = {ts} mm → {(thicknessOk ? "✅" : "❌")}" });

        // 宽厚比: cs > ts 且 cs ≤ 2ts
        double aspectRatio = cs / ts;
        bool aspectOk = cs >= ts && cs <= 2 * ts;

        steps.Add(new() { StepNo = sn++, Name = "宽厚比校核",
            Formula = "cs ≥ ts 且 cs ≤ 2ts (5.6.1[JGJ102-2003])",
            Result = $"cs/ts = {aspectRatio:F2}, {(aspectOk ? "✅ 满足" : "❌ 不满足")}" });

        // 风荷载拉力验算
        double sigma_w = (1.5 * wk + 0.5 * 1.4 * qEAk) * a / (2 * cs);
        bool windOk = sigma_w <= f1;

        // 自重剪力验算
        double tau_g = qG * a * b / (2 * (a + b) * cs);
        bool gravityOk = tau_g <= f2;

        return Ok(new SiliconeCalculationResult
        {
            Passed = widthOk && thicknessOk && aspectOk && windOk && gravityOk,
            RequiredMinWidth = Math.Round(csRequired, 3),
            RequiredMinThickness = Math.Round(tsRequired, 3),
            Standard = "JGJ 102-2003",
            WindTensionCheck = new CheckResult
            {
                Passed = windOk, ActualValue = Math.Round(sigma_w, 4), AllowableValue = f1,
                Description = $"σw = {sigma_w:F4} MPa ≤ f1 = {f1} MPa"
            },
            GravityShearCheck = new CheckResult
            {
                Passed = gravityOk, ActualValue = Math.Round(tau_g, 4), AllowableValue = f2,
                Description = $"τg = {tau_g:F4} MPa ≤ f2 = {f2} MPa"
            },
            WidthCheck = new CheckResult
            {
                Passed = widthOk, ActualValue = cs, AllowableValue = Math.Round(csRequired, 3),
                Description = $"cs = {cs}mm ≥ cs,req = {csRequired:F3}mm (且≥7mm)"
            },
            ThicknessCheck = new CheckResult
            {
                Passed = thicknessOk, ActualValue = ts, AllowableValue = Math.Round(tsRequired, 3),
                Description = $"ts = {ts}mm, 6mm ≤ ts ≤ 12mm, ts,req = {tsRequired:F3}mm"
            },
            AspectRatioCheck = new CheckResult
            {
                Passed = aspectOk, ActualValue = Math.Round(aspectRatio, 2), AllowableValue = 2.0,
                Description = $"cs/ts = {aspectRatio:F2}, 要求 1 ≤ cs/ts ≤ 2"
            },
            Steps = steps
        });
    }

    #region 辅助方法

    /// <summary>混凝土轴心抗压强度设计值 fc (MPa)</summary>
    private static double GetConcreteCompressiveStrength(string grade) => grade switch
    {
        "C20" => 9.6, "C25" => 11.9, "C30" => 14.3,
        "C35" => 16.7, "C40" => 19.1, "C45" => 21.1,
        "C50" => 23.1, _ => 14.3
    };

    /// <summary>混凝土轴心抗拉强度设计值 ft (MPa)</summary>
    private static double GetConcreteTensileStrength(string grade) => grade switch
    {
        "C20" => 1.10, "C25" => 1.27, "C30" => 1.43,
        "C35" => 1.57, "C40" => 1.71, "C45" => 1.80,
        "C50" => 1.89, _ => 1.43
    };

    #endregion
}
