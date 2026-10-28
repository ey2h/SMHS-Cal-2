using GlassManagement.Desktop.ViewModels.Calc;
using NUnit.Framework;

namespace GlassManagement.DesktopTests;

/// <summary>
/// 第一屏「基本参数输入」→ 下游屏 的参数传递守卫。
///
/// <para>用户硬要求：<b>所有参数都是从第一个面板传到后面的</b>。
/// 这组用例锁三件事：</para>
/// <list type="number">
///   <item>第一页真算一遍能得到 V31 启动默认的 1.285 / 1.37，且门禁位置 true；</item>
///   <item>第一页没算时，下游屏一律拒绝计算并给出统一提示（不再用常量兜底）；</item>
///   <item>面板口径与支承口径是两个不同的值，不许互相顶替。</item>
/// </list>
/// </summary>
[TestFixture]
public class FirstPageFlowTests
{
    [Test]
    public void 第一页算出支承与面板两套wk()
    {
        var (s, _) = FirstPage.Run();
        Assert.That(s.WindStandardResolved, Is.True, "第一页算完必须置门禁位");
        Assert.That(s.WindStandardKpa, Is.EqualTo(FirstPage.ExpectedSupportWk),
                    "支承口径 wk 与 V31 启动默认一致");
        Assert.That(s.PanelWindKpa, Is.EqualTo(FirstPage.ExpectedPanelWk),
                    "面板口径 wk 与 V31 启动默认一致");
        Assert.That(s.PanelWindKpa, Is.GreaterThan(s.WindStandardKpa),
                    "面板口径不折减 ψ，故应大于支承口径");
    }

    [Test]
    public void 第一页未算时_下游一律拒绝()
    {
        // 造一个「刚进软件、第一页还没点」的会话：门禁位为 false
        var wiz = new CalcWizardViewModel();
        var s = wiz.Session;
        s.EnterMenu("玻璃幕墙", "全隐框");   // 内部会把 WindStandardResolved 清零
        Assert.That(s.WindStandardResolved, Is.False, "切菜单后必须重新从第一页算");

        // 横梁：WindStandardKpa 的初值是 1.0，旧写法 `> 0` 会让门禁永远为真
        var beam = new BeamCalcViewModel(s, wiz);
        Assert.That(beam.PreviewEnabled, Is.False, "横梁屏不许拿初值 1.0 出结果");
        Assert.That(beam.PreviewBlockedReason, Is.EqualTo(CalcSession.UpstreamNotReadyText));

        // 单位梁
        var ub = new UnitBeamCalcViewModel(s, wiz);
        Assert.That(ub.PreviewEnabled, Is.False, "单位梁屏不许拿初值 1.0 出结果");

        // 玻璃（面板口径）
        var glass = new GlassCalcViewModel(s, wiz);
        Assert.That(glass.PreviewEnabled, Is.False, "玻璃屏不许拿初值出结果");

        // 连接 / 埋件 / 胶类
        Assert.That(new ConnectCalcViewModel(s, wiz).PreviewEnabled, Is.False);
        Assert.That(new EmbedCalcViewModel(s, wiz).PreviewEnabled, Is.False);
        var sealant = new SealantCalcViewModel(s, wiz);
        Assert.That(sealant.PreviewEnabled, Is.False);
        Assert.That(sealant.BuildInputs().WkPanelKpa, Is.EqualTo(0),
                    "门禁未开时不得退回 1.37 之类的常量");
    }

    [Test]
    public void 第一页算完后_下游全部放行()
    {
        var (s, wiz) = FirstPage.Run();

        Assert.That(new ConnectCalcViewModel(s, wiz).PreviewEnabled, Is.True);
        Assert.That(new EmbedCalcViewModel(s, wiz).PreviewEnabled, Is.True);
        Assert.That(new SealantCalcViewModel(s, wiz).PreviewEnabled, Is.True);
    }

