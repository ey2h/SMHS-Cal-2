using System;
using System.Collections.Generic;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 石材（天然石材 · 短槽连接）板块计算引擎。
///
/// <para><b>算式来源</b>：V31 实机 <c>天然石材幕墙&gt;花岗岩：短槽连接 → 板块计算</c> 屏（936×448）的
/// Variant 实值轨迹（Frida 钩 <c>msvbvm60</c> 的 <c>__vbaVarMul/VarDiv/VarAdd/VarPow</c> 与 <c>rtcRound</c>，
/// 78 条记录）＋ 脱壳镜像 <c>0x1BE2700–0x1BE8A00</c> 反汇编 ＋ 22 组参数扰动直读。
/// 四面对拍见 <c>docs/V31_STONE_PLATE_UI.md</c>。</para>
///
/// <para><b>严禁反推</b>：本文件所有系数（m 表、托板数系数、材质许用值、螺栓有效面积）均来自
/// 常量池原文或实机输出直读，不存在由实测结果拟合出来的数值。
/// 唯一未闭合的分支是「四边连接」，按规程<b>不实现、不拟合</b>（见 <see cref="Compute"/> 的异常）。</para>
///
/// <para><b>槽数组</b>：本屏 <c>ds:[0x1C911BC]</c>，16 字节/槽。关键槽位见各字段注释。</para>
/// </summary>
public sealed class StonePlateEngine
{
    // ───────────────────────── 系数表（常量池原文 / 实机直读） ─────────────────────────

    /// <summary>m 的比值断点（11 点，<c>0x401928=0.5</c> … <c>0x4019C0=1.0</c>，步长 0.05）。</summary>
    private static readonly double[] RatioX =
        { 0.50, 0.55, 0.60, 0.65, 0.70, 0.75, 0.80, 0.85, 0.90, 0.95, 1.00 };

    /// <summary>
    /// 石材弯矩系数 m（按 x = 计算短边/计算长边 升序）。
    /// 常量池：<c>0x4128B0=0.1303 / 0x404B28=0.132 / 0x4128A8=0.1338 / 0x404B18=0.136 /
    /// 0x4128A0=0.1383 / 0x412898=0.1408 / 0x412890=0.1435 / 0x412888=0.1463 /
    /// 0x412880=0.1494 / 0x412878=0.1526 / 0x412870=0.1559</c>。
    /// 函数 <c>0x1C3473F</c>；x&lt;0.5 钳位取 0.1303。
    /// </summary>
    private static readonly double[] MStone =
        { 0.1303, 0.132, 0.1338, 0.136, 0.1383, 0.1408, 0.1435, 0.1463, 0.1494, 0.1526, 0.1559 };

    /// <summary>
    /// 托板个数 → 抗剪系数 K（函数 <c>0x1C1A144</c>，对托板个数文本做 <c>Select Case</c>；
    /// 字面量：4→<c>0x401D48=1.25</c>、6→<c>0x4126F0=1.275</c>、8→<c>0x401D50=1.3</c>、
    /// 10→<c>0x4126E8=1.31</c>、12→<c>0x4123C0=1.32</c>）。
    /// </summary>
    private static readonly Dictionary<int, double> LugCoef = new()
    {
        [4] = 1.25,
        [6] = 1.275,
        [8] = 1.30,
        [10] = 1.31,
        [12] = 1.32,
    };

    /// <summary>
    /// 面板种类 → 默认板厚（函数 <c>0x1BE2557–0x1BE2749</c>，对面板种类文本做 <c>Select Case</c>，
    /// 仅在板厚输入为空时落值）。字符串常量 <c>0x4CD7B4/0x4CF63C/0x4CF648/0x4CF698/0x4CF664/
    /// 0x4CF674/0x4CF61C/0x4CF62C/0x4CF654</c>。
    /// </summary>
    private static readonly Dictionary<string, double> DefaultThickness = new()
    {
        ["石材"] = 30,
        ["瓷板"] = 25,
        ["陶板"] = 25,
        ["微晶玻璃板"] = 27,
        ["纤维水泥板"] = 25,
        ["超高性能混凝土（UHPC）板"] = 25,
        ["天然砂岩"] = 28,
        ["天然石灰石"] = 28,
        ["玻璃纤维板"] = 25,
    };

