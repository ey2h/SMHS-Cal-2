using System;
using System.Collections.Generic;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 门窗连接件计算引擎（V31「门窗系统计算」CW29 连接件参数选择预演）。
/// 取证三证（本轮全组合遍历补全）：
/// ① IDA 反编译 sub@0x1403DD4–0x14090A2（unpacked_v31，VA−0x400000；有钢附框分支 0x13FDBC0–0x1403000），
///    浮点常量 dbl_401D48=1.25 / dbl_40ADE0=3.7 / dbl_403338=2.4 / dbl_4020D8=1.5 / dbl_401928=0.5；
/// ② 材质 11 case、铆钉类型×规格 25 格、连接片数/结构片数、模式开关全部切换遍历
///    （door_conn_matrix.json / door_conn_probe4a.json，每格输出逐位实采）；
/// ③ Var(含 Sub) 链逐位归因（地址见表注）。
/// 语义（全部代码/遍历实证，无推测）：
///   V = round3(1.5·wk·H·B) + round3(7·αmax·Gk_area·H·B·0.5)（链 0x14049D3–0x1404DB6；
///     probe10 钉死：αmax 由地区行下传（北京 0.16→上海 0.08，链 0x1404C41 7×0.16/7×0.08）；
///     Gk_area=玻璃地震面荷载（N/mm²）由幕墙线立柱/板块预演写全局槽（probe10：0.0004），
///     门窗线单独时 0。probe10 非 0 态逐位：round3(7·0.08·0.0004·2500·2000·0.5)=560，
///     V=12555+560=13115→单点 683.073 逐位）；
///   边框侧单点力 = round3(round3(1.25·V/n)/pA)（0x1404F0D–0x140500F，n=连接点数、pA=连接片数 paramA）；
///   结构侧单点力 = round3(round3(1.25·V/n)/pB)（0x1407CF6，pB=结构侧片数 paramB）；
///   薄壁件(型材)抗剪设计值：
///     f37 = round3(3.7·√(t板³·d钉)·fa)（0x14057A3–0x14057F9；d钉=规格下拉值，非程序常数）；
///     f24 = round3(2.4·t板·d钉·fa)（0x1405923–0x140595B）；
///     r   = round3(max(t板,t墙)/min(t板,t墙))（0x1405A4A）；
///     r==1 → min(f37,f24)（d钉=4.0 时输出 1223.52=f37 反证「取 f2」不成立）；
///     r&gt;1  → round3(f37 + (f24−f37)·(r−1)/t板)（插值链 0x14061E8/有附框 0x13FEFC9–0x13FF014 除数=连接片自身厚）。
///   结构侧连接片设计值 = round3(3.7·t板·d2·f钢)（0x1408036–0x1408063；d2=结构侧钉径默认4.0；
///     「4」实为 d2 值——specB 4.2 时输出 4778.55=3.7·1.5·4.2·205 逐位命中）；
///     膨胀螺钉分支 = 常数 3956.4（无 Var 链，door_modes_probe2/4a 实采）。
///   卡槽连接：跳过「连接片与门窗边框」整块（输出仅剩玻璃行+结构块，kacao 实采）；
///   不计算连接件：无任何输出（var=0 实证）；
///   有钢附框：插入两块（分支地址 0x13FDBC0–0x1402FE3）：
///     A 连接片↔钢附框：薄壁件=钢附框(t=附框厚,fa=205)，插值除数=t板；钉=附框类型/规格表；
///     B 钢附框↔门窗边框：t自=边框厚、t他=附框厚、fa=边框材质；钉表同 A。
/// </summary>
public static class DoorConnectorEngine
{
    /// <summary>连接片/型材抗剪强度设计值 switch（材质下拉 11 项顺序，11 case 全实测）。</summary>
    public static readonly string[] MaterialNames =
        { "6061-T4", "6061-T6", "6063-T5", "6063-T6", "6063A-T5", "6063A-T6",
          "塑钢", "Q235", "Q355", "6060-T66", "玻纤增强聚氨酯" };
    public static readonly double[] MaterialShearMpa =
        { 90, 200, 90, 150, 135, 160, 37, 205, 310, 135, 40 };

