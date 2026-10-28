using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests
{
    public class LightningEngineTests
    {
        [Fact]
        public void 等效面积_百M50x50_GB50057()
        {
            // L=100, W=50, H=50：D=√(50×150)=86.6025，Ae=0.0545 km²（百度百科例）
            double d = LightningEngine.ExpandWidth(50);
            Assert.Equal(86.603, d, 3);   // √(50×150)=86.6025
            double ae = LightningEngine.EquivalentArea(100, 50, 50);
            Assert.Equal(0.0545, ae, 4);
        }

        [Fact]
        public void 校正系数_四档()
        {
            Assert.Equal(1.0, LightningEngine.KValue(LightningEngine.KFactor.一般情况));
            Assert.Equal(1.5, LightningEngine.KValue(LightningEngine.KFactor.河边湖边等));
            Assert.Equal(1.7, LightningEngine.KValue(LightningEngine.KFactor.金属屋面未接地砖木));
            Assert.Equal(2.0, LightningEngine.KValue(LightningEngine.KFactor.山顶旷野孤立));
        }

        [Fact]
        public void 雷击次数_北京Td36_公式链()
        {
            // L=100,W=50,H=50,Td=36.7,k=1 → Ng=3.67, Ae=0.0545, N=0.2002
            double n = LightningEngine.Compute(100, 50, 50, 36.7, LightningEngine.KFactor.一般情况);
            Assert.Equal(3.67, LightningEngine.Ng(36.7), 2);
            Assert.Equal(0.2002, n, 4);
        }

        [Fact]
        public void 雷击次数_V31实机对拍_北京0_272()
        {
            // ★ 四面对拍：V31 实机运行期实值（2026-10-28 12:38 取证）
            //   窗体「建筑预计雷击次数计算」默认输入 L=100 W=80 H=90
            //   城市=北京市（Td=36.3，取自窗体城市列表框条目文本 "北京市,36.3"）
            //   选项=「在一般情况下」(k=1) + 「当建筑物的高度小于100m时」
            //   V31 输出原文：建筑物年预计雷击次数:0.272次/a
            //   本用例复算：Ng=0.1×36.3=3.63；Ae=0.0749213 km²；N=0.271964 → 0.272 ✅
            //   判别意义：若用 94 版 Ng=0.024·Td^1.3=2.559，则 N=0.1917 → 0.192，与 V31 不符
            double ng = LightningEngine.Ng(36.3);
            Assert.Equal(3.63, ng, 4);

            double ae = LightningEngine.EquivalentArea(100, 80, 90);
            Assert.Equal(0.0749213, ae, 7);

            double n = LightningEngine.Compute(100, 80, 90, 36.3, LightningEngine.KFactor.一般情况);
            Assert.Equal(0.272, n, 3);          // 与 V31 显示值 0.272 逐位一致

            // 94 版系数会得到 0.192，明确排除
            double n94 = 0.024 * System.Math.Pow(36.3, 1.3) * ae;
            Assert.Equal(0.192, n94, 3);
        }

        // ─────────────────────────────────────────────────────────────
        // T7 周边「等效面积要求」逐档对拍（V31 实机）
        // 证据：t7_surround_options.json（H90_full 组）+ t7_groupB2.json（H=150 组）
        // ★ 语义：D 公式跟选项组走（低组=√(H(200−H))，高组=H），不跟实际 H 走
        // ─────────────────────────────────────────────────────────────
        [Theory]
        // 低组（H=90 实测）
        [InlineData(LightningTestData.Surround低_单座, 90, 0.272)]
        [InlineData(LightningTestData.Surround低_周边2X等低, 90, 0.245)]
        [InlineData(LightningTestData.Surround低_四周2X等低, 90, 0.122)]
        [InlineData(LightningTestData.Surround低_周边2X更高, 90, 0.218)]
        [InlineData(LightningTestData.Surround低_四周2X更高, 90, 0.029)]
        // 高组（H=150 实测，D=H）
        [InlineData(LightningTestData.Surround高_单座, 150, 0.482)]
        [InlineData(LightningTestData.Surround高_周边2X等低, 150, 0.441)]
        [InlineData(LightningTestData.Surround高_四周2X等低, 150, 0.191)]
        [InlineData(LightningTestData.Surround高_周边2X更高, 150, 0.4)]
        [InlineData(LightningTestData.Surround高_四周2X更高, 150, 0.029)]
        public void 雷击次数_V31实机对拍_周边修正10档(int mode, double h, double expected)
        {
            var s = (LightningEngine.Surround)mode;
            double n = LightningEngine.Compute(100, 80, h, 36.3,
                LightningEngine.KFactor.一般情况, s);
            Assert.Equal(expected, n, 3);
        }

        /// <summary>
        /// 判定实验：H=150 选「小于100m」组（低_单座）→ 程序不校验高度，
        /// 机械套 D=√(150×50)=86.6025 → N=0.228（V31 实机一致，证实写入生效 + D 跟选项组走）。
        /// </summary>
        [Fact]
        public void 雷击次数_V31实机对拍_H150低组判定()
        {
            double n = LightningEngine.Compute(100, 80, 150, 36.3,
                LightningEngine.KFactor.一般情况, LightningEngine.Surround.低_单座);
            Assert.Equal(0.228, n, 3);
        }
    }

    /// <summary>InlineData 需要编译期常量：Surround 枚举值的 int 形态。</summary>
    public static class LightningTestData
    {
        public const int Surround低_单座 = 0;
        public const int Surround低_周边2X等低 = 1;
        public const int Surround低_四周2X等低 = 2;
        public const int Surround低_周边2X更高 = 3;
        public const int Surround低_四周2X更高 = 4;
        public const int Surround高_单座 = 5;
        public const int Surround高_周边2X等低 = 6;
        public const int Surround高_四周2X等低 = 7;
        public const int Surround高_周边2X更高 = 8;
        public const int Surround高_四周2X更高 = 9;
    }
}
