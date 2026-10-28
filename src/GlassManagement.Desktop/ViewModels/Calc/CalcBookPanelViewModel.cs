using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassManagement.Calc.Book;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>
/// 「生成计算书（支持微软 office 及金山 wps office）」面板 ViewModel —— V31 的最后一屏（603×330）。
///
/// <para>坐标源：<c>CW22_officewpsoffice.axaml</c> 的静态复刻（26 个可见控件）+ V31 样本
/// <c>计算书样本文件集/全隐框玻璃幕墙计算书.DOC</c> 的封面与 15 章骨架。</para>
///
/// <para><b>与 V31 的差异</b>：V31 用 Word OLE 自动化（V35 里能看到「软件无法正常进行生成计算书操作，
/// 请关闭开启的」这句提示），复刻版直接产出 .docx（自带 OOXML 写入器），
/// 不依赖本机装 Office。面板上的排版项**全部真实生效**：字号、左右边距、A3、行间距、不输出附录。</para>
/// </summary>
public partial class CalcBookPanelViewModel : ObservableObject
{
    private readonly CalcWizardViewModel _wizard;
    public CalcSession Session => _wizard.Session;

    public CalcBookPanelViewModel(CalcWizardViewModel wizard)
    {
        _wizard = wizard;
        Preview.Clear();
        foreach (var l in BuildOutline())
            Preview.Add(l);
        StatusText = "就绪：点「生成计算书」输出 .docx";
    }

    // ---------------- 计算书参数设置 ----------------

    /// <summary>工程名称（面板 (75,49) 233×25）。</summary>
    [ObservableProperty] private string _projectName = "北京xx幕墙装饰工程";

    /// <summary>幕墙类型（面板 (75,89)）—— 直接决定计算书章节骨架。</summary>
    public IReadOnlyList<string> Kinds => BookSkeletons.AllKinds;

    [ObservableProperty] private int _kindIndex;

    /// <summary>当前类型名（封面副标题 + 骨架键）。</summary>
    public string Kind => BookSkeletons.AllKinds[
        Math.Clamp(KindIndex, 0, BookSkeletons.AllKinds.Length - 1)];

    partial void OnKindIndexChanged(int value)
    {
        OnPropertyChanged(nameof(Kind));
        Preview.Clear();
        foreach (var l in BuildOutline()) Preview.Add(l);
        OnPropertyChanged(nameof(HasPreview));
    }

    [ObservableProperty] private string _company = "";
    [ObservableProperty] private string _designer = "";
    [ObservableProperty] private string _checker = "";
    [ObservableProperty] private string _approver = "";
    [ObservableProperty] private string _ratifier = "";

    // ---------------- 计算书正文字体设置 ----------------

    public bool FontSmallFour
    {
        get => FontIndex == 0;
        set { if (value) FontIndex = 0; }
    }
    public bool FontFive
    {
        get => FontIndex == 1;
        set { if (value) FontIndex = 1; }
    }
    public bool FontSmallFive
    {
        get => FontIndex == 2;
        set { if (value) FontIndex = 2; }
    }

    /// <summary>0=小四(12pt) / 1=五号(10.5pt) / 2=小五(9pt)，实机默认五号。</summary>
    [ObservableProperty] private int _fontIndex = 1;

    partial void OnFontIndexChanged(int value)
    {
        OnPropertyChanged(nameof(FontSmallFour));
        OnPropertyChanged(nameof(FontFive));
        OnPropertyChanged(nameof(FontSmallFive));
    }

    [ObservableProperty] private bool _a3;                 // A3幅面输出
    [ObservableProperty] private bool _givenLineSpacing;   // 给定行间距
    [ObservableProperty] private string _lineSpacingText = "15.6";
    [ObservableProperty] private bool _traditional;        // 繁体文字输出
    [ObservableProperty] private bool _noAppendix;         // 不输出附录
    [ObservableProperty] private bool _outlineStructure;   // 计算书合并后整理文档结构图

    [ObservableProperty] private string _marginLeftText = "2.5";   // 页面左边距 cm
    [ObservableProperty] private string _marginRightText = "1.5";  // 页面右边距 cm

    public IReadOnlyList<string> OfficePriorities { get; } =
        new[] { "优先使用微软 Office", "优先使用金山 WPS Office" };
    [ObservableProperty] private int _officePriorityIndex;

