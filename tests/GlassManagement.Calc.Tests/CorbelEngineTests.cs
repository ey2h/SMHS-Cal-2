using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests
{
    /// <summary>
    /// 牛腿结构计算（P14）。公式由 V31 运行期 frida 抓 __vbaVar* 链逐条还原
    /// （recon/v31runs/b8_probe_corbel.json，73 条），φ 表 31 点运行期直读。
    /// </summary>
    public class CorbelEngineTests
    {
        private static CorbelEngine.Input Default() => new();

        [Fact]
        public void 牛腿_默认算例_五项逐位()
        {
            var it = Default();
            var o = CorbelEngine.Compute(it);

            Assert.Equal(400.0, o.B, 9);                 // L−H
            Assert.Equal(0.866158, o.Cos, 6);            // cos(30°)，π=3.14
            Assert.Equal(0.499770, o.Sin, 6);
            Assert.Equal(4008.234, o.ThrustR, 3);        // 水平推力 R
            Assert.Equal(586825.6, o.MomentA, 1);        // M_A = P·L − R·H
            Assert.Equal(9626.387, o.Ndiag, 3);          // 斜梁轴力
            Assert.Equal(15.277, o.Gyration, 3);         // i=√(Ix/A)
            Assert.Equal(1847.238, o.DiagLength, 3);     // H/cosθ
            Assert.Equal(120.916, o.Slenderness, 3);     // λ
            Assert.Equal(0.435, o.Phi, 3);               // φ（查表）
        }

        [Fact]
        public void 牛腿_默认算例_输出五项()
        {
            var it = Default();
            var o = CorbelEngine.Compute(it);
            Assert.Equal(200.080, o.StressA, 3);
            Assert.Equal(430.149, o.StressC, 3);
            Assert.Equal(46.075, o.StressDiag, 3);
            Assert.Equal(10.878, o.Deflection, 3);
            Assert.Equal(16.0, o.DeflectionLimit, 3);
        }

        [Fact]
        public void 牛腿_输出正文_逐字()
        {
            var it = Default();
            var s = CorbelEngine.FormatOutput(it, CorbelEngine.Compute(it));
            Assert.Contains("主梁A点强度计算值:200.08MPa", s);
            Assert.Contains("主梁C点强度计算值:430.149MPa", s);
            Assert.Contains("A点强度不满足要求(×)", s);   // 原文笔误（C 点却写 A 点）
            Assert.Contains("斜梁强度计算值:46.075MPa", s);
            Assert.Contains("斜梁长细比计算值:120.916", s);
            Assert.Contains("主梁挠度计算值:10.878mm", s);
            Assert.Contains("主梁挠度设计值:16mm", s);
        }

        [Theory]
        [InlineData(1200, 0.616)]   // λ=90.687
        [InlineData(1600, 0.435)]   // λ=120.916
        [InlineData(1000, 0.717)]   // λ=75.573
        public void 牛腿_稳定系数查表(double h, double phi)
        {
            var it = Default();
            it.H = h;
            var o = CorbelEngine.Compute(it);
            Assert.Equal(phi, o.Phi, 3);
        }

        [Fact]
        public void 牛腿_扰动H_输出随动()
        {
            // H=1200 实测：主梁A 31.09 / 主梁C 856.134 / 斜梁 47.314
            var it = Default();
            it.H = 1200;
            var o = CorbelEngine.Compute(it);
            Assert.Equal(31.09, o.StressA, 2);
            Assert.Equal(856.134, o.StressC, 3);
            Assert.Equal(47.314, o.StressDiag, 3);
        }

        [Fact]
        public void 牛腿_Pi是314不是MathPI()
        {
            Assert.Equal(3.14, CorbelEngine.Pi, 9);
            // 用 3.14 才能得 0.866158（Math.PI 会得 0.866025）
            Assert.Equal(0.866158, System.Math.Cos(30 * 3.14 / 180.0), 6);
        }
    }
}
