using System;
using System.Collections.Generic;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 背栓式点挂幕墙（花岗岩 / 瓷板 / 陶板 / 轻质陶瓷板 / 微晶玻璃 / 石材蜂窝板 / 纤维水泥板 / 木纤维板）板块计算引擎。
///
/// <para><b>算式来源</b>：V31 实机 <c>背栓式点挂幕墙&gt;花岗岩 → 板块计算</c> 屏（标题仍为「石材幕墙计算-板块计算」，
/// 782×536）的 Variant 实值轨迹（Frida 钩 <c>__vbaVarMul/Div/Add/Pow</c> 与 <c>rtcRound</c>，88 条 var + 28 定稿点）＋
/// 脱壳镜像 <c>0xE4F000–0xE56000</c>（板块计算）与 <c>0xE69C96–0xE69CD6</c>（m 表插值）反汇编 ＋
/// 拓孔直径/钻孔深度/背栓总数扰动直读。四面对拍见 <c>docs/V31_BACKBOLT_PLATE_UI.md</c>。</para>
///
/// <para><b>与短挂件人造板屏的关键差异</b>（这是独立引擎，不复用 <see cref="ManmadePlateEngine"/>）：</para>
/// <list type="bullet">
/// <item>背栓槽口剪应力 τ = 1.25·q_d·短边·长边/(n·π·d·(t−D))，n=背栓总数、d=钻孔深度、
///   t−D=面板厚度−前端拓孔直径（有效剪切长度）；</item>
/// <item>背栓抗拉 = 1.25·q_d·短边·长边/n；背栓抗拉设计值 = N/2.15；</item>
/// <item>背栓抗剪 = 1.3·G/2；背栓抗剪设计值 = 0.8·N/2.15；</item>
/// <item>m 表 15 断点（0.1…1.0），末 5 断点 0.144/0.147/0.150/0.153/0.156（短挂件是 0.142/…/0.152）；</item>
/// <item>材料重度 γ 表 8 种面板（花岗岩=28 等），与短挂件（瓷板/陶板/微晶玻璃）不同。</item>
/// </list>
/// </summary>
public sealed class BackboltPlateEngine
{
    // ───────────────────────── 系数表（常量池原文 / 实机直读） ─────────────────────────

    /// <summary>
    /// m 的比值断点（15 点，r = 小支承点间距 / 大支承点间距）。
    /// 常量池同短挂件：0.1(0x401DA0) 0.2(0x401918) 0.3(0x401970) 0.4(0x401910) 0.5(0x401928)
    /// 0.55(0x402998) 0.6(0x401938) 0.65(0x4028E0) 0.7(0x401930) 0.75(0x401D88)
    /// 0.8(0x401920) 0.85(0x401D78) 0.9(0x401D70) 0.95(0x401D58) 1.0(0x4019C0)。</summary>
    private static readonly double[] RatioX =
    {
        0.10, 0.20, 0.30, 0.40, 0.50, 0.55, 0.60, 0.65,
        0.70, 0.75, 0.80, 0.85, 0.90, 0.95, 1.00,
    };

    /// <summary>
    /// 背栓点挂弯矩系数 m（按 r 升序，<b>递增</b>）。
    /// 常量池：0.125(0x4029C0) 0.126(0x404B40) 0.127(0x404AE8) 0.129(0x404AE0) 0.13(0x404B30)
    /// 0.132(0x404B28) 0.134(0x404B20) 0.136(0x404B18) 0.138(0x404B10) 0.14(0x401980)
    /// 0.144(0x404B08) 0.147(0x404B00) 0.15(0x401978) 0.153(0x404AF8) 0.156(0x404AF0)。
    /// ★ 末 5 断点（0.8~1.0）与短挂件表（0.142/0.144/0.147/0.150/0.152）不同，不可混用。</summary>
    private static readonly double[] MBackbolt =
    {
        0.125, 0.126, 0.127, 0.129, 0.130, 0.132, 0.134, 0.136,
        0.138, 0.140, 0.144, 0.147, 0.150, 0.153, 0.156,
    };

