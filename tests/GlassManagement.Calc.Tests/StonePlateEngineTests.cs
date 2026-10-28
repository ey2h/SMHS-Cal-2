using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 板材线「板块计算」屏（天然石材 · 短槽连接）——逐位对拍。
///
/// <para><b>期望值来源</b>：V31 实机 RichText 原文 <c>v31runs/book_stone_slot_4.txt</c>（基线）
/// ＋ 22 组参数扰动直读 <c>v31runs/stone_perturb.txt</c> ＋ Frida 抓的 78 条 Variant 实值轨迹
/// <c>v31runs/stone_plate_var.json</c>（23 个 rtcRound 点）＋ 常量池原文。
/// 算式与地址见 <c>docs/V31_STONE_PLATE_UI.md</c>。</para>
///
/// <para>基线槽值：1=100 2=900 3=800 5=0.16 6=0.00075 7=0.0006 8=0.00137 9=0.002475
/// 10=600 12=600 13=800 14=3.72 15=0.75 16=0.1408 17=25 18=2.141 21=1.86 22=1.25 23=2 24=6 25=80
/// 26=900 27=800 28=0.733。</para>
/// </summary>
public class StonePlateEngineTests
{
    private const double Wk = 0.00137;      // 面板口径（μsl=1.0，面板部分风荷载标准值 1.37 KPa）
    private const double AlphaMax = 0.16;   // 参数库「地区参数」北京 8 度

    private static StonePlateEngine.Input Base() => new()
    {
        LongSideMm = 900,
        ShortSideMm = 800,
        ThicknessMm = 25,
        EdgeDistanceMm = 150,
        SlotMouthMm = 6,
        SlotBottomLengthMm = 80,
        LugCount = 4,
        LugAllowableShearMpa = 100,
        HangerWidthMm = 80,
        HangerThicknessMm = 3,
        HangerEccentricityMm = 30,
        HangerLengthMm = 60,
        HangerMaterial = "316不锈钢",
        HorizontalHangerCount = 2,
        LoadHangerCount = 2,
        BoltPerHanger = 1,
        BoltMaterial = "碳钢：4.6-C级",
        BoltDiameterMm = 8,
        StoneDensityKnM3 = 30,
        AlphaMax = AlphaMax,
        Wk = Wk,
        StoneAllowableBendingMpa = 3.72,
        StoneAllowableShearMpa = 1.86,
    };

    /// <summary>一次比对屏上 9 组输出的全部数值。</summary>
    private static void AssertAll(StonePlateEngine.Result r,
        double calcShort, double calcLong, double m,
        double sigma, double tau, double lugShear,
        double hangerStress, double hangerDefl,
        double boltN, double boltV, double combined)
    {
        Assert.Equal(calcShort, r.CalcShortMm);
        Assert.Equal(calcLong, r.CalcLongMm);
        Assert.Equal(m, r.MCoef);
        Assert.Equal(sigma, r.StressComputed);
        Assert.Equal(tau, r.ShearComputed);
        Assert.Equal(lugShear, r.LugShearComputed);
        Assert.Equal(hangerStress, r.HangerStressComputed);
        Assert.Equal(hangerDefl, r.HangerDeflectionComputed);
        Assert.Equal(0.24, r.HangerDeflectionAllowable);   // e=30、316不锈钢 → round3(2×30/250)
        Assert.Equal(boltN, r.BoltTensionComputed);
        Assert.Equal(boltV, r.BoltShearComputed);
        Assert.Equal(combined, r.BoltCombined);
    }

    // ───────────────── ① 荷载链（0x1BE27C2–0x1BE2ADC） ─────────────────

    [Fact]
    public void 荷载_自重地震qd_基线逐位()
    {
        var r = StonePlateEngine.Compute(Base());
        Assert.Equal(1.37, r.WindStandardKpa);
        Assert.Equal(0.00075, r.SelfWeightMpa);   // 槽6 = γ·t/1e6 = 30×25/1e6，不取整
        Assert.Equal(0.0006, r.SeismicMpa);       // 槽7 = round6(5×0.16×0.00075)
        Assert.Equal(0.002475, r.Qd);             // 槽9 = round6(1.5×0.00137 + 0.7×0.0006)
    }

