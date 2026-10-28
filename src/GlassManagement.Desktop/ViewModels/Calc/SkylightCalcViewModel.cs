using System;
using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>
/// 采光顶「单坡采光顶杆件」屏 ViewModel（V31 客户区 749×546）。
/// 主龙骨（沿坡长）+ 副龙骨（垂直坡长）双向受力。
/// </summary>
public partial class SkylightCalcViewModel : ObservableObject
{
    public CalcSession Session { get; }
    private readonly CalcWizardViewModel _wizard;

    public SkylightCalcViewModel(CalcSession session, CalcWizardViewModel wizard)
    {
        Session = session;
        _wizard = wizard;
        Session.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(CalcSession.PanelWindKpa) or nameof(CalcSession.AlphaMax))
                Invalidate("上游风荷载已变化");
        };
    }

    [ObservableProperty] private double _slopeAngleDeg = 30;   // 坡度角
    [ObservableProperty] private double _deadLoadKpa = 0.4;    // 恒载
    [ObservableProperty] private double _liveLoadKpa = 0.5;    // 活载
    [ObservableProperty] private double _mainSpanMm = 3000;    // 主龙骨跨度
    [ObservableProperty] private double _mainSpacingMm = 1500; // 主龙骨间距
    [ObservableProperty] private double _subSpanMm = 1500;     // 副龙骨跨度
    [ObservableProperty] private double _subSpacingMm = 1500;  // 副龙骨间距

    public string SlopeText { get => SlopeAngleDeg.ToString("0.###"); set { if (TryNum(value, out var v)) { SlopeAngleDeg = v; Invalidate("坡度角已改"); } } }
    public string DeadText { get => DeadLoadKpa.ToString("0.###"); set { if (TryNum(value, out var v)) { DeadLoadKpa = v; Invalidate("恒载已改"); } } }
    public string LiveText { get => LiveLoadKpa.ToString("0.###"); set { if (TryNum(value, out var v)) { LiveLoadKpa = v; Invalidate("活载已改"); } } }

    private static bool TryNum(string? s, out double v)
        => double.TryParse((s ?? "").Trim(), System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out v) && v > 0;

    public ObservableCollection<PreviewLine> Results { get; } = new();
    public bool HasResults => Results.Count > 0;
    public string PlaceholderText => HasResults ? "" : "点「预演」按当前采光顶参数与龙骨截面计算。";

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _statusIsWarning;

    public bool PreviewEnabled => SlopeAngleDeg > 0 && DeadLoadKpa > 0 && LiveLoadKpa > 0;
    public string PreviewBlockedReason => "坡度角或荷载无效";
    public string WindNote => "采光顶按恒载+活载组合（不取第一页风荷载）";

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

    private const string Sep44 = "********************************************";
    private const string Dash44 = "--------------------------------------------";

    private static readonly IBrush ColorNormal = Brushes.Black;
    private static readonly IBrush ColorRule = new SolidColorBrush(Color.Parse("#4B5563"));
    private static readonly IBrush ColorOk = new SolidColorBrush(Color.Parse("#0000C0"));
    private static readonly IBrush ColorFail = new SolidColorBrush(Color.Parse("#C00000"));

    [RelayCommand]
    private void Preview()
    {
        if (!PreviewEnabled) { StatusIsWarning = true; StatusText = PreviewBlockedReason; return; }

        var it = new SkylightEngine.Input
        {
            SlopeAngleDeg = SlopeAngleDeg,
            DeadLoadKpa = DeadLoadKpa,
            LiveLoadKpa = LiveLoadKpa,
            MainSpanMm = MainSpanMm,
            MainSpacingMm = MainSpacingMm,
            SubSpanMm = SubSpanMm,
            SubSpacingMm = SubSpacingMm,
        };

        Results.Clear();
        try
        {
            var r = SkylightEngine.Compute(it);

            Add(Sep44, ColorRule);
            Add($"风荷载标准值为:{LiveLoadKpa:0.##}KPa", ColorNormal);
            Add($"雪荷载标准值为:{DeadLoadKpa * 0.85:0.##}KPa", ColorNormal);
            Add($"活荷载标准值为:{LiveLoadKpa:0.##}KPa", ColorNormal);
            Add($"恒荷载标准值为:{DeadLoadKpa:0.##}KPa", ColorNormal);
            Add("取自重、风荷载和活荷载组合;", ColorNormal);
            Add(Sep44, ColorRule);

            Add($"主龙骨强度计算值:{r.MainSigma:0.###}MPa", ColorNormal);
            Add($"主龙骨强度设计值:90MPa(6063-T5)", ColorNormal);
            Judge(r.MainSigma <= 90);
            Add(Dash44, ColorRule);

            Add($"组合荷载作用下主龙骨挠度计算值:{r.MainCombinedDeflection:0.###}mm", ColorNormal);
            Add($"组合荷载作用下主龙骨挠度设计值:{r.MainCombinedLimit:0.###}mm", ColorNormal);
            Judge(r.MainCombinedOk);
            Add(Dash44, ColorRule);

            Add($"自重荷载作用下主龙骨挠度计算值:{r.MainDeadDeflection:0.###}mm", ColorNormal);
            Add($"自重荷载作用下主龙骨挠度设计值:{r.MainDeadLimit:0.###}mm", ColorNormal);
            Judge(r.MainDeadOk);
            Add(Sep44, ColorRule);

            Add($"副龙骨强度计算值:{r.SubSigma:0.###}MPa", ColorNormal);
            Add($"副龙骨强度设计值:90MPa(6063-T5)", ColorNormal);
            Judge(r.SubSigma <= 90);
            Add(Dash44, ColorRule);

            Add($"组合荷载作用下平行于采光顶表面方向的副龙骨挠度计算值:{r.SubParaCombinedDeflection:0.###}mm", ColorNormal);
            Add($"组合荷载作用下平行于采光顶表面方向的副龙骨挠度设计值:{r.SubParaLimit:0.###}mm", ColorNormal);
            Judge(r.SubParaCombinedDeflection <= r.SubParaLimit);
            Add(Dash44, ColorRule);

            Add($"组合荷载作用下垂直于采光顶表面方向的副龙骨挠度计算值:{r.SubVertCombinedDeflection:0.###}mm", ColorNormal);
            Add($"组合荷载作用下垂直于采光顶表面方向的副龙骨挠度设计值:{r.SubVertLimit:0.###}mm", ColorNormal);
            Judge(r.SubVertCombinedDeflection <= r.SubVertLimit);
            Add(Dash44, ColorRule);

            Add($"自重荷载作用下平行于采光顶表面方向的副龙骨挠度计算值:{r.SubParaDeadDeflection:0.###}mm", ColorNormal);
            Add($"自重荷载作用下平行于采光顶表面方向的副龙骨挠度设计值:{r.SubDeadLimit:0.###}mm", ColorNormal);
            Judge(r.SubParaDeadDeflection <= r.SubDeadLimit);
            Add(Dash44, ColorRule);

            Add($"自重荷载作用下垂直于采光顶表面方向的副龙骨挠度计算值:{r.SubVertDeadDeflection:0.###}mm", ColorNormal);
            Add($"自重荷载作用下垂直于采光顶表面方向的副龙骨挠度设计值:{r.SubDeadLimit:0.###}mm", ColorNormal);
            Judge(r.SubVertDeadDeflection <= r.SubDeadLimit);
            Add(Sep44, ColorRule);
        }
        finally
        {
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(PlaceholderText));
        }

        StatusIsWarning = false;
        StatusText = $"预演完成：单坡采光顶 坡度 {SlopeAngleDeg:0}° 主龙骨跨 {MainSpanMm:0}mm";
    }

    [RelayCommand] private void Prev() => _wizard.GoBasic();
    [RelayCommand] private void Next() => _wizard.GoBookPanel();
    [RelayCommand] private void Exit() => _wizard.CloseForm();

    private void Judge(bool ok) => Add(ok ? "满足要求(√)" : "不满足要求(×)", ok ? ColorOk : ColorFail);
    private void Add(string text, IBrush color) => Results.Add(new PreviewLine(text, color));
}
