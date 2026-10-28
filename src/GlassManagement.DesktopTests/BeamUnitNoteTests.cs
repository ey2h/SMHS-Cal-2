using GlassManagement.Desktop.ViewModels.Calc;
using NUnit.Framework;

namespace GlassManagement.DesktopTests;

/// <summary>
/// 横梁屏「单位说明」弹窗。
///
/// 取证：<c>local-data/recon/v31runs/btn_beam_unit.txt</c>（2026-10-26 实机）
/// <code>
/// 按钮「单位说明」en=1 → 弹 #32770 title='单位说明：' client 144x217
/// Static (15,54) 91x82，WM_GETTEXT 原文 repr：
///   '惯性矩单位cm^4\n抵抗矩单位cm^3\n壁厚单位mm\n截面积单位mm^2\n面积矩单位cm^3'
/// Button「确定」(35,174) 88x28
/// </code>
///
/// 这些断言存在的意义：正文是 <b>5 行</b>、<c>^</c> 是半角、单位大小写混排
/// （cm/mm 小写、^2/^3/^4 数字上标写法），任何「看起来一样」的改写都会被打回。
/// </summary>
[TestFixture]
public class BeamUnitNoteTests
{
    [Test]
    public void 单位说明正文_逐字符与V31一致()
    {
        const string expected =
            "惯性矩单位cm^4\n" +
            "抵抗矩单位cm^3\n" +
            "壁厚单位mm\n" +
            "截面积单位mm^2\n" +
            "面积矩单位cm^3";
        Assert.That(BeamCalcViewModel.UnitNoteText, Is.EqualTo(expected));
    }

    [Test]
    public void 单位说明_恰好5行()
    {
        var lines = BeamCalcViewModel.UnitNoteText.Split('\n');
        Assert.That(lines.Length, Is.EqualTo(5));
        Assert.That(lines[0], Is.EqualTo("惯性矩单位cm^4"));
        Assert.That(lines[4], Is.EqualTo("面积矩单位cm^3"));
    }

    [Test]
    public void 单位说明标题_带全角冒号()
    {
        Assert.That(BeamCalcViewModel.UnitNoteTitle, Is.EqualTo("单位说明："));
        Assert.That(BeamCalcViewModel.UnitNoteTitle[^1], Is.EqualTo('：'),
                    "V31 弹窗标题用的是全角冒号，不是半角 ':'");
    }

    [Test]
    public void 打开与关闭命令_切换可见性()
    {
        var wizard = CalcWizardViewModel.Default;
        var vm = new BeamCalcViewModel(CalcSession.Default, wizard);
        Assert.That(vm.IsUnitNoteOpen, Is.False, "默认不显示");

        vm.ShowUnitNoteCommand.Execute(null);
        Assert.That(vm.IsUnitNoteOpen, Is.True);

        vm.CloseUnitNoteCommand.Execute(null);
        Assert.That(vm.IsUnitNoteOpen, Is.False);
    }

    [Test]
    public void 供绑定的属性_与常量同源()
    {
        var wizard = CalcWizardViewModel.Default;
        var vm = new BeamCalcViewModel(CalcSession.Default, wizard);
        Assert.That(vm.UnitNoteBody, Is.EqualTo(BeamCalcViewModel.UnitNoteText));
        Assert.That(vm.UnitNoteCaption, Is.EqualTo(BeamCalcViewModel.UnitNoteTitle));
    }
}
