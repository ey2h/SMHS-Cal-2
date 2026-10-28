using GlassManagement.Calc.Engines;
using GlassManagement.Desktop.ViewModels.Calc;
using NUnit.Framework;

namespace GlassManagement.DesktopTests;

/// <summary>
/// 埋件屏「土建预埋参数 / 焊缝和转接件参数」两帧的可编辑性守卫。
///
/// <para>用户口径：<b>部分参数在后续界面仍可调整，不能写死</b>。这组用例把
/// V31 实机 <c>en=1</c> 的控件逐个钉住，并锁住实测出来的联动关系：</para>
/// <list type="bullet">
///   <item>钢筋层数 × 每层筋数 → 锚筋根数（实机 2层×2=4；旧实现只用了一个下拉）；</item>
///   <item>锚筋材质 → 锚筋外型（HPB300 光圆 α=0.16 / HRB400 带肋 α=0.14）；</item>
///   <item>e0 一个输入同时进弯矩力臂与转接件悬臂；</item>
///   <item>转接件规格 + 抵抗矩 + 惯性矩 + 截面积四个下拉共用一个行号。</item>
/// </list>
/// </summary>
[TestFixture]
public class EmbedPageTests
{
    private static EmbedCalcViewModel NewVm(out CalcSession s)
    {
        var (session, wiz) = FirstPage.Run();
        s = session;
        return new EmbedCalcViewModel(session, wiz);
    }

    [Test]
    public void 候选表与实机下拉同规模()
    {
        var vm = NewVm(out _);
        Assert.That(vm.RebarLayers, Has.Count.EqualTo(3));
        Assert.That(vm.ConcreteGrades, Has.Count.EqualTo(13));
        Assert.That(vm.RebarDiameters, Has.Count.EqualTo(14));
        Assert.That(vm.RebarPerLayerItems, Has.Count.EqualTo(3));
        Assert.That(vm.RebarMaterials, Has.Count.EqualTo(2), "实机只有 HPB300 / HRB400");
        Assert.That(vm.PlateThicknessItems, Has.Count.EqualTo(10));
        Assert.That(vm.RebarShapes, Has.Count.EqualTo(2));
        Assert.That(vm.SteelSections, Has.Count.EqualTo(20));
        Assert.That(vm.TransferModulusItems, Has.Count.EqualTo(20));
        Assert.That(vm.TransferInertiaItems, Has.Count.EqualTo(20));
        Assert.That(vm.TransferAreaItems, Has.Count.EqualTo(20));
        Assert.That(vm.WeldLegs, Has.Count.EqualTo(7));
    }

    [Test]
    public void 默认值与V31实机显示一致()
    {
        var vm = NewVm(out _);
        Assert.That(vm.RebarLayerIndex, Is.EqualTo(0), "2层");
        Assert.That(vm.ConcreteGradeIndex, Is.EqualTo(1), "C25");
        Assert.That(vm.RebarDiameterIndex, Is.EqualTo(2), "12");
        Assert.That(vm.RebarPerLayerIndex, Is.EqualTo(0), "2");
        Assert.That(vm.RebarMaterialIndex, Is.EqualTo(0), "HPB300");
        Assert.That(vm.PlateThicknessIndex, Is.EqualTo(1), "8");
        Assert.That(vm.WeldLegIndex, Is.EqualTo(2), "6");
        Assert.That(vm.SteelSectionIndex, Is.EqualTo(0), "10#槽钢");
        Assert.That(vm.RebarLengthText, Is.EqualTo("200"));
        Assert.That(vm.AnchorZText, Is.EqualTo("110"));
        Assert.That(vm.PlateAreaText, Is.EqualTo("60000"));
        Assert.That(vm.EccentricityText, Is.EqualTo("80"));
        Assert.That(vm.WeldHorizontalText, Is.EqualTo("50"));
        Assert.That(vm.WeldVerticalText, Is.EqualTo("100"));
        Assert.That(vm.HasHook, Is.True);
        Assert.That(vm.IsLWeld, Is.True);
    }

    [Test]
    public void 材质驱动锚筋外型()
    {
        var vm = NewVm(out _);
        Assert.That(vm.RebarShapeIndex, Is.EqualTo(0), "HPB300 → 光圆");

        vm.RebarMaterialIndex = 1;
        Assert.That(vm.RebarShapeIndex, Is.EqualTo(1), "HRB400 → 带肋");

        var x = vm.BuildInputs();
        Assert.That(x.RebarFy, Is.EqualTo(300));
        Assert.That(x.AnchorAlpha, Is.EqualTo(0.14).Within(1e-9));

        vm.RebarMaterialIndex = 0;
        x = vm.BuildInputs();
        Assert.That(x.RebarFy, Is.EqualTo(270));
        Assert.That(x.AnchorAlpha, Is.EqualTo(0.16).Within(1e-9));
    }

