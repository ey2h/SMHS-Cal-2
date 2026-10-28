using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 埋件与焊缝计算屏逐位对拍（土建预埋 + L 型转接件 + 三面围焊）。
///
/// 期望值全部来自 V31 计算书原文（<c>v31runs/book_embed.txt</c>）与槽数组直读，
/// 算式来自 <c>v31runs/embed_trace_vartrace.json</c>（234 条 __vbaVar* 实数轨迹）。
/// 判据：F3 逐位相等。
/// </summary>
public class EmbedEngineTests
{
    private static EmbedResult Run() => EmbedEngine.Compute(EmbedEngine.DefaultInputs());

    private static void Same(string label, double actual, double expected)
        => Assert.Equal(expected.ToString("F3"), actual.ToString("F3"));

    [Fact]
    public void 荷载与内力_逐位一致()
    {
        var r = Run();
        Same("风荷载 kPa", r.WkKpa, 1.285);
        Same("剪力设计值", r.ShearDesign, 1716);
        Same("轴力设计值", r.AxialDesign, 7098.3);
        Same("弯矩设计值", r.MomentDesign, 137280);
        Same("地震作用 qe", r.QuakeLoad, 0.00032);
        Same("组合荷载", r.CombinedLoad, 0.002151);
    }

    [Fact]
    public void 锚筋面积_逐位一致()
    {
        var r = Run();
        // ★ 命名订正：αv 才是 (4−0.08d)·√(fc/fy)（V 项分母里那个 0.638），αr（层数影响）恒 1.0
        Same("αv", r.AlphaV, 0.638);
        Same("αr", r.AlphaR, 1.0);
        Same("αb", r.AlphaB, 0.767);
        Same("锚筋总截面积计算值", r.RebarAreaRequired, 57.911);
        Same("实际锚筋总截面积", r.RebarAreaActual, 452.16);
        Same("0.5fcA", r.ConcreteBearing, 357000);
    }

    /// <summary>
    /// la 用的是 asReq **分支2**（N项+0.4 的 M项），不是屏幕显示的 max。
    ///
    /// <para>HRB400 实机轨迹（`v31runs/embed_hrb400_vartrace.json`）：
    /// <c>0x19244B7 Add 48.0155+4.1721 = 52.1876</c>（分支1，屏幕显示 52.188）、
    /// <c>0x1924A7D Add 38.5610+13.5593 = 52.1203</c>（分支2）、
    /// <c>0x1926B20 Div 52.12/452.16</c> → <c>0x1926B3F Mul ×436.535 = 50.31892</c>。</para>
    ///
    /// <para>HPB300 时分支2（57.911）恰是较大值，所以旧实现用 max 也能对；
    /// HRB400 才暴露 0.066 的差（50.385 vs 50.319）。</para>
    /// </summary>
    [Fact]
    public void la用分支2_HRB400逐位()
    {
        var r = EmbedEngine.Compute(EmbedEngine.DefaultInputs() with
        {
            RebarFy = EmbedMaterials.RebarFy(1),          // 300
            AnchorAlpha = EmbedMaterials.AnchorAlpha(1),  // 0.14（带肋）
        });
        Same("锚筋总截面积计算值", r.RebarAreaRequired, 52.188);
        Same("锚筋基本锚固长度lab", r.AnchorLab, 436.535);
        Same("折减后的锚固长度la", r.AnchorLa, 50.319);
        Same("αv", r.AlphaV, 0.605);   // (4−0.96)·√(11.9/300)
    }

    [Fact]
    public void 锚固长度_逐位一致()
    {
        var r = Run();
        Same("基本锚固长度 lab", r.AnchorLab, 449.008);
        Same("折减后锚固长度 la", r.AnchorLa, 57.507);
    }

