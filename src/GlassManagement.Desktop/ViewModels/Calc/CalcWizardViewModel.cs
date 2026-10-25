using System;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassManagement.Calc.Book;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>
/// 计算向导总 ViewModel：持有共享会话 CalcSession 与当前屏内容，
/// 负责「玻璃幕墙→全隐框 → 基本参数输入 → 立柱计算 → 多跨数据输入 → 预演」
/// 的整屏替换式导航（V35 实测：多跨分支点「下一步」= 整屏替换，不是 Frame 显隐）。
/// 非链上菜单仍走静态窗体（View 实例直接作为内容）。
/// </summary>
public partial class CalcWizardViewModel : ObservableObject
{
    public static CalcWizardViewModel Default { get; } = new();

    public CalcSession Session { get; } = CalcSession.Default;

    [ObservableProperty] private object? _currentContent;
    [ObservableProperty] private string _statusText = "就绪";

    private BasicParamsViewModel? _basic;
    private ColumnCalcViewModel? _column;
    private MultiSpanViewModel? _multi;
    private BeamCalcViewModel? _beam;
    private UnitBeamCalcViewModel? _unitBeam;
    private GlassCalcViewModel? _glass;
    private ConnectCalcViewModel? _connect;
    private EmbedCalcViewModel? _embed;
    private SealantCalcViewModel? _sealant;
    private CalcBookPanelViewModel? _bookPanel;

    /// <summary>菜单点击入口。返回 true 表示已走向导链；false 表示调用方应走静态窗体。</summary>
    public bool OpenMenuPath(string top, string? sub)
    {
        // ★C5：任何计算都由「顶级菜单 → 子项」发起。先记录路径并重置计算链，
        //   这样切换类型/子项就等于换了一条第一页，下游旧结果与门禁位一律作废。
        Session.EnterMenu(top, sub);

        // 玻璃幕墙 → 全隐框：V35 向导第一屏 = 幕墙计算-基本参数输入（907×612）
        if (top == "玻璃幕墙" && (string.IsNullOrWhiteSpace(sub) || sub == "全隐框"))
        {
            GoBasic();
            return true;
        }
        return false;
    }

    public void OpenStatic(object view, string title)
    {
        CurrentContent = view;
        StatusText = $"{title}  [静态复刻]";
    }

    // ---------------- 向导导航（整屏替换） ----------------

    [RelayCommand]
    public void GoBasic()
    {
        _basic ??= new BasicParamsViewModel(Session, this);
        MirrorStatus(_basic);
        CurrentContent = _basic;
        StatusText = "向导 1/3：幕墙计算-基本参数输入（参数已写入共享会话）";
    }

    [RelayCommand]
    public void GoColumnCalc()
    {
        _column ??= new ColumnCalcViewModel(Session, this);
        MirrorStatus(_column);
        CurrentContent = _column;
        StatusText = $"向导 2/3：幕墙计算-立柱计算（沿用基本参数：wk={Session.WindStandardKpa:0.###}KPa"
                   + $"｜{Session.WindStandardSource}）";
    }

    /// <summary>向导 3/3：横梁计算屏（MVVM 版，计算在 HorizontalBeamEngine）。</summary>
    [RelayCommand]
    public void GoBeamCalc()
    {
        // 横梁屏有两套模板：单元式（上/下横框 + 可选中横框）/ 构件式（单根横梁）
        if (Session.WallStructureIsUnit)
        {
            _beam?.ResetModeSwitches();     // 切走时复位构件式专属开关（V31 有残留勾选的 bug）
            _unitBeam ??= new UnitBeamCalcViewModel(Session, this);
            MirrorStatus(_unitBeam);
            CurrentContent = _unitBeam;
            StatusText = $"向导 3/3：幕墙计算-横梁计算（单元式｜跨度={Session.BeamSpanMm:0.###}mm"
                       + $"｜上分格高={Session.BeamUpperGridHeightMm:0.###}mm｜wk={Session.WindStandardKpa:0.###}KPa"
                       + $"｜αmax={Session.AlphaMax:0.###}）";
            return;
        }
        _beam ??= new BeamCalcViewModel(Session, this);
        MirrorStatus(_beam);
        CurrentContent = _beam;
        StatusText = $"向导 3/3：幕墙计算-横梁计算（跨度={Session.BeamSpanMm:0.###}mm"
                   + $"｜上分格高={Session.BeamUpperGridHeightMm:0.###}mm｜wk={Session.WindStandardKpa:0.###}KPa"
                   + $"｜αmax={Session.AlphaMax:0.###}｜参数库 {GlassManagement.Calc.Data.ParamLibrary.SourceNote}）";
    }

    public void GoMultiSpan()
    {
        _multi ??= new MultiSpanViewModel(Session, this);
        MirrorStatus(_multi);
        CurrentContent = _multi;
        StatusText = $"向导 3/3：多跨数据输入（总跨数={Session.TotalSpans} 跨长A={Session.SegmentAMm} 跨长L={Session.SegmentLMm}，"
                   + $"风荷载={Session.WindStandardKpa}KPa[{Session.WindStandardSource}]）";
    }

    /// <summary>
    /// 诊断入口（--selftest=preview）：从第一屏起走完整向导 ⇒ 基本参数 →「下一步」→ 立柱屏 →「预演」。
    /// 用于自动化回归验证「预演能显示结果」与 V31 数值对拍，不改变任何业务默认值。
    /// </summary>
    public void SelfTestColumnPreview()
    {
        Session.EnterMenu("玻璃幕墙", "全隐框");   // 与真实菜单点击同一路径（会重置计算链）
        GoBasic();                            // 第一屏
        _basic?.NextCommand.Execute(null);    // 真实「下一步」：内含 ResolveWindStandard + 导航到立柱屏
        _column?.PreviewCommand.Execute(null);
    }