    // ---------------- 输出 ----------------

    public ObservableCollection<PreviewLine> Preview { get; } = new();
    public bool HasPreview => Preview.Count > 0;

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _statusIsWarning;
    [ObservableProperty] private string _lastPath = "";

    private static readonly IBrush ColorNormal = Brushes.Black;
    private static readonly IBrush ColorRule = new SolidColorBrush(Color.Parse("#4B5563"));
    private static readonly IBrush ColorOk = new SolidColorBrush(Color.Parse("#0000C0"));

    private static double Parse(string s, double dflt)
        => double.TryParse(s, out var v) && v >= 0 ? v : dflt;

    /// <summary>面板左侧只读框显示的内容：15 章骨架预览（生成前就能看出将输出什么）。</summary>
    private IReadOnlyList<PreviewLine> BuildOutline()
    {
        var list = new List<PreviewLine>
        {
            new("计算书将按 V31 样本（全隐框玻璃幕墙计算书.DOC）的章节骨架输出：", ColorRule),
            new("", ColorNormal),
        };
        list.Add(new PreviewLine($"【{Kind}】共 {BookSkeletons.For(Kind).Length} 章（骨架取自 V31 样本 DOC）：", ColorRule));
        var skel = BookSkeletons.For(Kind);
        for (int i = 0; i < skel.Length; i++)
        {
            var t = skel[i];
            if (NoAppendix && t.Contains("附录")) continue;
            list.Add(new PreviewLine($"{i + 1} {t}", ColorNormal));
        }
        list.Add(new PreviewLine("结论", ColorNormal));
        list.Add(new PreviewLine("", ColorNormal));
        list.Add(new PreviewLine(
            $"字体：{FontName}｜左边距 {MarginLeftText}cm｜右边距 {MarginRightText}cm"
            + (A3 ? "｜A3 幅面" : "｜A4 幅面")
            + (GivenLineSpacing ? $"｜行距 {LineSpacingText}pt" : "｜单倍行距"), ColorRule));
        return list;
    }

    private string FontName => FontIndex switch
    {
        0 => "小四号（12pt）",
        2 => "小五号（9pt）",
        _ => "五号（10.5pt）",
    };

    /// <summary>排版设置（面板控件 → BookPageSetup）。</summary>
    public BookPageSetup BuildSetup() => new()
    {
        BodyHalfPoints = FontIndex switch { 0 => 24, 2 => 18, _ => 21 },
        MarginLeftCm = Parse(MarginLeftText, 2.5),
        MarginRightCm = Parse(MarginRightText, 1.5),
        MarginTopBottomCm = 2.0,
        A3 = A3,
        LineSpacingPt = GivenLineSpacing ? Parse(LineSpacingText, 0) : 0,
    };

    [RelayCommand]
    private void Generate()
    {
        var meta = new CalcBookMeta(ProjectName, Kind, Company,
                                    Designer, Checker, Approver, Ratifier);
        var path = _wizard.GenerateCalcBook(null, BuildSetup(), NoAppendix, meta, Kind);
        if (string.IsNullOrEmpty(path))
        {
            StatusIsWarning = true;
            StatusText = "生成失败：请检查输出目录写权限";
            return;
        }

        LastPath = path;
        StatusIsWarning = false;
        StatusText = $"计算书已生成：{path}";
        Preview.Clear();
        foreach (var l in BuildOutline()) Preview.Add(l);
        Preview.Add(new PreviewLine("", ColorNormal));
        Preview.Add(new PreviewLine($"已生成：{path}", ColorOk));
        Preview.Add(new PreviewLine(
            $"纸张 {(A3 ? "A3" : "A4")}｜正文 {FontName}｜左右边距 {MarginLeftText}/{MarginRightText} cm"
            + (GivenLineSpacing ? $"｜行距 {LineSpacingText} pt" : "｜单倍行距")
            + (NoAppendix ? "｜不含附录" : "｜含附录"), ColorOk));
        OnPropertyChanged(nameof(HasPreview));
    }

    [RelayCommand] private void Prev() => _wizard.GoSealantCalc();
    [RelayCommand] private void Exit() => _wizard.CloseForm();
}
