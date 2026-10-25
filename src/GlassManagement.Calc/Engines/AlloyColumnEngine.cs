using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 一根钢铝结合型材的截面参数（单位：mm 制）。
/// 参数库「钢铝结合铝立柱 / 钢铝结合钢立柱」两表原始单位是 cm⁴ / cm³ / cm²，
/// V31 读入后分别 ×10⁴ / ×10³ 换算成 mm（定稿点 <c>0xCE08FA</c> 读入 153.297 → 1532970 可证）。
/// </summary>
public sealed record AlloyProfile(
    string Code,
    double Ix,          // mm^4
    double Iy,          // mm^4
    double Wx1,         // mm^3
    double Wx2,         // mm^3
    double WebThickness,// mm
    double Area,        // mm^2
    double Sx,          // mm^3（面积矩）
    double WebWidth);   // mm（受剪腹板总宽度）

/// <summary>钢铝结合立柱的计算结果（与 V31 预演输出的一一对应）。</summary>
public sealed record AlloyColumnResult(
    double Qk,            // 线荷载标准值 N/mm
    double Qd,            // 线荷载设计值 N/mm
    double RatioAlStd,    // 铝（标准值）份额
    double RatioAlDes,    // 铝（设计值）份额
    double RatioStDes,    // 钢（设计值）份额
    double MAl,           // 铝承担弯矩 N·mm
    double MSt,           // 钢承担弯矩 N·mm（已除 1.05）
    double SigmaAl,       // MPa
    double SigmaSt,       // MPa
    double VAl, double VSt,
    double TauAl, double TauSt,
    double Deflection,    // mm
    double Limit)         // mm
{
}

/// <summary>
/// 钢铝结合立柱（铝型材 + 钢芯共同受弯）。
///
/// <para>
/// **全部公式来自 V31 脱壳镜像 <c>0xCDF000–0xCE7000</c> 的指令级 + Frida 运行期实值取证**
/// （钢铝结合页「预演」的 42 个 rtcRound 定稿点全部落在 <c>0xCE08FA–0xCE6BE2</c>）。
/// 取证链：
/// <list type="number">
/// <item>先按输出模板串（"铝框抗弯强度计算值" 等）在镜像里定位常量池，
///       再用 <c>v31_frida_rounds.py</c> 全范围挂 <c>rtcRound</c> 取返回地址聚类 → 函数区间。</item>
/// <item>用 <c>v31_frida_ops.py</c> 挂 <c>__vbaVarMul/Div/Add/Pow</c> 读每次运算的
///       「实参 → 出参」实值（<c>out = c2 op c1</c>），逐段还原算式；
///       再用 <c>v31_slice2.py</c> 做 x87 + 槽数组符号执行交叉印证。</item>
/// <item>槽数组 <c>ds:[0x1C91184]</c> 的索引在代码里是常量：<c>[0x02]=L</c>，
///       <c>[0x2e]=铝受剪腹板宽</c>，<c>[0x2f]=钢受剪腹板宽</c>（<c>0xCE3BBD</c>/<c>0xCE3C14</c> 处可见）。</item>
/// </list>
/// </para>
///
/// <para>
/// **对拍 60/60 逐位一致**（<c>local-data/recon/verify_alloy.py</c>）：
/// 铝 60/80·60/100·60/140·60/200 四个系列、钢 槽钢［5 / ［6.3 / ［8 / ［12.6、
/// L=2500 / 3000 / 3500 / 8000（后者走 &gt;7500 的 L/500 分支）。
/// </para>
///
/// <para>
/// **三个容易踩的点**：
/// <list type="bullet">
/// <item>铝与钢的「1.05 调整」方向相反：铝的份额乘 1.05，钢的弯矩最后除以 1.05。
///       （代码事实：<c>0xCE2CD0</c> 处铝侧 Variant 常量 1.05；<c>0xCE43C9</c> 处钢侧 fdiv 1.05。）</item>
/// <item>钢的份额用 <c>qd</c>（已含 1.5），**不是** <c>qk×1.5</c>；L=3500 那组是唯一能区分两者的样本
///       （1.4014×1.5 与 round3(1.4014)×1.5 差 0.0001，最终 σ 差 0.058）。</item>
/// <item>抗弯抵抗矩取 <c>min(Wx1, Wx2)</c>：60/200 系列 Wx1=85.746 / Wx2=82.026，
///       V31 出 22.548 = M/82.026；若取 Wx1 则 21.57。钢立柱表 Wx1≡Wx2，无法区分，按同一逻辑取 min。</item>
/// </list>
/// </para>
/// </summary>
public static class AlloyColumnEngine
{
    /// <summary>铝弹性模量 N/mm²（V31 内置常量，不随牌号变：换 6061-T6 计算值不变可证）。</summary>
    public const double AluminumE = 70000.0;

