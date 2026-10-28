using System;
using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>
/// U型玻璃线「U型玻璃结构设计计算」屏 ViewModel（V31 客户区 797×521）。
/// U 玻璃是简支梁线构件，输出强度/挠度两组校核。
/// </summary>
public partial class UGlassCalcViewModel : ObservableObject
{
    public CalcSession Session { get; }
    private readonly CalcWizardViewModel _wizard;

    public UGlassCalcViewModel(CalcSession session, CalcWizardViewModel wizard)
    {
        Session = session;
        _wizard = wizard;
        Session.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(CalcSession.PanelWindKpa) or nameof(CalcSession.AlphaMax))
                Invalidate("上游风荷载已变化");
        };
    }

    [ObservableProperty] private double _spanMm = 2500;   // 跨度
    [ObservableProperty] private double _widthMm = 234;   // 翼宽

    public string SpanText
    {
        get => SpanMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { SpanMm = v; Invalidate("跨度已改"); } }
    }

    public string WidthText
    {
        get => WidthMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { WidthMm = v; Invalidate("翼宽已改"); } }
    }

    private static bool TryNum(string? s, out double v)
        => double.TryParse((s ?? "").Trim(), System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out v) && v > 0;

    public ObservableCollection<PreviewLine> Results { get; } = new();
    public bool HasResults => Results.Count > 0;
    public string PlaceholderText => HasResults ? "" : "点「预演」按当前 U 型玻璃截面与跨度计算。";

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _statusIsWarning;

    public bool PreviewEnabled => Session.PanelWindReady && SpanMm > 0;
    public string PreviewBlockedReason => !Session.PanelWindReady ? CalcSession.UpstreamNotReadyText : "跨度无效";
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

        var sec = UGlassEngine.Sections[0];   // U玻璃234*41*6
        var it = new UGlassEngine.Input
        {
            WidthMm = WidthMm,
            AreaMm2 = sec.AreaMm2,
            Wmm3 = sec.Wmm3,
            Imm4 = sec.Imm4,
            EMpa = sec.EMpa,
            SpanMm = SpanMm,
            AlphaMax = Session.AlphaMax,
            Wk = Session.PanelWindNmm2,
        };

        Results.Clear();
        try
        {
            var r = UGlassEngine.Compute(it);

            Add(Sep39, ColorRule);
            Add($"风荷载标准值为:{r.WindStandardKpa:0.##}KPa", ColorNormal);
            Add(Sep39, ColorRule);

            Add($"U型玻璃强度计算值:{r.Sigma:0.###}MPa", ColorNormal);
            Add($"选用U型玻璃强度设计值:{r.SigmaAllowable:0.###}MPa", ColorNormal);
            Judge(r.SigmaOk);
            Add(Dash19, ColorRule);

            Add($"U型玻璃挠度计算值:{r.Deflection:0.###}mm", ColorNormal);
            Add($"选用U型玻璃挠度设计值:{r.DeflectionAllowable:0.###}mm", ColorNormal);
            Judge(r.DeflectionOk);
            Add(Sep39, ColorRule);
        }
        finally
        {
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(PlaceholderText));
        }

        StatusIsWarning = !Session.PanelWindResolved;
        StatusText = $"预演完成：U 玻璃翼宽 {WidthMm:0}mm 跨度 {SpanMm:0}mm";
    }

    [RelayCommand] private void Prev() => _wizard.GoBasic();
    [RelayCommand] private void Next() => _wizard.GoBookPanel();
    [RelayCommand] private void Exit() => _wizard.CloseForm();

    private void Judge(bool ok) => Add(ok ? "满足要求(√)" : "不满足要求(×)", ok ? ColorOk : ColorFail);
    private void Add(string text, IBrush color) => Results.Add(new PreviewLine(text, color));
}
