using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using GlassManagement.Desktop.ViewModels.Calc;

namespace GlassManagement.Desktop.Views.Calc;

/// <summary>
/// VB6 form reconstruction: 生成计算书（支持微软office及金山wps office）| client 603x330 | 25 nodes (14 labels)
///
/// <para><b>接线原则</b>：XAML 是 VB6 窗体资源的逐控件镜像（坐标、控件类、默认文本一律不动），
/// 计算与状态全部由 <see cref="CalcBookPanelViewModel"/> 承载，绑定只在本文件里做代码绑定。
/// 这样「界面像不像 V31」与「算得对不对」两件事互不污染。</para>
/// </summary>
public partial class CW22_officewpsoffice : UserControl
{
    public CW22_officewpsoffice()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Wire();
    }

    /// <summary>带 ViewModel 打开（向导末屏与窗体浏览器用）。</summary>
    public CW22_officewpsoffice(CalcBookPanelViewModel vm) : this() => DataContext = vm;

    private CalcBookPanelViewModel? Vm => DataContext as CalcBookPanelViewModel;

    private void Wire()
    {
        if (Vm is not { } vm) return;

        // ── 计算书参数设置 ──
        Bind(ProjectNameBox, TextBox.TextProperty, nameof(vm.ProjectName), vm);
        Bind(KindBox, TextBox.TextProperty, nameof(vm.Kind), vm, twoWay: false);

        // ── 正文字体：三个 RadioButton 互斥（VB6 的 OptionButton 数组） ──
        RbSmallFour.GroupName = "FontSize";
        RbFive.GroupName = "FontSize";
        RbSmallFive.GroupName = "FontSize";
        RbSmallFour.IsChecked = vm.FontIndex == 0;
        RbFive.IsChecked = vm.FontIndex == 1;
        RbSmallFive.IsChecked = vm.FontIndex == 2;
        RbSmallFour.IsCheckedChanged += (_, _) => { if (RbSmallFour.IsChecked == true) vm.FontIndex = 0; };
        RbFive.IsCheckedChanged += (_, _) => { if (RbFive.IsChecked == true) vm.FontIndex = 1; };
        RbSmallFive.IsCheckedChanged += (_, _) => { if (RbSmallFive.IsChecked == true) vm.FontIndex = 2; };

        // ── 选项与数值 ──
        Bind(CkA3, CheckBox.IsCheckedProperty, nameof(vm.A3), vm);
        Bind(CkLineSpacing, CheckBox.IsCheckedProperty, nameof(vm.GivenLineSpacing), vm);
        Bind(CkTraditional, CheckBox.IsCheckedProperty, nameof(vm.Traditional), vm);
        Bind(CkNoAppendix, CheckBox.IsCheckedProperty, nameof(vm.NoAppendix), vm);
        Bind(CkOutline, CheckBox.IsCheckedProperty, nameof(vm.OutlineStructure), vm);
        Bind(LineSpacingBox, TextBox.TextProperty, nameof(vm.LineSpacingText), vm);
        Bind(MarginLeftBox, TextBox.TextProperty, nameof(vm.MarginLeftText), vm);
        Bind(MarginRightBox, TextBox.TextProperty, nameof(vm.MarginRightText), vm);

        // ── Office 优先级（VB6 下拉，两项） ──
        OfficePriorityCombo.ItemsSource = vm.OfficePriorities;
        Bind(OfficePriorityCombo, ComboBox.SelectedIndexProperty, nameof(vm.OfficePriorityIndex), vm);

        // ── 按钮 ──
        BtnGenerate.Click += (_, _) => vm.GenerateCommand.Execute(null);
        BtnPrev.Click += (_, _) => vm.PrevCommand.Execute(null);
        BtnExit.Click += (_, _) => vm.ExitCommand.Execute(null);

        // ── 状态：(11,159) 那个 41×25 的只读框就是 V35 面板上的状态/输出框。
        //    ★ (6,10) 587×229 的大只读框在 V35 里被「计算书参数设置」Frame 完全盖住，
        //      原程序应是把 Visible 置 False 的隐藏控件 —— 复刻版**不往里写内容**，
        //      否则文字会从 Frame 上沿溢出来（曾因此把章节预览糊到面板上）。
        void RefreshStatus()
        {
            StatusBox.Text = vm.StatusText;
            PreviewBox.Text = "";
        }
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(vm.StatusText) or nameof(vm.Preview)
                or nameof(vm.LastPath) or nameof(vm.Kind))
                RefreshStatus();
        };
        RefreshStatus();
    }

    private static void Bind(AvaloniaObject target, AvaloniaProperty prop,
                             string path, object source, bool twoWay = true)
        => target.Bind(prop, new Binding(path)
        {
            Source = source,
            Mode = twoWay ? BindingMode.TwoWay : BindingMode.OneWay,
        });
}
