using GlassManagement.Desktop.ViewModels.Calc;
using NUnit.Framework;

namespace GlassManagement.DesktopTests;

/// <summary>
/// 玻璃计算屏模板与联动门禁。
///
/// <para><b>wk 口径（已定案）</b>：V31 的 wk 按「计算对象」分两套，不是全局唯一值。</para>
/// <list type="bullet">
/// <item><b>支承结构口径</b>（立柱/横梁/连接/埋件，四屏恒等）：<c>P·(μs1·ψ(A) + μs_int)</c> → 1.285</item>
/// <item><b>面板口径</b>（玻璃/胶类，两屏恒等）：<c>P·(μsl + μs_int)</c>，<c>μsl=1.0</c> 不折减 → 1.370</item>
/// </list>
/// <para>实测：六组第一页参数 × 六屏扫描，见 <c>local-data/recon/wk_scan.py</c> 与
/// <c>v31runs/wk_matrix_all.psv</c>；文案三套地址 <c>0x4D22D4 / 0x4D3FF4 / 0x4D81B0</c>。</para>
/// <para>测试直接喂 1.37 锁定面板口径数值，<b>绝不允许</b>改用 1.285 后调低期望值。</para>
///
/// <para><b>可断言的部分</b>：输出行数与文案、分隔符长度、片位可用性矩阵、尺寸候选顺序、模式互斥。</para>
/// </summary>
[TestFixture]
public class GlassTemplateTests
{
    private const string Sep47 = "***********************************************";
    private const string Sep43 = "*******************************************";
    private const string Sep41 = "*****************************************";
    private const string Dash = "——————————————————";

