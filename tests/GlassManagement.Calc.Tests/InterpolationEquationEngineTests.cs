using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests
{
    public class InterpolationEquationEngineTests
    {
        [Fact]
        public void 插值_线性_probe直读()
        {
            // x1=125,y1=0.1235,x2=280,y2=0.1358,x=200 → 0.12945
            Assert.Equal(0.12945, InterpolationEngine.Linear(200, 125, 0.1235, 280, 0.1358));
        }

        [Fact]
        public void 一元二次_求根公式_probe直读()
        {
            // 5x²+2x-15=0 → 1.54356 / -1.94356
            var r = EquationEngine.SolveQuadratic(5, 2, -15);
            Assert.True(r.HasRealRoots);
            Assert.Equal(1.54356, r.X1);
            Assert.Equal(-1.94356, r.X2);
        }

        [Fact]
        public void 一元二次_无实根()
        {
            var r = EquationEngine.SolveQuadratic(1, 0, 1); // x²+1=0
            Assert.False(r.HasRealRoots);
        }

        [Fact]
        public void 一元三次_单实根_probe直读()
        {
            // 500x³+45x²+25x+15=0 → -0.284632890979535
            var roots = EquationEngine.SolveCubic(500, 45, 25, 15);
            Assert.Single(roots);
            Assert.Equal(-0.284632890979535, roots[0], 12);
        }

        [Fact]
        public void 一元三次_三实根()
        {
            // x³-6x²+11x-6=0 → 1,2,3
            var roots = EquationEngine.SolveCubic(1, -6, 11, -6);
            Assert.Equal(3, roots.Length);
            Assert.Equal(1.0, roots[0], 6);
            Assert.Equal(2.0, roots[1], 6);
            Assert.Equal(3.0, roots[2], 6);
        }
    }
}