    /// <summary>
    /// 诊断入口（--selftest=beam）：第一屏 →「下一步」→ 立柱屏 →「下一步」→ 横梁屏 →「预演」。
    /// 走的是与真实按钮完全相同的命令链，不覆盖任何参数。
    /// </summary>
    public void SelfTestBeamPreview()
    {
        Session.EnterMenu("玻璃幕墙", "全隐框");
        GoBasic();
        _basic?.NextCommand.Execute(null);     // → 立柱屏（内含 ResolveWindStandard）
        _column?.NextCommand.Execute(null);    // → 横梁屏
        _beam?.PreviewCommand.Execute(null);
    }

    /// <summary>
    /// 诊断入口（--selftest=unit-beam）：第一屏 →「下一步」→ 立柱 →「下一步」→ 单元式横梁 → 预演。
    /// 与 V31「单元式·无中横」17 个数字对拍。
    /// </summary>
    public void SelfTestUnitBeamPreview()
    {
        Session.EnterMenu("玻璃幕墙", "全隐框");
        Session.WallStructureIsUnit = true;    // 第一页「幕墙结构」= 单元式
        GoBasic();
        _basic?.NextCommand.Execute(null);
        _column?.NextCommand.Execute(null);
        _unitBeam?.PreviewCommand.Execute(null);
    }

    /// <summary>把单元式横梁屏预演结果拼成纯文本。</summary>
    public string DumpUnitBeamPreviewText()
        => _unitBeam is null ? "" : string.Join("\n", _unitBeam.Results.Select(r => r.Text));

    /// <summary>把横梁屏预演结果拼成纯文本（对拍/落盘用）。</summary>
    public string DumpBeamPreviewText()
        => _beam is null ? "" : string.Join("\n", _beam.Results.Select(r => r.Text));

    /// <summary>
    /// 反面对照（--selftest=column-only）：直接进立柱屏并预演，**故意跳过第一屏**。
    /// 用于验证计算链门禁是否生效——预期右侧只出现「尚未完成第一屏」的拒绝提示，不产出任何数值。
    /// </summary>
    public void SelfTestColumnOnly()
    {
        GoColumnCalc();
        _column?.PreviewCommand.Execute(null);
    }

