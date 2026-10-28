using System;

namespace GlassManagement.Calc.Engines;

/// <summary>单元式横梁计算结果（上横框 / 下横框，与 V31「单元式·无中横」的 17 个数字一一对应）。</summary>
public sealed record UnitBeamResult(
    double Qk,            // 风荷载线荷载标准值 N/mm
    double Qy,            // 水平向线荷载设计值 N/mm
    double Mx,            // 竖向弯矩 N·mm（全归上框）
    double My,            // 水平向弯矩 N·mm（上下框各一半）
    double Vh2,           // 水平剪力的一半 N（上下框各承担）
    double Vv,            // 竖向剪力 N（只上框）
    double SigmaTop,      // 上框抗弯 MPa
    double SigmaBottom,   // 下框抗弯 MPa
    double TauTopX,       // 上框抗剪 tx MPa（V31 文案「上横框部分抗剪强度计算值tx」）
    double TauTopY,       // 上框抗剪 ty MPa
    double TauBottomX,    // 下框抗剪 tx MPa
    double TauBottomY,    // 下框抗剪 ty MPa（恒 0：不承担自重）
    double DeflectionV,   // 垂直向挠度 mm
    double DeflectionH,   // 水平向挠度 mm（组合：上下框惯性矩相加）
    double LimitV,        // 垂直向允许挠度 mm = B/250
    double LimitH,        // 水平向允许挠度 mm = round3(B/180)
    double U,             // 形状参数 r/(2B)
    // —— 中横框段（仅「有中横」时非 null）——
    double? MidSigma = null,       // 中横框抗弯 MPa
    double? MidDeflectionH = null, // 中横框水平向挠度 mm
    double? MidDeflectionV = null, // 中横框垂直向挠度 mm
    double? MidTauX = null,        // 中横框 τx MPa
    double? MidTauY = null,        // 中横框 τy MPa
    double? MidMx = null);         // 中横框竖向弯矩 N·mm

/// <summary>
/// 单元式幕墙横梁（上横框 + 下横框）。
///
/// <para>
/// **与构件式是两套模板**：构件式一套 12 个量；单元式无中横 17 个量、有中横 27 个量。
/// 本文只实现**上下框部分**（17 量）——中横框追加段（+10 量）尚未取证，见文末 TODO。
/// </para>
///
/// <para>
/// **取证**：Frida 挂 <c>rtcRound</c> 取定稿点，单元式横梁全序列落在 <c>0xA05312–0xA0A2DB</c>：
/// <code>
/// 0xA02839  Ix 115800 / 0xA02A0B Iy 123700 / 0xA02BBD Wx1 4100 / 0xA02DAF Wx2 3660
/// 0xA02F81  Wy1 5760 / 0xA03153 Wy2 6640 / 0xA03874 Sx 2994 / 0xA03A46 Sy 3546
/// 0xA05312  qk 1.4135→1.414    0xA053F7 qd 2.121
/// 0xA05581  g×5αmax 0.00024    0xA0576A ×1.4 → 0.37    0xA05885 q_y 2.306
/// 0xA05A1B/0xA05BB8  qk/2 = 0.707（上下框各一半）
/// 0xA05D55/0xA05EF2  q_y/2 = 1.153
/// 0xA066E0/0xA06988  M_y/2 = 116260.833   0xA06834 M_x = 81372.5   0xA06ADC 0
/// 0xA06E7D  上框 42.417096      0xA0721E  下框 20.184172
/// 0xA073E3  水平挠度 0.996188   0xA097D3  垂直挠度 0.973651
/// 0xA098FF  317.075（水平剪力） 0xA09A2B  295.9（竖向剪力）
/// 0xA09BC1  上 τx 3.029771     0xA09D57  上 τy 1.530094
/// 0xA0A145  下 τx 3.029771     0xA0A2DB  下 τy 0
/// </code>
/// </para>
///
/// <para>
/// **物理自检（与帮助 <c>html/B03.htm</c> 一致）**：非挂式横梁承**上部**板块自重 ⇒
/// 竖向弯矩 M_x 与竖向剪力 V_v **只进上框**（下框 σ 无 M_x 项、τy 恒 0），
/// 水平风载由上下框**各承担一半**。
/// </para>
///
/// <para>
/// **一处易错**：水平向挠度分母是 <c>E·(Iy上 + Iy下)</c>（两框惯性矩**相加**），
/// 不是取单框或平均 —— V31 文案叫「组合材料的挠度」即指此。
/// </para>
/// </summary>
public static class UnitBeamEngine
{
    /// <summary>铝弹性模量 N/mm²。</summary>
    public const double E = 70000.0;

