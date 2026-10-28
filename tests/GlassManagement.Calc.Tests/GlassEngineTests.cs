using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 玻璃面板（四边简支）——「幕墙计算-玻璃计算」屏。
///
/// 期望值全部来自 V31 实机：屏幕 RichText 原文
/// <c>local-data/recon/v31runs/py_玻璃.txt</c>（1.37 / 24.793 / 84 / 8.23 / 18.333）
/// + Frida 抓的运算 DAG 中间量（<c>v31runs/glass_ops.json</c>）
/// + 槽数组 0x1C911BC 直读（slot[7]=0.000154 slot[9]=0.000123 slot[11]=0.002141 slot[12]=0.001431
///   slot[14]=22.453 slot[15]=0.91 slot[16]=0.0631 slot[17]=24.793 slot[22]=0.00606
///   slot[23]=18.333 slot[24]=1350000 slot[25]=8.23 slot[30]=0.914）。
/// </summary>
public class GlassEngineTests
{
    // 实机工况：分格 1100×1380（第一页龙骨间距）、6mm 钢化单片、北京 αmax=0.16、wk=1.37kPa
    private const double Wk = 0.00137;
    private const double A = 1100.0;
    private const double B = 1380.0;
    private const double T = 6.0;
    private const double Alpha = 0.16;

    private static GlassResult Run() => GlassEngine.Compute(Wk, A, B, T, Alpha);

    [Fact]
    public void 荷载组合_四步取整逐位()
    {
        var r = Run();
        Assert.Equal(0.000154, r.SelfWeight);     // 6×2.56e-5 = 0.0001536 → round6（0xA20BDB）
        Assert.Equal(0.000123, r.Quake);          // 0.000154×0.8 = 0.0001232 → round6（0xA20D42）
        Assert.Equal(0.002141, r.WStrength);      // 1.5×0.00137+0.7×0.000123（0xA20FC7）
        Assert.Equal(0.001431, r.WDeflection);    // 0.00137+0.5×0.000123（0xA210CD）
    }

    [Fact]
    public void theta_两条链不同源_强度用标准组合_挠度用纯wk()
    {
        var r = Run();
        Assert.Equal(22.453, r.ThetaSigma);       // 0.001431×1100⁴/(72000×6⁴)（0xA21359）
        Assert.Equal(21.496, r.ThetaDefl);        // 0.00137 ×1100⁴/(72000×6⁴)（0xA21A95）
    }

    [Fact]
    public void 折减系数_分段线性插值_结果取3位()
    {
        var r = Run();
        // η 表断点 5/10/20/40/60/80/100/120/150/200/250/300/350/400
        Assert.Equal(0.910, GlassEngine.Eta(22.452922453703703));   // 0.92−(2.4529/20)×0.08
        Assert.Equal(0.914, GlassEngine.Eta(21.495809756515772));   // 0.92−(1.4958/20)×0.08
        Assert.Equal(0.910, r.EtaSigma);
        Assert.Equal(0.914, r.EtaDefl);

        // 边界：≤5 → 1.000；≥400 → 0.500
        Assert.Equal(1.000, GlassEngine.Eta(3.0));
        Assert.Equal(0.500, GlassEngine.Eta(500.0));
    }

    [Fact]
    public void 弯矩与挠度系数_按短长边比插值()
    {
        var r = Run();
        double ratio = A / B;                       // 0.7971014492753623

        // 插值规则：m 段 0.75→0.0683、0.80→0.0628；μ 段 0.75→0.00663、0.80→0.00603
        double mRaw = 0.0683 - (0.0683 - 0.0628) * (ratio - 0.75) / 0.05;
        double muRaw = 0.00663 - (0.00663 - 0.00603) * (ratio - 0.75) / 0.05;
        Assert.Equal(0.06311884057971015, mRaw, 14);
        Assert.Equal(0.006064782608695652, muRaw, 14);

        // 引擎返回的是 V31 真正参与乘法的值（m 取 4 位、μ 取 5 位）
        Assert.Equal(0.0631, r.BendingCoef, 12);
        Assert.Equal(0.00606, r.DeflCoef, 14);
        Assert.InRange(ratio, 0.7971, 0.7972);
    }

    [Fact]
    public void 刚度D_E乘t三次方除11点52()
    {
        var r = Run();
        Assert.Equal(1_350_000.0, r.Stiffness);     // 72000×216/11.52（0xA21D80）
    }

    [Fact]
    public void 强度与挠度_屏上数值逐位()
    {
        var r = Run();
        Assert.Equal(24.793, r.Sigma);             // 屏：玻璃强度计算值 24.793MPa（0xA216F8）
        Assert.Equal(8.23, r.Deflection);          // 屏：玻璃挠度计算值 8.23mm（0xA21F6C）
        Assert.Equal(18.333, r.Limit);             // 屏：挠度容许值 18.333mm = 1100/60（0xA21C38）
        Assert.True(r.Deflection <= r.Limit);
    }

    [Fact]
    public void 短边取min_长边取max_顺序无关()
    {
        var a = GlassEngine.Compute(Wk, 1100, 1380, T, Alpha);
        var b = GlassEngine.Compute(Wk, 1380, 1100, T, Alpha);
        Assert.Equal(a.Sigma, b.Sigma);
        Assert.Equal(a.Deflection, b.Deflection);
        Assert.Equal(a.BendingCoef, b.BendingCoef);
    }

    [Fact]
    public void 厚度增大_应力与挠度同时下降()
    {
        var t6 = GlassEngine.Compute(Wk, A, B, 6, Alpha);
        var t8 = GlassEngine.Compute(Wk, A, B, 8, Alpha);
        Assert.True(t8.Sigma < t6.Sigma);
        Assert.True(t8.Deflection < t6.Deflection);
        Assert.Equal(6.0 * t6.SelfWeight, t6.SelfWeight * 6, 9);   // 占位：自重可读
    }
}
