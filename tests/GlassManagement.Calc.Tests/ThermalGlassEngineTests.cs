using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// CW26「公共建筑--透明幕墙（门窗）热工计算」对拍：
/// V31 实机 b7_probe6_cw26.json（选上海→预演，RichText 输出全文）+
/// b7_probe7_cw26.json（frida var 链 70 条 + rtcRound 22 点，逐位舍入序）。
/// 城市行 = GB50176 参数库「上海,3A,3,0.5,-3.0」。
/// </summary>
public class ThermalGlassEngineTests
{
    [Fact]
    public void 玻璃热工_默认态上海_probe7逐位()
    {
        var r = ThermalGlassEngine.Compute(new ThermalGlassEngine.Input());
        Assert.Equal(-1.9499999999999997, r.TeMinWeighted); // V02: 0.3·0.5+0.7·(−3.0)（不 round，FP 原值）
        Assert.Equal(0.008, r.RGlass);                   // V10~V11→R04: 6/1000/0.76
        Assert.Equal(5.464, r.KGlassWinter);             // V16/R06（输出「玻璃自身K值」）
        Assert.Equal(4.926, r.KGlassSummer);             // V21/R08（1/6.9 侧，不在输出正文）
        Assert.Equal(5.961, r.KSystemWinter);            // V29/R10（输出「门窗系统冬季K值」）
        Assert.Equal(5.496, r.KSystemSummer);            // V35/R11
        Assert.Equal(0.161, r.ShgcFrame);                // V39/R12（框 SHGC）
        Assert.Equal(0.722, r.ShgcWeightedRaw);          // V43/R13
        Assert.Equal(0.83, r.Sc);                        // V44/R14: 0.722/0.87
        Assert.Equal(0.731, r.ShgcSummer);               // V50/R15（输出「夏季计算值」）
        Assert.Equal(0.725, r.ShgcWinter);               // V56/R16（输出「冬季计算值」）
        Assert.Equal(0.649, r.TauWeighted);              // V58/R17（加权；正文回显输入 0.75）
        Assert.Equal(0.75, r.TauDisplay);                // 输出正文「玻璃可见光透射比为:0.75」
        Assert.Equal(23.389, r.EIndoorRaw);              // V63/R18: 6.11·10^(7.5·20/257.3)
        Assert.Equal(7.017, r.EiPhi);                    // V64/R19: 0.3×23.389
        Assert.Equal(1.917, r.DewPoint);                 // V65→R20: Magnus 反变换
        Assert.Equal(5.008, r.Tsi);                      // V66~V69/R21
        Assert.Equal(-0.0005, r.UnattributedR09);        // R09 旁链（未归因登记）
    }

    [Fact]
    public void 玻璃热工_输出正文四段判定_probe6逐字()
    {
        var it = new ThermalGlassEngine.Input();
        var r = ThermalGlassEngine.Compute(it);
        string s = ThermalGlassEngine.FormatOutput(it, r);
        Assert.Contains("玻璃自身K值为:5.464W/(m2·K)", s);
        Assert.Contains("门窗系统冬季K值为:5.961W/(m2·K)", s);
        Assert.Contains("按要求，此处K值不应大于:2.2W/(m2·K)", s);
        Assert.Contains("计算单元的太阳得热系数夏季计算值为:0.731", s);
        Assert.Contains("计算单元的太阳得热系数冬季计算值为:0.725", s);
        Assert.Contains("玻璃可见光透射比为:0.75", s);
        Assert.Contains("窗墙比为:0.32", s);
        Assert.Contains("室内表面的计算温度为:5.008℃", s);
        Assert.Contains("空气的露点温度为:1.917℃", s);
        // K/SHGC 超限行判「×」，透射比/结露判「√」（V31 输出实录）
        Assert.Contains("不满足要求(×)", s);
        Assert.Contains("满足要求(√)", s);
    }

