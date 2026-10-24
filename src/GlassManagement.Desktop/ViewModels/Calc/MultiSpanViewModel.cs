using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>
/// 「多跨数据输入」屏的 ViewModel（CommunityToolkit.Mvvm 源生成）。
/// View（MultiSpanInputView）只负责布局与绑定，不含任何业务判断。
/// </summary>
public partial class MultiSpanViewModel : ObservableObject
{
    public sealed record SegmentRow(int Index, string Code, string Length);

    public sealed partial class ResultLine : ObservableObject
    {
        [ObservableProperty] private string _text = "";
        [ObservableProperty] private IBrush _color = Brushes.Black;
        [ObservableProperty] private TextDecorationCollection? _decoration;
    }

    private static readonly IBrush ColorNormal = new SolidColorBrush(Color.Parse("#000000"));
    private static readonly IBrush ColorJudge = new SolidColorBrush(Color.Parse("#0000FF"));
    private static readonly IBrush ColorFail = new SolidColorBrush(Color.Parse("#FF0000"));
    private static readonly IBrush ColorNote = new SolidColorBrush(Color.Parse("#808080"));

    /// <summary>向导会话（跨窗体沿用的参数都在这一份里）。</summary>
    public CalcSession Session { get; }
    private readonly CalcWizardViewModel _wizard;

    public ObservableCollection<SegmentRow> Segments { get; } = new();
    public ObservableCollection<ResultLine> Results { get; } = new();

    [ObservableProperty] private string _statusText = "就绪";

    /// <summary>DataGrid 可见行数（V35 实测固定 16 行，不足处补 0/0 占位）。</summary>
    public const int GridVisibleRows = 16;

    public MultiSpanViewModel(CalcSession session, CalcWizardViewModel wizard)
    {
        Session = session;
        _wizard = wizard;
        ClearResult();
    }

    // ---------------- 向导按钮（整屏替换式导航，V35 实测行为） ----------------
    [RelayCommand] private void WizardPrev() => _wizard.GoColumnCalc();
    [RelayCommand] private void WizardNext() => _wizard.StatusText = "下一步：V35 多跨屏之后为「预演→生成计算书」，该步复刻未接";
    [RelayCommand] private void WizardExit() => _wizard.CloseForm();

    // ---------------- 输入校验（值直接读写会话，保证跨屏沿用） ----------------
    private bool ReadInputs(out int spans, out double aMm, out double lMm)
    {
        spans = Session.TotalSpans; aMm = Session.SegmentAMm; lMm = Session.SegmentLMm;
        if (spans <= 0) { StatusText = "总跨数必须为正整数"; return false; }
        if (aMm <= 0) { StatusText = "跨长A₁ 必须为正数"; return false; }
        if (lMm <= 0) { StatusText = "跨长L₁ 必须为正数"; return false; }
        return true;
    }

    // ---------------- 确认数据：生成 2N 段 A/L 交替表 ----------------
    [RelayCommand]
    public void ConfirmData()
    {
        if (!ReadInputs(out int spans, out double aMm, out double lMm)) return;

        Segments.Clear();
        var segs = MultiSpanStaticEngine.BuildAlternating(spans, aMm, lMm);
        foreach (var s in segs)
            Segments.Add(new SegmentRow(s.Index, s.Code, s.LengthMm.ToString("0.###", CultureInfo.InvariantCulture)));
        // V35 实测：DataGrid 固定 16 行可见，不足处以「0 / 0」占位（序号继续递增）
        for (int i = Segments.Count + 1; i <= GridVisibleRows; i++)
            Segments.Add(new SegmentRow(i, "0", "0"));
        StatusText = $"已生成 {segs.Count} 段（A1,L1,…,A{spans},L{spans}；V35 为 2N 段，无末尾 A(N+1)）";
    }

    [RelayCommand]
    public void ClearRight()
    {
        Segments.Clear();
        StatusText = "右表已清空";
    }

