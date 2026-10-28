using GlassManagement.Desktop.ViewModels.Calc;
using NUnit.Framework;

namespace GlassManagement.DesktopTests;

/// <summary>
/// 板材线第 4 屏「铝单板幕墙计算-板块计算」（V31 客户区 795×530）。
///
/// <para><b>实机基线</b>（2026-10-26，<c>local-data/recon/v31runs/chain_metal6.txt</c> 第 4 屏原文）：
/// 默认参数 1100×1380、n=1、t=2.5（1060 H14/H24）、短边/60、面板口径 wk=1.37KPa 时，输出为</para>
/// <code>
/// *******************************************
/// 面板部分风荷载标准值为:1.37KPa
/// *******************************************
/// A板强度计算值:65.556MPa
/// A板强度设计值:54MPa
/// 不满足要求(×)
/// *******************************************
/// A板挠度计算值:48.543mm
/// A板挠度设计值:18.333mm
/// 不满足要求(×)
/// *******************************************
/// </code>
///
/// <para>★ 铁律：本夹具的 wk 一律由第一页真算（<see cref="FirstPage"/>）传下来，
/// 不许手工赋值凑数。</para>
/// </summary>
[TestFixture]
public class MetalPlatePageTests
{
    private const string Sep43 = "*******************************************";
    private const string Sep40 = "****************************************";
    private const string Dash40 = "------------------------------------------";

    /// <summary>
    /// 复位板块屏的**界面输入项**到 V31 启动默认。
    /// <para>★ <see cref="CalcSession.Default"/> 是静态单例，同一进程里多个用例会互相污染
    /// （前一个用例把短边改成 1300，后一个就全错）。这里复位的全是界面可编辑输入，
    /// <b>不涉及任何计算结果</b>；wk / αmax 仍由第一页 <c>Recalculate()</c> 真算产生。</para>
    /// </summary>
    private static void ResetPlateInputs(CalcSession s)
    {
        s.PlateShortSideMm = 1100;
        s.PlateLongSideMm = 1380;
        s.PlateLongSideDivision = 1;
        s.PlateThicknessMm = 2.5;
        s.PlateAllowableStressMpa = 54;
        s.PlateAllowableShearMpa = 32;
        s.PlateUseRib = true;
        s.PlateRibAlgorithmTwo = false;
        s.PlateDeflectionDivisor = 60;
        s.PlateRibDeflectionDivisor = 180;
        s.PlateRibInertiaMm4 = 25340;
        s.PlateRibSectionModulusMm3 = 2028;
        s.PlateRibContactWidthMm = 50;
        s.PlateRibMaterial = "6063-T5";
        s.PlateRibAllowableStressMpa = 90;
        s.PlateRibColdFormed = false;
    }

    /// <summary>建向导 → 记板材菜单 → 第一页真算 → 返回会话与向导。</summary>
    private static (CalcSession Session, CalcWizardViewModel Wizard) Plate(string top = "金属板幕墙",
                                                                         string sub = "铝单板")
    {
        var wiz = new CalcWizardViewModel();
        bool routed = wiz.OpenMenuPath(top, sub);
        Assert.That(routed, Is.True, $"板材菜单 {top}/{sub} 应走向导链，而不是静态窗体");

        // 第一页真算：wk 的支承口径/面板口径与门禁位全部由这条链产出
        new BasicParamsViewModel(wiz.Session, wiz).Recalculate();
        ResetPlateInputs(wiz.Session);
        return (wiz.Session, wiz);
    }

    private static MetalPlateCalcViewModel OpenPlate(string top = "金属板幕墙", string sub = "铝单板")
    {
        var (_, wiz) = Plate(top, sub);
        wiz.StepFromBeamNext();                       // 横梁 → 下一步
        Assert.That(wiz.CurrentContent, Is.InstanceOf<MetalPlateCalcViewModel>(),
            "板材菜单横梁屏的「下一步」必须进板块屏（玻璃幕墙线才进玻璃屏）");
        return (MetalPlateCalcViewModel)wiz.CurrentContent!;
    }

    private static string[] Render(Action<MetalPlateCalcViewModel>? setup = null)
    {
        var vm = OpenPlate();
        setup?.Invoke(vm);
        vm.PreviewCommand.Execute(null);
        var list = new string[vm.Results.Count];
        for (int i = 0; i < list.Length; i++) list[i] = vm.Results[i].Text;
        return list;
    }

    // ───────────────── 路由 ─────────────────

    [Test]
    public void 板材菜单走向导链_横梁下一步进板块屏()
    {
        var vm = OpenPlate();
        Assert.That(vm.Session.CurrentTopMenu, Is.EqualTo("金属板幕墙"));
        Assert.That(vm.Session.IsPlateWallMenu, Is.True);
    }

