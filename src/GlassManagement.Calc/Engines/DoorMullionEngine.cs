using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 门窗中竖挺/中横挺杆件本体计算引擎（V31「门窗系统计算」CW29 预演，输出块1/块2）。
/// 取证双证（Var 钩含后补的 __vbaVarSub@0x41377A，缺 Sub 曾是挠度系数"凭空出现"的根因）：
/// ① V31 实机 Var/round 轨迹 door_body_traces.json：
///    中竖挺 86 var / 20 round（0x1376F75–0x137D40C），中横挺 89 var / 27 round（0x1392013–0x139A0CA）；
///    材质扰动 door_body_perturbQ235.json：6063-T5→Q235 时 设计值 90→215 / 55→125 / E 70000→206000，
///    挠度 8.344→2.835（竖挺）、6.887→2.341（横挺水平）逐位联动。
/// ② IDA（unpacked_v31，VA−0x400000）：输出文案 BSTR 0x4EEB08/0x4EEB2C/0x4EEB4C/0x4EEB80
///    在 0x137D380–0x137D9D9 装载（输出语句区），与轨迹尾段 0x137D40C(wk×1000) 吻合。
/// 结构模型（每个常数都有轨迹地址）：
///   wk（杆件风荷载）= 玻璃预演 round6 下传，默认 0.001333 N/mm²（生成 0x14184AE→round6 0x14184D0）。
///   荷载：qk_i = round3(wk·Li/2)；qd_i = round( round3(qk_i·1.5) + 0.5·round(1.4·round(round6(5·0.16·α)·Li/2)) )
///         （竖挺外层 d3、横挺外层 d6，扰动 α=0 时全部 0；0x1376F75–0x1377A52 / 0x1392013–0x1392AF0）
///   弯矩：M1 = round3(qd1·Lt²/24·(3−(L1/Lt)²))（0x13784F0/0x13934BA）；
///         竖挺 M2 = round3(qd2·Lt²/24·(3−(L2/Lt)²))（0x1379569）；
///         横挺 M2 = round3(qd2·L²/12) 跨中模型（0x1393D24 Div 12）。
///   σ = round3((M1+M2)/γx/W)，γx=1（0x137A9A5 ÷1）。
///   剪力：V1 = round3(qd1·Lt/2·(1−L1/(2Lt)))；竖挺 V2 同式（0x13799F6）；
///         横挺 V2 = round3(qd2·L/4)（0x1393FC7 Div 4）。
///         τ = round3((V1+V2)·S/(I·tw))（÷'3' 字符串数字，0x137D076）。
///   挠度（两端固定）：f = round3( qk·Lt⁴/240/E/I · [ (3.125−5a²) + 2a⁴ ] )，a = Li/(2Lt)。
///         3.125 为程序浮点常数（Sub 0x137B8AC: 3.125−0.17578125→2.94921875 逐位实证）；
///         横挺第2组无系数、/120：f2 = round3(qk2·L⁴/120/E/I)（0x1397961）。
///   总挠度 = round3(f1+f2)（用 round3 后的 f1/f2）；限值 = round3(Lt/100)。
///   横挺垂直方向（自重，默认 0，链常数 650/×L²/÷8/×1.3/×5/÷384 实证 0x13B0E10–0x13B1384）：
///     截面改用 Wx2/Ix/Sx/tx（0x13963BA ÷2765、0x13B1362 ÷99690、0x1399D00 ×2962 实证）。
/// 已双证材料（f/fv/E MPa）：6063-T5 = 90/55/70000；Q235 = 215/125/206000。
/// 默认截面（参数库运行时数据，cm→mm 换算）：
///   立梃一（门窗用）"50系列"：Wx1=4.013→4013、Ix=15.444→154440、S=3.299→3299、tw=3；
///   横梃一（门窗用）"55"：Wy1=4.101→4101、Iy=14.235→142350、Sy=4.466→4466、ty=3、
///                         Wx2=2.765→2765、Ix=9.969→99690、Sx=2.962→2962、tx=3。
/// 诚实登记：地震项、横挺垂直方向自重链在 V31 默认态全 0——链常数与顺序可见但语义未全钉，
/// 按轨迹原序实现，默认 0 值逐位对拍成立；非 0 态未实测。
/// </summary>
public static class DoorMullionEngine
{
    /// <summary>
    /// 中梃材质全表（10 项，与 V31 竖/横中梃参数帧 OptionButton 一一对应）：
    /// 名称 → (抗弯强度设计值 f, 抗剪强度设计值 fv, 弹性模量 E) MPa。
    /// 全表由 door_mullion_matrix.json 逐材质遍历双证：f/fv 取输出「设计值」行原文；
    /// E 取挠度链 0x137B80C/0x137C566 Div 除数直读（70000/40000/2200/206000），
    /// 并经挠度值逐位联动验证（玻纤 14.603、塑钢 265.506、Q235 2.835）。
    /// 6061-T4 输出与 6063-T5 完全一致（E 链直读 70000 证实非缺料）。
    /// </summary>
    public static readonly (string Name, double F, double Fv, double E)[] Materials =
    {
        ("6061-T4", 90.0, 55.0, 70000.0),
        ("6061-T6", 200.0, 115.0, 70000.0),
        ("6063-T5", 90.0, 55.0, 70000.0),
        ("6063-T6", 150.0, 85.0, 70000.0),
        ("6063A-T5", 135.0, 75.0, 70000.0),
        ("6063A-T6", 160.0, 90.0, 70000.0),
        ("6060-T66", 135.0, 75.0, 70000.0),
        ("塑钢(仅塑材部分)", 37.0, 18.5, 2200.0),
        ("Q235", 215.0, 125.0, 206000.0),
        ("玻纤增强聚氨酯", 500.0, 40.0, 40000.0),
    };