    /// <summary>抽芯铆钉/螺钉最大抗剪承载力设计值表（类型×钉径 mm→N，25+10 格全实测）。</summary>
    public static readonly string[] FastenerTypeNames =
        { "抽芯铆钉(开口10/12级)", "抽芯铆钉(开口11/15级)", "抽芯铆钉(封闭11/15级)",
          "自攻螺钉", "自攻自钻螺钉" };
    /// <summary>每类型的钉径下拉候选（mm）。</summary>
    public static readonly double[][] FastenerDiameters =
    {
        new[] { 3.0, 3.2, 4.0, 4.8, 5.0 },   // 开口10/12
        new[] { 3.0, 3.2, 4.0, 4.8, 5.0 },   // 开口11/15
        new[] { 3.0, 3.2, 4.0, 4.8, 5.0 },   // 封闭11/15
        new[] { 3.5, 3.9, 4.2, 4.8, 5.5 },   // 自攻
        new[] { 3.5, 3.9, 4.2, 4.8, 5.5 },   // 自攻自钻
    };
    private static readonly double[][] RivetCapOpen1012 = { new[] { 3.0, 400.0 }, new[] { 3.2, 500.0 }, new[] { 4.0, 850.0 }, new[] { 4.8, 1200.0 }, new[] { 5.0, 1400.0 } };
    private static readonly double[][] RivetCapOpen1115 = { new[] { 3.0, 550.0 }, new[] { 3.2, 750.0 }, new[] { 4.0, 1250.0 }, new[] { 4.8, 1850.0 }, new[] { 5.0, 2150.0 } };
    private static readonly double[][] RivetCapClosed1115 = { new[] { 3.0, 930.0 }, new[] { 3.2, 1100.0 }, new[] { 4.0, 1600.0 }, new[] { 4.8, 2200.0 }, new[] { 5.0, 2420.0 } };
    private static readonly double[][] FastenerCapZigong = { new[] { 3.5, 1346.0 }, new[] { 3.9, 1671.0 }, new[] { 4.2, 1938.0 }, new[] { 4.8, 2532.0 }, new[] { 5.5, 3324.0 } };

