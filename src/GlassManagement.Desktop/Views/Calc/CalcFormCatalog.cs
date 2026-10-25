using System;
using System.Collections.Generic;
using Avalonia.Controls;
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
        new("幕墙计算-基本参数输入", 91, 907, 612, () => new CW10_Form10()),
        new("幕墙计算-立柱计算", 148, 936, 442, () => new CW11_Form11()),
        new("幕墙计算-横梁计算", 111, 949, 426, () => new CW12_Form12()),
        new("幕墙计算-玻璃计算", 43, 819, 361, () => new CW13_Form13()),
        // ★ 这是唯一需要 DataContext 的窗体（其余 CWxx 都是纯静态复刻，文本写死在 XAML 里）
        new("其它玻璃计算", 62, 763, 500,
            () => new OtherGlassCalcView { DataContext = new OtherGlassCalcViewModel() }),
        new("金属板幕墙计算-板块计算", 55, 795, 526, () => new CW14_Form14()),
        // ★ 连接屏已 MVVM 化（计算在 ConnectEngine），与其它玻璃计算同属需要 DataContext 的条目
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
        new("常用建筑结构静力计算(按《建筑结构静力计算手册》第二版计算)", 24, 899, 494, () => new CW20_Form20()),
        new("文件引用规范设置：", 169, 780, 500, () => new CW21_Form21()),
        new("生成计算书（支持微软office及金山wps office）", 33, 603, 330, () => new CW22_officewpsoffice()),
        new("截面特性计算(支持AUTOCAD、中望CAD、浩辰CAD)", 40, 854, 499, () => new CW23_AUTOCADCADCAD()),
        new("建筑护栏计算", 157, 904, 578, () => new CW24_Form24()),
        new("全玻璃幕墙设计计算", 89, 903, 438, () => new CW25_Form25()),
        new("玻璃幕墙热工计算", 119, 897, 553, () => new CW26_Form26()),
        new("非透明幕墙热工计算", 141, 995, 485, () => new CW27_Form27()),
        new("雨蓬计算", 172, 893, 513, () => new CW28_Form28()),
        new("门窗系统计算", 243, 924, 564, () => new CW29_Form29()),
        new("Form1", 30, 781, 522, () => new CW30_Form1()),
        new("转角立柱计算", 99, 915, 566, () => new CW31_Form31()),
        new("线材优化数据输入", 34, 918, 568, () => new CW32_Form32()),
        new("欢迎进入软件注册！", 19, 684, 413, () => new CW33_Form33()),
        new("多跨数据输入（立柱计算·可操作复刻）", 14, 936, 479, () => new CW136_MultiSpanInput()),
    };
}