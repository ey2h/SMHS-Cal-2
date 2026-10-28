using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// CW26「公共建筑--透明幕墙（门窗）热工计算」引擎。
/// 全部链路由 b7_probe7_cw26.json（frida var 链 70 条 + rtcRound 22 点）逐位闭合，
/// 舍入序一律按 rounds 记录原样复刻；常量出处：
///   9.139 = 程序内置 double（VA 0x40BC68，普通铝合金框 K）；
///   6.11 / 7.5 / 237.3 / 10 / 1.0 = 露点 Magnus 常量（IDA 0x40BCB8/4055E0/40BCB0/401710/4019C0 反汇编确认）；
///   城市三元组（加权湿度 0.5 / 冬季室外计算温度 −3.0）= 参数库 thermal_inx GB50176 行「上海,3A,3,0.5,-3.0」。
/// 未归因旁链：rtcRound(1/5.9 − 0.17, 4)（ra 0x1533AB2，主输出未见对应数值）→ 见 UnattributedR09。
/// </summary>
public sealed class ThermalGlassEngine
{
    private static double R(double v, int d = 3) => WindLoadEngine.VbaRound(v, d);

    /// <summary>玻璃构型（CW26「玻璃结构」combo 7 项，b7_probe23_multipane 逐一切换取证）。</summary>
    public enum GlassStructure
    {
        单片玻璃 = 0,
        夹层玻璃 = 1,
        中空玻璃 = 2,
        中空加内夹层 = 3,
        中空加外夹层 = 4,
        双夹层中空 = 5,
        双中空玻璃 = 6,
    }

    /// <summary>各构型的（玻璃片数, PVB 层数, 气隙腔数）。
    /// 双夹层中空 = 两片夹层(2×2 片 + 2 PVB) 中间 1 气隙；中空+内/外夹层物理等价（K 同）。</summary>
    private static readonly (int Panes, int Pvb, int Cavities)[] StructureSpec =
    {
        (1, 0, 0),  // 单片玻璃
        (2, 1, 0),  // 夹层玻璃
        (2, 0, 1),  // 中空玻璃
        (3, 1, 1),  // 中空+内夹层玻璃
        (3, 1, 1),  // 中空+外夹层玻璃
        (4, 2, 1),  // 双夹层中空玻璃
        (3, 0, 2),  // 双中空玻璃
    };

    // —— 气隙/夹层物理常量（程序内置：参数库 61 表全库扫描 0.0241/0.837/PVB 均无；b7_probe23 frida 链直读）——
    // PVB：链 ra=0x153e5dd `0.76/1000` → ra=0x153e5f1 `/0.16` = 0.00475（PVB 厚 0.76mm、λ 0.16）。
    // 辐射项：ra=0x154a77f/0x154a793 `Mul(2.268e-7, 0.7196904557) → Mul(·, 20346417) = 3.321060097486844`
    //   （4σ=2.268e-7、T³=273³=20346417、ε=0.837、分母 2/0.837−1=1.389486260454）。
    // 气体导热项：ra=0x154ce5f `0.0241/0.009 = 2.677778 → round3 = 2.678`。
    private const double PvbThicknessMm = 0.76;  // PVB 夹层厚度 mm
    private const double PvbLambda = 0.16;       // PVB 导热系数 W/(m·K)
    private const double GapEpsilon = 0.837;     // 气隙表面辐射率（普通玻璃）
    private const double GapTempK = 273.0;       // 气隙温度 K
    private const double Sigma4 = 2.268e-7;      // 4 × 斯蒂芬-玻尔兹曼常数 5.67e-8

    /// <summary>框材质（CW26 combo CB@586,449 九项）。Kf 值为 b7_probe10_frameK.json frida 运行期直读
    /// （var 链 `Mul 1.217 Kf` 的 op2）；程序内置——参数库/安装目录全盘二进制扫描均无此值。
    /// 钢框 Kf=9.139 与普通铝合金同值（故钢框 case 输出零差异，非切换失效）。</summary>
    public static double FrameK(string material) => material switch
    {
        "普通铝合金型材" => 9.139,
        "断热铝合金型材" => 3.318,
        "聚氨酯塑钢带筋" => 2.8,
        "PVC两腔无金属筋" => 2.2,
        "PVC两腔有金属筋" => 2.7,
        "PVC三腔无金属筋" => 2.0,
        "木框" => 1.8,
        "木-金属框" => 1.8,
        "钢框" => 9.139,
        _ => throw new ArgumentOutOfRangeException(nameof(material), material, "未知框材质"),
    };

