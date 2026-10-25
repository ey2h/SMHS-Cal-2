using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>
/// 幕墙计算向导会话：承载「基本参数输入 → 立柱计算 → 多跨数据输入 → 预演」
/// 一路沿用的参数。初值取自 V35 复刻界面默认显示值（与 V31 实测一致，
/// 便于对照）。所有窗体的 ViewModel 共享同一实例，不各自存副本。
/// </summary>
public partial class CalcSession : ObservableObject
{
    public static CalcSession Default { get; } = new();

    // ============================================================
    // ① 基本参数输入屏（CW10）
    // ============================================================

    /// <summary>重现期</summary>
    [ObservableProperty] private bool _return50 = true;
    [ObservableProperty] private bool _return100;

    /// <summary>地面类型 A/B/C/D</summary>
    [ObservableProperty] private bool _terrainA;
    [ObservableProperty] private bool _terrainB;
    // ★ 2026-10-25：V31 实机默认 = C 类地形（dump-basic 读回「C类地形 chk=1」）。
    //   此前默认 B 类，导致第一页算出的 wk 偏高（1.848 vs V31 1.285），立柱屏整体对不上。
    [ObservableProperty] private bool _terrainC = true;
    [ObservableProperty] private bool _terrainD;

    /// <summary>外部局部体型系数</summary>
    [ObservableProperty] private bool _coefExternalWall = true;
    [ObservableProperty] private bool _coefExternalCorner;
    [ObservableProperty] private bool _coefExternalManual;
    [ObservableProperty] private double _coefExternalManualValue = 1.0;

    /// <summary>内部局部体型系数</summary>
    [ObservableProperty] private bool _coefInnerEnclosed = true;
    [ObservableProperty] private bool _coefInnerOneWall010;
    [ObservableProperty] private bool _coefInnerOneWall030;
    [ObservableProperty] private bool _coefInnerOneWallGt030;
    [ObservableProperty] private bool _coefInnerManual;
    [ObservableProperty] private double _coefInnerManualValue = 0.2;

    /// <summary>风荷载标准值取值方式（链路里最关键的一组）</summary>
    [ObservableProperty] private bool _windByCodeMin = true;
    [ObservableProperty] private bool _windByCalcResult;
    [ObservableProperty] private bool _windManual;
    [ObservableProperty] private double _windManualKpa = 1.0;

    /// <summary>风荷载标准值（KPa）—— 预演输出口，前置屏算完写这里。<b>已 round 3 位</b>，用于显示与留痕。</summary>
    [ObservableProperty] private double _windStandardKpa = 1.0;

    /// <summary>
    /// 风荷载标准值（未取整）。<b>立柱链必须用它</b>：qk/qd/M/σ/挠度都由它派生。
    ///
    /// <para>V31 显示「1.848KPa」是 round3 后的字符串，但传给立柱的是 raw 值；
    /// 判别证据见 <c>WindLoadEngine.WkRaw</c> 注释（内0.6μs1 场景：raw→2.755 对，round3→2.756 错）。</para>
    /// </summary>
    [ObservableProperty] private double _windStandardRawKpa = 1.0;

    /// <summary>
    /// 未取整且<b>不含 K1/K2</b> 的主式值（w0·β·μz·μs合）。立柱链用它先算 qk 再乘 K：
    /// V31 是 round3(qk)×K 再 round3，不是 round3(qk×K)。
    /// </summary>
    [ObservableProperty] private double _windStandardRawMainKpa = 1.0;
    [ObservableProperty] private string _windStandardSource = "待前置屏计算";

    /// <summary>
    /// 计算链门禁：**只有走完第一屏「基本参数输入」并点「下一步」**（其 Next 里调 ResolveWindStandard）
    /// 才会置 true。立柱屏/多跨屏的「预演」必须先看这个位；为 false 时一律拒绝计算，
    /// 避免拿 <see cref="WindStandardKpa"/> 的初值 1.0 冒充真实结果。
    /// V35/V31 的数据流就是这样：后面的计算全部由第一屏的参数驱动。
    /// </summary>
    [ObservableProperty] private bool _windStandardResolved;

