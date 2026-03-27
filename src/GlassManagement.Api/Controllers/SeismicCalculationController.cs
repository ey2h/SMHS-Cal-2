using Microsoft.AspNetCore.Mvc;
using GlassManagement.Core.Models;

namespace GlassManagement.Api.Controllers;

/// <summary>地震计算 - 按JGJ102-2003 5.3-5.4节</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("地震计算")]
public class SeismicCalculationController : ControllerBase
{
    /// <summary>地震作用及效应组合计算</summary>
    [HttpPost("Calculate")]
    [ProducesResponseType(typeof(SeismicCalculationResult), StatusCodes.Status200OK)]
    public ActionResult<SeismicCalculationResult> Calculate(
        [FromBody] SeismicCalculationRequest req)
    {
        var steps = new List<CalculationStep>();
        int sn = 1;

        // (1) 垂直于幕墙平面的分布水平地震作用标准值
        // qEk = βE×αmax×Gk/A  (5.3.4[JGJ102-2003])
        double qEk = req.BetaE * req.AlphaMax * req.GravityLoad / req.Area;

        steps.Add(new() { StepNo = sn++, Name = "垂直于幕墙平面的分布水平地震作用",
            Formula = "qEk = βE×αmax×Gk/A  ……5.3.4[JGJ102-2003]",
            Process = $"qEk = {req.BetaE}×{req.AlphaMax}×{req.GravityLoad}/{req.Area}",
            Result = $"qEk = {qEk:F6} MPa" });

        // (2) 平行于幕墙平面的集中水平地震作用标准值
        // PEk = βE×αmax×Gk  (5.3.5[JGJ102-2003])
        double PEk = req.BetaE * req.AlphaMax * req.GravityLoad;

        steps.Add(new() { StepNo = sn++, Name = "平行于幕墙平面的集中水平地震作用",
            Formula = "PEk = βE×αmax×Gk  ……5.3.5[JGJ102-2003]",
            Process = $"PEk = {req.BetaE}×{req.AlphaMax}×{req.GravityLoad}",
            Result = $"PEk = {PEk:F3} N" });

        // (3) 效应组合 S = γG×SGk + ψw×γw×Swk + ψE×γE×SEk  (5.4.1[JGJ102-2003])
        // 量纲说明: qEk(MPa=N/mm²)×Area(mm²)=SEk(N), SGk/Swk也应为力(N)，各项量纲一致
        double SEk = qEk * req.Area; // 地震效应标准值(N) = 面荷载(MPa) × 面积(mm²)

        // 强度计算组合 (各项均为力N: SGk/Swk/SEk需保持量纲一致)
        double S_strength = req.GammaG * req.SGk + req.PsiW * req.GammaW * req.Swk
                          + req.PsiE * req.GammaE * SEk;

        // 挠度计算组合 (γG=1.0, γw=1.0, 不考虑地震)
        double S_deflection = 1.0 * req.SGk + 1.0 * req.Swk;

        steps.Add(new() { StepNo = sn++, Name = "作用效应组合 (强度)",
            Formula = "S = γG×SGk + ψw×γw×Swk + ψE×γE×SEk  ……5.4.1[JGJ102-2003]",
            Process = $"S = {req.GammaG}×{req.SGk} + {req.PsiW}×{req.GammaW}×{req.Swk} + {req.PsiE}×{req.GammaE}×{SEk:F3}",
            Result = $"S = {S_strength:F3}" });

        steps.Add(new() { StepNo = sn++, Name = "分项系数说明",
            Formula = "强度: γG=1.3, γw=1.5, γE=1.4; 挠度: γG=1.0, γw=1.0, 不考虑地震",
            Process = $"ψw=1.0, ψE=0.5",
            Result = "按JGJ102-2003 5.4.2~5.4.4" });

        steps.Add(new() { StepNo = sn++, Name = "作用效应组合 (挠度)",
            Formula = "S = SGk + Swk (不考虑地震)",
            Result = $"S = {S_deflection:F3}" });

        return Ok(new SeismicCalculationResult
        {
            QEk = Math.Round(qEk, 6),
            PEk = Math.Round(PEk, 3),
            SEk = Math.Round(SEk, 3),
            StrengthCombination = Math.Round(S_strength, 3),
            DeflectionCombination = Math.Round(S_deflection, 3),
            Standard = "JGJ 102-2003",
            Steps = steps
        });
    }
}