    [Test]
    public void 各屏输入里的wk与第一页同值()
    {
        var (s, wiz) = FirstPage.Run();

        // 支承口径：连接 / 埋件 三屏共用同一个 wk（单位换算：kPa → N/mm²）
        var conn = new ConnectCalcViewModel(s, wiz).BuildInputs();
        var embed = new EmbedCalcViewModel(s, wiz).BuildInputs();
        Assert.That(conn.Wk, Is.EqualTo(s.WindStandardKpa / 1000.0));
        Assert.That(embed.WkKpa, Is.EqualTo(s.WindStandardKpa));

        // 面板口径：胶类用 PanelWindKpa，不是支承值
        var seal = new SealantCalcViewModel(s, wiz).BuildInputs();
        Assert.That(seal.WkPanelKpa, Is.EqualTo(s.PanelWindKpa));
        Assert.That(seal.WkPanelKpa, Is.Not.EqualTo(s.WindStandardKpa));
    }

    [Test]
    public void 分格尺寸也来自第一页()
    {
        var (s, wiz) = FirstPage.Run();
        Assert.That(s.ColumnBayWidthMm, Is.GreaterThan(0), "立柱从属宽由第一页算");
        Assert.That(s.BayColLeft, Is.GreaterThan(0), "左分格宽由第一页算");

        // 连接/埋件的跨度与层高取自第一页填的分格，不是面板自带默认
        var conn = new ConnectCalcViewModel(s, wiz).BuildInputs();
        Assert.That(conn.PanelWidth, Is.EqualTo(s.ColumnBayWidthMm));
        Assert.That(conn.StoryHeight, Is.EqualTo(s.BeamUpperGridHeightMm));
    }

    /// <summary>
    /// 横梁屏「下片厚度」的可用性由**第一页的玻璃构型**决定（用户口径）：
    /// 单片只有一片 ⇒ 禁用；中空/夹层有两片 ⇒ 启用。
    /// 同时上片厚度在 V31 恒为 en=1，任何时候都必须可改。
    /// </summary>
    [Test]
    public void 玻璃厚度可配置性跟随第一页构型()
    {
        var (s, wiz) = FirstPage.Run();

        // ── 单片：下片禁用 ──
        s.GlassMono = true;
        var mono = new BeamCalcViewModel(s, wiz);
        Assert.That(mono.GlassInnerEnabled, Is.False, "单片玻璃只有一片，下片必须禁用");
        Assert.That(mono.GlassInnerDisabledReason, Is.Not.Empty, "禁用时要给出原因");

        // ── 中空：两片 ⇒ 下片启用 ──
        s.GlassInsulated = true;
        var insul = new BeamCalcViewModel(s, wiz);
        Assert.That(insul.GlassInnerEnabled, Is.True, "中空玻璃两片，下片应可改");
        Assert.That(insul.GlassInnerDisabledReason, Is.Empty);

        // ── 夹层：两片 ⇒ 下片启用 ──
        s.GlassLaminated = true;
        Assert.That(new BeamCalcViewModel(s, wiz).GlassInnerEnabled, Is.True, "夹层玻璃两片，下片应可改");

        // 上片任何时候都可改（V31 en=1）
        Assert.That(mono.GlassOuterIndex, Is.InRange(0, 9));
        Assert.That(insul.GlassOuterIndex, Is.InRange(0, 9));
    }

    [Test]
    public void 玻璃厚度初值取自第一页()
    {
        var (s, wiz) = FirstPage.Run();
        s.GlassOuterThicknessMm = 8;
        s.GlassInnerThicknessMm = 10;
        s.GlassInsulated = true;

        var vm = new BeamCalcViewModel(s, wiz);   // 构造时 SyncGlassLayers
        Assert.That(vm.GlassOuterThicknessText, Is.EqualTo("8"));
        Assert.That(vm.GlassInnerThicknessText, Is.EqualTo("10"));
    }
}