    // ---------- C5：当前菜单路径（每个幕墙类型/子项都有自己的第一页）----------
    /// <summary>当前顶级菜单，如「玻璃幕墙」「金属板幕墙」。</summary>
    [ObservableProperty] private string _currentTopMenu = "";
    /// <summary>当前子项，如「全隐框」「铝单板」「花岗岩：短槽连接」。</summary>
    [ObservableProperty] private string _currentSubMenu = "";

    /// <summary>菜单路径文本（状态栏/标题/门禁提示用）。</summary>
    public string CurrentMenuPath => string.IsNullOrWhiteSpace(CurrentSubMenu)
        ? CurrentTopMenu
        : CurrentTopMenu + " / " + CurrentSubMenu;

    /// <summary>
    /// 进入某个菜单子项：记录路径并**重置计算链**（门禁清零、风荷载结论作废）。
    /// 切类型/切子项 = 换了一条第一页，下游必须重新从第一页算起。
    /// </summary>
    public void EnterMenu(string top, string? sub)
    {
        CurrentTopMenu = top ?? "";
        CurrentSubMenu = sub ?? "";
        WindStandardResolved = false;
        WindStandardSource = "待前置屏计算";
        OnPropertyChanged(nameof(CurrentMenuPath));
    }

    /// <summary>幕墙结构形式</summary>
    [ObservableProperty] private bool _structFrame = true;   // 构件式（框架式）
    [ObservableProperty] private bool _structUnit;

    /// <summary>计算标高 m</summary>
    [ObservableProperty] private double _calcLevelM = 100;

    /// <summary>
    /// 荷载规范档位（V31 第一页下拉 (537,304)，实测三档）：
    /// 0 = 全国荷载规范；1 = 广东荷载规范(小于45米的低矮建筑)；2 = 广东荷载规范(大于45米的高层建筑)。
    ///
    /// <para><b>★ 2026-10-24 V31 实测差异（两处，不是一处）</b>：</para>
    /// <list type="bullet">
    /// <item>峰值因子 g：全国 <b>2.5</b> → 广东 <b>3.0</b>（V35 常量 0x401F18 / 0x401F68）</item>
    /// <item>封闭式内部体型系数：全国 <b>0.2</b> → 广东 <b>0.3</b>
    /// （V31 slot[47] 由 1.126 变 1.226 = 1×0.92582+0.3；0.4μs1 档两规范都是 1.326，说明只改封闭式）</item>
    /// </list>
    /// <para>基准算例：全国 wk=1.848（β=1.4956）→ 广东 wk=2.146（β=1.5947、μs合=1.226）。
    /// 低矮/高层两档在立柱屏数值**完全相同**（实测 2.146 / 1.5947 / 1.226 三个值一致）。</para>
    /// </summary>
    [ObservableProperty] private int _loadStandardIndex;

    /// <summary>是否按广东荷载规范（低矮或高层）。</summary>
    public bool IsGuangdongLoad => LoadStandardIndex > 0;

    /// <summary>
    /// 参数库查出的基本风压 w0（kN/m²）。由 ResolveWindStandard 写入。
    /// <para>用途：DBJ15-101-2014 <b>7.4.6</b> 只在「基本风压 ≥ 0.5 kN/m² 地区」才把峰值因子由 2.5 提到 3.0。
    /// V31 实测（v31_gd_threshold.csv）：北京 w0=0.45 选广东档，β 仍 <b>1.4956</b>（=2.5），
    /// 但内压仍取 0.3（μs合 1.226）→ 门槛只作用于 g，不作用于 μsi。</para>
    /// </summary>
    public double BasicWindW0 { get; set; }

    /// <summary>
    /// 峰值因子 g：全国 2.5 / 广东 3.0。
    /// <para><b>门槛</b>：广东档且 w0 ≥ 0.5 才取 3.0（DBJ15-101-2014 7.4.6）。
    /// V35 侧证据：0xA6FE6E <c>fld [0x401F68]</c>(3.0) 与 0xA6FF7A <c>fld [0x401F18]</c>(2.5) 为 if/else 两支，
    /// 其计算书文案分别为「按广东荷载规范取3.0」与国标文案。</para>
    /// </summary>
    public double PeakFactorG => IsGuangdongLoad && BasicWindW0 >= 0.5
        ? GlassManagement.Calc.Engines.WindLoadEngine.PeakFactorGd
        : GlassManagement.Calc.Engines.WindLoadEngine.PeakFactorGb;