    /// <summary>
    /// 第一页选项扫描（--selftest=sweep）：基准配置 + **每次只改一个选项**，各自重算 wk 并预演。
    /// 基准＝上海/50年/标高100/B类/墙面区/封闭式/按规范≥1kPa/二级γ0=1.0/立柱跨3000/立柱左分格1100/6063A-T5/60-80系列。
    /// 输出 CSV：场景,wk,抗弯矩预选,惯性矩预选,挠度,抗弯,抗剪。
    /// 用途：验证第一页每个单选/下拉/输入是否真的进到第二页计算（改了却不变＝该项未接）。
    /// </summary>
    public string SweepBasicPage()
    {
        GoBasic();
        var basic = _basic!;
        _column ??= new ColumnCalcViewModel(Session, this);
        var col = _column;

        var sb = new System.Text.StringBuilder();
        // 用户硬规则：过程参数必须逐项列表（不只给最终 wk）。
        // 前半段＝第二屏结果，后半段＝第一屏整条取值链（BasicParamsViewModel.ChainCsv）。
        sb.AppendLine("场景,wk_KPa,抗弯矩预选_mm3,惯性矩预选_mm4,挠度_mm,抗弯_MPa,抗剪_MPa,"
                    + "地形,w0,K1,K2,z,μz,βgz,g,β,面积A,ψ,μs1,μsi,wk算值,wk取用,w0查得,地区名,命中行,"
                    + "左框抗弯,右框抗弯,左框抗剪,右框抗剪");


        void Baseline()
        {
            Session.TerrainB = true;
            Session.TerrainA = Session.TerrainC = Session.TerrainD = false;
            Session.CoefExternalWall = true;
            Session.CoefExternalCorner = Session.CoefExternalManual = false;
            Session.CoefExternalManualValue = 1.0;
            Session.CoefInnerEnclosed = true;
            Session.CoefInnerOneWall010 = Session.CoefInnerOneWall030 = false;
            Session.CoefInnerOneWallGt030 = Session.CoefInnerManual = false;
            Session.CoefInnerManualValue = 0.2;
            Session.Return100 = false;
            Session.WindByCodeMin = true;   // 基准＝按规范不小于1KPa（互斥会清另两项）
            Session.GradeWhenNoInsert = "2级";
            Session.RegionSelected = "上海";
            Session.CalcLevelM = 100;
            // ★ 必须复位修正系数与人工给定：否则后一个场景会继承前一个的 K1/K2，
            //   曾经因此把 K2=0.8 算成 1.8477×1.2×0.8=1.7738，看着像产品 bug，其实是扫描脚本没清零。
            Session.TerrainFactor = 1.0;
            Session.WindDirFactor = 1.0;
            Session.WindManual = false;
            Session.WindManualKpa = 1.0;
            Session.GlassMono = true;          // 同上：玻璃配置也必须复位，否则会串到后面的 case
            Session.ColumnSpanMm = 3000;
            Session.ColumnBayWidthMm = 1100;
            Session.ColumnMaterial = "6063A-T5";
            Session.ColumnCode = "60/80系列";
            Session.StructFrame = true;        // 幕墙结构也必须复位，否则单元式会串到后面的 case
            Session.UnitLeftCode = "";         // 单元式左右框型材复位（空 = 沿用主型材）
            Session.UnitRightCode = "";
            Session.LoadStandardIndex = 0;     // 荷载规范复位为「全国」，否则广东档会串到后面的 case
            Session.WindStandardResolved = false;
        }

        static string Num(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var m = System.Text.RegularExpressions.Regex.Match(text!, @"[：:]\s*(-?[0-9]+(?:\.[0-9]+)?)");
            return m.Success ? m.Groups[1].Value : "";
        }

        void Run(string tag)
        {
            basic.Recalculate();               // 走第一屏真实取值逻辑（与点「下一步」同一函数）
            col.PreviewCommand.Execute(null);  // 第二屏预演
            // 同时支持构件式与单元式两套标签：
            //   构件式「实际挠度计算值：34.574mm」 / 单元式「单 元组合的挠度计算值:17.279mm」
            //   构件式「抗弯强度计算值:159.95MPa」 / 单元式「左单元框抗弯强度计算值:79.975MPa」
            // 先按 StartsWith 精确匹配，未命中再按 Contains 兜底（单元式标签前缀不同）。
            string Pick(params string[] prefixes)
            {
                foreach (var p in prefixes)
                {
                    var hit = System.Linq.Enumerable.FirstOrDefault(col.Results, r => r.Text.StartsWith(p))
                           ?? System.Linq.Enumerable.FirstOrDefault(col.Results, r => r.Text.Contains(p));
                    if (hit is not null) return Num(hit.Text);
                }
                return "";
            }

            sb.Append(tag).Append(',')
              .Append(Session.WindStandardKpa.ToString("0.####")).Append(',')
              .Append(Pick("抗弯矩预选")).Append(',')
              .Append(Pick("惯性矩预选")).Append(',')
              .Append(Pick("实际挠度计算值", "组合的挠度计算值")).Append(',')
              .Append(Pick("抗弯强度计算值")).Append(',')
              .Append(Pick("抗剪强度计算值")).Append(',')
              .Append(basic.ChainCsv()).Append(',')                             // 过程参数链
              .Append(Session.RegionWind50KPa.ToString("0.####")).Append(',')   // 诊断：w0 取到的值
              .Append(Session.RegionSelected ?? "<null>").Append(',')           // 诊断：RegionSelected 原文
              .Append(Session.CurrentRegion?.Name ?? "<null>").Append(',')       // 诊断：命中的地区行
              // 单元式专用 4 列（非单元式留空）：左/右框抗弯、左/右框抗剪
              .Append(Pick("左单元框抗弯强度计算值")).Append(',')
              .Append(Pick("右单元框抗弯强度计算值")).Append(',')
              .Append(Pick("左单元框抗剪强度计算值")).Append(',')
              .Append(Pick("右单元框抗剪强度计算值"))
              .AppendLine();
        }

        var cases = new (string Tag, Action Apply)[]
        {
            ("基准 B类/墙面区/封闭式/50年/二级", () => { }),
            ("地面类型 A类", () => { Session.TerrainA = true; }),
            ("地面类型 C类", () => { Session.TerrainC = true; }),
            ("地面类型 D类", () => { Session.TerrainD = true; }),
            ("外部体型 墙角边区", () => { Session.CoefExternalCorner = true; }),
            ("外部体型 人工给定1.5", () => { Session.CoefExternalManual = true; Session.CoefExternalManualValue = 1.5; }),
            ("内部体型 0.02<x≤0.10(0.4μs1)", () => { Session.CoefInnerOneWall010 = true; }),
            ("内部体型 0.10<x≤0.30(0.6μs1)", () => { Session.CoefInnerOneWall030 = true; }),
            ("内部体型 x>0.30(0.8μs1)", () => { Session.CoefInnerOneWallGt030 = true; }),
            ("内部体型 人工给定0(其它情况)", () => { Session.CoefInnerManual = true; Session.CoefInnerManualValue = 0.0; }),
            ("重现期 100年", () => { Session.Return100 = true; }),
            // 低风压组合：让算值降到 1.0 以下，才能验证「按实际计算」是否真的不加下限
            ("低风压D类30m 按规范(应抬到1.0)", () => { Session.RegionSelected = "重庆奉节"; Session.TerrainD = true; Session.CalcLevelM = 30; }),
            ("低风压D类30m 按实际(应低于1.0)", () => { Session.RegionSelected = "重庆奉节"; Session.TerrainD = true; Session.CalcLevelM = 30; Session.WindByCalcResult = true; }),
            ("γ0 一级(1.1)", () => { Session.GradeWhenNoInsert = "1级"; }),
            ("γ0 三级(0.9)", () => { Session.GradeWhenNoInsert = "3级"; }),
            ("地区 北京", () => { Session.RegionSelected = "北京"; }),
            ("地区 广州", () => { Session.RegionSelected = "广州"; }),
            // 诊断：参数库里叫「广东广州」，上面写「广州」若查不到会静默回落到 w0=0.3 下限。
            ("地区 广东广州(全名)", () => { Session.RegionSelected = "广东广州"; }),
            ("计算标高 50m", () => { Session.CalcLevelM = 50; }),
            ("计算标高 150m", () => { Session.CalcLevelM = 150; }),
            // 以下四项对应 2026-10-24 新增的 V31 实测（basic-cases2）：
            //   V31 实测 K1=0.75→1.386、K1=1.2→2.218、K2=0.8→1.478、人工给定0.5→0.5
            ("K1地形修正0.75", () => { Session.TerrainFactor = 0.75; }),
            ("K1地形修正1.2", () => { Session.TerrainFactor = 1.2; }),
            ("K2风向修正0.8", () => { Session.WindDirFactor = 0.8; }),
            ("风荷载 人工给定0.5", () => { Session.WindManual = true; Session.WindManualKpa = 0.5; }),
            // 玻璃配置：V31 实测（basic-cases）显示"中空/夹层"对立柱屏数值零影响，
            // 这里加两档用于验证复刻是否也零影响（若变了说明复刻把自重错接进了立柱轴力）。
            ("单元式幕墙(6063-T5)", () => { Session.StructUnit = true; Session.ColumnMaterial = "6063-T5"; }),
            // 2026-10-24 反推：V31 单元式那次左右框其实是**两个不同型材**
            //   （枚举 95 个型材 × 95 对，S 比 1.2578/1.3851 与实测 1.2580/1.3852 命中，err=0.0002）
            ("单元式 左60/100 右60/80", () => { Session.StructUnit = true; Session.ColumnMaterial = "6063-T5";
                Session.UnitLeftCode = "60/100系列"; Session.UnitRightCode = "60/80系列"; }),
            // 反序自证：左右对调后 σ/τ 必须互换（若实现里写死了 ratio=0.5 或把左框当主型材，这条会失败）
            ("单元式 左60/80 右60/100", () => { Session.StructUnit = true; Session.ColumnMaterial = "6063-T5";
                Session.UnitLeftCode = "60/80系列"; Session.UnitRightCode = "60/100系列"; }),
            ("广东低矮(≤45m)", () => { Session.LoadStandardIndex = 1; }),
            ("广东高层(>45m)", () => { Session.LoadStandardIndex = 2; }),
            // 2026-10-24 四面对拍新增：DBJ15-101-2014 7.4.6 只在 w0≥0.5 才把 g 提到 3.0。
            //   V31 实测（v31_gd_threshold.csv）：北京 w0=0.45 + 广东档 → β=1.4956（不提 g）、μs合=1.226（内压照旧 0.3）。
            //   低矮/高层两档在 60m/100m 全部同值，差异只在文案。
            ("广东低矮+北京0.45", () => { Session.RegionSelected = "北京"; Session.LoadStandardIndex = 1; }),
            ("广东高层+北京0.45", () => { Session.RegionSelected = "北京"; Session.LoadStandardIndex = 2; }),
            ("广东低矮+60m", () => { Session.LoadStandardIndex = 1; Session.CalcLevelM = 60; }),
            ("广东高层+60m", () => { Session.LoadStandardIndex = 2; Session.CalcLevelM = 60; }),
            ("玻璃_中空", () => { Session.GlassInsulated = true; }),
            ("玻璃_双中空", () => { Session.GlassDoubleInsul = true; }),
        };

        foreach (var c in cases)
        {
            Baseline();
            c.Apply();
            Run(c.Tag);
        }
        return sb.ToString();
    }

