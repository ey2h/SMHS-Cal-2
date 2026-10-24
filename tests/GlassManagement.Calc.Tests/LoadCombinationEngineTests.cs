using GlassManagement.Calc;
using GlassManagement.Calc.Engines;
using GlassManagement.Calc.Guifan;
using Xunit;

namespace GlassManagement.Calc.Tests;

public class LoadCombinationEngineTests
{
    private static readonly CalcService Svc = CalcService.CreateDefault();

    [Fact]
    public void Guifan_Parses_Embedded_Standards()
    {
        // 内嵌表应含 10 条规范（与原程序 guifan.dat 一致）
        Assert.Equal(10, Svc.Guifan.All.Count);
    }

    [Theory]
    [InlineData("玻璃幕墙规范JGJ102-2003", 0.5, 1.4, 1.0)]
    [InlineData("广东地方标准", 0.5, 1.5, 1.0)]
    [InlineData("重庆地方标准", 1.0, 1.4, 0.6)]
    [InlineData("抗震通用规范", 1.0, 1.4, 0.2)]
    [InlineData("金属与石材幕墙规范JGJ133-2001", 0.6, 1.4, 1.0)]
    public void Guifan_Entry_Matches_Reference(string name, double psiE, double gammaE, double psiW)
    {
        Assert.True(Svc.Guifan.TryGet(name, out var e));
        Assert.Equal(psiE, e.PsiE, 6);
        Assert.Equal(gammaE, e.GammaE, 6);
        Assert.Equal(psiW, e.PsiW, 6);
    }

    [Fact]
    public void JGJ102_WindDominant_Matches_CalcBook_Table3_2()
    {
        // 计算书 表3-2：风为主 γG=1.3 γw=1.5 γE=1.4 ψw=1.0 ψE=0.5
        var r = Svc.LoadCombination.Combine(
            CombinationScenario.WindDominant, "玻璃幕墙规范JGJ102-2003",
            sgk: 0, swkStructure: 0, swkPanel: 0, sek: 0);

        Assert.Equal(1.3, r.GammaG);
        Assert.Equal(1.5, r.GammaW);
        Assert.Equal(1.4, r.GammaE);   // guifan 中间数 = γE
        Assert.Equal(1.0, r.PsiW);
        Assert.Equal(0.5, r.PsiE);
    }

    [Fact]
    public void JGJ102_SeismicDominant_Matches_CalcBook_Table3_2()
    {
        // 地震为主 γG=1.3 γw=1.5 γE=1.4 ψw=0.2 ψE=1.0
        var r = Svc.LoadCombination.Combine(
            CombinationScenario.SeismicDominant, "玻璃幕墙规范JGJ102-2003",
            sgk: 0, swkStructure: 0, swkPanel: 0, sek: 0);

        Assert.Equal(1.3, r.GammaG);
        Assert.Equal(1.5, r.GammaW);
        Assert.Equal(1.4, r.GammaE);
        Assert.Equal(0.2, r.PsiW);
        Assert.Equal(1.0, r.PsiE);
    }

    [Fact]
    public void JGJ102_PermanentDominant_Matches_CalcBook_Table3_2()
    {
        // 永久为主 γG=1.35 γw=1.5 γE=1.4 ψw=0.6 ψE=0.5
        var r = Svc.LoadCombination.Combine(
            CombinationScenario.PermanentDominant, "玻璃幕墙规范JGJ102-2003",
            sgk: 0, swkStructure: 0, swkPanel: 0, sek: 0);

        Assert.Equal(1.35, r.GammaG);
        Assert.Equal(1.5, r.GammaW);
        Assert.Equal(1.4, r.GammaE);
        Assert.Equal(0.6, r.PsiW);
        Assert.Equal(0.5, r.PsiE);
    }

    [Fact]
    public void Guangdong_GammaE_Overrides_To_1_5()
    {
        // 广东地标 γE=1.5 应透传至组合（与 JGJ102 的 1.4 区分）
        var r = Svc.LoadCombination.Combine(
            CombinationScenario.WindDominant, "广东地方标准",
            sgk: 0, swkStructure: 0, swkPanel: 0, sek: 0);
        Assert.Equal(1.5, r.GammaE);
        Assert.Equal(0.5, r.PsiE);
        Assert.Equal(1.0, r.PsiW);
    }

    [Fact]
    public void Combination_Numeric_Example()
    {
        // S = 1.3·SGk + 1.0·1.5·Swk + 0.5·1.4·SEk
        var r = Svc.LoadCombination.Combine(
            CombinationScenario.WindDominant, "玻璃幕墙规范JGJ102-2003",
            sgk: 0.001, swkStructure: 0.002, swkPanel: 0.0025, sek: 0.0003);

        Assert.Equal(1.3 * 0.001 + 1.0 * 1.5 * 0.002 + 0.5 * 1.4 * 0.0003, r.StrengthStructure, 9);
        Assert.Equal(1.3 * 0.001 + 1.0 * 1.5 * 0.0025 + 0.5 * 1.4 * 0.0003, r.StrengthPanel, 9);
    }

    [Fact]
    public void DeflectionCombination_Uses_Unity_Coefficients()
    {
        var (s, p) = Svc.LoadCombination.DeflectionCombination(sgk: 0.001, swkStructure: 0.002, swkPanel: 0.0025);
        Assert.Equal(0.003, s, 9);
        Assert.Equal(0.0035, p, 9);
    }

    [Fact]
    public void UnknownStandard_FallsBack_To_JGJ102_Default()
    {
        var r = Svc.LoadCombination.Combine(
            CombinationScenario.WindDominant, "不存在的规范",
            sgk: 0, swkStructure: 0, swkPanel: 0, sek: 0);
        Assert.Equal(1.4, r.GammaE); // 默认回退 JGJ102
    }
}