    /// <summary>
    /// 面板种类 → 材料重度 γ（kN/m³）。
    /// 常量池（<c>0xE4F000–0xE4F600</c> Select Case，字符串比较 + fld 常量 ÷ 1e6）＋ BSTR 直读定名：
    /// 瓷板(0x4CF63C)→0x4049C0=23.5、木纤维板(0x4CF5FC)→0x404A10=12.7、
    /// 微晶玻璃板(0x4CF698)→0x4020F0=27.0、纤维水泥板(0x4CF664)→0x4049E8=16.7、
    /// 玻璃纤维板(0x4CF654)→0x4049E0=22.0、石材(0x4CD7B4)→0x402860=28.0、
    /// 轻质陶瓷板(0x4E0990)→0x4020D0=15.0、陶板(0x4CF648)→0x402858=24.0。</summary>
    private static readonly Dictionary<string, double> DensityKnM3 = new(StringComparer.Ordinal)
    {
        ["瓷板"] = 23.5,
        ["木纤维板"] = 12.7,
        ["微晶玻璃板"] = 27.0,
        ["纤维水泥板"] = 16.7,
        ["玻璃纤维板"] = 22.0,
        ["石材"] = 28.0,
        ["轻质陶瓷板"] = 15.0,
        ["陶板"] = 24.0,
    };

    /// <summary>
    /// 挂件材质 → （抗弯强度设计值 [σ] MPa，弹性模量 E MPa，挠度限值分母）。
    /// 与石材/人造板屏同构，实机直读：铝 6 项限值 = 2e/180 = 0.333（e=30），
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

    /// <summary>背栓总数候选（ComboBox，默认 4）。</summary>
    public static IReadOnlyList<int> BackboltTotalCountOptions { get; } = new[] { 4, 6, 8, 10, 12 };

    /// <summary>面板种类候选（背栓式点挂幕墙 8 种）。</summary>
    public static IReadOnlyList<string> PanelSpeciesOptions { get; } =
        new[] { "石材", "瓷板", "陶板", "轻质陶瓷板", "微晶玻璃板", "纤维水泥板", "木纤维板", "玻璃纤维板" };

    // ───────────────────────── 输入 ─────────────────────────

    public sealed class Input
    {
        /// <summary>面板种类（石材/瓷板/陶板/…），决定重度 γ 与默认强度。</summary>
        public string Species = "石材";

        /// <summary>板材总厚度 L（mm），界面 (284,65) 默认 25。</summary>
        public double ThicknessMm = 25;
        /// <summary>板材计算厚度（mm），界面 (295,89) 默认 25。</summary>
        public double CalcThicknessMm = 25;

        /// <summary>短边边长（mm），界面 (276,114) 默认 800。</summary>
        public double ShortSideMm = 800;
        /// <summary>长边边长（mm），界面 (276,138) 默认 900。</summary>
        public double LongSideMm = 900;

        /// <summary>较小支承点间距（mm），界面 (316,163) 默认 650。</summary>
        public double SmallSupportMm = 650;
        /// <summary>较大支承点间距（mm），界面 (316,187) 默认 800。</summary>
        public double LargeSupportMm = 800;

        /// <summary>背栓在面板中钻孔深度 d（mm），界面 (369,236) 默认 6。</summary>
        public double DrillDepthMm = 6;
        /// <summary>背栓在面板中前端拓孔直径 D（mm），界面 (348,211) 默认 15。</summary>
        public double HoleDiameterMm = 15;

        /// <summary>面板抗弯强度设计值 f（MPa），界面 (126,78) 默认 3.72（石材）。</summary>
        public double AllowableBendingMpa = 3.72;
        /// <summary>面板抗剪强度设计值 fv（MPa），界面 (126,99) 默认 1.86（石材）。</summary>
        public double AllowableShearMpa = 1.86;

