using System;
using System.Collections.Generic;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// CW27「公共建筑--非透明幕墙热工计算」引擎。
/// 链路与舍入序逐位来自 b7_probe11_cw27.json（frida varhook 21 条 + rtcRound 7 点）：
///   ① te = 0.3·RH_city + 0.7·te_min（V@0x16CFD68，全程不 round；城市行 GB50176「上海,3A,3,0.5,−3.0」）
///   ② 各层热阻 Σ(t/1000/λ)（默认 3 层 fp 位一致命中参数库「热工数值」表行 1-3：
///      千丝板 10/0.3、钢筋混凝土 240/1.74、聚苯乙烯泡沫塑料 100/0.033 —— Adodc 数据源直读）
///   ③ ΣR+Rsi(0.18) → round3（ra 0x16D2B3D 输入 3.3815674→3.382）；+R_air(0.11) 不 round（0x16D35C8）；
///      +Rse(0.04) 后 round5（0x16D35F4：3.532→3.532）；K = round3(1/R_total)（0x16D36D8→0x16D36B6 Div 1 3.532）
///   ④ 露点：e_i = round3(6.11·10^(7.5·t_i/(237.3+t_i)))（t_i=18 → 20.646，0x16D43A9）
///      → x = round3(φ·e_i)（0.3 → 6.194，0x16D45BC）→ ÷6.11 不 round → Magnus 反变换 → round3（0x16D4746）
///   ⑤ t_si = round3(t_i − (t_i−te)·R_air/R_total)（0x16D4922；R_total 用 round5 后的 3.532）
/// 限值：幕墙 K ≤ 0.8（输出文案实录）。室内计算温度默认 18℃（「房间采暖情况」帧，链直读）。
/// </summary>
public sealed class ThermalWallEngine
{
    private static double R(double v, int d = 3) => WindLoadEngine.VbaRound(v, d);

    /// <summary>
    /// 空气层档位查表（3 类型 × 3 热流方向）—— **2026-10-27 逐档 frida 直读**
    /// （b7_probe22_airgap.json：CW27 id=126，「空气层参数」帧两个下拉 3×3 全遍历，
    /// 取 Add 链 ra=0x16D2188 的 op2；每行/列顺序 = V31 下拉候选原文顺序）。
    /// 类型：0 一般空气间层 / 1 单面铝箔空气间层 / 2 双面铝箔空气间层
    /// 热流：0 热流向下（水平、倾斜） / 1 热流向上（水平、倾斜） / 2 垂直空气层
    /// ★ 默认（一般 + 垂直）= 0.18，与旧固定 Rsi 逐位一致 ⇒ 默认链不受影响。
    /// ★ 同批实测：R_air（ra=0x16D35C8 的 op1）9 档恒为 0.11，故 RAirGap 保持单值。
    /// </summary>
    public static readonly double[,] AirGapRsiTable =
    {
        { 0.20, 0.17, 0.18 },   // 一般空气间层
        { 0.64, 0.43, 0.50 },   // 单面铝箔空气间层
        { 1.01, 0.57, 0.71 },   // 双面铝箔空气间层
    };

    /// <summary>按空气层类型 + 热流方向取档位热阻（越界夹到合法范围）。</summary>
    public static double AirGapRsi(int type, int flow)
        => AirGapRsiTable[Math.Clamp(type, 0, 2), Math.Clamp(flow, 0, 2)];

    public sealed class Layer
    {
        public double ThkMm;
        public double Lambda;    // 导热系数 W/(m·K)（参数库「热工数值」表）
        public Layer(double thkMm, double lambda) { ThkMm = thkMm; Lambda = lambda; }
        public double RValue => ThkMm / 1000.0 / Lambda;   // 链①不 round，直接累加
    }

