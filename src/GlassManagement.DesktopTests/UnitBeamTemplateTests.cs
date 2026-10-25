using NUnit.Framework;
using GlassManagement.Desktop.ViewModels.Calc;

namespace GlassManagement.DesktopTests;

/// <summary>
/// 单元式横梁屏「输出逐行一致」门禁。
///
/// <para>期望值来自 V31 实机 RichText 原文
/// <c>local-data/recon/v31runs/py_u_mid.txt</c>（单元式 + 有中横，B=1100、上分格高 1000、
/// wk=1.285、单片玻璃自重 300 N/m²、6063-T5、北京 αmax=0.16），
/// 槽数组 <c>0x1C911A0</c> 直读互证。断言到<b>每一个字符</b>，包括 35/36 星分隔符的差异。</para>
///
/// <para>注意 1：<see cref="UnitBeamCalcViewModel.ShowTrace"/> 默认 false —— V31 结果区没有
/// 「[取证]」行，打开它就不再逐行一致，所以这里也不开。</para>
/// <para>注意 2：<c>CalcSession.Default</c> 是静态单例，两个 VM 实例共享同一会话。
/// 因此每个用例必须「建 → 立刻渲染 → 取快照」，不能先建两个再分别渲染。</para>
/// </summary>
[TestFixture]
public class UnitBeamTemplateTests
{
    private const string Sep35 = "***********************************";
    private const string Sep36 = "************************************";
    private const string Dash = "——————————————————";

    /// <summary>无中横输出行数（17 个数字 / 35 行）。</summary>
    private const int NoMidLines = 35;

    private static string[] RenderOnce(bool mid, double hMm)
    {
        var wiz = new CalcWizardViewModel();
        var s = wiz.Session;
        s.WindStandardKpa = 1.285;
        s.BeamLeftBayMm = 1100;
        s.BeamRightBayMm = 1100;
        s.BeamUpperGridHeightMm = hMm;
        s.BeamMaterial = "6063-T5";
        s.GlassMono = true;              // 单片玻璃 → 横梁自重 300 N/m²
        s.IgnoreSeismic = false;
        s.TransomMid = mid;
        s.TransomNone = !mid;

        var vm = new UnitBeamCalcViewModel(s, wiz);
        vm.PreviewCommand.Execute(null);

        var list = new string[vm.Results.Count];
        for (int i = 0; i < list.Length; i++) list[i] = vm.Results[i].Text;
        return list;
    }

    private static readonly string[] ExpectedNoMid =
    {
        Sep35,
        "风荷载标准值为:1.285KPa",
        Sep35,
        "横梁单元上横框部分抗弯强度计算值:36.301MPa",
        "选用铝型材的抗弯强度设计值为:90MPa",
        "满足要求(√)",
        Dash,
        "横梁单元下横框部分抗弯强度计算值:20.184MPa",
        "选用铝型材的抗弯强度设计值为:90MPa",
        "满足要求(√)",
        Sep35,
        "组合材料的挠度水平向计算值:0.996mm",
        "按规范的水平向挠度允许值:6.111mm",
        "满足要求(√)",
        Dash,
        "垂直向挠度计算值:0.706mm",
        "按规范的垂直向挠度允许值:4.4mm",
        "满足要求(√)",
        Sep36,
        "横梁单元上横框部分抗剪强度计算值tx:3.03MPa",
        "选用铝型材的抗剪强度设计值为:55MPa",
        "满足要求(√)",
        Dash,
        "横梁单元上横框部分抗剪强度计算值ty:1.109MPa",
        "选用铝型材的抗剪强度设计值为:55MPa",
        "满足要求(√)",
        Sep36,
        "横梁单元下横框部分抗剪强度计算值tx:3.03MPa",
        "选用铝型材的抗剪强度设计值为:55MPa",
        "满足要求(√)",
        Dash,
        "横梁单元下横框部分抗剪强度计算值ty:0MPa",
        "选用铝型材的抗剪强度设计值为:55MPa",
        "满足要求(√)",
        Sep36,
    };

    /// <summary>中横框追加段 20 行（5 个数字）。★ 中横抗弯后的分隔符回到 35 星。</summary>
    private static readonly string[] ExpectedMidBlock =
    {
        "横梁中横框部分抗弯强度计算值:62.147MPa",
        "选用铝型材的抗弯强度设计值为:90MPa",
        "满足要求(√)",
        Sep35,
        "横梁中横框挠度水平向计算值:1.992mm",
        "按规范的水平向挠度允许值:6.111mm",
        "满足要求(√)",
        Dash,
        "横梁中横框挠度垂直向计算值:0.952mm",
        "按规范的垂直向挠度允许值:4.4mm",
        "满足要求(√)",
        Sep36,
        "横梁中横框部分抗剪强度计算值tx:6.059MPa",
        "选用铝型材的抗剪强度设计值为:55MPa",
        "满足要求(√)",
        Dash,
        "横梁中横框部分抗剪强度计算值ty:1.497MPa",
        "选用铝型材的抗剪强度设计值为:55MPa",
        "满足要求(√)",
        Sep36,
    };

    [Test]
    public void 无中横_输出17量_逐行()
    {
        Assert.That(RenderOnce(mid: false, hMm: 1000), Is.EqualTo(ExpectedNoMid));
    }

    [Test]
    public void 有中横_追加中横框5量_逐行()
    {
        var got = RenderOnce(mid: true, hMm: 1000);
        Assert.That(got, Has.Length.EqualTo(NoMidLines + ExpectedMidBlock.Length));
        Assert.That(got.Take(NoMidLines).ToArray(), Is.EqualTo(ExpectedNoMid),
                    "中横是独立追加段，上下框 17 量必须与无中横逐行相同");
        Assert.That(got.Skip(NoMidLines).ToArray(), Is.EqualTo(ExpectedMidBlock));
    }

    [Test]
    public void 上分格高变化_只影响竖向量_水平量一字不变()
    {
        var a = RenderOnce(mid: true, hMm: 1000);     // V31 实机：36.301 / 0.706 / τy上 1.109
        var b = RenderOnce(mid: true, hMm: 1380);     // V31 实机：42.417 / 0.974 / τy上 1.53

        Assert.That(a[3], Is.EqualTo("横梁单元上横框部分抗弯强度计算值:36.301MPa"));
        Assert.That(b[3], Is.EqualTo("横梁单元上横框部分抗弯强度计算值:42.417MPa"));
        Assert.That(a[7], Is.EqualTo(b[7]), "下框抗弯只吃 M_y，与上分格高无关");

        Assert.That(a[11], Is.EqualTo(b[11]), "水平挠度与上分格高无关");
        Assert.That(a[15], Is.Not.EqualTo(b[15]), "垂直挠度随上分格高变");

        // 中横 5 量一字不变（自重高度是常量 1350，不吃上分格高）
        Assert.That(a.Skip(NoMidLines).ToArray(), Is.EqualTo(b.Skip(NoMidLines).ToArray()));
    }
}