    partial void OnLoadStandardIndexChanged(int value)
    {
        _ = value;
        OnPropertyChanged(nameof(IsGuangdongLoad));
        OnPropertyChanged(nameof(PeakFactorG));
    }

    /// <summary>地形修正系数 / 风向影响系数</summary>
    [ObservableProperty] private double _terrainFactor = 1.0;
    [ObservableProperty] private double _windDirFactor = 1.0;

    /// <summary>幕墙横框结构</summary>
    [ObservableProperty] private bool _transomNone = true;
    [ObservableProperty] private bool _transomMid;

    /// <summary>玻璃配置（六选一）</summary>
    [ObservableProperty] private bool _glassMono = true;
    [ObservableProperty] private bool _glassInsulated;
    [ObservableProperty] private bool _glassLaminated;
    [ObservableProperty] private bool _glassInsulLam;
    [ObservableProperty] private bool _glassDoubleInsul;
    [ObservableProperty] private bool _glassDoubleLam;

    /// <summary>
    /// 龙骨间距 6 项（V35 基本参数屏「龙骨间距」Frame，默认值取 V35 运行期控件树实测）：
    ///   立柱左分格宽度 1100 / 立柱右分格宽度 1100
    ///   横框上分格高度 1380 / 横框下分格高度 1380
    ///   中横框上分格高 1350 / 中横框下分格高 1350
    /// 立柱计算用的从属宽度 = **立柱左分格宽度**（V31 实测 qk = wk×1.100 印证），由基本参数屏写入、立柱屏读取。
    /// </summary>
    [ObservableProperty] private double _bayColLeft = 1100;
    [ObservableProperty] private double _bayColRight = 1100;
    [ObservableProperty] private double _bayTransTop = 1380;
    [ObservableProperty] private double _bayTransBottom = 1380;
    [ObservableProperty] private double _bayMidTop = 1350;
    [ObservableProperty] private double _bayMidBottom = 1350;

    /// <summary>
    /// 立柱计算跨（mm）。用于风荷载从属面积 A = 分格宽 × 立柱跨（GB50009-2012 8.3.4 折减），
    /// 由立柱屏选定力学模型后写入；基本参数屏先用默认值估算。
    /// </summary>
    [ObservableProperty] private double _columnSpanMm = 3000;

    /// <summary>短跨长（mm，立柱屏力学模型 Frame 第二个输入框；单跨简支不用）。</summary>
    [ObservableProperty] private double _shortSpanMm = 700;

    /// <summary>
    /// 立柱从属宽度（mm）= 基本参数屏「龙骨间距」中间那对框（V35 rel(122,297)/(123,277)，默认 1100）。
    /// 取证：V31 单跨简支（受拉）预演挠度 34.574mm，反解 qk = 5qL⁴/(384EI) → q=2.0329 N/mm，
    /// 而 wk=1.848KPa × 1.100m = 2.0328 N/mm（用 1.35m 则得 2.4948，与挠度不符）。
    /// 待 V31 改该框数值做二次验证后转正式结论。
    /// </summary>
    [ObservableProperty] private double _columnBayWidthMm = 1100;

    /// <summary>抗震设防分类 / 场地类型 / 地区</summary>
    [ObservableProperty] private string _seismicClass = "标准设防类";
    [ObservableProperty] private string _siteClass = "Ⅱ类";
    /// <summary>
    /// 地区（参数库「地区参数」表行名）。默认取「北京」——
    /// 取证：V31 第一页开机默认 50 年基本风压 450（= 北京）、αmax 运行值 0.16（= 北京 8 度），
    /// 横梁预演 wk=1.285KPa 与之一致。改成「上海」则 αmax 0.08、wk 变 1.571（已实测联动）。
    /// </summary>
    [ObservableProperty] private string _regionName = "北京";
    [ObservableProperty] private string _loadStandard = "全国荷载规范";

    // ============================================================
    // ② 立柱计算屏（CW11）
    // ============================================================

    /// <summary>立柱材质牌号（V31 实测样本 = 6063A-T5）</summary>
    [ObservableProperty] private string _columnMaterial = "6063A-T5";

