using GlassManagement.Calc.Guifan;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 原程序荷载效应组合引擎。
/// 公式：S = γG·SGk + ψw·γw·Swk + ψE·γE·SEk  ……5.4.1[JGJ102-2003]
/// γE 由 guifan.dat（所选项目规范）决定；γG/γw 与场景 ψw/ψE 由 JGJ102 第 5.4 节固定。
/// 经逆向验证（计算书 表3-2 与 guifan.dat 逐条比对）：
///   风为主   γG=1.3  γw=1.5  ψw=1.0  ψE=0.5
///   地震为主 γG=1.3  γw=1.5  ψw=0.2  ψE=1.0
///   永久为主 γG=1.35 γw=1.5  ψw=0.6  ψE=0.5
/// 挠度组合：γG=1.0  γw=1.0  不考虑地震。
/// </summary>
public enum CombinationScenario
{
    /// <summary>风荷载效应为主（按 JGJ102 表 5.4.1 风载组合）。</summary>
    WindDominant,
    /// <summary>地震作用为主。</summary>
    SeismicDominant,
    /// <summary>永久荷载效应控制（重力荷载效应控制）。</summary>
    PermanentDominant,
}

public sealed class LoadCombinationResult
{
    public CombinationScenario Scenario { get; init; }
    public string StandardName { get; init; } = "";
    public double GammaG { get; init; }
    public double GammaW { get; init; }
    public double GammaE { get; init; }
    public double PsiW { get; init; }
    public double PsiE { get; init; }
    /// <summary>强度组合 - 支承结构 (MPa)。</summary>
    public double StrengthStructure { get; init; }
    /// <summary>强度组合 - 面板 (MPa)。</summary>
    public double StrengthPanel { get; init; }
}

public sealed class LoadCombinationEngine
{
    private readonly GuifanData _guifan;

    public LoadCombinationEngine(GuifanData guifan) => _guifan = guifan;

    /// <summary>取规范对应的 γE（广东地标=1.5，其余=1.4）。</summary>
    public double GetGammaE(string? standardName) => _guifan.GetOrDefault(standardName).GammaE;

    /// <summary>
    /// 计算指定场景的荷载效应组合。
    /// </summary>
    /// <param name="scenario">组合场景。</param>
    /// <param name="standardName">项目规范名称（如 "玻璃幕墙规范JGJ102-2003"）。</param>
    /// <param name="sgk">重力荷载标准值效应 SGk。</param>
    /// <param name="swkStructure">支承结构风荷载标准值效应。</param>
    /// <param name="swkPanel">面板风荷载标准值效应。</param>
    /// <param name="sek">地震作用标准值效应 SEk。</param>
    public LoadCombinationResult Combine(
        CombinationScenario scenario,
        string? standardName,
        double sgk,
        double swkStructure,
        double swkPanel,
        double sek)
    {
        var g = _guifan.GetOrDefault(standardName);
        double gammaE = g.GammaE;

        (double gammaG, double gammaW, double psiW, double psiE) = scenario switch
        {
            CombinationScenario.WindDominant => (1.3, 1.5, 1.0, 0.5),
            CombinationScenario.SeismicDominant => (1.3, 1.5, 0.2, 1.0),
            CombinationScenario.PermanentDominant => (1.35, 1.5, 0.6, 0.5),
            _ => (1.3, 1.5, 1.0, 0.5),
        };

        double sStruct = gammaG * sgk + psiW * gammaW * swkStructure + psiE * gammaE * sek;
        double sPanel = gammaG * sgk + psiW * gammaW * swkPanel + psiE * gammaE * sek;

        return new LoadCombinationResult
        {
            Scenario = scenario,
            StandardName = g.StandardName,
            GammaG = gammaG,
            GammaW = gammaW,
            GammaE = gammaE,
            PsiW = psiW,
            PsiE = psiE,
            StrengthStructure = sStruct,
            StrengthPanel = sPanel,
        };
    }

    /// <summary>挠度计算组合（γG=1.0, γw=1.0, 不考虑地震）。</summary>
    public (double Structure, double Panel) DeflectionCombination(
        double sgk, double swkStructure, double swkPanel) =>
        (1.0 * sgk + 1.0 * swkStructure, 1.0 * sgk + 1.0 * swkPanel);

    /// <summary>
    /// 返回全部三个场景的组合，便于调用方取包络（最不利）值。
    /// </summary>
    public IReadOnlyList<LoadCombinationResult> AllScenarios(
        string? standardName, double sgk, double swkStructure, double swkPanel, double sek)
    {
        var list = new List<LoadCombinationResult>(3);
        foreach (CombinationScenario s in Enum.GetValues<CombinationScenario>())
        {
            list.Add(Combine(s, standardName, sgk, swkStructure, swkPanel, sek));
        }
        return list;
    }
}
