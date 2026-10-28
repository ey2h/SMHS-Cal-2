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
/// 石材线「板块计算」屏 ViewModel（V31 客户区 936×448，控件事见
/// <c>local-data/recon/v31runs/chain_stone_slot.txt</c> 第 4 屏）。
///
/// <para><b>连接方式只有「两侧连接」已闭合</b>：「四边连接」分支算式未定位
/// （four_side 扰动数据作废），引擎对四边连接抛 <see cref="NotSupportedException"/>；
/// 界面保留该单选但预演时给出明确提示。</para>
///
/// <para>算式与四面对拍见 <c>docs/V31_STONE_PLATE_UI.md</c>。</para>
/// </summary>
public partial class StonePlateCalcViewModel : ObservableObject
{
    public CalcSession Session { get; }
    private readonly CalcWizardViewModel _wizard;

    public StonePlateCalcViewModel(CalcSession session, CalcWizardViewModel wizard)
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

    // ---------------- 石板参数 ----------------

    public string ThicknessText
    {
        get => Session.StoneThicknessMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.StoneThicknessMm = v; Invalidate("石板厚度已改"); OnPropertyChanged(); } }
    }

    public string LongSideText
    {
        get => Session.StoneLongSideMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.StoneLongSideMm = v; Invalidate("长边尺寸已改"); OnPropertyChanged(); } }
    }

    public string ShortSideText
    {
        get => Session.StoneShortSideMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.StoneShortSideMm = v; Invalidate("短边尺寸已改"); OnPropertyChanged(); } }
    }

    public string BendingAllowableText
    {
        get => Session.StoneAllowableBendingMpa.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.StoneAllowableBendingMpa = v; Invalidate("抗弯强度设计值已改"); OnPropertyChanged(); } }
    }

    public string ShearAllowableText
    {
        get => Session.StoneAllowableShearMpa.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.StoneAllowableShearMpa = v; Invalidate("抗剪强度设计值已改"); OnPropertyChanged(); } }
    }

    // ---------------- 连接方式 ----------------

    public bool TwoSide
    {
        get => Session.StoneModeIsTwoSide;
        set { if (value) { Session.StoneModeIsTwoSide = true; OnPropertyChanged(); OnPropertyChanged(nameof(FourSide)); Invalidate("连接方式已改"); } }
    }

    public bool FourSide
    {
        get => !Session.StoneModeIsTwoSide;
        set { if (value) { Session.StoneModeIsTwoSide = false; OnPropertyChanged(); OnPropertyChanged(nameof(TwoSide)); Invalidate("连接方式已改"); } }
    }

    // ---------------- 托板参数 ----------------

    public string EdgeDistanceText
    {
        get => Session.StoneEdgeDistanceMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.StoneEdgeDistanceMm = v; Invalidate("短槽中心到板块边距已改"); OnPropertyChanged(); } }
    }

    public string SlotMouthText
    {
        get => Session.StoneSlotMouthMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.StoneSlotMouthMm = v; Invalidate("槽口宽度已改"); OnPropertyChanged(); } }
    }

    public string SlotBottomText
    {
        get => Session.StoneSlotBottomLengthMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.StoneSlotBottomLengthMm = v; Invalidate("槽底长度已改"); OnPropertyChanged(); } }
    }

    public IReadOnlyList<string> LugCountItems { get; } = new[] { "4", "6", "8", "10", "12" };

    public int LugCountIndex
    {
        get
        {
            int i = StonePlateEngine.LugCountOptions.ToList().IndexOf(Session.StoneLugCount);
            return i < 0 ? 0 : i;
        }
        set
        {
            var arr = StonePlateEngine.LugCountOptions;
            Session.StoneLugCount = arr[Math.Max(0, Math.Min(arr.Count - 1, value))];
            OnPropertyChanged();
            Invalidate("托板个数已改");
        }
    }

    public string LugAllowableText
    {
        get => Session.StoneLugAllowableShearMpa.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.StoneLugAllowableShearMpa = v; Invalidate("托板抗剪强度已改"); OnPropertyChanged(); } }
    }

    // ---------------- 挂件参数 ----------------

    public IReadOnlyList<string> HangerMaterials { get; } = StonePlateEngine.HangMaterialOptions;

    public int HangerMaterialIndex
    {
        get
        {
            int i = StonePlateEngine.HangMaterialOptions.ToList().IndexOf(Session.StoneHangerMaterial);
            return i < 0 ? 7 : i;
        }
        set
        {
            var arr = StonePlateEngine.HangMaterialOptions;
            Session.StoneHangerMaterial = arr[Math.Max(0, Math.Min(arr.Count - 1, value))];
            OnPropertyChanged();
            Invalidate("挂件材质已改");
        }
    }

    public string HangerWidthText
    {
        get => Session.StoneHangerWidthMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.StoneHangerWidthMm = v; Invalidate("挂件宽度已改"); OnPropertyChanged(); } }
    }

    public string HangerThicknessText
    {
        get => Session.StoneHangerThicknessMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.StoneHangerThicknessMm = v; Invalidate("挂件厚度已改"); OnPropertyChanged(); } }
    }

    public string EccentricityText
    {
        get => Session.StoneHangerEccentricityMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.StoneHangerEccentricityMm = v; Invalidate("参数s已改"); OnPropertyChanged(); } }
    }

    public string HangerLengthText
    {
        get => Session.StoneHangerLengthMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.StoneHangerLengthMm = v; Invalidate("参数L已改"); OnPropertyChanged(); } }
    }

    public IReadOnlyList<string> HorizontalHangerItems { get; } = new[] { "1", "2", "3", "4", "5", "6" };

    public int HorizontalHangerIndex
    {
        get => Math.Max(0, Math.Min(5, Session.StoneHorizontalHangerCount - 1));
        set { Session.StoneHorizontalHangerCount = Math.Max(1, Math.Min(6, value + 1)); OnPropertyChanged(); Invalidate("水平挂件数已改"); }
    }

    public IReadOnlyList<string> LoadHangerItems { get; } = new[] { "1", "2", "3", "4", "5", "6" };

    public int LoadHangerIndex
    {
        get => Math.Max(0, Math.Min(5, Session.StoneLoadHangerCount - 1));
        set { Session.StoneLoadHangerCount = Math.Max(1, Math.Min(6, value + 1)); OnPropertyChanged(); Invalidate("承重挂件数已改"); }
    }

    // ---------------- 螺栓 ----------------

    public IReadOnlyList<string> BoltMaterials { get; } = StonePlateEngine.BoltMaterialOptions;

    public int BoltMaterialIndex
    {
        get
        {
            int i = StonePlateEngine.BoltMaterialOptions.ToList().IndexOf(Session.StoneBoltMaterial);
            return i < 0 ? 0 : i;
        }
        set
        {
            var arr = StonePlateEngine.BoltMaterialOptions;
            Session.StoneBoltMaterial = arr[Math.Max(0, Math.Min(arr.Count - 1, value))];
            OnPropertyChanged();
            Invalidate("螺栓材质已改");
        }
    }

    public IReadOnlyList<string> BoltDiameterItems { get; } = new[] { "5", "6", "7", "8", "10" };

    public int BoltDiameterIndex
    {
        get
        {
            int i = StonePlateEngine.BoltDiameterOptions.ToList().IndexOf(Session.StoneBoltDiameterMm);
            return i < 0 ? 3 : i;
        }
        set
        {
            var arr = StonePlateEngine.BoltDiameterOptions;
            Session.StoneBoltDiameterMm = arr[Math.Max(0, Math.Min(arr.Count - 1, value))];
            OnPropertyChanged();
            Invalidate("螺栓直径已改");
        }
    }

    public IReadOnlyList<string> BoltPerHangerItems { get; } = new[] { "1", "2", "3", "4" };

    public int BoltPerHangerIndex
    {
        get => Math.Max(0, Math.Min(3, Session.StoneBoltPerHanger - 1));
        set { Session.StoneBoltPerHanger = Math.Max(1, Math.Min(4, value + 1)); OnPropertyChanged(); Invalidate("每挂件螺栓数已改"); }
    }

    private static bool TryNum(string? s, out double v)
        => double.TryParse((s ?? "").Trim(), System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out v) && v > 0;

    // ---------------- 结果 ----------------

    public ObservableCollection<PreviewLine> Results { get; } = new();
    public bool HasResults => Results.Count > 0;
    public string PlaceholderText => HasResults ? "" : "点「预演」按当前石板尺寸与托板/挂件/螺栓参数计算。";

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _statusIsWarning;

    /// <summary>石材线用**面板口径** wk（与玻璃同源），门禁同 PanelWindReady。</summary>
    public bool PreviewEnabled => Session.PanelWindReady && Session.StoneShortSideMm > 0 && Session.StoneLongSideMm > 0;

    public string PreviewBlockedReason => !Session.PanelWindReady
        ? CalcSession.UpstreamNotReadyText
        : Session.StoneShortSideMm <= 0 || Session.StoneLongSideMm <= 0
            ? "石板尺寸无效，请检查长边/短边"
            : "";

    public string WindNote => Session.PanelWindSource;

    public string InputSummary =>
        $"石板 {Session.StoneLongSideMm:0.###}×{Session.StoneShortSideMm:0.###}mm"
        + $"｜t={Session.StoneThicknessMm:0.###}mm｜[σ]={Session.StoneAllowableBendingMpa:0.###}MPa"
        + $"｜wk={Session.PanelWindKpa:0.###}kPa｜αmax={Session.AlphaMax:0.###}"
        + $"｜{Session.StoneHangerMaterial}｜M{Session.StoneBoltDiameterMm:0}";

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

    /// <summary>实机分隔符：43 星 / 40 星 / 40 短横。</summary>
    private const string Sep43 = "*******************************************";
    private const string Sep40 = "****************************************";
    private const string Dash40 = "------------------------------------------";

    private static readonly IBrush ColorNormal = Brushes.Black;
    private static readonly IBrush ColorRule = new SolidColorBrush(Color.Parse("#4B5563"));
    private static readonly IBrush ColorOk = new SolidColorBrush(Color.Parse("#0000C0"));
    private static readonly IBrush ColorFail = new SolidColorBrush(Color.Parse("#C00000"));

    [RelayCommand]
    private void Preview()
    {
        if (!PreviewEnabled) { StatusIsWarning = true; StatusText = PreviewBlockedReason; return; }

        if (!Session.StoneModeIsTwoSide)
        {
            StatusIsWarning = true;
            StatusText = "石材板块「四边连接」分支算式尚未定位（运行期无证据），暂不预演；请改用「两侧连接」。";
            return;
        }

        var it = new StonePlateEngine.Input
        {
            LongSideMm = Session.StoneLongSideMm,
            ShortSideMm = Session.StoneShortSideMm,
            ThicknessMm = Session.StoneThicknessMm,
            EdgeDistanceMm = Session.StoneEdgeDistanceMm,
            SlotMouthMm = Session.StoneSlotMouthMm,
            SlotBottomLengthMm = Session.StoneSlotBottomLengthMm,
            LugCount = Session.StoneLugCount,
            LugAllowableShearMpa = Session.StoneLugAllowableShearMpa,
            HangerWidthMm = Session.StoneHangerWidthMm,
            HangerThicknessMm = Session.StoneHangerThicknessMm,
            HangerEccentricityMm = Session.StoneHangerEccentricityMm,
            HangerLengthMm = Session.StoneHangerLengthMm,
            HangerMaterial = Session.StoneHangerMaterial,
            HorizontalHangerCount = Session.StoneHorizontalHangerCount,
            LoadHangerCount = Session.StoneLoadHangerCount,
            BoltPerHanger = Session.StoneBoltPerHanger,
            BoltMaterial = Session.StoneBoltMaterial,
            BoltDiameterMm = Session.StoneBoltDiameterMm,
            StoneDensityKnM3 = 30,
            AlphaMax = Session.AlphaMax,
            Wk = Session.PanelWindNmm2,             // ★ N/mm²，不经 kPa 反除
            StoneAllowableBendingMpa = Session.StoneAllowableBendingMpa,
            StoneAllowableShearMpa = Session.StoneAllowableShearMpa,
            Mode = StonePlateEngine.ConnectMode.TwoSide,
        };

        Results.Clear();
        try
        {
            var r = StonePlateEngine.Compute(it);
            Session.LastStoneResult = r;

            Add(Sep43, ColorRule);
            Add($"面板部分风荷载标准值为:{r.WindStandardKpa:0.##}KPa", ColorNormal);
            Add(Sep43, ColorRule);

            Add($"石材计算短边:{r.CalcShortMm:0.###}mm", ColorNormal);
            Add($"石材计算长边:{r.CalcLongMm:0.###}mm", ColorNormal);

            Add($"石材强度计算值:{r.StressComputed:0.###}MPa", ColorNormal);
            Add($"选用的石材抗弯强度设计值:{r.StressAllowable:0.###}MPa", ColorNormal);
            Judge(r.StressOk);
            Add(Dash40, ColorRule);

            Add($"石材抗剪强度计算值:{r.ShearComputed:0.###}MPa", ColorNormal);
            Add($"选用的石材抗剪强度设计值:{r.ShearAllowable:0.###}MPa", ColorNormal);
            Judge(r.ShearOk);
            Add(Sep43, ColorRule);

            Add($"托板剪应力计算值:{r.LugShearComputed:0.###}MPa", ColorNormal);
            Add($"选用托板的抗剪强度设计值:{r.LugShearAllowable:0.###}MPa", ColorNormal);
            Judge(r.LugShearOk);
            Add(Sep43, ColorRule);

            Add($"挂件的抗弯强度计算值:{r.HangerStressComputed:0.###}MPa", ColorNormal);
            Add($"所选挂件的抗弯强度设计值:{r.HangerStressAllowable:0.###}MPa", ColorNormal);
            Judge(r.HangerStressOk);
            Add(Sep43, ColorRule);

            Add($"挂件的挠度计算值:{r.HangerDeflectionComputed:0.###}mm", ColorNormal);
            Add($"所选挂件的挠度设计值:{r.HangerDeflectionAllowable:0.###}mm", ColorNormal);
            Judge(r.HangerDeflectionOk);
            Add(Sep43, ColorRule);

            Add($"螺栓的抗拉承受力计算值:{r.BoltTensionComputed:0.###}N", ColorNormal);
            Add($"所选螺栓的抗拉承受力设计值:{r.BoltTensionAllowable:0.###}N", ColorNormal);
            Judge(r.BoltTensionOk);
            Add(Dash40, ColorRule);

            Add($"螺栓的抗剪承受力计算值:{r.BoltShearComputed:0.###}N", ColorNormal);
            Add($"所选螺栓的抗剪承受力设计值:{r.BoltShearAllowable:0.###}N", ColorNormal);
            Judge(r.BoltShearOk);
            Add(Dash40, ColorRule);

            Add($"螺栓的拉剪复合承载力计算值:{r.BoltCombined:0.######}", ColorNormal);
            Add("所选螺栓的拉剪复合承载力计算值不得大于1", ColorNormal);
            Judge(r.BoltCombinedOk);
            Add(Sep43, ColorRule);
        }
        finally
        {
            Session.LastStoneSummary = CalcSession.SummaryOf(Results);
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(PlaceholderText));
        }

        StatusIsWarning = !Session.PanelWindResolved;
        StatusText = $"预演完成：{Session.StoneLongSideMm:0.###}×{Session.StoneShortSideMm:0.###}mm"
                   + $" t={Session.StoneThicknessMm:0.###}mm"
                   + $"｜{Session.StoneHangerMaterial}｜M{Session.StoneBoltDiameterMm:0}";
    }

    [RelayCommand] private void Prev() => _wizard.GoBeamCalc();
    /// <summary>石材板块屏「下一步」：进入连接计算屏（V31 石材线第 5 屏）。</summary>
    [RelayCommand] private void Next() => _wizard.StepFromGlassNext();
    [RelayCommand] private void Exit() => _wizard.CloseForm();

    private void Judge(bool ok) => Add(ok ? "满足要求(√)" : "不满足要求(×)", ok ? ColorOk : ColorFail);
    private void Add(string text, IBrush color) => Results.Add(new PreviewLine(text, color));
}
