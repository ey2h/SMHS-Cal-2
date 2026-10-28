using Microsoft.AspNetCore.Mvc;
using GlassManagement.Api.Data;
using GlassManagement.Core.Models;
using GlassManagement.Calc;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Api.Controllers;

#region ===== 请求/响应模型 =====

/// <summary>风荷载计算请求</summary>
public class WindLoadRequest
{
    /// <summary>地区名称 (可选, 用于自动查w0和αmax)</summary>
    public string? RegionName { get; set; }
    /// <summary>基本风压 w0 (MPa), 若给地区名称则自动取值</summary>
    public double? W0 { get; set; }
    /// <summary>重现期: 50 或 100</summary>
    public int ReturnPeriod { get; set; } = 50;
    /// <summary>计算点标高 z (m)</summary>
    public double Height { get; set; } = 100;
    /// <summary>地面粗糙度类别 A/B/C/D</summary>
    public string TerrainCategory { get; set; } = "C";
    /// <summary>建筑类型: Closed=封闭, Open=开洞</summary>
    public string BuildingType { get; set; } = "Closed";
    /// <summary>开洞率 (仅开洞时有效, 0~1)</summary>
    public double OpeningRatio { get; set; } = 0;
    /// <summary>构件从属面积 (m²) - 用于支承结构体型系数折减</summary>
    public double StructureArea { get; set; } = 4.95;
    /// <summary>墙面局部体型系数 μs1(1), 默认1.0</summary>
    public double ShapeCoefficient { get; set; } = 1.0;
    /// <summary>地形修正系数 K1, 默认1.0</summary>
    public double K1 { get; set; } = 1.0;
    /// <summary>风向影响系数 K2, 默认1.0</summary>
    public double K2 { get; set; } = 1.0;
}

/// <summary>风荷载计算结果</summary>
public class WindLoadResponse
{
    public double MuZ { get; set; }
    public double BetaGz { get; set; }
    public double Beta { get; set; }
    public double Mus1Structure { get; set; }
    public double Mus1Panel { get; set; }
    public double WkStructure { get; set; }
    public double WkPanel { get; set; }
    public double W0 { get; set; }
    public string Standard { get; set; } = "GB50009-2012, GB55001-2021";
    public List<CalculationStep> Steps { get; set; } = new();
}

/// <summary>地震作用计算请求</summary>
public class SeismicRequest
{
    /// <summary>地区名称</summary>
    public string? RegionName { get; set; }
    /// <summary>αmax 水平地震影响系数最大值</summary>
    public double? AlphaMax { get; set; }
    /// <summary>动力放大系数 βE, 默认5.0</summary>
    public double BetaE { get; set; } = 5.0;
    /// <summary>场地类型 I/II/III/IV</summary>
    public string SiteCategory { get; set; } = "II";
    /// <summary>场地地震动峰值加速度调整系数 Fa</summary>
    public double Fa { get; set; } = 1.0;
}

/// <summary>地震作用计算结果</summary>
public class SeismicResponse
{
    public double AlphaMax { get; set; }
    public double BetaE { get; set; }
    public string Formula { get; set; } = "";
    public string Standard { get; set; } = "JGJ102-2003, GB55002-2021";
    public List<CalculationStep> Steps { get; set; } = new();
}

/// <summary>作用效应组合请求</summary>
public class LoadCombinationRequest
{
    /// <summary>支承结构风荷载标准值 wk (MPa)</summary>
    public double WkStructure { get; set; }
    /// <summary>面板风荷载标准值 wk (MPa)</summary>
    public double WkPanel { get; set; }
    /// <summary>地震作用标准值 qEAk (MPa)</summary>
    public double QEAk { get; set; }
    /// <summary>重力荷载标准值 SGk (取决于具体构件)</summary>
    public double SGk { get; set; }

    /// <summary>项目规范名称（可空）。用于按 guifan.dat 取规范相关的 γE，如 "玻璃幕墙规范JGJ102-2003"、"广东地方标准"。为空时回退 JGJ102 默认。</summary>
    public string? StandardName { get; set; }
}

/// <summary>作用效应组合结果</summary>
public class LoadCombinationResponse
{
    /// <summary>强度计算组合 - 支承结构 (MPa)</summary>
    public double StrengthStructure { get; set; }
    /// <summary>强度计算组合 - 面板 (MPa)</summary>
    public double StrengthPanel { get; set; }
    /// <summary>挠度计算组合 - 支承结构 (MPa)</summary>
    public double DeflectionStructure { get; set; }
    /// <summary>挠度计算组合 - 面板 (MPa)</summary>
    public double DeflectionPanel { get; set; }
    public string Standard { get; set; } = "JGJ102-2003";
    public List<CalculationStep> Steps { get; set; } = new();
}

