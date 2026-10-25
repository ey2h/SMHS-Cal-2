using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 横梁型材截面参数（单位：mm 制）。
/// 参数库「横框」表原始单位是 cm⁴ / cm³ / cm²，V31 读入后 ×10⁴（惯性矩）/ ×10³（抵抗矩、面积矩），
/// 腹板总宽度 tx / ty 本身是 mm 不换算。
/// </summary>
public sealed record BeamProfile(
    string Code,
    double Ix,      // mm^4
    double Iy,      // mm^4
    double Wx1,     // mm^3
    double Wx2,     // mm^3
    double Wy1,     // mm^3
    double Wy2,     // mm^3
    double Sx,      // mm^3（面积矩）
    double Sy,      // mm^3
    double Tx,      // mm（腹板总宽度 tx）
    double Ty,      // mm（腹板总宽度 ty）
    double Area)    // mm^2
{
    /// <summary>抗弯抵抗矩取 min（与立柱、钢铝结合同一取法；60/200 系列可区分）。</summary>
    public double WxBending => Math.Min(Wx1, Wx2);

    /// <summary>绕 y 轴抗弯抵抗矩同样取 min（40/60：min(5763, 6639)=5763 可证）。</summary>
    public double WyBending => Math.Min(Wy1, Wy2);
}

/// <summary>横梁计算结果，与 V31 横梁页「预演」输出的 12 个量一一对应。</summary>
public sealed record BeamResult(
    double Qk,          // 线荷载标准值 N/mm
    double Qy,          // 水平向线荷载设计值 N/mm（含自重）
    double Mx,          // 竖向弯矩 N·mm
    double My,          // 水平向弯矩 N·mm
    double WxRequired,  // 绕 x 轴抗弯预选 mm^3
    double WyRequired,  // 绕 y 轴抗弯预选 mm^3
    double IxRequired,  // 竖向惯性矩预选 mm^4
    double IyRequired,  // 水平向惯性矩预选 mm^4
    double DeflectionV, // 竖向挠度 mm
    double DeflectionH, // 水平向挠度 mm
    double LimitV,      // 竖向允许挠度 mm
    double LimitH,      // 水平向允许挠度 mm
    double Sigma,       // 抗弯 MPa
    double TauH,        // 水平向剪应力 MPa（V31 打印标签是 tx，方向相反）
    double TauV,        // 竖向剪应力 MPa（V31 打印标签是 ty）
    double U)           // 形状参数 min(H,B)/(2B)
{
}

