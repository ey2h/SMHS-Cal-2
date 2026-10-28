using System.Collections.Generic;
using System.Linq;
using System.Text;
using GlassManagement.Desktop.ViewModels.Calc;
using NUnit.Framework;

namespace GlassManagement.DesktopTests;

/// <summary>
/// 隐框幕墙全流程 × 多参数矩阵：第一页（地区/地形/标高）→「下一步」→ 立柱屏「预演」。
/// 验证「填地区参数、选不同选项」时计算链确实生效、结果随参数变化，并输出过程参数留痕（Z / MuZ）。
/// 铁律：参数一律经第一页真实链路算出（不手工写 wk）。
/// </summary>
[TestFixture]
public class ImplicitFrameFlowMatrixTests
{
    private static readonly (string Region, char Terrain, string Level)[] Cases =
    {
        ("北京", 'C', "100"),   // V31 启动默认 → wk 1.285（未选地区回落 0.45）
        ("上海", 'C', "100"),
        ("广州", 'C', "100"),
        ("北京", 'A', "100"),
        ("北京", 'B', "100"),
        ("北京", 'D', "100"),
        ("北京", 'C', "50"),
        ("北京", 'C', "150"),
    };

    [Test]
    public void 隐框幕墙_多参数_第一页到立柱_结果矩阵()
    {
        var sb = new StringBuilder();
        sb.AppendLine("地区\t地形\t标高\twk(kPa)\tZ\tMuZ\t立柱输出长度");
        var wks = new List<double>();
        foreach (var (region, terrain, level) in Cases)
        {
            var wiz = new CalcWizardViewModel();
            var (wk, colText, chainZ, chainMuZ) = wiz.RunFlow(region, terrain, level);
            wks.Add(wk);
            sb.AppendLine($"{region}\t{terrain}\t{level}\t{wk:0.######}\t{chainZ:0.###}\t{chainMuZ:0.######}\t{colText.Length}");
            Assert.That(colText, Is.Not.Empty, $"{region}/{terrain}/{level} 立柱预演无输出（链路断了）");
            Assert.That(wk, Is.GreaterThan(0), $"{region}/{terrain}/{level} wk 非正");
            // 标高必须真的进入计算链
            Assert.That(chainZ, Is.EqualTo(double.Parse(level)).Within(1e-6),
                $"{region}/{terrain}/{level}：计算标高 Z 未按输入生效（实际 {chainZ}）");
        }
        TestContext.Out.WriteLine(sb.ToString());

        Assert.That(wks.Distinct().Count(), Is.GreaterThan(1),
            "不同参数下 wk 完全相同 → 第一页参数未进入计算链");
    }

    [Test]
    public void 隐框幕墙_未选地区_回落北京0p45_wk等于V31默认()
    {
        // V31 启动默认：地区未选 → 回落北京 0.45kPa / C 类 / 标高 100 → 支承口径 wk 1.285
        var (wk, colText, _, _) = new CalcWizardViewModel().RunFlow("", 'C', "100");
        Assert.That(wk, Is.EqualTo(FirstPage.ExpectedSupportWk).Within(1e-6));
        Assert.That(colText, Is.Not.Empty);
    }

    [Test]
    public void 隐框幕墙_显式选北京_记录实际wk供V31核对()
    {
        var wiz = new CalcWizardViewModel();
        var (wk, colText, _, _) = wiz.RunFlow("北京", 'C', "100");
        TestContext.Out.WriteLine($"显式选北京/C/100m → wk={wk:0.######}（V31 需实机核对；未选地区时回落 0.45 得 1.285）");
        Assert.That(wk, Is.GreaterThan(1.0));
        Assert.That(colText, Is.Not.Empty);
    }

    [Test]
    public void 隐框幕墙_地形A应大于D_标高150应大于50和100()
    {
        var (wkA, _, _, _) = new CalcWizardViewModel().RunFlow("北京", 'A', "100");
        var (wkD, _, _, _) = new CalcWizardViewModel().RunFlow("北京", 'D', "100");
        Assert.That(wkA, Is.GreaterThan(wkD), "A 类地形 μz 更大 → wk 应大于 D 类");

        var (wk50, _, _, _) = new CalcWizardViewModel().RunFlow("北京", 'C', "50");
        var (wk100, _, _, _) = new CalcWizardViewModel().RunFlow("北京", 'C', "100");
        var (wk150, _, _, _) = new CalcWizardViewModel().RunFlow("北京", 'C', "150");
        Assert.That(wk100, Is.GreaterThan(wk50), "标高 100m 的 μz 更大 → wk 应大于 50m");
        Assert.That(wk150, Is.GreaterThan(wk100), "标高 150m 的 μz 更大 → wk 应大于 100m");
    }
}