    /// <summary>
    /// 挂件材质 → （抗弯强度设计值 [σ] MPa，弹性模量 E MPa，挠度限值分母）。
    /// 实机逐项切换直读（<c>stone_material.txt</c>）：铝 6 项限值 = 2e/180 = 0.333（e=30），
    /// 304/316 不锈钢 = 2e/250 = 0.24。
    /// </summary>
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

    /// <summary>
    /// 螺栓有效直径 de（mm），参数库「螺栓1」表「有效直径」列原文；
    /// 有效面积 As = round1(π/4·de²)（实机 M8 → 36.6，M5 → 14.2，M6 → 20.1，M7 → 28.9，M10 → 58.0）。
    /// </summary>
    private static readonly Dictionary<double, double> BoltEffectiveDiameter = new()
    {
        [5] = 4.25314,
        [6] = 5.06015,
        [7] = 6.06756,
        [8] = 6.82819,
        [10] = 8.59566,
    };

    /// <summary>
    /// 螺栓材质 → （抗拉强度设计值 ft MPa，抗剪强度设计值 fv MPa）。
    /// 实机逐项切换直读（M8、As=36.6）：6222/5124 → 170/140 等（<c>stone_material.txt</c>）。
    /// </summary>
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

    /// <summary>挂件材质候选（窗体 <c>0x181,264</c> ComboBox，默认 316不锈钢）。</summary>
    public static IReadOnlyList<string> HangMaterialOptions { get; } =
        new[] { "6063-T5", "6063-T6", "6063A-T5", "6063A-T6", "6061-T4", "6061-T6", "304不锈钢", "316不锈钢" };

    /// <summary>螺栓材质候选（窗体 <c>0x389,390</c> ComboBox，默认 4.6-C级）。</summary>
    public static IReadOnlyList<string> BoltMaterialOptions { get; } = new[]
    {
        "碳钢：4.6-C级", "碳钢：4.8-C级", "碳钢：5.6-A级", "碳钢：5.6-B级", "碳钢：8.8-A级", "碳钢：8.8-B级",
        "不锈钢：A1-50级", "不锈钢：A2-50级", "不锈钢：A3-70级", "不锈钢：A4-70级", "不锈钢：A5-80级",
    };

    /// <summary>螺栓直径候选（窗体 <c>0x389,414</c> ComboBox，默认 8）。</summary>
    public static IReadOnlyList<double> BoltDiameterOptions { get; } = new[] { 5.0, 6.0, 7.0, 8.0, 10.0 };

    /// <summary>托板个数候选（窗体 <c>0x181,402</c> ComboBox，默认 4）。</summary>
    public static IReadOnlyList<int> LugCountOptions { get; } = new[] { 4, 6, 8, 10, 12 };

    // ───────────────────────── 输入 ─────────────────────────

    /// <summary>连接方式（界面 52,175 两侧连接 / 52,193 四边连接）。</summary>
    public enum ConnectMode
    {
        /// <summary>两侧连接（已闭合）。</summary>
        TwoSide = 0,
        /// <summary>四边连接（<b>未定位</b>，见 <see cref="Compute"/>）。</summary>
        FourSide = 1,
    }

    public sealed class Input
    {
        /// <summary>板块长边 A（mm），槽2。</summary>
        public double LongSideMm = 900;
        /// <summary>板块短边 B（mm），槽3。</summary>
        public double ShortSideMm = 800;
        /// <summary>板厚 t（mm），槽17。</summary>
        public double ThicknessMm = 25;
        /// <summary>短槽中心到板块边距（mm），槽36，界面 (181,219) 默认 150。</summary>
        public double EdgeDistanceMm = 150;
        /// <summary>槽口宽（mm），槽24，界面 (181,288) 默认 6。</summary>
        public double SlotMouthMm = 6;
        /// <summary>槽底长（mm），槽25，界面 (181,310) 默认 80。</summary>
        public double SlotBottomLengthMm = 80;

