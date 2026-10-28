using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>
/// 门窗系统计算屏 ViewModel（V31 CW29，client 916×534，五帧重叠当 Tab）。
/// 复刻 V31 全部选项控件（含运行时隐藏帧）——盘点取证 cw29_inventory.json：
///   帧1 门窗系统简图及玻璃参数：玻璃预演（wk 链下传）+ 杆件选择计算（计算左图竖杆/右图横杆）
///       + 竖梃/横梃参数 + 风荷载「不小于 Pa」+ 计算门窗杆件时挠度取值（3 模式 + 100/150 + 500）；
///   帧2 横梃受力形式选择（样式 1..21，默认样式1）；
///   帧3 竖向杆件参数：竖中梃参数（材质 10 项 + 断热/冷弯薄壁 + 截面 DataGrid）+ 内框参数（12 材质/木材）；
///   帧4 横向杆件参数：横中梃参数（材质 10 项）+ 钢框参数；
///   帧5 连接件参数选择：计算选择（计算/不计算连接件）+ 门窗安装形式（无/有钢附框）
///       + 连接片与边框连接形式（螺钉抽钉/卡槽）+ 整樘门窗洞口尺寸 + 连接片参数
///       （连接点数/连接片数/薄壁件厚/连接片厚/材质/抽芯铆钉类型+规格/射钉膨胀/结构侧钉径+片数）
///       + 钢附框参数（附框厚/类型/规格）。
/// 引擎：玻璃 wk = Session.PanelWindNmm2（round6 下传，V31 必须先「玻璃预演」初始化）；
///       杆件本体 DoorMullionEngine；连接件 DoorConnectorEngine。全部材质/组合逐位对拍见引擎测试。
/// </summary>
public partial class DoorWindowCalcViewModel : ObservableObject
{
    public CalcSession Session { get; }
    private readonly CalcWizardViewModel _wizard;