    public static (double F, double Fv, double E) Material(string name)
    {
        foreach (var mm in Materials)
            if (mm.Name == name) return (mm.F, mm.Fv, mm.E);
        throw new ArgumentException("未知中梃材质: " + name);
    }

    private static double R(double v, int d) => WindLoadEngine.VbaRound(v, d);

    /// <summary>地震项链（竖挺 0x137723B–0x137755B / 横挺 0x13922D9–0x13925F9）：
    /// qE = 0.5·round_dNext(1.4·round_dMid(round6(5·αmax)·Gk_area·L/2))。
    /// probe11（上海 αmax=0.08、Gk_area=0.0003/0.0004 非 0 态）逐位钉死：
    /// 竖挺 (dMid,dNext)=(3,3)：round6(5·0.08)=0.4·0.0004·600/2=0.048→1.4·0.048=0.0672→round3 0.067→×0.5=0.0335；
    /// 横梃 (2,6)：0.4·0.0003·650/2=0.039→round2 0.04→1.4·0.04=0.056→×0.5=0.028。
    /// Gk_area=0（门窗线单独默认）时整链 0（基线逐位不变）。</summary>
    private static double SeismicQ(double alphaMax, double gkArea, double L, int dMid, int dNext)
        => 0.5 * R(1.4 * R(R(R(R(5 * alphaMax, 6) * gkArea, 6) * L / 2.0, dMid), dNext), 6);

    /// <summary>两端固定梁挠度：round3(q·Lt⁴/240/E/I·((3.125−5a²)+2a⁴))，a=ai/Lt。</summary>
    private static double DeflFixed(double q, double Lt, double aiMm, double E, double I)
    {
        double a = aiMm / Lt;
        double basePart = q * Math.Pow(Lt, 4) / 240.0 / E / I;          // 轨迹无中间舍入
        double coeff = (3.125 - 5.0 * Math.Pow(a, 2)) + 2.0 * Math.Pow(a, 4);
        return R(basePart * coeff, 3);
    }

    public enum DeflLimitMode
    {
        /// <summary>自动取值：限值 = round3(L/100)（默认；V/H 共用）。</summary>
        Auto100 = 0,
        /// <summary>人工给定（水平与自重一致）：H=V=round3(L/分母)。</summary>
        ManualSame = 1,
        /// <summary>人工给定（不一致）：H=round3(L/分母)、V=round3(L/500下拉)。</summary>
        ManualDiff = 2,
    }

    // ═══════════════════════ 中竖挺 ═══════════════════════

