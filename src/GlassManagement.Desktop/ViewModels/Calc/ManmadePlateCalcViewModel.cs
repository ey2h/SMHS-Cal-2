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
/// 人造板材线「板块计算」屏 ViewModel（V31 客户区 782×536，控件坐标见
/// <c>local-data/recon/v31runs/chain_manmade_ci.txt</c> 第 4 屏）。
///
/// <para>覆盖「短挂件连接」的瓷板 / 陶板 / 微晶玻璃板三种面板（共用一套引擎，
/// 面板种类只改重度 γ 与默认强度）。其余连接方式（背栓/通长挂件/穿透支承等）属 B2 后续项，未做。</para>
///
/// <para>算式与四面对拍见 <c>docs/V31_MANMADE_PLATE_UI.md</c>。</para>
/// </summary>
public partial class ManmadePlateCalcViewModel : ObservableObject
{
    public CalcSession Session { get; }
    private readonly CalcWizardViewModel _wizard;

    public ManmadePlateCalcViewModel(CalcSession session, CalcWizardViewModel wizard)
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

    // ---------------- 面板尺寸参数 ----------------

    public string ThicknessText
    {
        get => Session.ManmadeThicknessMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.ManmadeThicknessMm = v; Invalidate("板材总厚度已改"); OnPropertyChanged(); } }
    }

    public string ShortSideText
    {
        get => Session.ManmadeShortSideMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.ManmadeShortSideMm = v; Invalidate("短边边长已改"); OnPropertyChanged(); } }
    }

    public string LongSideText
    {
        get => Session.ManmadeLongSideMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.ManmadeLongSideMm = v; Invalidate("长边边长已改"); OnPropertyChanged(); } }
    }

    public string SmallSupportText
    {
        get => Session.ManmadeSmallSupportMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.ManmadeSmallSupportMm = v; Invalidate("小支承点间距已改"); OnPropertyChanged(); } }
    }

    public string LargeSupportText
    {
        get => Session.ManmadeLargeSupportMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.ManmadeLargeSupportMm = v; Invalidate("大支承点间距已改"); OnPropertyChanged(); } }
    }

    // ---------------- 面板强度参数 ----------------

    public string BendingAllowableText
    {
        get => Session.ManmadeAllowableBendingMpa.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.ManmadeAllowableBendingMpa = v; Invalidate("抗弯强度设计值已改"); OnPropertyChanged(); } }
    }

    public string ShearAllowableText
    {
        get => Session.ManmadeAllowableShearMpa.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.ManmadeAllowableShearMpa = v; Invalidate("抗剪强度设计值已改"); OnPropertyChanged(); } }
    }

    // ---------------- 其它计算参数 ----------------

    public string SlotMouthText
    {
        get => Session.ManmadeSlotMouthMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.ManmadeSlotMouthMm = v; Invalidate("槽口宽度剪面厚度已改"); OnPropertyChanged(); } }
    }

    public string SlotShearLengthText
    {
        get => Session.ManmadeSlotShearLengthMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.ManmadeSlotShearLengthMm = v; Invalidate("槽口剪切面总长度已改"); OnPropertyChanged(); } }
    }

    public IReadOnlyList<string> HangerTotalItems { get; } = new[] { "2", "4", "6", "8", "10", "12" };

    public int HangerTotalIndex
    {
        get
        {
            int i = ManmadePlateEngine.HangerTotalCountOptions.ToList().IndexOf(Session.ManmadeHangerTotalCount);
            return i < 0 ? 1 : i;   // 默认 4 = 下标 1
        }
        set
        {
            var arr = ManmadePlateEngine.HangerTotalCountOptions;
            Session.ManmadeHangerTotalCount = arr[Math.Max(0, Math.Min(arr.Count - 1, value))];
            OnPropertyChanged();
            Invalidate("挂件总数已改");
        }
    }

    // ---------------- 挂件参数 ----------------

    public IReadOnlyList<string> HangerMaterials { get; } = ManmadePlateEngine.HangMaterialOptions;

    public int HangerMaterialIndex
    {
        get
        {
            int i = ManmadePlateEngine.HangMaterialOptions.ToList().IndexOf(Session.ManmadeHangerMaterial);
            return i < 0 ? 7 : i;
        }
        set
        {
            var arr = ManmadePlateEngine.HangMaterialOptions;
            Session.ManmadeHangerMaterial = arr[Math.Max(0, Math.Min(arr.Count - 1, value))];
            OnPropertyChanged();
            Invalidate("挂件材质已改");
        }
    }

    public string HangerWidthText
    {
        get => Session.ManmadeHangerWidthMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.ManmadeHangerWidthMm = v; Invalidate("挂件宽度已改"); OnPropertyChanged(); } }
    }

    public string HangerThicknessText
    {
        get => Session.ManmadeHangerThicknessMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.ManmadeHangerThicknessMm = v; Invalidate("挂件厚度已改"); OnPropertyChanged(); } }
    }

    public string EccentricityText
    {
        get => Session.ManmadeHangerEccentricityMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.ManmadeHangerEccentricityMm = v; Invalidate("参数e已改"); OnPropertyChanged(); } }
    }

    public string HangerLengthText
    {
        get => Session.ManmadeHangerLengthMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.ManmadeHangerLengthMm = v; Invalidate("参数L已改"); OnPropertyChanged(); } }
    }

    public IReadOnlyList<string> HorizontalHangerItems { get; } = new[] { "1", "2", "3", "4", "5", "6" };

    public int HorizontalHangerIndex
    {
        get => Math.Max(0, Math.Min(5, Session.ManmadeHorizontalHangerCount - 1));
        set { Session.ManmadeHorizontalHangerCount = Math.Max(1, Math.Min(6, value + 1)); OnPropertyChanged(); Invalidate("水平挂件数已改"); }
    }

    public IReadOnlyList<string> LoadHangerItems { get; } = new[] { "1", "2", "3", "4", "5", "6" };

    public int LoadHangerIndex
    {
        get => Math.Max(0, Math.Min(5, Session.ManmadeLoadHangerCount - 1));
        set { Session.ManmadeLoadHangerCount = Math.Max(1, Math.Min(6, value + 1)); OnPropertyChanged(); Invalidate("承重挂件数已改"); }
    }

    // ---------------- 螺栓 ----------------

    public IReadOnlyList<string> BoltMaterials { get; } = ManmadePlateEngine.BoltMaterialOptions;

    public int BoltMaterialIndex
    {
        get
        {
            int i = ManmadePlateEngine.BoltMaterialOptions.ToList().IndexOf(Session.ManmadeBoltMaterial);
            return i < 0 ? 0 : i;
        }
        set
        {
            var arr = ManmadePlateEngine.BoltMaterialOptions;
            Session.ManmadeBoltMaterial = arr[Math.Max(0, Math.Min(arr.Count - 1, value))];
            OnPropertyChanged();
            Invalidate("螺栓材质已改");
        }
    }

    public IReadOnlyList<string> BoltDiameterItems { get; } = new[] { "5", "6", "7", "8", "10" };

    public int BoltDiameterIndex
    {
        get
        {
            int i = ManmadePlateEngine.BoltDiameterOptions.ToList().IndexOf(Session.ManmadeBoltDiameterMm);
            return i < 0 ? 3 : i;
        }
        set
        {
            var arr = ManmadePlateEngine.BoltDiameterOptions;
            Session.ManmadeBoltDiameterMm = arr[Math.Max(0, Math.Min(arr.Count - 1, value))];
            OnPropertyChanged();
            Invalidate("螺栓直径已改");
        }
    }

    public IReadOnlyList<string> BoltPerHangerItems { get; } = new[] { "1", "2", "3", "4" };

    public int BoltPerHangerIndex
    {
        get => Math.Max(0, Math.Min(3, Session.ManmadeBoltPerHanger - 1));
        set { Session.ManmadeBoltPerHanger = Math.Max(1, Math.Min(4, value + 1)); OnPropertyChanged(); Invalidate("每挂件螺栓数已改"); }
    }

    private static bool TryNum(string? s, out double v)
        => double.TryParse((s ?? "").Trim(), System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out v) && v > 0;

    // ---------------- 结果 ----------------

    public ObservableCollection<PreviewLine> Results { get; } = new();
    public bool HasResults => Results.Count > 0;
    public string PlaceholderText => HasResults ? "" : "点「预演」按当前面板尺寸与挂件/螺栓参数计算。";

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _statusIsWarning;

    public bool PreviewEnabled => Session.PanelWindReady && Session.ManmadeShortSideMm > 0 && Session.ManmadeLongSideMm > 0;

    public string PreviewBlockedReason => !Session.PanelWindReady
        ? CalcSession.UpstreamNotReadyText
        : Session.ManmadeShortSideMm <= 0 || Session.ManmadeLongSideMm <= 0
            ? "面板尺寸无效，请检查短边/长边"
            : "";

    public string WindNote => Session.PanelWindSource;

    public string InputSummary =>
        $"{Session.ManmadeSpecies} {Session.ManmadeLongSideMm:0.###}×{Session.ManmadeShortSideMm:0.###}mm"
        + $"｜t={Session.ManmadeThicknessMm:0.###}mm｜f={Session.ManmadeAllowableBendingMpa:0.###}MPa"
        + $"｜wk={Session.PanelWindKpa:0.###}kPa"
        + $"｜{Session.ManmadeHangerMaterial}｜M{Session.ManmadeBoltDiameterMm:0}";

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

    /// <summary>实机分隔符：40 星 / 20 个全角破折号（— U+2014）。</summary>
    private const string Sep40 = "****************************************";
    private const string Dash20 = "————————————————————";

    private static readonly IBrush ColorNormal = Brushes.Black;
    private static readonly IBrush ColorRule = new SolidColorBrush(Color.Parse("#4B5563"));
    private static readonly IBrush ColorOk = new SolidColorBrush(Color.Parse("#0000C0"));
    private static readonly IBrush ColorFail = new SolidColorBrush(Color.Parse("#C00000"));

    [RelayCommand]
    private void Preview()
    {
        if (!PreviewEnabled) { StatusIsWarning = true; StatusText = PreviewBlockedReason; return; }

        var it = new ManmadePlateEngine.Input
        {
            Species = Session.ManmadeSpecies,
            ThicknessMm = Session.ManmadeThicknessMm,
            ShortSideMm = Session.ManmadeShortSideMm,
            LongSideMm = Session.ManmadeLongSideMm,
            SmallSupportMm = Session.ManmadeSmallSupportMm,
            LargeSupportMm = Session.ManmadeLargeSupportMm,
            AllowableBendingMpa = Session.ManmadeAllowableBendingMpa,
            AllowableShearMpa = Session.ManmadeAllowableShearMpa,
            SlotMouthMm = Session.ManmadeSlotMouthMm,
            SlotShearLengthMm = Session.ManmadeSlotShearLengthMm,
            HangerTotalCount = Session.ManmadeHangerTotalCount,
            HangerAllowableShearMpa = Session.ManmadeHangerAllowableShearMpa,
            HangerShearAreaMm2 = Session.ManmadeHangerShearAreaMm2,
            HangerWidthMm = Session.ManmadeHangerWidthMm,
            HangerThicknessMm = Session.ManmadeHangerThicknessMm,
            HangerEccentricityMm = Session.ManmadeHangerEccentricityMm,
            HangerLengthMm = Session.ManmadeHangerLengthMm,
            HangerMaterial = Session.ManmadeHangerMaterial,
            HorizontalHangerCount = Session.ManmadeHorizontalHangerCount,
            LoadHangerCount = Session.ManmadeLoadHangerCount,
            BoltPerHanger = Session.ManmadeBoltPerHanger,
            BoltMaterial = Session.ManmadeBoltMaterial,
            BoltDiameterMm = Session.ManmadeBoltDiameterMm,
            AlphaMax = Session.AlphaMax,
            Wk = Session.PanelWindNmm2,             // ★ N/mm²，不经 kPa 反除
        };

        Results.Clear();
        try
        {
            var r = ManmadePlateEngine.Compute(it);
            Session.LastManmadeResult = r;

            Add(Sep40, ColorRule);
            Add($"面板部分风荷载标准值为:{r.WindStandardKpa:0.##}KPa", ColorNormal);
            Add(Sep40, ColorRule);

            Add($"{Session.ManmadeSpecies}抗弯强度计算值:{r.StressComputed:0.###}MPa", ColorNormal);
            Add($"选用的{Session.ManmadeSpecies}抗弯强度设计值:{r.StressAllowable:0.###}MPa", ColorNormal);
            Judge(r.StressOk);
            Add(Dash20, ColorRule);

            Add($"{Session.ManmadeSpecies}抗剪强度计算值:{r.ShearComputed:0.###}MPa", ColorNormal);
            Add($"选用的{Session.ManmadeSpecies}抗剪强度设计值:{r.ShearAllowable:0.###}MPa", ColorNormal);
            Judge(r.ShearOk);
            Add(Sep40, ColorRule);

            Add($"水平荷载作用下挂件抗剪计算值:{r.HangerShearHorizontal:0.###}MPa", ColorNormal);
            Add($"选用挂件的抗剪承载力设计值:{r.HangerShearAllowable:0.###}MPa", ColorNormal);
            Judge(r.HangerShearHorizontalOk);
            Add(Dash20, ColorRule);

            Add($"自重荷载作用下挂件抗剪计算值:{r.HangerShearVertical:0.###}MPa", ColorNormal);
            Add($"选用挂件的抗剪承载力设计值:{r.HangerShearAllowable:0.###}MPa", ColorNormal);
            Judge(r.HangerShearVerticalOk);
            Add(Sep40, ColorRule);

            Add($"挂件的抗弯强度计算值:{r.HangerStressComputed:0.###}MPa", ColorNormal);
            Add($"所选挂件的抗弯强度设计值:{r.HangerStressAllowable:0.###}MPa", ColorNormal);
            Judge(r.HangerStressOk);
            Add(Sep40, ColorRule);

            Add($"挂件的挠度计算值:{r.HangerDeflectionComputed:0.###}mm", ColorNormal);
            Add($"所选挂件的挠度设计值:{r.HangerDeflectionAllowable:0.###}mm", ColorNormal);
            Judge(r.HangerDeflectionOk);
            Add(Sep40, ColorRule);

            Add($"螺栓的抗拉承受力计算值:{r.BoltTensionComputed:0.###}N", ColorNormal);
            Add($"所选螺栓的抗拉承受力设计值:{r.BoltTensionAllowable:0.###}N", ColorNormal);
            Judge(r.BoltTensionOk);
            Add(Dash20, ColorRule);

            Add($"螺栓的抗剪承受力计算值:{r.BoltShearComputed:0.###}N", ColorNormal);
            Add($"所选螺栓的抗剪承受力设计值:{r.BoltShearAllowable:0.###}N", ColorNormal);
            Judge(r.BoltShearOk);
            Add(Dash20, ColorRule);

            Add($"螺栓的拉剪复合承载力计算值:{r.BoltCombined:0.######}", ColorNormal);
            Add("所选螺栓的拉剪复合承载力计算值不得大于1", ColorNormal);
            Judge(r.BoltCombinedOk);
            Add(Sep40, ColorRule);
        }
        finally
        {
            Session.LastManmadeSummary = CalcSession.SummaryOf(Results);
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(PlaceholderText));
        }

        StatusIsWarning = !Session.PanelWindResolved;
        StatusText = $"预演完成：{Session.ManmadeSpecies} {Session.ManmadeLongSideMm:0.###}×{Session.ManmadeShortSideMm:0.###}mm"
                   + $" t={Session.ManmadeThicknessMm:0.###}mm"
                   + $"｜{Session.ManmadeHangerMaterial}｜M{Session.ManmadeBoltDiameterMm:0}";
    }

    [RelayCommand] private void Prev() => _wizard.GoBeamCalc();
    /// <summary>人造板板块屏「下一步」：进入连接计算屏。</summary>
    [RelayCommand] private void Next() => _wizard.StepFromGlassNext();
    [RelayCommand] private void Exit() => _wizard.CloseForm();

    private void Judge(bool ok) => Add(ok ? "满足要求(√)" : "不满足要求(×)", ok ? ColorOk : ColorFail);
    private void Add(string text, IBrush color) => Results.Add(new PreviewLine(text, color));
}
