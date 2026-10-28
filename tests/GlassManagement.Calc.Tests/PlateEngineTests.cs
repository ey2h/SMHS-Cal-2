using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 板材线「板块计算」屏（铝单板）——逐位对拍。
///
/// <para><b>期望值来源</b>：V31 实机 RichText 原文（<c>v31runs/metalplate_rib.txt</c>）
/// ＋ Frida 抓的 Variant 实值轨迹（<c>v31runs/metalplate_*_vartrace.json</c>、
/// <c>plate_rib2/3/4_vartrace.json</c>）＋ 常量池原文。
/// 算式与地址见 <c>docs/V31_PLATE_UI.md</c>。</para>
/// </summary>
public class PlateEngineTests
{
    private const double Wk = 0.00137;      // 面板口径（μsl=1.0）
    private const double AlphaMax = 0.16;   // 参数库「地区参数」北京 8 度

    private static PlateEngine.Input Base() => new()
    {
        ShortSideMm = 1100,
        LongSideMm = 1380,
        LongSideDivision = 1,
        ThicknessMm = 2.5,
        AllowableStressMpa = 54,            // 参数库「铝单板」1060 H14/H24，t=2.5 → 54
        Wk = Wk,
        AlphaMax = AlphaMax,
        ElasticModulusMpa = 70000,
        PoissonRatio = 0.3,
        DensityKnM3 = 27,
        UseRib = false,
        DeflectionDivisor = 60,
        RibDeflectionDivisor = 180,
        RibInertiaMm4 = 25340,
        RibSectionModulusMm3 = 2028,
        RibAllowableStressMpa = 90,
    };

    // ───────────────── 荷载（两处 round6 都是必需的） ─────────────────

    [Fact]
    public void 荷载_t2点5_地震项与qd_qk逐位()
    {
        var r = PlateEngine.Compute(Base());
        Assert.Equal(0.000054, r.SeismicTerm);   // 5×0.16×6.75e-05 = 5.4e-05（round6）
        Assert.Equal(0.002093, r.Qd);            // 1.5×0.00137 + 0.7×5.4e-05 = 0.0020928 → 0.002093
        Assert.Equal(0.001397, r.Qk);            // 0.00137 + 0.5×5.4e-05 = 0.001397
        Assert.Equal(1.37, r.WindStandardKpa);   // 0.00137×1000，显示 2 位
    }

    [Fact]
    public void 荷载_t3_半偶舍入才是实机值()
    {
        var it = Base();
        it.ThicknessMm = 3.0;
        var r = PlateEngine.Compute(it);
        // g_t = 27×3/1e6 = 8.1e-05；5×0.16×8.1e-05 = 6.48e-05 → round6 = 6.5e-05
        Assert.Equal(0.000065, r.SeismicTerm);
        // 1.5×0.00137 + 0.7×6.5e-05 = 0.0021005 → round6（半偶）= 0.0021
        Assert.Equal(0.0021, r.Qd);
        // 0.00137 + 0.5×6.5e-05 = 0.0014025 → round6（半偶）= 0.001402
        Assert.Equal(0.001402, r.Qk);
    }

    // ───────────────── A 板（n=1 / 不算肋） ─────────────────

    [Fact]
    public void A板_基线_逐位()
    {
        var r = PlateEngine.Compute(Base());
        Assert.False(r.IsRibbedPanel);

        Assert.Equal(0.06741, r.M);              // x=1100/1380=0.79710 → m 表 (0.75,0.8]
        Assert.Equal(0.00606, r.Alpha);          // 同区间 → α 表
        Assert.Equal(748.013, r.LambdaStrength); // round3(qk·a⁴/(E·t⁴))
        Assert.Equal(733.556, r.LambdaDeflection);
        Assert.Equal(0.4, r.KStrength);          // λ ≥ 400 → 常量 0.4
        Assert.Equal(0.4, r.KDeflection);

        Assert.Equal(65.556, r.A_StressComputed);       // 实机 65.556 MPa
        Assert.Equal(54.0, r.A_StressAllowable);        // 实机 54 MPa
        Assert.False(r.A_StressOk);                     // 实机「不满足要求(×)」

        Assert.Equal(48.543, r.A_DeflectionComputed);   // 实机 48.543 mm
        Assert.Equal(18.333, r.A_DeflectionAllowable);  // 1100/60
        Assert.False(r.A_DeflectionOk);
    }

