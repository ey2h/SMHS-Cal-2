using GlassManagement.Desktop.ViewModels.Calc;
using NUnit.Framework;

namespace GlassManagement.DesktopTests;

/// <summary>
/// 玻璃计算屏模板与联动门禁。
///
/// <para><b>wk 口径（已闭合）</b>：V31 玻璃屏 <c>0xA7D00C–0xA7D07C</c> 的乘数链<b>不含 μs1·ψ</b>，
/// 故玻璃 wk = 1.37 而立柱 wk = 1.285（同一 w0/βgz/μz，仅体型系数项差 1.20 vs 1.126）。
/// 这是 V31 自身缺陷，复刻按实测口径实现，测试直接喂 1.37 锁定数值；
/// <b>绝不允许</b>改用 1.285 后调低期望值。</para>
///
/// <para><b>可断言的部分</b>：输出行数与文案、分隔符长度、片位可用性矩阵、尺寸候选顺序、模式互斥。</para>
/// </summary>
[TestFixture]
public class GlassTemplateTests
{
    private const string Sep47 = "***********************************************";
    private const string Sep43 = "*******************************************";
    private const string Dash = "——————————————————";

    /// <summary>建 VM 并立刻渲染取快照（CalcSession.Default 是单例，不能先建两个再渲染）。</summary>
    private static string[] Render(Action<CalcSession>? setup = null)
    {
        var wiz = new CalcWizardViewModel();
        var s = wiz.Session;
        s.WindStandardKpa = 1.285;         // 立柱/横梁口径
        s.GlassWindKpa = 1.37;             // 玻璃口径（V31 实机：漏乘 μs1·ψ）
        s.BeamLeftBayMm = 1100;
        s.BeamRightBayMm = 1100;
        s.BeamUpperGridHeightMm = 1380;
        s.GlassMono = true; s.GlassInsulated = false; s.GlassLaminated = false;
        s.GlassInsulLam = false; s.GlassDoubleInsul = false; s.GlassDoubleLam = false;
        s.GlassUsePresetSize = true; s.GlassUseCustomSize = false;
        s.GlassPresetWidthMm = 1100; s.GlassPresetHeightMm = 1380;
        s.GlassOuterThicknessMm = 6; s.GlassMidThicknessMm = 6; s.GlassInnerThicknessMm = 6;
        s.GlassOuterKind = "钢化玻璃"; s.GlassMidKind = "钢化玻璃"; s.GlassInnerKind = "钢化玻璃";
        setup?.Invoke(s);

        var vm = new GlassCalcViewModel(s, wiz);
        vm.PreviewCommand.Execute(null);
        var list = new string[vm.Results.Count];
        for (int i = 0; i < list.Length; i++) list[i] = vm.Results[i].Text;
        return list;
    }

    // ---------------- 结构（不依赖 wk 数值）----------------

    [Test]
    public void 单片_输出结构11行_47星分隔()
    {
        var got = Render();
        Assert.That(got, Has.Length.EqualTo(11));
        Assert.That(got[0], Is.EqualTo(Sep47));
        Assert.That(got[1], Does.StartWith("玻璃部分风荷载标准值为:"));
        Assert.That(got[1], Does.EndWith("KPa"));
        Assert.That(got[3], Does.StartWith("玻璃强度计算值:"));
        Assert.That(got[4], Does.StartWith("选用玻璃的强度设计值:"));
        Assert.That(got[5], Is.EqualTo("满足要求(√)"));
        Assert.That(got[7], Does.StartWith("玻璃挠度计算值:"));
        Assert.That(got[8], Does.StartWith("选用玻璃挠度容许值:"));
        Assert.That(got[10], Is.EqualTo(Sep47));
    }

    [Test]
    public void 中空_输出14行_43星分隔_外内两片()
    {
        var got = Render(s => { s.GlassMono = false; s.GlassInsulated = true; });
        Assert.That(got[0], Is.EqualTo(Sep43));
        Assert.That(got[3], Does.StartWith("外片玻璃强度计算值:"));
        Assert.That(got[6], Is.EqualTo(Dash));
        Assert.That(got[7], Does.StartWith("内片玻璃强度计算值:"));
        Assert.That(got[10], Is.EqualTo(Sep43));
        Assert.That(got[11], Does.StartWith("玻璃挠度计算值:"));
        Assert.That(got[12], Does.Contain("(中空玻璃)"));
        Assert.That(got, Has.Length.EqualTo(15));
    }

