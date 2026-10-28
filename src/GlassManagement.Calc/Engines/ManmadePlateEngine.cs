using System;
using System.Collections.Generic;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 人造板材（瓷板 / 陶板 / 微晶玻璃 · 短挂件连接）板块计算引擎。
///
/// <para><b>算式来源</b>：V31 实机 <c>人造板材幕墙&gt;瓷板：短挂件连接 → 板块计算</c> 屏（782×536）的
/// Variant 实值轨迹（Frida 钩 <c>__vbaVarMul/Div/Add/Pow</c> 与 <c>rtcRound</c>，92 条记录）＋
/// 脱壳镜像 <c>0xE3E057–0xE46000</c>（板块计算）与 <c>0xE69217–0xE6B260</c>（m 表）反汇编 ＋
/// 15 组 r 值扰动直读 m 插值局部变量。四面对拍见 <c>docs/V31_MANMADE_PLATE_UI.md</c>。</para>
///
/// <para><b>与石材板块屏的关键差异</b>（这是独立引擎，不复用 <see cref="StonePlateEngine"/>）：</para>
/// <list type="bullet">
/// <item>抗弯 σ = 6·m·q_d·<b>大支承点间距²</b>/t²（石材用「计算长边」）；</item>
/// <item>抗剪 τ = 1.25·q_d·短边·长边/(<b>4</b>·槽口宽·槽底长)（4 = 挂件总数）；</item>
/// <item>m 表 15 断点（0.1…1.0），<b>m 随 r 增大而增大</b>（石材 11 断点、m 随 r 增大而减小）；</item>
/// <item>r = 小支承点间距 / 大支承点间距（不是「计算短边/计算长边」）；</item>
/// <item>挂件挠度 w = N_v·e³·<b>2</b>/(3·E·I)（比石材多一个 ×2）。</item>
/// </list>
///
/// <para><b>严禁反推</b>：m 表 15 断点 + 表值、材料重度 γ（23.5/24/27）、挂件材质许用值、
/// 螺栓有效面积全部来自常量池原文或实机扰动直读，不存在拟合。</para>
///
/// <para><b>槽数组</b>：本屏 <c>ds:[0x1C911BC]</c>，16 字节/槽。关键槽位：slot3=短边、slot4=长边、
/// slot5=小支承点间距 B、slot6=大支承点间距、slot7=板厚、slot8=αmax、slot10=qd'、slot11=wk、
/// slot12=qd、slot13=抗弯 f、slot14=r、slot15=m、slot19=抗剪 fv、slot20=槽口宽、slot21=槽底长、
/// slot23=挂件总数、slot27=挂件抗剪 fv、slot28=Av、slot34=γ/1e6、slot35=t。</para>
/// </summary>
public sealed class ManmadePlateEngine
{
    // ───────────────────────── 系数表（常量池原文 / 实机直读） ─────────────────────────

    /// <summary>
    /// m 的比值断点（15 点，r = 小支承点间距 / 大支承点间距）。
    /// 常量池：0.1(0x401DA0) 0.2(0x401918) 0.3(0x401970) 0.4(0x401910) 0.5(0x401928)
    /// 0.55(0x402998) 0.6(0x401938) 0.65(0x4028E0) 0.7(0x401930) 0.75(0x401D88)
    /// 0.8(0x401920) 0.85(0x401D78) 0.9(0x401D70) 0.95(0x401D58) 1.0(0x4019C0)。
    /// 0.125/0.2 之间另有一个 0.125(0x4029C0) 分段，但扰动实测 r&lt;0.125 走钳位路径，
    /// 有效断点序列即下表。</summary>
    private static readonly double[] RatioX =
    {
        0.10, 0.20, 0.30, 0.40, 0.50, 0.55, 0.60, 0.65,
        0.70, 0.75, 0.80, 0.85, 0.90, 0.95, 1.00,
    };