    [Test]
    public void 背栓式点挂菜单_花岗岩路由到背栓板块屏()
    {
        var (_, wiz) = Plate("背栓式点挂幕墙", "花岗岩");
        wiz.StepFromBeamNext();
        Assert.That(wiz.CurrentContent, Is.InstanceOf<BackboltPlateCalcViewModel>());
    }

    [Test]
    public void 天然石材菜单_花岗岩短槽连接路由到石材屏()
    {
        var (_, wiz) = Plate("天然石材幕墙", "花岗岩：短槽连接");
        wiz.StepFromBeamNext();
        Assert.That(wiz.CurrentContent, Is.InstanceOf<StonePlateCalcViewModel>());
    }

    [Test]
    public void 人造板材菜单_瓷板短挂件连接路由到人造板屏()
    {
        var (_, wiz) = Plate("人造板材幕墙", "瓷板：短挂件连接");
        wiz.StepFromBeamNext();
        Assert.That(wiz.CurrentContent, Is.InstanceOf<ManmadePlateCalcViewModel>());
    }

    [Test]
    public void 玻璃幕墙横梁下一步仍进玻璃屏_不被板材分支抢走()
    {
        var wiz = new CalcWizardViewModel();
        wiz.OpenMenuPath("玻璃幕墙", "全隐框");
        new BasicParamsViewModel(wiz.Session, wiz).Recalculate();
        wiz.StepFromBeamNext();
        Assert.That(wiz.CurrentContent, Is.InstanceOf<GlassCalcViewModel>());
    }

    // ───────────────── A 板（n=1）逐位 ─────────────────

    [Test]
    public void A板_默认参数_屏上数值逐位一致()
    {
        var got = Render();

        Assert.That(got[0], Is.EqualTo(Sep43));
        Assert.That(got, Has.Some.EqualTo("面板部分风荷载标准值为:1.37KPa"));
        Assert.That(got, Has.Some.EqualTo("A板强度计算值:65.556MPa"));
        Assert.That(got, Has.Some.EqualTo("A板强度设计值:54MPa"));
        Assert.That(got, Has.Some.EqualTo("A板挠度计算值:48.543mm"));
        Assert.That(got, Has.Some.EqualTo("A板挠度设计值:18.333mm"));
        Assert.That(got, Has.Exactly(2).EqualTo("不满足要求(×)"));
        Assert.That(got[got.Length - 1], Is.EqualTo(Sep43));
    }

    [Test]
    public void A板_分隔符是43星_强度与挠度之间也是43星()
    {
        // 实机原文：A 板分支上强度/挠度两段的分隔都是 43 星（与 B 板分支的 40 星+40 短横不同）
        var got = Render();
        Assert.That(got, Has.None.EqualTo(Dash40));
        Assert.That(got.Count(l => l == Sep43), Is.EqualTo(4));
    }

    [Test]
    public void 面板口径wk_来自第一页而不是支承口径()
    {
        var got = Render();
        // 支承口径是 1.285KPa，面板口径 1.37KPa —— 板块屏必须用面板口径（μsl=1.0）
        Assert.That(got, Has.None.EqualTo("面板部分风荷载标准值为:1.285KPa"));
        Assert.That(got, Has.Some.EqualTo("面板部分风荷载标准值为:1.37KPa"));
    }

    // ───────────────── B 板 + 加强肋（n=2，计算肋） ─────────────────

    [Test]
    public void B板加肋_n等于2_逐位一致()
    {
        var got = Render(vm => vm.DivisionIndex = 1);   // sel=1 → 2 等分，Lx=690

        Assert.That(got, Has.Some.EqualTo("B板强度计算值:67.18MPa"));
        Assert.That(got, Has.Some.EqualTo("B板挠度计算值:8.818mm"));
        Assert.That(got, Has.Some.EqualTo("B板挠度设计值:11.5mm"));      // 690/60
        Assert.That(got, Has.Some.EqualTo("加强肋强度计算值:93.581MPa"));
        Assert.That(got, Has.Some.EqualTo("加强肋强度设计值:90MPa"));
        Assert.That(got, Has.Some.EqualTo("加强肋挠度计算值:8.623mm"));
        Assert.That(got, Has.Some.EqualTo("加强肋挠度设计值:6.111mm"));  // 1100/180
        // B 板分支里 A 板标签必须消失
        Assert.That(got, Has.None.Contains("A板"));
        Assert.That(got, Has.Some.EqualTo(Sep40));
        Assert.That(got, Has.Some.EqualTo(Dash40));
    }

