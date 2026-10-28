using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests
{
    /// <summary>
    /// 多跨静定梁解析公式（P16：替换原 3 个实测拟合系数 1.0743/1.1463/1.0364
    /// 与 5 个双样本比例插值）。数据源：recon/verify_multispan.py 4 组 V31 实机样本，
    /// 型材 I=885950mm⁴ W=21452mm³ S=13606mm³ t=6mm BAY=1100mm E=70000MPa。
    /// </summary>
    public class MultiSpanAnalyticalTests
    {
        private const double I = 885950.0, W = 21452.0, S = 13606.0, TW = 6.0, BAY = 1100.0;

        // (A, L, wk, σ1, τ1, σ2, τ2, σ3, τ3, eq2, eq3, df1, df2) —— V31 预演输出逐字
        [Theory]
        [InlineData(600, 2700, 1.278, 80.958, 6.928, 93.397, 10.526, 52.972, 1.198, 93.988, 52.986, 18.1775, 7.404)]
        [InlineData(600, 3000, 1.273, 101.495, 7.740, 102.200, 11.288, 69.017, 1.239, 102.821, 69.028, 25.8395, 10.988)]
        [InlineData(600, 2400, 1.285, 62.567, 6.107, 84.535, 9.772, 39.105, 1.144, 85.098, 39.122, 12.5275, 4.996)]
        [InlineData(800, 2700, 1.274, 74.300, 6.626, 127.890, 11.568, 43.316, 1.382, 128.412, 43.338, 23.692, 9.7355)]
        public void 多跨解析公式_四组V31样本逐位(
            double a, double l, double wk,
            double s1, double t1, double s2, double t2, double s3, double t3,
            double eq2, double eq3, double df1, double df2)
        {
            var (qk, qd) = MultiSpanStaticEngine.LineLoads(wk, BAY);
            var f = MultiSpanStaticEngine.InternalForces(qd, a, l);

            Assert.Equal(s1, f.Span1Moment / W, 3);
            Assert.Equal(t1, S * f.Span1Shear / (I * TW), 3);
            Assert.Equal(s2, f.SupportMoment / W, 3);
            Assert.Equal(t2, S * f.SupportShear / (I * TW), 3);
            Assert.Equal(s3, f.Span3Moment / W, 3);
            Assert.Equal(t3, S * f.Span3Shear / (I * TW), 3);

            double b2 = WindLoadEngine.VbaRound(f.SupportMoment / W, 3);
            double v2 = WindLoadEngine.VbaRound(S * f.SupportShear / (I * TW), 3);
            double b3 = WindLoadEngine.VbaRound(f.Span3Moment / W, 3);
            double v3 = WindLoadEngine.VbaRound(S * f.Span3Shear / (I * TW), 3);
            Assert.Equal(eq2, WindLoadEngine.VbaRound(System.Math.Sqrt(b2 * b2 + v2 * v2), 3), 3);
            Assert.Equal(eq3, WindLoadEngine.VbaRound(System.Math.Sqrt(b3 * b3 + v3 * v3), 3), 3);

            var (d1, d2) = MultiSpanStaticEngine.Deflections(qk, qd, a, l, 70000.0, I);
            Assert.Equal(df1, d1, 4);
            Assert.Equal(df2, d2, 4);
        }

        [Fact]
        public void 有效跨_等于L减A平方除L()
        {
            Assert.Equal(3750.0, MultiSpanStaticEngine.EffectiveSpan(1000, 4000), 9);
            Assert.Equal(2250.0, MultiSpanStaticEngine.EffectiveSpan(600, 2400), 9);
        }

        [Fact]
        public void 线荷载_先round3再乘15再round3()
        {
            // wk=1.526, B=1350 → qk=2.06, qd=3.09
            var (qk, qd) = MultiSpanStaticEngine.LineLoads(1.526, 1350);
            Assert.Equal(2.06, qk, 9);
            Assert.Equal(3.09, qd, 9);
        }

        [Fact]
        public void 拟合系数已删除()
        {
            // P16：3 个实测拟合系数与 5 个比例函数必须不存在（编译期已删）
            var t = typeof(MultiSpanStaticEngine);
            Assert.Null(t.GetField("MeasuredFitMoment"));
            Assert.Null(t.GetField("MeasuredFitShear"));
            Assert.Null(t.GetField("MeasuredFitDeflection"));
            Assert.Null(t.GetMethod("SupportBendingRatio"));
            Assert.Null(t.GetMethod("Span3BendingRatio"));
            Assert.Null(t.GetMethod("SupportShearRatio"));
            Assert.Null(t.GetMethod("Span3ShearRatio"));
            Assert.Null(t.GetMethod("SecondSpanDeflectionRatio"));
        }
    }
}