    /// <summary>
    /// 瓷板弯矩系数 m（按 r 升序，<b>递增</b>）。
    /// 15 组扰动直读 m 插值局部变量（<c>manmade_mtable.json</c>），每个表值均可在常量池定位：
    /// 0.125(0x4029C0) 0.126(0x404B40) 0.127(0x404AE8) 0.129(0x404AE0) 0.13(0x404B30)
    /// 0.132(0x404B28) 0.134(0x404B20) 0.136(0x404B18) 0.138(0x404B10) 0.14(0x401980)
    /// 0.142(0x404AD8) 0.144(0x404B08) 0.147(0x404B00) 0.15(0x401978) 0.152(0x404AB0)。
    /// 函数 <c>0xE69217</c>（面板种类 Select Case 后内联插值），内核 <c>round4(v1−(v1−v2)·(r1−x)/(r1−r2))</c>。</summary>
    private static readonly double[] MManmade =
    {
        0.125, 0.126, 0.127, 0.129, 0.130, 0.132, 0.134, 0.136,
        0.138, 0.140, 0.142, 0.144, 0.147, 0.150, 0.152,
    };

    /// <summary>
    /// 面板种类 → 材料重度 γ（kN/m³）。
    /// 常量池（<c>0xE3ECB0–0xE3EE78</c> 的 Select Case，字符串比较后 fld 常量 ÷ 1e6 入 slot34）：
    /// 瓷板(0x4CF63C)→0x4049C0=23.5、陶板(0x4CF648)→0x402858=24.0、微晶玻璃板(0x4CF698)→0x4020F0=27.0。
    /// </summary>
    private static readonly Dictionary<string, double> DensityKnM3 = new(StringComparer.Ordinal)
    {
        ["瓷板"] = 23.5,
        ["陶板"] = 24.0,
        ["微晶玻璃板"] = 27.0,
    };

    /// <summary>
    /// 挂件材质 → （抗弯强度设计值 [σ] MPa，弹性模量 E MPa，挠度限值分母）。
    /// 与石材屏同构（挂件参数框共用），实机直读：铝 6 项限值 = 2e/180 = 0.333（e=30），
    /// 304/316 不锈钢 = 2e/250 = 0.24。</summary>
    private static readonly Dictionary<string, (double Fb, double E, double Div)> HangMaterialTable = new()
    {
        ["6063-T5"] = (90, 70000, 180),
        ["6063-T6"] = (150, 70000, 180),
        ["6063A-T5"] = (135, 70000, 180),
        ["6063A-T6"] = (160, 70000, 180),
        ["6061-T4"] = (90, 70000, 180),
        ["6061-T6"] = (200, 70000, 180),
        ["304不锈钢"] = (180, 206000, 250),
        ["316不锈钢"] = (180, 206000, 250),
    };

    /// <summary>螺栓有效直径 de（mm），参数库「螺栓1」表「有效直径」列原文。</summary>
    private static readonly Dictionary<double, double> BoltEffectiveDiameter = new()
    {
        [5] = 4.25314,
        [6] = 5.06015,
        [7] = 6.06756,
        [8] = 6.82819,
        [10] = 8.59566,
    };

    /// <summary>螺栓材质 → （抗拉强度设计值 ft MPa，抗剪强度设计值 fv MPa）。实机直读。</summary>
    private static readonly Dictionary<string, (double Ft, double Fv)> BoltMaterialTable = new()
    {
        ["碳钢：4.6-C级"] = (170, 140),
        ["碳钢：4.8-C级"] = (170, 140),
        ["碳钢：5.6-A级"] = (210, 190),
        ["碳钢：5.6-B级"] = (210, 190),
        ["碳钢：8.8-A级"] = (400, 320),
        ["碳钢：8.8-B级"] = (400, 320),
        ["不锈钢：A1-50级"] = (230, 175),
        ["不锈钢：A2-50级"] = (230, 175),
        ["不锈钢：A3-70级"] = (320, 245),
        ["不锈钢：A4-70级"] = (320, 245),
        ["不锈钢：A5-80级"] = (370, 280),
    };