    [Fact]
    public void 荷载_板厚30_地震项取6位后才能对上qk()
    {
        var it = Base(); it.ThicknessMm = 30;
        var r = StonePlateEngine.Compute(it);
        Assert.Equal(0.0009, r.SelfWeightMpa);
        Assert.Equal(0.00072, r.SeismicMpa);
        Assert.Equal(0.002559, r.Qd);             // 实机 qd=0.002559
    }

    // ───────────────── ② 计算边长与 m 插值（0x1BE2C6B / 0x1C3473F） ─────────────────

    [Fact]
    public void 计算边长_短边取min长边取max_边距200()
    {
        var it = Base(); it.EdgeDistanceMm = 200;
        var r = StonePlateEngine.Compute(it);
        Assert.Equal(500, r.CalcShortRawMm);      // 900 − 2×200
        Assert.Equal(500, r.CalcShortMm);         // min(500, 800)
        Assert.Equal(800, r.CalcLongMm);          // max(500, 800)
        Assert.Equal(0.625, r.Ratio);
        Assert.Equal(0.1349, r.MCoef);            // 0.1338 + (0.136−0.1338)×0.5
        Assert.Equal(2.051, r.StressComputed);
        Assert.Equal(0.733, r.ShearComputed);     // 抗剪用全长边×短边，不随边距变
    }

    [Fact]
    public void m表_七点插值与钳位()
    {
        Assert.Equal(0.1303, StonePlateEngine.Compute(With(ratio: 0.5)).MCoef);   // 钳位
        Assert.Equal(0.1322, StonePlateEngine.Compute(With(ratio: 0.556)).MCoef);
        Assert.Equal(0.1349, StonePlateEngine.Compute(With(ratio: 0.625)).MCoef);
        Assert.Equal(0.1396, StonePlateEngine.Compute(With(ratio: 0.727)).MCoef);
        Assert.Equal(0.1408, StonePlateEngine.Compute(With(ratio: 0.75)).MCoef);
        Assert.Equal(0.1478, StonePlateEngine.Compute(With(ratio: 0.875)).MCoef);
        Assert.Equal(0.1487, StonePlateEngine.Compute(With(ratio: 0.889)).MCoef);
        Assert.Equal(0.1559, StonePlateEngine.Compute(With(ratio: 1.0)).MCoef);
    }

    /// <summary>构造一个 r 精确等于给定值的算例：短边 = 长边 × r（边距置 0）。</summary>
    private static StonePlateEngine.Input With(double ratio)
    {
        var it = Base();
        it.EdgeDistanceMm = 0;
        it.ShortSideMm = 1000.0 * ratio;
        it.LongSideMm = 1000.0;
        return it;
    }

    // ───────────────── ③ 九组输出：实机 14 组扰动逐位 ─────────────────

    [Fact]
    public void 基线_全部九组输出逐位()
    {
        var r = StonePlateEngine.Compute(Base());
        AssertAll(r, 600, 800, 0.1408,
            2.141, 0.733, 2.32,
            92.391, 0.131,
            351, 1113.75, 0.050428);
        Assert.Equal(1.25, r.LugCoef);
        Assert.Equal(2, r.LugPerSide);
        Assert.Equal(240, r.LugAreaMm2);
        Assert.Equal(270, r.LoadPerHanger);
        Assert.Equal(10530, r.HangerMomentNmm);
        Assert.Equal(1113.75, r.HangerShearN);
        Assert.Equal(240, r.HangerSectionMm3);
        Assert.Equal(120, r.HangerModulusMm3);
        Assert.Equal(180, r.HangerInertiaMm4);
        Assert.Equal(206000, r.HangerElasticModulusMpa);
        Assert.Equal(36.6, r.BoltAreaMm2);
        Assert.Equal(6222, r.BoltTensionAllowable);
        Assert.Equal(5124, r.BoltShearAllowable);
        Assert.True(r.StressOk && r.ShearOk && r.LugShearOk);
        Assert.True(r.HangerStressOk && r.HangerDeflectionOk);
        Assert.True(r.BoltTensionOk && r.BoltShearOk && r.BoltCombinedOk);
    }