/// <summary>
/// 玻璃幕墙横梁（水平向受风、竖向受自重）计算。
///
/// <para>
/// **全部公式来自 V31 脱壳镜像 + Frida 运行期实参取证**（2026-10-25）。
/// 取证方法：挂 <c>__vbaVarMul/Div/Add/Sub/Pow</c> 读每次运算的「实参 → 出参」（语义 <c>out = c2 op c1</c>）。
/// </para>
///
/// <para>
/// **关键结构发现：程序有两条分支，且分界不是"两个函数"，而是同一段代码用两个不同的 min(B,H) 取值点。**
/// <list type="bullet">
/// <item>B ≤ H（竖向长条格，三角形分布）：函数区 <c>0x8AEA00–0x8AF3xx</c>，
///       线荷载按 <b>B</b> 取（<c>0x8AEB0F</c>：1100×0.001285=1.4135）。</item>
/// <item>B &gt; H（横向长条格，梯形分布）：整体换到 <c>0x8CDF34</c> 起，
///       线荷载按 <b>H</b> 取（<c>0x8CDF34</c>：1000×0.001264=1.264 → 槽 [14]=2.064）。</item>
/// </list>
/// 两者可统一为 <c>r = min(B, H)</c>：B ≤ H 时 r=B、B &gt; H 时 r=H，公式形式完全相同。
/// 判定依据不是显式比较指令，而是两分支各自的实参来源（槽 [2]=B、[3]=H 分别被读入）。
/// </para>
///
/// <para>
/// **水平向系数随 B/H 连续变化（这是"梯形"的量化）**：
/// <list type="bullet">
/// <item>弯矩：<c>M_y = q_y·B²·(3 − (r/B)²)/24</c>
///       （<c>0x8CE8BE</c> r=H/B → <c>0x8CE8D2</c> r² → <c>0x8CE8DF</c> 3−r² → <c>0x8CE900</c> /24）。
///       r=B 时给 2/24 = **1/12**（三角形）；r→0 时趋近 3/24 = **1/8**（均布）。</item>
/// <item>挠度：<c>k = (3.125 − 5u² + 2u⁴)/240</c>，<c>u = r/(2B)</c>
///       （<c>0x8D2DB7</c> H/2 → <c>0x8D2DD6</c> u → <c>0x8D2DF7</c> 5u² → <c>0x8D2E04</c> 3.125−5u²
///       → <c>0x8D2E64</c> u⁴ → <c>0x8D2E7E</c> +2u⁴ → <c>0x8D2E9F</c> /240）。
///       u=0.5（即 r=B，三角形）时给 2/240 = **1/120**，与 B ≤ H 分支完全一致。</item>
/// </list>
/// </para>
///
/// <para>
/// **对拍 64/64 逐位一致**（<c>local-data/recon/verify_beam_trap.py</c>）：
/// 1100×1380（三角形）、1500×1000、2000×800、1200×1000（中间比）、1000×1000（临界 r=B）、
/// 以及**上海地区**（αmax=0.08，验证 5αmax 联动）。
/// </para>
///
/// <para>
/// **四个容易踩的点**：
/// <list type="bullet">
/// <item>竖向与水平向的荷载高度不同：竖向 <c>q_x = round3(H·g·1.3)</c> 恒用<b>上分格高 H</b>
///       （非挂式横梁承上部板块自重，帮助 <c>html/B03.htm</c> 原文），水平向用 <c>r=min(B,H)</c>。</item>
/// <item><b>水平向加的那一项是地震作用，不是自重</b>：系数 <c>5·αmax</c>
///       （<c>0x8AED5D</c> 的 <c>0.16×5</c>，0.16 读自全局 <c>0x1C913D8</c> = 参数库「地区参数」表 αmax）。
///       改地区到上海（αmax 0.16→0.08）后该系数 0.8→0.4，Wy 预选 2583.574→3007.074 可证。</item>
/// <item><c>q_y = round3(qd + gp)</c> 要多取一次整：1.6255 → 1.626 才会进弯矩
///       （<c>0x8CE664</c> 得 1.6255，<c>0x8CE886</c> 用的是 1.626，2000×800 组唯一能暴露）。</item>
/// <item>剪力的风荷载项用 <b>B</b>（不是 r）：<c>V_h风 = 1.5·qk·B·(1−u)/2</c>
///       （<c>0x8D5324</c> 1500×1.264=1896 → <c>0x8D539E</c> /2=632 → <c>0x8D5483</c> ×1.5=948）；
///       地震项才用 r：<c>B·(r·g·5αmax)·(1−u)/2·1.4·0.5</c>（<c>0x8D5617</c> 1500×0.24=360 → 84）。</item>
/// <item>打印标签 <c>tx</c> 对应<b>水平向</b>剪应力、<c>ty</c> 对应<b>竖向</b>（与直觉相反），
///       且 <c>τ_h = V_h·Sy/(Iy·ty)</c>、<c>τ_v = V_v·Sx/(Ix·tx)</c>（各自用对面方向的腹板宽）。</item>
/// </list>
/// </para>
/// </summary>
public static class HorizontalBeamEngine
{
    /// <summary>铝弹性模量 N/mm²（横梁页型材均为铝框）。</summary>
    public const double E = 70000.0;

    /// <summary>荷载分项系数（<c>0x8CE019</c>）。</summary>
    public const double LoadFactor = 1.5;

    /// <summary>
    /// **地震作用放大系数 5**（<c>0x8AED5D</c> / <c>0x8CE32C</c>：`0.16 × 5 = 0.8` 中的 5）。
    /// 与之相乘的 0.16 是全局 <c>0x1C913D8</c>，实测等于参数库「地区参数」表的 <b>αmax</b>：
    /// 北京(8 度) αmax=0.16 → 0.8；把地区改成上海(7 度) αmax=0.08 后该系数变 0.4，
    /// 横梁 Wy 预选 2583.574 → 3007.074、σ 62.568 → 69.182（2026-10-25 实机联动验证）。
    /// 因此 <b>水平向 q_y 里加的这项是「地震作用」，不是自重</b>。
    /// </summary>
    public const double SeismicFactor = 5.0;

    /// <summary>地震作用分项系数（<c>0x8CE534</c>）。</summary>
    public const double DeadFactor = 1.4;

    /// <summary>竖向自重活荷载系数（<c>0x8CEAE9</c>：1.3）。</summary>
    public const double VerticalFactor = 1.3;

    /// <summary>竖向挠度限值分母（<c>0x8D0612</c>：B/250）。</summary>
    public const double LimVDiv = 250.0;

    /// <summary>水平向挠度限值分母（<c>0x8D00FA</c>：B/180）。</summary>
    public const double LimHDiv = 180.0;

