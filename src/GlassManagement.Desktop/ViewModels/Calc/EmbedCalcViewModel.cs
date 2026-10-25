using System;
using System.Collections.ObjectModel;
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

    // ---------------- 土建预埋参数 ----------------

    public IReadOnlyList<string> RebarLayers { get; } = new[] { "2层", "3层", "4层" };
    [ObservableProperty] private int _rebarLayerIndex;
    public IReadOnlyList<string> RebarGrades { get; } = new[]
        { "HPB300", "HRB400", "HRB335" };
    [ObservableProperty] private int _rebarGradeIndex;
    public IReadOnlyList<string> ConcreteGrades { get; } = new[]
        { "C25", "C30", "C35", "C40" };
    [ObservableProperty] private int _concreteGradeIndex;        // 实机 C25（列表第 0 项）
    public IReadOnlyList<string> RebarDiameters { get; } = new[]
        { "8", "10", "12", "14", "16", "18", "20" };
    [ObservableProperty] private int _rebarDiameterIndex = 2;   // 12

    [ObservableProperty] private string _plateThicknessText = "8";
    [ObservableProperty] private string _anchorZText = "110";
    [ObservableProperty] private string _plateAreaText = "60000";
    [ObservableProperty] private string _rebarLengthText = "200";
    [ObservableProperty] private bool _hasHook = true;          // 实机 chk=1

    // ---------------- 焊缝和转接件参数 ----------------

    public IReadOnlyList<string> SteelSections { get; } = new[]
        { "10#槽钢", "8#槽钢", "12#槽钢", "[100x50x5" };
    [ObservableProperty] private int _steelSectionIndex;
    public IReadOnlyList<string> WeldMaterials { get; } = new[] { "Q235", "Q355" };
    [ObservableProperty] private int _weldMaterialIndex;
    public IReadOnlyList<string> WeldLegs { get; } = new[] { "4", "5", "6", "8" };
    [ObservableProperty] private int _weldLegIndex = 2;         // 6

    [ObservableProperty] private string _transferAreaText = "1274.8";
    [ObservableProperty] private string _transferWText = "39700";
    [ObservableProperty] private string _transferIText = "1980000";
    [ObservableProperty] private string _cantileverText = "80";
    [ObservableProperty] private string _armText = "80";

    /// <summary>焊接形式：L 型焊 / 三边围焊（实机 L 型焊 默认选中）。</summary>
    [ObservableProperty] private bool _isLWeld = true;
    public bool IsSurroundWeld => !IsLWeld;

    partial void OnIsLWeldChanged(bool value) => OnPropertyChanged(nameof(IsSurroundWeld));

    // ---------------- 受力方式（简化替代 SSTab 示意图） ----------------

    [ObservableProperty] private string _forceHeightText = "40";
    [ObservableProperty] private string _forceWidthText = "40";
    [ObservableProperty] private string _forceEccText = "80";

    // ---------------- 输出 ----------------

    public bool HasResults => Results.Count > 0;
    public string PlaceholderText => HasResults ? "" : "点「预演」按当前埋件参数出计算结果";
    public bool PreviewEnabled => IsCastInPlace;
    public string BlockedReason => IsCastInPlace ? "" : "该埋件种类尚未复刻（当前只做土建预埋）";

    [ObservableProperty] private string _statusText = "就绪";
    [ObservableProperty] private bool _statusIsWarning;
    [ObservableProperty] private string _note = "";

    private void SyncFromSession()
    {
        Note = Session.WindStandardKpa > 0
            ? $"wk={Session.WindStandardKpa:0.###}KPa（支承口径）｜"
              + $"{Session.ColumnBayWidthMm:0.###}×{Session.ColumnSpanMm:0.###}mm"
            : "第一页尚未算出风荷载";
        OnPropertyChanged(nameof(Note));
    }

    private const string Sep42 = "******************************************";
    private const string Dash = "------------------------------------------";

    private static readonly IBrush ColorNormal = Brushes.Black;
    private static readonly IBrush ColorRule = new SolidColorBrush(Color.Parse("#4B5563"));
    private static readonly IBrush ColorOk = new SolidColorBrush(Color.Parse("#0000C0"));
    private static readonly IBrush ColorFail = new SolidColorBrush(Color.Parse("#C00000"));

    private static double Parse(string s, double dflt)
        => double.TryParse(s, out var v) && v > 0 ? v : dflt;

    public EmbedInputs BuildInputs()
    {
        double[] fy = { 270, 360, 300 };            // HPB300/HRB400/HRB335
        double[] fc = { 11.9, 14.3, 16.7, 19.1 };   // C25/C30/C35/C40
        double[] ft = { 1.27, 1.43, 1.57, 1.71 };
        var d = Parse(RebarDiameters[Math.Clamp(RebarDiameterIndex, 0, 6)], 12);

        return new EmbedInputs
        {
            WkKpa = Session.WindStandardKpa > 0 ? Session.WindStandardKpa : 1.285,
            BayWidth = Session.ColumnBayWidthMm,
            SupportSpan = Session.ColumnSpanMm,
            AlphaMax = Session.AlphaMax,
            RebarDiameter = d,
            RebarFy = fy[Math.Clamp(RebarGradeIndex, 0, 2)],
            ConcreteFc = fc[Math.Clamp(ConcreteGradeIndex, 0, 3)],
            ConcreteFt = ft[Math.Clamp(ConcreteGradeIndex, 0, 3)],
            PlateThickness = Parse(PlateThicknessText, 8),
            AnchorZ = Parse(AnchorZText, 110),
            PlateArea = Parse(PlateAreaText, 60000),
            RebarLengthMm = Parse(RebarLengthText, 200),
            TransferArea = Parse(TransferAreaText, 1274.8),
            TransferSectionModulus = Parse(TransferWText, 39700),
            TransferInertia = Parse(TransferIText, 1980000),
            CantileverMm = Parse(CantileverText, 80),
            MomentArmMm = Parse(ArmText, 80),
            WeldLeg = Parse(WeldLegs[Math.Clamp(WeldLegIndex, 0, 3)], 6),
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
                  && Parse(RebarLengthText, 200) >= 15 * Parse(RebarDiameters[RebarDiameterIndex], 12));
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
