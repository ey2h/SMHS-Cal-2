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
/// 全玻璃幕墙线「设计计算」屏 ViewModel（V31 客户区 903×438，控件坐标见
/// <c>local-data/recon/v31runs/fullglass_design_var.json</c>）。
///
/// <para>大面玻璃由玻璃肋支承（两对边简支），输出玻璃强度/挠度 + 玻璃肋高度/挠度 + 结构胶 6 组校核。
/// 算式与四面对拍见 <c>docs/V31_FULLGLASS_UI.md</c>。</para>
/// </summary>
public partial class FullGlassCalcViewModel : ObservableObject
{
    public CalcSession Session { get; }
    private readonly CalcWizardViewModel _wizard;

    public FullGlassCalcViewModel(CalcSession session, CalcWizardViewModel wizard)
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
                case nameof(CalcSession.FullGlassConfigIndex):
                    OnPropertyChanged(nameof(MidPlyVisible));
                    OnPropertyChanged(nameof(Outer2PlyVisible));
                    OnPropertyChanged(nameof(Inner2PlyVisible));
                    OnPropertyChanged(nameof(InnerPlyVisible));
                    Invalidate("大面玻璃构型已切换");
                    break;
            }
        };
    }

    // ---------------- 玻璃板尺寸 ----------------

    public string ShortSideText
    {
        get => Session.FullGlassShortSideMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.FullGlassShortSideMm = v; Invalidate("玻璃板短边已改"); OnPropertyChanged(); } }
    }

    public string RibSpanText
    {
        get => Session.FullGlassRibSpanMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.FullGlassRibSpanMm = v; Invalidate("玻璃肋跨度已改"); OnPropertyChanged(); } }
    }

    public string ThicknessText
    {
        get => Session.FullGlassThicknessMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.FullGlassThicknessMm = v; Invalidate("玻璃厚度已改"); OnPropertyChanged(); } }
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
            int i = Array.IndexOf(GlassThicknessOptions, Session.FullGlassThicknessMm);
            return i < 0 ? 3 : i;   // 默认 10 = 下标 3
        }
        set
        {
            Session.FullGlassThicknessMm = GlassThicknessOptions[Math.Max(0, Math.Min(GlassThicknessOptions.Length - 1, value))];
            OnPropertyChanged();
            Invalidate("玻璃厚度已改");
        }
    }

    /// <summary>内片玻璃厚度（中空/夹层/三玻构型启用，实机界面 (338,224)）。</summary>
    public int InnerThicknessIndex
    {
        get
        {
            int i = Array.IndexOf(GlassThicknessOptions, Session.FullGlassInnerThicknessMm);
            return i < 0 ? 3 : i;
        }
        set
        {
            Session.FullGlassInnerThicknessMm = GlassThicknessOptions[Math.Max(0, Math.Min(GlassThicknessOptions.Length - 1, value))];
            OnPropertyChanged();
            Invalidate("内片玻璃厚度已改");
        }
    }

    /// <summary>中片玻璃厚度（三玻/中空+夹层构型启用，实机界面 (198,223)）。</summary>
    public int MidThicknessIndex
    {
        get
        {
            int i = Array.IndexOf(GlassThicknessOptions, Session.FullGlassMidThicknessMm);
            return i < 0 ? 3 : i;
        }
        set
        {
            Session.FullGlassMidThicknessMm = GlassThicknessOptions[Math.Max(0, Math.Min(GlassThicknessOptions.Length - 1, value))];
            OnPropertyChanged();
            Invalidate("中片玻璃厚度已改");
        }
    }

    /// <summary>外片第二片厚度（仅「双夹层-中空」启用，实机界面 (89,247)，隐藏副本）。</summary>
    public int Outer2ThicknessIndex
    {
        get
        {
            int i = Array.IndexOf(GlassThicknessOptions, Session.FullGlassOuter2ThicknessMm);
            return i < 0 ? 3 : i;
        }
        set
        {
            Session.FullGlassOuter2ThicknessMm = GlassThicknessOptions[Math.Max(0, Math.Min(GlassThicknessOptions.Length - 1, value))];
            OnPropertyChanged();
            Invalidate("外片第二片厚度已改");
        }
    }

    /// <summary>内片第二片厚度（仅「双夹层-中空」启用，实机界面 (338,247)，隐藏副本）。</summary>
    public int Inner2ThicknessIndex
    {
        get
        {
            int i = Array.IndexOf(GlassThicknessOptions, Session.FullGlassInner2ThicknessMm);
            return i < 0 ? 3 : i;
        }
        set
        {
            Session.FullGlassInner2ThicknessMm = GlassThicknessOptions[Math.Max(0, Math.Min(GlassThicknessOptions.Length - 1, value))];
            OnPropertyChanged();
            Invalidate("内片第二片厚度已改");
        }
    }

    /// <summary>含玻璃及中空层的玻璃面板总厚度（mm，纯校验，须 &gt; 玻璃厚度和 + 中空层数×12）。</summary>
    public string TotalThicknessText
    {
        get => Session.FullGlassTotalThicknessMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.FullGlassTotalThicknessMm = v; Invalidate("玻璃面板总厚度已改"); OnPropertyChanged(); } }
    }

    // ---------------- 玻璃肋参数 ----------------

    public IReadOnlyList<string> RibThicknessItems { get; } =
        FullGlassEngine.RibThicknessOptions.Select(x => x.ToString("0")).ToList();

    public int RibThicknessIndex
    {
        get
        {
            int i = Array.IndexOf(FullGlassEngine.RibThicknessOptions, Session.FullGlassRibThicknessMm);
            return i < 0 ? 3 : i;   // 默认 12 = 下标 3
        }
        set
        {
            var arr = FullGlassEngine.RibThicknessOptions;
            Session.FullGlassRibThicknessMm = arr[Math.Max(0, Math.Min(arr.Length - 1, value))];
            OnPropertyChanged();
            Invalidate("玻璃肋厚度已改");
        }
    }

    public string RibHeightText
    {
        get => Session.FullGlassRibHeightMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.FullGlassRibHeightMm = v; Invalidate("玻璃肋高度已改"); OnPropertyChanged(); } }
    }

    private static bool TryNum(string? s, out double v)
        => double.TryParse((s ?? "").Trim(), System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out v) && v > 0;

    /// <summary>各构型的片厚度列表（总厚度校验用：总厚度须 &gt; 各片之和）。</summary>
    private double[] PlyThicknesses(int cfg) => cfg switch
    {
        3 => new[] { Session.FullGlassThicknessMm, Session.FullGlassOuter2ThicknessMm,
                     Session.FullGlassInnerThicknessMm, Session.FullGlassInner2ThicknessMm },
        1 or 2 => new[] { Session.FullGlassThicknessMm, Session.FullGlassInnerThicknessMm },
        _ => new[] { Session.FullGlassThicknessMm, Session.FullGlassMidThicknessMm, Session.FullGlassInnerThicknessMm },
    };

    // ---------------- 构型 → 片厚度下拉可见性（V31 实机隐藏副本） ----------------

    /// <summary>中片可见：中空+夹层外/内、三玻双中空、三玻双夹层。</summary>
    public bool MidPlyVisible => Session.FullGlassConfigIndex is 4 or 5 or 6 or 7;

    /// <summary>外片第二片可见：仅「双夹层-中空」。</summary>
    public bool Outer2PlyVisible => Session.FullGlassConfigIndex == 3;

    /// <summary>内片第二片可见：仅「双夹层-中空」。</summary>
    public bool Inner2PlyVisible => Session.FullGlassConfigIndex == 3;

    /// <summary>内片可见：非单片构型。</summary>
    public bool InnerPlyVisible => Session.FullGlassConfigIndex != 0;

    // ---------------- 结果 ----------------

    public ObservableCollection<PreviewLine> Results { get; } = new();
    public bool HasResults => Results.Count > 0;
    public string PlaceholderText => HasResults ? "" : "点「预演」按当前玻璃板尺寸与玻璃肋参数计算。";

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _statusIsWarning;

    public bool PreviewEnabled => Session.PanelWindReady && Session.FullGlassShortSideMm > 0 && Session.FullGlassRibSpanMm > 0;

    public string PreviewBlockedReason => !Session.PanelWindReady
        ? CalcSession.UpstreamNotReadyText
        : Session.FullGlassShortSideMm <= 0 || Session.FullGlassRibSpanMm <= 0
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

    /// <summary>实机分隔符：47 星 / 41 星（双夹层-中空）/ 21 破折号 / 20 破折号（— U+2014）。</summary>
    private const string Sep47 = "***********************************************";
    private const string Sep41 = "*****************************************";
    private const string Dash21 = "—————————————————————";
    private const string Dash20 = "————————————————————";

    private static readonly IBrush ColorNormal = Brushes.Black;
    private static readonly IBrush ColorRule = new SolidColorBrush(Color.Parse("#4B5563"));
    private static readonly IBrush ColorOk = new SolidColorBrush(Color.Parse("#0000C0"));
    private static readonly IBrush ColorFail = new SolidColorBrush(Color.Parse("#C00000"));

    [RelayCommand]
    private void Preview()
    {
        if (!PreviewEnabled) { StatusIsWarning = true; StatusText = PreviewBlockedReason; return; }

        var it = new FullGlassEngine.Input
        {
            GlassShortSideMm = Session.FullGlassShortSideMm,
            RibSpanMm = Session.FullGlassRibSpanMm,
            GlassThicknessMm = Session.FullGlassThicknessMm,
            InnerThicknessMm = Session.FullGlassInnerThicknessMm,
            MidThicknessMm = Session.FullGlassMidThicknessMm,
            Outer2ThicknessMm = Session.FullGlassOuter2ThicknessMm,
            Inner2ThicknessMm = Session.FullGlassInner2ThicknessMm,
            TotalThicknessMm = Session.FullGlassTotalThicknessMm,
            RibThicknessMm = Session.FullGlassRibThicknessMm,
            RibActualHeightMm = Session.FullGlassRibHeightMm,
            AlphaMax = Session.AlphaMax,
            Wk = Session.PanelWindNmm2,             // ★ N/mm²，不经 kPa 反除
        };

        int cfg = Session.FullGlassConfigIndex;

        Results.Clear();
        try
        {
            // 总厚度校验（非单片构型均须 > 各片玻璃厚度之和）
            if (cfg != 0 && FullGlassEngine.TotalThicknessInvalid(
                Session.FullGlassTotalThicknessMm, PlyThicknesses(cfg)))
            {
                StatusIsWarning = true;
                StatusText = "请您注意，您输入的玻璃厚度参数无效!";
                return;
            }

            FullGlassEngine.Result r = cfg switch
            {
                1 => FullGlassEngine.ComputeInsulated(it),
                2 => FullGlassEngine.ComputeLaminated(it),
                3 => FullGlassEngine.ComputeDoubleLamInsul(it),
                4 => FullGlassEngine.ComputeInsulLamOuter(it),
                5 => FullGlassEngine.ComputeInsulLamInner(it),
                6 => FullGlassEngine.ComputeTripleInsul(it),
                7 => FullGlassEngine.ComputeTripleLam(it),
                _ => FullGlassEngine.Compute(it),
            };
            Session.LastFullGlassResult = r;

            bool dblLamInsul = cfg == 3;
            var sep = dblLamInsul ? Sep41 : Sep47;

            Add(sep, ColorRule);
            Add(dblLamInsul
                ? $"玻璃部分风荷载标准值为:{r.WindStandardKpa:0.##}KPa"
                : $"风荷载标准值为:{r.WindStandardKpa:0.##}KPa", ColorNormal);
            Add(sep, ColorRule);

            switch (cfg)
            {
                case 2:  // 夹层：内片在前、外片在后，标签「选用XX玻璃的强度设计值」
                    Add($"内片玻璃强度计算值:{r.InnerSigma:0.###}MPa", ColorNormal);
                    Add($"选用内片玻璃的强度设计值:{r.InnerSigmaAllowable:0.###}MPa(钢化玻璃)", ColorNormal);
                    Judge(r.InnerSigmaOk);
                    Add(Dash20, ColorRule);
                    Add($"外片玻璃强度计算值:{r.OuterSigma:0.###}MPa", ColorNormal);
                    Add($"选用外片玻璃的强度设计值:{r.OuterSigmaAllowable:0.###}MPa(钢化玻璃)", ColorNormal);
                    Judge(r.OuterSigmaOk);
                    Add(Dash20, ColorRule);
                    break;

                case 3:  // 双夹层-中空：4 片（最外1#/次外2#/次内3#/最内4#）
                    Add($"最外片(1#)玻璃强度计算值:{r.OuterSigma:0.###}MPa", ColorNormal);
                    Add($"选用的最外片(1#)玻璃强度设计值:{r.OuterSigmaAllowable:0.###}MPa(钢化玻璃)", ColorNormal);
                    Judge(r.OuterSigmaOk);
                    Add(Dash20, ColorRule);
                    Add($"次外片(2#)玻璃强度计算值:{r.Outer2Sigma:0.###}MPa", ColorNormal);
                    Add($"选用的次外片(2#)玻璃强度设计值:{r.Outer2SigmaAllowable:0.###}MPa(钢化玻璃)", ColorNormal);
                    Judge(r.Outer2SigmaOk);
                    Add(Dash20, ColorRule);
                    Add($"次内片(3#)玻璃强度计算值:{r.InnerSigma:0.###}MPa", ColorNormal);
                    Add($"选用的次内片(3#)玻璃强度设计值:{r.InnerSigmaAllowable:0.###}MPa(钢化玻璃)", ColorNormal);
                    Judge(r.InnerSigmaOk);
                    Add(Dash20, ColorRule);
                    Add($"最内片(4#)玻璃强度计算值:{r.Inner2Sigma:0.###}MPa", ColorNormal);
                    Add($"选用的最内片(4#)玻璃强度设计值:{r.Inner2SigmaAllowable:0.###}MPa(钢化玻璃)", ColorNormal);
                    Judge(r.Inner2SigmaOk);
                    Add(sep, ColorRule);   // 4# 后是 41 星（非 20dash）
                    break;

                case 4:  // 中空+夹层外：外片+中片+内片
                case 5:  // 中空+夹层内：外片+中片+内片
                case 6:  // 三玻双中空：外片+中片+内片
                case 7:  // 三玻双夹层：外片+中片+内片
                    Add($"外片玻璃强度计算值:{r.OuterSigma:0.###}MPa", ColorNormal);
                    Add($"选用的外片玻璃强度设计值:{r.OuterSigmaAllowable:0.###}MPa(钢化玻璃)", ColorNormal);
                    Judge(r.OuterSigmaOk);
                    Add(Dash20, ColorRule);
                    Add($"中片玻璃强度计算值:{r.MidSigma:0.###}MPa", ColorNormal);
                    Add($"选用的中片玻璃强度设计值:{r.MidSigmaAllowable:0.###}MPa(钢化玻璃)", ColorNormal);
                    Judge(r.MidSigmaOk);
                    Add(Dash20, ColorRule);
                    Add($"内片玻璃强度计算值:{r.InnerSigma:0.###}MPa", ColorNormal);
                    Add($"选用的内片玻璃强度设计值:{r.InnerSigmaAllowable:0.###}MPa(钢化玻璃)", ColorNormal);
                    Judge(r.InnerSigmaOk);
                    Add(Dash20, ColorRule);
                    break;

                case 1:  // 中空：外片+内片
                    Add($"外片玻璃强度计算值:{r.OuterSigma:0.###}MPa", ColorNormal);
                    Add($"选用的外片玻璃强度设计值:{r.OuterSigmaAllowable:0.###}MPa(钢化玻璃)", ColorNormal);
                    Judge(r.OuterSigmaOk);
                    Add(Dash20, ColorRule);
                    Add($"内片玻璃强度计算值:{r.InnerSigma:0.###}MPa", ColorNormal);
                    Add($"选用的内片玻璃强度设计值:{r.InnerSigmaAllowable:0.###}MPa(钢化玻璃)", ColorNormal);
                    Judge(r.InnerSigmaOk);
                    Add(Dash20, ColorRule);
                    break;

                default:  // 单片
                    Add($"玻璃强度计算值:{r.GlassSigma:0.###}MPa", ColorNormal);
                    Add($"选用玻璃的强度设计值:{r.GlassSigmaAllowable:0.###}MPa(钢化玻璃)", ColorNormal);
                    Judge(r.GlassSigmaOk);
                    Add(Dash21, ColorRule);
                    break;
            }

            string deflTag = cfg switch
            {
                1 => "中空玻璃",
                2 => "等片夹层板块",
                3 => "",                                  // 双夹层-中空无括号
                4 or 5 => "中空夹层玻璃",
                6 => "双中空玻璃",
                7 => "双夹层玻璃",
                _ => "钢化玻璃",
            };
            Add($"玻璃挠度计算值:{r.GlassDeflection:0.###}mm", ColorNormal);
            Add(deflTag.Length == 0
                ? $"选用玻璃挠度容许值:{r.GlassDeflectionAllowable:0.###}mm"
                : $"选用玻璃挠度容许值:{r.GlassDeflectionAllowable:0.###}mm({deflTag})", ColorNormal);
            Judge(r.GlassDeflectionOk);
            Add(sep, ColorRule);

            Add($"玻璃肋实际截面高度为:{r.RibActualHeight:0.###}mm", ColorNormal);
            Add($"玻璃肋实际截面高度不应小于:{r.RibRequiredHeight:0.###}mm", ColorNormal);
            Judge(r.RibHeightOk);
            Add(Dash20, ColorRule);

            Add($"玻璃肋挠度计算值:{r.RibDeflection:0.###}mm", ColorNormal);
            Add($"玻璃肋挠度限值:{r.RibDeflectionAllowable:0.###}mm", ColorNormal);
            Judge(r.RibDeflectionOk);
            Add(Sep47, ColorRule);

            Add($"硅酮结构胶在荷载作用下强度校核计算为:{r.SealantStress:0.###}MPa", ColorNormal);
            Add($"硅酮结构胶在荷载作用下强度设计值为{r.SealantAllowable:0.###}MPa", ColorNormal);
            Judge(r.SealantOk);
            Add(Sep47, ColorRule);
        }
        finally
        {
            Session.LastFullGlassSummary = CalcSession.SummaryOf(Results);
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(PlaceholderText));
        }

        StatusIsWarning = !Session.PanelWindResolved;
        StatusText = $"预演完成：玻璃 {Session.FullGlassShortSideMm:0.###}×{Session.FullGlassRibSpanMm:0.###}mm"
                   + $" t={Session.FullGlassThicknessMm:0.###}mm"
                   + $"｜肋 t={Session.FullGlassRibThicknessMm:0} h={Session.FullGlassRibHeightMm:0}";
    }

    [RelayCommand] private void Prev() => _wizard.GoBasic();
    [RelayCommand] private void Next() => _wizard.GoBookPanel();
    [RelayCommand] private void Exit() => _wizard.CloseForm();

    private void Judge(bool ok) => Add(ok ? "满足要求(√)" : "不满足要求(×)", ok ? ColorOk : ColorFail);
    private void Add(string text, IBrush color) => Results.Add(new PreviewLine(text, color));
}