    public sealed class Input
    {
        // —— 玻璃层（V10~V11: 6/1000/0.76 → R04 round3=0.008）——
        public GlassStructure Structure = GlassStructure.单片玻璃; // 玻璃构型（默认单片，多片走分层热阻链）
        public double GlassThkMm = 6;        // 玻璃厚度 mm（每片同厚；多片构型下各片均取此值）
        public double LambdaGlass = 0.76;    // 玻璃导热系数 W/(m·K)（参数库「热工系数」表组0「玻璃 0.76」，可导入）
        // —— 气隙（多片构型；默认空气 9mm）——
        public double GapThicknessMm = 9;    // 气隙厚度 mm
        public double GapGasLambda = 0.0241; // 气隙气体导热系数 W/(m·K)（空气）
        // —— 气隙综合传热系数 h（V03~V05: 3.9·ε/ε=3.9 + 16 → 19.9；ε=0.837 链内抵消，
        //    3.9/16 的输入分解待 probe8 气隙厚度/气体类型扰动实测后拆分，暂按观测值直传）——
        public double HGap = 19.9;
        public double Hsi = 8;               // 内表面传热系数（R14: 1/8）
        public double Hse = 6.9;             // 外表面夏季（V08/V09: 4.4+3.6=8 / 4.4+2.5=6.9 → 1/6.9）
        // —— 框（V24~V25: 7.783×K_glass + 1.217×9.139；9.139=内置常量）——
        public double KFrame = 9.139;
        public double AlphaFrame = 0.4;      // 框太阳吸收比（V36/V46: 0.4）
        public double AreaGlass = 7.783;     // 玻璃面积 m²（输入框）
        public double AreaFrame = 1.217;     // 框面积 m²（输入框）
        public double AreaSpandrel = 27.12;  // 实体/窗下墙面积 m²（输入框；×0 参与，默认无实体 K）
        public double KSpandrel = 0;         // 实体墙 K（默认态 V27 乘 0）
        public double AreaTotal = 9;         // 总面积 m²（输入框，= 加权分母）
        // —— 日照（V40~V56）——
        public double ShgcGlass = 0.81;      // 玻璃太阳得热系数（输入框）
        public double AreaFrameShadow = 1.385; // 框受阴影面积（V37: 1.385/1.217）
        public double DenShgcSummer = 16;    // 夏季框得热分母（V48 I2=16）
        public double DenShgcWinter = 20;    // 冬季框得热分母（V54 I2=20）
        public double ShgcStandardBase = 0.87; // SC 分母（V44: 0.722/0.87）
        public double TauGlass = 0.75;       // 玻璃可见光透射比（输入框）
        public double WindowWallRatio = 0.32; // 窗墙比（输入框，透射比判定文案分档 0.40）
        // —— 结露 / 露点（V00~V02, V59~V69；城市行=GB50176「上海,3A,3,0.5,-3.0」）——
        public BuildingType Building = BuildingType.公共建筑; // 建筑类别（居住/工业复用本引擎，限值/文案分档）
        public double TIndoor = 20;          // 室内计算温度 ℃
        // te 加权固定权重 0.3/0.7（b7_probe8e humid60 case：湿度 combo 切换后 tsi 仍 5.008
        // ⇒ 湿度不进 te，只进露点 e_i 系数）。
        public double TeWeightRh = 0.3;      // te = 0.3·RH_city + 0.7·te_min（V00~V02）
        public double PhiIndoor = 0.3;       // 室内相对湿度（CB19「30」→0.3；V64 e_i 系数；60→0.6 probe9/8e）
        public double CityWinterRh = 0.5;    // 城市冬季相对湿度（参数库 0.5）
        public double CityTeMin = -3.0;      // 城市冬季室外计算温度（参数库 −3.0）
        // —— 规范限值（按建筑类别/窗墙比/气候区分档；probe8e cls_B/wwr0.5 case 逐位）——
        public double KLimb = 2.2;           // 甲类=2.2、乙类=3（cls_B 输出实录）
        public double ShgcSummerLimit = 0.35;// 甲类&窗墙比<0.40=0.35；≥0.40=0.3（wwr0.5 实录）；乙类=0.45
    }

