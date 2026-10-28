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
/// 背栓式点挂幕墙线「板块计算」屏 ViewModel（V31 客户区 782×536，控件坐标见
/// <c>local-data/recon/v31runs/backbolt_granite_var.json</c>）。
///
/// <para>覆盖背栓式点挂幕墙的 8 种面板（石材/瓷板/陶板/轻质陶瓷板/微晶玻璃/纤维水泥板/木纤维板/玻璃纤维板，
/// 共用一套引擎，面板种类只改重度 γ 与默认强度）。</para>
///
/// <para>算式与四面对拍见 <c>docs/V31_BACKBOLT_PLATE_UI.md</c>。</para>
/// </summary>
public partial class BackboltPlateCalcViewModel : ObservableObject
{
    public CalcSession Session { get; }
    private readonly CalcWizardViewModel _wizard;

    public BackboltPlateCalcViewModel(CalcSession session, CalcWizardViewModel wizard)
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
        get => Session.BackboltThicknessMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.BackboltThicknessMm = v; Invalidate("板材总厚度已改"); OnPropertyChanged(); } }
    }

    public string CalcThicknessText
    {
        get => Session.BackboltCalcThicknessMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.BackboltCalcThicknessMm = v; Invalidate("板材计算厚度已改"); OnPropertyChanged(); } }
    }

    public string ShortSideText
    {
        get => Session.BackboltShortSideMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.BackboltShortSideMm = v; Invalidate("短边边长已改"); OnPropertyChanged(); } }
    }

    public string LongSideText
    {
        get => Session.BackboltLongSideMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.BackboltLongSideMm = v; Invalidate("长边边长已改"); OnPropertyChanged(); } }
    }

    public string SmallSupportText
    {
        get => Session.BackboltSmallSupportMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.BackboltSmallSupportMm = v; Invalidate("小支承点间距已改"); OnPropertyChanged(); } }
    }

    public string LargeSupportText
    {
        get => Session.BackboltLargeSupportMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.BackboltLargeSupportMm = v; Invalidate("大支承点间距已改"); OnPropertyChanged(); } }
    }

    public string DrillDepthText
    {
        get => Session.BackboltDrillDepthMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.BackboltDrillDepthMm = v; Invalidate("背栓钻孔深度已改"); OnPropertyChanged(); } }
    }

    public string HoleDiameterText
    {
        get => Session.BackboltHoleDiameterMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.BackboltHoleDiameterMm = v; Invalidate("前端拓孔直径已改"); OnPropertyChanged(); } }
    }

    // ---------------- 面板强度参数 ----------------

    public string BendingAllowableText
    {
        get => Session.BackboltAllowableBendingMpa.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.BackboltAllowableBendingMpa = v; Invalidate("抗弯强度设计值已改"); OnPropertyChanged(); } }
    }

    public string ShearAllowableText
    {
        get => Session.BackboltAllowableShearMpa.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.BackboltAllowableShearMpa = v; Invalidate("抗剪强度设计值已改"); OnPropertyChanged(); } }
    }

    // ---------------- 其它计算参数 ----------------

    public string TensionCapacityText
    {
        get => Session.BackboltTensionCapacityN.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.BackboltTensionCapacityN = v; Invalidate("背栓受拉破坏承载力已改"); OnPropertyChanged(); } }
    }

    public IReadOnlyList<string> BackboltTotalItems { get; } = new[] { "4", "6", "8", "10", "12" };

    public int BackboltTotalIndex
    {
        get
        {
            int i = BackboltPlateEngine.BackboltTotalCountOptions.ToList().IndexOf(Session.BackboltTotalCount);
            return i < 0 ? 0 : i;
        }
        set
        {
            var arr = BackboltPlateEngine.BackboltTotalCountOptions;
            Session.BackboltTotalCount = arr[Math.Max(0, Math.Min(arr.Count - 1, value))];
            OnPropertyChanged();
            Invalidate("背栓总数已改");
        }
    }

    // ---------------- 挂件参数 ----------------

    public IReadOnlyList<string> HangerMaterials { get; } = BackboltPlateEngine.HangMaterialOptions;

    public int HangerMaterialIndex
    {
        get
        {
            int i = BackboltPlateEngine.HangMaterialOptions.ToList().IndexOf(Session.BackboltHangerMaterial);
            return i < 0 ? 7 : i;
        }
        set
        {
            var arr = BackboltPlateEngine.HangMaterialOptions;
            Session.BackboltHangerMaterial = arr[Math.Max(0, Math.Min(arr.Count - 1, value))];
            OnPropertyChanged();
            Invalidate("挂件材质已改");
        }
    }

    public string HangerWidthText
    {
        get => Session.BackboltHangerWidthMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.BackboltHangerWidthMm = v; Invalidate("挂件宽度已改"); OnPropertyChanged(); } }
    }

    public string HangerThicknessText
    {
        get => Session.BackboltHangerThicknessMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.BackboltHangerThicknessMm = v; Invalidate("挂件厚度已改"); OnPropertyChanged(); } }
    }

    public string EccentricityText
    {
        get => Session.BackboltHangerEccentricityMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.BackboltHangerEccentricityMm = v; Invalidate("参数e已改"); OnPropertyChanged(); } }
    }

    public string HangerLengthText
    {
        get => Session.BackboltHangerLengthMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.BackboltHangerLengthMm = v; Invalidate("参数L已改"); OnPropertyChanged(); } }
    }

    public IReadOnlyList<string> HorizontalHangerItems { get; } = new[] { "1", "2", "3", "4", "5", "6" };

    public int HorizontalHangerIndex
    {
        get => Math.Max(0, Math.Min(5, Session.BackboltHorizontalHangerCount - 1));
        set { Session.BackboltHorizontalHangerCount = Math.Max(1, Math.Min(6, value + 1)); OnPropertyChanged(); Invalidate("水平挂件数已改"); }
    }

    public IReadOnlyList<string> LoadHangerItems { get; } = new[] { "1", "2", "3", "4", "5", "6" };

    public int LoadHangerIndex
    {
        get => Math.Max(0, Math.Min(5, Session.BackboltLoadHangerCount - 1));
        set { Session.BackboltLoadHangerCount = Math.Max(1, Math.Min(6, value + 1)); OnPropertyChanged(); Invalidate("承重挂件数已改"); }
    }

    // ---------------- 螺栓 ----------------

    public IReadOnlyList<string> BoltMaterials { get; } = BackboltPlateEngine.BoltMaterialOptions;

    public int BoltMaterialIndex
    {
        get
        {
            int i = BackboltPlateEngine.BoltMaterialOptions.ToList().IndexOf(Session.BackboltBoltMaterial);
            return i < 0 ? 0 : i;
        }
        set
        {
            var arr = BackboltPlateEngine.BoltMaterialOptions;
            Session.BackboltBoltMaterial = arr[Math.Max(0, Math.Min(arr.Count - 1, value))];
            OnPropertyChanged();
            Invalidate("螺栓材质已改");
        }
    }

    public IReadOnlyList<string> BoltDiameterItems { get; } = new[] { "5", "6", "7", "8", "10" };

    public int BoltDiameterIndex
    {
        get
        {
            int i = BackboltPlateEngine.BoltDiameterOptions.ToList().IndexOf(Session.BackboltBoltDiameterMm);
            return i < 0 ? 3 : i;
        }
        set
        {
            var arr = BackboltPlateEngine.BoltDiameterOptions;
            Session.BackboltBoltDiameterMm = arr[Math.Max(0, Math.Min(arr.Count - 1, value))];
            OnPropertyChanged();
            Invalidate("螺栓直径已改");
        }
    }

    public IReadOnlyList<string> BoltPerHangerItems { get; } = new[] { "1", "2", "3", "4" };

    public int BoltPerHangerIndex
    {
        get => Math.Max(0, Math.Min(3, Session.BackboltBoltPerHanger - 1));
        set { Session.BackboltBoltPerHanger = Math.Max(1, Math.Min(4, value + 1)); OnPropertyChanged(); Invalidate("每挂件螺栓数已改"); }
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

    public bool PreviewEnabled => Session.PanelWindReady && Session.BackboltShortSideMm > 0 && Session.BackboltLongSideMm > 0;

    public string PreviewBlockedReason => !Session.PanelWindReady
        ? CalcSession.UpstreamNotReadyText
        : Session.BackboltShortSideMm <= 0 || Session.BackboltLongSideMm <= 0
            ? "面板尺寸无效，请检查短边/长边"
            : "";

    public string WindNote => Session.PanelWindSource;

    public string InputSummary =>
        $"{Session.BackboltSpecies} {Session.BackboltLongSideMm:0.###}×{Session.BackboltShortSideMm:0.###}mm"
        + $"｜t={Session.BackboltThicknessMm:0.###}mm｜f={Session.BackboltAllowableBendingMpa:0.###}MPa"
        + $"｜wk={Session.PanelWindKpa:0.###}kPa"
        + $"｜{Session.BackboltHangerMaterial}｜M{Session.BackboltBoltDiameterMm:0}";

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

    /// <summary>实机分隔符：40 星 / 40 短横（ASCII -）/ 20 全角破折号（— U+2014）。</summary>
    private const string Sep40 = "****************************************";
    private const string Dash40 = "----------------------------------------";
    private const string Dash20 = "————————————————————";

    private static readonly IBrush ColorNormal = Brushes.Black;
    private static readonly IBrush ColorRule = new SolidColorBrush(Color.Parse("#4B5563"));
    private static readonly IBrush ColorOk = new SolidColorBrush(Color.Parse("#0000C0"));
    private static readonly IBrush ColorFail = new SolidColorBrush(Color.Parse("#C00000"));

    [RelayCommand]
    private void Preview()
    {
        if (!PreviewEnabled) { StatusIsWarning = true; StatusText = PreviewBlockedReason; return; }

        var it = new BackboltPlateEngine.Input
        {
            Species = Session.BackboltSpecies,
            ThicknessMm = Session.BackboltThicknessMm,
            CalcThicknessMm = Session.BackboltCalcThicknessMm,
            ShortSideMm = Session.BackboltShortSideMm,
            LongSideMm = Session.BackboltLongSideMm,
            SmallSupportMm = Session.BackboltSmallSupportMm,
            LargeSupportMm = Session.BackboltLargeSupportMm,
            DrillDepthMm = Session.BackboltDrillDepthMm,
            HoleDiameterMm = Session.BackboltHoleDiameterMm,
            AllowableBendingMpa = Session.BackboltAllowableBendingMpa,
            AllowableShearMpa = Session.BackboltAllowableShearMpa,
            BackboltTensionCapacityN = Session.BackboltTensionCapacityN,
            BackboltTotalCount = Session.BackboltTotalCount,
            HangerWidthMm = Session.BackboltHangerWidthMm,
            HangerThicknessMm = Session.BackboltHangerThicknessMm,
            HangerEccentricityMm = Session.BackboltHangerEccentricityMm,
            HangerLengthMm = Session.BackboltHangerLengthMm,
            HangerMaterial = Session.BackboltHangerMaterial,
            HorizontalHangerCount = Session.BackboltHorizontalHangerCount,
            LoadHangerCount = Session.BackboltLoadHangerCount,
            BoltPerHanger = Session.BackboltBoltPerHanger,
            BoltMaterial = Session.BackboltBoltMaterial,
            BoltDiameterMm = Session.BackboltBoltDiameterMm,
            AlphaMax = Session.AlphaMax,
            Wk = Session.PanelWindNmm2,             // ★ N/mm²，不经 kPa 反除
        };

        Results.Clear();
        try
        {
            var r = BackboltPlateEngine.Compute(it);
            Session.LastBackboltResult = r;

            Add(Sep40, ColorRule);
            Add($"面板部分风荷载标准值为:{r.WindStandardKpa:0.##}KPa", ColorNormal);
            Add(Sep40, ColorRule);

            Add($"{Session.BackboltSpecies}抗弯强度计算值:{r.StressComputed:0.###}MPa", ColorNormal);
            Add($"选用的{Session.BackboltSpecies}抗弯强度设计值:{r.StressAllowable:0.###}MPa", ColorNormal);
            Judge(r.StressOk);
            Add(Sep40, ColorRule);

            Add($"背栓在面板槽口中剪应力计算值:{r.SlotShearComputed:0.###}MPa", ColorNormal);
            Add($"面板的抗剪承载力设计值:{r.SlotShearAllowable:0.###}MPa", ColorNormal);
            Judge(r.SlotShearOk);
            Add(Sep40, ColorRule);

            Add($"背栓抗拉计算值:{r.BackboltTensionComputed:0.###}N", ColorNormal);
            Add($"选用背栓的抗拉承载力设计值:{r.BackboltTensionAllowable:0.###}N", ColorNormal);
            Judge(r.BackboltTensionOk);
            Add(Dash40, ColorRule);

            Add($"背栓抗剪计算值:{r.BackboltShearN:0.###}N", ColorNormal);
            Add($"选用背栓的抗剪承载力设计值:{r.BackboltShearAllowable:0.###}N", ColorNormal);
            Judge(r.BackboltShearOk);
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
            Session.LastBackboltSummary = CalcSession.SummaryOf(Results);
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(PlaceholderText));
        }

        StatusIsWarning = !Session.PanelWindResolved;
        StatusText = $"预演完成：{Session.BackboltSpecies} {Session.BackboltLongSideMm:0.###}×{Session.BackboltShortSideMm:0.###}mm"
                   + $" t={Session.BackboltThicknessMm:0.###}mm"
                   + $"｜{Session.BackboltHangerMaterial}｜M{Session.BackboltBoltDiameterMm:0}";
    }

    [RelayCommand] private void Prev() => _wizard.GoBeamCalc();
    /// <summary>背栓板块屏「下一步」：进入连接计算屏。</summary>
    [RelayCommand] private void Next() => _wizard.StepFromGlassNext();
    [RelayCommand] private void Exit() => _wizard.CloseForm();

    private void Judge(bool ok) => Add(ok ? "满足要求(√)" : "不满足要求(×)", ok ? ColorOk : ColorFail);
    private void Add(string text, IBrush color) => Results.Add(new PreviewLine(text, color));
}
