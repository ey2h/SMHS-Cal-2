using Microsoft.AspNetCore.Mvc;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

/// <summary>石材计算</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("石材计算")]
public class StoneCalculationController : ControllerBase
{
    /// <summary>石材面板计算</summary>
    [HttpPost("Panel")]
    [ProducesResponseType(typeof(StoneCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<StoneCalculationResult> CalculatePanel(
        [FromBody] StonePanelCalculationRequest req)
    {
        double a = Math.Min(req.ShortSide, req.LongSide);
        double b = Math.Max(req.ShortSide, req.LongSide);
        double t = req.Thickness;

        // 石材参数
        double density = req.Density ?? (req.StoneType switch
        {
            "Granite" => 26.5,  // kN/m³
            "Marble" => 27.0,
            "Limestone" => 25.0,
            _ => 26.5
        });

        double fb = req.FlexuralStrength ?? (req.StoneType switch
        {
            "Granite" => 10.0,   // MPa
            "Marble" => 7.0,
            "Limestone" => 5.0,
            _ => 10.0
        });

        // 安全系数 K (JGJ 133-2001)
        double K = req.StoneType switch
        {
            "Granite" => 4.0,
            "Marble" => 8.0,
            "Limestone" => 8.0,
            _ => 4.0
        };

        // 设计强度
        double fgd = fb / K;

        // 风荷载
        double Wk = req.WindLoad;
        double W = 1.4 * Wk;

        // 自重
        double G = density * t / 1000.0; // kN/m²

        // 弯矩计算
        double ratio = b / a;
        // 修正: 原4段离散值精度不足，替换为14点精细插值表(两对边简支板弯矩系数)
        double k1 = InterpolateK1(ratio);

        // 风荷载方向弯矩
        double M_wind = k1 * W * 1e-3 * a * a; // N·mm/mm (单位宽度)
        double sigma_wind = 6.0 * M_wind / (t * t); // σ = 6M/(bt²), b=1mm

        // 自重方向弯矩 (竖向简支)
        double M_gravity = 1.3 * G * 1e-3 * b * b / 8.0; // N·mm/mm (γG=1.3 per JGJ102-2003 5.4.2)
        double sigma_gravity = 6.0 * M_gravity / (t * t);

        // 取较大值
        double sigma_max = Math.Max(sigma_wind, sigma_gravity);

        // 挠度
        double E_stone = req.StoneType switch
        {
            "Granite" => 50000.0,
            "Marble" => 30000.0,
            "Limestone" => 20000.0,
            _ => 50000.0
        };
        double delta = 5.0 * Wk * 1e-3 * Math.Pow(a, 4) / (384.0 * E_stone * Math.Pow(t, 3) / 12.0);
        double deltaLimit = a / 250.0; // 石材挠度限值较严

        bool strengthOk = sigma_max <= fgd;
        bool deflectionOk = delta <= deltaLimit;

        return Ok(new StoneCalculationResult
        {
            Passed = strengthOk && deflectionOk,
            MaxBendingMoment = Math.Round(Math.Max(M_wind, M_gravity) / 1e6, 4),
            Standard = "JGJ 133-2001",
            FlexuralCheck = new CheckResult
            {
                Passed = strengthOk,
                ActualValue = Math.Round(sigma_max, 2),
                AllowableValue = Math.Round(fgd, 2),
                Description = $"σ = {sigma_max:F2} MPa ≤ fb/K = {fgd:F2} MPa (K={K})"
            },
            DeflectionCheck = new CheckResult
            {
                Passed = deflectionOk,
                ActualValue = Math.Round(delta, 2),
                AllowableValue = Math.Round(deltaLimit, 2),
                Description = $"δ = {delta:F2} mm ≤ a/250 = {deltaLimit:F2} mm"
            },
            Steps = new List<CalculationStep>
            {
                new() { StepNo = 1, Name = "石材参数",
                    Result = $"fb = {fb} MPa, ρ = {density} kN/m³, K = {K}" },
                new() { StepNo = 2, Name = "设计强度",
                    Formula = "fgd = fb / K",
                    Result = $"fgd = {fgd:F2} MPa" },
                new() { StepNo = 3, Name = "风荷载应力",
                    Result = $"σ_wind = {sigma_wind:F2} MPa" },
                new() { StepNo = 4, Name = "自重应力",
                    Result = $"σ_gravity = {sigma_gravity:F2} MPa" },
                new() { StepNo = 5, Name = "控制应力",
                    Result = $"σ_max = {sigma_max:F2} MPa" }
            }
        });
    }

    /// <summary>石材挂件计算</summary>
    [HttpPost("Bracket")]
    [ProducesResponseType(typeof(StoneCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<StoneCalculationResult> CalculateBracket(
        [FromBody] StoneBracketCalculationRequest req)
    {
        double W = req.PanelWidth / 1000.0;  // m
        double H = req.PanelHeight / 1000.0; // m
        double t = req.StoneThickness / 1000.0; // m
        int n = req.BracketCount;

        // 单个挂件承受的风荷载
        double Fw = 1.4 * req.WindLoad * W * H / n; // kN

        // 单个挂件承受的自重 (仅下部挂件承重，一般n/2个)
        double G = req.StoneDensity * W * H * t; // kN
        double Fg = 1.3 * G / (n / 2.0); // kN (下部挂件, γG=1.3 per JGJ102-2003)

        // 合力
        double F_total = Math.Sqrt(Fw * Fw + Fg * Fg);

        // 挂件承载力 (依据材质)
        double f_bracket = req.BracketMaterial switch
        {
            "SS304" => 137.0, // MPa
            "SS316" => 147.0,
            _ => 137.0
        };

        // SE挂件截面估算 (假设4mm厚, 25mm宽)
        double A_bracket = 4.0 * 25.0; // mm² (简化)
        double F_allow = f_bracket * A_bracket * 1e-3; // kN

        bool bracketOk = F_total <= F_allow;

        // 开槽处石材强度验算
        CheckResult? slotCheck = null;
        if (req.SlotDepth.HasValue && req.SlotEdgeDistance.HasValue)
        {
            double d_slot = req.SlotDepth.Value;
            double e = req.SlotEdgeDistance.Value;
            double t_eff = req.StoneThickness - d_slot; // 有效厚度
            double sigma_slot = Fg * 1e3 / (t_eff * 25.0); // 简化 (MPa)

            double fb_stone = 10.0; // 默认花岗岩
            double fgd = fb_stone / 4.0;

            slotCheck = new CheckResult
            {
                Passed = sigma_slot <= fgd,
                ActualValue = Math.Round(sigma_slot, 2),
                AllowableValue = Math.Round(fgd, 2),
                Description = $"槽口处 σ = {sigma_slot:F2} MPa ≤ {fgd:F2} MPa"
            };
        }

        return Ok(new StoneCalculationResult
        {
            Passed = bracketOk && (slotCheck?.Passed ?? true),
            MaxBendingMoment = Math.Round(F_total, 3),
            Standard = "JGJ 133-2001",
            FlexuralCheck = new CheckResult
            {
                Passed = bracketOk,
                ActualValue = Math.Round(F_total, 3),
                AllowableValue = Math.Round(F_allow, 3),
                Description = $"F = {F_total:F3} kN ≤ F_allow = {F_allow:F3} kN"
            },
            DeflectionCheck = new CheckResult
            {
                Passed = true,
                Description = "挂件挠度不作为控制条件"
            },
            BracketCheck = new CheckResult
            {
                Passed = bracketOk,
                ActualValue = Math.Round(F_total, 3),
                AllowableValue = Math.Round(F_allow, 3),
                Description = $"风荷载 Fw={Fw:F3}kN, 自重 Fg={Fg:F3}kN, 合力 F={F_total:F3}kN"
            },
            SlotCheck = slotCheck,
            Steps = new List<CalculationStep>
            {
                new() { StepNo = 1, Name = "面板自重", Result = $"G = {G:F3} kN" },
                new() { StepNo = 2, Name = "单挂件风荷载", Result = $"Fw = {Fw:F3} kN" },
                new() { StepNo = 3, Name = "单挂件自重", Result = $"Fg = {Fg:F3} kN" },
                new() { StepNo = 4, Name = "合力", Result = $"F = {F_total:F3} kN" },
                new() { StepNo = 5, Name = "挂件承载力", Result = $"F_allow = {F_allow:F3} kN ({req.BracketMaterial})" }
            }
        });
    }

    /// <summary>两对边简支板弯矩系数k1插值</summary>
    private static double InterpolateK1(double ratio)
    {
        (double r, double k)[] table = {
            (1.0, 0.0479), (1.1, 0.0554), (1.2, 0.0627), (1.3, 0.0694),
            (1.4, 0.0755), (1.5, 0.0812), (1.6, 0.0862), (1.7, 0.0908),
            (1.8, 0.0948), (1.9, 0.0985), (2.0, 0.1017), (2.5, 0.1139),
            (3.0, 0.1189), (999, 0.1250)
        };
        if (ratio <= 1.0) return table[0].k;
        for (int i = 1; i < table.Length; i++)
        {
            if (ratio <= table[i].r)
            {
                double frac = (ratio - table[i-1].r) / (table[i].r - table[i-1].r);
                return table[i-1].k + frac * (table[i].k - table[i-1].k);
            }
        }
        return 0.1250;
    }
}