        /// <summary>每块板块托板个数，界面 (181,402) 默认 4；内部「每边托板数」= 个数/2。</summary>
        public int LugCount = 4;
        /// <summary>托板抗剪强度设计值（MPa），界面 (181,379) 显示 100。</summary>
        public double LugAllowableShearMpa = 100;

        /// <summary>挂件宽（mm），界面 (396,288) 默认 80。</summary>
        public double HangerWidthMm = 80;
        /// <summary>挂件厚（mm），界面 (396,311) 默认 3。</summary>
        public double HangerThicknessMm = 3;
        /// <summary>挂件螺栓中心到板边距离 e（mm），界面 (515,288) 默认 30。</summary>
        public double HangerEccentricityMm = 30;
        /// <summary>挂件长度 L（mm），界面 (515,311) 默认 60。</summary>
        public double HangerLengthMm = 60;
        /// <summary>挂件材质（8 项），界面 (398,264) 默认 316不锈钢。</summary>
        public string HangerMaterial = "316不锈钢";

        /// <summary>每块板块承水平荷载挂件数，界面 (473,334) 默认 2。</summary>
        public int HorizontalHangerCount = 2;
        /// <summary>每块板块承重挂件数，界面 (437,357) 默认 2。</summary>
        public int LoadHangerCount = 2;
        /// <summary>每挂件螺栓数，界面 (539,414) 默认 1。</summary>
        public int BoltPerHanger = 1;
        /// <summary>螺栓材质（11 项），界面 (389,390) 默认 4.6-C级。</summary>
        public string BoltMaterial = "碳钢：4.6-C级";
        /// <summary>螺栓公称直径（5/6/7/8/10），界面 (389,414) 默认 8。</summary>
        public double BoltDiameterMm = 8;

        /// <summary>石材重度 γ（kN/m³），由面板种类材料记录读出，石材默认 30。</summary>
        public double StoneDensityKnM3 = 30;
        /// <summary>地震影响系数最大值 αmax，槽5（参数库「地区参数」，默认北京 0.16）。</summary>
        public double AlphaMax = 0.16;
        /// <summary>面板口径风荷载标准值 wk（N/mm²），槽8。★必须传 N/mm²，不要用 kPa/1000。</summary>
        public double Wk = 0.00137;

        /// <summary>石材抗弯强度设计值 [σ]（MPa），界面 (240,59) 默认 3.72。</summary>
        public double StoneAllowableBendingMpa = 3.72;
        /// <summary>石材抗剪强度设计值 [τ]（MPa），界面 (240,86) 默认 1.86。</summary>
        public double StoneAllowableShearMpa = 1.86;

        /// <summary>连接方式。</summary>
        public ConnectMode Mode = ConnectMode.TwoSide;
    }

    // ───────────────────────── 输出 ─────────────────────────

    public sealed class Result
    {
        /// <summary>面板部分风荷载标准值（KPa，2 位）。</summary>
        public double WindStandardKpa;

        /// <summary>自重标准值 g_k = γ·t/1e6（N/mm²，槽6，不取整）。</summary>
        public double SelfWeightMpa;
        /// <summary>水平地震作用标准值 = round6(5·αmax·g_k)（N/mm²，槽7）。</summary>
        public double SeismicMpa;
        /// <summary>面荷载设计值 q_d = round6(1.5·wk + 0.7·地震)（N/mm²，槽9）。</summary>
        public double Qd;

        /// <summary>长边 − 2·边距（mm，槽10，未取 min）。</summary>
        public double CalcShortRawMm;
        /// <summary>石材计算短边 = min(槽10, 短边)（mm，槽12）。</summary>
        public double CalcShortMm;
        /// <summary>石材计算长边 = max(槽10, 短边)（mm，槽13）。</summary>
        public double CalcLongMm;
        /// <summary>比值 r = round3(计算短边/计算长边)（槽15）。</summary>
        public double Ratio;
        /// <summary>弯矩系数 m = round4(表插值)（槽16）。</summary>
        public double MCoef;

