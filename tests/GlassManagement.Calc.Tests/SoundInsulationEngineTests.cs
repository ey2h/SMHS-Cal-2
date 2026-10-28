using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 空气声隔声（菜单 id=137）对拍：默认单片 4mm → R=26.639dB（b7_probe 运行期直读）。
/// 质量定律 R = 13.5·lg(t×2.56) + 13，密度 2.56 = 参数库「玻璃材料」表。
/// </summary>
public class SoundInsulationEngineTests
{
    [Fact]
    public void 空气声隔声_单片4mm_probe直读()
    {
        Assert.Equal(26.639, SoundInsulationEngine.SinglePane(4));
    }

    [Theory]
    [InlineData(6, 29.016)]
    [InlineData(8, 30.703)]
    [InlineData(10, 32.011)]
    [InlineData(12, 33.08)]
    public void 空气声隔声_质量定律_单片(double t, double expected)
    {
        Assert.Equal(expected, SoundInsulationEngine.SinglePane(t));
    }

    [Theory]
    [InlineData(SoundInsulationEngine.GlassStructure.单片玻璃, 26.639)]
    [InlineData(SoundInsulationEngine.GlassStructure.中空玻璃, 31.203)]
    [InlineData(SoundInsulationEngine.GlassStructure.夹层玻璃, 34.703)]
    [InlineData(SoundInsulationEngine.GlassStructure.中空加夹层, 37.58)]
    [InlineData(SoundInsulationEngine.GlassStructure.三玻双中空, 34.08)]
    [InlineData(SoundInsulationEngine.GlassStructure.三玻双夹层, 41.08)]
    [InlineData(SoundInsulationEngine.GlassStructure.四玻双夹层中空, 47.331)]
    [InlineData(SoundInsulationEngine.GlassStructure.四玻夹层双中空, 43.831)]
    public void 空气声隔声_八结构默认厚度_probe35逐位(SoundInsulationEngine.GlassStructure s, double expected)
    {
        Assert.Equal(expected, SoundInsulationEngine.ComputeDefault(s));
    }

    // ★ 2026-10-28 T12b 纠正：实机隔声帧 RichText 直读，分级 = 国标级 + 1
    //   实测 4 点：26.639→3/4/2、31.903→4/5/3、34.703→4/5/3、34.78→4/5/3
    [Theory]
    [InlineData(26.639, 3, 4, 2)]
    [InlineData(31.203, 4, 5, 3)]
    [InlineData(37.58, 5, 6, 4)]
    [InlineData(41.08, 6, 7, 5)]
    [InlineData(47.331, 7, 8, 6)]
    public void 空气声隔声_三标准分级_实机直读(double r, int g8485, int g50121, int g21086)
    {
        Assert.Equal(g8485, SoundInsulationEngine.Grade8485(r));
        Assert.Equal(g50121, SoundInsulationEngine.Grade50121(r));
        Assert.Equal(g21086, SoundInsulationEngine.Grade21086(r));
    }

    // ─────────────────────────────────────────────────────────────
    // T11 气隙厚度取证（2026-10-28，V31 实机中空气隙档扫描 t11_sound_scan.json）
    // ★ 气隙每 +1mm R+0.1dB（旧「0.5×个数」是默认 5mm 的巧合）；8 结构默认态 + 气隙 5 档 13 点全对拍
    // ─────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(5.0, 31.203)]
    [InlineData(6.0, 31.303)]
    [InlineData(9.0, 31.603)]
    [InlineData(12.0, 31.903)]
    [InlineData(15.0, 32.203)]
    public void 空气声隔声_气隙厚度档_V31实机对拍(double gapMm, double expected)
    {
        // 中空玻璃（总厚 8mm、PVB 0、1 腔），V31 实机逐档预演值
        Assert.Equal(expected, SoundInsulationEngine.Compute(
            SoundInsulationEngine.GlassStructure.中空玻璃, 8.0, gapMm));
    }

    // ─────────────────────────────────────────────────────────────
    // T11 组合墙隔声（链级还原 t11_wall_chain.json：ra 0x1731478-0x1731524）
    // R_组合 = R_玻璃 + 10·lg((t_墙+t_等效)/t_等效)；V31 实机输出 35.483/35.945/36.674
    // ─────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(50.0, 39.06, 35.483)]
    [InlineData(60.0, 39.06, 35.945)]
    [InlineData(50.0, 25.0, 36.674)]
    public void 空气声隔声_组合墙_V31链级对拍(double wall, double equiv, double expected)
    {
        double rGlass = SoundInsulationEngine.Compute(
            SoundInsulationEngine.GlassStructure.中空玻璃, 8.0, 12.0);   // 31.903
        Assert.Equal(31.903, rGlass, 3);
        Assert.Equal(expected, SoundInsulationEngine.ComputeWall(rGlass, wall, equiv), 3);
    }

    [Fact]
    public void 空气声隔声_默认气隙等价性_新公式与旧直读一致()
    {
        // 默认态（气隙 5mm/腔）下新公式必须复现全部 b7 直读值
        var cases = new (SoundInsulationEngine.GlassStructure s, double t, double r)[]
        {
            (SoundInsulationEngine.GlassStructure.单片玻璃, 4, 26.639),
            (SoundInsulationEngine.GlassStructure.中空玻璃, 8, 31.203),
            (SoundInsulationEngine.GlassStructure.夹层玻璃, 8, 34.703),
            (SoundInsulationEngine.GlassStructure.中空加夹层, 12, 37.58),
            (SoundInsulationEngine.GlassStructure.三玻双中空, 12, 34.08),
            (SoundInsulationEngine.GlassStructure.三玻双夹层, 12, 41.08),
            (SoundInsulationEngine.GlassStructure.四玻双夹层中空, 32, 47.331),
            (SoundInsulationEngine.GlassStructure.四玻夹层双中空, 32, 43.831),
        };
        foreach (var (s, t, r) in cases)
            Assert.Equal(r, SoundInsulationEngine.ComputeDefault(s));
    }
}