        /// <summary>实测背栓受拉破坏承载力最小值 N（N），界面 (90,227) 默认 8000。</summary>
        public double BackboltTensionCapacityN = 8000;
        /// <summary>每个板块上背栓总数 n，界面 (87,170) 默认 4。</summary>
        public int BackboltTotalCount = 4;

        // ── 挂件参数（与石材/人造板屏同构） ──
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

        /// <summary>每块板块水平荷载挂件数，界面 (156,419) 默认 2。</summary>
        public int HorizontalHangerCount = 2;
        /// <summary>每块板块承重挂件数，界面 (119,442) 默认 2。</summary>
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

        /// <summary>背栓在面板槽口剪应力 τ = round3(1.25·q_d·短边·长边/(n·π·d·(t−D)))（MPa）。</summary>
        public double SlotShearComputed;
        /// <summary>面板抗剪承载力设计值（MPa）。</summary>
        public double SlotShearAllowable;
        /// <summary>τ ≤ [τ]。</summary>
        public bool SlotShearOk;

        /// <summary>背栓抗拉 = round3(1.25·q_d·短边·长边/n)（N）。</summary>
        public double BackboltTensionComputed;
        /// <summary>背栓抗拉承载力设计值 = round3(N/2.15)（N）。</summary>
        public double BackboltTensionAllowable;
        /// <summary>背栓抗拉满足。</summary>
        public bool BackboltTensionOk;

        /// <summary>自重标准值 G = round3((γ/1e6)·短边·长边·t)（N）。</summary>
        public double SelfWeightN;
        /// <summary>背栓抗剪 = round3(1.3·G/2)（N）。</summary>
        public double BackboltShearN;
        /// <summary>背栓抗剪承载力设计值 = round3(0.8·N/2.15)（N）。</summary>
        public double BackboltShearAllowable;
        /// <summary>背栓抗剪满足。</summary>
        public bool BackboltShearOk;

        /// <summary>螺栓抗剪承受力 V = 1.25·q_d·短边·长边/水平挂件数（N）。</summary>
        public double BoltShearN;

        /// <summary>挂件抗弯强度计算值 = round3(V/Av + M/W_e)（MPa）。</summary>
        public double HangerStressComputed;
        /// <summary>挂件抗弯强度设计值（MPa，按材质）。</summary>
        public double HangerStressAllowable;
        /// <summary>挂件抗弯满足。</summary>
        public bool HangerStressOk;

        /// <summary>挂件挠度计算值 = round3(N_v·e³·2/(3·E·I))（mm）。</summary>
        public double HangerDeflectionComputed;
        /// <summary>挂件挠度设计值 = round3(2e/分母)。</summary>
        public double HangerDeflectionAllowable;
        /// <summary>挂件挠度满足。</summary>
        public bool HangerDeflectionOk;

        /// <summary>螺栓抗拉承受力计算值（N）。</summary>
        public double BoltTensionComputed;
        /// <summary>螺栓抗拉承受力设计值（N）。</summary>
        public double BoltTensionAllowable;
        /// <summary>螺栓抗拉满足。</summary>
        public bool BoltTensionOk;

        /// <summary>螺栓抗剪承受力计算值（N）。</summary>
        public double BoltShearComputed;
        /// <summary>螺栓抗剪承受力设计值（N）。</summary>
        public double BoltShearAllowable;
        /// <summary>螺栓抗剪满足。</summary>
        public bool BoltShearOk;

        /// <summary>拉剪复合 = round6((N/N_d)² + (V/V_d)²)。</summary>
        public double BoltCombined;
        /// <summary>拉剪复合 ≤ 1。</summary>
        public bool BoltCombinedOk;

        // ── 过程量 ──
        /// <summary>挂件截面面积 Av = b·t（mm²）。</summary>
        public double HangerSectionMm2;
        /// <summary>挂件截面模量 W_e = b·t²/6（mm³）。</summary>
        public double HangerModulusMm3;
        /// <summary>挂件惯性矩 I = b·t³/12（mm⁴）。</summary>
        public double HangerInertiaMm4;
        /// <summary>挂件弯矩 M = N·e（N·mm）。</summary>
        public double HangerMomentNmm;
        /// <summary>每承重挂件自重标准值 N_v（N）。</summary>
        public double LoadPerHanger;
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

