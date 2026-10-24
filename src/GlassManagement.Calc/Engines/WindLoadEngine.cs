using System;

namespace GlassManagement.Calc.Engines;

/// <summary>地面粗糙度类别（对应界面「地面类型」A/B/C/D 类地形）。</summary>
public enum TerrainCategory
{
    A = 0,
    B = 1,
    C = 2,
    D = 3,
}

/// <summary>
/// 幕墙风荷载标准值 wk 计算（公式来源：V35 脱壳代码 + V31 实机五点验证）。
///
/// <para>wk = w0 · βgz(z) · μz(z) · [ μs1 · ψ(A) + μs_int ]</para>
///
/// <para>常量出处（unpacked_runtime.exe）：
/// βgz/μz 常量表 0x401E88–0x401F18；βgz 尾部 Pow→Div→Add(1) 在 0xA71840；
/// μz 四类块 0xA702E0(A)/0xA708CF(B)/0xA70EBA(C)/0xA714A9(D)；
/// 内部系数在 0xA73604/0xA736B9/0xA73810/0xA739AF（存 var[0x45]），
/// 大开孔档 = 0.8×μs1 在 0xA73BB2（对应文本「取0.8μ~s1~」）。
/// 内部档位按 GB50009-2012 8.3.5 取 0.2 / 0.4μs1 / 0.6μs1 / 0.8μs1（2026-10-24 已订正，见 MuSIntByOption）。</para>
///
/// <para>ψ(A) 从属面积折减：GB50009-2012 8.3.4（围护结构局部体型系数按 lg 插值到 25 m²，
/// μsl(25)=0.8μsl(1)）。已用 V31 两组分格实测反解，k=0.2/lg25 在 A=3.3 与 A=6.0 两点一致。</para>
/// </summary>
public static class WindLoadEngine
{
    /// <summary>JGJ102-2003：幕墙风荷载标准值不应小于 1.0 kN/m²。</summary>
    public const double WindStandardFloorKpa = 1.0;

    /// <summary>基本风压下限 0.3 kN/m²（程序文本「基本风压不得小于0.3KN/m^2」，0x4DAA2C）。</summary>
    public const double BasicWindFloorKpa = 0.3;

    /// <summary>峰值因子 g —— 国标 GB50009-2012 取 2.5（V35 常量 0x401F18 / V31 0x4019A0）。</summary>
    public const double PeakFactorGb = 2.5;

    /// <summary>
    /// 峰值因子 g —— 广东 DBJ/T 15-101 取 3.0（V35 常量 0x401F68 / V31 0x4019A8）。
    ///
    /// <para><b>★ 2026-10-24 V31 实测</b>（第一页「荷载规范」下拉 (537,304) 三档）：
    /// 0 全国荷载规范 / 1 广东(小于45米低矮) / 2 广东(大于45米高层)。
    /// 后两档在立柱屏数值**完全相同**（β=1.5947、μs合=1.226、wk=2.146），区别在别处。</para>
    /// </summary>
    public const double PeakFactorGd = 3.0;

    /// <summary>默认峰值因子（国标），保留给未指定规范的调用。</summary>
    public const double PeakFactor = PeakFactorGb;

    /// <summary>从属面积折减的参考面积（m²）：μsl 在 25 m² 处折到 0.8 倍。</summary>
    public const double AreaReferenceM2 = 25.0;

    /// <summary>μz 的 (系数, 指数)，程序原文 0x511500–0x5116A0。</summary>
    private static readonly double[] MuZCoef = { 1.284, 1.000, 0.544, 0.262 };
    private static readonly double[] MuZExp = { 0.24, 0.30, 0.44, 0.60 };

    /// <summary>βgz 的 (湍流度 I10, 指数 α)，常量表 0x401EB0–0x401F18。</summary>
    private static readonly double[] I10 = { 0.12, 0.14, 0.23, 0.39 };
    private static readonly double[] Alpha = { 0.12, 0.15, 0.22, 0.30 };

