using GlassManagement.Calc.Engines.StaticCalc;
using Xunit;

namespace GlassManagement.Calc.Tests
{
    public class StaticCalcEngineTests
    {
        [Fact]
        public void 简支梁_均布荷载_手册公式()
        {
            // 表2-1：Mmax=qL²/8, Vmax=qL/2（弯矩剪力不含 EI，直接按手册）
            var req = new BeamCalcRequest
            {
                Support = SupportCondition.SimpleBeam,
                Load = LoadType.Uniform,
                L = 3.0, Q = 10, E = 70000, I = 1250000,
            };
            var r = new StaticCalcService().Calculate(req);
            Assert.Equal(10 * 9 / 8.0, r.MaxMoment);      // 11.25
            Assert.Equal(10 * 3 / 2.0, r.MaxShear);        // 15
            Assert.Equal(2, r.Reactions.Count);
            Assert.Equal(15, r.Reactions[0].Value);
            // δmax = 5qL⁴/(384·E·I·1e-6)·1000（引擎约定：E MPa、I mm^4、EI×1e-6）
            double EI = 70000.0 * 1250000.0 * 1e-6;
            Assert.Equal(5 * 10 * 81 / (384 * EI) * 1000, r.MaxDeflection, 3);
        }

        [Fact]
        public void 悬臂梁_均布荷载_手册公式()
        {
            // 表3-2：固定端弯矩 -qL²/2（引擎记入 MinMoment，负弯矩），Vmax=qL
            var req = new BeamCalcRequest
            {
                Support = SupportCondition.Cantilever,
                Load = LoadType.Uniform,
                L = 3.0, Q = 10, E = 70000, I = 1250000,
            };
            var r = new StaticCalcService().Calculate(req);
            Assert.Equal(-10 * 9 / 2.0, r.MinMoment);     // -45
            Assert.Equal(10 * 3, r.MaxShear);              // 30
        }
    }
}