        // ① 荷载：地震 → 设计值（0xE4F7B3–0xE4F9AF）
        double densityPerMm = r.DensityKnM3 / 1e6;                 // γ/1e6
        r.SeismicMpa = WindLoadEngine.VbaRound(5.0 * it.AlphaMax * densityPerMm * it.ThicknessMm, 6);
        r.Qd = WindLoadEngine.VbaRound(1.5 * it.Wk + 0.7 * r.SeismicMpa, 6);

        // ② 比值 + m（0xE4FB62 / 0xE69C96）
        r.Ratio = WindLoadEngine.VbaRound(it.SmallSupportMm / it.LargeSupportMm, 3);
        r.MCoef = WindLoadEngine.VbaRound(MOf(r.Ratio), 4);

        // ③ 抗弯（0xE4FD5A–0xE4FDD6）：σ = 6·m·q_d·大支承点²/t²
        r.StressComputed = WindLoadEngine.VbaRound(
            6.0 * r.MCoef * r.Qd * it.LargeSupportMm * it.LargeSupportMm
            / (it.ThicknessMm * it.ThicknessMm), 3);
        r.StressAllowable = it.AllowableBendingMpa;
        r.StressOk = r.StressComputed <= r.StressAllowable;

        // ④ 背栓槽口剪应力（0xE51FB6–0xE5208F）：
        //    τ = 1.25·q_d·短边·长边/(n·π·d·(t−D))
        //    ★ π 用 3.14（VB6 常量 0x402DD8 = 3.14，非 Math.PI 3.14159…）
        double shortLong = it.ShortSideMm * it.LongSideMm;
        double effectiveShearLength = it.ThicknessMm - it.HoleDiameterMm;   // t − D
        r.SlotShearComputed = WindLoadEngine.VbaRound(
            1.25 * r.Qd * shortLong
            / (it.BackboltTotalCount * 3.14 * it.DrillDepthMm * effectiveShearLength), 3);
        r.SlotShearAllowable = it.AllowableShearMpa;
        r.SlotShearOk = r.SlotShearComputed <= r.SlotShearAllowable;

        // ⑤ 背栓抗拉（0xE50F7C–0xE510C5）
        r.BackboltTensionComputed = WindLoadEngine.VbaRound(
            1.25 * r.Qd * shortLong / it.BackboltTotalCount, 3);
        r.BackboltTensionAllowable = WindLoadEngine.VbaRound(it.BackboltTensionCapacityN / 2.15, 3);
        r.BackboltTensionOk = r.BackboltTensionComputed <= r.BackboltTensionAllowable;

        // ⑥ 自重 + 背栓抗剪（0xE513E9–0xE51954）
        r.SelfWeightN = WindLoadEngine.VbaRound(
            densityPerMm * it.ShortSideMm * it.LongSideMm * it.ThicknessMm, 3);
        r.BackboltShearN = WindLoadEngine.VbaRound(1.3 * r.SelfWeightN / 2.0, 3);
        r.BackboltShearAllowable = WindLoadEngine.VbaRound(
            0.8 * it.BackboltTensionCapacityN / 2.15, 3);
        r.BackboltShearOk = r.BackboltShearN <= r.BackboltShearAllowable;

        // ⑦ 螺栓抗剪 + 挂件抗弯（0xE52624–0xE5381E）
        r.BoltShearN = 1.25 * r.Qd * shortLong / it.HorizontalHangerCount;

        // 挂件几何（0xE52928 / 0xE52FD4–0xE52FEF）
        r.HangerSectionMm2 = WindLoadEngine.VbaRound(it.HangerWidthMm * it.HangerThicknessMm, 3);
        r.HangerModulusMm3 = WindLoadEngine.VbaRound(
            it.HangerWidthMm * Math.Pow(it.HangerThicknessMm, 2) / 6.0, 3);
        r.HangerInertiaMm4 = WindLoadEngine.VbaRound(
            it.HangerWidthMm * Math.Pow(it.HangerThicknessMm, 3) / 12.0, 3);