    /// <summary>挂件材质候选（ComboBox，默认 316不锈钢）。</summary>
    public static IReadOnlyList<string> HangMaterialOptions { get; } =
        new[] { "6063-T5", "6063-T6", "6063A-T5", "6063A-T6", "6061-T4", "6061-T6", "304不锈钢", "316不锈钢" };

    /// <summary>螺栓材质候选（ComboBox，默认 4.6-C级）。</summary>
    public static IReadOnlyList<string> BoltMaterialOptions { get; } = new[]
    {
        "碳钢：4.6-C级", "碳钢：4.8-C级", "碳钢：5.6-A级", "碳钢：5.6-B级", "碳钢：8.8-A级", "碳钢：8.8-B级",
        "不锈钢：A1-50级", "不锈钢：A2-50级", "不锈钢：A3-70级", "不锈钢：A4-70级", "不锈钢：A5-80级",
    };

    /// <summary>螺栓直径候选（ComboBox，默认 8）。</summary>
    public static IReadOnlyList<double> BoltDiameterOptions { get; } = new[] { 5.0, 6.0, 7.0, 8.0, 10.0 };

    /// <summary>挂件总数候选（ComboBox，默认 4）。</summary>
    public static IReadOnlyList<int> HangerTotalCountOptions { get; } = new[] { 2, 4, 6, 8, 10, 12 };

    /// <summary>面板种类候选（短挂件连接覆盖瓷板 / 陶板 / 微晶玻璃板）。</summary>
    public static IReadOnlyList<string> PanelSpeciesOptions { get; } = new[] { "瓷板", "陶板", "微晶玻璃板" };

    // ───────────────────────── 输入 ─────────────────────────

    public sealed class Input
    {
        /// <summary>面板种类（瓷板/陶板/微晶玻璃板），决定重度 γ 与默认强度。</summary>
        public string Species = "瓷板";

        /// <summary>板材总厚度 L（mm），界面 (100,126) 默认 25。</summary>
        public double ThicknessMm = 25;

        /// <summary>短边边长（mm），界面 (92,170) 默认 800。</summary>
        public double ShortSideMm = 800;
        /// <summary>长边边长（mm），界面 (92,191) 默认 900。</summary>
        public double LongSideMm = 900;

        /// <summary>板小支承点间距 B（mm），界面 (129,213) 默认 650。</summary>
        public double SmallSupportMm = 650;
        /// <summary>板大支承点间距（mm），界面 (129,235) 默认 900。</summary>
        public double LargeSupportMm = 900;

        /// <summary>面板抗弯强度设计值 f（MPa），界面 (322,56) 默认 15（瓷板）。</summary>
        public double AllowableBendingMpa = 15;
        /// <summary>面板抗剪强度设计值 fv（MPa），界面 (322,80) 默认 7.5（瓷板）。</summary>
        public double AllowableShearMpa = 7.5;

        /// <summary>面板槽口宽度剪面厚度 t（mm），界面 (347,157) 默认 3。</summary>
        public double SlotMouthMm = 3;
        /// <summary>槽口剪切面总长度 fSG（mm），界面 (337,176) 默认 56。</summary>
        public double SlotShearLengthMm = 56;
        /// <summary>每个板块挂件总数 n，界面 (350,195) 默认 4。</summary>
        public int HangerTotalCount = 4;

        /// <summary>金属挂件抗剪强度设计值 fv（MPa），界面 (369,216) 默认 100（禁用回显）。</summary>
        public double HangerAllowableShearMpa = 100;
        /// <summary>单个挂件抗剪截面积 Av（mm²），界面 (345,237) 默认 240（禁用回显）。</summary>
        public double HangerShearAreaMm2 = 240;

        // ── 挂件参数（与石材屏同构） ──
        /// <summary>挂件宽（mm），界面 (79,373) 默认 80。</summary>
        public double HangerWidthMm = 80;
        /// <summary>挂件厚（mm），界面 (79,396) 默认 3。</summary>
        public double HangerThicknessMm = 3;
        /// <summary>螺栓中心到板边距离 e（mm），界面 (171,373) 默认 30。</summary>
        public double HangerEccentricityMm = 30;
        /// <summary>挂件长 L（mm），界面 (171,396) 默认 60。</summary>
        public double HangerLengthMm = 60;
        /// <summary>挂件材质（8 项），界面 (81,348) 默认 316不锈钢。</summary>
        public string HangerMaterial = "316不锈钢";

