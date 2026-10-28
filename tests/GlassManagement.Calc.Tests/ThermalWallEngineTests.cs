using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// CW27「公共建筑--非透明幕墙热工计算」对拍：
/// V31 实机 b7_probe11_cw27.json（消息法选上海→预演；frida varhook 21 条 + rtcRound 7 点）。
/// 输出基线同 b7_probe6_cw27.json（K 0.283 / 内表面 17.379 / 露点 0.188）。
/// 默认层 = 参数库「热工数值」表行 1-3（Adodc 数据源；运行期层热阻 fp 位一致）。
/// </summary>
public class ThermalWallEngineTests
{
    [Fact]
    public void 非透明热工_默认态上海_probe11逐位()
    {
        var r = ThermalWallEngine.Compute(new ThermalWallEngine.Input());
        Assert.Equal(-1.9499999999999997, r.TeMinWeighted);   // V 0x16CFD68~：0.3·0.5+0.7·(−3.0) 不 round
        Assert.Equal(3.2015673981191224, r.RLayersSum);       // V 0x16D1C9F 累加：10/0.3+240/1.74+100/0.033（÷1000）
        Assert.Equal(3.382, r.RAfterRsi);                     // R0 0x16D2B3D: round3(3.3815673981191225)
        Assert.Equal(3.492, r.RWithAir);                      // V 0x16D35C8: +0.11 不 round
        Assert.Equal(3.532, r.RTot);                          // R1 0x16D35F4: round5(3.492+0.04)
        Assert.Equal(0.283, r.KWinter);                       // R2 0x16D36D8: round3(1/3.532)=0.28312570781426954
        Assert.Equal(20.646, r.EIndoor);                      // R3 0x16D43A9: 6.11·10^(7.5·18/255.3)
        Assert.Equal(6.194, r.EiPhi);                         // R4 0x16D45BC: round3(0.3·20.646=6.1938)
        Assert.Equal(0.188, r.DewPoint);                      // R5 0x16D4746: Magnus 反变换
        Assert.Equal(17.379, r.Tsi);                          // R6 0x16D4922: 18−19.95·0.11/3.532
    }

    [Fact]
    public void 非透明热工_输出正文两段_probe6逐字()
    {
        var it = new ThermalWallEngine.Input();
        var r = ThermalWallEngine.Compute(it);
        string s = ThermalWallEngine.FormatOutput(it, r);
        Assert.Contains("幕墙的冬季K值为:0.283W/(m2·K)", s);
        Assert.Contains("按要求，幕墙的K值不应大于:0.8W/(m2·K)", s);
        Assert.Contains("幕墙室内表面的计算温度为:17.379℃", s);
        Assert.Contains("空气的露点温度为:0.188℃", s);
        Assert.DoesNotContain("(×)", s); // probe6 实录两段均判「√」
        Assert.Contains("满足要求(√)", s);
    }

    [Fact]
    public void 非透明热工_层参数库默认行_fp位一致()
    {
        // 参数库「热工数值」表行 1-3：千丝板 10/0.3、钢筋混凝土 240/1.74、聚苯乙烯泡沫塑料 100/0.033
        var it = new ThermalWallEngine.Input();
        Assert.Equal(3, it.Layers.Count);
        Assert.Equal(0.03333333333333333, it.Layers[0].RValue);
        Assert.Equal(0.13793103448275862, it.Layers[1].RValue);
        Assert.Equal(3.0303030303030303, it.Layers[2].RValue);
    }

    [Fact]
    public void 非透明热工_湿度60_probe12逐位()
    {
        // probe12 humid60：R 0x16D45BC round3(0.6·20.646=12.3876)→12.388；R 0x16D4746→10.127；tsi 不变 17.379
        var it = new ThermalWallEngine.Input { PhiIndoor = 0.6 };
        var r = ThermalWallEngine.Compute(it);
        Assert.Equal(12.388, r.EiPhi);
        Assert.Equal(10.127, r.DewPoint);
        Assert.Equal(17.379, r.Tsi);
    }

