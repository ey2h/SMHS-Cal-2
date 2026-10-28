using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests
{
    public class ExternalShadingEngineTests
    {
        [Fact]
        public void 外遮阳_水平遮阳板_默认800x1800_逐位()
        {
            var r = ExternalShadingEngine.Compute(ExternalShadingEngine.ShadingType.水平遮阳板, 800, 1800);
            Assert.Equal(0.4444, r.PF);   // 800/1800 = 0.4444
            Assert.Equal(0.7314, r.SD);   // 0.35*0.4444² + (-0.76)*0.4444 + 1
        }

        [Fact]
        public void 外遮阳_垂直遮阳板_默认800x1800_逐位()
        {
            var r = ExternalShadingEngine.Compute(ExternalShadingEngine.ShadingType.垂直遮阳板, 800, 1800);
            Assert.Equal(0.4444, r.PF);
            Assert.Equal(0.7832, r.SD);   // 0.32*0.4444² + (-0.63)*0.4444 + 1
        }

        [Fact]
        public void 外遮阳_PF超1截断为1()
        {
            var r = ExternalShadingEngine.Compute(ExternalShadingEngine.ShadingType.水平遮阳板, 2000, 1800);
            Assert.Equal(1.0, r.PF);       // 2000/1800 = 1.1111 > 1 → 取 1
            Assert.Equal(0.59, r.SD);      // 0.35*1 + (-0.76)*1 + 1 = 0.59
        }

        [Fact]
        public void 外遮阳_输出正文_含公式与系数()
        {
            var s = ExternalShadingEngine.FormatOutput(ExternalShadingEngine.ShadingType.水平遮阳板, 800, 1800);
            Assert.Contains("SDh=ahPF2+bhPF+1", s);
            Assert.Contains("=0.35*0.44442+(-0.76)*0.4444+1", s);
            Assert.Contains("=0.7314", s);
            Assert.Contains("GB50189-2005", s);
        }
    }
}