        /// <summary>每块板块水平荷载挂件数，界面 (119,442) 默认 2。</summary>
        public int HorizontalHangerCount = 2;
        /// <summary>每块板块承重挂件数，界面 (156,419) 默认 2。</summary>
        public int LoadHangerCount = 2;
        /// <summary>每挂件螺栓数，界面 (198,502) 默认 1。</summary>
        public int BoltPerHanger = 1;
        /// <summary>螺栓材质（11 项），界面 (72,478) 默认 4.6-C级。</summary>
        public string BoltMaterial = "碳钢：4.6-C级";
        /// <summary>螺栓公称直径（5/6/7/8/10），界面 (69,502) 默认 8。</summary>
        public double BoltDiameterMm = 8;

        /// <summary>地震影响系数最大值 αmax（参数库「地区参数」，默认北京 0.16）。</summary>
        public double AlphaMax = 0.16;
        /// <summary>面板口径风荷载标准值 wk（N/mm²）。★必须传 N/mm²，不要用 kPa/1000。</summary>
        public double Wk = 0.00137;
    }

    // ───────────────────────── 输出 ─────────────────────────

    public sealed class Result
    {
        /// <summary>面板部分风荷载标准值（KPa，2 位）。</summary>
        public double WindStandardKpa;

        /// <summary>材料重度 γ（kN/m³，按面板种类）。</summary>
        public double DensityKnM3;
        /// <summary>水平地震作用标准值 qd' = round6(5·αmax·(γ/1e6)·t)（N/mm²）。</summary>
        public double SeismicMpa;
        /// <summary>面荷载设计值 q_d = round6(1.5·wk + 0.7·qd')（N/mm²）。</summary>
        public double Qd;

        /// <summary>比值 r = round3(小支承点间距/大支承点间距)。</summary>
        public double Ratio;
        /// <summary>弯矩系数 m = round4(表插值)。</summary>
        public double MCoef;

        /// <summary>面板抗弯强度计算值 σ = round3(6·m·q_d·大支承点²/t²)（MPa）。</summary>
        public double StressComputed;
        /// <summary>面板抗弯强度设计值（MPa）。</summary>
        public double StressAllowable;
        /// <summary>σ ≤ [σ]。</summary>
        public bool StressOk;

        /// <summary>面板抗剪强度计算值 τ = round3(1.25·q_d·短边·长边/(n·槽口宽·槽底长))（MPa）。</summary>
        public double ShearComputed;
        /// <summary>面板抗剪强度设计值（MPa）。</summary>
        public double ShearAllowable;
        /// <summary>τ ≤ [τ]。</summary>
        public bool ShearOk;

        /// <summary>水平荷载作用下挂件抗剪 = round3(1.25·q_d·短边·长边/(2·Av))（MPa）。</summary>
        public double HangerShearHorizontal;
        /// <summary>挂件抗剪承载力设计值（MPa）。</summary>
        public double HangerShearAllowable;
        /// <summary>水平挂件抗剪满足。</summary>
        public bool HangerShearHorizontalOk;

        /// <summary>自重标准值 G = round3((γ/1e6)·短边·长边·t)（N）。</summary>
        public double SelfWeightN;
        /// <summary>自重荷载作用下挂件抗剪 = round3(1.3·G/(2·Av))（MPa）。</summary>
        public double HangerShearVertical;
        /// <summary>自重挂件抗剪满足。</summary>
        public bool HangerShearVerticalOk;