#endregion

/// <summary>项目参数 - 地区选择、风荷载、地震、荷载组合</summary>
/// <remarks>
/// 按计算书第2章"基本参数"和"幕墙承受荷载计算"完整流程。
/// 规范依据: GB50009-2012, GB55001-2021, JGJ102-2003, GB55002-2021, GB18306-2015
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Tags("项目参数")]
public class ProjectParameterController : ControllerBase
{
    private readonly RegionDataService _regionService;
    // 原程序 guifan.dat 驱动的荷载组合引擎（内嵌系数表，无需依赖原程序安装目录）
    private readonly CalcService _calc = CalcService.CreateDefault();

    public ProjectParameterController(RegionDataService regionService)
    {
        _regionService = regionService;
    }

    /// <summary>获取所有地区参数</summary>
    /// <remarks>返回634个城市的基本风压、抗震参数、雪压等数据</remarks>
    [HttpGet("Regions")]
    [ProducesResponseType(typeof(List<RegionData>), StatusCodes.Status200OK)]
    public ActionResult<List<RegionData>> GetRegions([FromQuery] string? keyword = null)
    {
        if (!string.IsNullOrWhiteSpace(keyword))
            return Ok(_regionService.Search(keyword));
        return Ok(_regionService.GetAll());
    }

    /// <summary>获取单个地区参数</summary>
    [HttpGet("Regions/{name}")]
    [ProducesResponseType(typeof(RegionData), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<RegionData> GetRegion(string name)
    {
        var region = _regionService.GetByName(name);
        if (region == null) return NotFound($"未找到地区: {name}");
        return Ok(region);
    }

    /// <summary>风荷载计算</summary>
    /// <remarks>
    /// wk = β×K1×K2×μz×μs1×w0 (GB55001-2021 4.6.1)
    /// 
    /// 完整计算流程:
    /// 1. μz 风压高度变化系数 (精确公式)
    /// 2. βgz 阵风系数 = 1+2g×I10×(z/10)^(-α)
    /// 3. β = max(βgz, 1+0.7/√μz)
    /// 4. μs1 局部风压体型系数 (含面积折减和内压系数)
    /// 5. wk = β×K1×K2×μz×μs1×w0
    /// </remarks>
    [HttpPost("WindLoad")]
    [ProducesResponseType(typeof(WindLoadResponse), StatusCodes.Status200OK)]
    public ActionResult<WindLoadResponse> CalculateWindLoad([FromBody] WindLoadRequest req)
    {
        var steps = new List<CalculationStep>();
        int sn = 1;

        // 确定 w0
        double w0;
        if (req.W0.HasValue)
        {
            w0 = req.W0.Value;
        }
        else if (!string.IsNullOrEmpty(req.RegionName))
        {
            var region = _regionService.GetByName(req.RegionName);
            if (region == null) return NotFound($"未找到地区: {req.RegionName}");
            // CSV中风压单位为Pa(N/m²), 转为MPa: ÷1e6
            double wp = req.ReturnPeriod == 100 ? region.WindPressure100 : region.WindPressure50;
            w0 = wp / 1e6; // Pa → MPa
            steps.Add(new() { StepNo = sn++, Name = "基本风压",
                Formula = $"查地区参数表 ({req.RegionName}, {req.ReturnPeriod}年重现期)",
                Result = $"w0 = {wp} Pa = {w0:F6} MPa (不小于0.3kN/m²=0.0003MPa)" });
            w0 = Math.Max(w0, 0.0003); // 不小于0.3kN/m²
        }
        else
        {
            return BadRequest("请提供地区名称或基本风压w0");
        }

        double z = req.Height;
        string cat = req.TerrainCategory.ToUpper();

        // (1) 风压高度变化系数 μz - 精确公式
        // GB50009-2012 条文说明8.2.1
        var (zMin, zMax, coeff, exp) = cat switch
        {
            "A" => (5.0, 300.0, 1.284, 0.24),
            "B" => (10.0, 350.0, 1.000, 0.30),
            "C" => (15.0, 450.0, 0.544, 0.44),
            "D" => (30.0, 550.0, 0.262, 0.60),
            _ => (15.0, 450.0, 0.544, 0.44)
        };

        double zCalc = Math.Max(Math.Min(z, zMax), zMin);
        double muZ = coeff * Math.Pow(zCalc / 10.0, exp);

        steps.Add(new() { StepNo = sn++, Name = "风压高度变化系数μz",
            Formula = $"μz = {coeff}×(z/10)^{exp} ({cat}类场地, 条文说明8.2.1[GB50009-2012])",
            Process = $"z = {z}m, 截断范围[{zMin},{zMax}]m, z_calc = {zCalc}m",
            Result = $"μz = {coeff}×({zCalc}/10)^{exp} = {muZ:F4}" });

        // (2) 阵风系数 βgz
        // βgz = 1 + 2g×I10×(z/10)^(-α)  条文说明8.6.1[GB50009-2012]
        double g = 2.5;
        var (I10, alpha) = cat switch
        {
            "A" => (0.12, 0.12),
            "B" => (0.14, 0.15),
            "C" => (0.23, 0.22),
            "D" => (0.39, 0.30),
            _ => (0.23, 0.22)
        };

        double betaGz = 1 + 2 * g * I10 * Math.Pow(zCalc / 10.0, -alpha);
        double betaAlt = 1 + 0.7 / Math.Sqrt(muZ);
        double beta = Math.Max(betaGz, betaAlt);

        steps.Add(new() { StepNo = sn++, Name = "阵风系数β",
            Formula = "βgz = 1+2g×I10×(z/10)^(-α), β = max(βgz, 1+0.7/√μz)  条文说明8.6.1[GB50009-2012]",
            Process = $"g={g}, I10={I10}, α={alpha}\nβgz = 1+2×{g}×{I10}×({zCalc}/10)^(-{alpha}) = {betaGz:F4}\n1+0.7/√{muZ:F4} = {betaAlt:F4}",
            Result = $"β = max({betaGz:F4}, {betaAlt:F4}) = {beta:F4}" });

        // (3) 局部风压体型系数 μs1
        double mus1_1 = req.ShapeCoefficient; // μs1(1)

        // 面板: μs1 = μs1(1)
        double mus1_panel = mus1_1;

        // 支承结构: 按从属面积折减 8.3.4[GB50009-2012]
        double A_struct = req.StructureArea;
        double mus1_struct;
        if (A_struct <= 1.0)
        {
            mus1_struct = mus1_1;
        }
        else if (A_struct >= 25.0)
        {
            mus1_struct = 0.8 * mus1_1;
        }
        else
        {
            // 对数插值 μs1(A) = μs1(1) + [μs1(25)-μs1(1)]×logA/1.4  ……8.3.4[GB50009-2012]
            double mus1_25 = 0.8 * mus1_1;
            double logA = Math.Log10(A_struct);
            mus1_struct = mus1_1 + (mus1_25 - mus1_1) * logA / 1.4;
        }

        steps.Add(new() { StepNo = sn++, Name = "局部风压体型系数(折减前)",
            Formula = "μs1(A) = μs1(1)+[μs1(25)-μs1(1)]×logA/1.4  ……8.3.4[GB50009-2012]",
            Process = $"μs1(1) = {mus1_1}, 从属面积A = {A_struct:F2}m², μs1(25) = {0.8 * mus1_1}",
            Result = $"支承结构: μs1 = {mus1_struct:F4}, 面板: μs1 = {mus1_panel}" });

        // 内压系数 8.3.5[GB50009-2012]
        double internalCoeff;
        if (req.BuildingType == "Closed")
        {
            internalCoeff = 0.2;
        }
        else
        {
            double openRatio = req.OpeningRatio;
            if (openRatio > 0.30) internalCoeff = 0.8 * mus1_1;
            else if (openRatio > 0.10) internalCoeff = 0.6 * mus1_1;
            else if (openRatio > 0.02) internalCoeff = 0.4 * mus1_1;
            else internalCoeff = 0.2;
        }

        double mus1_struct_final = mus1_struct + internalCoeff;
        double mus1_panel_final = mus1_panel + internalCoeff;

        steps.Add(new() { StepNo = sn++, Name = "含内压系数的体型系数",
            Formula = $"内压系数 = {internalCoeff} ({req.BuildingType}), 8.3.5[GB50009-2012]",
            Result = $"支承结构: μs1 = {mus1_struct:F4}+{internalCoeff} = {mus1_struct_final:F4}\n面板: μs1 = {mus1_panel}+{internalCoeff} = {mus1_panel_final}" });

        // (4) 风荷载标准值
        double wk_struct = beta * req.K1 * req.K2 * muZ * mus1_struct_final * w0;
        double wk_panel = beta * req.K1 * req.K2 * muZ * mus1_panel_final * w0;

        steps.Add(new() { StepNo = sn++, Name = "支承结构风荷载标准值",
            Formula = "wk = β×K1×K2×μz×μs1×w0  ……4.6.1[GB55001-2021]",
            Process = $"wk = {beta:F4}×{req.K1}×{req.K2}×{muZ:F4}×{mus1_struct_final:F4}×{w0:F6}",
            Result = $"wk = {wk_struct:F6} MPa" });

        steps.Add(new() { StepNo = sn++, Name = "面板风荷载标准值",
            Formula = "wk = β×K1×K2×μz×μs1×w0  ……4.6.1[GB55001-2021]",
            Process = $"wk = {beta:F4}×{req.K1}×{req.K2}×{muZ:F4}×{mus1_panel_final}×{w0:F6}",
            Result = $"wk = {wk_panel:F6} MPa" });

        return Ok(new WindLoadResponse
        {
            MuZ = Math.Round(muZ, 4),
            BetaGz = Math.Round(betaGz, 4),
            Beta = Math.Round(beta, 4),
            Mus1Structure = Math.Round(mus1_struct_final, 4),
            Mus1Panel = Math.Round(mus1_panel_final, 4),
            WkStructure = Math.Round(wk_struct, 6),
            WkPanel = Math.Round(wk_panel, 6),
            W0 = w0,
            Steps = steps
        });
    }

    /// <summary>地震作用参数计算</summary>
    /// <remarks>
    /// αmax = Fa × αmaxⅡ  (GB18306-2015)
    /// qEAk = βE × αmax × Gk/A  ……5.3.4[JGJ102-2003]
    /// PEk = βE × αmax × Gk  ……5.3.5[JGJ102-2003]
    /// </remarks>
    [HttpPost("Seismic")]
    [ProducesResponseType(typeof(SeismicResponse), StatusCodes.Status200OK)]
    public ActionResult<SeismicResponse> CalculateSeismic([FromBody] SeismicRequest req)
    {
        var steps = new List<CalculationStep>();
        int sn = 1;

        double alphaMax;
        if (req.AlphaMax.HasValue)
        {
            alphaMax = req.AlphaMax.Value;
            steps.Add(new() { StepNo = sn++, Name = "αmax (用户指定)",
                Result = $"αmax = {alphaMax}" });
        }
        else if (!string.IsNullOrEmpty(req.RegionName))
        {
            var region = _regionService.GetByName(req.RegionName);
            if (region == null) return NotFound($"未找到地区: {req.RegionName}");

            double alphaMaxII = region.AlphaMax;
            alphaMax = req.Fa * alphaMaxII;

            steps.Add(new() { StepNo = sn++, Name = "查地区参数表",
                Result = $"地区: {req.RegionName}, 抗震烈度: {region.SeismicIntensity}度, 峰值加速度: {region.PeakAcceleration}g, αmaxⅡ = {alphaMaxII}" });

            steps.Add(new() { StepNo = sn++, Name = "αmax计算",
                Formula = "αmax = Fa × αmaxⅡ  (GB18306-2015, 表E.1)",
                Process = $"αmax = {req.Fa} × {alphaMaxII}",
                Result = $"αmax = {alphaMax}" });
        }
        else
        {
            return BadRequest("请提供地区名称或αmax");
        }

        steps.Add(new() { StepNo = sn++, Name = "场地参数",
            Result = $"场地类型: {req.SiteCategory}类, Fa = {req.Fa}, βE = {req.BetaE}" });

        steps.Add(new() { StepNo = sn++, Name = "地震作用公式",
            Formula = "qEAk = βE×αmax×Gk/A  ……5.3.4[JGJ102-2003]\nPEk = βE×αmax×Gk  ……5.3.5[JGJ102-2003]",
            Process = $"βE = {req.BetaE}, αmax = {alphaMax}",
            Result = $"qEAk = {req.BetaE}×{alphaMax}×Gk/A = {req.BetaE * alphaMax}×Gk/A\nPEk = {req.BetaE}×{alphaMax}×Gk = {req.BetaE * alphaMax}×Gk" });

        return Ok(new SeismicResponse
        {
            AlphaMax = alphaMax,
            BetaE = req.BetaE,
            Formula = $"qEAk = {req.BetaE}×{alphaMax}×Gk/A, PEk = {req.BetaE}×{alphaMax}×Gk",
            Steps = steps
        });
    }

    /// <summary>作用效应组合</summary>
    /// <remarks>
    /// S = γG×SGk + ψw×γw×Swk + ψE×γE×SEk  ……5.4.1[JGJ102-2003]
    /// 
    /// 强度计算: γG=1.3, γw=1.5, γE=1.4, ψw=1.0, ψE=0.5
    /// 挠度计算: γG=1.0, γw=1.0, 不考虑地震
    /// </remarks>
    [HttpPost("LoadCombination")]
    [ProducesResponseType(typeof(LoadCombinationResponse), StatusCodes.Status200OK)]
    public ActionResult<LoadCombinationResponse> CalculateLoadCombination([FromBody] LoadCombinationRequest req)
    {
        var steps = new List<CalculationStep>();
        int sn = 1;

        // 分项系数（按原程序 guifan.dat 驱动，规范相关 γE；其余按 JGJ102-2003 第 5.4 节固定）
        // 公式: S = γG×SGk + ψw×γw×Swk + ψE×γE×SEk  ……5.4.1[JGJ102-2003]
        var guifanEntry = _calc.Guifan.GetOrDefault(req.StandardName);
        double gammaE = guifanEntry.GammaE;

        // 三个组合场景（风为主 / 地震为主 / 永久为主）的系数表（与原程序计算书 表3-2 一致）
        steps.Add(new() { StepNo = sn++, Name = "荷载效应组合系数表",
            Formula = "S = γG×SGk + ψw×γw×Swk + ψE×γE×SEk  ……5.4.1[JGJ102-2003]",
            Result = $"规范={guifanEntry.StandardName}  γE={gammaE}（广东地标=1.5，其余=1.4）\n" +
                     $"风为主  : γG=1.30 γw=1.50 γE={gammaE} ψw=1.0 ψE=0.5\n" +
                     $"地震为主: γG=1.30 γw=1.50 γE={gammaE} ψw=0.2 ψE=1.0\n" +
                     $"永久为主: γG=1.35 γw=1.50 γE={gammaE} ψw=0.6 ψE=0.5" });

        // 主要组合（风为主）—— 与历史接口保持 StrengthStructure/Panel 语义一致
        var wind = _calc.LoadCombination.Combine(
            CombinationScenario.WindDominant, req.StandardName,
            req.SGk, req.WkStructure, req.WkPanel, req.QEAk);
        double S_str_struct = wind.StrengthStructure;
        double S_str_panel = wind.StrengthPanel;

        steps.Add(new() { StepNo = sn++, Name = "强度计算组合（风为主） - 支承结构",
            Formula = $"S = {wind.GammaG}×SGk + {wind.PsiW}×{wind.GammaW}×Swk + {wind.PsiE}×{wind.GammaE}×SEk",
            Process = $"S = {wind.GammaG}×{req.SGk} + {wind.PsiW * wind.GammaW}×{req.WkStructure} + {wind.PsiE * wind.GammaE}×{req.QEAk}",
            Result = $"S = {S_str_struct:F6} MPa" });

        steps.Add(new() { StepNo = sn++, Name = "强度计算组合（风为主） - 面板",
            Process = $"S = {wind.GammaG}×{req.SGk} + {wind.PsiW * wind.GammaW}×{req.WkPanel} + {wind.PsiE * wind.GammaE}×{req.QEAk}",
            Result = $"S = {S_str_panel:F6} MPa" });

        // 挠度组合 (γG=1.0, γw=1.0, 不考虑地震)
        var defl = _calc.LoadCombination.DeflectionCombination(req.SGk, req.WkStructure, req.WkPanel);
        double S_defl_struct = defl.Structure;
        double S_defl_panel = defl.Panel;

        steps.Add(new() { StepNo = sn++, Name = "挠度计算组合",
            Formula = "S = 1.0×SGk + 1.0×Swk (不考虑地震)  ……5.4.1[JGJ102-2003]",
            Result = $"支承结构: S = {S_defl_struct:F6} MPa\n面板: S = {S_defl_panel:F6} MPa" });

        steps.Add(new() { StepNo = sn++, Name = "说明",
            Result = "按JGJ102-2003 5.4节（γE 由规范经 guifan.dat 决定）:\n" +
                     "• 强度/连接/预埋件: 风为主 γG=1.3, γw=1.5; 地震为主 ψE=1.0; 永久为主 γG=1.35\n" +
                     "• 挠度: γG=1.0, γw=1.0, 不考虑地震" });

        return Ok(new LoadCombinationResponse
        {
            StrengthStructure = Math.Round(S_str_struct, 6),
            StrengthPanel = Math.Round(S_str_panel, 6),
            DeflectionStructure = Math.Round(S_defl_struct, 6),
            DeflectionPanel = Math.Round(S_defl_panel, 6),
            Steps = steps
        });
    }
}
