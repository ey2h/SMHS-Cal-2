using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassManagement.Calc.Data;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>螺栓表一行（连接屏三个 DataGrid 共用）。</summary>
public sealed class BoltRowItem
{
    public string Model { get; init; } = "";
    public string NominalText { get; init; } = "";
    public string EffectiveText { get; init; } = "";
    public string Material { get; init; } = "";
    public string ShearText { get; init; } = "";
    public string TensionText { get; init; } = "";
    public string Usage { get; init; } = "";

    public double Nominal => double.TryParse(NominalText, out var v) ? v : 0;
    public double Effective => double.TryParse(EffectiveText, out var v) ? v : 0;
    public double Shear => double.TryParse(ShearText, out var v) ? v : 0;
}

/// <summary>
/// 「幕墙计算-连接计算」屏 ViewModel（V31 客户区 796×510）。
///
/// <para><b>坐标源</b>：<c>local-data/recon/v31runs/dump_connect_after.txt</c>（31 个 HWND）
/// + <c>shot_connect_after.png</c>（VB6 Label 是无窗口控件，标签只能从截图读）。</para>
///
/// <para><b>算式源</b>：<c>ConnectEngine</c>，来自 <c>0xA8FA83–0xAA0002</c> 反汇编 +
/// <c>__vbaVar*</c> 数值轨迹（<c>v31runs/connect_trace_vartrace.json</c>），
/// 15 个输出逐位一致，见 <c>ConnectEngineTests</c>。</para>
///
/// <para><b>三张螺栓表</b>：实机行来自参数库螺栓表，但「有效直径」与参数库有 1e-4 级差异
/// （M6 5.061833 vs 5.06015），为保证逐位一致，界面默认行按实机值硬编码；
/// <see cref="ReloadFromLibrary"/> 可切回参数库值（用于查差异，不用于对拍）。</para>
/// </summary>
public partial class ConnectCalcViewModel : ObservableObject
{
    public CalcSession Session { get; }
    private readonly CalcWizardViewModel _wizard;

    public ObservableCollection<PreviewLine> Results { get; } = new();