    private static double R3(double v) => WindLoadEngine.VbaRound(v, 3);

    /// <summary>
    /// 横梁计算。
    /// </summary>
    /// <param name="wkKPa">风荷载标准值 kPa（第一页算出的 wk，原样传入，不在此覆盖）。</param>
    /// <param name="B">横梁跨度 mm = 立柱左右两侧分格宽取<b>大</b>值（帮助：自动选最大一跨，不计立柱宽）。</param>
    /// <param name="H">自重高度 mm = <b>上</b>分格高（非挂式横梁承上部板块自重，不取平均）。</param>
    /// <param name="gravityNmm2">自重面荷载 N/mm²（参数库「幕墙自重参数」计算横梁时自重 / 1e6）。</param>
    /// <param name="alphaMax">
    /// 地震影响系数最大值 αmax（参数库「地区参数」表同名列；北京 8 度 0.16、上海 7 度 0.08）。
    /// 水平向的地震作用 = 5·αmax·g。
    /// </param>
    /// <param name="f">抗弯强度设计值 MPa。</param>
    /// <param name="p">型材截面。</param>
    public static BeamResult Compute(
        double wkKPa, double B, double H, double gravityNmm2, double alphaMax, double f, BeamProfile p)
    {
        // —— 形状参数：r = min(B,H) 决定"线荷载取多宽"，u = r/(2B) 决定分布形状 ——
        double r = Math.Min(B, H);
        double u = r / (2.0 * B);
        double sw = 1.0 - u;

        // —— 水平向线荷载（用 r 取宽）+ 地震作用（5αmax·g）——
        double qk = R3(r * wkKPa / 1000.0);
        double qd = R3(qk * LoadFactor);
        double kq = SeismicFactor * alphaMax;
        double gp = R3(r * gravityNmm2 * kq * DeadFactor) / 2.0;
        double qy = R3(qd + gp);   // ★ 二次取整，2000×800 组可暴露

        // —— 水平向弯矩：系数 (3 − (r/B)²)/24，r=B 时退化为 1/12 ——
        double kM = (3.0 - Math.Pow(r / B, 2.0)) / 24.0;
        double my = qy * B * B * kM;
        double wyReq = my / f;

        // —— 竖向弯矩：恒用上分格高 H，均布 /8 ——
        double qx = R3(H * gravityNmm2 * VerticalFactor);
        double mx = qx * B * B / 8.0;
        double wxReq = mx / f;

        // —— 挠度限值：都以跨度 B 为基准 ——
        double limV = B / LimVDiv;
        double limH = R3(B / LimHDiv);

        // —— 水平向挠度系数：(3.125 − 5u² + 2u⁴)/240，u=0.5 时退化为 1/120 ——
        double kDf = (3.125 - 5.0 * u * u + 2.0 * Math.Pow(u, 4.0)) / 240.0;
        double iyReq = qk * Math.Pow(B, 4.0) * kDf / (E * limH);
        double dfH = qk * Math.Pow(B, 4.0) * kDf / (E * p.Iy);

        // —— 竖向挠度：5/384 均布，荷载用标准值 H·g（不带 1.3）——
        double ixReq = 5.0 * (H * gravityNmm2) * Math.Pow(B, 4.0) / (384.0 * E * limV);
        double dfV = 5.0 * (H * gravityNmm2) * Math.Pow(B, 4.0) / (384.0 * E * p.Ix);

        // —— 剪力：竖向 V_v = H·(B·g·1.3)/2（两分支同式）——
        double vv = H * (B * gravityNmm2 * VerticalFactor) / 2.0;
        // 水平：风项用 B，地震项用 r
        double vh = LoadFactor * qk * B * sw / 2.0
                    + B * (r * gravityNmm2 * kq) * sw / 2.0 * DeadFactor * 0.5;

        // —— 抗弯：两方向正应力直接相加（非平方和、非取大）——
        double sigma = R3(mx / p.WxBending + my / p.WyBending);

        // —— 剪应力：hp 用 Sy/Iy/ty，vp 用 Sx/Ix/tx ——
        double tauH = R3(vh * p.Sy / (p.Iy * p.Ty));
        double tauV = R3(vv * p.Sx / (p.Ix * p.Tx));

        return new BeamResult(qk, qy, mx, my, wxReq, wyReq, ixReq, iyReq,
                              R3(dfV), R3(dfH), R3(limV), limH,
                              sigma, tauH, tauV, u);
    }

    /// <summary>自重面荷载换算：参数库值 N/m² → N/mm²。</summary>
    public static double GravityFromLibrary(double nPerM2) => nPerM2 / 1e6;
}