    [Test]
    public void 锚筋根数由层数乘每层筋数()
    {
        var vm = NewVm(out _);
        Assert.That(vm.BuildInputs().RebarCount, Is.EqualTo(4), "2层 × 2 = 4（实机 452.16mm² = 4根φ12）");

        vm.RebarLayerIndex = 2;                  // 4层
        vm.RebarPerLayerIndex = 2;               // 4 根
        Assert.That(vm.BuildInputs().RebarCount, Is.EqualTo(16));

        // 实际锚筋面积 = π·d²/4 × 根数（程序 π 取 3.14）
        var r = EmbedEngine.Compute(vm.BuildInputs());
        Assert.That(r.RebarAreaActual.ToString("F3"), Is.EqualTo(1808.64.ToString("F3")));
    }

    [Test]
    public void e0一个输入同时进弯矩与悬臂()
    {
        var vm = NewVm(out _);
        var a = vm.BuildInputs();
        Assert.That(a.MomentArmMm, Is.EqualTo(80));
        Assert.That(a.CantileverMm, Is.EqualTo(80), "实机只有一个 e0 框，挠度悬臂取同一个值");

        vm.EccentricityText = "100";
        var b = vm.BuildInputs();
        Assert.That(b.MomentArmMm, Is.EqualTo(100));
        Assert.That(b.CantileverMm, Is.EqualTo(100));

        // 六个输出同时跟着变（V31 扰动实测值）
        var rb = EmbedEngine.Compute(b);
        Assert.That(rb.MomentDesign.ToString("F0"), Is.EqualTo("171600"));
        Assert.That(rb.TransferDeflection.ToString("0.######"), Is.EqualTo("0.00054"));
        Assert.That(rb.TransferDeflLimit.ToString("F3"), Is.EqualTo(0.8.ToString("F3")));
        Assert.That(rb.WeldStress.ToString("F3"), Is.EqualTo(14.152.ToString("F3")));
    }

    [Test]
    public void 转接件四个下拉共用一个行号()
    {
        var vm = NewVm(out _);
        // 第 0 行 10#槽钢 → A 1274.8 / I 198 / W 39.7（与参数库「槽钢［10」一致）
        Assert.That(vm.TransferAreaItems[0], Is.EqualTo("1274.8"));
        Assert.That(vm.TransferInertiaItems[0], Is.EqualTo("198"));
        Assert.That(vm.TransferModulusItems[0], Is.EqualTo("39.7"));

        // 改行号后三个候选值应在同一行一起换（实机 12#槽钢 1536.2/346/57.7）
        vm.SteelSectionIndex = 1;
        Assert.That(vm.TransferAreaItems[1], Is.EqualTo("1536.2"));
        Assert.That(vm.TransferInertiaItems[1], Is.EqualTo("346"));
        Assert.That(vm.TransferModulusItems[1], Is.EqualTo("57.7"));

        var x = vm.BuildInputs();
        Assert.That(x.TransferArea, Is.EqualTo(1536.2));
        Assert.That(x.TransferInertia, Is.EqualTo(3460000));
        Assert.That(x.TransferSectionModulus, Is.EqualTo(57700));

        // 转接件强度与挠度随行号变化（实机 4.43→3.444 / 0.000276→0.000158）
        var r = EmbedEngine.Compute(x);
        Assert.That(r.TransferStress.ToString("F3"), Is.EqualTo(3.444.ToString("F3")));
        Assert.That(r.TransferDeflection.ToString("0.######"), Is.EqualTo("0.000158"));
    }

    [Test]
    public void 玻璃厚度与焊缝长都可改不写死()
    {
        var vm = NewVm(out _);
        vm.WeldHorizontalText = "60";
        vm.WeldVerticalText = "120";
        var x = vm.BuildInputs();
        Assert.That(x.WeldHorizontalMm, Is.EqualTo(60));
        Assert.That(x.WeldVerticalMm, Is.EqualTo(120));

        // V31 扰动实测：Lh 50→60 焊缝 12.452→11.761；Lv 100→120 →9.596
        var lh = EmbedEngine.Compute(EmbedEngine.DefaultInputs() with { WeldHorizontalMm = 60 });
        Assert.That(lh.WeldStress.ToString("F3"), Is.EqualTo(11.761.ToString("F3")));
        var lv = EmbedEngine.Compute(EmbedEngine.DefaultInputs() with { WeldVerticalMm = 120 });
        Assert.That(lv.WeldStress.ToString("F3"), Is.EqualTo(9.596.ToString("F3")));
    }
}