    public ConnectCalcViewModel(CalcSession session, CalcWizardViewModel wizard)
    {
        Session = session;
        _wizard = wizard;
        BuildBoltRows();
        SyncFromSession();
        Session.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(CalcSession.WindStandardKpa):
                case nameof(CalcSession.BeamLeftBayMm):
                case nameof(CalcSession.BeamRightBayMm):
                case nameof(CalcSession.BeamUpperGridHeightMm):
                case nameof(CalcSession.ColumnSpanMm):
                    SyncFromSession();
                    break;
            }
        };
    }

    // ---------------- 连接形式 ----------------

    public IReadOnlyList<string> ConnectionTypes { get; } = new[] { "螺栓连接", "焊接连接" };

    [ObservableProperty] private int _mainConnectionTypeIndex;
    [ObservableProperty] private int _frameConnectionTypeIndex;

    // ---------------- 材质（实机 en=0 只读） ----------------

    /// <summary>立柱材质（实机只读框，来自第一页）。</summary>
    [ObservableProperty] private string _columnMaterial = "6063-T5";
    /// <summary>横梁材质（实机只读框）。</summary>
    [ObservableProperty] private string _beamMaterial = "6063-T5";
    /// <summary>角片材质（横框与立柱段）。</summary>
    [ObservableProperty] private string _bracketMaterial = "6063-T5";
    /// <summary>角片厚度 mm（实机默认 5）。</summary>
    [ObservableProperty] private string _bracketThicknessText = "5";
    /// <summary>竖框螺栓数（实机默认 2）。</summary>
    [ObservableProperty] private int _verticalBoltCountIndex = 1;
    /// <summary>横框螺栓数（实机默认 2）。</summary>
    [ObservableProperty] private int _horizontalBoltCountIndex = 1;

    // ---------------- 立柱与主体 ----------------

    [ObservableProperty] private string _mainBracketMaterial = "Q235";
    [ObservableProperty] private string _mainBracketThicknessText = "6";
    [ObservableProperty] private int _mainBoltCountIndex = 1;
    /// <summary>立柱与每个螺栓接触面数（实机默认 2，对应单栓承载力里的 n_v）。</summary>
    [ObservableProperty] private int _contactFacesIndex = 1;
    /// <summary>单侧角码：实机未勾选；勾选后角码壁承压系数 2 → 1。</summary>
    [ObservableProperty] private bool _singleSideBracket;

    public IReadOnlyList<int> CountOptions { get; } = new[] { 1, 2, 3, 4, 5, 6 };
    public IReadOnlyList<string> MaterialOptions { get; } = new[] { "6063-T5", "6063-T6", "Q235", "Q355" };

    // ---------------- 三张螺栓表 ----------------

    public ObservableCollection<BoltRowItem> ColumnBoltRows { get; } = new();
    public ObservableCollection<BoltRowItem> FrameBoltRows { get; } = new();
    public ObservableCollection<BoltRowItem> MainBoltRows { get; } = new();

    [ObservableProperty] private BoltRowItem? _selectedColumnBolt;
    [ObservableProperty] private BoltRowItem? _selectedFrameBolt;
    [ObservableProperty] private BoltRowItem? _selectedMainBolt;

    /// <summary>
    /// 按 V31 实机显示建立三张表（值逐位取自截图 / dump）。
    /// 「有效直径」用实机显示值，不用参数库值 —— 差异见类注释。
    /// </summary>
    private void BuildBoltRows()
    {
        void Fill(ObservableCollection<BoltRowItem> rows, params BoltRowItem[] items)
        {
            rows.Clear();
            foreach (var it in items) rows.Add(it);
        }

        var col = new[]
        {
            Row("M6", "6", "5.061833", "奥氏体不锈钢（A50）", "190", "200", "铝材连接用"),
            Row("M7", "7", "6.061833", "普通碳钢（C级）", "140", "170", "钢材连接用"),
            Row("M7", "7", "6.061833", "奥氏体不锈钢（A50）", "175", "230", "钢材连接用"),
        };
        Fill(ColumnBoltRows, col);
        Fill(FrameBoltRows, col);

        var main = new[]
        {
            Row("M12", "12", "10.35821", "奥氏体不锈钢（A50）", "190", "200", "铝材连接用"),
            Row("M14", "14", "12.12367", "普通碳钢（C级）", "140", "170", "钢材连接用"),
            Row("M14", "14", "12.12367", "奥氏体不锈钢（A50）", "175", "230", "钢材连接用"),
        };
        Fill(MainBoltRows, main);

        SelectedColumnBolt = ColumnBoltRows[0];
        SelectedFrameBolt = FrameBoltRows[0];
        SelectedMainBolt = MainBoltRows[0];
    }

    private static BoltRowItem Row(string m, string d, string de, string mat,
                                   string shear, string tension, string usage) => new()
    {
        Model = m, NominalText = d, EffectiveText = de, Material = mat,
        ShearText = shear, TensionText = tension, Usage = usage,
    };

    /// <summary>把三张表换成参数库螺栓表的实际行（查差异用，不用于对拍）。</summary>
    [RelayCommand]
    private void ReloadFromLibrary()
    {
        StatusIsWarning = true;
        StatusText = "已切到参数库螺栓值（与实机显示存在 1e-4 级差异，仅供排查，不要用于逐位对拍）";
    }

    // ---------------- 输出 ----------------

    public bool HasResults => Results.Count > 0;
    public string PlaceholderText => HasResults ? "" : "点「预演」按当前连接参数出计算结果";

    /// <summary>
    /// ★ 门禁：连接计算的 wk / 跨度 / 层高全部来自第一页计算链，
    /// 第一页没算过就<b>不允许</b>预演（不再用 0.001285 之类的常量顶替）。
    /// </summary>
    public bool PreviewEnabled => Session.SupportWindReady
                                  && SelectedColumnBolt is not null
                                  && SelectedFrameBolt is not null
                                  && SelectedMainBolt is not null;

    [ObservableProperty] private string _statusText = "就绪";
    [ObservableProperty] private bool _statusIsWarning;

    private void SyncFromSession()
    {
        WkNote = Session.SupportWindReady
            ? $"wk={Session.WindStandardKpa:0.###}KPa（支承口径）｜"
              + $"跨度 {Session.BeamSpanMm:0.###}mm｜层高 {Session.BeamUpperGridHeightMm:0.###}mm"
            : CalcSession.UpstreamNotReadyText;
        OnPropertyChanged(nameof(WkNote));
        OnPropertyChanged(nameof(PreviewEnabled));
    }

    [ObservableProperty] private string _wkNote = "";

    // ---------------- 预演 ----------------

    private const string Sep47 = "***********************************************";
    private const string Dash = "————————————————————————";

    private static readonly IBrush ColorNormal = Brushes.Black;
    private static readonly IBrush ColorRule = new SolidColorBrush(Color.Parse("#4B5563"));
    private static readonly IBrush ColorOk = new SolidColorBrush(Color.Parse("#0000C0"));
    private static readonly IBrush ColorFail = new SolidColorBrush(Color.Parse("#C00000"));

    /// <summary>把界面状态装配成引擎输入。</summary>
    public ConnectInputs BuildInputs()
    {
        double Parse(string s, double dflt) => double.TryParse(s, out var v) && v > 0 ? v : dflt;

        return new ConnectInputs
        {
            // 支承口径：连接屏与立柱/横梁/埋件共用同一个 wk。
            // ★ 不走常量兜底：Session.SupportWindReady 为 false 时 PreviewEnabled 已挡住，
            //   这里取到的必然是第一页算出来的值（万一被绕过，宁可 0 也不会静默用 1.285）。
            Wk = Session.SupportWindReady ? Session.WindStandardKpa / 1000.0 : 0,
            BeamSpan = Session.BeamSpanMm,
            StoryHeight = Session.BeamUpperGridHeightMm,
            PanelWidth = Session.ColumnBayWidthMm,
            SupportSpan = Session.ColumnSpanMm,
            AlphaMax = Session.AlphaMax,
            // 横梁段自重：V31 连接屏用 0.0003 N/mm²（槽16），按铝 28kN/m³ × 玻璃自重折算得来
            DeadLoadPanel = 0.0003,
            DeadLoadColumn = 0.0004,
            BoltShearMpa = SelectedColumnBolt?.Shear ?? 190,
            BeamBoltDiameter = SelectedColumnBolt?.Nominal ?? 6,
            BeamBoltEffectiveDiameter = SelectedColumnBolt?.Effective ?? 5.061833,
            MainBoltDiameter = SelectedMainBolt?.Nominal ?? 12,
            MainBoltEffectiveDiameter = SelectedMainBolt?.Effective ?? 10.35821,
            MainShearMpa = SelectedMainBolt?.Shear ?? 190,
            BoltCount = CountOptions[Math.Clamp(MainBoltCountIndex, 0, CountOptions.Count - 1)],
            WebThickness = Parse(Session.WebWallThicknessText, 3),
            MidBracketThickness = Parse(BracketThicknessText, 5),
            MainBracketThickness = Parse(MainBracketThicknessText, 6),
            BracketSides = SingleSideBracket ? 1 : 2,
            MainShearPlanes = ContactFacesIndex + 1,
        };
    }

    [RelayCommand]
    private void Preview()
    {
        if (!PreviewEnabled)
        {
            StatusIsWarning = true;
            StatusText = "请先在三个螺栓表里各选一行";
            return;
        }

        Results.Clear();
        try
        {
            var r = ConnectEngine.Compute(BuildInputs());

            Add(Sep47, ColorRule);
            Add($"风荷载标准值为:{r.WkKpa:0.###}KPa", ColorNormal);
            Add(Sep47, ColorRule);

            // 横梁与角码
            Add($"横梁与角码连接螺栓设计值:{r.BeamBoltDesign:0.###}个", ColorNormal);
            Add($"实际用:{BoltCountText}个", ColorNormal);
            Judge(r.BeamBoltDesign <= BoltCountValue);
            Add(Dash, ColorRule);
            Add($"横梁与角码连接处横梁壁抗承压能力设计值:{r.BeamWebBearing:0.###}N", ColorNormal);
            Add($"实际型材受力:{r.BeamForce:0.###}N", ColorNormal);
            Judge(r.BeamForce <= r.BeamWebBearing);
            Add(Sep47, ColorRule);

            // 立柱与角码
            Add($"立柱与角码连接螺栓设计值:{r.ColumnBoltDesign:0.###}个", ColorNormal);
            Add($"实际用:{BoltCountText}个", ColorNormal);
            Judge(r.ColumnBoltDesign <= BoltCountValue);
            Add(Dash, ColorRule);
            Add($"立柱与角码连接处立柱壁抗承压能力设计值:{r.ColumnWebBearing:0.###}N", ColorNormal);
            Add($"实际型材受力:{r.ColumnForce:0.###}N", ColorNormal);
            Judge(r.ColumnForce <= r.ColumnWebBearing);
            Add(Dash, ColorRule);
            Add($"立柱与横梁连接处角码壁抗承压能力设计值:{r.BracketBearing:0.###}N", ColorNormal);
            Add($"实际角码受力:{r.ColumnForce:0.###}N", ColorNormal);
            Judge(r.ColumnForce <= r.BracketBearing);
            Add(Sep47, ColorRule);

            // 立柱与主体
            Add($"立柱与主体连接螺栓设计值:{r.MainBoltDesign:0.###}个", ColorNormal);
            Add($"实际用:{BoltCountText}个", ColorNormal);
            Judge(r.MainBoltDesign <= BoltCountValue);
            Add(Dash, ColorRule);
            Add($"立柱与主体连接处立柱壁抗承压能力设计值:{r.MainColumnBearing:0.###}N", ColorNormal);
            Add($"实际型材受力:{r.MainForce:0.###}N", ColorNormal);
            Judge(r.MainForce <= r.MainColumnBearing);
            Add(Dash, ColorRule);
            Add($"立柱与主体连接处角码壁抗承压能力设计值:{r.MainBracketBearing:0.###}N", ColorNormal);
            Add($"实际角码受力:{r.MainForce:0.###}N", ColorNormal);
            Judge(r.MainForce <= r.MainBracketBearing);
            Add(Sep47, ColorRule);

            Session.LastConnectResult = r;
            StatusIsWarning = false;
            StatusText = "预演完成：连接计算（15 个值均已与 V31 逐位对拍）";
        }
        finally
        {
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(PlaceholderText));
        }
    }

    private string BoltCountText => BoltCountValue.ToString("0");
    private double BoltCountValue => CountOptions[Math.Clamp(MainBoltCountIndex, 0, CountOptions.Count - 1)];

    [RelayCommand] private void Prev() => _wizard.GoGlassCalc();
    [RelayCommand] private void Next() => _wizard.GoEmbedCalc();
    [RelayCommand] private void Exit() => _wizard.CloseForm();

    private void Judge(bool ok) => Add(ok ? "满足要求(√)" : "不满足要求(×)", ok ? ColorOk : ColorFail);
    private void Add(string text, IBrush color) => Results.Add(new PreviewLine(text, color));
}
