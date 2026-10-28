using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 复合玻璃（中空 + 单侧夹胶，3 片）—— V31 玻璃屏第四段函数区 0xA46000–0xA49B00。
///
/// 实机工况：1100×1380、三片均 6mm、wk=0.00137、αmax=0.16、PVB
/// 屏上：外片 10.345MPa（夹胶两片同值）、室内单片 9.578MPa、df 3.469mm、lim 18.333mm。
/// </summary>
public class CompositeGlassTests
{
    private const double Wk = 0.00137;
    private const double A = 1100.0;
    private const double B = 1380.0;
    private const double Alpha = 0.16;

    private static CompositeGlassResult Run() =>
        GlassEngine.ComputeComposite(Wk, A, B, new[] { 6.0, 6.0, 6.0 }, InterlayerKind.Pvb, Alpha);

    [Fact]
    public void 第一级_总刚度是全部玻璃片之和()
    {
        var r = Run();
        Assert.Equal(648.0, r.TotalStiffness);        // 3×6³ = 648（0xA4608D）
        // 「先分侧再分片」与「一次按 t³/S 分」等价：侧份额 432/648 × 片份额 216/432 = 216/648
        double sideShare = 432.0 / 648.0;
        double plyShare = 216.0 / 432.0;
        Assert.Equal(216.0 / 648.0, sideShare * plyShare, 15);
    }

    [Fact]
    public void 第二级_只有最外片乘1点1()
    {
        var r = Run();
        // 外片：round6(1.1×0.00137×216/648) = 0.000502
        Assert.Equal(0.000502, r.Plies[0].WkShare);
        // 夹胶内侧片：不乘 1.1，但同厚 → 也应是 0.000457
        Assert.Equal(0.000457, r.Plies[1].WkShare);
        // 室内单片：0.000457
        Assert.Equal(0.000457, r.Plies[2].WkShare);
    }

    [Fact]
    public void 第三级_逐片组合与强度()
    {
        var r = Run();
        Assert.Equal(0.000564, r.Plies[0].WDeflection);   // 0.000502+0.5×0.000123（0xA47B1A）
        Assert.Equal(0.000839, r.Plies[0].WStrength);     // 1.5×0.000502+0.7×0.000123（0xA47E96）
        Assert.Equal(8.849, r.Plies[0].Theta);            // 0xA47B83
        Assert.Equal(0.969, r.Plies[0].Eta);
        Assert.Equal(10.345, r.Plies[0].Sigma);           // 屏：外片玻璃强度计算值（0xA47F3D）

        Assert.Equal(0.000518, r.Plies[2].WDeflection);   // 0xA475B3
        Assert.Equal(0.000772, r.Plies[2].WStrength);     // 0xA476FF
        Assert.Equal(8.128, r.Plies[2].Theta);            // 0xA48487
        Assert.Equal(0.975, r.Plies[2].Eta);
        Assert.Equal(9.578, r.Plies[2].Sigma);            // 屏：室内单片（0xA487A8）
    }

    [Fact]
    public void 逐级衔接_外片份额大于夹胶内侧片_因乘1点1()
    {
        var r = Run();
        Assert.True(r.Plies[0].WkShare > r.Plies[1].WkShare);
        Assert.Equal(r.Plies[1].WkShare, r.Plies[2].WkShare);   // 同厚同系数 → 同份额
        // 三片份额之和 ≈ 1.1×wk + wk + wk = 3.1×wk? 不是 —— 分摊后应等于总荷载
        double sum = r.Plies[0].WkShare + r.Plies[1].WkShare + r.Plies[2].WkShare;
        Assert.Equal(0.001416, sum, 6);                          // 0.000502+0.000457×2
    }

    [Fact]
    public void 挠度_等效厚度含0点95_屏上数值逐位()
    {
        var r = Run();
        // 0.95×(648)^(1/3) = 0.95×8.653497 = 8.220822 → round3 = 8.221（0xA49449）
        Assert.Equal(8.221, r.EquivalentThickness);
        Assert.Equal(6.099, r.ThetaDefl);                 // 0xA4976A
        Assert.Equal(0.991, r.EtaDefl);                   // 0.991208 → 0.991
        Assert.Equal(3_472_593.612, r.Stiffness, 3);      // 72000×8.221³/11.52（0xA495B1）
        Assert.Equal(3.469, r.Deflection);                // 屏：玻璃挠度计算值 3.469mm（0xA49A3B）
        Assert.Equal(18.333, r.Limit);
    }

    [Fact]
    public void SGP未闭合时显式报错_不臆造公式()
    {
        var ex = Assert.Throws<NotSupportedException>(() =>
            GlassEngine.ComputeComposite(Wk, A, B, new[] { 6.0, 6.0, 6.0 }, InterlayerKind.Sgp, Alpha));
        Assert.Contains("0xA52", ex.Message);
    }

    [Fact]
    public void 胶片类型判定依据_来自本屏下拉而非第一页()
    {
        // PVB=0 / SGP=1，对应 V31 玻璃屏「夹层配置」ctrl id=5 的项序
        Assert.Equal(0, (int)InterlayerKind.Pvb);
        Assert.Equal(1, (int)InterlayerKind.Sgp);
    }
}