    [Fact]
    public void A板_板厚3_强度46点545()
    {
        var it = Base();
        it.ThicknessMm = 3.0;
        var r = PlateEngine.Compute(it);
        Assert.Equal(362.023, r.LambdaStrength);   // round3(362.02261)
        Assert.Equal(0.4076, r.KStrength);         // 350<λ≤400 → 0.41−(350−362.023)/5000
        Assert.Equal(46.545, r.A_StressComputed);  // 实机 46.545 MPa
    }

    [Fact]
    public void A板_短边改1200_应力与挠度同步上升()
    {
        var it = Base();
        it.ShortSideMm = 1200;
        var r = PlateEngine.Compute(it);
        Assert.Equal(0.06026, r.M);                    // x=0.86957
        Assert.Equal(69.742, r.A_StressComputed);      // 实机 69.742 MPa
        Assert.Equal(59.789, r.A_DeflectionComputed);  // 实机 59.789 mm
        Assert.Equal(20.0, r.A_DeflectionAllowable);   // 1200/60
    }

    [Fact]
    public void A板_挠度限值改90()
    {
        var it = Base();
        it.DeflectionDivisor = 90;
        var r = PlateEngine.Compute(it);
        Assert.Equal(12.222, r.A_DeflectionAllowable);   // 1100/90 = 12.2222
        Assert.Equal(48.543, r.A_DeflectionComputed);    // 挠度本身不变
    }

    // ───────────────── B 板 + 加强肋（n ≥ 2） ─────────────────

    [Fact]
    public void B板_n2_逐位()
    {
        var it = Base();
        it.LongSideDivision = 2;
        it.UseRib = true;
        var r = PlateEngine.Compute(it);
        Assert.True(r.IsRibbedPanel);

        Assert.Equal(690.0, r.SpanAMm);          // Lx = 1380/2
        Assert.Equal(1100.0, r.SpanBMm);
        Assert.Equal(0.11395, r.M);              // 加肋板 m 表（0.6,0.65]
        Assert.Equal(0.00459, r.Alpha);          // 加肋板 α 表
        Assert.Equal(0.61629, r.KStrength);      // λ=115.807
        Assert.Equal(0.61965, r.KDeflection);    // λ=113.569

        Assert.Equal(67.180, r.B_StressComputed);      // 实机 67.18 MPa
        Assert.Equal(8.818, r.B_DeflectionComputed);   // 实机 8.818 mm
        Assert.Equal(11.5, r.B_DeflectionAllowable);   // 690/60
        Assert.True(r.B_DeflectionOk);                 // 实机「满足要求(√)」

        // 加强肋
        Assert.Equal(1100.0, r.RibSpanMm);             // L = 短边
        Assert.Equal(690.0, r.RibLoadWidthMm);         // Lx
        Assert.Equal(189781.990, r.RibMoment);         // 轨迹 189781.99012
        Assert.Equal(93.581, r.RibStressComputed);     // 实机 93.581 MPa
        Assert.Equal(90.0, r.RibStressAllowable);      // 6063-T5
        Assert.Equal(8.623, r.RibDeflectionComputed);  // 实机 8.623 mm
        Assert.Equal(6.111, r.RibDeflectionAllowable); // 1100/180
    }

