using System;
using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassManagement.Calc.Book;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>
/// 「幕墙计算-胶类、伸缩及紧固计算」屏 ViewModel（V31 客户区 784×537）。
///
/// <para>坐标源：<c>v31runs/dump_sealant_after.txt</c> + <c>shot_sealant_after.png</c>。</para>
/// <para>算式源：<c>SealantEngine</c>（`0xC51197–0xC60814` + `0xC735A1–0xC78B67` 轨迹），
/// 11 个值逐位一致，见 <c>SealantEngineTests</c>。</para>
/// <para><b>★ 本屏 wk 用面板口径</b>（实机 1.37 kPa），不是支承口径 1.285。</para>
/// </summary>
public partial class SealantCalcViewModel : ObservableObject
{
    public CalcSession Session { get; }
    private readonly CalcWizardViewModel _wizard;

    public ObservableCollection<PreviewLine> Results { get; } = new();

    public SealantCalcViewModel(CalcSession session, CalcWizardViewModel wizard)
    {
        Session = session;
        _wizard = wizard;
        SyncFromSession();
        Session.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(CalcSession.PanelWindKpa)
                or nameof(CalcSession.BeamUpperGridHeightMm) or nameof(CalcSession.ColumnBayWidthMm))
                SyncFromSession();
        };
    }

    // ---------------- 公用参数 ----------------

    public IReadOnlyList<string> FrameTypes { get; } = new[] { "框架", "半隐框", "明框" };
    [ObservableProperty] private int _frameTypeIndex;
    [ObservableProperty] private string _panelSizeText = "20";
    [ObservableProperty] private bool _calcSubFrame;         // 计算副框和承托件
    [ObservableProperty] private bool _skipClamp;            // 不计算压板(耳板)
    [ObservableProperty] private bool _openStyle;            // 开放式

    // ---------------- 全隐框参数 ----------------

    public IReadOnlyList<string> CodeOptions { get; } = new[] { "按JGJ102规范", "按JGJ133规范" };
    [ObservableProperty] private int _codeIndex;

    // ── 结构胶参数：候选表全部是 V31 实机 CB_GETLBTEXT 直读，不是编的 ──
    //    取证：local-data/recon/sealant_act_probe.py → v31runs/sealant_act_probe.txt
    //    (153,178) 06..12 sel=0   (153,200) 06..30 sel=2
    //    (153,222) 06/09/10/12/15 sel=0 en=0   (153,244) 06..30 sel=2 en=0
    //    (153,266) 0.1/0.12/0.125/0.14/0.15 sel=0
    //    (163,288) 0.01 单值      (163,310) 0.2 单值
    public IReadOnlyList<string> StructThicknessOptions { get; } = Mm(6, 12);
    public IReadOnlyList<string> StructWidthOptions { get; } = Mm(6, 30);
    public IReadOnlyList<string> InsulationThicknessOptions { get; } = new[] { "06", "09", "10", "12", "15" };
    public IReadOnlyList<string> InsulationWidthOptions { get; } = Mm(6, 30);
    public IReadOnlyList<string> StructMovementOptions { get; } = new[] { "0.1", "0.12", "0.125", "0.14", "0.15" };
    public IReadOnlyList<string> LongTermStrengthOptions { get; } = new[] { "0.01" };
    public IReadOnlyList<string> ShortTermStrengthOptions { get; } = new[] { "0.2" };

    [ObservableProperty] private int _structThicknessIndex;      // 0 → 06
    [ObservableProperty] private int _structWidthIndex = 2;      // 2 → 08
    [ObservableProperty] private int _insulationThicknessIndex;  // 0 → 06（en=0 只读）
    [ObservableProperty] private int _insulationWidthIndex = 2;  // 2 → 08（en=0 只读）
    [ObservableProperty] private int _structMovementIndex;       // 0 → 0.1
    [ObservableProperty] private int _longTermStrengthIndex;     // 0 → 0.01
    [ObservableProperty] private int _shortTermStrengthIndex;    // 0 → 0.2

    /// <summary>"06"…"30" 形式的两位补零毫米档位（V31 下拉原文就是这个格式）。</summary>
    private static string[] Mm(int from, int to)
    {
        var a = new string[to - from + 1];
        for (var i = from; i <= to; i++) a[i - from] = i.ToString("00");
        return a;
    }

    private static double Pick(IReadOnlyList<string> opts, int idx, double dflt)
        => idx >= 0 && idx < opts.Count && double.TryParse(opts[idx], out var v) && v > 0 ? v : dflt;

    /// <summary>实际结构胶宽度：V31 (153,200) 选值（"08" → 8）。</summary>
    public double ActualWidthMm => Pick(StructWidthOptions, StructWidthIndex, 8);

    /// <summary>实际结构胶厚度：V31 (153,178) 选值（"06" → 6）。</summary>
    public double ActualThicknessMm => Pick(StructThicknessOptions, StructThicknessIndex, 6);

    /// <summary>结构胶位移能力：V31 (153,266) 选值（默认 0.1）。</summary>
    public double StructMovement => Pick(StructMovementOptions, StructMovementIndex, 0.1);

    [ObservableProperty] private string _weatherMovementText = "0.25";
    [ObservableProperty] private string _weatherActualText = "16";

    /// <summary>板块间对接方式：玻璃对接 / 铝框对接（实机 玻璃对接 选中）。</summary>
    [ObservableProperty] private bool _glassButt = true;

    // ---------------- 压板参数 ----------------

    [ObservableProperty] private string _clampSpacingText = "50";
    [ObservableProperty] private string _clampArmText = "45";
    [ObservableProperty] private string _clampWidthText = "6";
    [ObservableProperty] private string _clampPitchText = "350";
    public IReadOnlyList<string> ClampMaterials { get; } = new[] { "6063-T5", "6063-T6", "Q235" };
    [ObservableProperty] private int _clampMaterialIndex;

    [ObservableProperty] private string _jointActualText = "20";

    // ---------------- 输出 ----------------

    public bool HasResults => Results.Count > 0;
    public string PlaceholderText => HasResults ? "" : "点「预演」按当前胶类参数出计算结果";

    /// <summary>
    /// ★ 门禁：胶类用**面板口径** wk（μsl=1.0 不折减），由第一页计算链写入。
    /// 第一页没算过就不许预演 —— 不再用 1.37 这类常量顶替。
    /// </summary>
    public bool PreviewEnabled => Session.PanelWindReady;

    [ObservableProperty] private string _statusText = "就绪";
    [ObservableProperty] private bool _statusIsWarning;
    [ObservableProperty] private string _note = "";

    private void SyncFromSession()
    {
        Note = Session.PanelWindReady
            ? $"wk={Session.PanelWindKpa:0.###}KPa（面板口径）｜{Session.ColumnBayWidthMm:0.###}mm"
            : CalcSession.UpstreamNotReadyText;
        OnPropertyChanged(nameof(Note));
        OnPropertyChanged(nameof(PreviewEnabled));
    }

    private const string Sep45 = "*********************************************";
    private const string Dash = "—————————————————————";

    private static readonly IBrush ColorNormal = Brushes.Black;
    private static readonly IBrush ColorRule = new SolidColorBrush(Color.Parse("#4B5563"));
    private static readonly IBrush ColorOk = new SolidColorBrush(Color.Parse("#0000C0"));
    private static readonly IBrush ColorFail = new SolidColorBrush(Color.Parse("#C00000"));

    private static double Parse(string s, double dflt)
        => double.TryParse(s, out var v) && v > 0 ? v : dflt;

    public SealantInputs BuildInputs()
    {
        // 面板口径：由第一页计算链写入 PanelWindKpa；没算过就是 0（PreviewEnabled 已挡住）。
        double wkPanel = Session.PanelWindReady ? Session.PanelWindKpa : 0;

        return new SealantInputs
        {
            WkPanelKpa = wkPanel,
            BayWidth = Session.ColumnBayWidthMm,
            StoryHeight = Session.BeamUpperGridHeightMm,
            WeatherMovement = Parse(WeatherMovementText, 0.25),
            ClampArm = Parse(ClampArmText, 45),
            ClampWidth = Parse(ClampWidthText, 6),
            ClampSpacing = Parse(ClampPitchText, 350),
            // 结构胶三件：全部来自界面下拉（V31 实测：它们既喂算式的分母，也进计算书表格）
            StructMovement = StructMovement,
            ActualWidth = ActualWidthMm,
            ActualThickness = ActualThicknessMm,
        };
    }

    [RelayCommand]
    private void Preview()
    {
        Results.Clear();
        try
        {
            var r = SealantEngine.Compute(BuildInputs());
            double actW = r.ActualWidth;          // 来自 (153,200) 下拉，不再另存一份
            double actT = r.ActualThickness;      // 来自 (153,178) 下拉
            double actWeather = Parse(WeatherActualText, 16);
            double actJoint = Parse(JointActualText, 20);

            Add(Sep45, ColorRule);
            Add($"面板部分风荷载标准值为:{r.WkPanelKpa:0.###}KPa", ColorNormal);
            Add(Sep45, ColorRule);

            Add($"按计算，玻璃与铝框间结构胶宽度最小值不小于:{r.StructuralWidth:0.###}mm", ColorNormal);
            Add($"按计算，玻璃与铝框间结构胶厚度最小值不小于:{r.StructuralThickness:0.###}mm", ColorNormal);
            Add($"玻璃与铝框间实际结构胶宽度取:{actW:0.###}mm", ColorNormal);
            Add($"玻璃与铝框间实际结构胶厚度取:{actT:0.###}mm", ColorNormal);
            Add(Dash, ColorRule);

            Add("按5.6.1[JGJ102-2003]规定，硅酮结构胶还需要满足下面要求：", ColorRule);
            Add("1:粘接宽度≥7mm;", ColorRule);
            Add("2:12mm≥粘接厚度≥6mm;", ColorRule);
            Add("3:粘接宽度大于厚度,但不宜大于厚度的2倍;", ColorRule);
            Add("综合上面计算结果,本工程设计中:", ColorRule);
            bool csOk = actW >= 7 && actT >= 6 && actT <= 12
                        && actW > actT && actW <= 2 * actT
                        && r.StructuralWidth <= actW && r.StructuralThickness <= actT;
            Add($"   玻璃与铝框间结构胶{(csOk ? "满足要求(√)" : "不满足要求(×)")}",
                csOk ? ColorOk : ColorFail);
            Add(Sep45, ColorRule);

            Add($"硅酮耐候胶胶缝计算宽度为:{r.WeatherWidth:0.###}mm", ColorNormal);
            Add($"实际胶缝为:{actWeather:0.###}mm", ColorNormal);
            Judge(r.WeatherWidth <= actWeather);
            Add(Sep45, ColorRule);

            Add($"立柱伸缩缝计算值为:{r.JointWidth:0.###}mm", ColorNormal);
            Add($"实际缝为:{actJoint:0.###}mm", ColorNormal);
            Judge(r.JointWidth <= actJoint);
            Add(Sep45, ColorRule);

            if (SkipClamp)
            {
                Add("（已勾选「不计算压板(耳板)」，压板与连接螺栓段跳过）", ColorRule);
            }
            else
            {
                Add($"压板最大弯曲应力计算值为:{r.ClampBending:0.###}MPa", ColorNormal);
                Add($"选用材料的抗弯强度设计值为:{r.ClampBendingLimit:0.###}MPa", ColorNormal);
                Judge(r.ClampBending <= r.ClampBendingLimit);
                Add(Dash, ColorRule);
                Add($"压板最大剪应力计算值为:{r.ClampShear:0.###}MPa", ColorNormal);
                Add($"选用材料的抗剪强度设计值为:{r.ClampShearLimit:0.###}MPa", ColorNormal);
                Judge(r.ClampShear <= r.ClampShearLimit);
                Add(Dash, ColorRule);
                Add($"连接螺栓抗拉承载力设计值为:{r.BoltCapacity:0.###}N", ColorNormal);
                Add($"连接螺栓实际受力为:{r.BoltForce:0.###}N", ColorNormal);
                Judge(r.BoltForce <= r.BoltCapacity);
            }
            Add(Sep45, ColorRule);

            Session.LastSealantResult = r;
            StatusIsWarning = false;
            StatusText = "预演完成：胶类、伸缩及紧固（11 个值与 V31 逐位一致）";
        }
        finally
        {
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(PlaceholderText));
        }
    }

    [RelayCommand] private void Prev() => _wizard.GoEmbedCalc();
    /// <summary>胶类屏「下一步」= 进入「生成计算书」面板（V31 的最后一屏）。</summary>
    [RelayCommand]
    private void Next() => _wizard.StepFromSealantNext();
    [RelayCommand] private void Exit() => _wizard.CloseForm();

    private void Judge(bool ok) => Add(ok ? "满足要求(√)" : "不满足要求(×)", ok ? ColorOk : ColorFail);
    private void Add(string text, IBrush color) => Results.Add(new PreviewLine(text, color));
}
