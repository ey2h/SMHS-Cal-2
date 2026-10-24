using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 玻璃面板引擎：薄膜效应折减系数 η 与四边支承应力/挠度（s1 计算书锁死）。
/// 计算书 表6-2.3/6-2.4/6-3/6-5 原值：q=15 kPa, θ=333.33, η=0.05477, σ=5.58 MPa, df=2.26 mm。
/// </summary>
public class GlassPanelEngineTests
{
    private readonly GlassPanelEngine _panel = new();
    private const double A = 1200.0, B = 2400.0, T = 6.0, E = 72000.0, Nu = 0.20;
    private const double Q = 0.015; // 15 kPa = 0.015 MPa

    [Fact]
    public void MembraneEta_S1_Equals_Reference()
    {
        double theta = Q * Math.Pow(A, 4) / (E * Math.Pow(T, 4));
        Assert.Equal(333.3333, theta, 3);
        double eta = GlassPanelEngine.MembraneEta(Q, A, E, T);
        Assert.Equal(0.05477, eta, 5);
    }

    [Fact]
    public void FourEdge_Stress_S1_Matches_Calcbook()
    {
        var s = _panel.ComputeStress(GlassPanelEngine.SupportType.FourEdge, GlassPanelEngine.GlassType.Monolithic, A, B, T, Q, E);
        // 计算书 表6-5 σ=5.58 MPa
        Assert.Equal(5.58, s.SigmaMPa, 2);
        // η 自动按 θ 计算并应用
        Assert.Equal(0.05477, s.Eta, 5);
    }

    [Fact]
    public void FourEdge_Deflection_S1_Matches_Calcbook()
    {
        var d = _panel.ComputeDeflection(GlassPanelEngine.SupportType.FourEdge, A, B, T, Q, E, Nu);
        // 计算书 表6-5 df=2.26 mm（用 D=E·t³/[12(1-ν²)] 且乘 η）
        Assert.Equal(2.26, d.DfMm, 2);
        Assert.Equal(0.05477, d.Eta, 5);
    }

    [Fact]
    public void BendingStiffnessD_Formula()
    {
        double d = GlassPanelEngine.BendingStiffnessD(E, T, Nu);
        double exp = E * Math.Pow(T, 3) / (12.0 * (1.0 - Nu * Nu));
        Assert.Equal(exp, d, 6);
    }

    [Fact]
    public void MembraneEta_ThetaLe1_IsOne()
    {
        // 小荷载/厚玻璃 → θ≤1 → η=1
        double q = 0.001, a = 600, e = 72000, t = 10;
        double theta = q * Math.Pow(a, 4) / (e * Math.Pow(t, 4));
        Assert.True(theta <= 1.0, $"θ={theta} 应≤1");
        Assert.Equal(1.0, GlassPanelEngine.MembraneEta(q, a, e, t));
    }
}