    // ── 「其它材质」人工输入（V31 立柱屏 Frame(169,117) 的 3 个参数框）──────────────────
    // 取证（2026-10-25 实机）：点「其它材质」(359,140) 后
    //   (529,162) → 输出的「选用型材的抗弯强度设计值」= 填入值
    //   (529,185) → 输出「抗剪强度设计值」= 填入值
    //   (529,140) 弹性模量框被禁用，恒 70000（铝）
    //   (391,162) 填入值不进结果（非强度项）
    // 实测：双跨 3000/1500 + f=90/fv=55 → w_pre = M/90 = 6628.134，σ/τ/df 不变。
    [ObservableProperty] private bool _customMaterial;
    [ObservableProperty] private double _customElasticModulus = 70000;
    [ObservableProperty] private double _customAllowBending = 90;
    [ObservableProperty] private double _customAllowShear = 55;

    /// <summary>挠度限值规范</summary>
    [ObservableProperty] private string _deflectionStandard = "JGJ336-2016";

    /// <summary>立柱选料：型材代号与截面特性（来源：V35 参数库「立柱」表 95 行，默认取表内第一项）。</summary>
    [ObservableProperty] private string _columnCode = "60/80系列";

    /// <summary>型材弹性模量 E（MPa，立柱计算屏输入框，V35 默认 70000）。</summary>
    [ObservableProperty] private double _elasticModulusMpa = 70000;

    /// <summary>当前选中型材的截面特性（I/W/S/t），供强度/挠度验算引用。</summary>
    public GlassManagement.Calc.Data.ParamLibrary.ColumnProfile? ColumnProfile
        => GlassManagement.Calc.Data.ParamLibrary.FindColumn(ColumnCode);

    partial void OnColumnCodeChanged(string value) => OnPropertyChanged(nameof(ColumnProfile));

    // ── 单元式幕墙：左右两根框各自一套型材 ─────────────────────────────
    // V31 立柱屏在单元式下会**重建**为两个独立 Frame：「单元立柱左框参数」「单元立柱右框参数」，
    // 每个 Frame 自带「快速查找型材」（实测 dump-col：Frame(13,145) 与 Frame(13,292)）。
    // V35 计算书原文（0x4EC890 一带）：
    //   f_1左 = γ0·(N_1/2A_1n + M_1·(I_1x/(I_1x+I_2x))/γW_1nx)
    //   f_1右 = γ0·(N_1/2A_2n + M_1·(I_2x/(I_1x+I_2x))/γW_2nx)
    //   「由于采用等刚度原则计算，所以挠度只须计算其中一根框即可，此处计算左单元框」
    //   d_f1,mid = 5·q_k左·L_1⁴/(384·E·I_1x)·(1−2.4(a_1/L_1)²)
    // 空串 = 沿用 ColumnCode（左右同型材，对应 V31 只选一套时的情形）。
    [ObservableProperty] private string _unitLeftCode = "";
    [ObservableProperty] private string _unitRightCode = "";

    /// <summary>左框型材：从参数库「单元立柱一」表里取（空 = 表内第一行）。</summary>
    public GlassManagement.Calc.Data.ParamLibrary.ColumnProfile? UnitLeftProfile
        => GlassManagement.Calc.Data.ParamLibrary.FindUnitColumnLeft(UnitLeftCode);

    /// <summary>右框型材：从参数库「单元立柱二」表里取（空 = 表内第一行）。**与左框是两张独立的表**。</summary>
    public GlassManagement.Calc.Data.ParamLibrary.ColumnProfile? UnitRightProfile
        => GlassManagement.Calc.Data.ParamLibrary.FindUnitColumnRight(UnitRightCode);

    partial void OnUnitLeftCodeChanged(string value) => OnPropertyChanged(nameof(UnitLeftProfile));
    partial void OnUnitRightCodeChanged(string value) => OnPropertyChanged(nameof(UnitRightProfile));

    /// <summary>力学模型（六选一，V35 立柱计算屏「力学模型」Frame 实测）：
    /// 单跨简支（受拉）/ 单跨简支（受压）/ 双跨梁 / 连续梁（四点三跨）/
    /// 多跨铰接连续静定梁 / 多跨铰接连续一次超静定梁</summary>
    [ObservableProperty] private bool _modelSingleTension = true;   // V31 实测默认 = 单跨简支（受拉）
    [ObservableProperty] private bool _modelSingleCompression;
    [ObservableProperty] private bool _modelDoubleSpan;
    [ObservableProperty] private bool _modelContinuous3Span;
    [ObservableProperty] private bool _modelMultiStatic;
    [ObservableProperty] private bool _modelMultiHyperStatic;