        /// <summary>石材强度计算值 σ = round3(6·m·q_d·计算长边²/t²)（MPa，槽18）。</summary>
        public double StressComputed;
        /// <summary>石材抗弯强度设计值（MPa，槽14）。</summary>
        public double StressAllowable;
        /// <summary>σ ≤ [σ]。</summary>
        public bool StressOk;

        /// <summary>石材抗剪计算值 τ = round3(K·q_d·A·B/(每边托板数·(t−槽口宽)·槽底长))（MPa，槽28）。</summary>
        public double ShearComputed;
        /// <summary>石材抗剪强度设计值（MPa，槽21）。</summary>
        public double ShearAllowable;
        /// <summary>τ ≤ [τ]。</summary>
        public bool ShearOk;

        /// <summary>托板剪应力 = round3(K·q_d·A·B/(2·每边托板数·托板截面积))（MPa）。</summary>
        public double LugShearComputed;
        /// <summary>托板抗剪强度设计值（MPa）。</summary>
        public double LugShearAllowable;
        /// <summary>托板剪应力 ≤ 许用值。</summary>
        public bool LugShearOk;

        /// <summary>挂件抗弯强度计算值 = round3(V/W + M/W_e)（MPa）。</summary>
        public double HangerStressComputed;
        /// <summary>挂件抗弯强度设计值（MPa，按材质）。</summary>
        public double HangerStressAllowable;
        /// <summary>挂件抗弯满足。</summary>
        public bool HangerStressOk;

        /// <summary>挂件挠度计算值 = round3(G_每挂件·e²·L/(3·E·I))（mm）。</summary>
        public double HangerDeflectionComputed;
        /// <summary>挂件挠度设计值 = round3(2e/分母)，分母 180（铝）/250（钢）。</summary>
        public double HangerDeflectionAllowable;
        /// <summary>挂件挠度满足。</summary>
        public bool HangerDeflectionOk;

        /// <summary>螺栓抗拉承受力计算值 = round3(M/(L−e)/每挂件螺栓数)（N）。</summary>
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

        // ── 过程量（列表用） ──
        /// <summary>抗剪系数 K（托板个数查表）。</summary>
        public double LugCoef;
        /// <summary>每边托板数 = 托板个数/2。</summary>
        public double LugPerSide;
        /// <summary>托板截面积 = 挂件宽 × 挂件厚（mm²）。</summary>
        public double LugAreaMm2;
        /// <summary>每个承重挂件承担的自重标准值（N）。</summary>
        public double LoadPerHanger;
        /// <summary>挂件螺栓拉力 N_v = 1.3·G_每挂件（N）。</summary>
        public double HangerTensionN;
        /// <summary>挂件弯矩 M = N_v·e（N·mm）。</summary>
        public double HangerMomentNmm;
        /// <summary>每个水平挂件承担的剪力 V（N）。</summary>
        public double HangerShearN;
        /// <summary>挂件截面抵抗矩 W = b·t（mm³）。</summary>
        public double HangerSectionMm3;
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