    [Fact]
    public void 转接件与焊缝_逐位一致()
    {
        var r = Run();
        Same("转接件强度", r.TransferStress, 4.43);
        Same("转接件强度设计值", r.TransferStressLimit, 215);
        Same("转接件挠度", r.TransferDeflection, 0.000276);
        Same("转接件挠度设计值", r.TransferDeflLimit, 0.64);
        Same("焊缝强度", r.WeldStress, 12.452);
        Same("焊缝强度设计值", r.WeldStressLimit, 160);
        Same("焊缝计算面积", r.WeldArea, 529.2);
        Same("焊缝截面模量", r.WeldSectionModulus, 8214.426);
    }

    /// <summary>弯矩项两分支取大：1.3 分支得 57.443，0.4 分支得 57.911，实机取后者。</summary>
    [Fact]
    public void 弯矩项取大分支()
    {
        var r = Run();
        Assert.True(r.RebarAreaRequired > 57.443,
            $"应为 0.4 分支的 57.911，实际 {r.RebarAreaRequired}");
        Same("取大分支结果", r.RebarAreaRequired, 57.911);
    }

    /// <summary>锚筋直径变化时 la 必须跟着变（防止把 57.507 写成常量）。</summary>
    [Fact]
    public void 锚筋直径变化_la随之变化()
    {
        var a = EmbedEngine.Compute(EmbedEngine.DefaultInputs());
        var b = EmbedEngine.Compute(EmbedEngine.DefaultInputs() with { RebarDiameter = 14 });
        Assert.NotEqual(a.AnchorLa.ToString("F3"), b.AnchorLa.ToString("F3"));
        Assert.NotEqual(a.AnchorLab.ToString("F3"), b.AnchorLab.ToString("F3"));
    }

    // ════════════════════════════════════════════════════════════════
    //  焊缝几何：全部由 hf / Lh / Lv / 焊接形式 算出（不再抄常量）
    //  期望值 = V31 实机扰动（v31runs/embed_weld_probe.txt）
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void 焊缝几何_L型焊_逐位一致()
    {
        var g = EmbedEngine.ComputeWeldGeometry(EmbedEngine.DefaultInputs());
        Same("he 有效厚度", g.Throat, 4.2);
        Same("Lv'", g.LegV, 88);
        Same("Lh'", g.LegH, 38);
        Same("计算周长", g.Perimeter, 126);
        Same("计算面积", g.Area, 529.2);
        Same("最远纤维", g.Centroid, 57.903);
        Same("惯性矩", g.Inertia, 475639.887);
        Same("截面模量", g.SectionModulus, 8214.426);
    }

    /// <summary>
    /// 三边围焊（`embed3side_vartrace.json`）：周长多一条水平焊缝（+38），
    /// 水平焊缝自身惯性矩由 /12 变 /6，ymax 变成对称的 Lv'/2+he = 48.2。
    /// </summary>
    [Fact]
    public void 焊缝几何_三边围焊_逐位一致()
    {
        var x = EmbedEngine.DefaultInputs() with { WeldType = WeldType.ThreeSide };
        var g = EmbedEngine.ComputeWeldGeometry(x);
        Same("计算周长", g.Perimeter, 164);
        Same("计算面积", g.Area, 688.8);
        Same("最远纤维", g.Centroid, 48.2);
        Same("惯性矩", g.Inertia, 917351.456);
        Same("截面模量", g.SectionModulus, 19032.188);
        Same("焊缝强度", EmbedEngine.Compute(x).WeldStress, 7.287);
    }