    public sealed class Result
    {
        // 逐位对拍目标（默认态 = V31 输出/rounds 实测值）：
        public double TeMinWeighted = -1.95;    // V02: 0.3×0.5 + 0.7×(−3.0) = −1.95（未舍入）
        public double RGlass;                    // V11→R04: 玻璃层总热阻（含 PVB，round3）
        public double RGap;                      // 气隙总热阻（N 腔，round3）
        public double KGlassWinter = 5.464;      // V16/R06: 1/round3(1/19.9+0.008+1/8)
        public double KGlassSummer = 4.926;      // V21/R08: 1/round3(1/19.9+0.008+1/6.9)
        public double KSystemWinter = 5.961;     // V29/R10: 输出「门窗系统冬季K值」
        public double KSystemSummer = 5.496;     // V35/R11（计算但不在输出正文）
        public double ShgcFrame = 0.161;         // V39/R12
        public double ShgcWeightedRaw = 0.722;   // V43/R13
        public double Sc = 0.83;                 // V44/R14: round3(0.722/0.87)
        public double ShgcSummer = 0.731;        // V50/R15: 输出「夏季计算值」
        public double ShgcWinter = 0.725;        // V56/R16: 输出「冬季计算值」
        public double TauWeighted = 0.649;       // V58/R17（加权值；输出正文显示输入 0.75）
        public double EIndoorRaw;                // V63→R18: 6.11×10^(7.5·20/257.3) → 23.389
        public double EiPhi = 7.017;             // V64→R19: round3(0.3×23.389)
        public double DewPoint = 1.917;          // R20: 237.3/(7.5/log10(7.017/6.11) − 1)
        public double Tsi = 5.008;               // V66~V69/R21: 20 − (20−te)×K_glass冬/8
        public double UnattributedR09 = -0.0005; // R09: round4(1/5.9−0.17)，输出未归因（登记备查）

        /// <summary>输出正文显示透射比 = 玻璃输入值（V31 原样回显，非加权 0.649）。</summary>
        public double TauDisplay => TauGlassDisplay;
        private double TauGlassDisplay = 0.75;
        internal void SetTauDisplay(double v) => TauGlassDisplay = v;
    }