    /// <summary>各场地类别的 z 截断区间 (下限, 上限)，程序原文 0x511724–0x5118B0。</summary>
    private static readonly double[] ZMin = { 5, 10, 15, 30 };
    private static readonly double[] ZMax = { 300, 350, 450, 550 };

    /// <summary>
    /// ψ(A) 分母。<b>不是 lg(25)=1.39794，而是字面量 1.4。</b>
    /// <para><b>★ 2026-10-25 代码级 + 实测双重确证</b></para>
    /// <para>① 代码：V31 里 slot[46]（= μs1·ψ）唯一写入点 <c>0x00E2CE3E</c>，其计算链为
    /// <c>0x00E2CD45 fld ds:dbl_4017F8</c> —— 该地址存的就是 <b>1.4</b>，且整个镜像里 1.4 只出现这一次；
    /// 链路 = 读三个控件串 → <c>__vbaR8Str</c> → <c>fmul 0.8</c> → <c>fsubr</c>（得 0.8·μs1−μs1 = −0.2μs1）
    /// → <c>__vbaVarMul</c>(×slot[45]=lg A) → <c>__vbaVarDiv</c>(÷1.4) → <c>__vbaVarAdd</c>(+μs1) → <c>rtcRound(…,3)</c>。
    /// 即 <c>slot46 = round3( μs1 − 0.2·μs1·lg A / 1.4 )</c>。</para>
    /// <para>② 实测：6 个分格值，其中 5 个是「1.4 与 lg(25) 给出不同 3 位结果」的判别点，
    /// <b>6/6 全部落在 1.4 分支</b>（见 <c>local-data/recon/v31runs/v31_psidisc.psv</c>）。</para>
    /// </summary>
    public const double PsiLogDenominator = 1.4;

    /// <summary>lg(A) 先按 3 位取整（V31 slot[45]：A=3.3→0.519、A=3.9→0.591），再进 ψ 式子。</summary>
    public const int DigitsLgArea = 3;

    // ─────────────────────────────────────────────────────────────────────────
    // ★ V35 的逐步取整（rtcRound）。复刻原先全程不取整，导致"算对了但位数不同"：
    //   不复现这些 round，即使公式完全一致，wk 也会差 0.01%~0.03%。
    //
    // 取整点与位数全部来自 V35 脱壳镜像的 rtcRound 调用（thunk sub_419DDE = rtcRound）：
    //   βgz  → 4 位   0xA703BC / 0xA709AB / 0xA70F96 / 0xA71585（A/B/C/D 四块）
    //   μz   → 4 位   0xA70562 / 0xA70B4D / 0xA7113C / 0xA7172B（push 4 明确可见）
    //   β    → 4 位   0xA71936（max 之后，push 4 @0xA7191B）
    //   μsi  → 3 位   0xA73361 / 0xA73456（push 3）
    //   wk   → 3 位   V31 实机输出 1.848 / 2.029 / 1.571 / 1.277 全为 3 位小数
    //
    // 数值铁证（V31 运行期槽值 → 复刻同式验算）：
    //   B 类 z=100：0.55 × 1.4956(β,4位) × 1.9953(μz,4位) × 1.126(μs合,3位)
    //              = 1.848096902 → round3 = 1.8480 ＝ V31 的 1.848 ✅
    //   A 类 z=100：0.55 × 1.4686 × 2.2313 × 1.126
    //              = 2.029376231 → round3 = 2.0290 ＝ V31 的 2.029 ✅
    //   若不取整：0.55 × 1.495562 × 1.995262 × 1.12582 = 1.847719 → 1.848（碰巧同），
    //   但中间量 β/μz 与 V31 槽值就不是逐位相等了 —— 那正是之前对不齐的地方。
    // ─────────────────────────────────────────────────────────────────────────
    public const int DigitsBeta = 4;      // βgz / μz / β
    public const int DigitsMuS = 3;       // μsi / μs1·ψ+μsi
    public const int DigitsWk = 3;        // wk