    /// <summary>
    /// 第一页 40 案全量扫描（--selftest=basic40）。
    /// <para>★ 与 V31 侧 <c>v31_attach.ps1</c> 的 <c>basicsweep</c> 一一对应（同名同序），
    /// 输出格式也相同（<c>|</c> 分隔），可直接逐位 diff。</para>
    /// <para>覆盖：重现期 / 雪荷载 / 地形 A·B·C·D / 外部体型 3+1 / 内部体型 4+1 /
    /// K1 / K2 / 标高 / 风荷载取值 3 / 荷载规范 3 / 场地 5 / 抗震 4 / 玻璃 6 / 从属宽。</para>
    /// </summary>
    public string SweepBasic40()
    {
        GoBasic();
        var basic = _basic!;
        _column ??= new ColumnCalcViewModel(Session, this);
        var col = _column;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("场景|wk|抗弯预选|惯性矩预选|挠度|限值|抗弯|抗剪");

        void Baseline()
        {
            // ── 与 V31 basicsweep 的 $BASE_B 同序同义 ──
            Session.SiteClassSelected = "Ⅱ类场地";
            Session.LoadStandardIndex = 0;
            Session.SnowSensitive = false;
            Session.Return50 = true; Session.Return100 = false;
            Session.TerrainB = true;
            Session.TerrainA = Session.TerrainC = Session.TerrainD = false;
            Session.CoefExternalManual = true;      // V31 基准就是「人工给定 1.0」
            Session.CoefExternalWall = Session.CoefExternalCorner = false;
            Session.CoefExternalManualValue = 1.0;
            Session.CoefInnerEnclosed = true;
            Session.CoefInnerOneWall010 = Session.CoefInnerOneWall030 = false;
            Session.CoefInnerOneWallGt030 = Session.CoefInnerManual = false;
            Session.CoefInnerManualValue = 0.2;
            Session.TerrainFactor = 1.0;
            Session.WindDirFactor = 1.0;
            Session.StructFrame = true;
            Session.WindByCodeMin = true;
            Session.WindManual = false; Session.WindManualKpa = 1.0;
            Session.SeismicStandard = true;
            Session.SeismicKey = Session.SeismicSpecial = Session.SeismicModerate = false;
            Session.GlassMono = true;
            Session.CalcLevelM = 100;
            Session.ColumnBayWidthMm = 1100;
            Session.BayColLeft = 1100;
            Session.BayColRight = 1100;
            Session.ColumnSpanMm = 3000;
            Session.RegionSelected = "上海";
            Session.ColumnMaterial = "6063A-T5";
            Session.ColumnCode = "60/80系列";
            Session.UnitLeftCode = ""; Session.UnitRightCode = "";
            Session.WindStandardResolved = false;
        }

        static string Num(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var m = System.Text.RegularExpressions.Regex.Match(text!, @"[：:]\s*(-?[0-9]+(?:\.[0-9]+)?)");
            return m.Success ? m.Groups[1].Value : "";
        }

        void Run(string tag)
        {
            basic.Recalculate();
            col.PreviewCommand.Execute(null);

            string Pick(params string[] prefixes)
            {
                foreach (var p in prefixes)
                {
                    var hit = System.Linq.Enumerable.FirstOrDefault(col.Results, r => r.Text.StartsWith(p))
                           ?? System.Linq.Enumerable.FirstOrDefault(col.Results, r => r.Text.Contains(p));
                    if (hit is not null) return Num(hit.Text);
                }
                return "";
            }

            sb.Append(tag).Append('|')
              .Append(Session.WindStandardKpa.ToString("0.000")).Append('|')
              .Append(Pick("抗弯矩预选")).Append('|')
              .Append(Pick("惯性矩预选")).Append('|')
              .Append(Pick("实际挠度计算值", "组合的挠度计算值")).Append('|')
              .Append(Pick("规范要求的挠度值")).Append('|')
              .Append(Pick("抗弯强度计算值")).Append('|')
              .Append(Pick("抗剪强度计算值"))
              .AppendLine();
        }

        var cases = new (string Tag, Action Apply)[]
        {
            ("B01_基准",                 () => { }),
            ("B02_重现期100年",          () => { Session.Return100 = true; }),
            ("B03_雪荷载勾选",           () => { Session.SnowSensitive = true; }),
            ("B04_地形A",                () => { Session.TerrainA = true; }),
            ("B05_地形C",                () => { Session.TerrainC = true; }),
            ("B06_地形D",                () => { Session.TerrainD = true; }),
            ("B07_外部墙面区",           () => { Session.CoefExternalWall = true; }),
            ("B08_外部墙角边区",         () => { Session.CoefExternalCorner = true; }),
            ("B09_外部人工1.4",          () => { Session.CoefExternalManual = true; Session.CoefExternalManualValue = 1.4; }),
            ("B10_外部人工0.6",          () => { Session.CoefExternalManual = true; Session.CoefExternalManualValue = 0.6; }),
            ("B11_内部0.02开孔",         () => { Session.CoefInnerOneWall010 = true; }),
            ("B12_内部0.10开孔",         () => { Session.CoefInnerOneWall030 = true; }),
            ("B13_内部0.30开孔",         () => { Session.CoefInnerOneWallGt030 = true; }),
            // ★ V31 实测（basic40）：第一页「其它情况(人工给定)」**没有输入框**，
            //   选中后 μsi 仍取默认 0.2 → 与封闭式完全等价（wk 1.848、μs合 1.126，与 B01 逐位同）。
            //   复刻此前用 0.0 → wk 掉到 1.520，已按 V31 改为保持默认值。
            ("B14_内部其它人工",         () => { Session.CoefInnerManual = true; }),
            ("B15_地形修正1.2",          () => { Session.TerrainFactor = 1.2; }),
            ("B16_地形修正0.8",          () => { Session.TerrainFactor = 0.8; }),
            ("B17_风向影响1.1",          () => { Session.WindDirFactor = 1.1; }),
            ("B18_风向影响0.9",          () => { Session.WindDirFactor = 0.9; }),
            ("B19_标高50",               () => { Session.CalcLevelM = 50; }),
            ("B20_标高200",              () => { Session.CalcLevelM = 200; }),
            ("B21_标高300",              () => { Session.CalcLevelM = 300; }),
            ("B22_风取值按实际计算",     () => { Session.WindByCalcResult = true; }),
            ("B23_风取值人工2.0",        () => { Session.WindManual = true; Session.WindManualKpa = 2.0; }),
            ("B24_荷规广东低矮",         () => { Session.LoadStandardIndex = 1; }),
            ("B25_荷规广东高层",         () => { Session.LoadStandardIndex = 2; }),
            ("B26_场地I0",               () => { Session.SiteClassSelected = "Ⅰ0类场地"; }),
            ("B27_场地I1",               () => { Session.SiteClassSelected = "Ⅰ1类场地"; }),
            ("B28_场地III",              () => { Session.SiteClassSelected = "Ⅲ类场地"; }),
            ("B29_场地IV",               () => { Session.SiteClassSelected = "Ⅳ类场地"; }),
            ("B30_抗震重点",             () => { Session.SeismicKey = true; }),
            ("B31_抗震特殊",             () => { Session.SeismicSpecial = true; }),
            ("B32_抗震适度",             () => { Session.SeismicModerate = true; }),
            ("B33_玻璃中空",             () => { Session.GlassInsulated = true; }),
            ("B34_玻璃夹层",             () => { Session.GlassLaminated = true; }),
            ("B35_玻璃中空夹层",         () => { Session.GlassInsulLam = true; }),
            ("B36_玻璃双中空",           () => { Session.GlassDoubleInsul = true; }),
            ("B37_玻璃双夹层中空",       () => { Session.GlassDoubleLam = true; }),
            // ★ V31 实测（basic40 槽值）：改第一页「龙骨间距·左分格 1350」后
            //   slot[42] 从属宽 = (1350+1100)/2 = 1225、slot[44] A = 3.675、μs合 = 1.119
            //   → 立柱从属宽 = (左分格 + 右分格)/2，不是直接用输入值。
            //   输入 1500 同理得 (1500+1100)/2 = 1300、A = 3.9。
            ("B38_从属宽1350",           () => { Session.BayColLeft = 1350; Session.BayColRight = 1100; Session.ColumnBayWidthMm = 1225; }),
            ("B39_从属宽1500",           () => { Session.BayColLeft = 1500; Session.BayColRight = 1100; Session.ColumnBayWidthMm = 1300; }),
            ("B40_基准复跑",             () => { }),
        };

        foreach (var c in cases)
        {
            Baseline();
            c.Apply();
            Run(c.Tag);
        }
        return sb.ToString();
    }