        /// <summary>水平剪力 V = 1.25·q_d·短边·长边/水平挂件数（N，不取整）。</summary>
        public double HangerShearN;
        /// <summary>每承重挂件自重标准值 N_v = G/承重挂件数（N）。</summary>
        public double LoadPerHanger;
        /// <summary>挂件螺栓设计拉力 N = N_v·1.3（N）。</summary>
        public double HangerTensionN;
        /// <summary>挂件弯矩 M = N·e（N·mm）。</summary>
        public double HangerMomentNmm;

        /// <summary>挂件抗弯强度计算值 = round3(V/Av + M/W_e)（MPa）。</summary>
        public double HangerStressComputed;
        /// <summary>挂件抗弯强度设计值（MPa，按材质）。</summary>
        public double HangerStressAllowable;
        /// <summary>挂件抗弯满足。</summary>
        public bool HangerStressOk;

        /// <summary>挂件挠度计算值 = round3(N_v·e³·2/(3·E·I))（mm）。</summary>
        public double HangerDeflectionComputed;
        /// <summary>挂件挠度设计值 = round3(2e/分母)，分母 180（铝）/250（钢）。</summary>
        public double HangerDeflectionAllowable;
        /// <summary>挂件挠度满足。</summary>
        public bool HangerDeflectionOk;

        /// <summary>螺栓抗拉承受力计算值 = round3(N/每挂件螺栓数)（N）。</summary>
        public double BoltTensionComputed;
        /// <summary>螺栓抗拉承受力设计值 = round3(As·ft)（N）。</summary>
        public double BoltTensionAllowable;
        /// <summary>螺栓抗拉满足。</summary>
        public bool BoltTensionOk;

        /// <summary>螺栓抗剪承受力计算值 = round3(V/每挂件螺栓数)（N）。</summary>
        public double BoltShearComputed;
        /// <summary>螺栓抗剪承受力设计值 = round3(As·fv)（N）。</summary>
        public double BoltShearAllowable;
        /// <summary>螺栓抗剪满足。</summary>
        public bool BoltShearOk;

        /// <summary>拉剪复合 = round6((N/N_d)² + (V/V_d)²)。</summary>
        public double BoltCombined;
        /// <summary>拉剪复合 ≤ 1。</summary>
        public bool BoltCombinedOk;

        // ── 过程量 ──
        /// <summary>挂件截面面积 W = b·t（mm²）。</summary>
        public double HangerSectionMm2;
        /// <summary>挂件截面模量 W_e = b·t²/6（mm³）。</summary>
        public double HangerModulusMm3;
        /// <summary>挂件惯性矩 I = b·t³/12（mm⁴）。</summary>
        public double HangerInertiaMm4;
        /// <summary>挂件弹性模量（MPa）。</summary>
        public double HangerElasticModulusMpa;
        /// <summary>螺栓有效面积 As（mm²）。</summary>
        public double BoltAreaMm2;
    }

    // ───────────────────────── 计算 ─────────────────────────