    [Theory]
    // 长边扰动（短边 800）
    [InlineData(1200, 800, 25, 800, 900, 0.1487, 2.862, 0.977, 3.094, 123.188, 0.175, 468, 1485, 0.089649)]
    [InlineData(1000, 800, 25, 700, 800, 0.1478, 2.248, 0.814, 2.578, 102.656, 0.146, 390, 1237.5, 0.062256)]
    [InlineData(1400, 800, 25, 800, 1100, 0.1396, 4.013, 1.14, 3.609, 143.719, 0.204, 546, 1732.5, 0.122022)]
    // 短边扰动
    [InlineData(900, 600, 25, 600, 600, 0.1559, 1.334, 0.55, 1.74, 69.293, 0.098, 263.25, 835.313, 0.028366)]
    [InlineData(1000, 900, 25, 700, 900, 0.1423, 2.739, 0.916, 2.9, 115.488, 0.164, 438.75, 1392.188, 0.078793)]
    [InlineData(800, 900, 25, 500, 900, 0.1322, 2.544, 0.733, 2.32, 92.391, 0.131, 351, 1113.75, 0.050428)]
    public void 边长扰动_逐位(double a, double b, double t,
        double calcShort, double calcLong, double m,
        double sigma, double tau, double lug, double hangSigma, double hangDefl,
        double boltN, double boltV, double combined)
    {
        var it = Base();
        it.LongSideMm = a; it.ShortSideMm = b; it.ThicknessMm = t;
        var r = StonePlateEngine.Compute(it);
        AssertAll(r, calcShort, calcLong, m, sigma, tau, lug,
            hangSigma, hangDefl, boltN, boltV, combined);
    }

    [Theory]
    [InlineData(30, 1.537, 0.6, 2.399, 110.098, 0.157, 421.2, 1151.55, 0.055089)]
    [InlineData(20, 3.232, 0.961, 2.242, 74.683, 0.105, 280.8, 1075.95, 0.046129)]
    public void 板厚扰动_逐位(double t, double sigma, double tau, double lug,
        double hangSigma, double hangDefl, double boltN, double boltV, double combined)
    {
        var it = Base(); it.ThicknessMm = t;
        var r = StonePlateEngine.Compute(it);
        Assert.Equal(sigma, r.StressComputed);
        Assert.Equal(tau, r.ShearComputed);
        Assert.Equal(lug, r.LugShearComputed);
        Assert.Equal(hangSigma, r.HangerStressComputed);
        Assert.Equal(hangDefl, r.HangerDeflectionComputed);
        Assert.Equal(boltN, r.BoltTensionComputed);
        Assert.Equal(boltV, r.BoltShearComputed);
        Assert.Equal(combined, r.BoltCombined);
    }

    // ───────────────── ④ 抗剪两支：槽口宽 / 槽底长 / 托板数 ─────────────────

    [Fact]
    public void 槽口宽8_抗剪0点819_托板不变()
    {
        var it = Base(); it.SlotMouthMm = 8;
        var r = StonePlateEngine.Compute(it);
        Assert.Equal(0.819, r.ShearComputed);      // 2227.5/(2×17×80)
        Assert.Equal(2.32, r.LugShearComputed);    // 托板项不含槽口宽
    }

    [Fact]
    public void 槽底长100_抗剪0点586_托板不变()
    {
        var it = Base(); it.SlotBottomLengthMm = 100;
        var r = StonePlateEngine.Compute(it);
        Assert.Equal(0.586, r.ShearComputed);      // 2227.5/(2×19×100)
        Assert.Equal(2.32, r.LugShearComputed);
    }

    [Fact]
    public void 托板个数6_系数1点275_抗剪与托板同变()
    {
        var it = Base(); it.LugCount = 6;
        var r = StonePlateEngine.Compute(it);
        Assert.Equal(1.275, r.LugCoef);            // 常量池 0x4126F0
        Assert.Equal(3, r.LugPerSide);
        Assert.Equal(0.498, r.ShearComputed);      // 1.275×2227.5/1.25/(3×19×80)
        Assert.Equal(1.578, r.LugShearComputed);
        Assert.Equal(1113.75, r.BoltShearComputed); // 挂件剪力用的是常数 1.25，不随托板数变
    }