    /// <summary>
    /// V31/V35 的 <c>rtcRound</c> 等价实现（<b>double</b> 语义，2026-10-25 实机定案）。
    ///
    /// <para><b>算法（用 V31 自己的 rtcRound 逐位对齐反推并验证 4/4 位模式全等）</b>：</para>
    /// <code>
    /// y = x;  重复 digits 次  y *= 10      // ★ 必须逐次乘 10，不能一次乘 10^digits
    /// n = 四舍六入五成双(y)
    /// return n / 10^digits                 // 除法，不是乘法
    /// </code>
    ///
    /// <para><b>为什么"逐次乘 10"是关键</b>：qd = rtcRound(qk×1.5) 的四个平局点，
    /// 一次乘 1000 与逐次乘 10 得到不同的末位：</para>
    /// <list type="table">
    /// <item><term>4.0514999999999999</term><description>×1000=4051.4999999999995→4051；逐次×10 得 4051.5 → 4052 ✅</description></item>
    /// <item><term>2.0474999999999999</term><description>×1000=2047.5（看似平局）→2048；逐次×10 得 2047.4999999999998 → 2047 ✅</description></item>
    /// <item><term>3.0495000000000001</term><description>→ 3050 ✅（V31 实际 3.05）</description></item>
    /// <item><term>4.1325000000000003</term><description>→ 4132（半偶）✅（V31 实际 4.132）</description></item>
    /// </list>
    ///
    /// <para>实证手段：Frida 直接调用 V31 的 <c>0x41371A</c>（见 <c>v31_frida_round.py</c> /
    /// <c>v31_frida_chain.py</c>），读回 VT_R8 位模式；本地按上式复现为
    /// 3.0499999999999998 / 4.0519999999999996 / 4.1319999999999997 / 2.0470000000000002，与 V31 逐位相同。</para>
    /// </summary>
    public static double VbaRound(double v, int digits)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) return v;

        // ★ 逐次乘 10：这是与 Math.Round(v, digits) 的唯一实质区别
        double y = v;
        for (int i = 0; i < digits; i++) y *= 10.0;

        double n = Math.Round(y, MidpointRounding.ToEven);

        double div = 1.0;
        for (int i = 0; i < digits; i++) div *= 10.0;
        return n / div;
    }

    /// <summary>V35 的 rtcRound（= <see cref="VbaRound"/>，double 语义）。</summary>
    private static double R(double v, int digits) => VbaRound(v, digits);

    /// <summary>按 V35 的 rtcRound 取整（供外部对齐位数用）。</summary>
    public static double RoundLikeV35(double v, int digits) => R(v, digits);

    private static int Idx(TerrainCategory t) => (int)t;

    /// <summary>按类别截断计算高度 z（m）。</summary>
    public static double ClampHeight(TerrainCategory t, double zM)
    {
        if (double.IsNaN(zM) || zM <= 0) zM = ZMin[Idx(t)];
        return Math.Min(Math.Max(zM, ZMin[Idx(t)]), ZMax[Idx(t)]);
    }

    /// <summary>风压高度变化系数 μz = C·(z/10)^p，结果按 V35 取 4 位（rtcRound @0xA70562 等）。</summary>
    public static double MuZ(TerrainCategory t, double zM)
    {
        int i = Idx(t);
        double z = ClampHeight(t, zM);
        return R(MuZCoef[i] * Math.Pow(z / 10.0, MuZExp[i]), DigitsBeta);
    }

    /// <summary>
    /// 阵风系数 βgz = 1 + 2·g·I10·(z/10)^(−α)，结果按 V35 取 4 位（rtcRound @0xA703BC 等）。
    /// GB50009-2012 8.6.1 / 条文说明 8.6.1。
    /// </summary>
    public static double BetaGz(TerrainCategory t, double zM, double peakFactor = PeakFactorGb)
    {
        int i = Idx(t);
        double z = ClampHeight(t, zM);
        return R(1.0 + 2.0 * peakFactor * I10[i] * Math.Pow(z / 10.0, -Alpha[i]), DigitsBeta);
    }

    /// <summary>
    /// 围护结构风荷载放大系数 β（<b>实际进 wk 的那个量</b>）。
    ///
    /// <para>β = max( βgz , 1 + 0.7/√μz )</para>
    ///
    /// <para>下限来源：<b>《工程结构通用规范》GB55001-2021 第 4.6.5 条第 2 款</b>——
    /// 「围护结构的风荷载放大系数应根据地形特征、脉动风特性和流场特征等因素确定，
    /// <b>且不应小于 1+0.7/√μz</b>，其中 μz 为风压高度变化系数」。</para>
    ///
    /// <para><b>★ 2026-10-24 找到的真正漏项</b>：复刻原先只取 βgz，漏了这条下限，
    /// 导致 A 类偏低 0.93%。之所以只有 A 类表现出来，是因为代数上
    /// 1+0.7/√μz = 1 + (0.7/√C)·(z/10)^(−p/2)，而 μz 的 C/p 与 βgz 的 I10/α 并非处处自洽：</para>
    /// <list type="table">
    /// <item><term>A</term><description>0.7/√1.284 = 0.6178 &gt; 2·g·I10 = 0.6000 → 下限胜出</description></item>
    /// <item><term>B</term><description>0.7/√1.000 = 0.7000 = 2·g·I10 = 0.7000 → 恰好相等</description></item>
    /// <item><term>C</term><description>0.7/√0.544 = 0.9490 &lt; 1.1500 → βgz 胜出</description></item>
    /// <item><term>D</term><description>0.7/√0.262 = 1.3675 &lt; 1.9500 → βgz 胜出</description></item>
    /// </list>
    ///
    /// <para>V31 实机 7 个点全部命中（槽 [0x1C91168]→slot[8]）：
    /// A/10=1.6178、A/50=1.5093、A/100=1.4686、B/10=1.7000、B/100=1.4956、
    /// C/10=2.0519、D/10=2.4025。</para>
    /// </summary>
    public static double Beta(TerrainCategory t, double zM, double peakFactor = PeakFactorGb)
    {
        // V35 读的是「已经取整并写进槽」的 μz（__vbaVarPow(slot[10], 0.5)），
        // 所以这里 MuZ() 的 4 位取整必须先发生 —— 顺序不能换。
        double bz = BetaGz(t, zM, peakFactor);
        double floor = 1.0 + 0.7 / Math.Sqrt(MuZ(t, zM));
        return R(Math.Max(bz, floor), DigitsBeta);   // rtcRound 4 位 @0xA71936
    }

    /// <summary>
    /// 从属面积折减系数 ψ(A) = 1 − 0.2·lg(A)/<b>1.4</b>。
    /// <para>★ 分母是 <b>1.4</b>，不是规范写法里的 lg(25)=1.39794 —— 见 <see cref="PsiLogDenominator"/> 的确证。
    /// 规范 GB50009-2012 8.3.4 的条文式写作 lg(25)，原程序用 1.4 近似，两者在多数 A 上同值，
    /// 但在「精确值落在 3 位取整边界」时会差 0.001（如 A=3.9：规范式 0.915 vs 原程序 0.916）。</para>
    /// <para>A ≤ 1 m² 不折减；A ≥ 25 m² 取 0.8（与 1.4 式在 A=25 处得 0.8003→0.800 一致）。</para>
    /// </summary>
    public static double Psi(double areaM2)
    {
        if (double.IsNaN(areaM2) || areaM2 <= 1.0) return 1.0;
        if (areaM2 >= AreaReferenceM2) return 0.8;
        double lgA = R(Math.Log10(areaM2), DigitsLgArea);      // slot[45]：lg A 取 3 位
        return 1.0 - 0.2 * lgA / PsiLogDenominator;
    }

    /// <summary>
    /// 局压体型系数总量。μs1 为外部（墙面区 1.0 / 墙角边区 1.4 / 人工给定），μsInt 为内部系数。
    /// GB50009-2012 8.3.5 把内部系数写成「0.4μs1 / 0.6μs1 / 0.8μs1」的倍率形式；
    /// 由于 μsInt 与 μs1 同量纲，实现上是「加在 μs1·ψ 上」，倍率部分由调用方按 μs1 折算。
    /// </summary>
    /// <para><b>★ 两级取整</b>：V31 先算 slot[46] = round3(μs1·ψ)，再由 slot[47] = round3(slot[46] + μsInt)。
    /// 一次算完再取整在边界上会差 0.001，故这里分两步。</para>
    public static double MuSTotal(double muS1, double muSInt, double areaM2)
    {
        double mu1psi = R(muS1 * Psi(areaM2), DigitsMuS);   // slot[46]
        return R(mu1psi + muSInt, DigitsMuS);               // slot[47]
    }

    /// <summary>slot[46]：μs1·ψ 单独取 3 位（诊断/留痕用，与 V31 槽一一对应）。</summary>
    public static double MuS1Psi(double muS1, double areaM2)
        => R(muS1 * Psi(areaM2), DigitsMuS);

    /// <summary>
    /// 风荷载标准值 wk（kPa）。w0 为基本风压（kPa，来自参数库地区参数表）。
    /// </summary>
    /// <param name="w0Kpa">基本风压（kPa）。小于 0.3 时按 0.3 取（程序下限）。</param>
    /// <param name="k1">地形修正系数（GB55001-2021 4.6.6，一般 1.0；闭塞地形≥0.75；谷口山口≥1.2）。</param>
    /// <param name="k2">风向影响系数（GB55001-2021 4.6.7，一般 1.0；有可靠资料时 0.8～1.0）。</param>
    /// <param name="applyFloor">是否套用 JGJ102 的 1.0 kPa 下限。</param>
    public static double Wk(double w0Kpa, TerrainCategory terrain, double zM,
                            double muS1, double muSInt, double areaM2,
                            bool applyFloor = true, double k1 = 1.0, double k2 = 1.0,
                            double peakFactor = PeakFactorGb)
        => R(WkRaw(w0Kpa, terrain, zM, muS1, muSInt, areaM2, applyFloor, k1, k2, peakFactor), DigitsWk);

    /// <summary>
    /// 未取整的风荷载标准值。
    ///
    /// <para><b>★ 为什么需要它</b>：V31 显示「风荷载标准值为:1.848KPa」是 round3 之后的字符串，
    /// 但<b>传给立柱链的是未取整值</b>。判别证据（三个场景必须同时成立，只有 raw 能全对）：</para>
    /// <list type="table">
    /// <item><term>基准</term><description>raw β·μz·μs 得 1.8480969 → qk=2.0329067→round3 2.033 → qd 3.05 → M=3431250 → wReq=25416.667 ✅</description></item>
    /// <item><term>墙角边区</term><description>raw 2.4553758 → qk=2.7009134→2.701 → qd 4.052 → M=4558500 → wReq=33766.667 ✅</description></item>
    /// <item><term>内 0.6μs1</term><description>raw 2.5046145 → qk=2.7550760→<b>2.755</b> → qd 4.132 → M=4648500 → wReq=34433.333 ✅<br/>
    /// 若改用 round3 的 2.505，qk 得 2.7555000000000001 → 2.756 → wReq=34450 ❌（与 V31 差 17）</description></item>
    /// </list>
    /// </summary>
    public static double WkRaw(double w0Kpa, TerrainCategory terrain, double zM,
                               double muS1, double muSInt, double areaM2,
                               bool applyFloor = true, double k1 = 1.0, double k2 = 1.0,
                               double peakFactor = PeakFactorGb)
    {
        double w0 = Math.Max(w0Kpa, BasicWindFloorKpa);
        // 完全不取整（连主式都不 round）—— 立柱链要的就是这个原值，见 WkRaw 的判别证据
        double wk = w0 * Beta(terrain, zM, peakFactor) * MuZ(terrain, zM)
                    * MuSTotal(muS1, muSInt, areaM2) * SafeFactor(k1) * SafeFactor(k2);
        return applyFloor ? Math.Max(wk, WindStandardFloorKpa) : wk;
    }

    /// <summary>
    /// 不含 K1/K2 的未取整主式值（w0·β·μz·μs合）。
    /// 立柱链要单独乘 K1/K2（V31 是先 round3 出 qk 再乘 K，见 <c>ColumnCalcViewModel</c>）。
    /// </summary>
    public static double WkMainRaw(double w0Kpa, TerrainCategory terrain, double zM,
                                   double muS1, double muSInt, double areaM2,
                                   double peakFactor = PeakFactorGb)
        => Math.Max(w0Kpa, BasicWindFloorKpa) * Beta(terrain, zM, peakFactor) * MuZ(terrain, zM)
           * MuSTotal(muS1, muSInt, areaM2);

    /// <summary>
    /// V35/V31 的十进制取整（银行家舍入）。
    ///
    /// <para><b>为什么必须用 decimal</b>：V31 的 rtcRound 在 Variant 的 Decimal 子类型上做十进制
    /// 银行家舍入，三个判别点只有「十进制精确值 + ToEven」能同时成立：</para>
    /// <list type="table">
    /// <item><term>3.0495</term><description>→ 3.050（第三位 9 奇，进位）基准 qd=3.05 ✅</description></item>
    /// <item><term>4.0515</term><description>→ 4.052（第三位 1 奇，进位）墙角 qd=4.052 ✅</description></item>
    /// <item><term>4.1325</term><description>→ 4.132（第三位 2 偶，保持）内0.6 qd=4.132 ✅</description></item>
    /// </list>
    /// <para>若用 double 的 Math.Round，.0495/.0515/.1325 的二进制表示误差会让三者至少错一个
    /// （实测 ToEven/AwayFromZero 两种模式都只能对上 2/3）。</para>
    /// </summary>
    public static decimal RoundDec(decimal v, int digits)
        => decimal.Round(v, digits, MidpointRounding.ToEven);

    /// <summary>K1/K2 兜底：非正数按 1.0（界面留空时不至于把 wk 算成 0）。</summary>
    private static double SafeFactor(double k) => (double.IsNaN(k) || k <= 0) ? 1.0 : k;

    /// <summary>
    /// 内部体型系数系数（倍率形式）。按界面 5 个选项取
    /// （1=封闭式、2=0.02&lt;开洞率≤0.10、3=0.10&lt;开洞率≤0.30、4=开洞率&gt;0.30、5=其它情况/人工给定）。
    ///
    /// <para>GB50009-2012 8.3.5：封闭式取 <b>0.2</b>（绝对值，不乘 μs1）；
    /// 仅一面墙有主导洞口时 0.02&lt;开洞率≤0.10 → <b>0.4μs1</b>、
    /// 0.10&lt;开洞率≤0.30 → <b>0.6μs1</b>、开洞率&gt;0.30 → <b>0.8μs1</b>。</para>
    ///
    /// <para>★2026-10-24 修正：原返回 0.3/0.4/0.6（整体低一档）。
    /// V35 与 V31 的内部系数链完全一致（0.2/0.3/0.4/0.6/0.8 五档，写 slot[0x45]/[0x46]，
    /// V35 在 0xA73604/0xA736B9/0xA73810/0xA739AF/0xA73B4E，V31 在 0x861459/0x86150E/0x86165F/0x8617F8/0x861991），
    /// 三个开洞率档实测反解为 0.4000 / 0.6000 / 0.8000（μs1=1.0、ψ=0.9258）。</para>
    ///
    /// <para>倍率语义实测：外部人工给定 μs1=1.5 + 本方法第 2 档 → V31 得 wk=3.265，
    /// 对应 μsi=0.4×1.5=0.6（若按绝对 0.4 只有 2.936），故第 2~4 档必须乘 μs1。</para>
    /// </summary>
    public static double MuSIntByOption(int oneBasedOption)
        => oneBasedOption switch
        {
            1 => 0.2,
            2 => 0.4,
            3 => 0.6,
            4 => 0.8,
            _ => 1.0,
        };

    /// <summary>该内部档位是否为「μs1 倍率」语义（封闭式与人工给定为绝对值）。</summary>
    public static bool MuSIntIsRatio(int oneBasedOption) => oneBasedOption is 2 or 3 or 4;
}