    public static Result Compute(Input it)
    {
        if (it is null) throw new ArgumentNullException(nameof(it));

        var r = new Result
        {
            WindStandardKpa = WindLoadEngine.VbaRound(it.Wk * 1000.0, 2),
        };

        // 材料重度（按面板种类）
        r.DensityKnM3 = DensityOf(it.Species);

        // ① 荷载：地震 → 设计值（0xE3F09C–0xE3F2A2）
        double densityPerMm = r.DensityKnM3 / 1e6;                 // γ/1e6 = slot34
        r.SeismicMpa = WindLoadEngine.VbaRound(5.0 * it.AlphaMax * densityPerMm * it.ThicknessMm, 6);
        r.Qd = WindLoadEngine.VbaRound(1.5 * it.Wk + 0.7 * r.SeismicMpa, 6);

        // ② 比值 + m（0xE3F7C3 / 0xE69217）
        r.Ratio = WindLoadEngine.VbaRound(it.SmallSupportMm / it.LargeSupportMm, 3);
        r.MCoef = WindLoadEngine.VbaRound(MOf(r.Ratio), 4);

        // ③ 抗弯（0xE3F999–0xE3FA54）：σ = 6·m·q_d·大支承点²/t²
        r.StressComputed = WindLoadEngine.VbaRound(
            6.0 * r.MCoef * r.Qd * it.LargeSupportMm * it.LargeSupportMm
            / (it.ThicknessMm * it.ThicknessMm), 3);
        r.StressAllowable = it.AllowableBendingMpa;
        r.StressOk = r.StressComputed <= r.StressAllowable;

        // ④ 抗剪（0xE401C4–0xE40256）：τ = 1.25·q_d·短边·长边/(n·槽口宽·槽底长)
        double shortLong = it.ShortSideMm * it.LongSideMm;
        r.ShearComputed = WindLoadEngine.VbaRound(
            1.25 * r.Qd * shortLong
            / (it.HangerTotalCount * it.SlotMouthMm * it.SlotShearLengthMm), 3);
        r.ShearAllowable = it.AllowableShearMpa;
        r.ShearOk = r.ShearComputed <= r.ShearAllowable;

        // ⑤ 水平挂件抗剪（0xE408FC–0xE4096F）：1.25·q_d·短边·长边/(2·Av)
        r.HangerShearHorizontal = WindLoadEngine.VbaRound(
            1.25 * r.Qd * shortLong / (2.0 * it.HangerShearAreaMm2), 3);
        r.HangerShearAllowable = it.HangerAllowableShearMpa;
        r.HangerShearHorizontalOk = r.HangerShearHorizontal <= r.HangerShearAllowable;

        // ⑥ 自重 + 自重挂件抗剪（0xE40CBE–0xE410BD）
        r.SelfWeightN = WindLoadEngine.VbaRound(
            densityPerMm * it.ShortSideMm * it.LongSideMm * it.ThicknessMm, 3);
        r.HangerShearVertical = WindLoadEngine.VbaRound(
            1.3 * r.SelfWeightN / (2.0 * it.HangerShearAreaMm2), 3);
        r.HangerShearVerticalOk = r.HangerShearVertical <= r.HangerShearAllowable;

        // ⑦ 水平剪力 + 自重挂件 + 挂件抗弯（0xE41652–0xE4284C）
        r.HangerShearN = 1.25 * r.Qd * shortLong / it.HorizontalHangerCount;

        // ★ N_v 链与自重 G 链乘法顺序不同（0xE41A3C–0xE41C84）：
        //   N_v 分子 = round6(γ/1e6 · t) × 短边 × 长边（先 γ/1e6·t 取整，再乘边长），
        //   自重 G  = round3(γ/1e6 · 短边 · 长边 · t)（先乘边长再乘 t）。
        //   两者浮点位模式不同，必须分开算，不能用 G/承重挂件数 反推 N_v。
        double gammaT = WindLoadEngine.VbaRound(densityPerMm * it.ThicknessMm, 6);
        r.LoadPerHanger = gammaT * it.ShortSideMm * it.LongSideMm / it.LoadHangerCount;
        r.HangerTensionN = r.LoadPerHanger * 1.3;
        r.HangerMomentNmm = WindLoadEngine.VbaRound(r.HangerTensionN * it.HangerEccentricityMm, 3);

        r.HangerSectionMm2 = WindLoadEngine.VbaRound(it.HangerWidthMm * it.HangerThicknessMm, 3);
        r.HangerModulusMm3 = WindLoadEngine.VbaRound(
            it.HangerWidthMm * Math.Pow(it.HangerThicknessMm, 2) / 6.0, 3);
        r.HangerInertiaMm4 = WindLoadEngine.VbaRound(
            it.HangerWidthMm * Math.Pow(it.HangerThicknessMm, 3) / 12.0, 3);

        var hm = HangProps(it.HangerMaterial);
        r.HangerElasticModulusMpa = hm.E;

        r.HangerStressComputed = WindLoadEngine.VbaRound(
            r.HangerShearN / it.HangerShearAreaMm2 + r.HangerMomentNmm / r.HangerModulusMm3, 3);
        r.HangerStressAllowable = hm.Fb;
        r.HangerStressOk = r.HangerStressComputed <= r.HangerStressAllowable;

        // ⑧ 挂件挠度（0xE42EEA–0xE42FB1）：w = N_v·e³·2/(3·E·I)
        double e = it.HangerEccentricityMm;
        r.HangerDeflectionComputed = WindLoadEngine.VbaRound(
            r.LoadPerHanger * e * e * e * 2.0 / (3.0 * hm.E * r.HangerInertiaMm4), 3);
        r.HangerDeflectionAllowable = WindLoadEngine.VbaRound(2.0 * e / hm.Div, 3);
        r.HangerDeflectionOk = r.HangerDeflectionComputed <= r.HangerDeflectionAllowable;

        // ⑨ 螺栓（0xE433E3–0xE449D8）
        r.BoltTensionComputed = WindLoadEngine.VbaRound(r.HangerTensionN / it.BoltPerHanger, 3);
        r.BoltShearComputed = WindLoadEngine.VbaRound(r.HangerShearN / it.BoltPerHanger, 3);

        var bm = BoltProps(it.BoltMaterial);
        r.BoltAreaMm2 = BoltArea(it.BoltDiameterMm);
        r.BoltTensionAllowable = WindLoadEngine.VbaRound(r.BoltAreaMm2 * bm.Ft, 3);
        r.BoltShearAllowable = WindLoadEngine.VbaRound(r.BoltAreaMm2 * bm.Fv, 3);
        r.BoltTensionOk = r.BoltTensionComputed <= r.BoltTensionAllowable;
        r.BoltShearOk = r.BoltShearComputed <= r.BoltShearAllowable;

        r.BoltCombined = WindLoadEngine.VbaRound(
            Math.Pow(r.BoltTensionComputed / r.BoltTensionAllowable, 2)
            + Math.Pow(r.BoltShearComputed / r.BoltShearAllowable, 2), 6);
        r.BoltCombinedOk = r.BoltCombined <= 1.0;

        return r;
    }