    /// <summary>
    /// V31 实机四组扰动（同一 base 会话内逐项改、逐项预演）：
    ///   Lv 100→120 → 9.596 ｜ Lh 50→60 → 11.761 ｜ hf 6→8 → 10.112 ｜ 三边围焊 → 7.287
    /// 只有焊缝强度变，其余量（剪力/轴力/弯矩/锚筋/转接件）全不动。
    /// </summary>
    [Theory]
    [InlineData(100, 50, 6, WeldType.LShape, 12.452)]
    [InlineData(120, 50, 6, WeldType.LShape, 9.596)]
    [InlineData(100, 60, 6, WeldType.LShape, 11.761)]
    [InlineData(100, 50, 8, WeldType.LShape, 10.112)]
    [InlineData(100, 50, 6, WeldType.ThreeSide, 7.287)]
    public void 焊缝几何_四组实机扰动(double lv, double lh, double hf,
                                     WeldType type, double expected)
    {
        var x = EmbedEngine.DefaultInputs() with
        {
            WeldVerticalMm = lv,
            WeldHorizontalMm = lh,
            WeldLeg = hf,
            WeldType = type,
        };
        var r = EmbedEngine.Compute(x);
        Same("焊缝强度", r.WeldStress, expected);
        // 焊缝几何之外的量必须与基线一致（本屏只有焊缝跟着这三个输入走）
        var b = EmbedEngine.Compute(EmbedEngine.DefaultInputs());
        Same("剪力不受焊缝几何影响", r.ShearDesign, b.ShearDesign);
        Same("轴力不受焊缝几何影响", r.AxialDesign, b.AxialDesign);
        Same("锚筋面积不受焊缝几何影响", r.RebarAreaRequired, b.RebarAreaRequired);
        Same("转接件强度不受焊缝几何影响", r.TransferStress, b.TransferStress);
    }
}

/// <summary>
/// 「土建预埋参数」帧的材料表与 e0 逐位对拍。
///
/// 期望值**全部是 V31 运行期实读**（`v31runs/embed_grades_probe.txt`、
/// `v31runs/embed_params_probe.txt`），不是查规范表反填：
///   · fc  ← 输出 `0.5fcA` ÷ 30000（锚板面积 60000）
///   · ft  ← 输出 `lab` 反读（HPB300 时 lab = 1.1·0.16·(270/ft)·12）
///   · fy / α ← `锚筋总截面积计算值`（∝1/fy，得 fy=300）与 `lab`（得 α·fy=42）两条互相印证
/// </summary>
public class EmbedMaterialTests
{
    private static void Same(string label, double actual, double expected)
        => Assert.Equal(expected.ToString("F3"), actual.ToString("F3"));

    /// <summary>13 个混凝土等级：0.5fcA 逐级实读（锚板面积 60000）。</summary>
    [Theory]
    [InlineData(0, "C20", 288000)]
    [InlineData(1, "C25", 357000)]
    [InlineData(2, "C30", 429000)]
    [InlineData(3, "C35", 501000)]
    [InlineData(4, "C40", 573000)]
    [InlineData(5, "C45", 633000)]
    [InlineData(6, "C50", 693000)]
    [InlineData(7, "C55", 759000)]
    [InlineData(8, "C60", 825000)]
    [InlineData(9, "C65", 891000)]
    [InlineData(10, "C70", 954000)]
    [InlineData(11, "C75", 1014000)]
    [InlineData(12, "C80", 1077000)]
    public void 混凝土等级_0点5fcA逐级实读(int index, string grade, double fcA)
    {
        Assert.Equal(grade, EmbedMaterials.ConcreteGrades[index]);
        var r = EmbedEngine.Compute(EmbedEngine.DefaultInputs() with
        {
            ConcreteFc = EmbedMaterials.ConcreteFc(index),
        });
        Same("0.5fcA", r.ConcreteBearing, fcA);
    }

    /// <summary>
    /// ft 逐级：13 级一一对应，**C65~C80 是四条独立分支、字面量都写 2.04**（反汇编 ft 链
    /// <c>0x19261A1 / 0x19262A4 / 0x19263A7 / 0x19264AA</c>）。
    /// 判据：lab 逐级与实机一致（518.4 / 449.008 / … / 279.529），C60~C80 相同是**程序如此**，
    /// 不是"表越界回退"。
    /// </summary>
    [Theory]
    [InlineData(0, 518.4)]
    [InlineData(1, 449.008)]
    [InlineData(2, 398.769)]
    [InlineData(3, 363.21)]
    [InlineData(4, 333.474)]
    [InlineData(5, 316.8)]
    [InlineData(6, 301.714)]
    [InlineData(7, 290.939)]
    [InlineData(8, 279.529)]
    [InlineData(9, 279.529)]
    [InlineData(10, 279.529)]
    [InlineData(11, 279.529)]
    [InlineData(12, 279.529)]
    public void 混凝土等级_ft逐级(int index, double lab)
    {
        var r = EmbedEngine.Compute(EmbedEngine.DefaultInputs() with
        {
            ConcreteFt = EmbedMaterials.ConcreteFt(index),
        });
        Same("lab", r.AnchorLab, lab);
    }

