using System;
using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>
/// 雨篷（雨蓬）计算屏 ViewModel（V31 客户区 893×513，CW28）。
/// 5 大模块：荷载组合 → 悬臂梁（集中荷载）→ 焊缝 → 玻璃（点式支承）→ 埋件锚固。
/// 引擎 AwningEngine，取证参照 yupeng_var.json。
/// </summary>
public partial class AwningCalcViewModel : ObservableObject
{
    public CalcSession Session { get; }
    private readonly CalcWizardViewModel _wizard;

    public AwningCalcViewModel(CalcSession session, CalcWizardViewModel wizard)
    {
        Session = session;
        _wizard = wizard;
        Session.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(CalcSession.PanelWindKpa) or nameof(CalcSession.AlphaMax))
                Invalidate("上游风荷载已变化");
        };
    }

    // ── 荷载（KPa）──
    [ObservableProperty] private double _basicWindKpa = 0.45;
    [ObservableProperty] private double _snowKpa = 0.8;
    [ObservableProperty] private double _liveKpa = 0.5;
    [ObservableProperty] private double _deadKpa = 0.4;

    // ── 几何（mm）──
    [ObservableProperty] private double _eccentricityMm = 100;
    [ObservableProperty] private double _panelWidthMm = 1500;
    [ObservableProperty] private double _panelLengthMm = 1400;

    // ── 悬臂主梁 ──
    [ObservableProperty] private double _beamSectionModulus = 62100;
    [ObservableProperty] private double _beamInertia = 3910000;
    [ObservableProperty] private double _beamElasticModulus = 206000;

    // ── 焊缝 ──
    [ObservableProperty] private double _weldLegMm = 4;

    // ── 玻璃（点式支承）──
    [ObservableProperty] private double _glassShortSideMm = 1250;
    [ObservableProperty] private double _glassThicknessMm = 10;

    // ── 埋件 ──
    [ObservableProperty] private double _anchorBarDiaMm = 12;
    [ObservableProperty] private double _anchorBarCount = 4;
    [ObservableProperty] private double _anchorFy = 270;

    public string BasicWindText { get => BasicWindKpa.ToString("0.###"); set { if (TryNum(value, out var v)) { BasicWindKpa = v; Invalidate("基本风压已改"); } } }
    public string SnowText { get => SnowKpa.ToString("0.###"); set { if (TryNum(value, out var v)) { SnowKpa = v; Invalidate("雪荷载已改"); } } }
    public string LiveText { get => LiveKpa.ToString("0.###"); set { if (TryNum(value, out var v)) { LiveKpa = v; Invalidate("活荷载已改"); } } }
    public string DeadText { get => DeadKpa.ToString("0.###"); set { if (TryNum(value, out var v)) { DeadKpa = v; Invalidate("恒荷载已改"); } } }
    public string EccText { get => EccentricityMm.ToString("0.###"); set { if (TryNum(value, out var v)) { EccentricityMm = v; Invalidate("偏心已改"); } } }
    public string PanelWidthText { get => PanelWidthMm.ToString("0.###"); set { if (TryNum(value, out var v)) { PanelWidthMm = v; Invalidate("板宽已改"); } } }
    public string PanelLengthText { get => PanelLengthMm.ToString("0.###"); set { if (TryNum(value, out var v)) { PanelLengthMm = v; Invalidate("板长已改"); } } }
    public string BeamWText { get => BeamSectionModulus.ToString("0.###"); set { if (TryNum(value, out var v)) { BeamSectionModulus = v; Invalidate("主梁截面抵抗矩已改"); } } }
    public string BeamIText { get => BeamInertia.ToString("0.###"); set { if (TryNum(value, out var v)) { BeamInertia = v; Invalidate("主梁惯性矩已改"); } } }
    public string WeldLegText { get => WeldLegMm.ToString("0.###"); set { if (TryNum(value, out var v)) { WeldLegMm = v; Invalidate("焊脚已改"); } } }
    public string GlassShortText { get => GlassShortSideMm.ToString("0.###"); set { if (TryNum(value, out var v)) { GlassShortSideMm = v; Invalidate("玻璃短边已改"); } } }
    public string GlassThickText { get => GlassThicknessMm.ToString("0.###"); set { if (TryNum(value, out var v)) { GlassThicknessMm = v; Invalidate("玻璃厚度已改"); } } }
    public string AnchorDiaText { get => AnchorBarDiaMm.ToString("0.###"); set { if (TryNum(value, out var v)) { AnchorBarDiaMm = v; Invalidate("锚筋直径已改"); } } }
    public string AnchorCountText { get => AnchorBarCount.ToString("0.###"); set { if (TryNum(value, out var v)) { AnchorBarCount = v; Invalidate("锚筋根数已改"); } } }
    public string AnchorFyText { get => AnchorFy.ToString("0.###"); set { if (TryNum(value, out var v)) { AnchorFy = v; Invalidate("锚筋强度已改"); } } }

    private static bool TryNum(string? s, out double v)
        => double.TryParse((s ?? "").Trim(), System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out v) && v > 0;

    public ObservableCollection<PreviewLine> Results { get; } = new();
    public bool HasResults => Results.Count > 0;
    public string PlaceholderText => HasResults ? "" : "点「预演」按当前雨篷参数计算。";

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _statusIsWarning;

    public bool PreviewEnabled => BasicWindKpa > 0 && DeadKpa > 0 && PanelWidthMm > 0 && PanelLengthMm > 0;
    public string PreviewBlockedReason => "风压或几何参数无效";
    public string WindNote => "雨篷按负压风（上吸）控制：标准=负压−恒，设计=1.5·负压−恒";

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

        var it = new AwningEngine.Input
        {
            BasicWindKpa = BasicWindKpa,
            SnowKpa = SnowKpa,
            LiveKpa = LiveKpa,
            DeadKpa = DeadKpa,
            EccentricityMm = EccentricityMm,
            PanelWidthMm = PanelWidthMm,
            PanelLengthMm = PanelLengthMm,
            BeamSectionModulus = BeamSectionModulus,
            BeamInertia = BeamInertia,
            BeamElasticModulus = BeamElasticModulus,
            WeldLegMm = WeldLegMm,
            GlassShortSideMm = GlassShortSideMm,
            GlassThicknessMm = GlassThicknessMm,
            AnchorBarDiaMm = AnchorBarDiaMm,
            AnchorBarCount = AnchorBarCount,
            AnchorFy = AnchorFy,
        };

        Results.Clear();
        try
        {
            var r = AwningEngine.Compute(it);

            Add(Sep44, ColorRule);
            Add($"正压风荷载标准值为:{r.PosWindKpa:0.###}KPa", ColorNormal);
            Add($"负压风荷载标准值为:{r.NegWindKpa:0.###}KPa", ColorNormal);
            Add($"雪荷载标准值为:{SnowKpa:0.##}KPa", ColorNormal);
            Add($"活荷载标准值为:{LiveKpa:0.##}KPa", ColorNormal);
            Add($"恒荷载标准值为:{DeadKpa:0.##}KPa", ColorNormal);
            Add($"龙骨荷载标准组合值为:{r.StdCombineKpa:0.###}KPa", ColorNormal);
            Add($"龙骨荷载设计组合值为:{r.DsnCombineKpa:0.###}KPa", ColorNormal);
            Add(Sep44, ColorRule);

            Add($"悬臂主梁强度计算值:{r.BeamSigma:0.###}MPa", ColorNormal);
            Add($"悬臂主梁强度设计值:215MPa(Q235)", ColorNormal);
            Judge(r.BeamSigma <= 215.0);
            Add(Dash44, ColorRule);

            Add($"悬臂主梁挠度计算值:{r.BeamDeflection:0.###}mm", ColorNormal);
            Add($"悬臂主梁挠度设计值:{r.BeamDeflLimit:0.###}mm", ColorNormal);
            Judge(r.BeamDeflection <= r.BeamDeflLimit);
            Add(Sep44, ColorRule);

            Add($"焊缝强度计算值:{r.WeldSigma:0.###}MPa", ColorNormal);
            Add($"焊缝强度设计值:160MPa", ColorNormal);
            Judge(r.WeldSigma <= 160.0);
            Add(Dash44, ColorRule);

            Add($"玻璃板块强度计算值:{r.GlassSigma:0.###}MPa", ColorNormal);
            Add($"玻璃板块强度设计值:84MPa(钢化)", ColorNormal);
            Judge(r.GlassSigma <= 84.0);
            Add($"玻璃板块挠度计算值:{r.GlassDeflection:0.###}mm", ColorNormal);
            Add($"玻璃板块挠度设计值:{r.GlassDeflLimit:0.###}mm", ColorNormal);
            Judge(r.GlassDeflection <= r.GlassDeflLimit);
            Add(Dash44, ColorRule);

            Add($"埋件锚筋面积计算值:{r.AnchorAreaReq:0.###}mm²", ColorNormal);
            Add($"埋件锚筋面积实配值:{r.AnchorAreaActual:0.###}mm²", ColorNormal);
            Add($"埋件锚筋基本锚固长度:{r.AnchorLab:0.###}mm", ColorNormal);
            Add($"埋件锚筋锚固长度计算值:{r.AnchorLa:0.###}mm", ColorNormal);
            Add(Sep44, ColorRule);
        }
        finally
        {
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(PlaceholderText));
        }

        StatusIsWarning = false;
        StatusText = $"预演完成：雨篷 板宽{PanelWidthMm:0}×板长{PanelLengthMm:0} 悬挑{EccentricityMm + PanelWidthMm + PanelLengthMm:0}mm";
    }

    [RelayCommand] private void Prev() => _wizard.GoBasic();
    [RelayCommand] private void Exit() => _wizard.CloseForm();

    private void Judge(bool ok) => Add(ok ? "满足要求(√)" : "不满足要求(×)", ok ? ColorOk : ColorFail);
    private void Add(string text, IBrush color) => Results.Add(new PreviewLine(text, color));
}