    // ───────────────────────── 辅助 ─────────────────────────

    /// <summary>面板种类 → 材料重度 γ（kN/m³）。未知种类回落瓷板 23.5。</summary>
    public static double DensityOf(string species)
        => DensityKnM3.TryGetValue(species ?? string.Empty, out var v) ? v : 23.5;

    /// <summary>挂件材质 → （[σ], E, 挠度分母）。</summary>
    public static (double Fb, double E, double Div) HangProps(string material)
        => HangMaterialTable.TryGetValue(material ?? string.Empty, out var v) ? v : HangMaterialTable["316不锈钢"];

    /// <summary>螺栓材质 → （ft, fv）。</summary>
    public static (double Ft, double Fv) BoltProps(string material)
        => BoltMaterialTable.TryGetValue(material ?? string.Empty, out var v)
            ? v
            : BoltMaterialTable["碳钢：4.6-C级"];

    /// <summary>螺栓有效面积 As = round1(π/4·de²)。</summary>
    public static double BoltArea(double diameterMm)
    {
        double de = BoltEffectiveDiameter.TryGetValue(diameterMm, out var d) ? d : 6.82819;
        return WindLoadEngine.VbaRound(Math.PI / 4.0 * de * de, 1);
    }

    /// <summary>
    /// m(x) 分段线性插值 + 钳位，取 4 位。
    /// 内核（<c>0xE6B142–0xE6B216</c>）：<c>round4(v1 − (v1−v2)·(r1−x)/(r1−r2))</c>；
    /// x&lt;0.1 钳位取 0.125，x&gt;1.0 钳位取 0.152。</summary>
    private static double MOf(double x)
    {
        if (x <= RatioX[0]) return MManmade[0];
        if (x >= RatioX[RatioX.Length - 1]) return MManmade[MManmade.Length - 1];

        int i = 0;
        while (i < RatioX.Length - 2 && x > RatioX[i + 1]) i++;

        double r1 = RatioX[i], r2 = RatioX[i + 1];
        double v1 = MManmade[i], v2 = MManmade[i + 1];
        return v1 - (v1 - v2) * (r1 - x) / (r1 - r2);
    }
}