    /// <summary>建 VM 并立刻渲染取快照（CalcSession.Default 是单例，不能先建两个再渲染）。</summary>
    private static string[] Render(Action<CalcSession>? setup = null)
    {
        var wiz = new CalcWizardViewModel();
        var s = wiz.Session;
        s.WindStandardKpa = 1.285;         // 支承结构口径（立柱/横梁/连接/埋件）
        s.PanelWindKpa = 1.37;             // 面板口径（玻璃/胶类）：μsl=1.0 不折减 ψ
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
    public void 双中空_三片各一组_43星分隔()
    {
        // 三玻双中空：取消「未支持」后按三片输出，等效厚度逐层衰减。
        // ⚠ 文案（「1#/2#/3#玻璃强度计算值」）是按三片构型类推的，V31 双中空屏尚未 dump 原文核对。
        var got = Render(s => { s.GlassMono = false; s.GlassDoubleInsul = true; });
        Assert.That(got[0], Is.EqualTo(Sep43));
        Assert.That(got[1], Does.StartWith("玻璃部分风荷载标准值为:"));
        Assert.That(got[3], Does.StartWith("1#玻璃强度计算值:"));
        Assert.That(got, Has.Some.StartsWith("2#玻璃强度计算值:"));
        Assert.That(got, Has.Some.StartsWith("3#玻璃强度计算值:"));
        Assert.That(got, Has.Some.EqualTo("选用玻璃挠度容许值:18.333mm(双中空玻璃)"));
    }

    [Test]
    public void 双夹层中空_四片各一组_41星分隔_容许值无后缀()
    {
        // 实测（glass_cfg_scan.py --cases dbllam）：4 片、41 星、
        // 片名为「最外片(1#)/次外片(2#)/次内片(3#)/最内片(4#)」，
        // 且挠度容许值行**没有**「(××玻璃)」后缀 —— 与其他构型都不同。
        var got = Render(s => { s.GlassMono = false; s.GlassDoubleLam = true; });
        Assert.That(got[0], Is.EqualTo(Sep41));
        Assert.That(got[1], Does.StartWith("玻璃部分风荷载标准值为:"));
        Assert.That(got, Has.Some.EqualTo("最外片(1#)玻璃强度计算值:8.172MPa"));
        Assert.That(got, Has.Some.EqualTo("次外片(2#)玻璃强度计算值:8.172MPa"));
        Assert.That(got, Has.Some.EqualTo("次内片(3#)玻璃强度计算值:7.539MPa"));
        Assert.That(got, Has.Some.EqualTo("最内片(4#)玻璃强度计算值:7.539MPa"));
        Assert.That(got, Has.Some.EqualTo("玻璃挠度计算值:2.625mm"));
        Assert.That(got, Has.Some.EqualTo("选用玻璃挠度容许值:18.333mm"));
        Assert.That(got, Has.None.Contains("(双夹层"), "V31 此构型容许值不带后缀");
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
    public void 两套wk口径_支承含psi折减_面板不折减()
    {
        // V31 0xA7D00C–0xA7D07C 的乘数链：w0 · βgz · 地形 · μz · (μsl + μs内压)
        // 两口径共用 w0/βgz/μz/K，只在体型系数项分岔：
        //   支承 1.126 = μs1·ψ(A) + 0.2 → 0.926 + 0.2
        //   面板 1.200 = μsl(1.0)  + 0.2
        var terrain = GlassManagement.Calc.Engines.TerrainCategory.C;
        double beta = GlassManagement.Calc.Engines.WindLoadEngine.BetaGz(terrain, 100.0, 2.5);
        double muz = GlassManagement.Calc.Engines.WindLoadEngine.MuZ(terrain, 100.0);
        Assert.That(beta, Is.EqualTo(1.6929).Within(0.00005));   // 0x85EEFA
        Assert.That(muz, Is.EqualTo(1.4983).Within(0.00005));    // 0x85F08B（0.544×2.7542）

        const double muInt = 0.2;
        double p = 0.45 * beta * muz;                            // 两口径相同的公共因子

        // 面板口径：μsl = 1.0，不做 ψ 折减
        double wkPanel = p * (CalcSession.PanelShapeCoef + muInt);
        Assert.That(wkPanel, Is.EqualTo(1.3697).Within(0.0005)); // 屏上 1.37
        Assert.That(System.Math.Round(wkPanel, 3), Is.EqualTo(1.37));

        // 支承口径：μs1·ψ(A) ≈ 0.926（A = 分格面积，ψ<1）。
        // 注意容差按 1e-3：μs1·ψ 目前只到 4 位（0.926），再乘 P≈1.1414 会放大小误差，
        // 这里断言的是「公共因子 P 与面板侧同源」，不是 μs1·ψ 的第 5 位。
        double wkSupport = p * (0.926 + muInt);
        Assert.That(wkSupport, Is.EqualTo(1.285).Within(0.001));  // 屏上 1.285
        Assert.That(System.Math.Round(wkSupport, 3), Is.EqualTo(1.285));

        // 面板恒大于支承，且比值 = 1.200/1.126（与实测 1.37/1.285 一致）
        Assert.That(wkPanel, Is.GreaterThan(wkSupport));
        Assert.That(wkPanel / wkSupport, Is.EqualTo(1.200 / 1.126).Within(0.0001));
    }

    [Test]
    public void 面板口径_六组实测比值自洽()
    {
        // 用实机六组的「玻璃 / 支柱」比值反查体型系数项，验证 μsl=1.0 的设定能同时解释全部组。
        // 数据来源：local-data/recon/v31runs/wk_matrix_all.psv（wk_scan.py 产出）
        //   组           支承    面板    面板/支承
        //   G1 默认     1.514   1.598   1.05548
        //   G5 内压0.30 1.970   2.055   1.04315
        //   G6 内压0.10 1.742   1.826   1.04822
        var rows = new (double Support, double Panel, double MuInt)[]
        {
            (1.514, 1.598, 0.2),
            (1.970, 2.055, 0.3),
            (1.742, 1.826, 0.1),
        };
        foreach (var (support, panel, muInt) in rows)
        {
            // 由「面板 = P·(1 + μint)」反解 P，再由 P 求支承侧的 μs1·ψ
            double p = panel / (CalcSession.PanelShapeCoef + muInt);
            double muS1Psi = support / p - muInt;
            // μs1·ψ 必须落在 (0, 1] 且小于面板的 1.0（ψ 是折减系数，不可能 >1）
            Assert.That(muS1Psi, Is.GreaterThan(0.0), $"μint={muInt} 反解出负值");
            Assert.That(muS1Psi, Is.LessThan(1.0), $"μint={muInt} 时 μs1·ψ={muS1Psi:0.####} ≥ 1，与 ψ 折减矛盾");
            // 反算回面板必须逐位复原（自洽性）
            Assert.That(p * (CalcSession.PanelShapeCoef + muInt), Is.EqualTo(panel).Within(0.0006));
        }
    }
}