    /// <summary>诊断（--selftest=param）：确认型材数据确实来自参数库（而不是内置快照）。</summary>
    public string DumpParamSource()
    {
        var sb = new System.Text.StringBuilder();
        var P = typeof(GlassManagement.Calc.Data.ParamLibrary);
        sb.AppendLine("数据源," + GlassManagement.Calc.Data.ParamLibrary.SourceNote);
        sb.AppendLine("参数库目录," + (GlassManagement.Calc.Data.ParamLibrary.ParamDir is { Length: > 0 } d ? d : "<未找到>"));
        sb.AppendLine("构件式立柱表行数," + GlassManagement.Calc.Data.ParamLibrary.Columns.Count);
        sb.AppendLine("左框表行数（单元立柱一）," + GlassManagement.Calc.Data.ParamLibrary.UnitColumnsLeft.Count);
        sb.AppendLine("右框表行数（单元立柱二）," + GlassManagement.Calc.Data.ParamLibrary.UnitColumnsRight.Count);
        foreach (var c in GlassManagement.Calc.Data.ParamLibrary.UnitColumnsLeft)
            sb.AppendLine($"  左框,{c.Code},{c.Ix},{c.Wx1},{c.Wx2},{c.StaticMoment},{c.ShearWebWidth}");
        foreach (var c in GlassManagement.Calc.Data.ParamLibrary.UnitColumnsRight)
            sb.AppendLine($"  右框,{c.Code},{c.Ix},{c.Wx1},{c.Wx2},{c.StaticMoment},{c.ShearWebWidth}");
        return sb.ToString();
    }