    /// <summary>
    /// 锚筋材质 HRB400：fy=300 且 α=0.14（带肋），两个输出同时吻合
    /// ⇒ 不是"取一个凑一个"。
    /// </summary>
    [Fact]
    public void 锚筋材质HRB400_fy300_alpha014_逐位()
    {
        Assert.Equal(2, EmbedMaterials.RebarMaterials.Length);     // 实机只有 HPB300/HRB400
        Assert.Equal(270, EmbedMaterials.RebarFy(0));
        Assert.Equal(300, EmbedMaterials.RebarFy(1));
        Assert.Equal(0.16, EmbedMaterials.AnchorAlpha(0), 3);
        Assert.Equal(0.14, EmbedMaterials.AnchorAlpha(1), 3);

        var r = EmbedEngine.Compute(EmbedEngine.DefaultInputs() with
        {
            RebarFy = EmbedMaterials.RebarFy(1),
            AnchorAlpha = EmbedMaterials.AnchorAlpha(1),
        });
        Same("锚筋总截面积计算值", r.RebarAreaRequired, 52.188);
        Same("锚筋基本锚固长度lab", r.AnchorLab, 436.535);
        // 已于 2026-10-26 定位并闭合：la 的分子是 asReq **分支2**（52.1203），不是显示的 max（52.188）。
        // 见 la用分支2_HRB400逐位 与 v31runs/embed_hrb400_vartrace.json。
        Same("折减后的锚固长度la", r.AnchorLa, 50.319);
    }

    /// <summary>材质→锚筋外型联动（实机 en=0 的原因）。</summary>
    [Fact]
    public void 材质驱动锚筋外型()
    {
        Assert.Equal("光圆", EmbedMaterials.RebarShapes[EmbedMaterials.ShapeIndex(0)]);
        Assert.Equal("带肋", EmbedMaterials.RebarShapes[EmbedMaterials.ShapeIndex(1)]);
    }

    /// <summary>
    /// e0 80→100：弯矩 137280→171600、挠度设计值 0.64→0.8、挠度 0.000276→0.00054、
    /// 焊缝 12.452→14.152、转接件强度 4.43→4.842、锚筋面积 57.911→61.678。
    /// 六个数同时对上，证明 e0 一个输入进两条算式。
    /// </summary>
    [Fact]
    public void e0同时作弯矩力臂与悬臂_逐位()
    {
        var r = EmbedEngine.Compute(EmbedEngine.DefaultInputs() with
        {
            MomentArmMm = 100,
            CantileverMm = 100,
        });
        Same("弯矩设计值", r.MomentDesign, 171600);
        Same("转接件挠度设计值", r.TransferDeflLimit, 0.8);
        Assert.Equal("0.00054", r.TransferDeflection.ToString("0.######"));
        Same("焊缝强度", r.WeldStress, 14.152);
        Same("转接件强度", r.TransferStress, 4.842);
        Same("锚筋总截面积计算值", r.RebarAreaRequired, 61.678);
        Same("折减后的锚固长度la", r.AnchorLa, 61.248);
        // 剪力/轴力不随 e0 变
        var b = EmbedEngine.Compute(EmbedEngine.DefaultInputs());
        Same("剪力设计值", r.ShearDesign, b.ShearDesign);
        Same("轴力设计值", r.AxialDesign, b.AxialDesign);
    }