    public static Result Compute(Input it)
    {
        var o = new Result();

        // ① 加权室外温度 te = 0.3·RH_city + 0.7·te_min（V00~V02，全程不 round；
        //    权重固定，湿度 combo 不进此链——probe8e humid60 后 tsi 不变）
        double te = it.TeWeightRh * it.CityWinterRh + (1 - it.TeWeightRh) * it.CityTeMin;
        o.TeMinWeighted = te;

        // ② 玻璃层总热阻 = round3( Σ片 t/1000/λ + ΣPVB 0.76/1000/0.16 )（单片即 round3(6/1000/0.76)=0.008）
        var spec = StructureSpec[(int)it.Structure];
        double rGlassRaw = spec.Panes * (it.GlassThkMm / 1000.0 / it.LambdaGlass)
                         + spec.Pvb * (PvbThicknessMm / 1000.0 / PvbLambda);
        o.RGlass = R(rGlassRaw, 3);

        // ②′ 气隙热阻（N 腔）= round3( N × 1/(round3(λ_g/d) + round3(4σ·T³/(2/ε−1))) )
        //    h_c = round3(0.0241/0.009)=2.678；h_r = round3(2.268e-7·273³/1.389486)=3.321；
        //    每腔 1/5.999=0.166694（未 round），N 腔相加后再 round3。
        double hc = R(it.GapGasLambda / (it.GapThicknessMm / 1000.0), 3);
        double hr = R(Sigma4 * Math.Pow(GapTempK, 3) / (2.0 / GapEpsilon - 1.0), 3);
        double gapPerCavity = 1.0 / (hc + hr);
        o.RGap = R(spec.Cavities * gapPerCavity, 3);

        // ③ 冬季 K_glass（1/h外 + R_gap + R_glass + 1/h内 → round3 → 1/x）
        double rSumWinter = 1.0 / it.HGap + o.RGap + o.RGlass + 1.0 / it.Hsi;
        o.KGlassWinter = R(1.0 / R(rSumWinter, 3), 3);

        // ④ 夏季 K_glass（内表面换 1/6.9，同构）。
        //    ★ 仅无气隙构型（单片/夹层）计算；含气隙构型 V31 只算冬 K——probe23 链里中空/双中空等
        //    无 1/6.9 累加、无系统夏 K 面积加权（对比夹层有两组加权），故夏 K 保持 0。
        bool hasSummerK = spec.Cavities == 0;
        if (hasSummerK)
        {
            double rSumSummer = 1.0 / it.HGap + o.RGap + o.RGlass + 1.0 / it.Hse;
            o.KGlassSummer = R(1.0 / R(rSumSummer, 3), 3);
        }
        else
        {
            o.KGlassSummer = 0;
        }

        // ⑤ 系统冬季/夏季 K 面积加权（V24~V29 / V30~V35，逐项乘后加、最后 /总面积、round3）
        double kSumWinter = it.AreaGlass * o.KGlassWinter + it.AreaFrame * it.KFrame
                          + it.AreaSpandrel * it.KSpandrel;
        o.KSystemWinter = R(kSumWinter / it.AreaTotal, 3);
        o.KSystemSummer = hasSummerK
            ? R((it.AreaGlass * o.KGlassSummer + it.AreaFrame * it.KFrame
                + it.AreaSpandrel * it.KSpandrel) / it.AreaTotal, 3)
            : 0;

        // ⑥ 框 SHGC（V36~V39：0.4·K_f ÷ (1.385/1.217·h)）
        o.ShgcFrame = R(it.AlphaFrame * it.KFrame
                      / (it.AreaFrameShadow / it.AreaFrame * it.HGap), 3);

        // ⑦ SC：round3(玻璃项加权)/0.87（V40~V43 → R13；V44 → R14）
        o.ShgcWeightedRaw = R((it.ShgcGlass * it.AreaGlass + o.ShgcFrame * it.AreaFrame)
                            / it.AreaTotal, 3);
        o.Sc = R(o.ShgcWeightedRaw / it.ShgcStandardBase, 3);

        // ⑧ SHGC 夏/冬（V45~V50 /20：分母 16 夏 / 20 冬；框得热项 0.4·A_f·K_f/den）
        double frameHeatSummer = it.AlphaFrame * it.AreaFrame * it.KFrame / it.DenShgcSummer;
        o.ShgcSummer = R((it.ShgcGlass * it.AreaGlass + frameHeatSummer) / it.AreaTotal, 3);
        double frameHeatWinter = it.AlphaFrame * it.AreaFrame * it.KFrame / it.DenShgcWinter;
        o.ShgcWinter = R((it.ShgcGlass * it.AreaGlass + frameHeatWinter) / it.AreaTotal, 3);

        // ⑨ 加权透射比（V57~V58；输出正文回显输入 τ=0.75）
        o.TauWeighted = R(it.TauGlass * it.AreaGlass / it.AreaTotal, 3);
        o.SetTauDisplay(it.TauGlass);

        // ⑩ 室内水汽分压 e_i = 6.11·10^(7.5·t_i/(237.3+t_i))（V59~V63 → R18 round3）
        double eRaw = 6.11 * Math.Pow(10.0, 7.5 * it.TIndoor / (237.3 + it.TIndoor));
        double eRound = R(eRaw, 3);
        o.EIndoorRaw = eRound;

        // ⑪ e_i·φ（V64 → R19 round3；φ=室内湿度 CB19：30→0.3 / 60→0.6，probe9 运行期直读）
        //    → 露点 L=log10(x/6.11), td = 237.3/(7.5/L − 1)（V65 → R20）
        double eiPhi = R(it.PhiIndoor * eRound, 3);
        o.EiPhi = eiPhi;
        double lnRatio = Math.Log(eiPhi / 6.11);
        double lg = lnRatio / Math.Log(10.0);
        o.DewPoint = R(237.3 / (7.5 / lg - 1.0), 3);

        // ⑫ 内表面温度 t_si = t_i − (t_i − te)·K_glass冬/Hsi（V66~V69，链内不 round、末端 R21）
        o.Tsi = R(it.TIndoor - (it.TIndoor - te) * o.KGlassWinter / it.Hsi, 3);

        // 旁链登记（V22~V23 → R09，round4）：主输出未归因
        o.UnattributedR09 = R(1.0 / 5.9 - 0.17, 4);

        return o;
    }

