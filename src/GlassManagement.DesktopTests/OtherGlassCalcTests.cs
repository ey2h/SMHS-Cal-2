using GlassManagement.Desktop.ViewModels.Calc;
using NUnit.Framework;

namespace GlassManagement.DesktopTests;

/// <summary>
/// 「其它玻璃计算」面板（V31 菜单「其它计算 → 其它玻璃计算」id=168）。
///
/// 实机实测（2026-10-25，四玻外夹层双中空、1000×2000、4×6mm、wk=1.0KPa、αmax=0.16）：
///   外片 8.833 / 次外片 8.833 / 次内片 7.217 / 内片 7.217
///   挠度 2.356 / 容许值 16.667mm(四边简支) / 36 星分隔
/// </summary>
[TestFixture]
public class OtherGlassCalcTests
{
    private const string Sep36 = "************************************";

    private static string[] Render(Action<OtherGlassCalcViewModel>? setup = null)
    {
        var vm = new OtherGlassCalcViewModel();
        setup?.Invoke(vm);
        vm.PreviewCommand.Execute(null);
        var list = new string[vm.Rows.Count];
        for (int i = 0; i < list.Length; i++) list[i] = vm.Rows[i].Text;
        return list;
    }

    [Test]
    public void 四玻外夹层双中空_屏上数值逐位()
    {
        var got = Render();
        Assert.That(got, Has.Some.EqualTo("玻璃部分风荷载标准值为:1KPa"));
        Assert.That(got, Has.Some.EqualTo("外片玻璃强度计算值:8.833MPa"));
        Assert.That(got, Has.Some.EqualTo("次外片玻璃强度计算值:8.833MPa"));
        Assert.That(got, Has.Some.EqualTo("次内片玻璃强度计算值:7.217MPa"));
        Assert.That(got, Has.Some.EqualTo("内片玻璃强度计算值:7.217MPa"));
        Assert.That(got, Has.Some.EqualTo("玻璃挠度计算值:2.356mm"));
    }

    [Test]
    public void 挠度容许值_文案无选用二字且带四边简支后缀()
    {
        // 实测原文是「玻璃挠度容许值:16.667mm(四边简支)」，
        // 比常规幕墙玻璃屏少了「选用」二字 —— 两个模块文案不同。
        var got = Render();
        Assert.That(got, Has.Some.EqualTo("玻璃挠度容许值:16.667mm(四边简支)"));
        Assert.That(got, Has.None.Contains("选用玻璃挠度容许值"));
    }

    [Test]
    public void 分隔符是36星_与常规三屏都不同()
    {
        var got = Render();
        Assert.That(got[0], Is.EqualTo(Sep36));
        Assert.That(got[got.Length - 1], Is.EqualTo(Sep36));
        Assert.That(Sep36, Has.Length.EqualTo(36));
    }

    [Test]
    public void 强设设计值文案无片号前缀()
    {
        // 常规幕墙三片构型写「选用的1#玻璃强度设计值」，本模块写「选用的玻璃强度设计值」
        var got = Render();
        Assert.That(got, Has.Some.EqualTo("选用的玻璃强度设计值:84MPa(钢化玻璃)"));
    }

    [Test]
    public void 四玻内夹层双中空_屏上数值逐位()
    {
        // 实测（other_glass_probe.py --inner_lam）：7.783 / 7.783 / 8.167 / 8.167、df 2.356
        var got = Render(vm => { vm.IsInnerLaminated = true; });
        Assert.That(got, Has.Some.EqualTo("外片玻璃强度计算值:7.783MPa"));
        Assert.That(got, Has.Some.EqualTo("次外片玻璃强度计算值:7.783MPa"));
        Assert.That(got, Has.Some.EqualTo("次内片玻璃强度计算值:8.167MPa"));
        Assert.That(got, Has.Some.EqualTo("内片玻璃强度计算值:8.167MPa"));
        Assert.That(got, Has.Some.EqualTo("玻璃挠度计算值:2.356mm"));
        Assert.That(got, Has.Some.EqualTo("玻璃挠度容许值:16.667mm(四边简支)"));
    }

    [Test]
    public void 多片夹层_未复刻时给提示不崩()
    {
        var vm = new OtherGlassCalcViewModel { IsMultiLaminated = true };
        vm.PreviewCommand.Execute(null);
        Assert.That(vm.StatusIsWarning, Is.True);
        Assert.That(vm.StatusText, Does.Contain("多片夹层"));
        Assert.That(vm.Rows, Is.Empty);
    }

    [Test]
    public void 三种构型互斥()
    {
        var vm = new OtherGlassCalcViewModel { IsInnerLaminated = true };
        Assert.That(vm.IsOuterLaminated, Is.False);
        Assert.That(vm.IsMultiLaminated, Is.False);
        vm.IsMultiLaminated = true;
        Assert.That(vm.IsOuterLaminated, Is.False);
        Assert.That(vm.IsInnerLaminated, Is.False);
    }

    [Test]
    public void 改尺寸后结果失效_需重新预演()
    {
        var vm = new OtherGlassCalcViewModel();
        vm.PreviewCommand.Execute(null);
        Assert.That(vm.Rows, Is.Not.Empty);
        vm.WidthMm = 1200;
        Assert.That(vm.Rows, Is.Empty);
        Assert.That(vm.StatusText, Does.Contain("重新预演"));
    }
}