    /// <summary>地震放大系数（与构件式同源：<c>0x8AED5D</c> 的 5）。</summary>
    public const double SeismicFactor = 5.0;

    /// <summary>竖向自重活荷载系数（<c>0x8CEAE9</c> 同构件式）。</summary>
    public const double VerticalFactor = 1.3;

    /// <summary>
    /// 中横框自重取值高度 mm = **1350**（实测钉死的程序内置值）。
    /// 取证（Frida 实参）：<c>0x96797A2</c> 直接 <c>1350 × 0.0003 = 0.405</c>，
    /// 而全局槽只有 <c>[3]=上分格</c>、<c>[4]=下分格</c>，**没有 1350**。
    /// 实测：上分格改 1500 / 1000、第一页第三组尺寸改 1500，中横 5 个量**全部不变**
    /// ⇒ 与界面任何尺寸框都无关，是常量。来源（为何是 1350）未定位。
    /// </summary>
    public const double MidGridHeightMm = 1350.0;

    private static double R3(double v) => WindLoadEngine.VbaRound(v, 3);

    /// <summary>
    /// 单元式横梁（上下框）计算。
    /// </summary>
    /// <param name="wkKPa">风荷载标准值 kPa（第一页链路产出）。</param>
    /// <param name="B">跨度 mm = 左右分格宽取大。</param>
    /// <param name="H">上分格高 mm。</param>
    /// <param name="gravityNmm2">自重面荷载 N/mm²（参数库「幕墙自重参数」计算横梁时自重 / 1e6）。</param>
    /// <param name="alphaMax">参数库「地区参数」αmax。</param>
    /// <param name="top">上横框截面（参数库「单元横框一」）。</param>
    /// <param name="bottom">下横框截面（参数库「单元横框二」）。</param>
    /// <param name="ignoreSeismic">不考虑地震作用（复刻版修正 V31 该开关不生效的问题）。</param>
    public static UnitBeamResult Compute(
        double wkKPa, double B, double H, double gravityNmm2, double alphaMax,
        BeamProfile top, BeamProfile bottom, bool ignoreSeismic = false, BeamProfile? mid = null)
    {
        // ★ 单元式的**水平**荷载链一律按跨度 B 取从属宽度，不取 min(B,H)。
        //   2026-10-26 实机反证：上分格高改 1000（< B=1100）后 ——
        //   qk 仍为 1.414（=round3(1100×1.285/1000)，而非 round3(1000×1.285/1000)=1.285）、
        //   qy 仍 2.306、My 仍 232521.667、V_h/2 仍 317.075、M_y 侧全部不变；
        //   只有竖向量随 H 变：M_x 58987.5、V_v 214.5、df_v 0.706、τy上 1.109。
        //   构件式横梁才走 min(B,H) 的梯形分支（0x8CDF34）。
        double r = B;
        double u = r / (2.0 * B);
        double kq = ignoreSeismic ? 0.0 : SeismicFactor * alphaMax;

        // —— 线荷载（与构件式同式）——
        double qk = R3(r * wkKPa / 1000.0);
        double qd = R3(qk * 1.5);
        double gp = R3(r * gravityNmm2 * kq * 1.4) / 2.0;
        double qy = R3(qd + gp);

        // —— 弯矩 ——
        double my = qy * B * B * (3.0 - Math.Pow(r / B, 2.0)) / 24.0;
        double qx = R3(H * gravityNmm2 * VerticalFactor);
        double mx = qx * B * B / 8.0;
        double half = my / 2.0;

        // —— 抗弯：上框 = 水平一半 + 竖向全部；下框 = 只有水平一半 ——
        double sigmaTop = R3(half / top.WyBending + mx / top.WxBending);
        double sigmaBottom = R3(half / bottom.WyBending);

        // —— 剪力：水平一半；竖向只上框 ——
        double vh2 = qy * B / 8.0;
        double vv = qx * B / 2.0;

        // —— 剪应力：tx 用 Sy/(Iy·ty)，ty 用 Sx/(Ix·tx) ——
        double tauTopX = R3(vh2 * top.Sy / (top.Iy * top.Ty));
        double tauTopY = R3(vv * top.Sx / (top.Ix * top.Tx));
        double tauBottomX = R3(vh2 * bottom.Sy / (bottom.Iy * bottom.Ty));

        // —— 挠度 ——
        double limV = B / 250.0;
        double limH = R3(B / 180.0);
        double dfV = R3(5.0 * (H * gravityNmm2) * Math.Pow(B, 4.0) / (384.0 * E * top.Ix));
        double dfH = R3(qk * Math.Pow(B, 4.0) * (1.0 / 120.0) / (E * (top.Iy + bottom.Iy)));

        // 中横框用「完整」的水平剪力（不是上下框的一半）：0x96A708 实参 634.095
        // 中横的 qk 用 r=B（见下方 qkMid）；此处按 B 计算完整水平剪力
        double qkForFull = R3(B * wkKPa / 1000.0);
        double vhFull = 1.5 * qkForFull * B / 4.0
                        + (B * (B * gravityNmm2 * kq)) / 4.0 * 1.4 / 2.0;

        // —— 中横框（参数库「单元横框三」；自重高度用常量 1350，水平弯矩拿整份 M_y）——
        double? mSigma = null, mDfH = null, mDfV = null, mTauX = null, mTauY = null, mMx = null;
        if (mid is not null)
        {
            // ★ 两步取整（实测暴露）：0x96797A2 round3(1350×g)=0.405 → 0x969887 0.405×1.3=0.5265
            //   → 0x9698A9 round3=0.527。若合并成 round3(H·g·1.3) 会因浮点得 0.526，σ 差 0.042。
            double qxMid = R3(R3(MidGridHeightMm * gravityNmm2) * VerticalFactor);
            double mxMid = qxMid * B * B / 8.0;                                  // 0x9699DB
            // ★ 中横框的水平向用 r = B（固定按跨度），**不取 min(B,H)**：
            //   实测上分格改 1000（<B=1100）时上下框 qy 变、中横 62.147 不变；
            //   0x9696C0 实参 M_y=232521.667 即 qy·B²/12（r=B 时 (3−1)/24=1/12）。
            double qkMid = R3(B * wkKPa / 1000.0);
            double qdMid = R3(qkMid * 1.5);
            double gpMid = R3(B * gravityNmm2 * kq * 1.4) / 2.0;
            double qyMid = R3(qdMid + gpMid);
            double myMid = qyMid * B * B / 12.0;
            mSigma = R3(myMid / mid.WyBending + mxMid / mid.WxBending);          // 0x969D9E
            mDfH = R3(qkMid * Math.Pow(B, 4.0) * (1.0 / 120.0) / (E * mid.Iy)); // 0x969F63（单根 Iy）
            _ = my;
            mDfV = R3(5.0 * (MidGridHeightMm * gravityNmm2) * Math.Pow(B, 4.0)
                      / (384.0 * E * mid.Ix));                                   // 0x96A1CB
            mTauX = R3(vhFull * mid.Sy / (mid.Iy * mid.Ty));                     // 0x96ABD3
            // ★ 剪力走「未取整」的 qx —— 与弯矩链不同源。2026-10-26 槽数组直读定案：
            //   slot[119]=0.527（取整后，供 Mx/My/σ）   slot[153]=289.575 = 0.5265×B/2（供 τy）
            //   若误用取整后的 0.527：289.85 → τy=1.499（差 0.002）。
            //   反汇编 0x96A9AD 亦为 τy = round3( slot153 / slot124 / slot132 ) = 289.575/115800/5。
            double qxShear = MidGridHeightMm * gravityNmm2 * VerticalFactor;      // 0.5265，刻意不取整
            double vvMid2 = qxShear * B / 2.0;                                   // 289.575（= slot[153]）
            mTauY = R3(vvMid2 * mid.Sx / (mid.Ix * mid.Tx));                      // 0x96AA3D
            mMx = mxMid;
        }

        return new UnitBeamResult(qk, qy, mx, my, vh2, vv,
                                  sigmaTop, sigmaBottom,
                                  tauTopX, tauTopY, tauBottomX, 0.0,
                                  dfV, dfH, R3(limV), limH, u,
                                  mSigma, mDfH, mDfV, mTauX, mTauY, mMx);
    }

    // TODO(未取证，禁止先写公式)：中横框追加段 10 个量
    //   实测值（B=1100/H=1380、40/60、αmax=0.16）：抗弯 62.147、水平挠度 1.992、垂直挠度 0.952、
    //   τx 6.059、τy 1.497。用参数库「单元横框三（中横框）」表，跨度与荷载分配规则尚未取证；
    //   已知：有中横时上/下框的 17 个量与无中横**逐位相同**，即中横是独立追加段，不影响上下框。
}