    public sealed class Input
    {
        // 默认 3 层 = 参数库「热工数值」表行 1-3（Adodc 数据源；probe11 运行期 R 位一致）
        public List<Layer> Layers = new()
        {
            new Layer(10, 0.3),     // 千丝板
            new Layer(240, 1.74),   // 钢筋混凝土
            new Layer(100, 0.033),  // 聚苯乙烯泡沫塑料
        };
        public double Rsi = 0.18;   // 内表面热阻（内置，+后 round3）——默认=一般空气间层+垂直档
        /// <summary>空气层类型：0 一般空气间层 / 1 单面铝箔空气间层 / 2 双面铝箔空气间层。</summary>
        public int AirGapTypeIndex = 0;
        /// <summary>热流方向：0 热流向下（水平、倾斜）/ 1 热流向上（水平、倾斜）/ 2 垂直空气层。</summary>
        public int AirGapFlowIndex = 2;
        public double RAirGap = 0.11; // 空气间层热阻（「空气层参数」帧默认，链内不 round；9 档实测恒 0.11）
        public double Rse = 0.04;   // 外表面热阻（内置，+后 round5）
        // 室内计算温度：「房间采暖情况」CB@623,431（probe12 heat_alt 运行期直读 e_i 链 7.5·t_i）：
        //   采暖房间 → 18℃；非采暖房间 → 12℃。t_si 链与 e_i 链共用 t_i。
        public BuildingType Building = BuildingType.公共建筑; // 建筑类别（居住/工业复用本引擎，限值/文案分档）
        public double TIndoor = 18; // 采暖房间默认 18；非采暖=12（probe12：R 0x16D43A9 in 14.0298=12℃）
        public double PhiIndoor = 0.3; // 室内湿度 CB@508,423：30→0.3 / 60→0.6（probe12 R 0x16D45BC 双证）
        public double TeWeightRh = 0.3; // te 固定权重（与 CW26 同构）
        public double CityWinterRh = 0.5; // 城市行 tw（GB50176「上海」=0.5）
        public double CityTeMin = -3.0;   // 城市行 te,min（上海=−3.0）
        public double KLimb = 0.8;  // 「按要求，幕墙的K值不应大于」限值（输出实录）
    }

    public sealed class Result
    {
        public double TeMinWeighted;   // ① −1.9499999999999997（不 round）
        public double RLayersSum;      // ② 3.2015673981191224（Σ 原值）
        public double RAfterRsi;       // ③ round3 → 3.382
        public double RWithAir;        // ③ round5 → 3.492
        public double RTot;            // ③ round5 → 3.532
        public double KWinter;         // ③ round3 → 0.283（输出「幕墙的冬季K值」）
        public double EIndoor;         // ④ round3 → 20.646
        public double EiPhi;           // ④ round3 → 6.194
        public double DewPoint;        // ④ → 0.188（输出「空气的露点温度」）
        public double Tsi;             // ⑤ → 17.379（输出「幕墙室内表面的计算温度」）
    }