    // ---------------- 预演 ----------------
    [RelayCommand]
    public void Preview()
    {
        // ★计算链门禁：多跨屏同样只能吃第一屏算出的风荷载标准值，不走第一屏不出结果
        if (!Session.WindStandardResolved)
        {
            Results.Clear();
            OnPropertyChanged(nameof(HasResults));
            StatusText = "多跨数据输入：计算链须从第一屏出发——请回「基本参数输入」完成并点「下一步」";
            return;
        }
        if (!ReadInputs(out int spans, out double aMm, out double lMm)) return;
        if (Segments.Count == 0) ConfirmData();
        if (Segments.Count == 0) return;

        var baseline = MultiSpanBaseline.Match(spans, aMm, lMm);
        Results.Clear();

        void Add(string text, IBrush? color = null, bool underline = false)
        {
            Results.Add(new ResultLine
            {
                Text = text,
                Color = color ?? ColorNormal,
                Decoration = underline ? TextDecorations.Underline : null,
            });
            OnPropertyChanged(nameof(HasResults));
        }

        if (baseline is not null)
        {
            foreach (var raw in baseline.RawText.Split('\n'))
            {
                var t = raw.TrimEnd('\r');
                if (t.StartsWith("***")) Add(t);
                else if (t.Contains("满足要求(√)")) Add(t, ColorJudge, true);
                else if (t.Contains("不满足要求(×)")) Add(t, ColorFail, true);
                else Add(t);
            }
            StatusText = $"【实测基线】{baseline.SampleId}：{baseline.Note}；输出为实测原文逐字符复现";
            return;
        }

        // ---- 非基线几何：挠度限值实算（已确证）+ 估算验算（如实标注） ----
        double lim = MultiSpanStaticEngine.DeflectionLimit(lMm, aMm);
        string bar = new string('*', 42);
        // 分格宽取基本参数屏「立柱左分格宽度」（q = wk × 分格宽 / 1000）
        double bayWidth = Session.BayColLeft > 0 ? Session.BayColLeft : 1350;
        var est = MultiSpanStaticEngine.Estimate(spans, aMm, lMm, Session.WindStandardKpa,
                                                 bayWidth, Session.ColumnProfile,
                                                 Session.ElasticModulusMpa);

        Add($"【复刻计算】几何 = 总跨数 {spans} / 跨长A {Fmt(aMm)} / 跨长L {Fmt(lMm)}（非实测基线）", ColorNote);
        Add(bar);
        Add($"    风荷载标准值为:{Fmt(Session.WindStandardKpa)}KPa（来源：{Session.WindStandardSource}）",
            Session.WindStandardKpa > 0 ? ColorNormal : ColorNote);
        Add(bar);

        if (Session.ColumnProfile is null)
        {
            // 未选型材：只给已确证的挠度限值与提示
            Add("    第1跨立柱挠度设计值为:" + Fmt(lim) + "mm（JGJ336：计算跨度=" + Fmt(est.CalcSpanMm)
                + "mm，" + LimitRuleText(est.CalcSpanMm) + "）", ColorNormal);
            Add("    （未选型材：请先回「立柱计算」屏从参数库「立柱」95 行中选一行，方可估算强度/挠度计算值）", ColorNote);
            Add(bar);
            StatusText = "【复刻计算】非基线几何：已实算挠度限值；未选型材，强度/挠度计算值待选型材后估算";
            return;
        }

        void Check(string label, double computed, double allowable, string unit)
        {
            Add($"    {label}为:{Fmt(computed)}{unit}");
            Add($"    立柱型材{label switch
            {
                var s when s.Contains("抗弯") => "抗弯强度设计值",
                var s when s.Contains("抗剪") => "抗剪强度设计值",
                var s when s.Contains("折算") => "折算强度设计值",
                var s when s.Contains("挠度") => "挠度设计值",
                _ => ""
            }}为:{Fmt(allowable)}{unit}");
            Add(computed <= allowable ? "    满足要求(√)" : "    不满足要求(×)",
                computed <= allowable ? ColorJudge : ColorFail,
                computed <= allowable);
        }

        Check("第1跨立柱抗弯强度计算值", est.BendingSpan1MPa, est.AllowBendingMPa, "MPa");
        Check("第1跨立柱抗剪强度计算值", est.ShearSpan1MPa, est.AllowShearMPa, "MPa");
        Check("第3跨立柱抗弯强度计算值", est.BendingSpan3MPa, est.AllowBendingMPa, "MPa");
        Check("第3跨立柱抗剪强度计算值", est.ShearSpan3MPa, est.AllowShearMPa, "MPa");
        Check("第3跨立柱折算强度计算值", est.EquivalentSpan3MPa, est.AllowEquivalentMPa, "MPa");
        Check("第2跨立柱支座处抗弯强度计算值", est.BendingSupportMPa, est.AllowBendingMPa, "MPa");
        Check("第2跨立柱支座处抗剪强度计算值", est.ShearSupportMPa, est.AllowShearMPa, "MPa");
        Check("第2跨立柱支座处折算强度计算值", est.EquivalentSupportMPa, est.AllowEquivalentMPa, "MPa");
        Check("第1跨立柱挠度计算值", est.DeflectionSpan1Mm, lim, "mm");
        Check("第2跨后立柱挠度计算值", est.DeflectionSpan2AfterMm, lim, "mm");
        Add(bar);
        Add($"    （{est.Note}）", ColorNote);
        Add($"    型材：{est.SectionSummary}", ColorNote);
        Add($"    线荷载 q = wk×分格宽/1000 = {Fmt(Session.WindStandardKpa)}×{Fmt(bayWidth)}/1000 = {Fmt(est.LineLoadNPerMm)}N/mm", ColorNote);
        Add(bar);

        StatusText = $"【复刻计算】非基线几何：挠度限值实算（{LimitRuleText(est.CalcSpanMm)}）、强度/挠度计算值按简支+V31双样本比例估算；"
                   + $"型材 {Session.ColumnCode}";
    }

    /// <summary>挠度限值规则文本（JGJ336 三段，V35 计算书原文）。</summary>
    private static string LimitRuleText(double calcSpan)
        => calcSpan <= 4500 ? "≤4500 → /180"
         : calcSpan <= 7000 ? "4500~7000 → /250+7"
         : ">7000 → /200";

    private static string Fmt(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private void ClearResult()
    {
        Results.Clear();
        OnPropertyChanged(nameof(HasResults));
    }

    /// <summary>结果区是否有内容（XAML 用它切换空态提示，避免空白青色面板无引导）。</summary>
    public bool HasResults => Results.Count > 0;
}
