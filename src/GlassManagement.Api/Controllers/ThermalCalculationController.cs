using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

#region ===== 热工计算模型 =====

/// <summary>玻璃系统传热系数计算请求</summary>
public class GlassThermalRequest
{
    /// <summary>玻璃层配置 (从外到内, 每层厚度mm)</summary>
    [Required] public List<double> GlassThicknesses { get; set; } = new();

    /// <summary>气体间层厚度 (mm)</summary>
    public List<double> GasGapThicknesses { get; set; } = new();

    /// <summary>气体间层类型 (Air/Argon/Krypton)</summary>
    public List<string> GasTypes { get; set; } = new();

    /// <summary>各表面发射率 (从外到内), 默认普通玻璃0.837, Low-E约0.1</summary>
    public List<double> Emissivities { get; set; } = new();

    /// <summary>胶片层厚度 (mm), 夹层玻璃用</summary>
    public List<double> InterlayerThicknesses { get; set; } = new();

    /// <summary>气体间层高度 H (mm), 用于Nu计算</summary>
    public double GapHeight { get; set; } = 3000;

    /// <summary>安装倾角 θ (度), 90=竖直</summary>
    public double TiltAngle { get; set; } = 90;

    /// <summary>室外对流换热系数 hc,out W/(m²·K)</summary>
    public double HcOut { get; set; } = 16;

    /// <summary>室内对流换热系数(冬季) hc,in W/(m²·K)</summary>
    public double HcInWinter { get; set; } = 3.6;

    /// <summary>室内对流换热系数(夏季) hc,in W/(m²·K)</summary>
    public double HcInSummer { get; set; } = 2.5;
}

/// <summary>结露校核请求</summary>
public class CondensationCheckRequest
{
    /// <summary>玻璃系统传热系数 K W/(m²·K)</summary>
    [Required] public double KValue { get; set; }

    /// <summary>室内温度 (℃)</summary>
    public double IndoorTemp { get; set; } = 20;

    /// <summary>室外计算温度 te (℃)</summary>
    [Required] public double OutdoorTemp { get; set; }

    /// <summary>室内相对湿度 (%)</summary>
    public double IndoorHumidity { get; set; } = 60;

    /// <summary>室内表面换热系数 hin W/(m²·K)</summary>
    public double Hin { get; set; } = 8.0;
}

/// <summary>门窗整体K值计算请求</summary>
public class WindowOverallThermalRequest
{
    /// <summary>玻璃传热系数 Kg W/(m²·K)</summary>
    [Required] public double KGlass { get; set; }

    /// <summary>框传热系数 Kf W/(m²·K)</summary>
    [Required] public double KFrame { get; set; }

    /// <summary>玻璃面积 Ag (m²)</summary>
    [Required] public double GlassArea { get; set; }

    /// <summary>框面积 Af (m²)</summary>
    [Required] public double FrameArea { get; set; }

    /// <summary>玻璃与框线性传热系数 ψg W/(m·K)</summary>
    public double PsiGlass { get; set; } = 0.06;

    /// <summary>玻璃边缘周长 Lg (m)</summary>
    public double GlassPerimeter { get; set; }
}

/// <summary>热工计算结果</summary>
public class ThermalCalculationResult
{
    public bool Passed { get; set; }

    /// <summary>冬季传热系数 K W/(m²·K)</summary>
    public double KValueWinter { get; set; }

    /// <summary>夏季传热系数 K W/(m²·K)</summary>
    public double KValueSummer { get; set; }

    /// <summary>内表面温度 (℃)</summary>
    public double InnerSurfaceTemp { get; set; }

    /// <summary>露点温度 (℃)</summary>
    public double DewPointTemp { get; set; }

    /// <summary>结露校核</summary>
    public CheckResult? CondensationCheck { get; set; }

    /// <summary>传热系数校核</summary>
    public CheckResult? KValueCheck { get; set; }

    public string Standard { get; set; } = "JGJ/T151-2008, GB50176-2016";
    public List<CalculationStep> Steps { get; set; } = new();
}

#endregion