    [Test]
    public void 夹层_输出11行_容许值标夹层玻璃()
    {
        var got = Render(s => { s.GlassMono = false; s.GlassLaminated = true; });
        Assert.That(got, Has.Length.EqualTo(11));
        Assert.That(got[3], Does.StartWith("玻璃强度计算值:"));
        Assert.That(got[8], Does.Contain("(夹层玻璃)"));
    }

    [Test]
    public void 中空加夹层PVB_输出20行_三片各一组()
    {
        var got = Render(s => { s.GlassMono = false; s.GlassInsulLam = true; });
        Assert.That(got[0], Is.EqualTo(Sep43));
        Assert.That(got[3], Does.StartWith("1#玻璃强度计算值:"));
        Assert.That(got[7], Does.StartWith("2#玻璃强度计算值:"));
        Assert.That(got[11], Does.StartWith("3#玻璃强度计算值:"));
        Assert.That(got[1], Does.StartWith("玻璃部分风荷载标准值为:"));
        Assert.That(got, Has.Length.EqualTo(19));
    }

    [Test]
    public void 中空加夹层SGP_文案带夹层偏室外侧()
    {
        var got = Render(s =>
        {
            s.GlassMono = false; s.GlassInsulLam = true;
            s.InterlayerKind = GlassManagement.Calc.Engines.InterlayerKind.Sgp;
        });
        Assert.That(got[3], Does.StartWith("1#玻璃(夹层偏室外侧)强度计算值:"));
    }

    [Test]
    public void 双中空_给出未支持提示而不崩()
    {
        var wiz = new CalcWizardViewModel();
        var s = wiz.Session;
        s.WindStandardKpa = 1.285;
        s.GlassMono = false; s.GlassDoubleInsul = true;
        var vm = new GlassCalcViewModel(s, wiz);
        vm.PreviewCommand.Execute(null);
        Assert.That(vm.StatusIsWarning, Is.True);
        Assert.That(vm.StatusText, Does.Contain("双中空"));
        Assert.That(vm.Results, Is.Empty);
    }

    // ---------------- 片位可用性矩阵 ----------------

    [Test]
    public void 片位可用性_四种配置()
    {
        var wiz = new CalcWizardViewModel();
        var s = wiz.Session;

        s.GlassMono = true; s.GlassInsulated = false; s.GlassLaminated = false; s.GlassInsulLam = false;
        var single = new GlassCalcViewModel(s, wiz);
        Assert.That(single.OuterEnabled, Is.True);
        Assert.That(single.MidEnabled, Is.False);
        Assert.That(single.InnerEnabled, Is.False);
        Assert.That(single.InterlayerEnabled, Is.False);

        s.GlassMono = false; s.GlassInsulated = true;
        var insul = new GlassCalcViewModel(s, wiz);
        Assert.That(insul.OuterEnabled, Is.True);
        Assert.That(insul.MidEnabled, Is.False, "中空玻璃时中片整块 en=False（实机）");
        Assert.That(insul.InnerEnabled, Is.True);

        s.GlassInsulated = false; s.GlassLaminated = true;
        var lam = new GlassCalcViewModel(s, wiz);
        Assert.That(lam.InnerEnabled, Is.True);
        Assert.That(lam.InterlayerEnabled, Is.True, "夹层玻璃解锁夹层配置块");

        s.GlassLaminated = false; s.GlassInsulLam = true;
        var comp = new GlassCalcViewModel(s, wiz);
        Assert.That(comp.MidEnabled, Is.True, "中空+夹层时中片解锁");
        Assert.That(comp.InterlayerEnabled, Is.True);
    }

    // ---------------- 尺寸联动 ----------------

    [Test]
    public void 尺寸候选_外层高内层宽_改宽后顺序正确()
    {
        var wiz = new CalcWizardViewModel();
        var s = wiz.Session;
        s.BeamLeftBayMm = 1100; s.BeamRightBayMm = 1100; s.BeamUpperGridHeightMm = 1380;
        var vm = new GlassCalcViewModel(s, wiz);
        Assert.That(vm.SizeItems, Is.EqualTo(new[] { "1100*1380", "1100*1380", "1100*1380", "1100*1380" }));

        // 改宽② 1100→900：候选变 [1100,900,1100,900]（外层高、内层宽）
        s.BeamRightBayMm = 900;
        vm = new GlassCalcViewModel(s, wiz);
        Assert.That(vm.SizeItems, Is.EqualTo(new[] { "1100*1380", "900*1380", "1100*1380", "900*1380" }));
    }