    /// <summary>力学模型六选一互斥（VB6 同 Frame 内 OptionButton 语义）。</summary>
    partial void OnModelSingleTensionChanged(bool value) { if (value) ClearModelExcept(nameof(ModelSingleTension)); }
    partial void OnModelSingleCompressionChanged(bool value) { if (value) ClearModelExcept(nameof(ModelSingleCompression)); }
    partial void OnModelDoubleSpanChanged(bool value) { if (value) ClearModelExcept(nameof(ModelDoubleSpan)); }
    partial void OnModelContinuous3SpanChanged(bool value) { if (value) ClearModelExcept(nameof(ModelContinuous3Span)); }
    partial void OnModelMultiStaticChanged(bool value) { if (value) ClearModelExcept(nameof(ModelMultiStatic)); }
    partial void OnModelMultiHyperStaticChanged(bool value) { if (value) ClearModelExcept(nameof(ModelMultiHyperStatic)); }
    private void ClearModelExcept(string keep)
    {
        if (keep != nameof(ModelSingleTension)) ModelSingleTension = false;
        if (keep != nameof(ModelSingleCompression)) ModelSingleCompression = false;
        if (keep != nameof(ModelDoubleSpan)) ModelDoubleSpan = false;
        if (keep != nameof(ModelContinuous3Span)) ModelContinuous3Span = false;
        if (keep != nameof(ModelMultiStatic)) ModelMultiStatic = false;
        if (keep != nameof(ModelMultiHyperStatic)) ModelMultiHyperStatic = false;
    }

    /// <summary>力学模型六选项的可读文本（状态栏/预演引用）。</summary>
    public string ModelCaption =>
        ModelSingleTension ? "单跨简支（受拉）" :
        ModelSingleCompression ? "单跨简支（受压）" :
        ModelDoubleSpan ? "双跨梁" :
        ModelContinuous3Span ? "连续梁（四点三跨）" :
        ModelMultiStatic ? "多跨铰接连续静定梁" :
        ModelMultiHyperStatic ? "多跨铰接连续一次超静定梁" : "未选";

    /// <summary>重现期复选项（与 50/100 年正交）</summary>
    [ObservableProperty] private bool _snowSensitive;

    /// <summary>抗震设防分类四选一</summary>
    [ObservableProperty] private bool _seismicStandard = true;
    [ObservableProperty] private bool _seismicKey;
    [ObservableProperty] private bool _seismicSpecial;
    [ObservableProperty] private bool _seismicModerate;
    partial void OnSeismicStandardChanged(bool value) { if (value) { SeismicKey = SeismicSpecial = SeismicModerate = false; } }
    partial void OnSeismicKeyChanged(bool value) { if (value) { SeismicStandard = SeismicSpecial = SeismicModerate = false; } }
    partial void OnSeismicSpecialChanged(bool value) { if (value) { SeismicStandard = SeismicKey = SeismicModerate = false; } }
    partial void OnSeismicModerateChanged(bool value) { if (value) { SeismicStandard = SeismicKey = SeismicSpecial = false; } }

    /// <summary>场地类型下拉项。★ V31 第一页 (469,253) 实测为 <b>5 项</b>：
    /// Ⅰ0类场地 / Ⅰ1类场地 / Ⅱ类场地 / Ⅲ类场地 / Ⅳ类场地（默认 Ⅱ类）。
    /// 复刻此前只有 4 项（缺 Ⅰ0/Ⅰ1 之分），已按 V31 对齐。
    /// <para>对「立柱屏（非地震组合）」的数值影响：V31 实测 5 档输出逐位相同（见 v31_basicsweep.psv B26–B29）。</para></summary>
    public IReadOnlyList<string> SiteClasses { get; } = new[]
    {
        "Ⅰ0类场地", "Ⅰ1类场地", "Ⅱ类场地", "Ⅲ类场地", "Ⅳ类场地"
    };

    // ============================================================
    // ③ 多跨数据输入屏
    // ============================================================

