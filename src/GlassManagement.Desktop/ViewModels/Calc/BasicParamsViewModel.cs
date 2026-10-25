using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>
/// 「幕墙计算-基本参数输入」屏 ViewModel（V35 向导第 1 屏）。
/// 单选/复选/输入框全部读写共享 CalcSession —— 点「下一步」后立柱屏、多跨屏沿用同一值。
/// 可见性语义与 V35 一致：
///   · 「地形修正系数及风向影响系数」两个输入框仅当该 Frame 可见时可编辑（本复刻恒可见）；
///   · 外部/内部「人工给定」输入框仅当选了「人工给定」时启用（V35 实测联动）；
///   · 风荷载「人工给定」输入框仅当选「人工给定」时启用，且写入 WindManualKpa。
/// </summary>
public partial class BasicParamsViewModel : ObservableObject
{
    public CalcSession Session { get; }
    private readonly CalcWizardViewModel _wizard;

    public BasicParamsViewModel(CalcSession session, CalcWizardViewModel wizard)
    {
        Session = session;
        _wizard = wizard;
        // V35 联动：互斥单选变化会改变对应「人工给定」输入框的可用性
        Session.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(CalcSession.CoefExternalManual): OnPropertyChanged(nameof(ExternalManualEnabled)); break;
                case nameof(CalcSession.CoefInnerManual): OnPropertyChanged(nameof(InnerManualEnabled)); break;
                case nameof(CalcSession.WindManual): OnPropertyChanged(nameof(WindManualEnabled)); break;
            }
        };
    }

    /// <summary>「★按 ___ 计算荷载」下拉。内容 = V35 数据文件 guifan.dat 原文（GBK）：
    /// 抗震通用规范,1.0,1.4,0.2 / 玻璃幕墙规范JGJ102-2003,0.5,1.4,1.0 / …（三列=地震组合系数,地震分项系数,风荷载组合系数）</summary>
    public static readonly (string Name, double SeismicComb, double SeismicPart, double WindComb)[] GuifanTable =
    {
        ("抗震通用规范", 1.0, 1.4, 0.2),
        ("玻璃幕墙规范JGJ102-2003", 0.5, 1.4, 1.0),
        ("金属与石材幕墙规范JGJ133-2001", 0.6, 1.4, 1.0),
        ("上海地方标准", 1.0, 1.4, 0.2),
        ("浙江地方标准", 1.0, 1.4, 0.2),
        ("江苏地方标准", 1.0, 1.4, 0.2),
        ("湖北地方标准", 1.0, 1.4, 0.2),
        ("广东地方标准", 0.5, 1.5, 1.0),
        ("重庆地方标准", 1.0, 1.4, 0.6),
        ("四川地方标准", 1.0, 1.4, 0.6),
        ("工程实际情况", 0, 0, 0),
    };

    public System.Collections.Generic.IReadOnlyList<string> LoadStandards { get; } =
        System.Array.ConvertAll(GuifanTable, x => x.Name);

    /// <summary>地区下拉数据源 = V35 参数库「地区参数」表全量 634 个地区名（与界面表格同一份数据）。</summary>
    public System.Collections.Generic.IReadOnlyList<string> RegionNames { get; } =
        System.Array.ConvertAll(GlassManagement.Calc.Data.ParamLibrary.Regions.ToArray(),
                                r => r.Name);

    // ---------------- 导航 ----------------
    [RelayCommand] private void Next()
    {
        // 人工给定值已在 OnXxxTextChanged 里写入 Session（字符串 → double 解析），此处只做取值与导航
        ResolveWindStandard();
        _wizard.StepFromBasicNext();
    }

    [RelayCommand] private void Prev() => _wizard.CloseForm();

    // ── 过程参数留痕：ResolveWindStandard 每次执行后把整条链的值留在这里 ──
    // 用户硬规则：任何测试/对拍/报告都必须把过程参数逐项列表，不能只给最终 wk。
    public string ChainTerrain { get; private set; } = "";
    public double ChainW0 { get; private set; }
    public double ChainK1 { get; private set; }
    public double ChainK2 { get; private set; }
    public double ChainZ { get; private set; }
    public double ChainMuZ { get; private set; }
    public double ChainBetaGz { get; private set; }
    public double ChainG { get; private set; }
    public double ChainBeta { get; private set; }
    public double ChainArea { get; private set; }
    public double ChainPsi { get; private set; }
    public double ChainMuS1 { get; private set; }
    public double ChainMuInt { get; private set; }
    public double ChainRaw { get; private set; }
    public double ChainWk { get; private set; }

    /// <summary>过程参数一行（CSV 片段），供扫描/对拍直接拼接。</summary>
    public string ChainCsv()
        => string.Join(",",
            ChainTerrain,
            ChainW0.ToString("0.####"),
            ChainK1.ToString("0.####"),
            ChainK2.ToString("0.####"),
            ChainZ.ToString("0.###"),
            ChainMuZ.ToString("0.#####"),
            ChainBetaGz.ToString("0.#####"),
            ChainG.ToString("0.0"),
            ChainBeta.ToString("0.#####"),
            ChainArea.ToString("0.####"),
            ChainPsi.ToString("0.#####"),
            ChainMuS1.ToString("0.####"),
            ChainMuInt.ToString("0.####"),
            ChainRaw.ToString("0.#####"),
            ChainWk.ToString("0.#####"));

    /// <summary>「查找并定位地区」：在参数库「地区参数」634 行里按关键字定位并选中该行。</summary>
    [RelayCommand] private void FindRegion()
    {
        var hit = GlassManagement.Calc.Data.ParamLibrary.FindRegion(Session.RegionKeyword);
        if (hit is null)
        {
            _wizard.StatusText = $"查找并定位地区：参数库「地区参数」表 634 行内未找到「{Session.RegionKeyword}」";
            return;
        }
        Session.RegionSelected = hit.Name;
        _wizard.StatusText = $"已定位地区：{hit.Name}（50年基本风压={hit.Wind50Pa}Pa={hit.Wind50KPa:0.###}KPa，"
                           + $"抗震烈度={hit.SeismicIntensity}度，αmax={hit.AlphaMax}，极限温差={hit.LimitTempDiff}℃）";
    }
    [RelayCommand] private void PresetWeight()
        => _wizard.StatusText = "自重参数预置（参数库「幕墙自重参数」表按玻璃配置自动取行）：" + Session.DeadLoadCaption;
    [RelayCommand] private void Help()
        => _wizard.StatusText = "帮助：V35 弹出对应参数说明窗口，复刻未接";
    [RelayCommand] private void Explain()
        => _wizard.StatusText = "说明：地震作用效应组合分项/组合系数说明窗口，复刻未接";

    // ---------------- 输入框绑定的字符串镜像（VB6 TextBox 是文本语义） ----------------
    [ObservableProperty] private string _calcLevelText = "100";
    [ObservableProperty] private string _terrainFactorText = "1.0";
    [ObservableProperty] private string _windDirFactorText = "1.0";
    [ObservableProperty] private string _coefExternalValueText = "1.0";
    [ObservableProperty] private string _coefInnerValueText = "0.2";
    [ObservableProperty] private string _windManualKpaText = "1.0";
    [ObservableProperty] private string _bayColLeftText = "1100";
    [ObservableProperty] private string _bayColRightText = "1100";
    [ObservableProperty] private string _bayTransTopText = "1380";
    [ObservableProperty] private string _bayTransBottomText = "1380";
    [ObservableProperty] private string _bayMidTopText = "1350";
    [ObservableProperty] private string _bayMidBottomText = "1350";

    partial void OnCalcLevelTextChanged(string value) { if (TryD(value, out double d)) Session.CalcLevelM = d; }
    partial void OnTerrainFactorTextChanged(string value) { if (TryD(value, out double d)) Session.TerrainFactor = d; }
    partial void OnWindDirFactorTextChanged(string value) { if (TryD(value, out double d)) Session.WindDirFactor = d; }
    partial void OnCoefExternalValueTextChanged(string value) { if (TryD(value, out double d)) Session.CoefExternalManualValue = d; }
    partial void OnCoefInnerValueTextChanged(string value) { if (TryD(value, out double d)) Session.CoefInnerManualValue = d; }
    partial void OnWindManualKpaTextChanged(string value) { if (TryD(value, out double d)) Session.WindManualKpa = d; }
    // 立柱从属宽度 = (左分格宽 + 右分格宽)/2 —— V31 槽数组实证：
    //   slot[42]=1.65、slot[43]=3、slot[44]=A=4.95、slot[47]=μs组合1.101
    //   （左改 2200 时 A=1650×3/1000=4.95，而不是 2200×3/1000=6.6）
    partial void OnBayColLeftTextChanged(string value)
    {
        if (TryD(value, out double d)) { Session.BayColLeft = d; SyncBayWidth(); }
    }
    partial void OnBayColRightTextChanged(string value)
    {
        if (TryD(value, out double d)) { Session.BayColRight = d; SyncBayWidth(); }
    }
    private void SyncBayWidth()
        => Session.ColumnBayWidthMm = (Session.BayColLeft + Session.BayColRight) / 2.0;
    partial void OnBayTransTopTextChanged(string value) { if (TryD(value, out double d)) Session.BayTransTop = d; }
    partial void OnBayTransBottomTextChanged(string value) { if (TryD(value, out double d)) Session.BayTransBottom = d; }
    partial void OnBayMidTopTextChanged(string value) { if (TryD(value, out double d)) Session.BayMidTop = d; }
    partial void OnBayMidBottomTextChanged(string value) { if (TryD(value, out double d)) Session.BayMidBottom = d; }

    private static bool TryD(string s, out double d) => double.TryParse(s, out d);

    // V35 联动：输入框启用状态（IsEnabled 绑这些只读属性，由 Session.PropertyChanged 通知刷新）
    public bool ExternalManualEnabled => Session.CoefExternalManual;
    public bool InnerManualEnabled => Session.CoefInnerManual;
    public bool WindManualEnabled => Session.WindManual;

    /// <summary>诊断/对拍用：按当前会话参数重算风荷载标准值（与点「下一步」完全同一函数）。</summary>
    public void Recalculate() => ResolveWindStandard();

    /// <summary>
    /// 「下一步」时确定风荷载标准值（下游立柱/多跨屏共用）。
    /// 公式已由脱壳 V35 代码 + V31 实机五点验证闭合（docs/V35_COLUMN_FORMULAS.md §13）：
    ///   wk = w0 · βgz(z) · μz(z) · [ μs1·ψ(A) + μs_int ]
    ///   · w0   —— 参数库「地区参数」表 50 年 / 100 年基本风压（查表），下限 0.3 KN/m²（0x4DAA2C）；
    ///   · βgz  —— 1 + 2·g·I10·(z/10)^(−α)，g=2.5（常量表 0x401EB0–0x401F18；尾部代码 0xA71840）；
    ///   · μz    —— C·(z/10)^p（0x511500 原文 + 四类块 0xA702E0/0xA708CF/0xA70EBA/0xA714A9）；
    ///   · ψ(A) —— 1 − 0.2·lg(A)/lg(25)，A = 分格宽 × 立柱计算跨（从属面积折减，GB50009-2012 8.3.4）；
    ///   · μs_int —— 封闭式 0.2 / 0.3 / 0.4 / 0.6，人工给定取输入值（0xA73604 … 0xA73BB2）；
    ///   · 最后按 JGJ102-2003 取不小于 1.0 KN/㎡（0xA6E999 / 0xA6ED7A）。
    /// </summary>
    private void ResolveWindStandard()
    {
        // w0：参数库查表（50 年 / 100 年两列）
        double w0Lib = Session.Return100 ? Session.RegionWind100KPa : Session.RegionWind50KPa;
        if (w0Lib <= 0) w0Lib = Session.RegionWind50KPa;

        if (Session.WindManual)
        {
            Session.WindStandardKpa = Session.WindManualKpa;
            Session.WindStandardRawKpa = Session.WindManualKpa;   // 人工给定即精确值，无取整问题
            // 人工给定取代整条派生链 ⇒ 面板口径同样取该值（两口径只在体型系数项上分岔，
            // 人工给定后已无从区分，V31 行为也是全屏同值）。
            Session.PanelWindKpa = Session.WindManualKpa;
            Session.PanelWindRawKpa = Session.WindManualKpa;
            Session.WindStandardSource = "基本参数输入屏·人工给定";
            Session.WindStandardResolved = true;      // 计算链门禁：第一屏已出结论
            // 过程参数：人工给定直接取代，整条派生链不参与，置空以示区别
            ChainTerrain = "人工给定";
            ChainW0 = 0; ChainK1 = 0; ChainK2 = 0; ChainZ = 0;
            ChainMuZ = 0; ChainBetaGz = 0; ChainBeta = 0;
            ChainArea = 0; ChainPsi = 0; ChainMuS1 = 0; ChainMuInt = 0;
            ChainRaw = 0; ChainWk = Session.WindStandardKpa;
            _wizard.StatusText = $"风荷载标准值 = {Session.WindStandardKpa:0.###} KPa（{Session.WindStandardSource}）";
            return;
        }

        var terrain = Session.TerrainA ? TerrainCategory.A
                    : Session.TerrainB ? TerrainCategory.B
                    : Session.TerrainC ? TerrainCategory.C
                    : TerrainCategory.D;

        // 外部局部体型系数 μs1
        //   GB50009-2012 表8.3.3 封闭式矩形平面房屋墙面：迎风面 1.0、侧面 Sa 区 1.4、Sb 区 1.0、背风面 0.6
        //   → 墙面区 1.0；墙角边区（= 侧面 Sa 区，靠近迎风边、宽度取 min(2H,B)）= 1.4
        //   ★2026-10-24 修正：原写 1.8 是错的（1.8 是屋面角隅/檐口的局部体型系数，不是墙面墙角区）。
        //      V31 实跑取证：外部切「墙角边区」后 wk 1.848→2.455，反解 μs1 = 1.3994 ≈ 1.4。
        double muS1 = Session.CoefExternalCorner ? 1.4
                    : Session.CoefExternalManual ? Session.CoefExternalManualValue
                    : 1.0;

        // 内部局部体型系数 μsi
        //   GB50009-2012 8.3.5：
        //     1 封闭式建筑物：取 -0.2 或 0.2（绝对值，**不乘** μs1）
        //     2 仅一面墙有主导洞口：0.02<开洞率≤0.10 → 0.4μs1；0.10<开洞率≤0.30 → 0.6μs1；>0.30 → 0.8μs1
        //     3 其他情况：按开放式建筑物取值（界面「其它情况(人工给定)」，绝对值）
        //   ★2026-10-24 修正两处：
        //     ① 档位原写 0.3/0.4/0.6（整体低一档）→ 正确 0.4/0.6/0.8；
        //        V35 与 V31 该链完全一致（五档 0.2/0.3/0.4/0.6/0.8），
        //        V31 三个开洞率档实测反解 0.4000/0.6000/0.8000（μs1=1.0，ψ=0.9258，基准 wk=1.848）。
        //     ② 原实现一律当绝对值相加 → 开洞率档必须乘 μs1（规范写的就是 0.4μs1 这种倍率）。
        //        实测：外部人工给定 μs1=1.5 + 第一档 → V31 wk=3.265，
        //        对应 μsi=0.4×1.5=0.6（若按绝对 0.4 只有 2.936）。
        bool innerRatio = Session.CoefInnerOneWall010 || Session.CoefInnerOneWall030 || Session.CoefInnerOneWallGt030;
        double innerFactor = Session.CoefInnerOneWallGt030 ? 0.8
                           : Session.CoefInnerOneWall030 ? 0.6
                           : Session.CoefInnerOneWall010 ? 0.4
                           : 0.2;
        // ★ V35 在倍率乘法后立即 rtcRound 3 位（0xA73361 / 0xA73456 的 push 3），
        //   复刻原先不取整；V31 slot[47] 全部为 3 位小数（1.126 / 1.326 / 1.526 / 1.726）可证。
        // ★ 封闭式内部体型系数随荷载规范变：国标 0.2 / 广东 0.3（V31 实测 slot[47] 1.126→1.226）。
        //   0.4/0.6/0.8 三个开洞率档两规范相同（实测均为 1.326/1.526/1.726），只有封闭式这一档变。
        double muEnclosed = Session.IsGuangdongLoad ? 0.3 : 0.2;
        double muInt = Session.CoefInnerManual ? Session.CoefInnerManualValue
                     : innerRatio ? WindLoadEngine.RoundLikeV35(innerFactor * muS1, WindLoadEngine.DigitsMuS)
                     : muEnclosed;

        // 从属面积 A = 立柱从属宽度 × 立柱计算跨（m²）
        //   ★V31 槽数组实证（slot[42]=1.65、slot[43]=3、slot[44]=4.95、slot[47]=μs组合）：
        //     立柱从属宽度 = (立柱左分格宽度 + 立柱右分格宽度)/2，
        //     即 A = (1100+1100)/2/1000 × 3.0 = 3.3（基准，与 V35 一致）；
        //     把左分格宽改成 2200 时 V31 得 A=(2200+1100)/2/1000×3.0 = 4.95，而不是 6.6。
        //   此前复刻只用「立柱左分格宽度」，属于取值错误（改了左分格宽后 A 会翻倍偏差）。
        double bayAvgM = (Math.Max(0.1, Session.BayColLeft) + Math.Max(0.1, Session.BayColRight)) / 2.0 / 1000.0;
        double area = bayAvgM * Math.Max(0.1, Session.ColumnSpanMm / 1000.0);

        double z = Session.CalcLevelM > 0 ? Session.CalcLevelM : 100.0;

        // K1 地形修正系数 / K2 风向影响系数（GB55001-2021 4.6.6 / 4.6.7，一般各取 1.0）
        double k1 = Session.TerrainFactor > 0 ? Session.TerrainFactor : 1.0;
        double k2 = Session.WindDirFactor > 0 ? Session.WindDirFactor : 1.0;

        // 峰值因子 g：国标 2.5 / 广东 3.0（V31 实测 β 由 1.4956 → 1.5947）
        //   ★ DBJ15-101-2014 7.4.6 门槛：只有「基本风压 ≥ 0.5 kN/m² 地区」才提到 3.0。
        //     先把 w0 落到 Session 上，PeakFactorG 据此判断（否则复刻在 w0<0.5 时会多算一格）。
        //     V31 实测：北京 w0=0.45 + 广东档 → β=1.4956（不提）、μs合=1.226（内压照旧 0.3）；wk=1.646。
        Session.BasicWindW0 = w0Lib;
        double peakFactor = Session.PeakFactorG;
        double raw = WindLoadEngine.WkRaw(w0Lib, terrain, z, muS1, muInt, area,
                                          applyFloor: false, k1: k1, k2: k2, peakFactor: peakFactor);
        // 「风荷载标准值取值」三选一（V35 第一页）：
        //   按规范不小于1KPa → max(算值, 1.0)（0xA6E999/0xA6ED7A）
        //   按实际计算结果   → 直接用算值，**不加 1.0 下限**（此前无判断，等于该单选未接）
        //   人工给定         → 上面分支已提前返回
        double wkRawFinal = Session.WindByCalcResult
            ? raw
            : Math.Max(raw, WindLoadEngine.WindStandardFloorKpa);

        // ★ 三个值分工（2026-10-24 位数对齐）：
        //   WindStandardKpa      = round3，显示「风荷载标准值为:1.848KPa」
        //   WindStandardRawKpa   = 未取整（含 K1/K2、含下限），留痕用
        //   WindStandardRawMainKpa = 未取整且**不含 K1/K2**，立柱链用它重乘 K
        //     原因：V31 是先 round3 出 qk、再乘 K 再 round3（K1=1.2 场景可判别：
        //     round3(2.033×1.2)=2.44 ✅，而 round3(2.0329×1.2)=2.439 ❌）。
        Session.WindStandardRawKpa = wkRawFinal;
        Session.WindStandardRawMainKpa = k1 * k2 > 0 ? wkRawFinal / (k1 * k2) : wkRawFinal;
        double wk = WindLoadEngine.RoundLikeV35(wkRawFinal, WindLoadEngine.DigitsWk);

        Session.WindStandardKpa = wk;
        Session.WindStandardResolved = true;          // 计算链门禁：第一屏已出结论

        // ── 面板口径 wk（玻璃屏 / 胶类屏共用；V31 0xA7D00C–0xA7D07C 的乘数链）──
        //   wk_panel = w0 · βgz · 地形 · μz · (μsl + μs内压) · K1 · K2
        //   ★ 与支承口径的唯一差别：这里用**局部体型系数 μsl = 1.0 且不做 ψ 折减**，
        //     支承口径用 μs1·ψ(A)（A = 分格面积，ψ<1 故支承 wk 更小）。
        //
        //   实测依据（六组参数 × 六屏，local-data/recon/wk_scan.py）：
        //     立柱=横梁=连接=埋件 恒等；玻璃=胶类 恒等且恒大于前者。
        //     G1: 支承 1.514 / 面板 1.598；1.598/1.514 = 1.2/1.1369，1.1369 = 0.9369+0.2 ✓
        //     G5: 支承 1.970 / 面板 2.055；2.055/1.300 = 1.5808 = P，1.97/1.5808 = 1.2462 = 0.9462+0.3 ✓
        //
        //   ⚠ 更正：早前把「玻璃屏不含 μs1·ψ」当成 V31 缺陷/漏乘，属误判。
        //     面板取 μsl=1.0 不折减本身就是 V31 的口径，本轮已按正向建模。
        double wkPanelRaw = w0Lib
                            * WindLoadEngine.BetaGz(terrain, z, peakFactor)
                            * WindLoadEngine.MuZ(terrain, z)
                            * (CalcSession.PanelShapeCoef + muInt)
                            * k1 * k2;
        // 下限与支承同口径：按规范不小于 1.0KPa（G4 人工 0.6 时两屏都撞 1.0，实测确认）
        double wkPanelRawFinal = Session.WindByCalcResult
            ? wkPanelRaw
            : Math.Max(wkPanelRaw, WindLoadEngine.WindStandardFloorKpa);
        Session.PanelWindRawKpa = wkPanelRawFinal;
        Session.PanelWindKpa = WindLoadEngine.RoundLikeV35(wkPanelRawFinal, WindLoadEngine.DigitsWk);

        // ── 过程参数留痕（用户要求：所有过程参数都要能列表，不能只给结果）──
        ChainTerrain = terrain.ToString();
        ChainW0 = w0Lib; ChainK1 = k1; ChainK2 = k2; ChainZ = z;
        ChainMuZ = WindLoadEngine.MuZ(terrain, z);
        ChainBetaGz = WindLoadEngine.BetaGz(terrain, z, peakFactor);
        ChainBeta = WindLoadEngine.Beta(terrain, z, peakFactor);
        ChainG = peakFactor;
        ChainArea = area;
        ChainPsi = WindLoadEngine.Psi(area);
        ChainMuS1 = muS1; ChainMuInt = muInt;
        ChainRaw = raw; ChainWk = wk;
        Session.WindStandardSource = string.Format(
            "按规范不小于1KPa｜w0={0:0.###}KPa（参数库{1}·{2}）｜{3}类 z={4:0}m μz={5:0.####} g={15:0.0} βgz={6:0.####}"
            + "｜μs1={7:0.###}·ψ({8:0.##}m²)={9:0.####} μs内={10:0.##}｜K1={13:0.###} K2={14:0.###}｜wk={11:0.####}{12}",
            w0Lib, Session.RegionSelected, Session.Return100 ? "100年" : "50年",
            terrain, z,
            WindLoadEngine.MuZ(terrain, z), WindLoadEngine.BetaGz(terrain, z, peakFactor),
            muS1, area, muS1 * WindLoadEngine.Psi(area), muInt, raw,
            wk > raw ? "，已按 JGJ102 下限 1.0 取值" : "", k1, k2, peakFactor);

        _wizard.StatusText = $"风荷载标准值 = {Session.WindStandardKpa:0.###} KPa（{Session.WindStandardSource}）";
    }

    /// <summary>基本风压下限 0.3 KN/m²（=0.3 KPa）。V35 原文 0x4daa2c。</summary>
    public const double WindPressFloorKpa = 0.3;
    /// <summary>幕墙风荷载标准值下限 1.0 KN/㎡（JGJ102-2003）。V35 原文 0x4dd934。</summary>
    public const double WindStandardFloorKpa = 1.0;
}