    public sealed class MullionInput
    {
        /// <summary>计算杆件时风荷载标准值 wk（N/mm²），玻璃预演 round6 下传，默认 0.001333。</summary>
        public double WkRodNmm2 = 0.001333;
        /// <summary>挠度限值模式（「计算门窗杆件时挠度取值」帧，door_modes_probe2 实证）。</summary>
        public DeflLimitMode LimitMode = DeflLimitMode.Auto100;
        /// <summary>人工给定分母（下拉 100/150，默认 150；L=1600→10.667 逐位实证）。</summary>
        public double LimitDivisor = 150.0;
        /// <summary>竖梃受荷跨度 L1（mm），可见「竖梃参数」TB(600)。</summary>
        public double SpanL1Mm = 600.0;
        /// <summary>竖梃间距 L2（mm），默认 1500。</summary>
        public double SpacingL2Mm = 1500.0;
        /// <summary>杆件计算跨度 Lt（mm），「竖梃参数」长度 TB，默认 1600。</summary>
        public double LengthLtMm = 1600.0;
        /// <summary>地震影响系数最大值 αmax（round6(5·αmax) 链实证：北京 0.16/上海 0.08；
        /// 门窗屏无输入控件，由第一页地区选中行经全局槽下传；未选=0）。</summary>
        public double SeismicAlphaMax = 0.0;
        /// <summary>水平地震玻璃面荷载标准值 Gk_area（N/mm²，probe11 上海幕墙立柱预演后=0.0004；
        /// 幕墙线预演写全局槽，门窗线单独时 0）。</summary>
        public double GkAreaNmm2 = 0.0;
        /// <summary>截面特性（mm）：50系列 Wx1/Ix/S/tw。</summary>
        public double WMm3 = 4013.0;
        public double IxMm4 = 154440.0;
        public double SMm3 = 3299.0;
        public double TwMm = 3.0;
        /// <summary>截面塑性发展系数 γx（轨迹 ÷1 实证）。</summary>
        public double GammaX = 1.0;
        public double FMpa = 90.0;
        public double FvMpa = 55.0;
        public double EMpa = 70000.0;
    }

    public sealed class MullionResult
    {
        public double WkKpa;            // 1.333
        public double FMpa, FvMpa;      // 设计值回显（90/55 或 215/125）
        public double Qk1, Qd1;         // 0.4 / 0.6
        public double Qk2, Qd2;         // 1 / 1.5
        public double M1, M2, Sigma;    // 183000 / 339375 / 130.171
        public double V1, V2, Tau;      // 390 / 637.5 / 7.316
        public double F1, F2, Defl, DeflLimit; // 2.982 / 5.362 / 8.344 / 16
        public bool SigmaOk, DeflOk, TauOk;
    }

    public static MullionResult ComputeMullion(MullionInput it)
    {
        var r = new MullionResult { FMpa = it.FMpa, FvMpa = it.FvMpa };
        double L1 = it.SpanL1Mm, L2 = it.SpacingL2Mm, Lt = it.LengthLtMm;
        r.WkKpa = R(it.WkRodNmm2 * 1000.0, 3);   // 0x137D40C

        // ── 荷载（0x1376F75–0x1377A52，外层 round3）──
        r.Qk1 = R(it.WkRodNmm2 * L1 / 2.0, 3);
        r.Qd1 = R(R(r.Qk1 * 1.5, 3) + SeismicQ(it.SeismicAlphaMax, it.GkAreaNmm2, L1, 3, 3), 3);
        r.Qk2 = R(it.WkRodNmm2 * L2 / 2.0, 3);
        r.Qd2 = R(R(r.Qk2 * 1.5, 3) + SeismicQ(it.SeismicAlphaMax, it.GkAreaNmm2, L2, 3, 3), 3);

        // ── 弯矩：两端固定 M=qd·Lt²/24·(3−(Li/Lt)²)（0x13784F0–0x137A9C1）──
        r.M1 = R(r.Qd1 * Math.Pow(Lt, 2) / 24.0 * (3.0 - Math.Pow(L1 / Lt, 2)), 3);
        r.M2 = R(r.Qd2 * Math.Pow(Lt, 2) / 24.0 * (3.0 - Math.Pow(L2 / Lt, 2)), 3);
        r.Sigma = R((r.M1 + r.M2) / it.GammaX / it.WMm3, 3);

        // ── 剪力：V=qd·Lt/2·(1−Li/(2Lt))（0x137897D–0x137D076）──
        r.V1 = R(r.Qd1 * Lt / 2.0 * (1.0 - L1 / 2.0 / Lt), 3);
        r.V2 = R(r.Qd2 * Lt / 2.0 * (1.0 - L2 / 2.0 / Lt), 3);
        r.Tau = R((r.V1 + r.V2) * it.SMm3 / it.IxMm4 / it.TwMm, 3);

        // ── 挠度（0x137B7D2–0x137C7BF，ai=Li/2）──
        r.F1 = DeflFixed(r.Qk1, Lt, L1 / 2.0, it.EMpa, it.IxMm4);
        r.F2 = DeflFixed(r.Qk2, Lt, L2 / 2.0, it.EMpa, it.IxMm4);
        // ── 挠度限值（0x137C997 ÷100；人工模式 probe2 实证 ÷分母）──
        r.Defl = R(r.F1 + r.F2, 3);
        r.DeflLimit = R(Lt / (it.LimitMode == DeflLimitMode.Auto100 ? 100.0 : it.LimitDivisor), 3);

        r.SigmaOk = r.Sigma <= it.FMpa;
        r.DeflOk = r.Defl <= r.DeflLimit;
        r.TauOk = r.Tau <= it.FvMpa;
        return r;
    }