    [Theory]
    // P4 · 空气层档位 9 档逐位（2026-10-27 b7_probe22_airgap.json）
    // 类型 0 一般 / 1 单面铝箔 / 2 双面铝箔；热流 0 向下 / 1 向上 / 2 垂直
    [InlineData(0, 0, 0.20, 0.282)]   // 一般 + 热流向下
    [InlineData(0, 1, 0.17, 0.284)]   // 一般 + 热流向上
    [InlineData(0, 2, 0.18, 0.283)]   // 一般 + 垂直（默认档，= 旧固定 Rsi 0.18）
    [InlineData(1, 0, 0.64, 0.251)]   // 单面铝箔 + 热流向下
    [InlineData(1, 1, 0.43, 0.264)]   // 单面铝箔 + 热流向上
    [InlineData(1, 2, 0.50, 0.260)]   // 单面铝箔 + 垂直
    [InlineData(2, 0, 1.01, 0.229)]   // 双面铝箔 + 热流向下
    [InlineData(2, 1, 0.57, 0.255)]   // 双面铝箔 + 热流向上
    [InlineData(2, 2, 0.71, 0.246)]   // 双面铝箔 + 垂直
    public void 非透明热工_空气层档位9档_probe22逐位(int type, int flow, double rsi, double k)
    {
        Assert.Equal(rsi, ThermalWallEngine.AirGapRsi(type, flow));
        var it = new ThermalWallEngine.Input { AirGapTypeIndex = type, AirGapFlowIndex = flow };
        var r = ThermalWallEngine.Compute(it);
        Assert.Equal(k, r.KWinter);
    }

    [Fact]
    public void 非透明热工_非采暖房间_probe12逐位()
    {
        // probe12 heat_alt（房间采暖情况 CB@623,431=非采暖房间）：e_i 链运行期直读 t_i=12
        //（7.5·12、237.3+12）→ R 0x16D43A9=14.03、0x16D45BC=4.209、露点 −5.013、tsi 11.566
        var it = new ThermalWallEngine.Input { TIndoor = 12 };
        var r = ThermalWallEngine.Compute(it);
        Assert.Equal(14.03, r.EIndoor);
        Assert.Equal(4.209, r.EiPhi);
        Assert.Equal(-5.013, r.DewPoint);
        Assert.Equal(11.566, r.Tsi);
    }

    [Fact]
    public void 非透明热工_居住建筑_probe28逐位()
    {
        // b7_probe28 res_wall：居住建筑非透明幕墙 K≤1.0、t_i=18（结露同公共）。
        var it = new ThermalWallEngine.Input
        {
            Building = BuildingType.居住建筑,
            KLimb = ThermalLimits.WallKLimb(BuildingType.居住建筑),
        };
        var r = ThermalWallEngine.Compute(it);
        var s = ThermalWallEngine.FormatOutput(it, r);
        Assert.Contains("幕墙的K值不应大于:1W/(m2·K)", s);
        Assert.Equal(17.379, r.Tsi);   // t_i=18
        Assert.Equal(0.188, r.DewPoint);
    }

    [Fact]
    public void 非透明热工_工业建筑_probe28逐位()
    {
        // b7_probe28 ind_wall：工业建筑非透明幕墙 K≤1.1（建筑整体K值）、t_i=16。
        // 独立文案：GB51245 提醒 + 结露「不会有结露现象」。
        var it = new ThermalWallEngine.Input
        {
            Building = BuildingType.工业建筑,
            KLimb = ThermalLimits.WallKLimb(BuildingType.工业建筑),
            TIndoor = ThermalLimits.WallTIndoor(BuildingType.工业建筑),
        };
        var r = ThermalWallEngine.Compute(it);
        var s = ThermalWallEngine.FormatOutput(it, r);
        Assert.Contains("GB51245-2017", s);
        Assert.Contains("建筑整体K值不应大于:1.1W/(m2·K)", s);
        Assert.Contains("不会有结露现象。", s);
        Assert.Equal(15.441, r.Tsi);   // t_i=16
        Assert.Equal(-1.546, r.DewPoint);
    }
}