    public static Result Compute(Input it)
    {
        var o = new Result();

        // ① te（V 0x16CFD68~D9B：0.3·0.5 + 0.7·(−3.0)，不 round）
        o.TeMinWeighted = it.TeWeightRh * it.CityWinterRh
                        + (1 - it.TeWeightRh) * it.CityTeMin;

        // ② 层热阻累加（V 0x16D1C9F 累加器：逐层 t/1000/λ 原值相加，链内不 round）
        double sum = 0;
        foreach (var l in it.Layers) sum += l.RValue;
        o.RLayersSum = sum;

        // ③ 表面/空气层热阻叠加（rounds 实录：round3@0x16D2B3D 在 +Rsi 后；+R_air 无 round（0x16D35C8）；
        //    +Rse 后 round5@0x16D35F4（3.532→3.532）；K=round3(1/R_total)@0x16D36D8）
        // 档位热阻：按「空气层类型 × 热流方向」查表（默认 一般+垂直 → 0.18，与旧固定值同）
        double rsiEff = AirGapRsi(it.AirGapTypeIndex, it.AirGapFlowIndex);
        o.RAfterRsi = R(sum + rsiEff, 3);            // 默认 3.3815673981191225 → 3.382
        o.RWithAir = o.RAfterRsi + it.RAirGap;       // 3.492（链内不 round）
        o.RTot = R(o.RWithAir + it.Rse, 5);          // 3.532
        o.KWinter = R(1.0 / o.RTot, 3);              // 0.28312570781426954 → 0.283

        // ④ 露点（V 0x16D433A~5BC：t_i=18 的 Magnus 正变换 → φ 系数 → 反变换）
        double eRaw = 6.11 * Math.Pow(10.0, 7.5 * it.TIndoor / (237.3 + it.TIndoor));
        o.EIndoor = R(eRaw, 3);                     // 20.645759760406484 → 20.646
        double x = R(it.PhiIndoor * o.EIndoor, 3);  // 6.1938 → 6.194
        o.EiPhi = x;
        double lg = Math.Log(x / 6.11) / Math.Log(10.0);
        o.DewPoint = R(237.3 / (7.5 / lg - 1.0), 3); // 0.18777337413961956 → 0.188

        // ⑤ t_si（V 0x16D48B5~922：(t_i−te)·R_air ÷ R_total(3.532) → t_i − x → round3）
        o.Tsi = R(it.TIndoor - (it.TIndoor - o.TeMinWeighted) * it.RAirGap / o.RTot, 3);

        return o;
    }

    /// <summary>V31 输出正文两段判定（逐字对齐 RichText，probe6/probe11 实录）。
    /// 公共/居住共用模板（限值 KLimb 参数化）；工业建筑独立模板（GB51245 提醒 + 「建筑整体K值」+ 结露「不会有结露现象」）。</summary>
    public static string FormatOutput(Input it, Result o)
    {
        const string Sep = "********************************************";
        if (it.Building == BuildingType.工业建筑)
        {
            // 工业建筑：b7_probe28 ind_wall 逐字实录（含开头 GB51245 提醒段）
            string notice = "***请特别留意：工业建筑节能计算与民用建筑是不同的，工业建筑的节能是整体考虑的，并不是按面加权平均的。软件每次所计算的只是局部的具体某种结构的结果，不能直接按此结果与规范比对确认作为满足节能计算的依据，计算书也不能拿出来直接提供给相关单位，这一问题请大家特别留意。具体情况可点击下面的<节能计算使用前必读>按钮获得帮助，或直接参看规范《工业建筑节能设计统一标准》GB51245-2017获得更具体的介绍 ！！！";
            string s1 = $"幕墙的冬季K值为:{o.KWinter:0.###}W/(m2·K)      " +
                        $"按要求，建筑整体K值不应大于:{it.KLimb:0.###}W/(m2·K)      " +
                        "工业建筑节能计算需要进行整体判断，此处计算仅表示所计算的单项结构满足与否，不表示整体节能是否达标，本计算仅作参考。";
            string s2 = $"幕墙室内表面的计算温度为:{o.Tsi:0.###}℃      " +
                        $"空气的露点温度为:{o.DewPoint:0.###}℃      " +
                        "不会有结露现象。";
            return $"{notice}  {Sep}      {s1}  {Sep}      {s2}  {Sep}";
        }

        string t1 = $"幕墙的冬季K值为:{o.KWinter:0.###}W/(m2·K)      " +
                    $"按要求，幕墙的K值不应大于:{it.KLimb:0.###}W/(m2·K)      " +
                    (o.KWinter > it.KLimb ? "不满足要求(×)" : "满足要求(√)");
        string t2 = $"幕墙室内表面的计算温度为:{o.Tsi:0.###}℃      " +
                    $"空气的露点温度为:{o.DewPoint:0.###}℃      " +
                    (o.Tsi >= o.DewPoint ? "满足要求(√)" : "不满足要求(×)");
        return $"{Sep}      {t1}  {Sep}      {t2}  {Sep}";
    }
}