    /// <summary>查承载力表（类型 0..4 × 钉径）。表外值抛异常（V31 为查表非公式）。</summary>
    public static double FastenerCapacityN(int type, double d)
    {
        double[][] t = type switch
        {
            0 => RivetCapOpen1012,
            1 => RivetCapOpen1115,
            2 => RivetCapClosed1115,
            3 or 4 => FastenerCapZigong,
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
        foreach (var row in t)
            if (Math.Abs(row[0] - d) < 1e-9) return row[1];
        throw new ArgumentException($"承载力表无 ({type},{d}) 组合");
    }

    /// <summary>连接片↔结构固定方式：射钉/膨胀螺钉 → 结构侧为常数 3956.4（实采）。</summary>
    public const double ExpansionStructCapacityN = 3956.4;

    public sealed class Input
    {
        /// <summary>风荷载标准值 wk（N/mm²），玻璃预演下传，默认 0.00137。</summary>
        public double WkNmm2 = 0.00137;
        /// <summary>整樘门窗洞口高 H（mm）TB(1006,334)。</summary>
        public double OpeningHeightMm = 2500.0;
        /// <summary>整樘门窗洞口宽 B（mm）TB(1006,358)。</summary>
        public double OpeningWidthMm = 2000.0;
        /// <summary>地震影响系数最大值 αmax（地区行下传：北京 0.16/上海 0.08；未选=0）。</summary>
        public double SeismicAlphaMax = 0.0;
        /// <summary>玻璃地震面荷载标准值 Gk_area（N/mm²）——由幕墙线预演写全局槽（probe10=0.0004），
        /// 门窗线单独时 0 → 地震项 0（默认基线逐位不变）。</summary>
        public double SeismicAreaNmm2 = 0.0;

        /// <summary>连接点数 n（TB(667,475)=24，边框/附框/结构侧共用）。</summary>
        public double ConnectorCount = 24.0;
        /// <summary>连接片厚 t板（TB(929,559)=1.5）。</summary>
        public double PlateThicknessMm = 1.5;
        /// <summary>门窗边框壁厚 t墙（TB(803,475)=1.5）。</summary>
        public double WallThicknessMm = 1.5;
        /// <summary>边框材质 switch 索引（0..10，进 fa）。</summary>
        public int WallMaterialIndex = 0;

        /// <summary>边框侧钉类型 0..4（下拉 667,498）。</summary>
        public int FrameFastenerType = 0;
        /// <summary>边框侧钉径 d1（下拉 990,498，按类型候选）。</summary>
        public double FrameScrewDiameter = 3.0;
        /// <summary>连接片数 pA（下拉 782,522=1，边框侧计算值除数）。</summary>
        public double PlateCountFrameSide = 1.0;

        /// <summary>结构侧钉径 d2（下拉 918,522=4.0，进 3.7·t·d2·f钢）。</summary>
        public double StructScrewDiameter = 4.0;
        /// <summary>结构侧连接片数 pB（下拉 854,498=1，结构侧计算值除数）。</summary>
        public double PlateCountStructSide = 1.0;
        /// <summary>结构侧固定方式：true=射钉/膨胀螺钉（结构侧设计值改常数 3956.4）。</summary>
        public bool ExpansionMode = false;
        /// <summary>连接片（钢）抗剪强度设计值 f钢（默认 205=Q235 分支）。</summary>
        public double PlateShearMpa = 205.0;

        /// <summary>连接形式：true=螺钉/抽钉连接（默认），false=卡槽连接（跳过边框块）。</summary>
        public bool ScrewConnection = true;
        /// <summary>计算选择：false=不计算连接件（无输出）。</summary>
        public bool CalcConnector = true;

        /// <summary>安装形式：true=有钢附框（插两块附框校核）。</summary>
        public bool HasSteelFrame = false;
        /// <summary>钢附框厚（TB(630,710)=2.5）。</summary>
        public double SteelFrameThicknessMm = 2.5;
        /// <summary>钢附框材质抗剪 fa（V31 固定 Q235→205 实证）。</summary>
        public double SteelFrameShearMpa = 205.0;
        /// <summary>附框侧钉类型（0=自攻螺钉,1=自攻自钻；承载力同表）。</summary>
        public int FrameScrewType = 0;
        /// <summary>附框侧钉径（下拉 830,733=3.5）。</summary>
        public double SteelFrameScrewDiameter = 3.5;
    }

    /// <summary>一段「薄壁件+螺钉」校核结果。</summary>
    public sealed class FastenerCheck
    {
        public string BlockLabel = "";
        /// <summary>「单个X」两行的前缀。V31 实采（door_conn_matrix / door_conn_probe4a 全 40 case）：
        /// 边框块标题是「连接片与门窗边框连接处」但快固件行前缀**不带「门窗」**二字
        /// （「连接片与边框连接处单个抽芯铆钉…」）；A/B 块前缀与标题相同。</summary>
        public string FastenerRowPrefix = "";
        public string FastenerLabel = "";
        public double PointForceN;
        public double WallCapacityN;     // 被连接薄壁件抗剪强度设计值
        public double FastenerCapacityN; // 单个钉最大抗剪承载力设计值
        public bool WallOk => PointForceN <= WallCapacityN;
        public bool FastenerOk => PointForceN <= FastenerCapacityN;
    }

    public sealed class Result
    {
        public bool Calculated;
        public double WindShearN;     // 风分项（未舍入链 → round3）。基线 10275。
        public double SeismicShearN;  // 地震项。基线 0。
        public double TotalShearN;    // V。基线 10275。
        public FastenerCheck? FrameSide;     // 连接片与门窗边框（卡槽连接时 null）
        public FastenerCheck? SteelFrameToWallA; // 有附框：连接片与钢附框
        public FastenerCheck? SteelFrameToWallB; // 有附框：钢附框与边框
        public double StructPointForceN;     // 结构侧计算值（÷pB 后）
        public double StructCapacityN;       // 4551 / 3956.4
        public bool StructOk => StructPointForceN <= StructCapacityN;
    }

    private static double R(double v, int d = 3) => WindLoadEngine.VbaRound(v, d);

    /// <summary>薄壁件设计值（f37/f24/r/插值统一规则；tSelf=连接件自身厚=插值除数）。</summary>
    private static double WallCapacity(double tSelf, double tOther, double d, double fa)
    {
        double f37 = R(3.7 * Math.Sqrt(tSelf * tSelf * tSelf * d) * fa);
        double f24 = R(2.4 * tSelf * d * fa);
        double r = R(Math.Max(tSelf, tOther) / Math.Min(tSelf, tOther));
        if (r == 1.0) return Math.Min(f37, f24);
        return R(f37 + (f24 - f37) * (r - 1.0) / tSelf);
    }

    private static string FastenerLabel(int type) => FastenerTypeNames[type];

    public static Result Compute(Input it)
    {
        var r = new Result { Calculated = it.CalcConnector };
        if (!it.CalcConnector) return r;

        // ═══ 总剪力 V（0x14049D3–0x1404DB6）═══
        r.WindShearN = R(it.WkNmm2 * 1.5 * it.OpeningHeightMm * it.OpeningWidthMm);
        r.SeismicShearN = R(7 * it.SeismicAlphaMax * it.SeismicAreaNmm2
                            * it.OpeningHeightMm * it.OpeningWidthMm * 0.5);
        r.TotalShearN = R(r.WindShearN + r.SeismicShearN);

        double basePoint = R(R(1.25 * r.TotalShearN / it.ConnectorCount));

        // ═══ 连接片↔门窗边框（卡槽连接时整块跳过）═══
        if (it.ScrewConnection)
        {
            double faWall = MaterialShearMpa[it.WallMaterialIndex];
            r.FrameSide = new FastenerCheck
            {
                BlockLabel = "连接片与门窗边框连接处",
                FastenerRowPrefix = "连接片与边框连接处",
                FastenerLabel = FastenerLabel(it.FrameFastenerType),
                PointForceN = R(basePoint / it.PlateCountFrameSide),
                WallCapacityN = WallCapacity(it.PlateThicknessMm, it.WallThicknessMm,
                                             it.FrameScrewDiameter, faWall),
                FastenerCapacityN = FastenerCapacityN(it.FrameFastenerType, it.FrameScrewDiameter),
            };
        }

        // ═══ 有钢附框：A 连接片↔钢附框 / B 钢附框↔边框 ═══
        if (it.HasSteelFrame)
        {
            // A 块钉固定自攻自钻 3.5（链 0x13FE5CD 常数 3.5；附框规格/类型下拉不影响 A：
            //   遍历中 A 设计值 2596.28 / 承载力 1346 恒定，probe4a framed_v1..v4/framed_t1 实证）
            double capA = WallCapacity(it.PlateThicknessMm, it.SteelFrameThicknessMm,
                                       3.5, it.SteelFrameShearMpa);
            r.SteelFrameToWallA = new FastenerCheck
            {
                BlockLabel = "连接片与钢附框连接处",
                FastenerRowPrefix = "连接片与钢附框连接处",
                FastenerLabel = FastenerLabel(4),
                PointForceN = basePoint,   // 附框链无额外除数（0x13FE085–0x13FE187）
                WallCapacityN = capA,
                FastenerCapacityN = FastenerCapacityN(4, 3.5),
            };
            double capB = WallCapacity(it.WallThicknessMm, it.SteelFrameThicknessMm,
                                       it.SteelFrameScrewDiameter,
                                       MaterialShearMpa[it.WallMaterialIndex]);
            r.SteelFrameToWallB = new FastenerCheck
            {
                BlockLabel = "钢附框与边框连接处",
                FastenerRowPrefix = "钢附框与边框连接处",
                FastenerLabel = FastenerLabel(it.FrameScrewType == 0 ? 3 : 4),
                PointForceN = basePoint,   // 0x14009C7–0x14009E3
                WallCapacityN = capB,
                FastenerCapacityN = FastenerCapacityN(it.FrameScrewType == 0 ? 3 : 4,
                                                      it.SteelFrameScrewDiameter),
            };
        }

        // ═══ 连接片与建筑结构（0x1407CF6/0x1408036–0x1408063）═══
        r.StructPointForceN = R(basePoint / it.PlateCountStructSide);
        r.StructCapacityN = it.ExpansionMode
            ? ExpansionStructCapacityN
            : R(3.7 * it.PlateThicknessMm * it.StructScrewDiameter * it.PlateShearMpa);
        return r;
    }

    /// <summary>拼 V31 输出原文样式（块序与分派实采一致：玻璃行 → 边框块 → 附框A → 附框B → 结构块）。</summary>
    public static string FormatBook(Result r, double wkKpa)
    {
        if (!r.Calculated) return "";
        string Y(double v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        string Ok(bool b) => b ? "满足要求(√)" : "不满足要求(×)";
        string Sep() => new string('*', 38);
        var parts = new List<string>
        {
            Sep(),
            $"玻璃部分风荷载标准值为:{Y(wkKpa)}KPa",
            Sep(),
        };
        void Block(FastenerCheck f)
        {
            parts.Add(f.BlockLabel + ":");
            parts.Add($"被连接的薄壁件抗剪强度计算值:{Y(f.PointForceN)}N");
            parts.Add($"被连接的薄壁件抗剪强度设计值:{Y(f.WallCapacityN)}N");
            parts.Add(Ok(f.WallOk));
            parts.Add(new string('—', 19));
            parts.Add($"{f.FastenerRowPrefix}单个{f.FastenerLabel}的最大抗剪承载力设计值:{Y(f.FastenerCapacityN)}N");
            parts.Add($"{f.FastenerRowPrefix}单个{f.FastenerLabel}的实际受力为:{Y(f.PointForceN)}N");
            parts.Add(Ok(f.FastenerOk));
            parts.Add(Sep());
        }
        if (r.FrameSide is { } a) Block(a);
        if (r.SteelFrameToWallA is { } b) Block(b);
        if (r.SteelFrameToWallB is { } c) Block(c);
        parts.Add("连接片与建筑结构连接处:");
        parts.Add($"连接片的抗剪强度计算值:{Y(r.StructPointForceN)}N");
        parts.Add($"连接片的抗剪强度设计值:{Y(r.StructCapacityN)}N");
        parts.Add(Ok(r.StructOk));
        parts.Add(Sep());
        return string.Join("  ", parts);
    }
}