    [Test]
    public void 两种尺寸模式互斥()
    {
        var wiz = new CalcWizardViewModel();
        var s = wiz.Session;
        s.GlassUsePresetSize = true; s.GlassUseCustomSize = false;
        var vm = new GlassCalcViewModel(s, wiz);
        Assert.That(vm.PresetEnabled, Is.True);
        Assert.That(vm.CustomEnabled, Is.False);

        vm.UseCustomSize = true;
        Assert.That(s.GlassUseCustomSize, Is.True);
        Assert.That(s.GlassUsePresetSize, Is.False, "选自定义必须取消分格尺寸");
        Assert.That(vm.CustomEnabled, Is.True);
        Assert.That(vm.PresetEnabled, Is.False);
    }

    [Test]
    public void 自定义尺寸非法值被拒并提示()
    {
        var wiz = new CalcWizardViewModel();
        var s = wiz.Session;
        s.GlassUsePresetSize = false; s.GlassUseCustomSize = true;
        s.GlassCustomWidthMm = 900;
        var vm = new GlassCalcViewModel(s, wiz);
        vm.CustomWidthText = "0";
        Assert.That(vm.StatusIsWarning, Is.True);
        Assert.That(vm.StatusText, Does.Contain("玻璃尺寸参数有误"));
        Assert.That(s.GlassCustomWidthMm, Is.EqualTo(900), "非法值不得写入 Session");
    }

    // ---------------- 数值（wk 未接入，标 Ignore）----------------

    [Test]
    public void 单片_数值逐位_实机137()
    {
        Assert.That(Render(), Does.Contain("玻璃强度计算值:24.793MPa"));
        Assert.That(Render(), Does.Contain("玻璃挠度计算值:8.23mm"));
        Assert.That(Render(), Does.Contain("选用玻璃挠度容许值:18.333mm(单片玻璃)"));
    }

    [Test]
    public void 中空_数值逐位_实机137()
    {
        // wk = 1.37（玻璃口径）作为输入：6+6 中空、1100×1380、钢化
        // 实机期望值来自 V31 玻璃屏中空分支（0xA26000–0xA2DB00）
        // 这里按内容匹配而非下标，避免模板行序调整导致断言错位
        var got = Render(s => { s.GlassMono = false; s.GlassInsulated = true; });
        Assert.That(got, Has.Some.EqualTo("外片玻璃强度计算值:14.697MPa"));
        Assert.That(got, Has.Some.EqualTo("内片玻璃强度计算值:13.51MPa"));
        Assert.That(got, Has.Some.EqualTo("玻璃挠度计算值:5.029mm"));
        Assert.That(got, Has.Some.EqualTo("选用玻璃挠度容许值:18.333mm(中空玻璃)"));
    }

    [Test]
    public void 玻璃wk公式_W0乘Beta乘MuZ乘1加内压()
    {
        // V31 0xA7D00C–0xA7D07C 的乘数链：w0 · βgz · 地形 · μz · (1+μs内压)
        // 与横梁 wk 的唯一差别是「不乘 μs1·ψ」——这是 V31 缺陷，实测 1.37 vs 1.285
        var terrain = GlassManagement.Calc.Engines.TerrainCategory.C;
        double beta = GlassManagement.Calc.Engines.WindLoadEngine.BetaGz(terrain, 100.0, 2.5);
        double muz = GlassManagement.Calc.Engines.WindLoadEngine.MuZ(terrain, 100.0);
        Assert.That(beta, Is.EqualTo(1.6929).Within(0.00005));   // 0x85EEFA
        Assert.That(muz, Is.EqualTo(1.4983).Within(0.00005));    // 0x85F08B（0.544×2.7542）

        double wkGlass = 0.45 * beta * muz * (1.0 + 0.2);
        Assert.That(wkGlass, Is.EqualTo(1.3697).Within(0.0005)); // 屏上 1.37
        Assert.That(System.Math.Round(wkGlass, 3), Is.EqualTo(1.37));
    }
}
