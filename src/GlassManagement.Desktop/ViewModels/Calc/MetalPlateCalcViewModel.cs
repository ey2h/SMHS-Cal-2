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
/// 板材线「板块计算」屏 ViewModel（V31 客户区 795×530，控件事见
/// <c>local-data/recon/v31runs/chain_metal6.txt</c> 第 4 屏）。
///
/// <para><b>两条输出分支（实机实测，不是推测）</b></para>
/// <list type="bullet">
/// <item>长边等分数 = 1，或选「不算肋」→ 只出 <c>A板</c> 强度/挠度 4 个值；</item>
/// <item>等分数 ≥ 2 且「计算肋」→ A 板消失，改出 <c>B板</c> 4 个值 + <c>加强肋</c> 4 个值；</item>
/// <item>选「算法二」→ 加强肋标签变 <c>B-B加强肋</c>（该支算式尚未闭合，见文档 §6）。</item>
/// </list>
///
/// <para>算式与四面对拍见 <c>docs/V31_PLATE_UI.md</c>。</para>
/// </summary>
public partial class MetalPlateCalcViewModel : ObservableObject
{
    public CalcSession Session { get; }
    private readonly CalcWizardViewModel _wizard;

    /// <summary>铝单板参数表一行（DataGrid 展示用）。</summary>
    public sealed class PlateRowVm
    {
        public string Model { get; init; } = "";
        public double ThicknessMm { get; init; }
        public double BendingMpa { get; init; }
        public double ShearMpa { get; init; }
        public string Note { get; init; } = "";
        public string ThicknessText => ThicknessMm.ToString("0.###");
    }

