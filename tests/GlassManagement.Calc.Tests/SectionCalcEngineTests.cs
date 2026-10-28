using System.Collections.Generic;
using GlassManagement.Calc.Engines.SectionCalc;
using Xunit;

namespace GlassManagement.Calc.Tests
{
    public class SectionCalcEngineTests
    {
        [Fact]
        public void 矩形截面_惯性矩_Green定理()
        {
            // 矩形 100×50（宽100 高50）：Area=5000, Ix=bh³/12=100*125000/12, Iy=hb³/12
            var outer = new List<PointD>
            {
                new() { X = 0, Y = 0 },
                new() { X = 100, Y = 0 },
                new() { X = 100, Y = 50 },
                new() { X = 0, Y = 50 },
            };
            var r = new SectionCalculationService().CalculateSection(outer, null);
            Assert.Equal(5000, r.Area);
            Assert.Equal(100.0 * 50 * 50 * 50 / 12, r.Ix, 3);  // bh³/12
            Assert.Equal(50.0 * 100 * 100 * 100 / 12, r.Iy, 3); // hb³/12
            Assert.Equal(50, r.Cx, 3);
            Assert.Equal(25, r.Cy, 3);
        }

        [Fact]
        public void 矩形截面_带内孔_净面积()
        {
            var outer = new List<PointD>
            {
                new() { X = 0, Y = 0 }, new() { X = 100, Y = 0 },
                new() { X = 100, Y = 50 }, new() { X = 0, Y = 50 },
            };
            var hole = new List<PointD>
            {
                new() { X = 40, Y = 20 }, new() { X = 60, Y = 20 },
                new() { X = 60, Y = 30 }, new() { X = 40, Y = 30 },
            };
            var r = new SectionCalculationService().CalculateSection(outer, new List<List<PointD>> { hole });
            Assert.Equal(5000 - 200, r.Area, 3); // 5000 - 20×10
        }
    }
}