    // ═══════════════════════ 中横挺 ═══════════════════════

    public sealed class TransomInput
    {
        public double WkRodNmm2 = 0.001333;
        /// <summary>挠度限值模式（probe2/matrix 实证：自动=÷100；人工一致=÷分母（100/150下拉）；
        /// 不一致=水平÷分母、垂直÷500下拉（1500→3 逐位））。500 下拉只有单选项。</summary>
        public DeflLimitMode LimitMode = DeflLimitMode.Auto100;
        public double LimitDivisor = 150.0;
        /// <summary>垂直方向限值分母（500 下拉）。</summary>
        public double VertLimitDivisor = 500.0;
        /// <summary>横梃受荷宽度 L1（mm），「横梃参数」TB(650)。</summary>
        public double SpanL1Mm = 650.0;
        /// <summary>横梃计算跨度 L（mm），默认 1500。</summary>
        public double LengthL2Mm = 1500.0;
        /// <summary>地震影响系数最大值 αmax（0x13922D9=round6(5·αmax) 实证：北京 0.16/上海 0.08；
        /// 门窗屏无输入控件，由第一页地区 DataGrid 选中行经全局槽下传；未选=0）。</summary>
        public double SeismicAlphaMax = 0.0;
        /// <summary>水平地震玻璃面荷载标准值 Gk_area（N/mm²，0x13922F5 实证上海幕墙预演=0.0003/0.0004；
        /// 幕墙线立柱预演写全局槽，门窗线单独时 0）。</summary>
        public double GkAreaNmm2 = 0.0;
        /// <summary>垂直方向自重面荷载标准值 qgk_area（N/mm²，probe11 钉死：qk线=round3(qgk·L1)=0.195、
        /// qd线=round3(1.3·qk线)=0.254 进 σ 竖向项 Mvert/8；τ/f 用 qgk 组合；幕墙线预演写全局槽，默认 0）。
        /// 旧字段 DeadAreaKpa 语义并入此参数（kPa=×1000）。</summary>
        public double QgkAreaNmm2 = 0.0;
        /// <summary>水平方向截面：Wy1/Iy/Sy/ty。</summary>
        public double WMm3 = 4101.0;
        public double ImM4 = 142350.0;
        public double SMm3 = 4466.0;
        public double TwMm = 3.0;
        /// <summary>垂直方向截面（0x13963BA/0x13B1362 实证用 Wx2/Ix/Sx/tx）。</summary>
        public double WVertMm3 = 2765.0;
        public double IVertMm4 = 99690.0;
        public double SVertMm3 = 2962.0;
        public double TVertMm = 3.0;
        public double GammaX = 1.0;
        public double FMpa = 90.0;
        public double FvMpa = 55.0;
        public double EMpa = 70000.0;
    }