    /// <summary>V31 输出正文四段判定文本（逐字对齐 RichText 输出）。
    /// 公共/居住建筑共用模板（限值由 KLimb/ShgcSummerLimit 参数化）；工业建筑独立模板（GB51245 提醒 + 「建筑整体K值」+ 结露「不会有结露现象」）。</summary>
    public static string FormatOutput(Input it, Result o)
    {
        string Sep = "********************************************";
        if (it.Building == BuildingType.工业建筑)
        {
            // 工业建筑：b7_probe28 ind_glass 逐字实录（含开头 GB51245 提醒段）
            string notice = "***请特别留意：工业建筑节能计算与民用建筑是不同的，工业建筑的节能是整体考虑的，并不是按面加权平均的。软件每次所计算的只是局部的具体某种结构的结果，不能直接按此结果与规范比对确认作为满足节能计算的依据，计算书也不能拿出来直接提供给相关单位，这一问题请大家特别留意。具体情况可点击下面的<节能计算使用前必读>按钮获得帮助，或直接参看规范《工业建筑节能设计统一标准》GB51245-2017获得更具体的介绍 ！！！";
            string s1 = $"玻璃自身K值为:{o.KGlassWinter:0.###}W/(m2·K)      " +
                        $"门窗系统冬季K值为:{o.KSystemWinter:0.###}W/(m2·K)      " +
                        $"按要求，建筑整体K值不应大于:{it.KLimb:0.###}W/(m2·K)      " +
                        "工业建筑节能计算需要进行整体判断，此处计算仅表示所计算的单项结构满足与否，不表示整体节能是否达标，本计算仅作参考。";
            string s2 = $"计算单元的太阳得热系数夏季计算值为:{o.ShgcSummer:0.###}      " +
                        $"按规范，太阳得热系数不应大于:{it.ShgcSummerLimit:0.###}      " +
                        "工业建筑节能计算需要进行整体判断，此处计算仅作参考。";
            string s3 = "工业建筑的可见光透射比不作要求！";
            string s4 = $"室内表面的计算温度为:{o.Tsi:0.###}℃      " +
                        $"空气的露点温度为:{o.DewPoint:0.###}℃      " +
                        "不会有结露现象。";
            return $"{notice}  {Sep}      {s1}  {Sep}      {s2}  {Sep}      {s3}  {Sep}      {s4}  {Sep}";
        }

        string t1 = $"玻璃自身K值为:{o.KGlassWinter:0.###}W/(m2·K)      " +
                    $"门窗系统冬季K值为:{o.KSystemWinter:0.###}W/(m2·K)      " +
                    $"按要求，此处K值不应大于:{it.KLimb:0.###}W/(m2·K)      " +
                    (o.KSystemWinter > it.KLimb ? "不满足要求(×)" : "满足要求(√)");
        string t2 = $"计算单元的太阳得热系数夏季计算值为:{o.ShgcSummer:0.###}      " +
                    $"计算单元的太阳得热系数冬季计算值为:{o.ShgcWinter:0.###}      " +
                    $"按规范，太阳得热系数夏季不应大于:{it.ShgcSummerLimit:0.###}      " +
                    "按规范，太阳得热系数冬季不应小于:不作要求      " +
                    (o.ShgcSummer > it.ShgcSummerLimit ? "不满足要求(×)" : "满足要求(√)");
        string t3 = $"玻璃可见光透射比为:{o.TauDisplay:0.##}      " +
                    $"窗墙比为:{it.WindowWallRatio:0.##}      " +
                    "按规范要求，对于公共建筑，按照3.2.4[GB50189-2015]的规定，甲类公共建筑单一立面窗墙比小于0.40时，玻璃的可见光透射比不应小于0.6；甲类公共建筑单一立面窗墙比大于等于0.40时，玻璃的可见光透射比不应小于0.4；      " +
                    "按规范要求，对于居住建筑，按照3.1.17[GB55015-2021]要求，外窗玻璃的可见光透射比不应小于0.4；      " +
                    (it.WindowWallRatio < 0.40 ? (o.TauDisplay >= 0.6 ? "满足要求(√)" : "不满足要求(×)")
                                               : (o.TauDisplay >= 0.4 ? "满足要求(√)" : "不满足要求(×)"));
        string t4 = $"室内表面的计算温度为:{o.Tsi:0.###}℃      " +
                    $"空气的露点温度为:{o.DewPoint:0.###}℃      " +
                    (o.Tsi >= o.DewPoint ? "满足要求(√)" : "不满足要求(×)");
        return $"{Sep}      {t1}  {Sep}      {t2}  {Sep}      {t3}  {Sep}      {t4}  {Sep}";
    }
}