    /// <summary>诊断（--selftest=region）：逐个关键字跑参数库地区查找，输出命中行与 50/100 年风压。</summary>
    public string DumpRegionLookup()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("关键字,命中地区名,50年Pa,100年Pa");
        string[] keys = { "上海", "北京", "广州", "广东广州", "深圳", "不存在的城市" };
        foreach (var k in keys)
        {
            var r = GlassManagement.Calc.Data.ParamLibrary.FindRegion(k);
            sb.Append(k).Append(',')
              .Append(r?.Name ?? "<null>").Append(',')
              .Append(r is null ? "" : r.Wind50Pa.ToString()).Append(',')
              .Append(r is null ? "" : r.Wind100Pa.ToString())
              .AppendLine();
        }

        sb.AppendLine("-- session 层（复现扫描路径）--");
        sb.AppendLine("设定值,session.CurrentRegion,RegionWind50KPa,RegionWind100KPa");
        foreach (var k in new[] { "上海", "广州", "广东广州" })
        {
            Session.RegionSelected = k;
            sb.Append(k).Append(',')
              .Append(Session.CurrentRegion?.Name ?? "<null>").Append(',')
              .Append(Session.RegionWind50KPa.ToString("0.####")).Append(',')
              .Append(Session.RegionWind100KPa.ToString("0.####"))
              .AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>
    /// 诊断（对拍用）：把立柱屏预演结果按行导出成纯文本，供 `local-data/recon/compare_v31.py`
    /// 与 V31 实机 `v31_preview_result.txt` 逐项做差值比较。
    /// </summary>
    public string DumpColumnPreviewText()
        => _column is null
            ? ""
            : string.Join(Environment.NewLine, System.Linq.Enumerable.Select(_column.Results, r => r.Text));

    /// <summary>
    /// 诊断（对拍用）：双跨梁两组参数（L1=3000，L2=1500 等跨 / L2=1200 不等跨）各预演一次，
    /// 输出两段文本供与 V31 实机 <c>model_shuang_*.txt</c> 逐行比对。
    /// </summary>
    public string SelfTestDoubleSpan()
    {
        SelfTestColumnPreview();                       // 走真实链路：第一屏 →「下一步」→ 立柱屏（wk 由第一屏算出）
        Session.ModelDoubleSpan = true;
        Session.ColumnSpanMm = 3000;

        Session.ShortSpanMm = 1500;                    // 等跨：V31 会自动改成 1499/1501
        _column!.PreviewCommand.Execute(null);
        string t1 = DumpColumnPreviewText();

        Session.ShortSpanMm = 1200;                    // 不等跨：1200/1800 原样
        _column.PreviewCommand.Execute(null);
        string t2 = DumpColumnPreviewText();

        string nl = Environment.NewLine;
        return "### CASE L1=3000 L2=1500" + nl + t1 + nl + "### CASE L1=3000 L2=1200" + nl + t2 + nl;
    }

    /// <summary>
    /// 诊断（对拍用）：立柱页「模型 × 跨度」矩阵。
    /// **每组都先在第一页重算 wk（Recalculate）**，再从第一页结果算立柱 —— 不允许在立柱页覆盖荷载。
    /// 输出 CSV，供与 V31 实机 <c>v31runs/mat_*.csv</c> 逐位比对。
    /// </summary>
    public string SelfTestColumnMatrix()
    {
        SelfTestColumnPreview();                       // 建链并定位到立柱屏

        var cases = new (string model, double l1, double l2)[]
        {
            ("单跨受拉", 3000, 0), ("单跨受拉", 4000, 0), ("单跨受拉", 2500, 0),
            ("单跨受压", 3000, 0), ("单跨受压", 4000, 0),
            ("双跨梁", 3000, 1499), ("双跨梁", 3000, 1200), ("双跨梁", 3000, 900),
            ("双跨梁", 3000, 600), ("双跨梁", 4500, 2000), ("双跨梁", 2500, 1000),
            ("双跨梁", 4000, 1500),
            ("连续梁", 3000, 1500), ("连续梁", 4000, 2000), ("连续梁", 2500, 1250),
        };

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("model,L1,L2,wk,w_pre,i_pre,df,lim,sigma,tau");
        foreach (var (model, l1, l2) in cases)
        {
            Session.ModelSingleTension = model == "单跨受拉";
            Session.ModelSingleCompression = model == "单跨受压";
            Session.ModelDoubleSpan = model == "双跨梁";
            Session.ModelContinuous3Span = model == "连续梁";
            Session.ColumnSpanMm = l1;
            if (l2 > 0) Session.ShortSpanMm = l2;

            _basic?.Recalculate();                     // ① 第一页重算（A 随 L1 变 → wk 变）
            _column!.PreviewCommand.Execute(null);     // ② 立柱页预演

            var map = new System.Collections.Generic.Dictionary<string, string>();
            foreach (var r in _column.Results)
            {
                string t = r.Text ?? "";
                foreach (var (key, pat) in Patterns)
                {
                    var m = System.Text.RegularExpressions.Regex.Match(t, pat);
                    if (m.Success) { map[key] = m.Groups[1].Value; break; }
                }
            }
            string G(string k) => map.TryGetValue(k, out var v) ? v : "";
            sb.AppendLine($"{model},{l1},{(l2 > 0 ? l2 : 0)},{G("wk")},{G("w_pre")},{G("i_pre")},{G("df")},{G("lim")},{G("sigma")},{G("tau")}");
        }
        return sb.ToString();
    }

    /// <summary>
    /// 诊断（对拍用）：「其它材质」人工参数 + 换型材。
    /// V31 实机（双跨 3000/1500、wk=1.285、60/80系列、E=70000、f=90、fv=55）：
    /// w_pre=6628.134（=M/90）、i_pre=66756.466、df=0.628、lim=8.339、σ=27.808、τ=5.092。
    /// </summary>
    public string SelfTestCustomMaterial()
    {
        SelfTestColumnPreview();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("case,wk,w_pre,i_pre,df,lim,sigma,fAllow,tau,fvAllow,I,W");

        // ① 自定义材质 f=90 / fv=55（E 保持 70000）
        Session.ModelDoubleSpan = true;
        Session.ColumnSpanMm = 3000;
        Session.ShortSpanMm = 1500;
        Session.CustomMaterial = true;
        Session.CustomElasticModulus = 70000;
        Session.CustomAllowBending = 90;
        Session.CustomAllowShear = 55;
        _basic?.Recalculate();
        _column!.PreviewCommand.Execute(null);
        sb.AppendLine("custom_f90_fv55," + CollectRow());

        // ② 回到内置材质（6063A-T5），确认能复原
        Session.CustomMaterial = false;
        _basic?.Recalculate();
        _column!.PreviewCommand.Execute(null);
        sb.AppendLine("builtin_6063A_T5," + CollectRow());
        return sb.ToString();
    }

    /// <summary>把立柱页当前结果的 7 个量 + 材质/型材设计值抽成一行 CSV（内部用）。</summary>
    private string CollectRow()
    {
        var map = new System.Collections.Generic.Dictionary<string, string>();
        foreach (var r in _column!.Results)
        {
            string t = r.Text ?? "";
            foreach (var (key, pat) in Patterns)
            {
                var m = System.Text.RegularExpressions.Regex.Match(t, pat);
                if (m.Success) { map[key] = m.Groups[1].Value; break; }
            }
            var mi = System.Text.RegularExpressions.Regex.Match(t, @"选用型材的惯性矩设计值为[：:]([0-9.]+)");
            if (mi.Success) map["I"] = mi.Groups[1].Value;
            var mw = System.Text.RegularExpressions.Regex.Match(t, @"选用型材的抗弯矩设计值为[：:]([0-9.]+)");
            if (mw.Success) map["W"] = mw.Groups[1].Value;
            var mb = System.Text.RegularExpressions.Regex.Match(t, @"抗弯强度设计值为[：:]([0-9.]+)");
            if (mb.Success) map["fAllow"] = mb.Groups[1].Value;
            var mv = System.Text.RegularExpressions.Regex.Match(t, @"抗剪强度设计值为[：:]([0-9.]+)");
            if (mv.Success) map["fvAllow"] = mv.Groups[1].Value;
        }
        string G(string k) => map.TryGetValue(k, out var v) ? v : "";
        return string.Join(",", new[] { G("wk"), G("w_pre"), G("i_pre"), G("df"), G("lim"),
                                        G("sigma"), G("fAllow"), G("tau"), G("fvAllow"), G("I"), G("W") });
    }

    private static readonly (string key, string pat)[] Patterns =
    {
        ("wk", @"风荷载标准值为[：:]([0-9.]+)"),
        ("w_pre", @"抗弯矩预选[：:]([0-9.]+)"),
        ("i_pre", @"惯性矩预选[：:]([0-9.]+)"),
        ("df", @"实际挠度计算值为[：:]([0-9.]+)"),
        ("lim", @"规范要求的挠度值为[：:]([0-9.]+)"),
        ("sigma", @"抗弯强度计算值[：:]([0-9.]+)"),
        ("tau", @"抗剪强度计算值[：:]([0-9.]+)"),
    };

    /// <summary>子屏 VM 的 StatusText 变化（确认数据/预演结果说明）同步到向导状态文本，主窗口状态栏据此刷新。</summary>
    private void MirrorStatus(ObservableObject vm)
    {
        vm.PropertyChanged -= OnChildStatus;
        vm.PropertyChanged += OnChildStatus;
    }
    private void OnChildStatus(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "StatusText" && sender is not null)
        {
            var prop = sender.GetType().GetProperty("StatusText");
            var v = prop?.GetValue(sender) as string;
            if (!string.IsNullOrEmpty(v)) StatusText = v;
        }
    }

