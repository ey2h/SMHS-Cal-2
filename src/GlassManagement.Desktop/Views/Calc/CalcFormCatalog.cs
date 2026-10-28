using System;
using System.Collections.Generic;
using Avalonia.Controls;
using GlassManagement.Calc.Engines;
using GlassManagement.Desktop.ViewModels.Calc;

namespace GlassManagement.Desktop.Views.Calc;

/// <summary>Catalog of the 34 reconstructed Calc V35 forms.</summary>
public static class CalcFormCatalog
{
    public sealed record Entry(string Title, int SourceControls, int Width, int Height, Func<UserControl> Create);

    public static readonly IReadOnlyList<Entry> All = new Entry[]
    {
        new("untitled_0", 152, 945, 542, () => new CW00_untitled_0()),
        new("SMHS幕墙门窗结构计算软件V35.0  (已注册)", 3396, 768, 579, () => new CW01_V350()),
        new("幕墙计算-板块计算", 115, 782, 536, () => new CW02_Form2()),
        new("面板计算", 67, 876, 406, () => new CW03_Form3()),
        new("杆件计算", 106, 749, 546, () => new CW04_Form4()),
        new("帮助(退出本帮助窗口后 ，程序才可继续操作)", 4, 592, 316, () => new CW05_Form5()),
        new("关于  SMHS幕墙门窗结构计算软件 V35.0", 8, 792, 437, () => new CW06_V350()),
        new("金属板吊顶结构计算", 75, 806, 509, () => new CW07_Form7()),
        new("点式肋（无肋）驳接幕墙设计计算", 97, 909, 434, () => new CW08_Form8()),
        new("点式钢管结构设计计算", 148, 797, 521, () => new CW09_Form9()),
        // ★ 向导主链四屏同样走 XxxCalcView（CW10~CW13 是 GitHub 早期静态体，已弃用）
        new("幕墙计算-基本参数输入", 91, 902, 540,
            () => new BasicParamsView
            {
                DataContext = new BasicParamsViewModel(CalcSession.Default, CalcWizardViewModel.Default),
            }),
        new("幕墙计算-立柱计算", 148, 936, 442,
            () => new ColumnCalcView
            {
                DataContext = new ColumnCalcViewModel(CalcSession.Default, CalcWizardViewModel.Default),
            }),
        new("幕墙计算-横梁计算", 111, 949, 425,
            () => new BeamCalcView
            {
                DataContext = new BeamCalcViewModel(CalcSession.Default, CalcWizardViewModel.Default),
            }),
        new("幕墙计算-玻璃计算", 43, 819, 361,
            () => new GlassCalcView
            {
                DataContext = new GlassCalcViewModel(CalcSession.Default, CalcWizardViewModel.Default),
            }),
        // ★ 这是唯一需要 DataContext 的窗体（其余 CWxx 都是纯静态复刻，文本写死在 XAML 里）
        new("其它玻璃计算", 62, 763, 500,
            () => new OtherGlassCalcView { DataContext = new OtherGlassCalcViewModel() }),
        new("金属板幕墙计算-板块计算", 55, 795, 526, () => new CW14_Form14()),
        // ★ 界面以「我做的 XxxCalcView」为准（V31 实机坐标 + 结果面板 #C0FFFF）。
        //   CW{NN} 那批是从 GitHub 早期版本带过来的静态复刻体，已弃用，不再作为界面载体。
        new("幕墙计算-连接计算", 31, 796, 510,
            () => new ConnectCalcView
            {
                DataContext = new ConnectCalcViewModel(
                    CalcSession.Default, CalcWizardViewModel.Default),
            }),
        new("幕墙计算-埋件与焊缝计算", 136, 788, 490,
            () => new EmbedCalcView
            {
                DataContext = new EmbedCalcViewModel(
                    CalcSession.Default, CalcWizardViewModel.Default),
            }),
        new("埋件系统计算", 139, 787, 489, () => new CW17_Form17()),
        new("幕墙计算-胶类、伸缩及紧固计算", 129, 784, 537,
            () => new SealantCalcView
            {
                DataContext = new SealantCalcViewModel(
                    CalcSession.Default, CalcWizardViewModel.Default),
            }),
        new("幕墙（门窗）物理性能计算", 106, 885, 541, () => new CW19_Form19()),
        // ★ 物理性能 16 子项走复刻视图（CW19_Form19 静态体弃用）：4 计算页 + 12 说明页
        PhysicalPerf("建筑幕墙（门窗）物理性能-幕墙抗风压性能", 139),
        PhysicalPerf("建筑幕墙（门窗）物理性能-幕墙水密性能", 140),
        PhysicalPerf("建筑幕墙（门窗）物理性能-幕墙气密性能", 141),
        PhysicalPerf("建筑幕墙（门窗）物理性能-幕墙热工性能", 142),
        PhysicalPerf("建筑幕墙（门窗）物理性能-幕墙空气声隔声性能", 143),
        PhysicalPerf("建筑幕墙（门窗）物理性能-建筑幕墙平面内变形及抗震性能", 144),
        PhysicalPerf("建筑幕墙（门窗）物理性能-幕墙承载力性能", 145),
        PhysicalPerf("建筑幕墙（门窗）物理性能-建筑幕墙耐撞击性能", 146),
        PhysicalPerf("建筑幕墙（门窗）物理性能-建筑幕墙光学性能", 147),
        PhysicalPerf("建筑门窗物理性能-门窗抗风压性能", 149),
        PhysicalPerf("建筑门窗物理性能-门窗水密性能", 150),
        PhysicalPerf("建筑门窗物理性能-门窗气密性能", 151),
        PhysicalPerf("建筑门窗物理性能-门窗热工性能", 152),
        PhysicalPerf("建筑门窗物理性能-门窗空气声隔声性能", 153),
        PhysicalPerf("建筑门窗物理性能-门窗采光性能", 154),
        PhysicalPerf("建筑门窗物理性能-玻璃采光顶物理性能", 156),
        // ★ 空气声隔声（id=137 独立菜单，复用 CW19 隔声帧）
        PhysicalPerf("建筑玻璃空气声隔声计算（只适用于玻璃构件）", 137),
        // ★ 外遮阳系数计算（id=134）与 民用建筑节能计算加权计算器（id=136）
        new("节能(热工)-外遮阳系数计算", 8, 960, 560,
            () => new ExternalShadingView { DataContext = new ExternalShadingViewModel() }),
        new("节能(热工)-民用建筑节能计算加权计算器", 48, 960, 560,
            () => new WeightedAverageView { DataContext = new WeightedAverageViewModel() }),
        new("常用建筑结构静力计算(按《建筑结构静力计算手册》第二版计算)", 24, 899, 494,
            () => new StaticCalcView { DataContext = new StaticCalcViewModel() }),
        new("文件引用规范设置：", 169, 780, 500, () => new CW21_Form21()),
        new("生成计算书（支持微软office及金山wps office）", 26, 600, 322,
            () => new CalcBookPanelView
            {
                DataContext = new CalcBookPanelViewModel(CalcWizardViewModel.Default),
            }),
        new("截面特性计算(支持AUTOCAD、中望CAD、浩辰CAD)", 40, 854, 499,
            () => new SectionCalcView { DataContext = new SectionCalcViewModel() }),
        new("建筑预计雷击次数计算", 30, 900, 540,
            () => new LightningView { DataContext = new LightningViewModel() }),
        new("插值计算器", 10, 700, 490,
            () => new InterpolationView { DataContext = new InterpolationViewModel() }),
        new("方程求解", 12, 700, 490,
            () => new EquationView { DataContext = new EquationViewModel() }),
        new("牛腿结构计算", 60, 945, 542,
            () => new CorbelView { DataContext = new CorbelViewModel() }),
        new("建筑护栏计算", 157, 904, 578, () => new CW24_Form24()),
        new("全玻璃幕墙设计计算", 89, 903, 438,
            () => new FullGlassCalcView
            {
                DataContext = new FullGlassCalcViewModel(
                    CalcSession.Default, CalcWizardViewModel.Default),
            }),
        // ★ 热工两屏走复刻视图（CW26/CW27 静态体弃用；链逐位见 ThermalGlass/WallEngine 测试）
        // 6 个菜单项（公共/居住/工业 × 玻璃/非透明）→ 2 个复刻视图，建筑类别随标题分档
        new("公共建筑--透明幕墙（门窗）热工计算", 119, 896, 550,
            () => new ThermalGlassCalcView
            {
                DataContext = new ThermalGlassCalcViewModel(
                    CalcSession.Default, CalcWizardViewModel.Default, BuildingType.公共建筑),
            }),
        new("居住建筑--透明幕墙（门窗）热工计算", 119, 896, 550,
            () => new ThermalGlassCalcView
            {
                DataContext = new ThermalGlassCalcViewModel(
                    CalcSession.Default, CalcWizardViewModel.Default, BuildingType.居住建筑),
            }),
        new("工业建筑--透明幕墙（门窗）热工计算", 119, 896, 550,
            () => new ThermalGlassCalcView
            {
                DataContext = new ThermalGlassCalcViewModel(
                    CalcSession.Default, CalcWizardViewModel.Default, BuildingType.工业建筑),
            }),
        new("公共建筑--非透明幕墙热工计算", 141, 995, 485,
            () => new ThermalWallCalcView
            {
                DataContext = new ThermalWallCalcViewModel(
                    CalcSession.Default, CalcWizardViewModel.Default, BuildingType.公共建筑),
            }),
        new("居住建筑--非透明幕墙热工计算", 141, 995, 485,
            () => new ThermalWallCalcView
            {
                DataContext = new ThermalWallCalcViewModel(
                    CalcSession.Default, CalcWizardViewModel.Default, BuildingType.居住建筑),
            }),
        new("工业建筑--非透明幕墙热工计算", 141, 995, 485,
            () => new ThermalWallCalcView
            {
                DataContext = new ThermalWallCalcViewModel(
                    CalcSession.Default, CalcWizardViewModel.Default, BuildingType.工业建筑),
            }),
        new("雨蓬计算", 172, 893, 513,
            () => new AwningCalcView
            {
                DataContext = new AwningCalcViewModel(
                    CalcSession.Default, CalcWizardViewModel.Default),
            }),
        new("门窗系统计算", 243, 924, 564,
            () => new DoorWindowCalcView
            {
                DataContext = new DoorWindowCalcViewModel(
                    CalcSession.Default, CalcWizardViewModel.Default),
            }),
        new("Form1", 30, 781, 522, () => new CW30_Form1()),
        // ★ 转角立柱：CW31_Form31 是 GitHub 早期静态体（引擎早已有测试），改走通用工具视图
        new("转角立柱计算", 99, 945, 542,
            () => new SimpleToolView { DataContext = new CornerColumnToolViewModel() }),
        // ── 独立工具线（B8）其余 14 项：引擎已完成并有单元测试，本轮补界面 ──
        new("焊缝强度计算", 20, 945, 542,
            () => new SimpleToolView { DataContext = new WeldToolViewModel() }),
        new("螺栓群计算", 20, 945, 542,
            () => new SimpleToolView { DataContext = new BoltGroupToolViewModel() }),
        new("埋件厚度计算", 20, 945, 542,
            () => new SimpleToolView { DataContext = new AnchorPlateToolViewModel() }),
        new("建筑植筋计算", 20, 945, 542,
            () => new SimpleToolView { DataContext = new RebarAnchorToolViewModel() }),
        new("钢销/螺栓(扭+剪)计算", 20, 945, 542,
            () => new SimpleToolView { DataContext = new PinBoltToolViewModel() }),
        new("型材螺纹计算（先在母材上制出螺纹）", 20, 945, 542,
            () => new SimpleToolView { DataContext = new ThreadToolViewModel() }),
        new("自攻钉及自攻自钻螺钉计算", 20, 945, 542,
            () => new SimpleToolView { DataContext = new SelfTappingScrewToolViewModel() }),
        new("排烟面积计算", 20, 945, 542,
            () => new SimpleToolView { DataContext = new SmokeVentToolViewModel() }),
        new("开启扇通风面积", 20, 945, 542,
            () => new SimpleToolView { DataContext = new OpeningVentToolViewModel() }),
        new("百叶窗通风面积", 20, 945, 542,
            () => new SimpleToolView { DataContext = new LouverVentToolViewModel() }),
        new("型材米重计算", 20, 945, 542,
            () => new SimpleToolView { DataContext = new WeightPerMeterToolViewModel() }),
        new("型材热应力计算", 20, 945, 542,
            () => new SimpleToolView { DataContext = new ThermalStressToolViewModel() }),
        new("型（板）材热应力伸缩量计算", 20, 945, 542,
            () => new SimpleToolView { DataContext = new ThermalExpansionToolViewModel() }),
        new("多点锁及开启扇扇框计算", 20, 945, 542,
            () => new SimpleToolView { DataContext = new MultiPointLockToolViewModel() }),
        new("线材优化数据输入", 34, 918, 568,
            () => new CuttingStockView { DataContext = new CuttingStockViewModel() }),
        new("欢迎进入软件注册！", 19, 684, 413, () => new CW33_Form33()),
        // ★ 多跨数据输入走复刻视图（CW136 是 GitHub 早期静态体，已弃用）
        new("多跨数据输入（立柱计算·可操作复刻）", 14, 936, 479,
            () => new MultiSpanInputView
            {
                DataContext = new MultiSpanViewModel(CalcSession.Default, CalcWizardViewModel.Default),
            }),
    };

    /// <summary>物理性能子项条目（CW19 复刻视图，menuId 决定计算页/说明页）。</summary>
    private static Entry PhysicalPerf(string title, int menuId)
        => new(title, 106, 885, 541,
            () => new PhysicalPerformanceView { DataContext = new PhysicalPerformanceViewModel(menuId) });
}