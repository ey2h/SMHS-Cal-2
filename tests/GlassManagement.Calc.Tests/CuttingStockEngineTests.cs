using System.Collections.Generic;
using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests
{
    /// <summary>
    /// 一维套裁（P15）。⚠ V31 的算法一/二 是私有启发式，本引擎用行业标准 FFD，
    /// 输出格式一致但方案行不保证逐行相同（已在 Note 中登记）。
    /// </summary>
    public class CuttingStockEngineTests
    {
        [Fact]
        public void 套裁_单一零件_整除()
        {
            var parts = new[] { new CuttingStockEngine.Part(1000, 6) };
            var r = CuttingStockEngine.Optimize(parts, 6000, kerf: 0, endTrim: 0);
            Assert.Equal(1, r.StockCount);
            Assert.Single(r.Patterns);
            Assert.Equal(6, r.Patterns[0].Pieces.Count);
            Assert.Equal(0, r.Patterns[0].Waste, 9);
            Assert.Equal(1.0, r.OverallUtilization, 9);
        }

        [Fact]
        public void 套裁_不能整除_开新料()
        {
            // 6000 原料切 2500×3 → 每根切 2 个（5000），共 2 根（3 个）
            var parts = new[] { new CuttingStockEngine.Part(2500, 3) };
            var r = CuttingStockEngine.Optimize(parts, 6000, kerf: 0, endTrim: 0);
            Assert.Equal(2, r.StockCount);
            Assert.Equal(3, r.Patterns.Sum(p => p.Pieces.Count * p.Count));
        }

        [Fact]
        public void 套裁_混合规格_降序优先()
        {
            var parts = new[]
            {
                new CuttingStockEngine.Part(2050, 2),
                new CuttingStockEngine.Part(3560, 1),
                new CuttingStockEngine.Part(3650, 1),
            };
            var r = CuttingStockEngine.Optimize(parts, 6000, kerf: 0, endTrim: 0);
            Assert.True(r.StockCount >= 2);
            // 总零件长度守恒
            Assert.Equal(2050 * 2 + 3560 + 3650, r.TotalPieceLength, 6);
            // 每根都不超原料
            foreach (var p in r.Patterns)
                Assert.True(p.UsedLength <= 6000 + 1e-6);
        }

        [Fact]
        public void 套裁_超长零件被跳过并提示()
        {
            var parts = new[] { new CuttingStockEngine.Part(7000, 1) };
            var r = CuttingStockEngine.Optimize(parts, 6000, kerf: 5, endTrim: 50);
            Assert.Equal(0, r.StockCount);
            Assert.Contains("超长", r.Note);
        }

        [Fact]
        public void 套裁_锯缝与端头余量计入()
        {
            // 6000，端头 50，锯缝 5：1000×5 需 5000+5×4+50=5070 ≤6000，可放 5 个
            var parts = new[] { new CuttingStockEngine.Part(1000, 5) };
            var r = CuttingStockEngine.Optimize(parts, 6000, kerf: 5, endTrim: 50);
            Assert.Equal(1, r.StockCount);
            Assert.Equal(5, r.Patterns[0].Pieces.Count);
            Assert.Equal(6000 - 5070, r.Patterns[0].Waste, 6);
        }

        [Fact]
        public void 套裁_报表含汇总()
        {
            var parts = new[] { new CuttingStockEngine.Part(2000, 3) };
            var r = CuttingStockEngine.Optimize(parts, 6000, 0, 0);
            var s = CuttingStockEngine.ToReport(r, 6000);
            Assert.Contains("原材料长度：6000", s);
            Assert.Contains("综合利用率", s);
        }
    }
}