    public MultiSpanViewModel GetOrCreateMulti()
    {
        _multi ??= new MultiSpanViewModel(Session, this);
        return _multi;
    }

    public void CloseForm()
    {
        CurrentContent = null;
        StatusText = "已退出当前计算（向导会话保留，上一步可回）";
    }

    // 链上各屏的「上一步/下一步」由子 VM 调这些方法
    public void StepFromColumnNext()
    {
        if (Session.ModelMultiStatic) GoMultiSpan();
        else GoBeamCalc();      // 立柱之后进入横梁屏（V31 向导顺序：基本参数 → 立柱 → 横梁 → …）
    }

    public void StepFromBasicNext() => GoColumnCalc();

    /// <summary>向导 4：玻璃计算屏（MVVM 版，计算在 GlassEngine）。</summary>
    [RelayCommand]
    public void GoGlassCalc()
    {
        _glass ??= new GlassCalcViewModel(Session, this);
        MirrorStatus(_glass);
        CurrentContent = _glass;
        StatusText = $"向导 4：幕墙计算-玻璃计算（{Session.GlassCaption}｜"
                   + $"{Session.GlassWidthMm:0.###}×{Session.GlassHeightMm:0.###}mm"
                   + $"｜wk={Session.PanelWindKpa:0.###}KPa）";
    }

    /// <summary>横梁屏「下一步」：横梁之后是玻璃页。</summary>
    public void StepFromBeamNext() => GoGlassCalc();

    /// <summary>向导 5：连接计算屏（MVVM 版，计算在 ConnectEngine）。</summary>
    [RelayCommand]
    public void GoConnectCalc()
    {
        _connect ??= new ConnectCalcViewModel(Session, this);
        MirrorStatus(_connect);
        CurrentContent = _connect;
        StatusText = $"向导 5：幕墙计算-连接计算（wk={Session.WindStandardKpa:0.###}KPa｜"
                   + $"{Session.BeamSpanMm:0.###}×{Session.BeamUpperGridHeightMm:0.###}mm）";
    }

