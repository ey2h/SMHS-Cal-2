using System;
using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>
/// 地板玻璃线「地板玻璃结构计算」屏 ViewModel（V31 客户区 945×542，按 JGJ113）。
/// 四边支承板受活荷载，双片夹层玻璃，输出强度/挠度两组校核。
/// </summary>
public partial class FloorGlassCalcViewModel : ObservableObject
{
    public CalcSession Session { get; }
    private readonly CalcWizardViewModel _wizard;

    public FloorGlassCalcViewModel(CalcSession session, CalcWizardViewModel wizard)
    {
        Session = session;
        _wizard = wizard;
    }

    [ObservableProperty] private double _liveLoadKpa = 2;
    [ObservableProperty] private double _shortSideMm = 1500;
    [ObservableProperty] private double _longSideMm = 2000;
    [ObservableProperty] private double _glassThicknessMm = 8;

    public string LiveLoadText
    {
        get => LiveLoadKpa.ToString("0.###");
        set { if (TryNum(value, out var v)) { LiveLoadKpa = v; Invalidate("活荷载已改"); } }
    }

    public string ShortSideText
    {
        get => ShortSideMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { ShortSideMm = v; Invalidate("短边已改"); } }
    }

    public string LongSideText
    {
        get => LongSideMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { LongSideMm = v; Invalidate("长边已改"); } }
    }

    private static bool TryNum(string? s, out double v)
        => double.TryParse((s ?? "").Trim(), System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out v) && v > 0;

    public ObservableCollection<PreviewLine> Results { get; } = new();
    public bool HasResults => Results.Count > 0;
    public string PlaceholderText => HasResults ? "" : "点「预演」按当前地板玻璃参数计算。";

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _statusIsWarning;

    public bool PreviewEnabled => ShortSideMm > 0 && LongSideMm > 0;
    public string PreviewBlockedReason => "玻璃尺寸无效";

    private void Invalidate(string reason)
    {
        OnPropertyChanged(nameof(PreviewEnabled));
        OnPropertyChanged(nameof(PreviewBlockedReason));
        if (Results.Count == 0) return;
        Results.Clear();
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(PlaceholderText));
        StatusIsWarning = true;
        StatusText = reason + "，请重新预演";
    }

    private const string Sep36 = "************************************";
    private const string Sep47 = "***********************************************";
    private const string Dash24 = "————————————————————————";

    private static readonly IBrush ColorNormal = Brushes.Black;
    private static readonly IBrush ColorRule = new SolidColorBrush(Color.Parse("#4B5563"));
    private static readonly IBrush ColorOk = new SolidColorBrush(Color.Parse("#0000C0"));
    private static readonly IBrush ColorFail = new SolidColorBrush(Color.Parse("#C00000"));

    [RelayCommand]
    private void Preview()
    {
        if (!PreviewEnabled) { StatusIsWarning = true; StatusText = PreviewBlockedReason; return; }

        var it = new FloorGlassEngine.Input
        {
            LiveLoadKpa = LiveLoadKpa,
            ShortSideMm = ShortSideMm,
            LongSideMm = LongSideMm,
            GlassThicknessMm = GlassThicknessMm,
        };

        Results.Clear();
        try
        {
            var r = FloorGlassEngine.Compute(it);

            Add(Sep36, ColorRule);
            Add($"地板玻璃活荷载标准值为:{r.LiveLoadKpa:0.##}KPa", ColorNormal);
            Add(Sep36, ColorRule);

            Add($"地板玻璃的强度计算值:{r.Sigma:0.###}MPa", ColorNormal);
            Add($"地板玻璃的强度设计值:{r.SigmaAllowable:0.###}MPa", ColorNormal);
            Judge(r.SigmaOk);
            Add(Dash24, ColorRule);

            Add($"地板玻璃的挠度计算值:{r.Deflection:0.###}mm", ColorNormal);
            Add($"地板玻璃的挠度设计值:{r.DeflectionAllowable:0.###}mm", ColorNormal);
            Judge(r.DeflectionOk);
            Add(Sep47, ColorRule);
        }
        finally
        {
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(PlaceholderText));
        }

        StatusIsWarning = false;
        StatusText = $"预演完成：地板玻璃 {ShortSideMm:0}×{LongSideMm:0}mm t={GlassThicknessMm:0}";
    }

    [RelayCommand] private void Prev() => _wizard.GoBasic();
    [RelayCommand] private void Next() => _wizard.GoBookPanel();
    [RelayCommand] private void Exit() => _wizard.CloseForm();

    private void Judge(bool ok) => Add(ok ? "满足要求(√)" : "不满足要求(×)", ok ? ColorOk : ColorFail);
    private void Add(string text, IBrush color) => Results.Add(new PreviewLine(text, color));
}
