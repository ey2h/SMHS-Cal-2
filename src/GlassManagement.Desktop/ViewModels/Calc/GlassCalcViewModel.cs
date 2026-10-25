using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassManagement.Calc.Data;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>
/// 「幕墙计算-玻璃计算」屏 ViewModel。
///
/// <para><b>模板按第一页「玻璃配置」六选一分成 5 种输出</b>（V31 实测）：
/// 单片 5 个数字（47 星分隔）／中空 7 个（43 星）／夹层 5 个／中空+夹层 PVB 10 个／SGP 2 个。
/// 双中空、双夹层-中空尚未闭合 → 选中时提示不支持。</para>
///
/// <para><b>片位可用性</b>（实机 en 状态，与设计稿 docs/ui/figma/V31_玻璃_*.svg 对应）：
/// 单片只外片；中空外+内（中片禁用）；夹层外+内+夹层块解锁；中空+夹层全解锁。</para>
/// </summary>
public partial class GlassCalcViewModel : ObservableObject
{
    public CalcSession Session { get; }
    private readonly CalcWizardViewModel _wizard;

    public GlassCalcViewModel(CalcSession session, CalcWizardViewModel wizard)
    {
        Session = session;
        _wizard = wizard;
        Session.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(CalcSession.PanelWindKpa):
                case nameof(CalcSession.BeamLeftBayMm):
                case nameof(CalcSession.BeamRightBayMm):
                case nameof(CalcSession.BeamUpperGridHeightMm):
                case nameof(CalcSession.GlassUsePresetSize):
                case nameof(CalcSession.GlassUseCustomSize):
                case nameof(CalcSession.GlassCustomWidthMm):
                case nameof(CalcSession.GlassCustomHeightMm):
                case nameof(CalcSession.InterlayerKind):
                case nameof(CalcSession.GlassOuterThicknessMm):
                case nameof(CalcSession.GlassDoubleLam):
                case nameof(CalcSession.GlassDoubleInsul):
                    Invalidate("上游参数已变化");
                    OnPropertyChanged(nameof(SecondPlyVisible));
                    OnPropertyChanged(nameof(SizeItems));
                    OnPropertyChanged(nameof(PaneStateText));
                    OnPropertyChanged(nameof(OuterEnabled));
                    OnPropertyChanged(nameof(MidEnabled));
                    OnPropertyChanged(nameof(InnerEnabled));
                    OnPropertyChanged(nameof(InterlayerEnabled));
                    break;
            }
        };
    }

    // ---------------- 尺寸 ----------------

    public bool UsePresetSize
    {
        get => Session.GlassUsePresetSize;
        set { Session.GlassUsePresetSize = value; OnPropertyChanged(); OnPropertyChanged(nameof(CustomEnabled)); OnPropertyChanged(nameof(PresetEnabled)); }
    }

    public bool UseCustomSize
    {
        get => Session.GlassUseCustomSize;
        set { Session.GlassUseCustomSize = value; OnPropertyChanged(); OnPropertyChanged(nameof(CustomEnabled)); OnPropertyChanged(nameof(PresetEnabled)); }
    }

    public bool PresetEnabled => Session.GlassUsePresetSize;
    public bool CustomEnabled => Session.GlassUseCustomSize;

    /// <summary>下拉候选文本（外层高、内层宽，与实机顺序一致）。</summary>
    public IReadOnlyList<string> SizeItems =>
        Session.GlassSizeCandidates.Select(c => $"{c.W:0}*{c.H:0}").ToList();

    [ObservableProperty] private int _selectedSizeIndex;

    partial void OnSelectedSizeIndexChanged(int value)
    {
        var list = Session.GlassSizeCandidates;
        if (value >= 0 && value < list.Count)
        {
            Session.GlassPresetWidthMm = list[value].W;
            Session.GlassPresetHeightMm = list[value].H;
        }
        Invalidate("分格尺寸已切换");
    }

    public string CustomWidthText
    {
        get => Session.GlassCustomWidthMm.ToString("0.###");
        set => SetCustomSize(value, true);
    }

    public string CustomHeightText
    {
        get => Session.GlassCustomHeightMm.ToString("0.###");
        set => SetCustomSize(value, false);
    }

    private void SetCustomSize(string raw, bool isWidth)
    {
        // V31 校验：≤0 / 非数字 → 「请您注意，玻璃尺寸参数有误」，中止预演。
        // 复刻修正：回滚到上一个合法值（V31 回滚到默认 1000），文案只写一次。
        if (!double.TryParse(raw, out var v) || v <= 0)
        {
            StatusIsWarning = true;
            StatusText = "请您注意，玻璃尺寸参数有误";
            OnPropertyChanged(isWidth ? nameof(CustomWidthText) : nameof(CustomHeightText));
            return;
        }
        if (isWidth) Session.GlassCustomWidthMm = v;
        else Session.GlassCustomHeightMm = v;
        OnPropertyChanged(nameof(InputSummary));
        Invalidate("自定义尺寸已修改");
    }

    // ---------------- 片位可用性 ----------------

    private GlassBuildKind Kind => Session.GlassBuildKind;

    /// <summary>单片 / 中空 / 夹层 / 复合：外片恒可用。</summary>
    public bool OuterEnabled => true;

    /// <summary>中片：只有「中空+夹层」与「双中空」才可用（实机：中空玻璃时中片整块 en=False）。</summary>
    public bool MidEnabled => Kind is GlassBuildKind.InsulLamPvb or GlassBuildKind.InsulLamSgp
                                   or GlassBuildKind.DoubleInsulated or GlassBuildKind.DoubleLaminated;

    /// <summary>内片：单片与中空+夹层以外都可用。</summary>
    public bool InnerEnabled => Kind != GlassBuildKind.Single;

    /// <summary>夹层配置块：夹层 / 中空+夹层 / 双夹层-中空 才解锁。</summary>
    public bool InterlayerEnabled => Kind is GlassBuildKind.Laminated or GlassBuildKind.InsulLamPvb
                                          or GlassBuildKind.InsulLamSgp or GlassBuildKind.DoubleLaminated;

    public string PaneStateText => Kind switch
    {
        GlassBuildKind.Single => "单片：仅外片可选",
        GlassBuildKind.Insulated => "中空：外片 + 内片（中片禁用）",
        GlassBuildKind.Laminated => "夹层：外片 + 内片 + 夹层配置",
        GlassBuildKind.InsulLamPvb => "中空+夹层（PVB）：三片全解锁",
        GlassBuildKind.InsulLamSgp => "中空+夹层（SGP）：三片全解锁",
        GlassBuildKind.DoubleInsulated => "双中空：计算未闭合",
        _ => "双夹层-中空：计算未挖",
    };

    // ---------------- 厚度 / 种类 ----------------

    /// <summary>V31 厚度下拉 10 档。</summary>
    public IReadOnlyList<double> ThicknessOptions { get; } =
        new[] { 3.2, 4, 5, 6, 8, 10, 12, 15, 19, 20 };

    public IReadOnlyList<string> KindOptions => GlassAllowables.Kinds;

    public double OuterThickness
    {
        get => Session.GlassOuterThicknessMm;
        set { Session.GlassOuterThicknessMm = value; OnPropertyChanged(); Invalidate("外片厚度已改"); }
    }

    public double MidThickness
    {
        get => Session.GlassMidThicknessMm;
        set { Session.GlassMidThicknessMm = value; OnPropertyChanged(); Invalidate("中片厚度已改"); }
    }

    public double InnerThickness
    {
        get => Session.GlassInnerThicknessMm;
        set { Session.GlassInnerThicknessMm = value; OnPropertyChanged(); Invalidate("内片厚度已改"); }
    }

    /// <summary>
    /// 双夹层-中空（4 片）时外夹层的**第二片**厚度。
    /// <para>V31 该构型下外片框出现 2 个厚度下拉（实机外框 80,203 + 80,225；
    /// 内框 376,203 + 375,225），中片框整体禁用。普通构型（≤3 片）不用。</para>
    /// </summary>
    public double Outer2Thickness
    {
        get => Session.GlassOuter2ThicknessMm;
        set { Session.GlassOuter2ThicknessMm = value; OnPropertyChanged(); Invalidate("外夹层第二片厚度已改"); }
    }

    /// <summary>双夹层-中空（4 片）时内夹层的第二片厚度。</summary>
    public double Inner2Thickness
    {
        get => Session.GlassInner2ThicknessMm;
        set { Session.GlassInner2ThicknessMm = value; OnPropertyChanged(); Invalidate("内夹层第二片厚度已改"); }
    }

    /// <summary>仅双夹层-中空（4 片）才显示外夹层/内夹层的第二个厚度下拉。</summary>
    public bool SecondPlyVisible => Kind == GlassBuildKind.DoubleLaminated;

    public string OuterKind
    {
        get => Session.GlassOuterKind;
        set { Session.GlassOuterKind = value; OnPropertyChanged(); Invalidate("外片种类已改"); }
    }

    public string MidKind
    {
        get => Session.GlassMidKind;
        set { Session.GlassMidKind = value; OnPropertyChanged(); Invalidate("中片种类已改"); }
    }

    public string InnerKind
    {
        get => Session.GlassInnerKind;
        set { Session.GlassInnerKind = value; OnPropertyChanged(); Invalidate("内片种类已改"); }
    }

    /// <summary>按片位设置玻璃种类（命令参数 "outer|钢化玻璃"）。</summary>
    [RelayCommand]
    private void SetPaneKind(string? arg)
    {
        if (string.IsNullOrWhiteSpace(arg)) return;
        var parts = arg.Split('|');
        if (parts.Length != 2) return;
        switch (parts[0])
        {
            case "outer": Session.GlassOuterKind = parts[1]; break;
            case "mid": Session.GlassMidKind = parts[1]; break;
            case "inner": Session.GlassInnerKind = parts[1]; break;
            default: return;
        }
        OnPropertyChanged(nameof(OuterKind));
        OnPropertyChanged(nameof(MidKind));
        OnPropertyChanged(nameof(InnerKind));
        Invalidate("玻璃种类已切换");
    }

    // ---------------- 胶片 ----------------

    public bool IsSgp => Session.InterlayerKind == InterlayerKind.Sgp;

    public bool FilmThicknessEnabled => IsSgp;   // PVB 下 V31 该框 en=False

    [RelayCommand]
    private void SetInterlayer(string? name)
    {
        Session.InterlayerKind = name == "SGP" ? InterlayerKind.Sgp : InterlayerKind.Pvb;
        OnPropertyChanged(nameof(IsSgp));
        OnPropertyChanged(nameof(FilmThicknessEnabled));
        OnPropertyChanged(nameof(PaneStateText));
        Invalidate("胶片类型已切换");
    }

    /// <summary>胶片类型下拉索引：0=PVB / 1=SGP（V31 ctrl id=5 的项序）。</summary>
    public int InterlayerIndex
    {
        get => (int)Session.InterlayerKind;
        set
        {
            var k = value == 1 ? InterlayerKind.Sgp : InterlayerKind.Pvb;
            if (Session.InterlayerKind == k) return;
            Session.InterlayerKind = k;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsSgp));
            OnPropertyChanged(nameof(FilmThicknessEnabled));
            OnPropertyChanged(nameof(PaneStateText));
            Invalidate("胶片类型已切换");
        }
    }

    /// <summary>胶片厚度下拉索引（0.38/0.76/1.14/1.52/2.28，默认 0.76 → 索引 1）。</summary>
    public int FilmIndex
    {
        get
        {
            var opts = new[] { 0.38, 0.76, 1.14, 1.52, 2.28 };
            int i = Array.IndexOf(opts, Session.FilmThicknessMm);
            return i < 0 ? 1 : i;
        }
        set
        {
            var opts = new[] { 0.38, 0.76, 1.14, 1.52, 2.28 };
            if (value < 0 || value >= opts.Length) return;
            Session.FilmThicknessMm = opts[value];
            OnPropertyChanged();
            Invalidate("胶片厚度已改");
        }
    }

    public double FilmThickness
    {
        get => Session.FilmThicknessMm;
        set { Session.FilmThicknessMm = value; OnPropertyChanged(); Invalidate("胶片厚度已改"); }
    }

    // ---------------- 输入摘要 ----------------

    public string InputSummary =>
        $"玻璃：{Session.GlassCaption}｜{Session.GlassWidthMm:0.###}×{Session.GlassHeightMm:0.###}mm"
        + $"（短边 {Session.GlassShortSpanMm:0.###}）"
        + $"｜厚 {Session.GlassOuterThicknessMm:0.##}/{Session.GlassMidThicknessMm:0.##}/{Session.GlassInnerThicknessMm:0.##}mm"
        + $"｜wk={Session.PanelWindKpa:0.###}kPa｜αmax={Session.AlphaMax:0.###}";

    public string WindNote => Session.PanelWindSource;

    // ---------------- 结果 ----------------

    public ObservableCollection<PreviewLine> Results { get; } = new();
    public bool HasResults => Results.Count > 0;
    public string PlaceholderText => HasResults ? "" : "点「预演」按当前玻璃构造分别计算强度与挠度。";

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _statusIsWarning;

    public bool PreviewEnabled => Session.PanelWindKpa > 0 && Session.GlassShortSpanMm > 0;

    public string PreviewBlockedReason => Session.PanelWindKpa <= 0
        ? "上游未算出风荷载标准值，请先在第一页点「计算荷载」"
        : Session.GlassShortSpanMm <= 0 ? "玻璃尺寸无效，请检查分格尺寸或自定义宽高" : "";

    /// <summary>诊断行开关（默认关，保证与 V31 结果区逐行一致）。</summary>
    public bool ShowTrace { get; set; }

    private void Invalidate(string reason)
    {
        OnPropertyChanged(nameof(PreviewEnabled));
        OnPropertyChanged(nameof(PreviewBlockedReason));
        OnPropertyChanged(nameof(InputSummary));
        OnPropertyChanged(nameof(WindNote));
        if (Results.Count == 0) return;
        Results.Clear();
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(PlaceholderText));
        StatusIsWarning = true;
        StatusText = reason + "，请重新预演";
    }

    // ---------------- 命令 ----------------

    private const string Sep47 = "***********************************************";
    private const string Sep43 = "*******************************************";
    /// <summary>双夹层-中空（4 片）用 41 星 —— 实测该构型分隔符与 47/43 都不同。</summary>
    private const string Sep41 = "*****************************************";
    private const string Dash = "——————————————————";

    private static readonly IBrush ColorNormal = Brushes.Black;
    private static readonly IBrush ColorRule = new SolidColorBrush(Color.Parse("#4B5563"));
    private static readonly IBrush ColorOk = new SolidColorBrush(Color.Parse("#0000C0"));
    private static readonly IBrush ColorFail = new SolidColorBrush(Color.Parse("#C00000"));

    [RelayCommand]
    private void Preview()
    {
        if (!PreviewEnabled) { StatusIsWarning = true; StatusText = PreviewBlockedReason; return; }

        double a = Session.GlassShortSpanMm, b = Session.GlassLongSpanMm;
        double wk = Session.PanelWindKpa / 1000.0;      // kPa → N/mm²
        double alpha = Session.AlphaMax;

        Results.Clear();
        try
        {
            switch (Kind)
            {
                case GlassBuildKind.Single:
                {
                    var r = GlassEngine.Compute(wk, a, b, Session.GlassOuterThicknessMm, alpha);
                    Add(Sep47, ColorRule);
                    Add($"玻璃部分风荷载标准值为:{Session.PanelWindKpa:0.###}KPa", ColorNormal);
                    Add(Sep47, ColorRule);
                    Add($"玻璃强度计算值:{r.Sigma:0.###}MPa", ColorNormal);
                    Add($"选用玻璃的强度设计值:{GlassAllowables.Caption(Session.GlassOuterKind, Session.GlassOuterThicknessMm)}", ColorNormal);
                    Judge(r.Sigma <= GlassAllowables.Bending(Session.GlassOuterKind, Session.GlassOuterThicknessMm));
                    Add(Sep47, ColorRule);
                    Add($"玻璃挠度计算值:{r.Deflection:0.###}mm", ColorNormal);
                    Add($"选用玻璃挠度容许值:{r.Limit:0.###}mm({Session.GlassCaption})", ColorNormal);
                    Judge(r.Deflection <= r.Limit);
                    Add(Sep47, ColorRule);
                    break;
                }
                case GlassBuildKind.Insulated:
                {
                    var r = GlassEngine.ComputeInsulated(wk, a, b, Session.GlassOuterThicknessMm,
                                                        Session.GlassInnerThicknessMm, alpha);
                    Add(Sep43, ColorRule);
                    Add($"玻璃部分风荷载标准值为:{Session.PanelWindKpa:0.###}KPa", ColorNormal);
                    Add(Sep43, ColorRule);
                    Add($"外片玻璃强度计算值:{r.Outer.Sigma:0.###}MPa", ColorNormal);
                    Add($"选用的外片玻璃强度设计值:{GlassAllowables.Caption(Session.GlassOuterKind, Session.GlassOuterThicknessMm)}", ColorNormal);
                    Judge(r.Outer.Sigma <= GlassAllowables.Bending(Session.GlassOuterKind, Session.GlassOuterThicknessMm));
                    Add(Dash, ColorRule);
                    Add($"内片玻璃强度计算值:{r.Inner.Sigma:0.###}MPa", ColorNormal);
                    Add($"选用的内片玻璃强度设计值:{GlassAllowables.Caption(Session.GlassInnerKind, Session.GlassInnerThicknessMm)}", ColorNormal);
                    Judge(r.Inner.Sigma <= GlassAllowables.Bending(Session.GlassInnerKind, Session.GlassInnerThicknessMm));
                    Add(Sep43, ColorRule);
                    Add($"玻璃挠度计算值:{r.Deflection:0.###}mm", ColorNormal);
                    Add($"选用玻璃挠度容许值:{r.Limit:0.###}mm(中空玻璃)", ColorNormal);
                    Judge(r.Deflection <= r.Limit);
                    Add(Sep43, ColorRule);
                    break;
                }
                case GlassBuildKind.Laminated:
                {
                    var r = GlassEngine.ComputeLaminated(wk, a, b, Session.GlassOuterThicknessMm,
                                                        Session.GlassInnerThicknessMm, alpha);
                    Add(Sep47, ColorRule);
                    Add($"玻璃部分风荷载标准值为:{Session.PanelWindKpa:0.###}KPa", ColorNormal);
                    Add(Sep47, ColorRule);
                    Add($"玻璃强度计算值:{r.Ply.Sigma:0.###}MPa", ColorNormal);
                    Add($"选用玻璃的强度设计值:{GlassAllowables.Caption(Session.GlassOuterKind, Session.GlassOuterThicknessMm)}", ColorNormal);
                    Judge(r.Ply.Sigma <= GlassAllowables.Bending(Session.GlassOuterKind, Session.GlassOuterThicknessMm));
                    Add(Sep47, ColorRule);
                    Add($"玻璃挠度计算值:{r.Deflection:0.###}mm", ColorNormal);
                    Add($"选用玻璃挠度容许值:{r.Limit:0.###}mm(夹层玻璃)", ColorNormal);
                    Judge(r.Deflection <= r.Limit);
                    Add(Sep47, ColorRule);
                    break;
                }
                case GlassBuildKind.InsulLamPvb:
                {
                    var r = GlassEngine.ComputeComposite(wk, a, b,
                        new[] { Session.GlassOuterThicknessMm, Session.GlassMidThicknessMm, Session.GlassInnerThicknessMm },
                        InterlayerKind.Pvb, alpha);
                    var kinds = new[] { Session.GlassOuterKind, Session.GlassMidKind, Session.GlassInnerKind };
                    var ths = new[] { Session.GlassOuterThicknessMm, Session.GlassMidThicknessMm, Session.GlassInnerThicknessMm };
                    Add(Sep43, ColorRule);
                    Add($"玻璃部分风荷载标准值为:{Session.PanelWindKpa:0.###}KPa", ColorNormal);
                    Add(Sep43, ColorRule);
                    for (int i = 0; i < r.Plies.Length; i++)
                    {
                        Add($"{i + 1}#玻璃强度计算值:{r.Plies[i].Sigma:0.###}MPa", ColorNormal);
                        Add($"选用的{i + 1}#玻璃强度设计值:{GlassAllowables.Caption(kinds[i], ths[i])}", ColorNormal);
                        Judge(r.Plies[i].Sigma <= GlassAllowables.Bending(kinds[i], ths[i]));
                        if (i < r.Plies.Length - 1) Add(Dash, ColorRule);
                    }
                    Add(Sep43, ColorRule);
                    Add($"玻璃挠度计算值:{r.Deflection:0.###}mm", ColorNormal);
                    Add($"选用玻璃挠度容许值:{r.Limit:0.###}mm(中空+夹层玻璃)", ColorNormal);
                    Judge(r.Deflection <= r.Limit);
                    Add(Sep43, ColorRule);
                    break;
                }
                case GlassBuildKind.InsulLamSgp:
                {
                    var r = GlassEngine.ComputeCompositeSgp(wk, a, b,
                        Session.GlassOuterThicknessMm, Session.GlassMidThicknessMm, Session.GlassInnerThicknessMm,
                        Session.FilmThicknessMm, alpha);
                    Add(Sep43, ColorRule);
                    Add($"玻璃部分风荷载标准值为:{Session.PanelWindKpa:0.###}KPa", ColorNormal);
                    Add(Sep43, ColorRule);
                    Add($"1#玻璃(夹层偏室外侧)强度计算值:{r.Plies[0].Sigma:0.###}MPa", ColorNormal);
                    Add($"选用的1#玻璃强度设计值:{GlassAllowables.Caption(Session.GlassOuterKind, Session.GlassOuterThicknessMm)}", ColorNormal);
                    Judge(r.Plies[0].Sigma <= GlassAllowables.Bending(Session.GlassOuterKind, Session.GlassOuterThicknessMm));
                    Add(Sep43, ColorRule);
                    Add($"玻璃挠度计算值:{r.Deflection:0.###}mm", ColorNormal);
                    Add($"选用玻璃挠度容许值:{r.Limit:0.###}mm(中空+夹层玻璃)", ColorNormal);
                    Judge(r.Deflection <= r.Limit);
                    Add(Sep43, ColorRule);
                    break;
                }
                case GlassBuildKind.DoubleInsulated:
                {
                    // 三玻双中空：三片各算一组，等效厚度逐层衰减（每穿一层腔 ×0.95）
                    // 文案实测（glass_cfg_scan.py --cases dblinsul）：
                    //   41 星；片名「外片 / 中片 / 内片」；容许值后缀「(三玻双中空结构)」
                    var r = GlassEngine.ComputeDoubleInsulated(wk, a, b,
                        Session.GlassOuterThicknessMm, Session.GlassMidThicknessMm, Session.GlassInnerThicknessMm,
                        alpha);
                    var name = new[] { "外片", "中片", "内片" };
                    var kinds = new[] { Session.GlassOuterKind, Session.GlassMidKind, Session.GlassInnerKind };
                    var ths = new[] { Session.GlassOuterThicknessMm, Session.GlassMidThicknessMm, Session.GlassInnerThicknessMm };
                    Add(Sep41, ColorRule);
                    Add($"玻璃部分风荷载标准值为:{Session.PanelWindKpa:0.###}KPa", ColorNormal);
                    Add(Sep41, ColorRule);
                    for (int i = 0; i < r.Plies.Length; i++)
                    {
                        Add($"{name[i]}玻璃强度计算值:{r.Plies[i].Sigma:0.###}MPa", ColorNormal);
                        Add($"选用的{name[i]}玻璃强度设计值:{GlassAllowables.Caption(kinds[i], ths[i])}", ColorNormal);
                        Judge(r.Plies[i].Sigma <= GlassAllowables.Bending(kinds[i], ths[i]));
                        if (i < r.Plies.Length - 1) Add(Dash, ColorRule);
                    }
                    Add(Sep41, ColorRule);
                    Add($"玻璃挠度计算值:{r.Deflection:0.###}mm", ColorNormal);
                    Add($"选用玻璃挠度容许值:{r.Limit:0.###}mm(三玻双中空结构)", ColorNormal);
                    Judge(r.Deflection <= r.Limit);
                    Add(Sep41, ColorRule);
                    break;
                }
                case GlassBuildKind.DoubleLaminated:
                {
                    // 双夹层-中空 = 4 片：外夹层(1#/2#) + 中空腔 + 内夹层(3#/4#)
                    // 实测文案：最外片/次外片/次内片/最内片，41 星分隔，容许值**无「(××玻璃)」后缀**
                    var r = GlassEngine.ComputeDoubleLaminated(wk, a, b,
                        Session.GlassOuterThicknessMm, Session.GlassOuter2ThicknessMm,
                        Session.GlassInnerThicknessMm, Session.GlassInner2ThicknessMm,
                        alpha);
                    var name = new[] { "最外片(1#)", "次外片(2#)", "次内片(3#)", "最内片(4#)" };
                    var kinds = new[]
                    {
                        Session.GlassOuterKind, Session.GlassOuterKind,
                        Session.GlassInnerKind, Session.GlassInnerKind,
                    };
                    var ths = new[]
                    {
                        Session.GlassOuterThicknessMm, Session.GlassOuter2ThicknessMm,
                        Session.GlassInnerThicknessMm, Session.GlassInner2ThicknessMm,
                    };
                    Add(Sep41, ColorRule);
                    Add($"玻璃部分风荷载标准值为:{Session.PanelWindKpa:0.###}KPa", ColorNormal);
                    Add(Sep41, ColorRule);
                    for (int i = 0; i < r.Plies.Length; i++)
                    {
                        Add($"{name[i]}玻璃强度计算值:{r.Plies[i].Sigma:0.###}MPa", ColorNormal);
                        Add($"选用的{name[i]}玻璃强度设计值:{GlassAllowables.Caption(kinds[i], ths[i])}", ColorNormal);
                        Judge(r.Plies[i].Sigma <= GlassAllowables.Bending(kinds[i], ths[i]));
                        if (i < r.Plies.Length - 1) Add(Dash, ColorRule);
                    }
                    Add(Sep41, ColorRule);
                    Add($"玻璃挠度计算值:{r.Deflection:0.###}mm", ColorNormal);
                    Add($"选用玻璃挠度容许值:{r.Limit:0.###}mm", ColorNormal);   // 实测无后缀
                    Judge(r.Deflection <= r.Limit);
                    Add(Sep41, ColorRule);
                    break;
                }
                default:
                    StatusIsWarning = true;
                    StatusText = "该玻璃构型暂不支持预演";
                    return;
            }

            if (ShowTrace)
                Add($"[取证] a={a:0.###} b={b:0.###} t_e 见 doc｜w0 口径={Session.PanelWindSource}", ColorRule);
        }
        finally
        {
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(PlaceholderText));
        }

        StatusIsWarning = !Session.PanelWindResolved;
        StatusText = $"预演完成：{Session.GlassCaption}"
                   + (Session.PanelWindResolved
                        ? "（wk 取面板口径：μsl=1.0 不折减 ψ）"
                        : "（第一页尚未写入面板 wk，暂用支承口径）");
    }

    [RelayCommand] private void Prev() => _wizard.GoBeamCalc();
    [RelayCommand] private void Next()
        => StatusText = "玻璃计算：已完成；连接/埋件/胶类三屏尚未复刻（按面板顺序推进）";
    [RelayCommand] private void Exit() => _wizard.CloseForm();

    private void Judge(bool ok) => Add(ok ? "满足要求(√)" : "不满足要求(×)", ok ? ColorOk : ColorFail);
    private void Add(string text, IBrush color) => Results.Add(new PreviewLine(text, color));
}