    public MetalPlateCalcViewModel(CalcSession session, CalcWizardViewModel wizard)
    {
        Session = session;
        _wizard = wizard;

        Profiles = new ObservableCollection<PlateRowVm>(
            ParamLibrary.AluminumPlates.Select(r => new PlateRowVm
            {
                Model = r.Model,
                ThicknessMm = r.ThicknessMm,
                BendingMpa = r.BendingMpa,
                ShearMpa = r.ShearMpa,
                Note = r.Note,
            }));

        // 实机默认选中「1060 H14、H24 / 2.5」（屏上 A板强度设计值 54MPa）
        int idx = 0;
        for (int i = 0; i < Profiles.Count; i++)
            if (Math.Abs(Profiles[i].ThicknessMm - Session.PlateThicknessMm) < 1e-9 &&
                Profiles[i].Model.StartsWith("1060", StringComparison.Ordinal))
            { idx = i; break; }
        _selectedProfile = Profiles.Count > 0 ? Profiles[idx] : null;
        SyncAllowablesFromRow();

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

    // ---------------- 铝单板参数表 ----------------

    public ObservableCollection<PlateRowVm> Profiles { get; }

    [ObservableProperty] private PlateRowVm? _selectedProfile;

    partial void OnSelectedProfileChanged(PlateRowVm? value)
    {
        SyncAllowablesFromRow();
        Invalidate("板厚/型号已切换");
        OnPropertyChanged(nameof(ThicknessText));
    }

    private void SyncAllowablesFromRow()
    {
        if (SelectedProfile is null) return;
        Session.PlateThicknessMm = SelectedProfile.ThicknessMm;
        Session.PlateAllowableStressMpa = SelectedProfile.BendingMpa;
        Session.PlateAllowableShearMpa = SelectedProfile.ShearMpa;
    }

    public string ThicknessText => Session.PlateThicknessMm.ToString("0.###");

    // ---------------- 尺寸与等分数 ----------------

    public string ShortSideText
    {
        get => Session.PlateShortSideMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.PlateShortSideMm = v; Invalidate("短边尺寸已改"); OnPropertyChanged(); } }
    }

    public string LongSideText
    {
        get => Session.PlateLongSideMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.PlateLongSideMm = v; Invalidate("长边尺寸已改"); OnPropertyChanged(); OnPropertyChanged(nameof(LxText)); } }
    }

    /// <summary>长边等分数下拉（实机 sel 0..3 → 1..4）。</summary>
    public IReadOnlyList<string> DivisionItems { get; } = new[] { "1", "2", "3", "4" };

    public int DivisionIndex
    {
        get => Math.Max(0, Math.Min(3, Session.PlateLongSideDivision - 1));
        set
        {
            Session.PlateLongSideDivision = Math.Max(1, Math.Min(4, value + 1));
            OnPropertyChanged();
            OnPropertyChanged(nameof(LxText));
            OnPropertyChanged(nameof(RibSectionVisible));
            Invalidate("长边等分数已改");
        }
    }

    /// <summary>Lx（B板/C板）= 长边/等分数 —— 实机两个 en=0 回显框，只读。</summary>
    public string LxText => Session.PlateLx.ToString("0.###");

    private static bool TryNum(string? s, out double v)
        => double.TryParse((s ?? "").Trim(), System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out v) && v > 0;

    // ---------------- 挠度限值 ----------------

    public bool DeflDiv60
    {
        get => Math.Abs(Session.PlateDeflectionDivisor - 60) < 1e-9;
        set { if (value) { Session.PlateDeflectionDivisor = 60; OnPropertyChanged(); Invalidate("挠度限值已改"); } }
    }

    public bool DeflDiv90
    {
        get => Math.Abs(Session.PlateDeflectionDivisor - 90) < 1e-9;
        set { if (value) { Session.PlateDeflectionDivisor = 90; OnPropertyChanged(); Invalidate("挠度限值已改"); } }
    }

    // ---------------- 加强肋 ----------------

    public bool UseRib
    {
        get => Session.PlateUseRib;
        set
        {
            Session.PlateUseRib = value;
            OnPropertyChanged(); OnPropertyChanged(nameof(NoRib));
            OnPropertyChanged(nameof(RibEnabled));
            Invalidate("肋选项已切换");
        }
    }

    public bool NoRib
    {
        get => !Session.PlateUseRib;
        set { if (value) UseRib = false; }
    }

    public bool AlgoOne
    {
        get => !Session.PlateRibAlgorithmTwo;
        set { if (value) { Session.PlateRibAlgorithmTwo = false; OnPropertyChanged(); OnPropertyChanged(nameof(AlgoTwo)); Invalidate("肋算法已改"); } }
    }

    public bool AlgoTwo
    {
        get => Session.PlateRibAlgorithmTwo;
        set { if (value) { Session.PlateRibAlgorithmTwo = true; OnPropertyChanged(); OnPropertyChanged(nameof(AlgoOne)); Invalidate("肋算法已改"); } }
    }

    /// <summary>等分数 &lt;2 时加强肋整块不参与（实机同样不出肋结果）。</summary>
    public bool RibEnabled => Session.PlateUseRib && Session.PlateLongSideDivision >= 2;
    public bool RibSectionVisible => true;

    /// <summary>肋挠度限值 Ly/250 · Ly/180 · Ly/120（Ly = 短边）。</summary>
    public bool RibDiv250
    {
        get => Math.Abs(Session.PlateRibDeflectionDivisor - 250) < 1e-9;
        set { if (value) { Session.PlateRibDeflectionDivisor = 250; OnPropertyChanged(); OnPropertyChanged(nameof(RibDiv180)); OnPropertyChanged(nameof(RibDiv120)); Invalidate("肋挠度限值已改"); } }
    }

    public bool RibDiv180
    {
        get => Math.Abs(Session.PlateRibDeflectionDivisor - 180) < 1e-9;
        set { if (value) { Session.PlateRibDeflectionDivisor = 180; OnPropertyChanged(); OnPropertyChanged(nameof(RibDiv250)); OnPropertyChanged(nameof(RibDiv120)); Invalidate("肋挠度限值已改"); } }
    }

    public bool RibDiv120
    {
        get => Math.Abs(Session.PlateRibDeflectionDivisor - 120) < 1e-9;
        set { if (value) { Session.PlateRibDeflectionDivisor = 120; OnPropertyChanged(); OnPropertyChanged(nameof(RibDiv250)); OnPropertyChanged(nameof(RibDiv180)); Invalidate("肋挠度限值已改"); } }
    }

    public string RibIText
    {
        get => Session.PlateRibInertiaMm4.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.PlateRibInertiaMm4 = v; Invalidate("肋惯性矩已改"); OnPropertyChanged(); } }
    }

    public string RibWText
    {
        get => Session.PlateRibSectionModulusMm3.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.PlateRibSectionModulusMm3 = v; Invalidate("肋抵抗矩已改"); OnPropertyChanged(); } }
    }

    /// <summary>肋材质 8 选（实机默认 6063-T5 → [σ]=90MPa）。</summary>
    public IReadOnlyList<string> RibMaterials { get; } = new[]
    { "6063-T5", "6063-T6", "6061-T4", "6061-T6", "6063A-T5", "6063A-T6", "Q235", "Q355" };

    public string RibMaterial
    {
        get => Session.PlateRibMaterial;
        set
        {
            Session.PlateRibMaterial = value;
            ApplyRibProps();
            OnPropertyChanged();
            OnPropertyChanged(nameof(RibMat6063T5)); OnPropertyChanged(nameof(RibMat6063T6));
            OnPropertyChanged(nameof(RibMat6061T4)); OnPropertyChanged(nameof(RibMat6061T6));
            OnPropertyChanged(nameof(RibMat6063AT5)); OnPropertyChanged(nameof(RibMat6063AT6));
            OnPropertyChanged(nameof(RibMatQ235)); OnPropertyChanged(nameof(RibMatQ355));
            Invalidate("肋材质已改");
        }
    }

    // 8 个单选按钮各自绑一个 bool（Avalonia 无 WPF 的 RadioButton 值转换器；
    // GroupName 分组后只有被选中的那一个回写 true）
    public bool RibMat6063T5 { get => RibMaterial == "6063-T5"; set { if (value) RibMaterial = "6063-T5"; } }
    public bool RibMat6063T6 { get => RibMaterial == "6063-T6"; set { if (value) RibMaterial = "6063-T6"; } }
    public bool RibMat6061T4 { get => RibMaterial == "6061-T4"; set { if (value) RibMaterial = "6061-T4"; } }
    public bool RibMat6061T6 { get => RibMaterial == "6061-T6"; set { if (value) RibMaterial = "6061-T6"; } }
    public bool RibMat6063AT5 { get => RibMaterial == "6063A-T5"; set { if (value) RibMaterial = "6063A-T5"; } }
    public bool RibMat6063AT6 { get => RibMaterial == "6063A-T6"; set { if (value) RibMaterial = "6063A-T6"; } }
    public bool RibMatQ235 { get => RibMaterial == "Q235"; set { if (value) RibMaterial = "Q235"; } }
    public bool RibMatQ355 { get => RibMaterial == "Q355"; set { if (value) RibMaterial = "Q355"; } }

    /// <summary>
    /// 「冷弯薄壁型钢」勾选（实机 (176,482) 115×14，ToolTip「注意区分冷弯型钢和热轧型钢的区别」）。
    /// <para><b>已实机实证</b>：只对钢材质生效 —— Q235 [σ] 215→205、Q355 315→310，
    /// 且塑性发展系数 γ 由 1.05 回到 1.00（肋应力 89.125 → 93.581）；对铝无影响。</para>
    /// </summary>
    public bool RibColdFormed
    {
        get => Session.PlateRibColdFormed;
        set
        {
            Session.PlateRibColdFormed = value;
            ApplyRibProps();
            OnPropertyChanged();
            Invalidate("肋材质附加选项已改");
        }
    }

    /// <summary>把当前材质（+冷弯薄壁）解析出的 [σ] 写回会话，供状态栏与计算书取用。</summary>
    private void ApplyRibProps()
    {
        var props = PlateEngine.RibProps(Session.PlateRibMaterial, Session.PlateRibColdFormed);
        Session.PlateRibAllowableStressMpa = props.AllowableStressMpa;
    }

    /// <summary>肋与面板接触面宽度 mm（实机默认 50；算法一未读取）。</summary>
    public string RibContactWidthText
    {
        get => Session.PlateRibContactWidthMm.ToString("0.###");
        set { if (TryNum(value, out var v)) { Session.PlateRibContactWidthMm = v; Invalidate("接触面宽度已改"); OnPropertyChanged(); } }
    }

    // ---------------- 结果 ----------------

    public ObservableCollection<PreviewLine> Results { get; } = new();
    public bool HasResults => Results.Count > 0;
    public string PlaceholderText => HasResults ? "" : "点「预演」按当前板厚与分格计算板强度与实际挠度。";

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _statusIsWarning;

    /// <summary>板材线用**面板口径** wk（与玻璃同源），门禁同 PanelWindReady。</summary>
    public bool PreviewEnabled => Session.PanelWindReady && Session.PlateShortSideMm > 0 && Session.PlateLongSideMm > 0;

    public string PreviewBlockedReason => !Session.PanelWindReady
        ? CalcSession.UpstreamNotReadyText
        : Session.PlateShortSideMm <= 0 || Session.PlateLongSideMm <= 0
            ? "板格尺寸无效，请检查短边/长边"
            : "";

    public string WindNote => Session.PanelWindSource;

    public string InputSummary =>
        $"分格 {Session.PlateShortSideMm:0.###}×{Session.PlateLongSideMm:0.###}mm"
        + $"｜{Session.PlateLongSideDivision} 等分（Lx={Session.PlateLx:0.###}）"
        + $"｜t={Session.PlateThicknessMm:0.###}mm｜[σ]={Session.PlateAllowableStressMpa:0.###}MPa"
        + $"｜限值 短边/{Session.PlateDeflectionDivisor:0.##}"
        + $"｜wk={Session.PanelWindKpa:0.###}kPa｜αmax={Session.AlphaMax:0.###}";

    private void Invalidate(string reason)
    {
        OnPropertyChanged(nameof(PreviewEnabled));
        OnPropertyChanged(nameof(PreviewBlockedReason));
        OnPropertyChanged(nameof(InputSummary));
        OnPropertyChanged(nameof(WindNote));
        OnPropertyChanged(nameof(RibEnabled));
        if (Results.Count == 0) return;
        Results.Clear();
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(PlaceholderText));
        StatusIsWarning = true;
        StatusText = reason + "，请重新预演";
    }

    // ---------------- 命令 ----------------

    /// <summary>实机分隔符：43 星 / 40 星，强度与挠度之间是 40 个短横。</summary>
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

        var it = new PlateEngine.Input
        {
            ShortSideMm = Session.PlateShortSideMm,
            LongSideMm = Session.PlateLongSideMm,
            LongSideDivision = Session.PlateLongSideDivision,
            ThicknessMm = Session.PlateThicknessMm,
            AllowableStressMpa = Session.PlateAllowableStressMpa,
            Wk = Session.PanelWindNmm2,             // ★ N/mm²，不经 kPa 反除（差 1 ulp 会让 qd 末位变）
            AlphaMax = Session.AlphaMax,
            ElasticModulusMpa = 70000,
            PoissonRatio = 0.3,
            DensityKnM3 = 27,
            UseRib = Session.PlateUseRib,
            RibAlgorithmTwo = Session.PlateRibAlgorithmTwo,
            DeflectionDivisor = Session.PlateDeflectionDivisor,
            RibDeflectionDivisor = Session.PlateRibDeflectionDivisor,
            RibInertiaMm4 = Session.PlateRibInertiaMm4,
            RibSectionModulusMm3 = Session.PlateRibSectionModulusMm3,
            RibAllowableStressMpa = Session.PlateRibAllowableStressMpa,
            RibMaterialCode = Session.PlateRibMaterial,
            RibColdFormed = Session.PlateRibColdFormed,
        };

        Results.Clear();
        try
        {
            var r = PlateEngine.Compute(it);
            Session.LastPlateResult = r;

            Add(Sep43, ColorRule);
            Add($"面板部分风荷载标准值为:{r.WindStandardKpa:0.###}KPa", ColorNormal);
            Add(Sep43, ColorRule);

            if (r.IsRibbedPanel)
            {
                Add($"B板强度计算值:{r.B_StressComputed:0.###}MPa", ColorNormal);
                Add($"B板强度设计值:{r.B_StressAllowable:0.###}MPa", ColorNormal);
                Judge(r.B_StressOk);
                Add(Dash40, ColorRule);

                Add($"B板挠度计算值:{r.B_DeflectionComputed:0.###}mm", ColorNormal);
                Add($"B板挠度设计值:{r.B_DeflectionAllowable:0.###}mm", ColorNormal);
                Judge(r.B_DeflectionOk);

                Add(Sep43, ColorRule);
                string rib = Session.PlateRibAlgorithmTwo ? "B-B加强肋" : "加强肋";
                Add($"{rib}强度计算值:{r.RibStressComputed:0.###}MPa", ColorNormal);
                Add($"{rib}强度设计值:{r.RibStressAllowable:0.###}MPa", ColorNormal);
                Judge(r.RibStressOk);

                Add(Sep40, ColorRule);
                Add($"{rib}挠度计算值:{r.RibDeflectionComputed:0.###}mm", ColorNormal);
                Add($"{rib}挠度设计值:{r.RibDeflectionAllowable:0.###}mm", ColorNormal);
                Judge(r.RibDeflectionOk);
                Add(Sep40, ColorRule);
            }
            else
            {
                Add($"A板强度计算值:{r.A_StressComputed:0.###}MPa", ColorNormal);
                Add($"A板强度设计值:{r.A_StressAllowable:0.###}MPa", ColorNormal);
                Judge(r.A_StressOk);

                Add(Sep43, ColorRule);
                Add($"A板挠度计算值:{r.A_DeflectionComputed:0.###}mm", ColorNormal);
                Add($"A板挠度设计值:{r.A_DeflectionAllowable:0.###}mm", ColorNormal);
                Judge(r.A_DeflectionOk);
                Add(Sep43, ColorRule);
            }
        }
        finally
        {
            Session.LastPlateSummary = CalcSession.SummaryOf(Results);
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(PlaceholderText));
        }

        StatusIsWarning = !Session.PanelWindResolved;
        StatusText = $"预演完成：{Session.PlateThicknessMm:0.###}mm {SelectedProfile?.Model}"
                   + $"｜qk={((PlateEngine.Result?)Session.LastPlateResult)?.Qk:0.######}"
                   + (Session.PlateRibAlgorithmTwo && Session.PlateLongSideDivision >= 2
                        ? "｜算法二分支尚未闭合，仅算法一逐位对拍" : "");
    }

    [RelayCommand] private void Prev() => _wizard.GoBeamCalc();
    /// <summary>板块屏「下一步」：进入连接计算屏（V31 板材线第 5 屏）。</summary>
    [RelayCommand] private void Next() => _wizard.StepFromGlassNext();
    [RelayCommand] private void Exit() => _wizard.CloseForm();

    private void Judge(bool ok) => Add(ok ? "满足要求(√)" : "不满足要求(×)", ok ? ColorOk : ColorFail);
    private void Add(string text, IBrush color) => Results.Add(new PreviewLine(text, color));
}
