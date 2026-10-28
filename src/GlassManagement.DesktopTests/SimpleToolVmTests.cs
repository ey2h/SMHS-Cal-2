using System.Collections.Generic;
using System.Linq;
using GlassManagement.Desktop.ViewModels.Calc;
using NUnit.Framework;

namespace GlassManagement.DesktopTests;

/// <summary>
/// 独立工具线（B8）界面接线守护：15 个工具共用 <see cref="SimpleToolViewModel"/> + SimpleToolView。
///
/// <para><b>为什么需要这组测试</b>：这 15 个引擎早就完成并有单元测试，缺的只是桌面接界面。
/// 这里守的是「接得对不对」——每个 VM 必须①能实例化②字段清单非空③点计算能出数
/// ④默认算例出的是该工具的 V31 基准值（不是 0、不是异常文本）。</para>
///
/// <para>引擎算法本身的正确性由 <c>GlassManagement.Calc.Tests</c> 里对应 *EngineTests 负责，
/// 本文件不重复验证公式，只验证界面 → 引擎 → 输出这条链路通。</para>
/// </summary>
[TestFixture]
public class SimpleToolVmTests
{
    /// <summary>15 个工具：标题 + 默认算例输出里必须出现的关键片段。</summary>
    private static IEnumerable<TestCaseData> AllTools()
    {
        yield return Case(new WeightPerMeterToolViewModel(), "型材米重计算", "2.7 kg/m");
        yield return Case(new LouverVentToolViewModel(), "百叶窗通风面积计算", "0.960 m²");
        yield return Case(new OpeningVentToolViewModel(), "开启扇通风面积计算", "2.7 m²");
        yield return Case(new ThermalExpansionToolViewModel(), "型（板）材热应力伸缩量计算", "5.52 mm");
        yield return Case(new ThermalStressToolViewModel(), "型材热应力计算", "128.8 MPa");
        yield return Case(new SmokeVentToolViewModel(), "排烟面积计算", "m²");
        yield return Case(new SelfTappingScrewToolViewModel(), "自攻钉及自攻自钻螺钉计算", "As =");
        yield return Case(new ThreadToolViewModel(), "型材螺纹计算（先在母材上制出螺纹）", "抗拉承载力");
        yield return Case(new PinBoltToolViewModel(), "钢销/螺栓(扭+剪)计算", "最大单销剪力");
        yield return Case(new BoltGroupToolViewModel(), "螺栓群计算", "Σr²");
        yield return Case(new AnchorPlateToolViewModel(), "埋件厚度计算", "所需承压面积");
        yield return Case(new RebarAnchorToolViewModel(), "建筑植筋计算", "最大单根拉力");
        yield return Case(new MultiPointLockToolViewModel(), "多点锁及开启扇扇框计算", "扇框强度");
        yield return Case(new CornerColumnToolViewModel(), "转角立柱计算", "组合一");
        yield return Case(new WeldToolViewModel(), "焊缝强度计算", "有效厚度");
    }

    private static TestCaseData Case(SimpleToolViewModel vm, string title, string expect)
        => new TestCaseData(vm, title, expect).SetName($"工具_{title}");

    [TestCaseSource(nameof(AllTools))]
    public void 默认算例能算出基准输出(SimpleToolViewModel vm, string title, string expect)
    {
        Assert.That(vm.Title, Is.EqualTo(title), "标题须与 V31 菜单项一致");
        Assert.That(vm.Fields.Count, Is.GreaterThan(0), "字段清单不能为空");

        vm.CalcCommand.Execute(null);

        Assert.That(vm.Output, Is.Not.Empty, $"{title} 点计算后输出为空");
        Assert.That(vm.Output, Does.Not.Contain("计算失败"), $"{title} 计算抛异常：{vm.Output}");
        Assert.That(vm.Output, Does.Contain(expect), $"{title} 输出缺少基准片段「{expect}」\n{vm.Output}");
    }

    [Test]
    public void 十五个工具全部接线()
    {
        var titles = new[]
        {
            "焊缝强度计算", "螺栓群计算", "埋件厚度计算", "建筑植筋计算", "钢销/螺栓(扭+剪)计算",
            "型材螺纹计算（先在母材上制出螺纹）", "自攻钉及自攻自钻螺钉计算", "排烟面积计算",
            "开启扇通风面积", "百叶窗通风面积", "型材米重计算", "型材热应力计算",
            "型（板）材热应力伸缩量计算", "多点锁及开启扇扇框计算", "转角立柱计算",
        };
        var catalog = GlassManagement.Desktop.Views.Calc.CalcFormCatalog.All
            .Select(e => e.Title).ToHashSet();
        var missing = titles.Where(t => !catalog.Contains(t)).ToArray();
        Assert.That(missing, Is.Empty, "以下工具未接进 CalcFormCatalog：" + string.Join("、", missing));
    }

    [Test]
    public void 空输入不抛异常且按默认值计算()
    {
        var vm = new WeightPerMeterToolViewModel();
        foreach (var f in vm.Fields) f.Text = "";
        vm.CalcCommand.Execute(null);
        Assert.That(vm.Output, Does.Not.Contain("计算失败"));
        Assert.That(vm.Output, Does.Contain("2.7 kg/m"), "空输入应回落引擎默认算例");
    }

    [Test]
    public void 材料下拉切换改变密度取值()
    {
        var vm = new WeightPerMeterToolViewModel();
        var mat = vm.Fields[0];
        Assert.That(mat.HasOptions, Is.True);
        Assert.That(mat.Options, Does.Contain("钢材").And.Contain("铝材"));

        mat.Text = "钢材";
        vm.CalcCommand.Execute(null);
        Assert.That(vm.Output, Does.Contain("7.85 kg/m"), "钢材（ρ=7850）应得 7.85 kg/m");
    }
}