    [Fact]
    public void 玻璃热工_露点Magnus链_IDA常量()
    {
        // IDA 反汇编常量：6.11@0x40BCB8、7.5@0x4055E0、1.0@0x4019C0、237.3@0x40BCB0、10@0x401710
        // e_i(20℃)=23.389→×0.3=7.017→td=237.3/(7.5/log10(7.017/6.11)−1)=1.917（R20 输入 1.9172550535382198）
        var r = ThermalGlassEngine.Compute(new ThermalGlassEngine.Input());
        Assert.Equal(23.389, r.EIndoorRaw);
        Assert.Equal(1.917, r.DewPoint);
    }

    [Fact]
    public void 玻璃热工_框材质九项_probe10逐位()
    {
        // b7_probe10_frameK.json：frida var 链「Mul 1.217 Kf」直读 + 预演输出逐位。
        // 钢框 Kf=9.139 与普通铝合金同值 → 输出逐位=默认态（probe8e material_steel 零差异实证）。
        var cases = new (string M, double Kf, double K, double S, double W)[]
        {
            ("普通铝合金型材", 9.139, 5.961, 0.731, 0.725),
            ("断热铝合金型材", 3.318, 5.174, 0.712, 0.709),
            ("聚氨酯塑钢带筋", 2.8, 5.104, 0.710, 0.708),
            ("PVC两腔无金属筋", 2.2, 5.023, 0.708, 0.706),
            ("PVC两腔有金属筋", 2.7, 5.090, 0.710, 0.708),
            ("PVC三腔无金属筋", 2.0, 4.996, 0.707, 0.706),
            ("木框", 1.8, 4.969, 0.707, 0.705),
            ("木-金属框", 1.8, 4.969, 0.707, 0.705),
            ("钢框", 9.139, 5.961, 0.731, 0.725),
        };
        foreach (var (m, kf, k, s, w) in cases)
        {
            Assert.Equal(kf, ThermalGlassEngine.FrameK(m));
            var it = new ThermalGlassEngine.Input { KFrame = kf };
            var r = ThermalGlassEngine.Compute(it);
            Assert.Equal(k, r.KSystemWinter);   // 「门窗系统冬季K值」
            Assert.Equal(s, r.ShgcSummer);      // 「夏季计算值」
            Assert.Equal(w, r.ShgcWinter);      // 「冬季计算值」
        }
    }

    [Fact]
    public void 玻璃热工_室内湿度60_probe9逐位()
    {
        // probe9 var 段：Mul 0.6·23.389→14.0334 →R(round3)=14.033 → ÷6.11 → Magnus 反变换
        // →R20 in=12.003439220809653 → 12.003（probe8e humid60 输出实录，判×）。
        var it = new ThermalGlassEngine.Input { PhiIndoor = 0.6 };
        var r = ThermalGlassEngine.Compute(it);
        Assert.Equal(14.033, r.EiPhi);
        Assert.Equal(12.003, r.DewPoint);
        Assert.Equal(5.008, r.Tsi); // 湿度不进 te 链（probe8e：切 60 后 tsi 仍 5.008）
        var s = ThermalGlassEngine.FormatOutput(it, r);
        Assert.Contains("空气的露点温度为:12.003℃", s);
    }

    [Fact]
    public void 玻璃热工_扰动逐位_probe8e系列()
    {
        // 玻璃 SHGC 0.81→0.4（probe8e shgc0.4：0.377 / 0.371）
        var a = ThermalGlassEngine.Compute(new ThermalGlassEngine.Input { ShgcGlass = 0.4 });
        Assert.Equal(0.377, a.ShgcSummer);
        Assert.Equal(0.371, a.ShgcWinter);
        // 内片厚 6→12（probe8e thk_in12：5.236 / 5.764 / tsi 5.634）
        var b = ThermalGlassEngine.Compute(new ThermalGlassEngine.Input { GlassThkMm = 12 });
        Assert.Equal(5.236, b.KGlassWinter);
        Assert.Equal(5.764, b.KSystemWinter);
        Assert.Equal(5.634, b.Tsi);
        // 窗墙比 0.32→0.5：SHGC 夏季限值档 0.35→0.3（probe8e wwr0.5 实录文案）
        var c = ThermalGlassEngine.Compute(new ThermalGlassEngine.Input
            { WindowWallRatio = 0.5, ShgcSummerLimit = 0.3 });
        Assert.Contains("太阳得热系数夏季不应大于:0.3      ", ThermalGlassEngine.FormatOutput(
            new ThermalGlassEngine.Input { WindowWallRatio = 0.5, ShgcSummerLimit = 0.3 }, c));
        // 建筑类别乙类：K 限值 2.2→3、SHGC 限值 0.35→0.45（probe8e cls_B 实录文案）
        var itD = new ThermalGlassEngine.Input { KLimb = 3, ShgcSummerLimit = 0.45 };
        var d = ThermalGlassEngine.Compute(itD);
        var sd = ThermalGlassEngine.FormatOutput(itD, d);
        Assert.Contains("按要求，此处K值不应大于:3W/(m2·K)", sd);
        Assert.Contains("太阳得热系数夏季不应大于:0.45", sd);
    }