    [Fact]
    public void 托板个数8_系数1点3()
    {
        var it = Base(); it.LugCount = 8;
        var r = StonePlateEngine.Compute(it);
        Assert.Equal(1.3, r.LugCoef);              // 常量池 0x401D50
        Assert.Equal(0.381, r.ShearComputed);
        Assert.Equal(1.207, r.LugShearComputed);
    }

    [Theory]
    [InlineData(4, 1.25)]      // 0x401D48
    [InlineData(6, 1.275)]     // 0x4126F0
    [InlineData(8, 1.30)]      // 0x401D50
    [InlineData(10, 1.31)]     // 0x4126E8
    [InlineData(12, 1.32)]     // 0x4123C0
    public void 托板系数表_常量池原文(int count, double k)
        => Assert.Equal(k, StonePlateEngine.LugCoefOf(count));

    // ───────────────── ⑤ 挂件与螺栓 ─────────────────

    [Fact]
    public void 挂件长40_挠度0点087_螺栓抗拉1053()
    {
        var it = Base(); it.HangerLengthMm = 40;
        var r = StonePlateEngine.Compute(it);
        Assert.Equal(0.087, r.HangerDeflectionComputed);
        Assert.Equal(1053, r.BoltTensionComputed);   // 10530/(40−30)
        Assert.Equal(92.391, r.HangerStressComputed); // 弯矩不随 L 变
    }

    [Fact]
    public void 偏心40_挠度0点233_限值0点32_螺栓702()
    {
        var it = Base(); it.HangerEccentricityMm = 40;
        var r = StonePlateEngine.Compute(it);
        Assert.Equal(121.641, r.HangerStressComputed);
        Assert.Equal(0.233, r.HangerDeflectionComputed);
        Assert.Equal(0.32, r.HangerDeflectionAllowable);   // round3(2×40/250)
        Assert.Equal(702, r.BoltTensionComputed);          // 14040/(60−40)
        Assert.Equal(0.059975, r.BoltCombined);
    }

    [Fact]
    public void 挂件宽100_托板截面同步变300()
    {
        var it = Base(); it.HangerWidthMm = 100;
        var r = StonePlateEngine.Compute(it);
        Assert.Equal(300, r.LugAreaMm2);
        Assert.Equal(1.856, r.LugShearComputed);      // 2227.5/(2×2×300)
        Assert.Equal(73.913, r.HangerStressComputed); // 1113.75/300 + 10530/150
        Assert.Equal(0.105, r.HangerDeflectionComputed);
    }

    [Fact]
    public void 挂件厚4_托板截面320()
    {
        var it = Base(); it.HangerThicknessMm = 4;
        var r = StonePlateEngine.Compute(it);
        Assert.Equal(320, r.LugAreaMm2);
        Assert.Equal(1.74, r.LugShearComputed);       // 2227.5/(2×2×320)
        Assert.Equal(52.84, r.HangerStressComputed);  // 1113.75/320 + 10530/213.333
        Assert.Equal(0.055, r.HangerDeflectionComputed);
    }

    [Fact]
    public void 水平挂件6_剪力与螺栓抗剪同除6()
    {
        var it = Base(); it.HorizontalHangerCount = 6;
        var r = StonePlateEngine.Compute(it);
        Assert.Equal(371.25, r.HangerShearN);
        Assert.Equal(89.297, r.HangerStressComputed);  // 371.25/240 + 87.75
        Assert.Equal(371.25, r.BoltShearComputed);
        Assert.Equal(0.008432, r.BoltCombined);
        Assert.Equal(351, r.BoltTensionComputed);      // 承重挂件数未变
    }

    [Fact]
    public void 每挂件螺栓2_抗拉抗剪各除2()
    {
        var it = Base(); it.BoltPerHanger = 2;
        var r = StonePlateEngine.Compute(it);
        Assert.Equal(175.5, r.BoltTensionComputed);
        Assert.Equal(556.875, r.BoltShearComputed);
        Assert.Equal(0.012607, r.BoltCombined);
    }