    public sealed class TransomResult
    {
        public double WkKpa;                     // 1.333
        public double FMpa, FvMpa;
        public double Qk1, Qd1, Qk2, Qd2;        // 0.433/0.65/1/1.5
        public double M1, M2, Sigma;             // 171369.792/281250/110.368
        public double V1, V2, TauHoriz;          // 381.875/562.5/9.876
        public double F1, F2, DeflHoriz;         // 2.653/4.234/6.887
        public double DeflVert, TauVert;         // 0/0（自重链默认）
        public double DeflLimit, DeflLimitVert;  // 15；不一致模式垂直=round3(L/500)=3
        public bool SigmaOk, DeflHorizOk, DeflVertOk, TauHorizOk, TauVertOk;
    }

    public static TransomResult ComputeTransom(TransomInput it)
    {
        var r = new TransomResult { FMpa = it.FMpa, FvMpa = it.FvMpa };
        double L1 = it.SpanL1Mm, L = it.LengthL2Mm;
        r.WkKpa = R(it.WkRodNmm2 * 1000.0, 3);   // 0x139A0CA

        // ── 荷载（0x1392013–0x1392AF0，外层 round6）──
        r.Qk1 = R(it.WkRodNmm2 * L1 / 2.0, 3);
        r.Qd1 = R(R(r.Qk1 * 1.5, 3) + SeismicQ(it.SeismicAlphaMax, it.GkAreaNmm2, L1, 2, 6), 6);
        r.Qk2 = R(it.WkRodNmm2 * L / 2.0, 3);
        r.Qd2 = R(R(r.Qk2 * 1.5, 3) + SeismicQ(it.SeismicAlphaMax, it.GkAreaNmm2, L, 2, 6), 6);

        // ── 垂直自重面荷载链（probe11 上海幕墙预演后非 0 态逐位钉死：
        //    qgk_area=0.0003 → qk线=round3(qgk·L1)=0.195（f 与 σ 的 Mvert 之源）；
        //    qd_area=round6(1.3·qgk)=0.00039 → τ 链 V=0.00039·L·L1/2=190.125）──
        double qArea = R(it.QgkAreaNmm2, 6);
        double qkLine = R(qArea * L1, 3);
        double qdLine = R(1.3 * qkLine, 3);

        // ── 弯矩：M1 两端固定 /24·(3−α²)，M2 跨中 /12（0x13934BA–0x1396428）──
        r.M1 = R(r.Qd1 * Math.Pow(L, 2) / 24.0 * (3.0 - Math.Pow(L1 / L, 2)), 3);
        r.M2 = R(r.Qd2 * Math.Pow(L, 2) / 12.0, 3);
        // 竖向自重弯矩项（0x1394C40/0x139639E–0x1396428：Mvert=round3(qdLine·L²/8)，
        //   σ=round3((M1+M2)/γx/Wy + Mvert/Wx2)；上海态 0.254·2250000/8=71437.5/2765=25.836，
        //   139.531+25.836=165.367 逐位）。
        double mVert = R(qdLine * Math.Pow(L, 2) / 8.0, 3);
        r.Sigma = R((r.M1 + r.M2) / it.GammaX / it.WMm3 + mVert / it.WVertMm3, 3);

        // ── 剪力：V1=qd1·L/2·(1−L1/(2L))，V2=qd2·L/4（0x1393947–0x139998C）──
        r.V1 = R(r.Qd1 * L / 2.0 * (1.0 - L1 / 2.0 / L), 3);
        r.V2 = R(r.Qd2 * L / 4.0, 3);
        r.TauHoriz = R((r.V1 + r.V2) * it.SMm3 / it.ImM4 / it.TwMm, 3);

        // ── 水平挠度：f1 两端固定带系数 /240；f2 简支 /120 无系数（0x13973A7–0x1398394）──
        r.F1 = DeflFixed(r.Qk1, L, L1 / 2.0, it.EMpa, it.ImM4);
        r.F2 = R(r.Qk2 * Math.Pow(L, 4) / 120.0 / it.EMpa / it.ImM4, 3);
        r.DeflHoriz = R(r.F1 + r.F2, 3);

        // ── 垂直方向 f/τ（0x13B0E10–0x13B1384：f_vert=5·qk线·L⁴/(384·E·Ix2)=1.842；
        //    τ 链 probe11 实测：V=round3(round6(1.3·qgk)·L·L1/2)=round3(0.00039·1500·650/2)
        //    =0.00039·1500=0.585→×650÷2=190.125 → τ=190.125·2962/(99690·3)=1.883 逐位；
        //    幕墙线预演写全局槽，门窗线单独为 0（probe10/11 实证）。──
        r.DeflVert = R(5.0 * qkLine * Math.Pow(L, 4) / 384.0 / it.EMpa / it.IVertMm4, 3);
        double qdArea = R(1.3 * qArea, 6);
        double qdLineL = R(qdArea * L, 3);
        double vVert = R(qdLineL * L1 / 2.0, 3);
        r.TauVert = R(vVert * it.SVertMm3 / it.IVertMm4 / it.TVertMm, 3);

        // 限值：自动=÷100；人工一致=÷分母（H=V）；不一致=H÷分母、V÷500（probe2 逐位：
        //   一致150→H=V=10；不一致→H=10、V=1500/500=3）。
        double divH = it.LimitMode == DeflLimitMode.Auto100 ? 100.0 : it.LimitDivisor;
        double divV = it.LimitMode == DeflLimitMode.ManualDiff ? it.VertLimitDivisor : divH;
        r.DeflLimit = R(L / divH, 3);
        r.DeflLimitVert = R(L / divV, 3);
        r.SigmaOk = r.Sigma <= it.FMpa;
        r.DeflHorizOk = r.DeflHoriz <= r.DeflLimit;
        r.DeflVertOk = r.DeflVert <= r.DeflLimitVert;
        r.TauHorizOk = r.TauHoriz <= it.FvMpa;
        r.TauVertOk = r.TauVert <= it.FvMpa;
        return r;
    }

