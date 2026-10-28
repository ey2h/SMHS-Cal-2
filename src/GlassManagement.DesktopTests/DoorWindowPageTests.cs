using System.Linq;
using GlassManagement.Desktop.ViewModels.Calc;
using NUnit.Framework;

namespace GlassManagement.DesktopTests;

/// <summary>
/// 门窗系统计算屏（V31 CW29 复刻 DoorWindowCalcView / DoorWindowCalcViewModel）逐位对拍。
///
/// <para><b>实机基线</b>（door_body_traces.json / door_shuting_var.json / door_conn_matrix.json /
/// door_conn_probe4a.json，2026-10-27 全选项组合遍历）：V31 默认态竖梃 σ=130.171/挠度 8.344(限16)/
/// τ=7.316、横梃 σ=110.368/水平挠度 6.887(限15)/τ=9.876、连接件 单点力 535.156/薄壁件 972(×不满足铆钉400)/
/// 结构侧 4551，输出逐字。</para>
///
/// <para>★ 铁律：杆件 wk 由第一页链产出下传（FirstPage 真算 → SetWkFromChain(w0·βgz·μz·1.168)），
/// 禁止手工赋值凑数；V31 默认链 wk杆件=0.001333 由链复现逐位。</para>
/// </summary>
[TestFixture]
public class DoorWindowPageTests
{
    private static DoorWindowCalcViewModel NewVm()
    {
        var (session, wiz) = FirstPage.Run();      // 第一页真算，wk 链下传
        session.EnterMenu("门窗", "门窗系统计算");
        var vm = new DoorWindowCalcViewModel(session, wiz);
        // 杆件 wk = round6(w0·βgz·μz·1.168)（V31 0x14184AE 链）由第一页链值产出
        var basic = new BasicParamsViewModel(session, wiz);
        basic.Recalculate();
        vm.SetWkFromChain(basic.ChainW0, basic.ChainBetaGz, basic.ChainMuZ);
        return vm;
    }

    [Test]
    public void 杆件wk_由第一页链复现V31实值()
    {
        var vm = NewVm();
        // V31 链 0x14184AE 直读 0.45×1.6929×1.4983×1.168 → round6 = 0.001333
        Assert.That(vm.WkRodNmm2, Is.EqualTo(0.001333));
        Assert.That(vm.WkGlassNmm2, Is.EqualTo(0.00137));   // 玻璃链 round6（输出 1.37KPa）
    }

    [Test]
    public void 竖梃预演_默认基线逐位()
    {
        var vm = NewVm();
        vm.GlassPreviewCommand.Execute(null);
        vm.MullionPreviewCommand.Execute(null);
        var s = string.Join(" ", vm.Results.Select(r => r.Text));
        Assert.That(s, Does.Contain("计算杆件时风荷载标准值为:1.333KPa"));
        Assert.That(s, Does.Contain("中竖挺抗弯强度计算值:130.171MPa"));
        Assert.That(s, Does.Contain("中竖挺抗弯强度设计值:90MPa"));
        Assert.That(s, Does.Contain("中竖挺挠度计算值:8.344mm"));
        Assert.That(s, Does.Contain("中竖挺挠度限值:16mm"));
        Assert.That(s, Does.Contain("中竖挺抗剪强度计算值:7.316MPa"));
        Assert.That(s, Does.Contain("中竖挺抗剪强度设计值:55MPa"));
    }

    [Test]
    public void 横梃预演_默认基线逐位()
    {
        var vm = NewVm();
        vm.GlassPreviewCommand.Execute(null);
        vm.TransomPreviewCommand.Execute(null);
        var s = string.Join(" ", vm.Results.Select(r => r.Text));
        Assert.That(s, Does.Contain("中横挺抗弯强度计算值:110.368MPa"));
        Assert.That(s, Does.Contain("中横挺水平方向挠度计算值:6.887mm"));
        Assert.That(s, Does.Contain("中横挺水平方向挠度限值:15mm"));
        Assert.That(s, Does.Contain("中横挺垂直方向挠度计算值:0mm"));
        Assert.That(s, Does.Contain("中横挺水平方向抗剪强度计算值:9.876MPa"));
    }