        var hm = HangProps(it.HangerMaterial);

        // 挂件弯矩 M = N·e（0xE52EBF：327.6×30=9828）
        r.HangerMomentNmm = WindLoadEngine.VbaRound(r.BackboltShearN * it.HangerEccentricityMm, 3);
        r.HangerStressComputed = WindLoadEngine.VbaRound(
            r.BoltShearN / r.HangerSectionMm2 + r.HangerMomentNmm / r.HangerModulusMm3, 3);
        r.HangerStressAllowable = hm.Fb;
        r.HangerStressOk = r.HangerStressComputed <= r.HangerStressAllowable;

        // ⑧ 挂件挠度（0xE53EBC–0xE53F83）：w = N_v·e³·2/(3·E·I)
        //    N_v = 自重 G/承重挂件数（0xE52C1E–0xE52C56：0.0007×800×900/2=252）
        r.LoadPerHanger = densityPerMm * it.ThicknessMm * it.ShortSideMm * it.LongSideMm / it.LoadHangerCount;
        double e = it.HangerEccentricityMm;
        r.HangerDeflectionComputed = WindLoadEngine.VbaRound(
            r.LoadPerHanger * e * e * e * 2.0 / (3.0 * hm.E * r.HangerInertiaMm4), 3);
        r.HangerDeflectionAllowable = WindLoadEngine.VbaRound(2.0 * e / hm.Div, 3);
        r.HangerDeflectionOk = r.HangerDeflectionComputed <= r.HangerDeflectionAllowable;

        // ⑨ 螺栓（0xE543B5–0xE559AA）
        r.BoltTensionComputed = WindLoadEngine.VbaRound(r.BackboltShearN / it.BoltPerHanger, 3);
        r.BoltShearComputed = WindLoadEngine.VbaRound(r.BoltShearN / it.BoltPerHanger, 3);

        var bm = BoltProps(it.BoltMaterial);
        double boltArea = BoltArea(it.BoltDiameterMm);
        r.BoltTensionAllowable = WindLoadEngine.VbaRound(boltArea * bm.Ft, 3);
        r.BoltShearAllowable = WindLoadEngine.VbaRound(boltArea * bm.Fv, 3);
        r.BoltTensionOk = r.BoltTensionComputed <= r.BoltTensionAllowable;
        r.BoltShearOk = r.BoltShearComputed <= r.BoltShearAllowable;

        r.BoltCombined = WindLoadEngine.VbaRound(
            Math.Pow(r.BoltTensionComputed / r.BoltTensionAllowable, 2)
            + Math.Pow(r.BoltShearComputed / r.BoltShearAllowable, 2), 6);
        r.BoltCombinedOk = r.BoltCombined <= 1.0;

        return r;
    }

    // ───────────────────────── 辅助 ─────────────────────────

    /// <summary>面板种类 → 材料重度 γ（kN/m³）。未知种类回落石材 28.0。</summary>
    public static double DensityOf(string species)
        => DensityKnM3.TryGetValue(species ?? string.Empty, out var v) ? v : 28.0;

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
    /// 内核同短挂件：<c>round4(v1 − (v1−v2)·(r1−x)/(r1−r2))</c>；
    /// x&lt;0.1 钳位取 0.125，x&gt;1.0 钳位取 0.156。</summary>
    private static double MOf(double x)
    {
        if (x <= RatioX[0]) return MBackbolt[0];
        if (x >= RatioX[RatioX.Length - 1]) return MBackbolt[MBackbolt.Length - 1];

        int i = 0;
        while (i < RatioX.Length - 2 && x > RatioX[i + 1]) i++;

        double r1 = RatioX[i], r2 = RatioX[i + 1];
        double v1 = MBackbolt[i], v2 = MBackbolt[i + 1];
        return v1 - (v1 - v2) * (r1 - x) / (r1 - r2);
    }
}