    [Fact]
    public void 玻璃热工_七构型分层热阻链_probe23逐位()
    {
        // b7_probe23_multipane.json：7 种玻璃结构逐一切换 + 预演 frida 链直读。
        // 每构型 (玻璃片数, PVB 层数, 气隙腔数)：
        //   单片(1,0,0) 夹层(2,1,0) 中空(2,0,1) 中空+内夹层(3,1,1) 中空+外夹层(3,1,1) 双夹层中空(4,2,1) 双中空(3,0,2)
        // RGlass = round3(Σ片 6/1000/0.76 + ΣPVB 0.76/1000/0.16)
        // RGap   = round3(N腔 × 1/(round3(0.0241/0.009)+round3(2.268e-7·273³/(2/0.837−1)))) = round3(N腔 × 0.166694)
        // K冬    = round3(1/round3(1/19.9 + RGap + RGlass + 1/8))
        var cases = new (ThermalGlassEngine.GlassStructure S, double Rg, double Rgap, double Kwin)[]
        {
            (ThermalGlassEngine.GlassStructure.单片玻璃,     0.008, 0.000, 5.464),
            (ThermalGlassEngine.GlassStructure.夹层玻璃,     0.021, 0.000, 5.102),
            (ThermalGlassEngine.GlassStructure.中空玻璃,     0.016, 0.167, 2.793),
            (ThermalGlassEngine.GlassStructure.中空加内夹层, 0.028, 0.167, 2.703),
            (ThermalGlassEngine.GlassStructure.中空加外夹层, 0.028, 0.167, 2.703),
            (ThermalGlassEngine.GlassStructure.双夹层中空,   0.041, 0.167, 2.611),
            (ThermalGlassEngine.GlassStructure.双中空玻璃,   0.024, 0.333, 1.880),
        };
        foreach (var (s, rg, rgap, kwin) in cases)
        {
            var r = ThermalGlassEngine.Compute(new ThermalGlassEngine.Input { Structure = s });
            Assert.Equal(rg, r.RGlass);   // 玻璃层总热阻（含 PVB）
            Assert.Equal(rgap, r.RGap);   // 气隙总热阻（N 腔）
            Assert.Equal(kwin, r.KGlassWinter);  // 「玻璃自身K值」冬季
        }
    }

    [Fact]
    public void 玻璃热工_夏季K_仅无气隙构型计算_probe23逐位()
    {
        // 无气隙构型（单片/夹层）在 V31 __vbaVar 链中计算夏季 K（1/6.9 侧）。
        var a = ThermalGlassEngine.Compute(new ThermalGlassEngine.Input { Structure = ThermalGlassEngine.GlassStructure.单片玻璃 });
        Assert.Equal(4.926, a.KGlassSummer);   // R夏=round3(1/19.9+0+0.008+1/6.9)=0.203 → 1/0.203
        var b = ThermalGlassEngine.Compute(new ThermalGlassEngine.Input { Structure = ThermalGlassEngine.GlassStructure.夹层玻璃 });
        Assert.Equal(4.63, b.KGlassSummer);    // R夏=round3(1/19.9+0+0.021+1/6.9)=0.216 → 1/0.216
        // 含气隙构型 V31 只算冬 K，夏 K 保持 0（probe23 链无 1/6.9 与系统夏 K 加权）
        foreach (var s in new[]
        {
            ThermalGlassEngine.GlassStructure.中空玻璃,
            ThermalGlassEngine.GlassStructure.中空加内夹层,
            ThermalGlassEngine.GlassStructure.中空加外夹层,
            ThermalGlassEngine.GlassStructure.双夹层中空,
            ThermalGlassEngine.GlassStructure.双中空玻璃,
        })
        {
            var r = ThermalGlassEngine.Compute(new ThermalGlassEngine.Input { Structure = s });
            Assert.Equal(0, r.KGlassSummer);
            Assert.Equal(0, r.KSystemSummer);
        }
    }