    [Fact]
    public void 挂件材质切换_铝与不锈钢的限值分母不同()
    {
        var alu = StonePlateEngine.Compute(Alu());
        Assert.Equal(90, alu.HangerStressAllowable);     // 6063-T5
        Assert.Equal(70000, alu.HangerElasticModulusMpa);
        Assert.Equal(0.333, alu.HangerDeflectionAllowable);   // round3(2×30/180)

        var steel = StonePlateEngine.Compute(Base());
        Assert.Equal(180, steel.HangerStressAllowable);       // 316不锈钢
        Assert.Equal(206000, steel.HangerElasticModulusMpa);
        Assert.Equal(0.24, steel.HangerDeflectionAllowable);  // round3(2×30/250)

        StonePlateEngine.Input Alu()
        {
            var it = Base(); it.HangerMaterial = "6063-T5"; return it;
        }
    }

    [Theory]
    [InlineData("碳钢：4.6-C级", 6222, 5124)]
    [InlineData("碳钢：4.8-C级", 6222, 5124)]
    [InlineData("碳钢：5.6-A级", 7686, 6954)]
    [InlineData("碳钢：5.6-B级", 7686, 6954)]
    [InlineData("碳钢：8.8-A级", 14640, 11712)]
    [InlineData("碳钢：8.8-B级", 14640, 11712)]
    [InlineData("不锈钢：A1-50级", 8418, 6405)]
    [InlineData("不锈钢：A2-50级", 8418, 6405)]
    [InlineData("不锈钢：A3-70级", 11712, 8967)]
    [InlineData("不锈钢：A4-70级", 11712, 8967)]
    [InlineData("不锈钢：A5-80级", 13542, 10248)]
    public void 螺栓材质11项_设计值实机直读(string material, double ft, double fv)
    {
        var it = Base(); it.BoltMaterial = material;
        var r = StonePlateEngine.Compute(it);
        Assert.Equal(ft, r.BoltTensionAllowable);
        Assert.Equal(fv, r.BoltShearAllowable);
    }

    [Theory]
    [InlineData(5, 14.2, 2414, 1988)]
    [InlineData(6, 20.1, 3417, 2814)]
    [InlineData(7, 28.9, 4913, 4046)]
    [InlineData(8, 36.6, 6222, 5124)]
    [InlineData(10, 58.0, 9860, 8120)]
    public void 螺栓直径5项_有效面积与设计值(double d, double area, double ft, double fv)
    {
        var it = Base(); it.BoltDiameterMm = d;
        var r = StonePlateEngine.Compute(it);
        Assert.Equal(area, r.BoltAreaMm2);
        Assert.Equal(ft, r.BoltTensionAllowable);
        Assert.Equal(fv, r.BoltShearAllowable);
    }

    // ───────────────── ⑥ 未闭合分支必须显式拒绝 ─────────────────

    [Fact]
    public void 四边连接_未定位必须抛异常而不是凑数()
    {
        var it = Base();
        it.Mode = StonePlateEngine.ConnectMode.FourSide;
        var ex = Assert.Throws<NotSupportedException>(() => StonePlateEngine.Compute(it));
        Assert.Contains("四边连接", ex.Message);
    }

    [Fact]
    public void 面板种类默认板厚表_常量池原文()
    {
        Assert.Equal(30, StonePlateEngine.DefaultThicknessOf("石材"));
        Assert.Equal(25, StonePlateEngine.DefaultThicknessOf("瓷板"));
        Assert.Equal(25, StonePlateEngine.DefaultThicknessOf("陶板"));
        Assert.Equal(27, StonePlateEngine.DefaultThicknessOf("微晶玻璃板"));
        Assert.Equal(25, StonePlateEngine.DefaultThicknessOf("纤维水泥板"));
        Assert.Equal(25, StonePlateEngine.DefaultThicknessOf("超高性能混凝土（UHPC）板"));
        Assert.Equal(28, StonePlateEngine.DefaultThicknessOf("天然砂岩"));
        Assert.Equal(28, StonePlateEngine.DefaultThicknessOf("天然石灰石"));
        Assert.Equal(25, StonePlateEngine.DefaultThicknessOf("玻璃纤维板"));
    }
}