/// <summary>热工性能计算 - 按JGJ/T151-2008及GB50176-2016</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("热工性能计算")]
public class ThermalCalculationController : ControllerBase
{
    /// <summary>玻璃系统传热系数K值计算</summary>
    [HttpPost("GlassKValue")]
    [ProducesResponseType(typeof(ThermalCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<ThermalCalculationResult> CalculateGlassKValue(
        [FromBody] GlassThermalRequest req)
    {
        var steps = new List<CalculationStep>();
        int sn = 1;

        int nGlass = req.GlassThicknesses.Count;
        int nGap = req.GasGapThicknesses.Count;

        // 外表面发射率 (默认普通玻璃)
        double epsOut = req.Emissivities.Count > 0 ? req.Emissivities[0] : 0.837;
        double epsIn = req.Emissivities.Count > 0 ? req.Emissivities[^1] : 0.837;

        // (1) 室外表面换热系数 hout
        double hrOut = 3.9 * epsOut / 0.837;
        double hout = hrOut + req.HcOut;

        steps.Add(new() { StepNo = sn++, Name = "室外表面换热系数",
            Formula = "hout = hr,out + hc,out = 3.9×εs/0.837 + hc,out",
            Result = $"hout = {hout:F1} W/(m²·K)" });

        // (2) 室内表面换热系数 hin (冬季)
        double hrIn = 4.4 * epsIn / 0.837;
        double hinWinter = hrIn + req.HcInWinter;
        double hinSummer = hrIn + req.HcInSummer;

        steps.Add(new() { StepNo = sn++, Name = "室内表面换热系数",
            Result = $"hin(冬季) = {hinWinter:F1}, hin(夏季) = {hinSummer:F1} W/(m²·K)" });

        // (3) 固体热阻 ΣRg,i
        double sumRg = 0;
        double lambdaGlass = 0.76; // W/(m·K)
        double lambdaInterlayer = 0.16; // 胶片

        foreach (double tg in req.GlassThicknesses)
            sumRg += (tg / 1000.0) / lambdaGlass;
        foreach (double ti in req.InterlayerThicknesses)
            sumRg += (ti / 1000.0) / lambdaInterlayer;

        steps.Add(new() { StepNo = sn++, Name = "固体热阻",
            Formula = "ΣRg,i = Σ(tg,i/λg,i)",
            Result = $"ΣRg = {sumRg:F4} m²·K/W" });

        // (4) 气体间层热阻 ΣRi
        double sumRi = 0;
        double Tm = 273.0; // K, 平均温度

        for (int i = 0; i < nGap; i++)
        {
            double dg = req.GasGapThicknesses[i] / 1000.0; // m
            string gasType = i < req.GasTypes.Count ? req.GasTypes[i] : "Air";

            // 气体物性参数 (简化, 273K)
            var (lambda, rho, cp, mu) = gasType switch
            {
                "Argon" => (0.01634, 1.699, 519.0, 0.00002110),
                "Krypton" => (0.00900, 3.560, 245.0, 0.00002330),
                _ => (0.02416, 1.232, 1008.0, 0.00001720) // Air
            };

            // 辐射换热系数
            double eps1 = (i * 2 + 1) < req.Emissivities.Count ? req.Emissivities[i * 2 + 1] : 0.837;
            double eps2 = (i * 2 + 2) < req.Emissivities.Count ? req.Emissivities[i * 2 + 2] : 0.837;
            double hr = 4 * 5.67e-8 * Math.Pow(1.0 / eps1 + 1.0 / eps2 - 1, -1) * Math.Pow(Tm, 3);

            // 对流换热 - 简化Nu计算
            double H = req.GapHeight / 1000.0; // m
            double beta = 1.0 / Tm;
            double dT = 15.0; // 估算温差
            double Ra = rho * rho * Math.Pow(dg, 3) * 9.8 * beta * cp * dT / (mu * lambda);
            double Ag = H / dg;

            // θ=90° Nu计算 (JGJ/T151-2008 6.3.4-3)
            double Nu1;
            if (Ra > 5e4) Nu1 = 0.0673838 * Math.Pow(Ra, 1.0 / 3);
            else if (Ra > 1e4) Nu1 = 0.028154 * Math.Pow(Ra, 0.4134);
            else Nu1 = 1 + 1.7596678e-10 * Math.Pow(Ra, 2.2984755);

            double Nu2 = 0.242 * Math.Pow(Ra / Ag, 0.272);
            double Nu = Math.Max(Nu1, Nu2);
            if (Nu < 1) Nu = 1;

            double hc = Nu * lambda / dg;
            double Ri = 1.0 / (hc + hr);
            sumRi += Ri;

            steps.Add(new() { StepNo = sn++, Name = $"第{i + 1}层气体间层",
                Process = $"Gas={gasType}, d={dg * 1000}mm, Ra={Ra:F1}, Nu={Nu:F4}",
                Result = $"hc={hc:F2}, hr={hr:F3}, Ri={Ri:F4} m²·K/W" });
        }

        // (5) 冬季K值
        double RtWinter = 1.0 / hout + sumRi + sumRg + 1.0 / hinWinter;
        double KWinter = 1.0 / RtWinter;

        // 夏季K值
        double RtSummer = 1.0 / hout + sumRi + sumRg + 1.0 / hinSummer;
        double KSummer = 1.0 / RtSummer;

        steps.Add(new() { StepNo = sn++, Name = "传热系数K值",
            Formula = "K = 1/Rt = 1/(1/hout + ΣRi + ΣRg + 1/hin)",
            Process = $"Rt(冬)={RtWinter:F4}, Rt(夏)={RtSummer:F4}",
            Result = $"K(冬季)={KWinter:F2} W/(m²·K), K(夏季)={KSummer:F2} W/(m²·K)" });

        return Ok(new ThermalCalculationResult
        {
            Passed = true,
            KValueWinter = Math.Round(KWinter, 2),
            KValueSummer = Math.Round(KSummer, 2),
            Standard = "JGJ/T151-2008",
            Steps = steps
        });
    }

    /// <summary>结露校核</summary>
    [HttpPost("CondensationCheck")]
    [ProducesResponseType(typeof(ThermalCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<ThermalCalculationResult> CheckCondensation(
        [FromBody] CondensationCheckRequest req)
    {
        var steps = new List<CalculationStep>();
        int sn = 1;

        double ti = req.IndoorTemp;
        double te = req.OutdoorTemp;
        double K = req.KValue;
        double hin = req.Hin;
        double rh = req.IndoorHumidity;

        // 内表面温度 θsi = ti - K×(ti-te)/hin
        double thetaSi = ti - K * (ti - te) / hin;

        steps.Add(new() { StepNo = sn++, Name = "内表面温度",
            Formula = "θsi = ti - K×(ti-te)/hin",
            Process = $"θsi = {ti} - {K}×({ti}-({te}))/{hin}",
            Result = $"θsi = {thetaSi:F1} ℃" });

        // 露点温度 (Magnus公式简化)
        double a = 17.27, b = 237.7;
        double gamma = a * ti / (b + ti) + Math.Log(rh / 100.0);
        double tdew = b * gamma / (a - gamma);

        steps.Add(new() { StepNo = sn++, Name = "露点温度",
            Formula = "Magnus公式",
            Process = $"ti={ti}℃, RH={rh}%",
            Result = $"tdew = {tdew:F1} ℃" });

        bool passed = thetaSi > tdew;

        steps.Add(new() { StepNo = sn++, Name = "结露校核",
            Formula = "θsi > tdew",
            Result = $"{thetaSi:F1}℃ {(passed ? ">" : "≤")} {tdew:F1}℃ → {(passed ? "✅ 不结露" : "❌ 结露")}" });

        return Ok(new ThermalCalculationResult
        {
            Passed = passed,
            InnerSurfaceTemp = Math.Round(thetaSi, 1),
            DewPointTemp = Math.Round(tdew, 1),
            CondensationCheck = new CheckResult
            {
                Passed = passed,
                ActualValue = Math.Round(thetaSi, 1),
                AllowableValue = Math.Round(tdew, 1),
                Description = $"内表面温度{thetaSi:F1}℃ {(passed ? ">" : "≤")} 露点{tdew:F1}℃"
            },
            Standard = "GB50176-2016",
            Steps = steps
        });
    }

    /// <summary>门窗整体传热系数K值</summary>
    [HttpPost("WindowOverallK")]
    [ProducesResponseType(typeof(ThermalCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<ThermalCalculationResult> CalculateWindowOverallK(
        [FromBody] WindowOverallThermalRequest req)
    {
        var steps = new List<CalculationStep>();
        int sn = 1;

        double Ag = req.GlassArea;
        double Af = req.FrameArea;
        double Kg = req.KGlass;
        double Kf = req.KFrame;
        double psi = req.PsiGlass;
        double Lg = req.GlassPerimeter;
        double At = Ag + Af;

        // Kw = (Kg×Ag + Kf×Af + ψg×Lg) / At
        double Kw = (Kg * Ag + Kf * Af + psi * Lg) / At;

        steps.Add(new() { StepNo = sn++, Name = "门窗整体K值",
            Formula = "Kw = (Kg×Ag + Kf×Af + ψg×Lg) / (Ag+Af)",
            Process = $"Kw = ({Kg}×{Ag} + {Kf}×{Af} + {psi}×{Lg}) / {At}",
            Result = $"Kw = {Kw:F2} W/(m²·K)" });

        return Ok(new ThermalCalculationResult
        {
            Passed = true,
            KValueWinter = Math.Round(Kw, 2),
            Standard = "JGJ/T151-2008",
            Steps = steps
        });
    }
}