    [Fact]
    public void 玻璃热工_气隙辐射项_物理常量直读()
    {
        // 中空玻璃单腔：h_r = 2.268e-7 × 273³ / (2/0.837 − 1) = 3.321060097486844 → round3 = 3.321
        // h_c = 0.0241/0.009 = 2.677778 → 2.678；R_gap = round3(1/5.999) = 0.167
        var r = ThermalGlassEngine.Compute(new ThermalGlassEngine.Input { Structure = ThermalGlassEngine.GlassStructure.中空玻璃 });
        Assert.Equal(0.167, r.RGap);
        Assert.Equal(2.793, r.KGlassWinter);
    }

    [Fact]
    public void 玻璃热工_居住建筑_probe28逐位()
    {
        // b7_probe28 res_glass：居住建筑玻璃幕墙 K≤2.5、SHGC≤0.4、t_i=20（结露同公共）。
        // 透射比文案与公共建筑相同（含 GB50189+GB55015 两段写死）。
        var it = new ThermalGlassEngine.Input
        {
            Building = BuildingType.居住建筑,
            KLimb = ThermalLimits.GlassKLimb(BuildingType.居住建筑),
            ShgcSummerLimit = ThermalLimits.GlassShgcLimit(BuildingType.居住建筑),
            TIndoor = ThermalLimits.GlassTIndoor(BuildingType.居住建筑),
        };
        var r = ThermalGlassEngine.Compute(it);
        var s = ThermalGlassEngine.FormatOutput(it, r);
        Assert.Contains("按要求，此处K值不应大于:2.5W/(m2·K)", s);
        Assert.Contains("太阳得热系数夏季不应大于:0.4", s);
        Assert.Contains("GB50189-2015", s);
        Assert.Contains("GB55015-2021", s);
        Assert.Equal(5.008, r.Tsi);   // t_i=20
        Assert.Equal(1.917, r.DewPoint);
    }

    [Fact]
    public void 玻璃热工_工业建筑_probe28逐位()
    {
        // b7_probe28 ind_glass：工业建筑玻璃幕墙 K≤3.4（建筑整体K值）、SHGC≤0.6、t_i=16。
        // 独立文案：GB51245 提醒 + 透射比「不作要求」+ 结露「不会有结露现象」。
        var it = new ThermalGlassEngine.Input
        {
            Building = BuildingType.工业建筑,
            KLimb = ThermalLimits.GlassKLimb(BuildingType.工业建筑),
            ShgcSummerLimit = ThermalLimits.GlassShgcLimit(BuildingType.工业建筑),
            TIndoor = ThermalLimits.GlassTIndoor(BuildingType.工业建筑),
        };
        var r = ThermalGlassEngine.Compute(it);
        var s = ThermalGlassEngine.FormatOutput(it, r);
        Assert.Contains("GB51245-2017", s);
        Assert.Contains("按要求，建筑整体K值不应大于:3.4W/(m2·K)", s);
        Assert.Contains("太阳得热系数不应大于:0.6", s);
        Assert.Contains("工业建筑的可见光透射比不作要求！", s);
        Assert.Contains("不会有结露现象。", s);
        Assert.Equal(3.74, r.Tsi);     // t_i=16
        Assert.Equal(-1.546, r.DewPoint);
    }
}