    [Test]
    public void 连接件预演_默认基线逐位()
    {
        var vm = NewVm();
        vm.GlassPreviewCommand.Execute(null);
        vm.ConnectorPreviewCommand.Execute(null);
        var s = string.Join(" ", vm.Results.Select(r => r.Text));
        Assert.That(s, Does.Contain("玻璃部分风荷载标准值为:1.37KPa"));
        Assert.That(s, Does.Contain("被连接的薄壁件抗剪强度计算值:535.156N"));
        Assert.That(s, Does.Contain("被连接的薄壁件抗剪强度设计值:972N"));
        Assert.That(s, Does.Contain("单个抽芯铆钉(开口10/12级)的最大抗剪承载力设计值:400N"));
        Assert.That(s, Does.Contain("连接片的抗剪强度设计值:4551N"));
    }

    [Test]
    public void 挠度限值_人工一致150_逐位()
    {
        var vm = NewVm();
        vm.GlassPreviewCommand.Execute(null);
        vm.DeflLimitModeIndex = 1;       // 人工一致
        vm.DeflLimitDivisorIndex = 1;    // 150
        vm.MullionPreviewCommand.Execute(null);
        var s = string.Join(" ", vm.Results.Select(r => r.Text));
        Assert.That(s, Does.Contain("中竖挺挠度限值:10.667mm"));   // round3(1600/150)
    }

    [Test]
    public void 连接件_卡槽连接跳过边框块()
    {
        var vm = NewVm();
        vm.GlassPreviewCommand.Execute(null);
        vm.GrooveConnection = true;
        vm.ConnectorPreviewCommand.Execute(null);
        var s = string.Join(" ", vm.Results.Select(r => r.Text));
        Assert.That(s, Does.Not.Contain("门窗边框"));
        Assert.That(s, Does.Contain("连接片的抗剪强度设计值:4551N"));
    }

    [Test]
    public void 连接件_不计算连接件_无输出()
    {
        var vm = NewVm();
        vm.GlassPreviewCommand.Execute(null);
        vm.NoCalcConnector = true;
        vm.ConnectorPreviewCommand.Execute(null);
        var s = string.Join(" ", vm.Results.Select(r => r.Text));
        Assert.That(s, Does.Not.Contain("抗剪强度"));
    }

    [Test]
    public void 连接件_有钢附框_插两块逐位()
    {
        var vm = NewVm();
        vm.GlassPreviewCommand.Execute(null);
        vm.HasSteelFrame = true;
        vm.ConnectorPreviewCommand.Execute(null);
        var s = string.Join(" ", vm.Results.Select(r => r.Text));
        Assert.That(s, Does.Contain("连接片与钢附框连接处:"));
        Assert.That(s, Does.Contain("被连接的薄壁件抗剪强度设计值:2596.28N"));
        Assert.That(s, Does.Contain("钢附框与边框连接处:"));
        // B 块（附框↔边框）两个薄壁件值：972(第一块边框侧) 与 1139.83(B)
        Assert.That(s, Does.Contain("被连接的薄壁件抗剪强度设计值:1139.83N"));
    }

    [Test]
    public void 材质切换_塑钢_挠度联动逐位()
    {
        var vm = NewVm();
        vm.GlassPreviewCommand.Execute(null);
        vm.MullionMaterial = "塑钢(仅塑材部分)";
        vm.MullionPreviewCommand.Execute(null);
        var s = string.Join(" ", vm.Results.Select(r => r.Text));
        Assert.That(s, Does.Contain("中竖挺挠度计算值:265.506mm"));   // E=2200 联动
        Assert.That(s, Does.Contain("中竖挺抗弯强度设计值:37MPa"));
    }

    [Test]
    public void 铆钉规格切换_设计值联动逐位()
    {
        var vm = NewVm();
        vm.GlassPreviewCommand.Execute(null);
        vm.ConnScrewDiameterIndex = 2;   // 4.0 → 薄壁件 1223.52 / 铆钉 850
        vm.ConnectorPreviewCommand.Execute(null);
        var s = string.Join(" ", vm.Results.Select(r => r.Text));
        Assert.That(s, Does.Contain("被连接的薄壁件抗剪强度设计值:1223.52N"));
        Assert.That(s, Does.Contain("最大抗剪承载力设计值:850N"));
    }

    [Test]
    public void 片数除数_结构侧联动逐位()
    {
        var vm = NewVm();
        vm.GlassPreviewCommand.Execute(null);
        vm.ConnStructPlateText = "2";    // 结构侧片数 1→2
        vm.ConnectorPreviewCommand.Execute(null);
        var s = string.Join(" ", vm.Results.Select(r => r.Text));
        Assert.That(s, Does.Contain("连接片的抗剪强度计算值:267.578N"));
        Assert.That(s, Does.Contain("被连接的薄壁件抗剪强度计算值:535.156N"));  // 边框侧不受影响
    }
}