    [Fact]
    public void B板_n3_比值小于0点5走兜底平均()
    {
        var it = Base();
        it.LongSideDivision = 3;
        it.UseRib = true;
        var r = PlateEngine.Compute(it);
        Assert.Equal(460.0, r.SpanAMm);
        Assert.Equal(0.4182, WindLoadEngine.VbaRound(r.Ratio, 4));
        // (0.1212 + 0.0843)/2：V31 轨迹得 0.10275000000000001 位型，按算式断言才逐位
        Assert.Equal((0.1212 + 0.0843) / 2.0, r.M);
        Assert.Equal(0.00504, r.Alpha);          // α 不平均，直接钳位
        Assert.Equal(38.752, r.B_StressComputed);
        Assert.Equal(2.744, r.B_DeflectionComputed);
        Assert.Equal(7.667, r.B_DeflectionAllowable);   // 460/60
        Assert.Equal(67.619, r.RibStressComputed);
        Assert.Equal(6.308, r.RibDeflectionComputed);
    }

    [Fact]
    public void B板_n4_逐位()
    {
        var it = Base();
        it.LongSideDivision = 4;
        it.UseRib = true;
        var r = PlateEngine.Compute(it);
        Assert.Equal(345.0, r.SpanAMm);
        Assert.Equal((0.1212 + 0.0843) / 2.0, r.M);
        Assert.Equal(24.023, r.B_StressComputed);
        Assert.Equal(0.956, r.B_DeflectionComputed);
        Assert.Equal(5.75, r.B_DeflectionAllowable);    // 345/60
        Assert.Equal(52.088, r.RibStressComputed);
        Assert.Equal(4.882, r.RibDeflectionComputed);
    }

    [Fact]
    public void 不算肋_即使等分数大于1也回到A板()
    {
        var it = Base();
        it.LongSideDivision = 3;
        it.UseRib = false;
        var r = PlateEngine.Compute(it);
        Assert.False(r.IsRibbedPanel);
        Assert.Equal(65.556, r.A_StressComputed);   // 与 n=1 完全相同
        Assert.Equal(0.0, r.B_StressComputed);
    }

    // ───────────────── 系数表与插值 ─────────────────

    [Theory]
    [InlineData(5.0, 1.0)]        // λ≤5 走「返回整型 1」分支
    [InlineData(7.238, 0.97762)]
    [InlineData(113.569, 0.61965)]
    [InlineData(115.807, 0.61629)]
    [InlineData(353.76, 0.40925)]
    [InlineData(362.023, 0.4076)]
    [InlineData(400.0, 0.4)]
    [InlineData(748.013, 0.4)]
    public void 刚度折减K_分段线性(double lambda, double expect)
        => Assert.Equal(expect, PlateEngine.KCoef(lambda));

    [Theory]
    [InlineData(0.5, 0.1022, 0.01013)]      // 表首
    [InlineData(1.0, 0.0489, 0.00406)]      // 表末
    [InlineData(0.797101, 0.06741, 0.00606)]
    [InlineData(0.3, 0.1022, 0.01013)]      // x<0.5 钳位（不平均）
    public void 无肋板系数(double x, double m, double alpha)
    {
        Assert.Equal(m, PlateEngine.MPlainCoef(x));
        Assert.Equal(alpha, PlateEngine.APlainCoef(x));
    }

    [Theory]
    [InlineData(0.5, 0.1212, 0.00504)]
    [InlineData(1.0, 0.0839, 0.00285)]
    [InlineData(0.627273, 0.11395, 0.00459)]
    [InlineData(0.313636, 0.10275000000000001, 0.00504)]   // m 平均、α 钳位
    public void 加肋板系数(double x, double m, double alpha)
    {
        Assert.Equal(m, PlateEngine.MRibCoef(x), 12);
        Assert.Equal(alpha, PlateEngine.ARibCoef(x));
    }

    [Fact]
    public void 比值大于1时取倒数()
    {
        Assert.Equal(PlateEngine.MPlainCoef(0.8), PlateEngine.MPlainCoef(1.25));
        Assert.Equal(PlateEngine.MRibCoef(0.8), PlateEngine.MRibCoef(1.25));
    }

