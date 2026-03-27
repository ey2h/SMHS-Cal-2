using NUnit.Framework;
using GlassManagement.Desktop.ViewModels;

namespace GlassManagement.DesktopTests;

[TestFixture]
public class ViewModelTests
{
    private MainViewModel _vm = null!;

    [SetUp]
    public void Setup()
    {
        _vm = new MainViewModel();
    }

    // ==================== 初始值 ====================

    [Test]
    public void Test_InitialValues_ServerUrl()
    {
        Assert.That(_vm.ServerUrl, Is.EqualTo("http://localhost:5188"));
    }

    [Test]
    public void Test_InitialValues_CalcElevation()
    {
        Assert.That(_vm.CalcElevation, Is.EqualTo(50));
    }

    [Test]
    public void Test_InitialValues_CurrentStep()
    {
        Assert.That(_vm.CurrentStep, Is.EqualTo(0));
    }

    [Test]
    public void Test_InitialValues_Steps_Count()
    {
        Assert.That(_vm.Steps.Count, Is.EqualTo(9));
    }

    // ==================== 步骤导航 ====================

    [Test]
    public void Test_StepNavigation_NextStep()
    {
        _vm.NextStep();
        Assert.That(_vm.CurrentStep, Is.EqualTo(1));
    }

    [Test]
    public void Test_StepNavigation_PrevStep_AtZero()
    {
        _vm.PrevStep();
        Assert.That(_vm.CurrentStep, Is.EqualTo(0), "不能回退到负数");
    }

    [Test]
    public void Test_StepNavigation_NextThenPrev()
    {
        _vm.NextStep();
        _vm.NextStep();
        _vm.PrevStep();
        Assert.That(_vm.CurrentStep, Is.EqualTo(1));
    }

    [Test]
    public void Test_StepNavigation_CanGoPrev()
    {
        Assert.That(_vm.CanGoPrev, Is.False);
        _vm.NextStep();
        Assert.That(_vm.CanGoPrev, Is.True);
    }

    [Test]
    public void Test_StepNavigation_CanGoNext()
    {
        Assert.That(_vm.CanGoNext, Is.True);
        // Navigate to last step
        for (int i = 0; i < 8; i++) _vm.NextStep();
        Assert.That(_vm.CanGoNext, Is.False);
    }

    [Test]
    public void Test_StepNavigation_IsLastStep()
    {
        Assert.That(_vm.IsLastStep, Is.False);
        for (int i = 0; i < 8; i++) _vm.NextStep();
        Assert.That(_vm.IsLastStep, Is.True);
    }

    [Test]
    public void Test_StepNavigation_MaxBound()
    {
        for (int i = 0; i < 20; i++) _vm.NextStep();
        Assert.That(_vm.CurrentStep, Is.EqualTo(8), "不能超过最大步骤");
    }

    // ==================== 地面类型互斥 ====================

    [Test]
    public void Test_TerrainSelection_Exclusive()
    {
        _vm.TerrainA = true;
        Assert.That(_vm.TerrainA, Is.True);
        Assert.That(_vm.TerrainB, Is.False);
        Assert.That(_vm.TerrainC, Is.False);
        Assert.That(_vm.TerrainD, Is.False);

        _vm.TerrainC = true;
        Assert.That(_vm.TerrainA, Is.False);
        Assert.That(_vm.TerrainC, Is.True);
    }

    [Test]
    public void Test_TerrainSelection_B()
    {
        _vm.TerrainB = true;
        Assert.That(_vm.TerrainB, Is.True);
        Assert.That(_vm.TerrainA, Is.False);
    }

    [Test]
    public void Test_TerrainSelection_D()
    {
        _vm.TerrainD = true;
        Assert.That(_vm.TerrainD, Is.True);
        Assert.That(_vm.TerrainA, Is.False);
        Assert.That(_vm.TerrainB, Is.False);
        Assert.That(_vm.TerrainC, Is.False);
    }