    [ObservableProperty] private int _totalSpans = 5;
    [ObservableProperty] private double _segmentAMm = 1000;   // 跨长 A（支座段 / 跨间距 Li）
    [ObservableProperty] private double _segmentLMm = 4000;   // 跨长 L（计算跨度 i）

    // ============================================================
    // 互斥组联动（VB6 语义：同 Frame 内 OptionButton 互斥）
    // ============================================================

    partial void OnReturn50Changed(bool value) { if (value) Return100 = false; }
    partial void OnReturn100Changed(bool value) { if (value) Return50 = false; }

    partial void OnTerrainAChanged(bool value) { if (value) { TerrainB = TerrainC = TerrainD = false; } }
    partial void OnTerrainBChanged(bool value) { if (value) { TerrainA = TerrainC = TerrainD = false; } }
    partial void OnTerrainCChanged(bool value) { if (value) { TerrainA = TerrainB = TerrainD = false; } }
    partial void OnTerrainDChanged(bool value) { if (value) { TerrainA = TerrainB = TerrainC = false; } }

    partial void OnCoefExternalWallChanged(bool value) { if (value) { CoefExternalCorner = CoefExternalManual = false; } }
    partial void OnCoefExternalCornerChanged(bool value) { if (value) { CoefExternalWall = CoefExternalManual = false; } }
    partial void OnCoefExternalManualChanged(bool value) { if (value) { CoefExternalWall = CoefExternalCorner = false; } }

    partial void OnCoefInnerEnclosedChanged(bool value)
    { if (value) { CoefInnerOneWall010 = CoefInnerOneWall030 = CoefInnerOneWallGt030 = CoefInnerManual = false; } }
    partial void OnCoefInnerOneWall010Changed(bool value)
    { if (value) { CoefInnerEnclosed = CoefInnerOneWall030 = CoefInnerOneWallGt030 = CoefInnerManual = false; } }
    partial void OnCoefInnerOneWall030Changed(bool value)
    { if (value) { CoefInnerEnclosed = CoefInnerOneWall010 = CoefInnerOneWallGt030 = CoefInnerManual = false; } }
    partial void OnCoefInnerOneWallGt030Changed(bool value)
    { if (value) { CoefInnerEnclosed = CoefInnerOneWall010 = CoefInnerOneWall030 = CoefInnerManual = false; } }
    partial void OnCoefInnerManualChanged(bool value)
    { if (value) { CoefInnerEnclosed = CoefInnerOneWall010 = CoefInnerOneWall030 = CoefInnerOneWallGt030 = false; } }

    partial void OnWindByCodeMinChanged(bool value) { if (value) { WindByCalcResult = WindManual = false; } }
    partial void OnWindByCalcResultChanged(bool value) { if (value) { WindByCodeMin = WindManual = false; } }
    partial void OnWindManualChanged(bool value) { if (value) { WindByCodeMin = WindByCalcResult = false; } }

    partial void OnStructFrameChanged(bool value) { if (value) StructUnit = false; }
    partial void OnStructUnitChanged(bool value) { if (value) StructFrame = false; }

    partial void OnGlassMonoChanged(bool value)
    { if (value) { GlassInsulated = GlassLaminated = GlassInsulLam = GlassDoubleInsul = GlassDoubleLam = false; } }
    partial void OnGlassInsulatedChanged(bool value)
    { if (value) { GlassMono = GlassLaminated = GlassInsulLam = GlassDoubleInsul = GlassDoubleLam = false; } }
    partial void OnGlassLaminatedChanged(bool value)
    { if (value) { GlassMono = GlassInsulated = GlassInsulLam = GlassDoubleInsul = GlassDoubleLam = false; } }
    partial void OnGlassInsulLamChanged(bool value)
    { if (value) { GlassMono = GlassInsulated = GlassLaminated = GlassDoubleInsul = GlassDoubleLam = false; } }
    partial void OnGlassDoubleInsulChanged(bool value)
    { if (value) { GlassMono = GlassInsulated = GlassLaminated = GlassInsulLam = GlassDoubleLam = false; } }
    partial void OnGlassDoubleLamChanged(bool value)
    { if (value) { GlassMono = GlassInsulated = GlassLaminated = GlassInsulLam = GlassDoubleInsul = false; } }

