using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 连接计算屏逐位对拍（构件式全隐框，V31 实机）。
///
/// 期望值全部来自 V31 槽数组直读 + 计算书原文，不是手算；
/// 算式来源见 docs/V31_CONNECTION_CODE_MAP.md（0xA8FA83–0xAA0002 + __vbaVar* 轨迹）。
/// 判据：逐位一致（ToString("F3") 完全相等），不是"约等于"。
/// </summary>
public class ConnectEngineTests
{
    private static ConnectResult Run() => ConnectEngine.Compute(ConnectEngine.DefaultInputs());

    private static void Same(string label, double actual, double expected)
        => Assert.Equal(expected.ToString("F3"), actual.ToString("F3"));

    [Fact]
    public void 横梁与角码段_逐位一致()
    {
        var r = Run();
        Same("风项 槽14", r.WindMomentTerm, 583.069);
        Same("地震项 槽17", r.QuakeMomentTerm, 72.6);
        Same("实际受力 槽19", r.BeamForce, 633.889);
        Same("单栓承载力 槽23", r.BeamBoltCapacity, 3821.544);
        Same("螺栓设计值 槽24", r.BeamBoltDesign, 0.166);
        Same("横梁壁抗承压 槽30", r.BeamWebBearing, 6660);
    }

    [Fact]
    public void 立柱与角码段_逐位一致()
    {
        var r = Run();
        Same("立柱受力 槽36", r.ColumnForce, 699.598);
        Same("螺栓设计值 槽41", r.ColumnBoltDesign, 0.183);
        Same("立柱壁抗承压 槽45", r.ColumnWebBearing, 6660);
        Same("角码壁抗承压 槽50", r.BracketBearing, 11100);
    }

    [Fact]
    public void 立柱与主体段_逐位一致()
    {
        var r = Run();
        Same("实际受力 槽60", r.MainForce, 7304.379);
        Same("单栓承载力 槽64", r.MainBoltCapacity, 32005.357);
        Same("螺栓设计值 槽65", r.MainBoltDesign, 0.228);
        Same("立柱壁抗承压 槽68", r.MainColumnBearing, 26640);
        Same("角码壁抗承压 槽73", r.MainBracketBearing, 87840);
    }

    [Fact]
    public void 计算书首行风荷载_kPa()
    {
        Same("wk→kPa", Run().WkKpa, 1.285);
    }

    /// <summary>
    /// 扰动实验回归：横框角码厚度 5→7 时，V31 实机 slot50 从 11100 变 15540
    /// （15540 = 2×6×7×185）。这条锁住"角码厚度以乘数身份参与承压"。
    /// </summary>
    [Fact]
    public void 角码厚度扰动_与实机一致()
    {
        var x = ConnectEngine.DefaultInputs() with { MidBracketThickness = 7 };
        Same("角码壁抗承压", ConnectEngine.Compute(x).BracketBearing, 15540);
    }

    /// <summary>扰动实验回归：勾选「单侧角码」后实机 slot73 从 87840 变 43920。</summary>
    [Fact]
    public void 单侧角码扰动_与实机一致()
    {
        var x = ConnectEngine.DefaultInputs() with { BracketSides = 1 };
        var r = ConnectEngine.Compute(x);
        Same("角码壁抗承压(单侧)", r.MainBracketBearing, 43920);
        Same("立柱壁抗承压不受影响", r.MainColumnBearing, 26640);
    }

    /// <summary>扰动实验回归：主体角码壁厚 6→8 时实机 slot73 从 87840 变 117120。</summary>
    [Fact]
    public void 主体角码壁厚扰动_与实机一致()
    {
        var x = ConnectEngine.DefaultInputs() with { MainBracketThickness = 8 };
        Same("角码壁抗承压", ConnectEngine.Compute(x).MainBracketBearing, 117120);
    }

    /// <summary>程序里的 π 就是 3.14：换成 Math.PI 会得到 3823.25，与实机 3821.544 不符。</summary>
    [Fact]
    public void 圆面积用程序字面量314()
    {
        Assert.Equal(3.14, ConnectEngine.ProgramPi, 12);
        Same("单栓承载力", ConnectEngine.BoltCapacity(5.061833, 190), 3821.544);
        // 反证：用 Math.PI 会偏离，说明不能"顺手用标准 π"
        double withRealPi = Math.PI * 5.061833 * 5.061833 * 190 / 4.0;
        Assert.NotEqual(3821.544.ToString("F3"), withRealPi.ToString("F3"));
    }
}