    // ═══════════════════════ 输出文本（V31 实采原文格式）═══════════════════════

    private static string N(double v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>中竖挺输出（实采块格式逐字对拍）。</summary>
    public static string FormatMullionBook(MullionResult r)
    {
        string Ok(bool b) => b ? "满足要求(√)" : "不满足要求(×)";
        return string.Join("  ",
            "*************************************",
            $"计算杆件时风荷载标准值为:{N(r.WkKpa)}KPa",
            "*************************************",
            $"中竖挺抗弯强度计算值:{N(r.Sigma)}MPa",
            $"中竖挺抗弯强度设计值:{N(r.FMpa)}MPa",
            Ok(r.SigmaOk),
            "*************************************",
            $"中竖挺挠度计算值:{N(r.Defl)}mm",
            $"中竖挺挠度限值:{N(r.DeflLimit)}mm",
            Ok(r.DeflOk),
            "*************************************",
            $"中竖挺抗剪强度计算值:{N(r.Tau)}MPa",
            $"中竖挺抗剪强度设计值:{N(r.FvMpa)}MPa",
            Ok(r.TauOk),
            "*************************************");
    }

    /// <summary>中横挺输出（实采块格式逐字对拍）。</summary>
    public static string FormatTransomBook(TransomResult r)
    {
        string Ok(bool b) => b ? "满足要求(√)" : "不满足要求(×)";
        return string.Join("  ",
            "*************************************",
            $"计算杆件时风荷载标准值为:{N(r.WkKpa)}KPa",
            "*************************************",
            $"中横挺抗弯强度计算值:{N(r.Sigma)}MPa",
            $"中横挺抗弯强度设计值:{N(r.FMpa)}MPa",
            Ok(r.SigmaOk),
            "*************************************",
            $"中横挺水平方向挠度计算值:{N(r.DeflHoriz)}mm",
            $"中横挺水平方向挠度限值:{N(r.DeflLimit)}mm",
            Ok(r.DeflHorizOk),
            "------------------------------------",
            $"中横挺垂直方向挠度计算值:{N(r.DeflVert)}mm",
            $"中横挺垂直方向挠度限值:{N(r.DeflLimitVert)}mm",
            Ok(r.DeflVertOk),
            "*************************************",
            $"中横挺水平方向抗剪强度计算值:{N(r.TauHoriz)}MPa",
            $"中横挺水平方向抗剪强度设计值:{N(r.FvMpa)}MPa",
            Ok(r.TauHorizOk),
            "------------------------------------",
            $"中横挺垂直方向抗剪强度计算值:{N(r.TauVert)}MPa",
            $"中横挺垂直方向抗剪强度设计值:{N(r.FvMpa)}MPa",
            Ok(r.TauVertOk),
            "*************************************");
    }
}