    // ============================================================
    // ① 基本参数输入屏 —— View 里已绑定的补充项（V35 权威控件）
    // ============================================================

    /// <summary>底部复选：按幕墙规范考虑80℃极限温差 / 计算中不考虑地震作用
    /// （V35 运行期两控件同位置 rel(163,585)，属互斥视图，按当前视图其一显示）</summary>
    [ObservableProperty] private bool _tempLimit80;
    [ObservableProperty] private bool _noSeismic;

    /// <summary>构件的安全性能等级-结构重要性系数：计算埋件时 / 计算非埋件时 两个下拉。
    /// 下拉宽仅 97px（V35 原值），条目文案必须短；γ0 映射放在 ToolTip/状态栏。</summary>
    public IReadOnlyList<string> GradeOptions { get; } = new[] { "1级", "2级", "3级" };
    [ObservableProperty] private string _gradeWhenInsert = "1级";
    [ObservableProperty] private string _gradeWhenNoInsert = "2级";

    /// <summary>安全等级 → 结构重要性系数 γ0（GB50068：一级 1.1 / 二级 1.0 / 三级 0.9）。</summary>
    public static double GammaZeroOf(string? grade) => grade switch
    {
        "1级" => 1.1,
        "3级" => 0.9,
        _ => 1.0,
    };
    /// <summary>计算埋件时的 γ0。</summary>
    public double GammaZero => GammaZeroOf(GradeWhenInsert);

    /// <summary>场地类型下拉选中值（绑 View）</summary>
    [ObservableProperty] private string _siteClassSelected = "Ⅱ类场地";

    /// <summary>地区查找输入框（关键字）与地区下拉</summary>
    [ObservableProperty] private string _regionKeyword = "";
    // ★ V31 启动时未选地区（w0 回落参数库首行北京 0.45kPa），见 CurrentRegion 注释
    [ObservableProperty] private string _regionSelected = "";

    /// <summary>地区参数表数据源 = V35 参数库「地区参数」表全量 634 行。
    /// 数据由 local-data/gen_param_library_cs.py 从 参数库.mdb 导出 CSV 编译期内嵌
    /// （GlassManagement.Calc.Data.ParamLibrary），界面不得使用自编样本。</summary>
    public System.Collections.Generic.IReadOnlyList<GlassManagement.Calc.Data.ParamLibrary.RegionRow> RegionRows
        => GlassManagement.Calc.Data.ParamLibrary.Regions;

    /// <summary>当前选中地区的参数库行（地区下拉 → 风压/地震/温差参数唯一来源）。</summary>
    /// <remarks>
    /// ★ 2026-10-25：V31 启动时「查找并定位地区」框为空（dump-basic 实读 TextBox(141,305)=''），
    /// 此时 w0 回落到**参数库首行 = 北京 450 → 0.45kPa**（反算证据：wk=1.285 = 0.45×βgz 1.6929×μz 1.4983×μs合 1.126）。
    /// 复刻此前默认「上海」(0.55)，导致立柱屏 wk 一直是 1.848/1.571 而 V31 是 1.285。
    /// </remarks>
    public GlassManagement.Calc.Data.ParamLibrary.RegionRow? CurrentRegion
        => string.IsNullOrWhiteSpace(RegionSelected)
            ? GlassManagement.Calc.Data.ParamLibrary.Regions.FirstOrDefault()
            : GlassManagement.Calc.Data.ParamLibrary.FindRegion(RegionSelected);

    /// <summary>50 年基本风压（kPa）= 参数库 Pa 值 / 1000。</summary>
    public double RegionWind50KPa => CurrentRegion?.Wind50KPa ?? 0;
    /// <summary>100 年基本风压（kPa）。</summary>
    public double RegionWind100KPa => CurrentRegion?.Wind100KPa ?? 0;