    /// <summary>
    /// 计算石材板块屏全部输出。
    ///
    /// <para><b>四边连接分支未定位</b>：<c>four_side</c> 扰动组的槽值已被证明写入错控件
    /// （槽2/3 互换、槽22=1.86、输出「计算长边:None」），数据作废；在没有可用运行期证据前
    /// 按规程<b>不实现、不拟合</b>，调用即抛 <see cref="NotSupportedException"/>。</para>
    /// </summary>
    public static Result Compute(Input it)
    {
        if (it is null) throw new ArgumentNullException(nameof(it));
        if (it.Mode == ConnectMode.FourSide)
            throw new NotSupportedException(
                "石材板块「四边连接」分支算式未定位（four_side 扰动数据作废，无运行期证据），按规程不实现、不拟合。");

        var r = new Result
        {
            WindStandardKpa = WindLoadEngine.VbaRound(it.Wk * 1000.0, 2),
        };

        // ① 荷载：自重 → 地震 → 设计值（0x1BE27C2–0x1BE2ADC）
        r.SelfWeightMpa = it.StoneDensityKnM3 * it.ThicknessMm / 1e6;              // 槽6，div 后直存不取整
        r.SeismicMpa = WindLoadEngine.VbaRound(5.0 * it.AlphaMax * r.SelfWeightMpa, 6);   // 槽7
        r.Qd = WindLoadEngine.VbaRound(1.5 * it.Wk + 0.7 * r.SeismicMpa, 6);      // 槽9

        // ② 计算边长（0x1BE2C6B–0x1BE2D32）：槽10 = 长边 − 2×边距
        r.CalcShortRawMm = it.LongSideMm - 2.0 * it.EdgeDistanceMm;
        r.CalcShortMm = Math.Min(r.CalcShortRawMm, it.ShortSideMm);                // 槽12
        r.CalcLongMm = Math.Max(r.CalcShortRawMm, it.ShortSideMm);                // 槽13
        r.Ratio = WindLoadEngine.VbaRound(r.CalcShortMm / r.CalcLongMm, 3);        // 槽15

        // ③ 石材强度（0x1BE3330–0x1BE36BB）
        r.MCoef = WindLoadEngine.VbaRound(MOf(r.Ratio), 4);                        // 槽16
        r.StressComputed = WindLoadEngine.VbaRound(
            6.0 * r.MCoef * r.Qd * r.CalcLongMm * r.CalcLongMm
            / (it.ThicknessMm * it.ThicknessMm), 3);                               // 槽18
        r.StressAllowable = it.StoneAllowableBendingMpa;
        r.StressOk = r.StressComputed <= r.StressAllowable;

        // ④ 石材抗剪 + 托板剪应力（0x1BE3FAB–0x1BE482C）
        //    托板截面积复用挂件截面：扰动证明改挂件宽→托板剪应力变（bw=100 得 1.856=2227.5/(4×300)），
        //    改挂件厚→bt=4 得 1.74=2227.5/(4×320)；界面 (181,356) 的 240 即 80×3。
        r.LugCoef = LugCoefOf(it.LugCount);
        r.LugPerSide = it.LugCount / 2.0;                                          // 槽23
        r.LugAreaMm2 = WindLoadEngine.VbaRound(it.HangerWidthMm * it.HangerThicknessMm, 3);
        double demand = r.LugCoef * r.Qd * it.LongSideMm * it.ShortSideMm;          // 2227.5（基线）

        r.ShearComputed = WindLoadEngine.VbaRound(
            demand / (r.LugPerSide * (it.ThicknessMm - it.SlotMouthMm) * it.SlotBottomLengthMm), 3);  // 槽28
        r.ShearAllowable = it.StoneAllowableShearMpa;
        r.ShearOk = r.ShearComputed <= r.ShearAllowable;

        r.LugShearComputed = WindLoadEngine.VbaRound(
            demand / (2.0 * r.LugPerSide * r.LugAreaMm2), 3);
        r.LugShearAllowable = it.LugAllowableShearMpa;
        r.LugShearOk = r.LugShearComputed <= r.LugShearAllowable;

        // ⑤ 挂件与螺栓（0x1BE4D9E–0x1BE8403）
        double e = it.HangerEccentricityMm;
        double L = it.HangerLengthMm;

        // 自重按承重挂件数分配（标准值），再 ×1.3 得设计拉力
        r.LoadPerHanger = WindLoadEngine.VbaRound(
            r.SelfWeightMpa * it.LongSideMm * it.ShortSideMm / it.LoadHangerCount, 3);
        r.HangerTensionN = r.LoadPerHanger * 1.3;
        r.HangerMomentNmm = WindLoadEngine.VbaRound(r.HangerTensionN * e, 3);

        // 水平力按水平挂件数分配
        r.HangerShearN = 1.25 * r.Qd * it.LongSideMm * it.ShortSideMm / it.HorizontalHangerCount;

        r.HangerSectionMm3 = WindLoadEngine.VbaRound(it.HangerWidthMm * it.HangerThicknessMm, 3);
        r.HangerModulusMm3 = WindLoadEngine.VbaRound(
            it.HangerWidthMm * Math.Pow(it.HangerThicknessMm, 2) / 6.0, 3);
        r.HangerInertiaMm4 = WindLoadEngine.VbaRound(
            it.HangerWidthMm * Math.Pow(it.HangerThicknessMm, 3) / 12.0, 3);

        var hm = HangProps(it.HangerMaterial);
        r.HangerElasticModulusMpa = hm.E;

        r.HangerStressComputed = WindLoadEngine.VbaRound(
            r.HangerShearN / r.HangerSectionMm3 + r.HangerMomentNmm / r.HangerModulusMm3, 3);
        r.HangerStressAllowable = hm.Fb;
        r.HangerStressOk = r.HangerStressComputed <= r.HangerStressAllowable;

        r.HangerDeflectionComputed = WindLoadEngine.VbaRound(
            r.LoadPerHanger * e * e * L / (3.0 * hm.E * r.HangerInertiaMm4), 3);
        r.HangerDeflectionAllowable = WindLoadEngine.VbaRound(2.0 * e / hm.Div, 3);
        r.HangerDeflectionOk = r.HangerDeflectionComputed <= r.HangerDeflectionAllowable;

        // 螺栓：抗拉 = M/(L−e)/每挂件螺栓数；抗剪 = V/每挂件螺栓数
        double lever = L - e;
        r.BoltTensionComputed = lever <= 0
            ? 0
            : WindLoadEngine.VbaRound(r.HangerMomentNmm / lever / it.BoltPerHanger, 3);
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

    /// <summary>托板个数 → 抗剪系数 K（<c>0x1C1A144</c>）。</summary>
    public static double LugCoefOf(int lugCount)
        => LugCoef.TryGetValue(lugCount, out var v) ? v : 1.25;

    /// <summary>面板种类 → 默认板厚（仅在板厚为空时使用，<c>0x1BE2557</c>）。</summary>
    public static double DefaultThicknessOf(string species)
        => DefaultThickness.TryGetValue(species ?? string.Empty, out var v) ? v : 25;

    /// <summary>挂件材质 → （[σ], E, 挠度分母）。</summary>
    public static (double Fb, double E, double Div) HangProps(string material)
        => HangMaterialTable.TryGetValue(material ?? string.Empty, out var v) ? v : HangMaterialTable["316不锈钢"];

    /// <summary>螺栓材质 → （ft, fv）。</summary>
    public static (double Ft, double Fv) BoltProps(string material)
        => BoltMaterialTable.TryGetValue(material ?? string.Empty, out var v)
            ? v
            : BoltMaterialTable["碳钢：4.6-C级"];

    /// <summary>螺栓有效面积 As = round1(π/4·de²)（de 取参数库「螺栓」表）。</summary>
    public static double BoltArea(double diameterMm)
    {
        double de = BoltEffectiveDiameter.TryGetValue(diameterMm, out var d) ? d : 6.82819;
        return WindLoadEngine.VbaRound(Math.PI / 4.0 * de * de, 1);
    }

    /// <summary>
    /// m(x) 分段线性插值 + 钳位，取 4 位。
    /// 内核（<c>0x1C34A1D–0x1C34A64</c>）：<c>round4(v1 − (v1−v2)·(r1−x)/(r1−r2))</c>；
    /// x&lt;0.5 钳位 0.1303（<c>0x1C34781 fld 0x4128B0</c>）。
    /// </summary>
    private static double MOf(double x)
    {
        if (x <= RatioX[0]) return MStone[0];
        if (x >= RatioX[RatioX.Length - 1]) return MStone[MStone.Length - 1];

        int i = 0;
        while (i < RatioX.Length - 2 && x > RatioX[i + 1]) i++;

        double r1 = RatioX[i], r2 = RatioX[i + 1];
        double v1 = MStone[i], v2 = MStone[i + 1];
        return v1 - (v1 - v2) * (r1 - x) / (r1 - r2);
    }
}