    /// <summary>玻璃屏「下一步」：玻璃之后是连接页。</summary>
    public void StepFromGlassNext() => GoConnectCalc();

    /// <summary>向导 6：埋件与焊缝计算屏（P2）。</summary>
    [RelayCommand]
    public void GoEmbedCalc()
    {
        _embed ??= new EmbedCalcViewModel(Session, this);
        MirrorStatus(_embed);
        CurrentContent = _embed;
        StatusText = "向导 6：幕墙计算-埋件与焊缝计算";
    }

    /// <summary>连接屏「下一步」：连接之后是埋件页。</summary>
    public void StepFromConnectNext() => GoEmbedCalc();

    /// <summary>向导 7：胶类、伸缩及紧固计算屏（P3）。</summary>
    [RelayCommand]
    public void GoSealantCalc()
    {
        _sealant ??= new SealantCalcViewModel(Session, this);
        MirrorStatus(_sealant);
        CurrentContent = _sealant;
        StatusText = "向导 7：幕墙计算-胶类、伸缩及紧固计算";
    }

    /// <summary>埋件屏「下一步」：埋件之后是胶类页。</summary>
    public void StepFromEmbedNext() => GoSealantCalc();

    /// <summary>向导 8：生成计算书面板（V31 603×330，CW22）。</summary>
    [RelayCommand]
    public void GoBookPanel()
    {
        _bookPanel ??= new CalcBookPanelViewModel(this);
        CurrentContent = _bookPanel;
        StatusText = "向导 8：生成计算书（支持微软 office 及金山 wps office）";
    }

    /// <summary>胶类屏「下一步」：胶类之后是生成计算书面板。</summary>
    public void StepFromSealantNext() => GoBookPanel();

    /// <summary>
    /// 生成计算书 docx（各屏先点过「预演」才有内容）。
    /// 输出目录：仓库根 <c>计算书输出/</c>，文件名带时间戳，不覆盖旧稿。
    /// </summary>
    public string? GenerateCalcBook(string? outputPath = null,
                                    BookPageSetup? setup = null,
                                    bool noAppendix = false,
                                    CalcBookMeta? meta = null)
    {
        try
        {
            // 立柱/横梁/玻璃三章靠各自预演生成的文本摘要；没预演过就自动跑一次
            // （失败不影响出书，那章会显示"本章未参与计算"）。
            TryFillSummary(() =>
            {
                if (Session.LastColumnSummary is null)
                {
                    _column ??= new ColumnCalcViewModel(Session, this);
                    _column.PreviewCommand.Execute(null);
                }
            });
            TryFillSummary(() =>
            {
                if (Session.LastBeamSummary is null)
                {
                    _beam ??= new BeamCalcViewModel(Session, this);
                    _beam.PreviewCommand.Execute(null);
                }
            });
            TryFillSummary(() =>
            {
                if (Session.LastGlassSummary is null)
                {
                    _glass ??= new GlassCalcViewModel(Session, this);
                    _glass.PreviewCommand.Execute(null);
                }
            });

            // 用户没逐屏点「预演」时，用当前会话参数补齐（一键出书 / 批量出书）
            if (Session.LastConnectResult is null)
                Session.LastConnectResult = ConnectEngine.Compute(
                    (_connect ??= new ConnectCalcViewModel(Session, this)).BuildInputs());
            if (Session.LastEmbedResult is null)
                Session.LastEmbedResult = EmbedEngine.Compute(
                    (_embed ??= new EmbedCalcViewModel(Session, this)).BuildInputs());
            if (Session.LastSealantResult is null)
                Session.LastSealantResult = SealantEngine.Compute(
                    (_sealant ??= new SealantCalcViewModel(Session, this)).BuildInputs());

            var input = new CalcBookInput
            {
                ProjectName = meta?.ProjectName ?? "北京xx幕墙装饰工程",
                ProjectSub = meta?.ProjectSub ?? "全隐框玻璃幕墙",
                Company = meta?.Company ?? "",
                Designer = meta?.Designer ?? "",
                Checker = meta?.Checker ?? "",
                Approver = meta?.Approver ?? "",
                Ratifier = meta?.Ratifier ?? "",
                WkKpa = Session.WindStandardKpa,
                WkPanelKpa = Session.PanelWindKpa,
                WindSource = Session.PanelWindSource,
                ColumnSummary = Session.LastColumnSummary,
                BeamSummary = Session.LastBeamSummary,
                GlassSummary = Session.LastGlassSummary,
                NoAppendix = noAppendix,
                Connect = Session.LastConnectResult,
                Embed = Session.LastEmbedResult as GlassManagement.Calc.Engines.EmbedResult?,
                Sealant = Session.LastSealantResult as GlassManagement.Calc.Engines.SealantResult?,
            };

            if (outputPath is null)
            {
                var root = FindRepoRoot() ?? Directory.GetCurrentDirectory();
                var dir = Path.Combine(root, "计算书输出");
                Directory.CreateDirectory(dir);
                outputPath = Path.Combine(dir,
                    $"计算书_{DateTime.Now:yyyyMMdd_HHmmss}.docx");
            }

            CalcBookWriter.Write(
                CalcBookComposer.Compose(input), outputPath);
            LastCalcBookPath = outputPath;
            return outputPath;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void TryFillSummary(Action act)
    {
        try { act(); }
        catch (Exception) { /* 该章留空即可，不阻断出书 */ }
    }

    /// <summary>最近一次生成的计算书路径（供 UI 打开）。</summary>
    public string? LastCalcBookPath { get; private set; }

    private static string? FindRepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            if (File.Exists(Path.Combine(d.FullName, "global.json"))) return d.FullName;
            d = d.Parent;
        }
        return null;
    }


}