    partial void OnRegionSelectedChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentRegion));
        OnPropertyChanged(nameof(RegionWind50KPa));
        OnPropertyChanged(nameof(RegionWind100KPa));
    }

    /// <summary>玻璃配置的可读文本（结果区/计算书引用）。</summary>
    public string GlassCaption =>
        GlassMono ? "单片玻璃" :
        GlassInsulated ? "中空玻璃" :
        GlassLaminated ? "夹层玻璃" :
        GlassInsulLam ? "中空+夹层玻璃" :
        GlassDoubleInsul ? "双中空玻璃" :
        GlassDoubleLam ? "双夹层-中空玻璃" : "未选";

    public string TerrainCaption =>
        TerrainA ? "A类地形" : TerrainB ? "B类地形" : TerrainC ? "C类地形" : "D类地形";

    // ---------- 幕墙自重 Gk（参数库「幕墙自重参数」表，V35「自重参数预置」按钮同表）----------
    private GlassManagement.Calc.Data.ParamLibrary.DeadLoadRow DeadLoad
        => GlassManagement.Calc.Data.ParamLibrary.DeadLoadForGlass(GlassCaption);

    /// <summary>立柱及主体自重 Gk（N/m²）——立柱轴力 N1=1.3·Gk/A·B·(A1+L1)（V35 计算书 0x4E90C4）用它。</summary>
    public double DeadLoadGkColumn => DeadLoad.ColumnWeight;
    /// <summary>计算横梁时自重 Gk（N/m²）。</summary>
    public double DeadLoadGkTransom => DeadLoad.TransomWeight;
    /// <summary>自重表类别名（界面显示用，含适用范围）。</summary>
    public string DeadLoadCaption =>
        $"{DeadLoad.Category}（立柱及主体自重 {DeadLoad.ColumnWeight} N/m²，计算横梁时 {DeadLoad.TransomWeight} N/m²；{DeadLoad.Scope}）";

    // ============================================================
    // ③ 横梁计算屏
    // 尺寸口径实机取证（docs/V31_BEAM_UI.md 四之二）：
    //   跨度 B = 立柱左右两侧分格宽的**大值**（改 900/1100 → 用 1100；1200/1100 → 用 1200）
    //   自重高度 H = **上**分格高，不取平均（上 1500/下 1380 → 槽 [19]=0.450=1500×g）
    // ============================================================

    /// <summary>立柱左侧分格宽 mm（第一页「龙骨间距」上排宽）。</summary>
    [ObservableProperty] private double _beamLeftBayMm = 1100;

    /// <summary>立柱右侧分格宽 mm（第一页「龙骨间距」下排宽）。</summary>
    [ObservableProperty] private double _beamRightBayMm = 1100;

    /// <summary>上分格高 mm（横梁自重取值来源，不取平均）。</summary>
    [ObservableProperty] private double _beamUpperGridHeightMm = 1380;

    /// <summary>横梁材质牌号（默认 6063-T5 → f=90 / fv=55，与 V31 横梁页默认输出一致）。</summary>
    [ObservableProperty] private string _beamMaterial = "6063-T5";

    /// <summary>
    /// 幕墙结构形式：<c>true</c> = 单元式、<c>false</c> = 构件式（框架式）。
    /// V31 第一页「幕墙结构」Frame (648,85)：构件式 (653,105) / 单元式 (800,105)。
    /// 它决定横梁屏用哪套模板（构件式 12 量 / 单元式 17~27 量）。
    /// </summary>
    [ObservableProperty] private bool _wallStructureIsUnit;

    /// <summary>
    /// 计算中不考虑地震作用（V31 第一页同名复选框）。
    /// <para>
    /// **V31 自身的 bug**：勾选后横梁 12 个输出一字未变、αmax 仍 0.16（2026-10-25 实测）。
    /// 复刻版按设计意图修正：勾选后地震项 5αmax·g 置 0，只保留风荷载。
    /// </para>
    /// </summary>
    [ObservableProperty] private bool _ignoreSeismic;

    /// <summary>横梁跨度 mm = 左右分格宽取大（帮助 html/B03.htm：自动选最大一跨，不计立柱宽）。</summary>
    public double BeamSpanMm => Math.Max(BeamLeftBayMm, BeamRightBayMm);

    /// <summary>
    /// 地震影响系数最大值 αmax —— 参数库「地区参数」表 αmax 列。
    /// 实机取证：全局 0x1C913D8 存的就是它（北京 8 度 0.16 / 上海 7 度 0.08），
    /// 改地区到上海后横梁预选 Wy 2583.574→3007.074。
    /// </summary>
    public double AlphaMax =>
        GlassManagement.Calc.Data.ParamLibrary.FindRegion(RegionName)?.AlphaMax ?? 0.16;
}
