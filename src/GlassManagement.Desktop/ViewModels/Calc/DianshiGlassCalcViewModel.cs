using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>
/// 点式玻璃幕墙线「点式肋（无肋）驳接幕墙设计计算」屏 ViewModel（V31 客户区 903×438）。
///
/// <para>玻璃按四点驳接模型计算（m/μ 查肋间距比），输出玻璃强度/挠度 + 玻璃肋宽度/挠度 5 组校核。
/// 算式与四面对拍见 <c>docs/V31_DIANSHI_UI.md</c>。</para>
/// </summary>
public partial class DianshiGlassCalcViewModel : ObservableObject
{
    public CalcSession Session { get; }
    private readonly CalcWizardViewModel _wizard;

    public DianshiGlassCalcViewModel(CalcSession session, CalcWizardViewModel wizard)
    {
        Session = session;
        _wizard = wizard;

        Session.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(CalcSession.PanelWindKpa):
                case nameof(CalcSession.AlphaMax):
                    Invalidate("上游风荷载已变化");
                    break;
            }
        };
    }

    // ---------------- 玻璃板尺寸 ----------------

    public string ShortSideText
    {
        get => Session.DianshiGlassShortSideMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.DianshiGlassShortSideMm = v; Invalidate("玻璃板短边已改"); OnPropertyChanged(); } }
    }

    public string LongSideText
    {
        get => Session.DianshiGlassLongSideMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.DianshiGlassLongSideMm = v; Invalidate("玻璃板长边已改"); OnPropertyChanged(); } }
    }

    public string RibSpacing1Text
    {
        get => Session.DianshiRibSpacing1Mm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.DianshiRibSpacing1Mm = v; Invalidate("肋间距1已改"); OnPropertyChanged(); } }
    }

    public string RibSpacing2Text
    {
        get => Session.DianshiRibSpacing2Mm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.DianshiRibSpacing2Mm = v; Invalidate("肋间距2已改"); OnPropertyChanged(); } }
    }

    // ---------------- 大面玻璃 ----------------

    /// <summary>大面玻璃厚度候选（ComboBox，默认 10，sel=3）。</summary>
    public static readonly double[] GlassThicknessOptions = { 5, 6, 8, 10, 12, 15, 19, 20, 22, 25 };
    public IReadOnlyList<string> GlassThicknessItems { get; } =
        GlassThicknessOptions.Select(x => x.ToString("0")).ToList();

    public int GlassThicknessIndex
    {
        get
        {
            int i = Array.IndexOf(GlassThicknessOptions, Session.DianshiGlassThicknessMm);
            return i < 0 ? 3 : i;
        }
        set
        {
            var arr = GlassThicknessOptions;
            Session.DianshiGlassThicknessMm = arr[Math.Max(0, Math.Min(arr.Length - 1, value))];
            OnPropertyChanged();
            Invalidate("玻璃厚度已改");
        }
    }

    // ---------------- 玻璃肋参数 ----------------

    public IReadOnlyList<string> RibThicknessItems { get; } =
        DianshiGlassEngine.RibThicknessOptions.Select(x => x.ToString("0")).ToList();

    public int RibThicknessIndex
    {
        get
        {
            int i = Array.IndexOf(DianshiGlassEngine.RibThicknessOptions, Session.DianshiRibThicknessMm);
            return i < 0 ? 3 : i;
        }
        set
        {
            var arr = DianshiGlassEngine.RibThicknessOptions;
            Session.DianshiRibThicknessMm = arr[Math.Max(0, Math.Min(arr.Length - 1, value))];
            OnPropertyChanged();
            Invalidate("玻璃肋厚度已改");
        }
    }

    public string RibHeightText
    {
        get => Session.DianshiRibHeightMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.DianshiRibHeightMm = v; Invalidate("玻璃肋高度已改"); OnPropertyChanged(); } }
    }

    public string RibSpanText
    {
        get => Session.DianshiRibSpanMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.DianshiRibSpanMm = v; Invalidate("玻璃肋跨度已改"); OnPropertyChanged(); } }
    }

    private static bool TryNum(string? s, out double v)
        => double.TryParse((s ?? "").Trim(), System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out v) && v > 0;

    // ---------------- 结果 ----------------

    public ObservableCollection<PreviewLine> Results { get; } = new();
    public bool HasResults => Results.Count > 0;
    public string PlaceholderText => HasResults ? "" : "点「预演」按当前玻璃板尺寸与玻璃肋参数计算。";

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _statusIsWarning;

    public bool PreviewEnabled => Session.PanelWindReady
        && Session.DianshiGlassShortSideMm > 0 && Session.DianshiRibSpacing2Mm > 0;

    public string PreviewBlockedReason => !Session.PanelWindReady
        ? CalcSession.UpstreamNotReadyText
        : Session.DianshiGlassShortSideMm <= 0 || Session.DianshiRibSpacing2Mm <= 0
            ? "玻璃板尺寸无效"
            : "";

    public string WindNote => Session.PanelWindSource;

    private void Invalidate(string reason)
    {
        OnPropertyChanged(nameof(PreviewEnabled));
        OnPropertyChanged(nameof(PreviewBlockedReason));
        OnPropertyChanged(nameof(WindNote));
        if (Results.Count == 0) return;
        Results.Clear();
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(PlaceholderText));
        StatusIsWarning = true;
        StatusText = reason + "，请重新预演";
    }

    // ---------------- 命令 ----------------

    /// <summary>实机分隔符：39 星 / 19 破折号（— U+2014）。</summary>
    private const string Sep39 = "***************************************";
    private const string Dash19 = "———————————————————";

    private static readonly IBrush ColorNormal = Brushes.Black;
    private static readonly IBrush ColorRule = new SolidColorBrush(Color.Parse("#4B5563"));
    private static readonly IBrush ColorOk = new SolidColorBrush(Color.Parse("#0000C0"));
    private static readonly IBrush ColorFail = new SolidColorBrush(Color.Parse("#C00000"));

    [RelayCommand]
    private void Preview()
    {
        if (!PreviewEnabled) { StatusIsWarning = true; StatusText = PreviewBlockedReason; return; }

        var it = new DianshiGlassEngine.Input
        {
            GlassShortSideMm = Session.DianshiGlassShortSideMm,
            GlassLongSideMm = Session.DianshiGlassLongSideMm,
            RibSpacing1Mm = Session.DianshiRibSpacing1Mm,
            RibSpacing2Mm = Session.DianshiRibSpacing2Mm,
            GlassThicknessMm = Session.DianshiGlassThicknessMm,
            RibThicknessMm = Session.DianshiRibThicknessMm,
            RibActualHeightMm = Session.DianshiRibHeightMm,
            RibSpanMm = Session.DianshiRibSpanMm,
            AlphaMax = Session.AlphaMax,
            Wk = Session.PanelWindNmm2,             // ★ N/mm²，不经 kPa 反除
        };

        Results.Clear();
        try
        {
            var r = DianshiGlassEngine.Compute(it);
            Session.LastDianshiResult = r;

            Add(Sep39, ColorRule);
            Add($"风荷载标准值为:{r.WindStandardKpa:0.##}KPa", ColorNormal);
            Add(Sep39, ColorRule);

            Add($"玻璃强度计算值:{r.GlassSigma:0.###}MPa", ColorNormal);
            Add($"选用玻璃的强度设计值:{r.GlassSigmaAllowable:0.###}MPa(钢化玻璃)", ColorNormal);
            Judge(r.GlassSigmaOk);
            Add(Dash19, ColorRule);

            Add($"玻璃挠度计算值:{r.GlassDeflection:0.###}mm", ColorNormal);
            Add($"选用玻璃挠度容许值:{r.GlassDeflectionAllowable:0.###}mm(钢化玻璃)", ColorNormal);
            Judge(r.GlassDeflectionOk);
            Add(Sep39, ColorRule);

            Add($"玻璃肋实际选用宽度为:{r.RibActualWidth:0.###}mm", ColorNormal);
            Add($"玻璃肋的计算宽度不应小于:{r.RibRequiredWidth:0.###}mm", ColorNormal);
            Judge(r.RibWidthOk);
            Add(Dash19, ColorRule);

            Add($"玻璃肋挠度计算值:{r.RibDeflection:0.###}mm", ColorNormal);
            Add($"玻璃肋挠度限值:{r.RibDeflectionAllowable:0.###}mm", ColorNormal);
            Judge(r.RibDeflectionOk);
            Add(Sep39, ColorRule);
        }
        finally
        {
            Session.LastDianshiSummary = CalcSession.SummaryOf(Results);
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(PlaceholderText));
        }

        StatusIsWarning = !Session.PanelWindResolved;
        StatusText = $"预演完成：玻璃 {Session.DianshiGlassShortSideMm:0.###}×{Session.DianshiGlassLongSideMm:0.###}mm"
                   + $" t={Session.DianshiGlassThicknessMm:0.###}mm"
                   + $"｜肋 t={Session.DianshiRibThicknessMm:0} h={Session.DianshiRibHeightMm:0}";
    }

    [RelayCommand] private void Prev() => _wizard.GoBasic();
    [RelayCommand] private void Next() => _wizard.GoBookPanel();
    [RelayCommand] private void Exit() => _wizard.CloseForm();

    private void Judge(bool ok) => Add(ok ? "满足要求(√)" : "不满足要求(×)", ok ? ColorOk : ColorFail);
    private void Add(string text, IBrush color) => Results.Add(new PreviewLine(text, color));
}
