using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests
{
    public class WeightedAverageEngineTests
    {
        [Fact]
        public void 加权平均_两行_逐位()
        {
            var rows = new[]
            {
                new WeightedAverageEngine.Row(100, 2.0, 200),
                new WeightedAverageEngine.Row(300, 4.0, 1200),
            };
            var r = WeightedAverageEngine.Compute(rows);
            Assert.Equal(400, r.SumWeight);      // ΣA
            Assert.Equal(3.5, r.WeightedA);      // (100*2+300*4)/400 = 3.5
            Assert.Equal(950, r.WeightedB);      // (100*200+300*1200)/400 = 950
        }

        [Fact]
        public void 加权平均_三行_4除3_round3()
        {
            var rows = new[]
            {
                new WeightedAverageEngine.Row(1, 1, 1),
                new WeightedAverageEngine.Row(1, 1, 1),
                new WeightedAverageEngine.Row(1, 2, 2),
            };
            var r = WeightedAverageEngine.Compute(rows);
            Assert.Equal(3, r.SumWeight);
            Assert.Equal(1.333, r.WeightedA);    // 4/3 round3
            Assert.Equal(1.333, r.WeightedB);
        }

        [Fact]
        public void 加权平均_空输入_零输出()
        {
            var r = WeightedAverageEngine.Compute(System.Array.Empty<WeightedAverageEngine.Row>());
            Assert.Equal(0, r.SumWeight);
            Assert.Equal(0, r.WeightedA);
            Assert.Equal(0, r.WeightedB);
        }
    }
}