    [Test]
    public void 算法二_肋标签改成B_B加强肋()
    {
        var got = Render(vm => { vm.DivisionIndex = 1; vm.AlgoTwo = true; });
        Assert.That(got.Any(l => l.StartsWith("B-B加强肋强度计算值:", StringComparison.Ordinal)), Is.True);
        Assert.That(got.Any(l => l.StartsWith("加强肋强度计算值:", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void 不算肋_等分数大于1仍回到A板()
    {
        var got = Render(vm => { vm.DivisionIndex = 1; vm.UseRib = false; });
        Assert.That(got, Has.Some.EqualTo("A板强度计算值:65.556MPa"));
        Assert.That(got, Has.None.Contains("加强肋"));
    }

    // ───────────────── 联动与门禁 ─────────────────

    [Test]
    public void 选板厚3_强度降到46点545()
    {
        var got = Render(vm =>
        {
            var row = vm.Profiles.First(p => Math.Abs(p.ThicknessMm - 3) < 1e-9);
            vm.SelectedProfile = row;
        });
        Assert.That(got, Has.Some.EqualTo("A板强度计算值:46.545MPa"));
    }

    [Test]
    public void 改尺寸并重新预演_旧结果先失效()
    {
        var vm = OpenPlate();
        vm.PreviewCommand.Execute(null);
        Assert.That(vm.HasResults, Is.True);

        vm.ShortSideText = "1300";                      // 改输入 → 结果作废
        Assert.That(vm.HasResults, Is.False);
        Assert.That(vm.StatusText, Does.Contain("请重新预演"));
        Assert.That(vm.StatusIsWarning, Is.True);
    }

    [Test]
    public void 长边等分数改4_Lx自动跟着变()
    {
        var vm = OpenPlate();
        vm.DivisionIndex = 3;                            // sel=3 → 4 等分
        Assert.That(vm.Session.PlateLongSideDivision, Is.EqualTo(4));
        Assert.That(vm.Session.PlateLx, Is.EqualTo(1380.0 / 4));
        Assert.That(vm.LxText, Is.EqualTo("345"));
        Assert.That(vm.RibEnabled, Is.True);
    }

    // ───────────────── 肋材质 8 选 + 冷弯薄壁型钢（实机 metalplate_ribmat*.txt） ─────────────────

    [TestCase("Q235", "加强肋强度设计值:215MPa", "加强肋强度计算值:89.125MPa")]
    [TestCase("Q355", "加强肋强度设计值:315MPa", "加强肋强度计算值:89.125MPa")]
    [TestCase("6063-T6", "加强肋强度设计值:150MPa", "加强肋强度计算值:93.581MPa")]
    [TestCase("6061-T6", "加强肋强度设计值:200MPa", "加强肋强度计算值:93.581MPa")]
    [TestCase("6063A-T5", "加强肋强度设计值:135MPa", "加强肋强度计算值:93.581MPa")]
    [TestCase("6063A-T6", "加强肋强度设计值:160MPa", "加强肋强度计算值:93.581MPa")]
    public void 切换肋材质_输出逐字符一致(string code, string expectAllow, string expectSigma)
    {
        var got = Render(vm =>
        {
            vm.DivisionIndex = 1;                 // n=2 → Lx=690，才有加强肋输出
            vm.RibMaterial = code;
        });
        Assert.That(got, Has.Some.EqualTo(expectAllow));
        Assert.That(got, Has.Some.EqualTo(expectSigma));
    }

    [Test]
    public void 冷弯薄壁型钢_只对钢生效()
    {
        // Q235 + 冷弯：215 → 205，且 γ 撤掉 ⇒ 应力回到 93.581
        var steel = Render(vm => { vm.DivisionIndex = 1; vm.RibMaterial = "Q235"; vm.RibColdFormed = true; });
        Assert.That(steel, Has.Some.EqualTo("加强肋强度设计值:205MPa"));
        Assert.That(steel, Has.Some.EqualTo("加强肋强度计算值:93.581MPa"));

        // 铝 + 冷弯：完全不变
        var alu = Render(vm => { vm.DivisionIndex = 1; vm.RibMaterial = "6063-T5"; vm.RibColdFormed = true; });
        Assert.That(alu, Has.Some.EqualTo("加强肋强度设计值:90MPa"));
        Assert.That(alu, Has.Some.EqualTo("加强肋强度计算值:93.581MPa"));
    }

    [Test]
    public void 第一页没算过_预演被门禁拦住()
    {
        // 只进菜单、不跑第一页 ⇒ PanelWindReady=false，预演必须拒绝
        var wiz = new CalcWizardViewModel();
        wiz.OpenMenuPath("金属板幕墙", "铝单板");
        wiz.StepFromBeamNext();
        var vm = (MetalPlateCalcViewModel)wiz.CurrentContent!;

        Assert.That(vm.PreviewEnabled, Is.False);
        vm.PreviewCommand.Execute(null);
        Assert.That(vm.HasResults, Is.False);
        Assert.That(vm.StatusIsWarning, Is.True);
    }
}