    // ───────────────── 肋材质：8 选 + 冷弯薄壁型钢 ─────────────────
    //
    // 取证方式：同一算例（n=2、Lx=690、B=1100、t=2.5、I=25340、W=2028）
    //           逐一点击界面 8 个 RadioButton，读屏上「加强肋强度设计值」与「挠度计算值」。
    //           原始记录 v31runs/metalplate_ribmat.txt、metalplate_ribmat2.txt

    private static PlateEngine.Input Ribbed(string code, bool cold = false) => new()
    {
        ShortSideMm = 1100,
        LongSideMm = 1380,
        LongSideDivision = 2,           // Lx = 690
        ThicknessMm = 2.5,
        AllowableStressMpa = 54,
        Wk = Wk,
        AlphaMax = AlphaMax,
        ElasticModulusMpa = 70000,      // 板永远是铝
        PoissonRatio = 0.3,
        DensityKnM3 = 27,
        UseRib = true,
        DeflectionDivisor = 60,
        RibDeflectionDivisor = 180,
        RibInertiaMm4 = 25340,
        RibSectionModulusMm3 = 2028,
        RibMaterialCode = code,
        RibColdFormed = cold,
    };

    [Theory]
    [InlineData("6063-T5", 90)]
    [InlineData("6063-T6", 150)]
    [InlineData("6061-T4", 90)]
    [InlineData("6061-T6", 200)]
    [InlineData("6063A-T5", 135)]
    [InlineData("6063A-T6", 160)]
    [InlineData("Q235", 215)]
    [InlineData("Q355", 315)]
    public void 八种肋材质_强度设计值(string code, double sigma)
    {
        var r = PlateEngine.Compute(Ribbed(code));
        Assert.Equal(sigma, r.RibStressAllowable);
    }

    [Fact]
    public void 铝材质_肋应力93点581_挠度8点623()
    {
        var r = PlateEngine.Compute(Ribbed("6063-T5"));
        Assert.Equal(93.581, r.RibStressComputed);      // 189781.99/2028
        Assert.Equal(8.623, r.RibDeflectionComputed);
    }

    [Fact]
    public void 钢材质_肋应力除塑性发展系数1点05_挠度按E206000()
    {
        foreach (var code in new[] { "Q235", "Q355" })
        {
            var r = PlateEngine.Compute(Ribbed(code));
            // 93.581 / 1.05 = 89.1246 → 89.125
            Assert.Equal(89.125, r.RibStressComputed);
            // 8.623 × 70000/206000 = 2.930
            Assert.Equal(2.93, r.RibDeflectionComputed);
            // 板（B 板）不受肋材质影响，仍是铝 E=70000
            Assert.Equal(8.818, r.B_DeflectionComputed);
        }
    }

    [Fact]
    public void 冷弯薄壁型钢_钢降強度且撤掉1点05()
    {
        var q235 = PlateEngine.Compute(Ribbed("Q235", cold: true));
        Assert.Equal(205, q235.RibStressAllowable);
        Assert.Equal(93.581, q235.RibStressComputed);   // γ 回 1.00

        var q355 = PlateEngine.Compute(Ribbed("Q355", cold: true));
        Assert.Equal(310, q355.RibStressAllowable);
        Assert.Equal(93.581, q355.RibStressComputed);
    }

    [Fact]
    public void 冷弯薄壁型钢_对铝材质无影响()
    {
        var a = PlateEngine.Compute(Ribbed("6063-T5", cold: false));
        var b = PlateEngine.Compute(Ribbed("6063-T5", cold: true));
        Assert.Equal(a.RibStressAllowable, b.RibStressAllowable);
        Assert.Equal(a.RibStressComputed, b.RibStressComputed);
        Assert.Equal(a.RibDeflectionComputed, b.RibDeflectionComputed);
    }
}