    public DoorWindowCalcViewModel(CalcSession session, CalcWizardViewModel wizard)
    {
        Session = session;
        _wizard = wizard;
        Session.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(CalcSession.PanelWindKpa) or nameof(CalcSession.AlphaMax))
                Invalidate("上游风荷载已变化");
        };
    }

    // ══ 帧1：杆件参数（可见主帧）══
    /// <summary>计算左图竖杆（默认勾选）。</summary>
    [ObservableProperty] private bool _calcLeftMullion = true;
    /// <summary>计算右图横杆（默认不勾）。</summary>
    [ObservableProperty] private bool _calcRightTransom;
    /// <summary>竖梃受荷跨度 L1 / 间距 L2 / 长度 Lt。</summary>
    [ObservableProperty] private double _mullionSpanMm = 600;
    [ObservableProperty] private double _mullionSpacingMm = 1500;
    [ObservableProperty] private double _mullionLengthMm = 1600;
    /// <summary>横梃受荷宽度 / 跨度两值。</summary>
    [ObservableProperty] private double _transomLoadWidthMm = 650;
    [ObservableProperty] private double _transomSpan1Mm = 1500;
    [ObservableProperty] private double _transomSpan2Mm = 1500;
    /// <summary>风荷载标准值方式：true=「不小于 Pa」人工下限（默认），false=以计算为准。</summary>
    [ObservableProperty] private bool _windUseManualMin = true;
    [ObservableProperty] private double _windManualMinPa = 1000;

    // V31 成对 RadioButton（同 ColumnCalcView 的 ModelXxx 模式，避免 !取反+TwoWay 绑定）
    public bool WindUseCalcOnly
    {
        get => !WindUseManualMin;
        set { WindUseManualMin = !value; if (value) Invalidate("风荷载方式已改"); }
    }
    public bool NoCalcConnector
    {
        get => !CalcConnector;
        set { CalcConnector = !value; if (value) Invalidate("计算选择已改"); }
    }
    public bool NoSteelFrame
    {
        get => !HasSteelFrame;
        set { HasSteelFrame = !value; if (value) Invalidate("安装形式已改"); }
    }
    public bool GrooveConnection
    {
        get => !ScrewConnection;
        set { ScrewConnection = !value; if (value) Invalidate("连接形式已改"); }
    }

    // ══ 挠度取值（3 模式）══
    /// <summary>0=自动（规范/标准自动 L/100），1=人工一致，2=人工不一致（probe2 逐位）。</summary>
    [ObservableProperty] private int _deflLimitModeIndex;
    /// <summary>人工分母下拉索引（0=100，1=150，V31 默认选 150）。</summary>
    [ObservableProperty] private int _deflLimitDivisorIndex = 1;
    /// <summary>人工分母（100/150 下拉）。</summary>
    public double DeflLimitDivisor => DeflLimitDivisorIndex == 0 ? 100 : 150;
    /// <summary>不一致模式垂直分母（500 下拉，仅单选项）。</summary>
    public double DeflVertDivisor { get; set; } = 500;

    public static IReadOnlyList<string> DeflLimitModes { get; } =
        new[] { "钢、铝按JGJ214和GB/T8478，塑钢按JGJ103自动取值",
                "人工给定限值（水平与自重方向要求一致）",
                "人工给定限值（水平与自重方向要求不一致）" };
    public static IReadOnlyList<string> DeflDivisorOptions { get; } =
        new[] { "100", "150" };

    // ══ 帧3：竖中梃材质/截面 ══
    [ObservableProperty] private string _mullionMaterial = "6063-T5";
    [ObservableProperty] private bool _mullionThermalBreak;      // 断热
    [ObservableProperty] private bool _mullionThinWall;           // 冷弯薄壁型钢
    /// <summary>立梃一（门窗用）50系列（参数库运行时数据 cm→mm）。</summary>
    [ObservableProperty] private double _mullionSectionW = 4013;
    [ObservableProperty] private double _mullionSectionI = 154440;
    [ObservableProperty] private double _mullionSectionS = 3299;
    [ObservableProperty] private double _mullionSectionTw = 3;

    // ══ 帧4：横中梃材质/截面 ══
    [ObservableProperty] private string _transomMaterial = "6063-T5";
    [ObservableProperty] private bool _transomThermalBreak;
    [ObservableProperty] private bool _transomThinWall;
    /// <summary>横梃一（门窗用）55系列（水平 Wy1/Iy/Sy/ty + 垂直 Wx2/Ix/Sx/tx）。</summary>
    [ObservableProperty] private double _transomSectionW = 4101;
    [ObservableProperty] private double _transomSectionI = 142350;
    [ObservableProperty] private double _transomSectionS = 4466;
    [ObservableProperty] private double _transomSectionTw = 3;
    [ObservableProperty] private double _transomVertW = 2765;
    [ObservableProperty] private double _transomVertI = 99690;
    [ObservableProperty] private double _transomVertS = 2962;
    [ObservableProperty] private double _transomVertTw = 3;

    public static IReadOnlyList<string> MaterialOptions { get; } =
        new[] { "6061-T4", "6061-T6", "6063-T5", "6063-T6", "6063A-T5", "6063A-T6",
                "6060-T66", "塑钢(仅塑材部分)", "Q235", "玻纤增强聚氨酯" };

    // ══ 帧5：连接件参数 ══
    [ObservableProperty] private bool _calcConnector = true;      // 计算连接件（默认）
    [ObservableProperty] private bool _hasSteelFrame;             // 有钢附框（默认无）
    [ObservableProperty] private bool _screwConnection = true;    // 螺钉/抽钉连接（默认）
    [ObservableProperty] private double _openingHeightMm = 2500;
    [ObservableProperty] private double _openingWidthMm = 2000;
    [ObservableProperty] private double _connectorCount = 24;
    [ObservableProperty] private double _connPlateThickness = 1.5;
    [ObservableProperty] private double _connWallThickness = 1.5;
    [ObservableProperty] private int _connWallMaterialIndex = 2;  // 6063-T5（11 项材质下拉）
    [ObservableProperty] private int _connFastenerTypeIndex;       // 0=抽芯铆钉(开口10/12级)
    [ObservableProperty] private int _connScrewDiameterIndex;      // 规格下拉索引（候选随类型联动）
    /// <summary>边框侧钉径（mm）= 当前类型候选表[索引]（V31 下拉联动）。</summary>
    public double ConnScrewDiameter =>
        DoorConnectorEngine.FastenerDiameters[Math.Clamp(ConnFastenerTypeIndex, 0, 4)][
            Math.Clamp(ConnScrewDiameterIndex, 0, 4)];
    [ObservableProperty] private double _connPlateCount = 1;      // 连接片数（边框侧除数）
    [ObservableProperty] private bool _connExpansionFix;          // true=射钉/膨胀螺钉
    [ObservableProperty] private int _connStructDiameterIndex;     // 结构侧钉径下拉索引（候选 4.0/4.2/4.5/5.2/5.5）
    public double ConnStructDiameter =>
        new[] { 4.0, 4.2, 4.5, 5.2, 5.5 }[Math.Clamp(ConnStructDiameterIndex, 0, 4)];
    [ObservableProperty] private double _connStructPlateCount = 1;// 结构侧连接片数
    [ObservableProperty] private double _steelFrameThickness = 2.5;
    [ObservableProperty] private int _steelFrameScrewTypeIndex;   // 0=自攻螺钉,1=自攻自钻
    [ObservableProperty] private int _steelFrameScrewDiameterIndex;// 附框规格候选 3.5/3.9/4.2/4.8/5.5
    public double SteelFrameScrewDiameter =>
        new[] { 3.5, 3.9, 4.2, 4.8, 5.5 }[Math.Clamp(SteelFrameScrewDiameterIndex, 0, 4)];

    public static IReadOnlyList<string> ConnFastenerTypes { get; } =
        DoorConnectorEngine.FastenerTypeNames;
    public static IReadOnlyList<string> ConnMaterials { get; } = DoorConnectorEngine.MaterialNames;
    public static IReadOnlyList<string> ConnWallMaterials { get; } = DoorConnectorEngine.MaterialNames;

    /// <summary>当前钉类型对应的规格候选（V31 下拉联动）。</summary>
    public IReadOnlyList<string> ConnDiameterOptions
    {
        get
        {
            var ds = DoorConnectorEngine.FastenerDiameters[
                Math.Clamp(ConnFastenerTypeIndex, 0, 4)];
            var list = new List<string>(ds.Length);
            foreach (var d in ds) list.Add(d.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            return list;
        }
    }

    partial void OnConnFastenerTypeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(ConnDiameterOptions));
        OnPropertyChanged(nameof(ConnScrewDiameter));
    }
    partial void OnConnScrewDiameterIndexChanged(int value) => OnPropertyChanged(nameof(ConnScrewDiameter));
    partial void OnConnStructDiameterIndexChanged(int value) => OnPropertyChanged(nameof(ConnStructDiameter));
    partial void OnSteelFrameScrewDiameterIndexChanged(int value) => OnPropertyChanged(nameof(SteelFrameScrewDiameter));
    partial void OnDeflLimitDivisorIndexChanged(int value) => OnPropertyChanged(nameof(DeflLimitDivisor));
    public IReadOnlyList<string> SteelFrameDiameterOptions
    {
        get
        {
            var list = new List<string>();
            foreach (var d in new[] { 3.5, 3.9, 4.2, 4.8, 5.5 })
                list.Add(d.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            return list;
        }
    }

    // 文本包装（数字框）
    private string T(double v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    private static bool TryNum(string? s, out double v)
        => double.TryParse((s ?? "").Trim(), System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out v) && v > 0;
    public string MullionSpanText { get => T(MullionSpanMm); set { if (TryNum(value, out var v)) { MullionSpanMm = v; Invalidate("竖梃跨度已改"); } } }
    public string MullionSpacingText { get => T(MullionSpacingMm); set { if (TryNum(value, out var v)) { MullionSpacingMm = v; Invalidate("竖梃间距已改"); } } }
    public string MullionLengthText { get => T(MullionLengthMm); set { if (TryNum(value, out var v)) { MullionLengthMm = v; Invalidate("竖梃长度已改"); } } }
    public string TransomLoadWidthText { get => T(TransomLoadWidthMm); set { if (TryNum(value, out var v)) { TransomLoadWidthMm = v; Invalidate("横梃受荷宽已改"); } } }
    public string TransomSpan1Text { get => T(TransomSpan1Mm); set { if (TryNum(value, out var v)) { TransomSpan1Mm = v; Invalidate("横梃跨度已改"); } } }
    public string TransomSpan2Text { get => T(TransomSpan2Mm); set { if (TryNum(value, out var v)) { TransomSpan2Mm = v; Invalidate("横梃跨度已改"); } } }
    public string WindMinPaText { get => T(WindManualMinPa); set { if (TryNum(value, out var v)) { WindManualMinPa = v; Invalidate("风下限已改"); } } }
    public string OpeningHeightText { get => T(OpeningHeightMm); set { if (TryNum(value, out var v)) { OpeningHeightMm = v; Invalidate("洞口高已改"); } } }
    public string OpeningWidthText { get => T(OpeningWidthMm); set { if (TryNum(value, out var v)) { OpeningWidthMm = v; Invalidate("洞口宽已改"); } } }
    public string ConnCountText { get => T(ConnectorCount); set { if (TryNum(value, out var v)) { ConnectorCount = v; Invalidate("连接点数已改"); } } }
    public string ConnPlateThkText { get => T(ConnPlateThickness); set { if (TryNum(value, out var v)) { ConnPlateThickness = v; Invalidate("连接片厚已改"); } } }
    public string ConnWallThkText { get => T(ConnWallThickness); set { if (TryNum(value, out var v)) { ConnWallThickness = v; Invalidate("薄壁件厚已改"); } } }
    public string ConnStructPlateText { get => T(ConnStructPlateCount); set { if (TryNum(value, out var v)) { ConnStructPlateCount = v; Invalidate("结构片数已改"); } } }
    public string SteelFrameThkText { get => T(SteelFrameThickness); set { if (TryNum(value, out var v)) { SteelFrameThickness = v; Invalidate("附框厚已改"); } } }

    // ══ 输出 ══
    public ObservableCollection<PreviewLine> Results { get; } = new();
    public bool HasResults => Results.Count > 0;
    public string PlaceholderText => HasResults ? "" :
        "点「玻璃预演」初始化风荷载链 → 点「竖梃/横梃/连接件 预演」逐模块计算（同 V31 顺序）。";

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _statusIsWarning;

    /// <summary>wk 是否已由「玻璃预演」下传（V31 铁律：先玻璃预演，后续预演才有输入）。</summary>
    public bool WkReady => Session.PanelWindNmm2 > 0;
    public string WkNote => WkReady
        ? $"玻璃 wk = {WkGlassNmm2 * 1000:0.###}KPa、杆件 wk = {WkRodNmm2 * 1000:0.###}KPa（V31 门窗玻璃预演两条链下传）"
        : CalcSession.UpstreamNotReadyText;

    /// <summary>
    /// 连接件用玻璃 wk（N/mm²）。V31 门窗玻璃预演链下传 0.00137（输出「1.37KPa」实采）；
    /// 上游第一屏已解析时用 Session.PanelWindNmm2（默认态同为 0.00137，逐位等值）。
    /// </summary>
    public double WkGlassNmm2 => WkReady ? Session.PanelWindNmm2 : 0.00137;

    /// <summary>
    /// 杆件本体用 wk（N/mm²）= 0.001333。V31 门窗玻璃预演的**第二条链**（0x14184AE：
    /// w0·βgz·μz·μs杆件(1.168) → round6 0x14184D0）专用，与玻璃 wk 0.00137 不同 μs；
    /// 默认值即 V31 启动默认链（0.45×1.6929×1.4983×1.168=1.333171Pa→0.001333），
    /// 走第一页时由 <see cref="SetWkFromChain"/> 由链重算（铁律：wk 由第一页链产出下传）。
    /// 实采 door_body_traces.json（输出「1.333KPa」逐位）。
    /// </summary>
    public double WkRodNmm2 { get; set; } = 0.001333;

    /// <summary>门窗杆件体型系数 μs（V31 链 0x14184AE 直读，门窗杆件专用）。</summary>
    public const double RodShapeCoef = 1.168;

    /// <summary>由第一页链值（w0/βgz/μz）重算杆件 wk = round6(w0·βgz·μz·1.168)。</summary>
    public void SetWkFromChain(double w0Kpa, double betaGz, double muZ)
    {
        WkRodNmm2 = WindLoadEngine.VbaRound(
            w0Kpa * betaGz * muZ * RodShapeCoef / 1000.0, 6);
    }

    private void Invalidate(string reason)
    {
        OnPropertyChanged(nameof(WkReady));
        OnPropertyChanged(nameof(WkNote));
        OnPropertyChanged(nameof(ConnDiameterOptions));
        OnPropertyChanged(nameof(ConnScrewDiameter));
        OnPropertyChanged(nameof(ConnStructDiameter));
        OnPropertyChanged(nameof(SteelFrameDiameterOptions));
        OnPropertyChanged(nameof(SteelFrameScrewDiameter));
        OnPropertyChanged(nameof(WindUseCalcOnly));
        OnPropertyChanged(nameof(NoCalcConnector));
        OnPropertyChanged(nameof(NoSteelFrame));
        OnPropertyChanged(nameof(GrooveConnection));
        if (Results.Count == 0) return;
        Results.Clear();
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(PlaceholderText));
        StatusIsWarning = true;
        StatusText = reason + "，请重新预演";
    }

    private const string Sep39 = "*************************************";
    private const string Sep38 = "**************************************";
    private const string Dash36 = "------------------------------------";
    private const string Dash19 = "———————————————————";

    private static readonly IBrush ColorNormal = Brushes.Black;
    private static readonly IBrush ColorRule = new SolidColorBrush(Color.Parse("#4B5563"));
    private static readonly IBrush ColorOk = new SolidColorBrush(Color.Parse("#0000C0"));
    private static readonly IBrush ColorFail = new SolidColorBrush(Color.Parse("#C00000"));

    private void Add(string text, IBrush color) => Results.Add(new PreviewLine(text, color));
    private void Judge(bool ok) => Add(ok ? "满足要求(√)" : "不满足要求(×)", ok ? ColorOk : ColorFail);
    private static string N(double v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>V31 计算杆件用 wk（N/mm²）：门窗玻璃预演第二条链下传（round6 0.001333，
    /// 与玻璃 wk 0.00137 分链——μs 不同，door_body_traces.json 0x137D40C 输出 1.333KPa 逐位）。</summary>
    private double WkRod() => WkRodNmm2;

    private DoorMullionEngine.DeflLimitMode LimitMode =>
        (DoorMullionEngine.DeflLimitMode)Math.Clamp(DeflLimitModeIndex, 0, 2);

    /// <summary>玻璃预演：wk 链初始化（V31 必须先点，后续预演才有输入）。</summary>
    [RelayCommand]
    private void GlassPreview()
    {
        if (!WkReady)
        {
            StatusIsWarning = true;
            StatusText = CalcSession.UpstreamNotReadyText;
            return;
        }
        Results.Clear();
        Add(Sep38, ColorRule);
        Add($"玻璃部分风荷载标准值为:{N(Session.PanelWindNmm2 * 1000)}KPa", ColorNormal);
        Add(Sep38, ColorRule);
        StatusIsWarning = false;
        StatusText = $"玻璃预演完成：wk={Session.PanelWindNmm2:0.######}N/mm² 已下传";
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(PlaceholderText));
    }

    /// <summary>竖梃（中竖挺）预演。</summary>
    [RelayCommand]
    private void MullionPreview()
    {
        if (!Guard()) return;
        // V31 实采：竖梃预演**重写输出框**（door_body_traces 竖挺块框内无玻璃行、直接以
        // 「计算杆件时风荷载标准值为」开头），不像实现初版那样追加在玻璃行之后。
        Results.Clear();
        var (f, fv, e) = DoorMullionEngine.Material(MullionMaterial);
        var r = DoorMullionEngine.ComputeMullion(new DoorMullionEngine.MullionInput
        {
            WkRodNmm2 = WkRod(),
            SpanL1Mm = MullionSpanMm,
            SpacingL2Mm = MullionSpacingMm,
            LengthLtMm = MullionLengthMm,
            WMm3 = MullionSectionW,
            IxMm4 = MullionSectionI,
            SMm3 = MullionSectionS,
            TwMm = MullionSectionTw,
            FMpa = f, FvMpa = fv, EMpa = e,
            LimitMode = LimitMode,
            LimitDivisor = DeflLimitDivisor,
        });
        Add(Sep39, ColorRule);
        Add($"计算杆件时风荷载标准值为:{N(r.WkKpa)}KPa", ColorNormal);
        Add(Sep39, ColorRule);
        Add($"中竖挺抗弯强度计算值:{N(r.Sigma)}MPa", ColorNormal);
        Add($"中竖挺抗弯强度设计值:{N(r.FMpa)}MPa", ColorNormal);
        Judge(r.SigmaOk);
        Add(Sep39, ColorRule);
        Add($"中竖挺挠度计算值:{N(r.Defl)}mm", ColorNormal);
        Add($"中竖挺挠度限值:{N(r.DeflLimit)}mm", ColorNormal);
        Judge(r.DeflOk);
        Add(Sep39, ColorRule);
        Add($"中竖挺抗剪强度计算值:{N(r.Tau)}MPa", ColorNormal);
        Add($"中竖挺抗剪强度设计值:{N(r.FvMpa)}MPa", ColorNormal);
        Judge(r.TauOk);
        Add(Sep39, ColorRule);
        Done($"中竖挺 {MullionMaterial} 预演完成");
    }

    /// <summary>横梃（中横挺）预演。</summary>
    [RelayCommand]
    private void TransomPreview()
    {
        if (!Guard()) return;
        // V31 实采：横梃预演同样重写输出框（door_body_traces 横挺块直接以分隔线开头）。
        Results.Clear();
        var (f, fv, e) = DoorMullionEngine.Material(TransomMaterial);
        var r = DoorMullionEngine.ComputeTransom(new DoorMullionEngine.TransomInput
        {
            WkRodNmm2 = WkRod(),
            SpanL1Mm = TransomLoadWidthMm,
            LengthL2Mm = TransomSpan1Mm,
            WMm3 = TransomSectionW,
            ImM4 = TransomSectionI,
            SMm3 = TransomSectionS,
            TwMm = TransomSectionTw,
            WVertMm3 = TransomVertW,
            IVertMm4 = TransomVertI,
            SVertMm3 = TransomVertS,
            TVertMm = TransomVertTw,
            FMpa = f, FvMpa = fv, EMpa = e,
            LimitMode = LimitMode,
            LimitDivisor = DeflLimitDivisor,
            VertLimitDivisor = DeflVertDivisor,
        });
        Add(Sep39, ColorRule);
        Add($"计算杆件时风荷载标准值为:{N(r.WkKpa)}KPa", ColorNormal);
        Add(Sep39, ColorRule);
        Add($"中横挺抗弯强度计算值:{N(r.Sigma)}MPa", ColorNormal);
        Add($"中横挺抗弯强度设计值:{N(r.FMpa)}MPa", ColorNormal);
        Judge(r.SigmaOk);
        Add(Sep39, ColorRule);
        Add($"中横挺水平方向挠度计算值:{N(r.DeflHoriz)}mm", ColorNormal);
        Add($"中横挺水平方向挠度限值:{N(r.DeflLimit)}mm", ColorNormal);
        Judge(r.DeflHorizOk);
        Add(Dash36, ColorRule);
        Add($"中横挺垂直方向挠度计算值:{N(r.DeflVert)}mm", ColorNormal);
        Add($"中横挺垂直方向挠度限值:{N(r.DeflLimitVert)}mm", ColorNormal);
        Judge(r.DeflVertOk);
        Add(Sep39, ColorRule);
        Add($"中横挺水平方向抗剪强度计算值:{N(r.TauHoriz)}MPa", ColorNormal);
        Add($"中横挺水平方向抗剪强度设计值:{N(r.FvMpa)}MPa", ColorNormal);
        Judge(r.TauHorizOk);
        Add(Dash36, ColorRule);
        Add($"中横挺垂直方向抗剪强度计算值:{N(r.TauVert)}MPa", ColorNormal);
        Add($"中横挺垂直方向抗剪强度设计值:{N(r.FvMpa)}MPa", ColorNormal);
        Judge(r.TauVertOk);
        Add(Sep39, ColorRule);
        Done($"中横挺 {TransomMaterial} 预演完成");
    }

    /// <summary>连接件预演（V31：卡槽连接跳过边框块；不计算连接件无输出；有钢附框插两块）。</summary>
    [RelayCommand]
    private void ConnectorPreview()
    {
        if (!WkReady) { StatusIsWarning = true; StatusText = CalcSession.UpstreamNotReadyText; return; }
        if (!CalcConnector)
        {
            // V31 实采：不计算连接件 → 0 输出（door_conn_probe4a noconn 实证）
            Results.Clear();
            Done("未勾选「计算连接件」：无输出（同 V31）");
            return;
        }
        var it = new DoorConnectorEngine.Input
        {
            WkNmm2 = Session.PanelWindNmm2,   // 连接件用玻璃 wk（round6），V31 链 0x14049D3 直取
            OpeningHeightMm = OpeningHeightMm,
            OpeningWidthMm = OpeningWidthMm,
            ConnectorCount = ConnectorCount,
            PlateThicknessMm = ConnPlateThickness,
            WallThicknessMm = ConnWallThickness,
            WallMaterialIndex = Math.Clamp(ConnWallMaterialIndex, 0, 10),
            FrameFastenerType = Math.Clamp(ConnFastenerTypeIndex, 0, 4),
            FrameScrewDiameter = ConnScrewDiameter,
            PlateCountFrameSide = ConnPlateCount,
            StructScrewDiameter = ConnStructDiameter,
            PlateCountStructSide = ConnStructPlateCount,
            ExpansionMode = ConnExpansionFix,
            ScrewConnection = ScrewConnection,
            HasSteelFrame = HasSteelFrame,
            SteelFrameThicknessMm = SteelFrameThickness,
            FrameScrewType = Math.Clamp(SteelFrameScrewTypeIndex, 0, 1),
            SteelFrameScrewDiameter = SteelFrameScrewDiameter,
        };
        var r = DoorConnectorEngine.Compute(it);
        double wkKpa = DoorMullionEngine.Materials.Length > 0
            ? WindLoadEngine.VbaRound(Session.PanelWindNmm2 * 1000.0, 3) : 0;

        Results.Clear();
        Add(Sep38, ColorRule);
        Add($"玻璃部分风荷载标准值为:{N(wkKpa)}KPa", ColorNormal);
        Add(Sep38, ColorRule);
        void Block(DoorConnectorEngine.FastenerCheck f)
        {
            Add(f.BlockLabel + ":", ColorNormal);
            Add($"被连接的薄壁件抗剪强度计算值:{N(f.PointForceN)}N", ColorNormal);
            Add($"被连接的薄壁件抗剪强度设计值:{N(f.WallCapacityN)}N", ColorNormal);
            Judge(f.WallOk);
            Add(Dash19, ColorRule);
            Add($"{f.FastenerRowPrefix}单个{f.FastenerLabel}的最大抗剪承载力设计值:{N(f.FastenerCapacityN)}N", ColorNormal);
            Add($"{f.FastenerRowPrefix}单个{f.FastenerLabel}的实际受力为:{N(f.PointForceN)}N", ColorNormal);
            Judge(f.FastenerOk);
            Add(Sep38, ColorRule);
        }
        if (r.FrameSide is { } a) Block(a);
        if (r.SteelFrameToWallA is { } b) Block(b);
        if (r.SteelFrameToWallB is { } c) Block(c);
        Add("连接片与建筑结构连接处:", ColorNormal);
        Add($"连接片的抗剪强度计算值:{N(r.StructPointForceN)}N", ColorNormal);
        Add($"连接片的抗剪强度设计值:{N(r.StructCapacityN)}N", ColorNormal);
        Judge(r.StructOk);
        Add(Sep38, ColorRule);
        Done("连接件预演完成");
    }

    private bool Guard()
    {
        if (WkReady) return true;
        StatusIsWarning = true;
        StatusText = CalcSession.UpstreamNotReadyText + "——请先完成「玻璃预演」";
        return false;
    }

    private void Done(string msg)
    {
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(PlaceholderText));
        StatusIsWarning = false;
        StatusText = msg;
    }

    [RelayCommand] private void Prev() => _wizard.GoBasic();
    [RelayCommand] private void Exit() => _wizard.CloseForm();
}