    // ==================== 幕墙类型 ====================

    [Test]
    public void Test_CurtainWallTypeChange()
    {
        _vm.CurtainWallTypeIndex = 0;
        Assert.That(_vm.Step1Title, Does.Contain("全隐框玻璃"));

        _vm.CurtainWallTypeIndex = 2;
        Assert.That(_vm.Step1Title, Does.Contain("明框玻璃"));
    }

    [Test]
    public void Test_CurtainWallTypes_Count()
    {
        Assert.That(_vm.CurtainWallTypes.Count, Is.EqualTo(6));
    }

    // ==================== 外压体型系数互斥 ====================

    [Test]
    public void Test_ExtWall_Exclusive()
    {
        _vm.ExtWallCenter = true;
        Assert.That(_vm.ExtWallCenter, Is.True);
        Assert.That(_vm.ExtWallCorner, Is.False);

        _vm.ExtWallCorner = true;
        Assert.That(_vm.ExtWallCorner, Is.True);
        Assert.That(_vm.ExtWallCenter, Is.False);
    }

    // ==================== 内压体型系数互斥 ====================

    [Test]
    public void Test_IntPressure_Exclusive()
    {
        _vm.IntSealed = true;
        Assert.That(_vm.IntSealed, Is.True);
        Assert.That(_vm.IntOpen002, Is.False);

        _vm.IntOpen010 = true;
        Assert.That(_vm.IntOpen010, Is.True);
        Assert.That(_vm.IntSealed, Is.False);
    }

    // ==================== 风荷载模式互斥 ====================

    [Test]
    public void Test_WindMode_Exclusive()
    {
        _vm.WindModeCodeMin = true;
        Assert.That(_vm.WindModeCodeMin, Is.True);
        Assert.That(_vm.WindModeCustom, Is.False);

        _vm.WindModeCustom = true;
        Assert.That(_vm.WindModeCustom, Is.True);
        Assert.That(_vm.WindModeCodeMin, Is.False);
    }

    // ==================== 计算结构形式互斥 ====================

    [Test]
    public void Test_CwStruct_Exclusive()
    {
        _vm.CwStructComponent = true;
        Assert.That(_vm.CwStructComponent, Is.True);
        Assert.That(_vm.CwStructUnit, Is.False);

        _vm.CwStructUnit = true;
        Assert.That(_vm.CwStructUnit, Is.True);
        Assert.That(_vm.CwStructComponent, Is.False);
    }

    // ==================== 输入验证 ====================

    [Test]
    public void Test_CalcElevation_Set()
    {
        _vm.CalcElevation = 100;
        Assert.That(_vm.CalcElevation, Is.EqualTo(100));
    }

    [Test]
    public void Test_ServerUrl_Set()
    {
        _vm.ServerUrl = "http://example.com:5000";
        Assert.That(_vm.ServerUrl, Is.EqualTo("http://example.com:5000"));
    }

    [Test]
    public void Test_WindLoadCustomValue_Set()
    {
        _vm.WindLoadCustomValue = 1.5;
        Assert.That(_vm.WindLoadCustomValue, Is.EqualTo(1.5));
    }

    [Test]
    public void Test_ExternalShapeValue_Set()
    {
        _vm.ExternalShapeValue = 0.8;
        Assert.That(_vm.ExternalShapeValue, Is.EqualTo(0.8));
    }

    [Test]
    public void Test_ThemeIndex_Set()
    {
        _vm.ThemeIndex = 1;
        Assert.That(_vm.ThemeIndex, Is.EqualTo(1));
    }

    // ==================== 重注期 ====================

    [Test]
    public void Test_ReturnPeriod()
    {
        _vm.ReturnPeriod50 = true;
        Assert.That(_vm.ReturnPeriod50, Is.True);

        _vm.ReturnPeriod100 = true;
        Assert.That(_vm.ReturnPeriod100, Is.True);
    }
}