    /// <summary>钢弹性模量 N/mm²（同上，Q355 也不变）。</summary>
    public const double SteelE = 206000.0;

    /// <summary>组合截面调整系数（铝乘、钢除）。V31 常量 1.05。</summary>
    public const double ComboFactor = 1.05;

    /// <summary>荷载分项系数 1.5（qd 链 <c>0xCE262A</c>）。</summary>
    public const double LoadFactor = 1.5;

    /// <summary>简支梁弯矩系数 1/8，挠度系数 5/384。</summary>
    private const double MomentDiv = 8.0;
    private const double DeflNum = 5.0;
    private const double DeflDen = 384.0;

    /// <summary>挠度限值：L≤7500 时 min(L/300, 15)；L&gt;7500 时 L/500。</summary>
    public const double LimitSwitchL = 7500.0;
    public const double LimitCapMm = 15.0;

    private static double R3(double v) => WindLoadEngine.VbaRound(v, 3);

    /// <summary>抗弯抵抗矩：V31 取 min(Wx1, Wx2)。</summary>
    public static double WxForBending(AlloyProfile p) => Math.Min(p.Wx1, p.Wx2);

    public static double LimitMm(double L)
        => R3(L <= LimitSwitchL ? Math.Min(L / 300.0, LimitCapMm) : L / 500.0);

    public static AlloyColumnResult Compute(
        double wkKPa, double bayMm, double LMm,
        AlloyProfile al, AlloyProfile st)
    {
        // —— 线荷载（第一页算出的 wk 传进来，不在这里覆盖）——
        double qk = R3(wkKPa / 1000.0 * bayMm);
        double qd = R3(qk * LoadFactor);

        // —— 组合截面刚度 ——
        double ea = AluminumE * al.Ix;
        double es = SteelE * st.Ix;
        double sei = ea + es;

        // —— 分配份额（铝标准值/设计值，钢设计值）——
        double rAlS = qk * ComboFactor * ea / sei;
        double rAlD = qd * ComboFactor * ea / sei;
        double rStD = qd * es / sei;

        // —— 弯矩：份额先取 3 位，再乘 L²/8，再取 3 位 ——
        double mAl = R3(R3(rAlD) * LMm * LMm / MomentDiv);
        double mSt = R3(R3(rStD) * LMm * LMm / MomentDiv) / ComboFactor;

        // —— 正应力 ——
        double sigAl = R3(mAl / WxForBending(al));
        double sigSt = R3(mSt / WxForBending(st));

        // —— 剪力：份额先取 3 位，乘 L 再取 3 位，最后 /2（不取整）——
        double vAl = R3(R3(rAlD) * LMm) / 2.0;
        double vSt = R3(R3(rStD) * LMm) / 2.0;

        // —— 剪应力 τ = Sx·V/(I·t) ——
        double tauAl = R3(al.Sx * vAl / al.Ix / al.WebWidth);
        double tauSt = R3(st.Sx * vSt / st.Ix / st.WebWidth);

        // —— 挠度：用铝的标准值份额 × 5L⁴/(384·Ea·Ia) ——
        double df = R3(DeflNum * R3(rAlS) * Math.Pow(LMm, 4) / (DeflDen * AluminumE * al.Ix));

        return new AlloyColumnResult(
            qk, qd, rAlS, rAlD, rStD, mAl, mSt, sigAl, sigSt,
            vAl, vSt, tauAl, tauSt, df, LimitMm(LMm));
    }
}