    /// <summary>候选表规模与实机下拉一致（CB_GETLBTEXT 直读的项数）。</summary>
    [Fact]
    public void 下拉候选表规模与实机一致()
    {
        Assert.Equal(13, EmbedMaterials.ConcreteGrades.Length);
        Assert.Equal(14, EmbedMaterials.RebarDiameters.Length);
        Assert.Equal(3, EmbedMaterials.RebarLayers.Length);
        Assert.Equal(3, EmbedMaterials.RebarPerLayer.Length);
        Assert.Equal(10, EmbedMaterials.PlateThicknesses.Length);
        Assert.Equal(20, WeldSectionTable.Count);
    }
}

/// <summary>
/// 混凝土 fc / ft 全表锁定 —— 期望值是**反汇编 If 链的字面量**，不是规范表、也不是输出反推。
///
/// <para>fc 链 <c>0x19224FB–0x1923168</c>（临时槽 18）、ft 链 <c>0x1925853–0x19264B7</c>（槽 48），
/// 各 13 条分支，形式都是 <c>If 等级="Cxx" Then 槽=fld qword ptr[常量池]</c>。</para>
///
/// <para>C65~C80 的 ft 在代码里是**四条各自独立的分支，字面量都写 2.04**
/// （`0x19261A1 / 0x19262A4 / 0x19263A7 / 0x19264AA`）。</para>
/// </summary>
public class EmbedMaterialLiteralTests
{
    /// <summary>fc：13 级逐项（反汇编字面量）。</summary>
    [Theory]
    [InlineData(0, "C20", 9.6)]
    [InlineData(1, "C25", 11.9)]
    [InlineData(2, "C30", 14.3)]
    [InlineData(3, "C35", 16.7)]
    [InlineData(4, "C40", 19.1)]
    [InlineData(5, "C45", 21.1)]
    [InlineData(6, "C50", 23.1)]
    [InlineData(7, "C55", 25.3)]
    [InlineData(8, "C60", 27.5)]
    [InlineData(9, "C65", 29.7)]
    [InlineData(10, "C70", 31.8)]
    [InlineData(11, "C75", 33.8)]
    [InlineData(12, "C80", 35.9)]
    public void fc_字面量逐级(int i, string grade, double fc)
    {
        Assert.Equal(grade, EmbedMaterials.ConcreteGrades[i]);
        Assert.Equal(fc, EmbedMaterials.ConcreteFc(i));
    }

    /// <summary>ft：13 级逐项（反汇编字面量，C65 起四条分支均为 2.04）。</summary>
    [Theory]
    [InlineData(0, "C20", 1.10)]
    [InlineData(1, "C25", 1.27)]
    [InlineData(2, "C30", 1.43)]
    [InlineData(3, "C35", 1.57)]
    [InlineData(4, "C40", 1.71)]
    [InlineData(5, "C45", 1.80)]
    [InlineData(6, "C50", 1.89)]
    [InlineData(7, "C55", 1.96)]
    [InlineData(8, "C60", 2.04)]
    [InlineData(9, "C65", 2.04)]
    [InlineData(10, "C70", 2.04)]
    [InlineData(11, "C75", 2.04)]
    [InlineData(12, "C80", 2.04)]
    public void ft_字面量逐级(int i, string grade, double ft)
    {
        Assert.Equal(grade, EmbedMaterials.ConcreteGrades[i]);
        Assert.Equal(ft, EmbedMaterials.ConcreteFt(i));
    }

    /// <summary>表长必须 13（防止有人把 ft 缩回 9 项再靠 clamp 兜底）。</summary>
    [Fact]
    public void 等级表长度13且无兜底()
    {
        Assert.Equal(13, EmbedMaterials.ConcreteGrades.Length);
        // 逐级都必须能取到确定值，且 C60 与 C65 的 ft 相同但仍是两条数据
        Assert.Equal(2.04, EmbedMaterials.ConcreteFt(8));
        Assert.Equal(2.04, EmbedMaterials.ConcreteFt(12));
        // fc 严格单调递增（若用兜底会变成平段）
        for (int i = 1; i < 13; i++)
            Assert.True(EmbedMaterials.ConcreteFc(i) > EmbedMaterials.ConcreteFc(i - 1));
    }
}
