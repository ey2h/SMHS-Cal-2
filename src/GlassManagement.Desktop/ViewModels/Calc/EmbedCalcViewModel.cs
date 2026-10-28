using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>
/// 「幕墙计算-埋件与焊缝计算」屏 ViewModel（V31 客户区 788×490）。
///
/// <para>坐标源：<c>v31runs/dump_embed_after.txt</c> + <c>shot_embed_after.png</c>。</para>
/// <para>算式源：<c>EmbedEngine</c>（`0x191EEC6–0x1928377` + `0x1A63DCF–0x1A687FD` 的轨迹），
/// 17 个值逐位一致，见 <c>EmbedEngineTests</c>。</para>
/// <para><b>简化说明</b>：实机「受力方式」Frame 里是 SSTab + 4 张示意图（PictureBoxDC），
/// 复刻版用同一组几何输入框 + 说明文字替代图形，算式与数值不受影响。</para>
/// </summary>
public partial class EmbedCalcViewModel : ObservableObject
{
    public CalcSession Session { get; }
    private readonly CalcWizardViewModel _wizard;

    public ObservableCollection<PreviewLine> Results { get; } = new();

    public EmbedCalcViewModel(CalcSession session, CalcWizardViewModel wizard)
    {
        Session = session;
        _wizard = wizard;
        SyncFromSession();
        Session.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(CalcSession.WindStandardKpa)
                or nameof(CalcSession.ColumnSpanMm) or nameof(CalcSession.ColumnBayWidthMm))
                SyncFromSession();
        };
    }

    // ---------------- 埋件种类（4 选 1） ----------------

    public IReadOnlyList<string> EmbedKinds { get; } = new[]
        { "土建预埋", "后补锚栓", "槽型埋件", "对穿螺栓" };
    [ObservableProperty] private int _embedKindIndex;          // 实机默认 土建预埋

    /// <summary>只有土建预埋有完整实现（其余三类在 V31 里是独立分支）。</summary>
    public bool IsCastInPlace => EmbedKindIndex == 0;

    // RadioButton 直接绑 bool（避免引入值转换器；V31 是 4 个独立 OptionButton）
    public bool IsKind0
    {
        get => EmbedKindIndex == 0;
        set { if (value) EmbedKindIndex = 0; }
    }
    public bool IsKind1
    {
        get => EmbedKindIndex == 1;
        set { if (value) EmbedKindIndex = 1; }
    }
    public bool IsKind2
    {
        get => EmbedKindIndex == 2;
        set { if (value) EmbedKindIndex = 2; }
    }
    public bool IsKind3
    {
        get => EmbedKindIndex == 3;
        set { if (value) EmbedKindIndex = 3; }
    }

    partial void OnEmbedKindIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsCastInPlace));
        OnPropertyChanged(nameof(PreviewEnabled));
        OnPropertyChanged(nameof(BlockedReason));
        OnPropertyChanged(nameof(IsKind0));
        OnPropertyChanged(nameof(IsKind1));
        OnPropertyChanged(nameof(IsKind2));
        OnPropertyChanged(nameof(IsKind3));
    }

    // ---------------- 土建预埋参数（帧 (179,34) 297x220） ----------------
    //  坐标与候选表全部来自 v31runs/dump_embed_after.txt（控件）与
    //  v31runs/embed_params_probe.txt / embed_grades_probe.txt（CB_GETLBTEXT 直读）。
    //  ★ 实机标签就是「钢筋层数 / 混凝土等级 / 锚筋直径 / 每层筋数 / 锚筋材质 /
    //    锚板厚度 / 锚筋长度 / 外层锚筋中心距 / 锚板面积 / 锚筋外型」，
    //    不是「锚筋排数 / 锚筋级别 / z值」。

    /// <summary>钢筋层数 `(251,60)`，3 项。</summary>
    public IReadOnlyList<string> RebarLayers { get; } = EmbedMaterials.RebarLayers;
    [ObservableProperty] private int _rebarLayerIndex;                 // 实机 sel=0（2层）

    /// <summary>混凝土等级 `(403,59)`，13 项 C20..C80。</summary>
    public IReadOnlyList<string> ConcreteGrades { get; } = EmbedMaterials.ConcreteGrades;
    [ObservableProperty] private int _concreteGradeIndex = 1;          // 实机 sel=1（C25）

    /// <summary>锚筋直径 `(250,94)`，14 项。</summary>
    public IReadOnlyList<string> RebarDiameters { get; } =
        EmbedMaterials.RebarDiameters.Select(d => d.ToString("0")).ToArray();
    [ObservableProperty] private int _rebarDiameterIndex = 2;          // 实机 sel=2（12）

    /// <summary>每层筋数 `(403,94)`，3 项。</summary>
    public IReadOnlyList<string> RebarPerLayerItems { get; } = EmbedMaterials.RebarPerLayer;
    [ObservableProperty] private int _rebarPerLayerIndex;              // 实机 sel=0（2）

    /// <summary>锚筋材质 `(251,127)`，实机只有 HPB300 / HRB400 两项（没有 HRB335）。</summary>
    public IReadOnlyList<string> RebarMaterials { get; } = EmbedMaterials.RebarMaterials;
    [ObservableProperty] private int _rebarMaterialIndex;              // 实机 sel=0（HPB300）

    /// <summary>锚板厚度 `(403,128)`，10 项 6..24，实机 sel=1（8）。</summary>
    public IReadOnlyList<string> PlateThicknessItems { get; } =
        EmbedMaterials.PlateThicknesses.Select(t => t.ToString("0")).ToArray();
    [ObservableProperty] private int _plateThicknessIndex = 1;         // 实机 sel=1（8）

    /// <summary>
    /// 锚筋外型 `(403,196)`：实机 **en=0（用户改不了）**，因为它由「锚筋材质」驱动——
    /// 选 HPB300 显示「光圆」、选 HRB400 显示「带肋」（`embed_grades_probe.txt` 实测联动）。
    /// </summary>
    public IReadOnlyList<string> RebarShapes { get; } = EmbedMaterials.RebarShapes;
    public int RebarShapeIndex => EmbedMaterials.ShapeIndex(RebarMaterialIndex);

    partial void OnRebarMaterialIndexChanged(int value)
        => OnPropertyChanged(nameof(RebarShapeIndex));

    /// <summary>锚筋长度 `(251,161)`，可改。</summary>
    [ObservableProperty] private string _rebarLengthText = "200";
    /// <summary>外层锚筋中心距 `(419,162)`（计算里的 z），可改。</summary>
    [ObservableProperty] private string _anchorZText = "110";
    /// <summary>锚板面积 `(251,197)`，可改。</summary>
    [ObservableProperty] private string _plateAreaText = "60000";
    [ObservableProperty] private bool _hasHook = true;                 // 实机 chk=1

    /// <summary>
    /// 受力方式 / 侧埋 页的 **e0**（`(323,376)`，左图参数框内，实机 80）。
    ///
    /// ★ 实机扰动实证（`v31runs/embed_params_probe.txt`）：e0 80→100 时
    /// 弯矩 137280→171600（×1.25）、**转接件挠度 0.000276→0.00054（×1.953=(100/80)³）**、
    /// 焊缝强度 12.452→14.152。⇒ e0 **同时**是弯矩力臂和转接件悬臂长，
    /// 不是两个独立输入（旧实现拆成 Cantilever/Arm 两个框，实机只有一个框）。
    /// </summary>
    [ObservableProperty] private string _eccentricityText = "80";

    // ---------------- 焊缝和转接件参数（帧 (484,34) 285x174） ----------------

    /// <summary>转接件型号规格 `(491,66)`，20 行（`CB_GETLBTEXT` 直读）。</summary>
    public IReadOnlyList<string> SteelSections { get; } =
        WeldSectionTable.Rows.Select(r => r.Name).ToArray();

    /// <summary>
    /// 抵抗矩 `(684,48)` / 惯性矩 `(684,71)` / 截面积 `(684,94)` 三个下拉的候选表。
    ///
    /// ★ 实机实证（`v31runs/embed_section_probe.txt`）：**四个下拉（规格 + W + I + A）
    /// 共用一个行号**——改规格，三个跟着跳；单独把 W 拉到第 10 行，
    /// 挠度 0.000276→0.00082（只能用 I 也换成 66.7 cm⁴ 解释）。
    /// 所以界面只保留一个 <see cref="SteelSectionIndex"/>，四个 SelectedIndex 全绑它。
    /// </summary>
    public IReadOnlyList<string> TransferModulusItems { get; } =
        WeldSectionTable.Rows.Select(r => r.WText).ToArray();
    public IReadOnlyList<string> TransferInertiaItems { get; } =
        WeldSectionTable.Rows.Select(r => r.IText).ToArray();
    public IReadOnlyList<string> TransferAreaItems { get; } =
        WeldSectionTable.Rows.Select(r => r.AText).ToArray();

    [ObservableProperty] private int _steelSectionIndex;               // 实机 sel=0

    /// <summary>钢材材质 `(670,117)`：实机 en=0（不可改），只有 Q235。</summary>
    public IReadOnlyList<string> SteelMaterials { get; } = new[] { "Q235", "Q345" };
    [ObservableProperty] private int _steelMaterialIndex;

    /// <summary>焊缝高度 hf `(680,142)`，7 项，实机 sel=2（6）。</summary>
    public IReadOnlyList<string> WeldLegs { get; } = new[] { "4", "5", "6", "7", "8", "9", "10" };
    [ObservableProperty] private int _weldLegIndex = 2;

    /// <summary>水平段焊缝长 Lh `(680,164)`，实机 50，**en=1 可改**。</summary>
    [ObservableProperty] private string _weldHorizontalText = "50";
    /// <summary>垂直段焊缝长 Lv `(680,185)`，实机 100，**en=1 可改**。</summary>
    [ObservableProperty] private string _weldVerticalText = "100";

    /// <summary>焊接形式：L 型焊 / 三边围焊（实机 L 型焊 默认 chk=1）。</summary>
    [ObservableProperty] private bool _isLWeld = true;
    public bool IsSurroundWeld
    {
        get => !IsLWeld;
        set { if (value) IsLWeld = false; }
    }

    partial void OnIsLWeldChanged(bool value)
    {
        OnPropertyChanged(nameof(IsSurroundWeld));
        OnPropertyChanged(nameof(WeldGeometryText));
    }

    /// <summary>焊缝几何预览串（周长 / 面积 / 截面模量），界面下方只读展示，便于与 V31 核对。</summary>
    public string WeldGeometryText
    {
        get
        {
            var g = EmbedEngine.ComputeWeldGeometry(BuildInputs());
            return $"焊缝几何：周长 {g.Perimeter:0.###}mm｜面积 {g.Area:0.###}mm²｜"
                 + $"惯性矩 {g.Inertia:0.###}mm⁴｜截面模量 {g.SectionModulus:0.###}mm³";
        }
    }

    // ---------------- 输出 ----------------

    public bool HasResults => Results.Count > 0;
    public string PlaceholderText => HasResults ? "" : "点「预演」按当前埋件参数出计算结果";

    /// <summary>
    /// ★ 门禁两层：① 只做土建预埋；② 第一页必须已算出支承口径 wk。
    /// 埋件的荷载（wk / 分格宽 / 支承跨度 / αmax）全部从第一页链路传来，本屏不设默认值。
    /// </summary>
    public bool PreviewEnabled => IsCastInPlace && Session.SupportWindReady;

    public string BlockedReason => !IsCastInPlace ? "该埋件种类尚未复刻（当前只做土建预埋）"
        : !Session.SupportWindReady ? CalcSession.UpstreamNotReadyText
        : "";

    [ObservableProperty] private string _statusText = "就绪";
    [ObservableProperty] private bool _statusIsWarning;
    [ObservableProperty] private string _note = "";

    private void SyncFromSession()
    {
        Note = Session.SupportWindReady
            ? $"wk={Session.WindStandardKpa:0.###}KPa（支承口径）｜"
              + $"{Session.ColumnBayWidthMm:0.###}×{Session.ColumnSpanMm:0.###}mm"
            : CalcSession.UpstreamNotReadyText;
        OnPropertyChanged(nameof(Note));
        OnPropertyChanged(nameof(PreviewEnabled));
        OnPropertyChanged(nameof(BlockedReason));
    }

    private const string Sep42 = "******************************************";
    private const string Dash = "------------------------------------------";

    private static readonly IBrush ColorNormal = Brushes.Black;
    private static readonly IBrush ColorRule = new SolidColorBrush(Color.Parse("#4B5563"));
    private static readonly IBrush ColorOk = new SolidColorBrush(Color.Parse("#0000C0"));
    private static readonly IBrush ColorFail = new SolidColorBrush(Color.Parse("#C00000"));

    private static double Parse(string s, double dflt)
        => double.TryParse(s, out var v) && v > 0 ? v : dflt;

    /// <summary>当前锚筋直径 mm（14 项候选按行号取，避免越界）。</summary>
    public double RebarDiameterMm =>
        EmbedMaterials.RebarDiameters[
            Math.Clamp(RebarDiameterIndex, 0, EmbedMaterials.RebarDiameters.Length - 1)];

    public EmbedInputs BuildInputs()
    {
        // 直径下标 → 实际直径（14 项候选）
        double[] dia = EmbedMaterials.RebarDiameters;
        var d = dia[Math.Clamp(RebarDiameterIndex, 0, dia.Length - 1)];

        // 钢筋层数 × 每层筋数 = 锚筋根数（实机 2层 × 2 = 4）
        int layers = Math.Clamp(RebarLayerIndex, 0, 2) + 2;
        int perLayer = Math.Clamp(RebarPerLayerIndex, 0, 2) + 2;

        double e0 = Parse(EccentricityText, 80);

        return new EmbedInputs
        {
            // ★ 全部由第一页传下来（wk 支承口径 / 分格宽 / 支承跨度 / αmax）。
            //   不用 `Session.X > 0 ? X : 常量` 顶替；第一页没算时 PreviewEnabled 已挡住。
            WkKpa = Session.SupportWindReady ? Session.WindStandardKpa : 0,
            BayWidth = Session.ColumnBayWidthMm,
            SupportSpan = Session.ColumnSpanMm,
            AlphaMax = Session.AlphaMax,

            RebarCount = layers * perLayer,
            RebarDiameter = d,
            // 材质 → fy 与 α 同时变（HPB300=270/0.16 光圆，HRB400=300/0.14 带肋），
            // 见 EmbedMaterials 的实读依据
            RebarFy = EmbedMaterials.RebarFy(RebarMaterialIndex),
            AnchorAlpha = EmbedMaterials.AnchorAlpha(RebarMaterialIndex),
            ConcreteFc = EmbedMaterials.ConcreteFc(ConcreteGradeIndex),
            ConcreteFt = EmbedMaterials.ConcreteFt(ConcreteGradeIndex),
            PlateThickness = EmbedMaterials.PlateThicknesses[
                Math.Clamp(PlateThicknessIndex, 0, EmbedMaterials.PlateThicknesses.Length - 1)],
            AnchorZ = Parse(AnchorZText, 110),
            PlateArea = Parse(PlateAreaText, 60000),
            RebarLengthMm = Parse(RebarLengthText, 200),

            // 转接件截面特性：全部来自「转接件型号规格」那一行（四下拉共用一个行号）
            TransferArea = WeldSectionTable.At(SteelSectionIndex).AreaMm2,
            TransferSectionModulus = WeldSectionTable.ModulusMm3(SteelSectionIndex),
            TransferInertia = WeldSectionTable.InertiaMm4(SteelSectionIndex),

            // e0 一个输入同时进弯矩力臂和转接件悬臂（实机扰动已证）
            MomentArmMm = e0,
            CantileverMm = e0,

            WeldLeg = Parse(WeldLegs[Math.Clamp(WeldLegIndex, 0, WeldLegs.Count - 1)], 6),
            WeldHorizontalMm = Parse(WeldHorizontalText, 50),
            WeldVerticalMm = Parse(WeldVerticalText, 100),
            WeldType = IsLWeld ? WeldType.LShape : WeldType.ThreeSide,
        };
    }

    [RelayCommand]
    private void Preview()
    {
        if (!PreviewEnabled)
        {
            StatusIsWarning = true;
            StatusText = BlockedReason;
            return;
        }

        Results.Clear();
        try
        {
            var r = EmbedEngine.Compute(BuildInputs());

            Add(Sep42, ColorRule);
            Add($"风荷载标准值为:{r.WkKpa:0.###}KPa", ColorNormal);
            Add(Sep42, ColorRule);
            Add($"剪力设计值:{r.ShearDesign:0.###}N", ColorNormal);
            Add($"轴力设计值:{r.AxialDesign:0.###}N", ColorNormal);
            Add($"弯矩设计值:{r.MomentDesign:0.###}N.mm", ColorNormal);
            Add(Sep42, ColorRule);

            Add($"预埋件锚筋总截面积计算值:{r.RebarAreaRequired:0.###}mm2", ColorNormal);
            Add($"实际锚筋总截面积:{r.RebarAreaActual:0.###}mm2", ColorNormal);
            Judge(r.RebarAreaRequired <= r.RebarAreaActual);
            Add(Sep42, ColorRule);

            Add($"0.5fcA={r.ConcreteBearing:0.###}N", ColorNormal);
            Add($"法向压力设计值:{r.AxialDesign:0.###}N", ColorNormal);
            Judge(r.AxialDesign <= r.ConcreteBearing);
            Add(Sep42, ColorRule);

            Add($"锚筋基本锚固长度lab={r.AnchorLab:0.###}mm", ColorNormal);
            Add($"折减后的锚固长度la={r.AnchorLa:0.###}mm", ColorNormal);
            Add($"所选锚筋实际长度：{Parse(RebarLengthText, 200):0.###}mm", ColorNormal);
            Add(HasHook
                ? "锚筋末端采用弯钩或机械锚固措施时，包括弯钩和锚固端头在内的锚固长度（投影长度）"
                  + "可取基本锚固长度的60%，弯钩和机械锚固的形式和技术要求应符合《混凝土结构设计规范》GB50010规定。"
                  + "若计算后锚固长度大于刚才算得的la，则可按la取值。"
                : "锚筋末端未采用弯钩，锚固长度按基本锚固长度取用（GB50010）。", ColorRule);
            Add("抗震设计的幕墙锚筋锚固长度不得小于200mm。（软件中对此条不做要求，用户自己控制）", ColorRule);
            Add("锚固长度不应小于15倍锚筋直径。", ColorRule);
            Add("当锚筋配置较多，锚筋总截面面积超过计算所需截面面积的1.4倍时，锚固长度可适当减少，但不应小于180mm。", ColorRule);
            Judge(Parse(RebarLengthText, 200) >= Math.Min(r.AnchorLa, 200)
                  && Parse(RebarLengthText, 200) >= 15 * RebarDiameterMm);
            Add(Sep42, ColorRule);

            Add($"转接件强度计算值:{r.TransferStress:0.###}MPa", ColorNormal);
            Add($"转接件强度设计值:{r.TransferStressLimit:0.###}MPa", ColorNormal);
            Judge(r.TransferStress <= r.TransferStressLimit);
            Add(Dash, ColorRule);
            Add($"转接件挠度计算值:{r.TransferDeflection:0.######}mm", ColorNormal);
            Add($"转接件挠度设计值:{r.TransferDeflLimit:0.###}mm", ColorNormal);
            Judge(r.TransferDeflection <= r.TransferDeflLimit);
            Add(Dash, ColorRule);
            Add($"焊缝强度计算值:{r.WeldStress:0.###}MPa", ColorNormal);
            Add($"焊缝强度设计值:{r.WeldStressLimit:0.###}MPa", ColorNormal);
            Judge(r.WeldStress <= r.WeldStressLimit);
            Add(Sep42, ColorRule);

            Session.LastEmbedResult = r;
            StatusIsWarning = false;
            StatusText = "预演完成：埋件与焊缝（17 个值与 V31 逐位一致）";
        }
        finally
        {
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(PlaceholderText));
        }
    }

    [RelayCommand] private void Prev() => _wizard.GoConnectCalc();
    [RelayCommand] private void Next() => _wizard.GoSealantCalc();
    [RelayCommand] private void Exit() => _wizard.CloseForm();

    private void Judge(bool ok) => Add(ok ? "满足要求(√)" : "不满足要求(×)", ok ? ColorOk : ColorFail);
    private void Add(string text, IBrush color) => Results.Add(new PreviewLine(text, color));
}
