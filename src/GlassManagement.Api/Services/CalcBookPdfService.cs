using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using GlassManagement.Api.Data;

namespace GlassManagement.Api.Services;

/// <summary>计算书PDF生成服务</summary>
public class CalcBookPdfService
{
    // 通用样式常量
    private const float TitleSize = 18;
    private const float SubTitleSize = 14;
    private const float HeadingSize = 12;
    private const float BodySize = 10.5f;
    private const float SmallSize = 9;
    private const string FontFamily = "Noto Sans SC";

    static CalcBookPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>生成玻璃幕墙计算书PDF</summary>
    public byte[] GenerateGlassCurtainWallReport(GlassCalcBookInput input)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginTop(2.5f, Unit.Centimetre);
                page.MarginBottom(2f, Unit.Centimetre);
                page.MarginHorizontal(2.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(BodySize).FontFamily(FontFamily));

                page.Header().Element(c => RenderHeader(c, input));
                page.Content().Element(c => RenderContent(c, input));
                page.Footer().Element(RenderFooter);
            });
        });

        return doc.GeneratePdf();
    }

    #region 封面与页眉页脚

    private void RenderHeader(IContainer container, GlassCalcBookInput input)
    {
        container.Row(row =>
        {
            row.RelativeItem().Text(input.ProjectName ?? "").FontSize(SmallSize).FontColor(Colors.Grey.Medium);
            row.ConstantItem(120).AlignRight().Text("玻璃幕墙设计计算书").FontSize(SmallSize).FontColor(Colors.Grey.Medium);
        });
    }

    private void RenderFooter(IContainer container)
    {
        container.AlignCenter().Text(text =>
        {
            text.Span("第 ").FontSize(SmallSize);
            text.CurrentPageNumber().FontSize(SmallSize);
            text.Span(" / ").FontSize(SmallSize);
            text.TotalPages().FontSize(SmallSize);
            text.Span(" 页").FontSize(SmallSize);
        });
    }

    #endregion

    #region 正文内容

    private void RenderContent(IContainer container, GlassCalcBookInput input)
    {
        container.Column(col =>
        {
            col.Spacing(8);

            // 1. 封面
            RenderCoverPage(col, input);
            col.Item().PageBreak();

            // 2. 规范引用
            RenderStandardsSection(col);
            col.Item().PageBreak();

            // 3. 基本参数
            RenderBasicParameters(col, input);

            // 4. 风荷载计算
            RenderWindLoadSection(col, input);

            // 5. 玻璃强度验算
            RenderGlassStrengthSection(col, input);

            // 6. 玻璃挠度验算
            RenderGlassDeflectionSection(col, input);

            // 7. 立柱计算
            if (input.Column != null)
            {
                col.Item().PageBreak();
                RenderColumnSection(col, input);
            }

            // 8. 横梁计算
            if (input.Beam != null)
            {
                col.Item().PageBreak();
                RenderBeamSection(col, input);
            }

            // 9. 结论
            col.Item().PageBreak();
            RenderConclusionSection(col, input);
        });
    }

    #endregion

    #region 1. 封面

    private void RenderCoverPage(ColumnDescriptor col, GlassCalcBookInput input)
    {
        col.Item().PaddingTop(80).AlignCenter().Text(input.ProjectName ?? "XX幕墙装饰工程").FontSize(22).Bold();
        col.Item().PaddingTop(30).AlignCenter().Text("玻璃幕墙").FontSize(28).Bold();
        col.Item().PaddingTop(10).AlignCenter().Text("设计计算书").FontSize(24).Bold();
        col.Item().PaddingTop(60).AlignCenter().Column(inner =>
        {
            inner.Spacing(12);
            inner.Item().Text($"设计：{input.Designer ?? "___________"}").FontSize(14);
            inner.Item().Text($"校对：{input.Reviewer ?? "___________"}").FontSize(14);
            inner.Item().Text($"审核：{input.Approver ?? "___________"}").FontSize(14);
            inner.Item().Text($"批准：{input.Supervisor ?? "___________"}").FontSize(14);
        });
        col.Item().PaddingTop(60).AlignCenter().Text(input.CompanyName ?? "XX工程有限公司").FontSize(16);
        col.Item().PaddingTop(10).AlignCenter().Text(input.ReportDate ?? DateTime.Now.ToString("yyyy年M月d日")).FontSize(14);
    }

    #endregion

    #region 2. 规范引用

    private void RenderStandardsSection(ColumnDescriptor col)
    {
        SectionTitle(col, "一、计算引用的规范、标准及资料");

        col.Item().Text("幕墙设计规范：").FontSize(BodySize).Bold();
        var standards = new (string name, string code)[]
        {
            ("《玻璃幕墙工程技术规范》", "JGJ102-2003"),
            ("《铝合金结构设计规范》", "GB50429-2007"),
            ("《金属与石材幕墙工程技术规范》", "JGJ133-2001"),
            ("《建筑幕墙》", "GB/T21086-2007"),
            ("《人造板材幕墙工程技术规范》", "JGJ336-2016"),
        };
        foreach (var s in standards)
            col.Item().PaddingLeft(20).Row(r =>
            {
                r.RelativeItem().Text(s.name).FontSize(BodySize);
                r.ConstantItem(150).AlignRight().Text(s.code).FontSize(BodySize);
            });

        col.Item().PaddingTop(8).Text("建筑设计规范：").FontSize(BodySize).Bold();
        var buildStds = new (string name, string code)[]
        {
            ("《建筑结构荷载规范》", "GB50009-2012"),
            ("《建筑抗震设计标准》", "GB/T50011-2010(2024年版)"),
            ("《钢结构设计标准》", "GB50017-2017"),
            ("《混凝土结构后锚固技术规程》", "JGJ145-2013"),
            ("《工程结构通用规范》", "GB55001-2021"),
            ("《民用建筑通用规范》", "GB55031-2022"),
        };
        foreach (var s in buildStds)
            col.Item().PaddingLeft(20).Row(r =>
            {
                r.RelativeItem().Text(s.name).FontSize(BodySize);
                r.ConstantItem(200).AlignRight().Text(s.code).FontSize(BodySize);
            });
    }

    #endregion

    #region 3. 基本参数

    private void RenderBasicParameters(ColumnDescriptor col, GlassCalcBookInput input)
    {
        SectionTitle(col, "二、基本设计参数");

        var p = input;
        ParamTable(col, new[]
        {
            ("工程地点", p.City ?? "—"),
            ("建筑高度 H (m)", F(p.BuildingHeight)),
            ("幕墙安装高度 (m)", F(p.InstallHeight)),
            ("分格宽度 a (mm)", F(p.PanelWidth)),
            ("分格高度 b (mm)", F(p.PanelHeight)),
            ("基本风压 W₀ (kPa)", F(p.BasicWindPressure / 1000.0)),
            ("地面粗糙度类别", p.TerrainCategory ?? "B"),
            ("抗震设防烈度", $"{p.SeismicIntensity}度"),
            ("玻璃类型", p.GlassDescription ?? "—"),
        });
    }

    #endregion

    #region 4. 风荷载

    private void RenderWindLoadSection(ColumnDescriptor col, GlassCalcBookInput input)
    {
        SectionTitle(col, "三、风荷载计算");

        col.Item().Text("根据《建筑结构荷载规范》GB50009-2012：").FontSize(BodySize);
        col.Item().PaddingTop(4).Text("Wk = βgz · μs1 · μz · W₀").FontSize(BodySize).Bold();

        col.Item().PaddingTop(8).Text("其中：").FontSize(BodySize);
        FormulaLine(col, "βgz", "阵风系数", F(input.GustFactor));
        FormulaLine(col, "μs1", "局部风压体型系数", F(input.ShapeFactor));
        FormulaLine(col, "μz", "风压高度变化系数", F(input.HeightFactor));
        FormulaLine(col, "W₀", "基本风压 (kPa)", F(input.BasicWindPressure / 1000.0));

        var wk = input.WindPressureDesign;
        col.Item().PaddingTop(8).Text($"Wk = {F(input.GustFactor)} × {F(input.ShapeFactor)} × {F(input.HeightFactor)} × {F(input.BasicWindPressure / 1000.0)} = {F(wk / 1000.0)} kPa").FontSize(BodySize).Bold();

        col.Item().PaddingTop(4).Text($"设计风荷载标准值 Wk = {F(wk)} Pa = {F(wk / 1000.0)} kPa").FontSize(BodySize);
    }

    #endregion

    #region 5. 玻璃强度验算

    private void RenderGlassStrengthSection(ColumnDescriptor col, GlassCalcBookInput input)
    {
        SectionTitle(col, "四、玻璃板块强度验算");

        col.Item().Text("根据《玻璃幕墙工程技术规范》JGJ102-2003 第5.4条：").FontSize(BodySize);
        col.Item().PaddingTop(4).Text("σmax = αq · q · a² / t² ≤ fg").FontSize(BodySize).Bold();

        var g = input.GlassCalc;
        if (g != null)
        {
            col.Item().PaddingTop(8).Text("其中：").FontSize(BodySize);
            FormulaLine(col, "a", "玻璃短边 (mm)", F(g.ShortSide));
            FormulaLine(col, "b", "玻璃长边 (mm)", F(g.LongSide));
            FormulaLine(col, "t", "玻璃等效厚度 (mm)", F(g.EquivalentThickness));
            FormulaLine(col, "q", "荷载设计值 (kPa)", F(g.LoadDesign / 1000.0));
            FormulaLine(col, "αq", "弯曲应力系数", F(g.StressCoeff));
            FormulaLine(col, "fg", "玻璃设计强度 (MPa)", F(g.DesignStrength));

            col.Item().PaddingTop(8).Text($"σmax = {F(g.StressCoeff)} × {F(g.LoadDesign / 1000.0)} × {F(g.ShortSide)}² / {F(g.EquivalentThickness)}²").FontSize(BodySize);
            col.Item().Text($"     = {F(g.MaxStress)} MPa").FontSize(BodySize).Bold();

            var pass = g.MaxStress <= g.DesignStrength;
            col.Item().PaddingTop(4).Text(text =>
            {
                text.Span($"σmax = {F(g.MaxStress)} MPa ");
                text.Span(pass ? "≤" : ">").FontSize(BodySize);
                text.Span($" fg = {F(g.DesignStrength)} MPa  →  ");
                text.Span(pass ? "✓ 满足要求" : "✗ 不满足要求")
                    .FontSize(BodySize).Bold()
                    .FontColor(pass ? Colors.Green.Darken2 : Colors.Red.Darken2);
            });
        }
    }

    #endregion

    #region 6. 玻璃挠度验算

    private void RenderGlassDeflectionSection(ColumnDescriptor col, GlassCalcBookInput input)
    {
        SectionTitle(col, "五、玻璃板块挠度验算");

        col.Item().Text("根据 JGJ102-2003 第5.5条：").FontSize(BodySize);
        col.Item().PaddingTop(4).Text("δmax = αd · q · a⁴ / (E · t³) ≤ [δ]").FontSize(BodySize).Bold();

        var g = input.GlassCalc;
        if (g != null)
        {
            FormulaLine(col, "αd", "挠度系数", F(g.DeflectionCoeff));
            FormulaLine(col, "E", "弹性模量 (MPa)", F(g.ElasticModulus));
            FormulaLine(col, "[δ]", "挠度限值 (mm)", F(g.DeflectionLimit));

            col.Item().PaddingTop(8).Text($"δmax = {F(g.MaxDeflection)} mm").FontSize(BodySize).Bold();

            var pass = g.MaxDeflection <= g.DeflectionLimit;
            col.Item().PaddingTop(4).Text(text =>
            {
                text.Span($"δmax = {F(g.MaxDeflection)} mm ");
                text.Span(pass ? "≤" : ">").FontSize(BodySize);
                text.Span($" [δ] = {F(g.DeflectionLimit)} mm  →  ");
                text.Span(pass ? "✓ 满足要求" : "✗ 不满足要求")
                    .FontSize(BodySize).Bold()
                    .FontColor(pass ? Colors.Green.Darken2 : Colors.Red.Darken2);
            });
        }
    }

    #endregion

    #region 7. 立柱计算

    private void RenderColumnSection(ColumnDescriptor col, GlassCalcBookInput input)
    {
        SectionTitle(col, "六、立柱强度及挠度验算");

        var c = input.Column!;
        col.Item().Text($"立柱型材：{c.ProfileCode}").FontSize(BodySize);
        col.Item().Text($"计算跨度 L = {F(c.Span)} mm").FontSize(BodySize);
        col.Item().Text($"支承条件：{c.SupportType}").FontSize(BodySize);

        ParamTable(col, new[]
        {
            ("Ix (cm⁴)", F(c.Ix)),
            ("Wx (cm³)", F(c.Wx)),
            ("截面积 A (mm²)", F(c.Area)),
            ("荷载宽度 (mm)", F(c.LoadWidth)),
        });

        col.Item().PaddingTop(8).Text("— 强度验算 —").FontSize(HeadingSize).Bold();
        col.Item().Text($"σmax = M / Wx = {F(c.MaxStress)} MPa").FontSize(BodySize);
        ResultLine(col, c.MaxStress, c.AllowableStress, "MPa", "σ", "[σ]");

        col.Item().PaddingTop(8).Text("— 挠度验算 —").FontSize(HeadingSize).Bold();
        col.Item().Text($"δmax = {F(c.MaxDeflection)} mm").FontSize(BodySize);
        ResultLine(col, c.MaxDeflection, c.DeflectionLimit, "mm", "δ", "[δ]");
    }

    #endregion

    #region 8. 横梁计算

    private void RenderBeamSection(ColumnDescriptor col, GlassCalcBookInput input)
    {
        SectionTitle(col, "七、横梁强度及挠度验算");

        var b = input.Beam!;
        col.Item().Text($"横梁型材：{b.ProfileCode}").FontSize(BodySize);
        col.Item().Text($"计算跨度 L = {F(b.Span)} mm").FontSize(BodySize);

        col.Item().PaddingTop(8).Text("— 强度验算 —").FontSize(HeadingSize).Bold();
        col.Item().Text($"σmax = {F(b.MaxStress)} MPa").FontSize(BodySize);
        ResultLine(col, b.MaxStress, b.AllowableStress, "MPa", "σ", "[σ]");

        col.Item().PaddingTop(8).Text("— 挠度验算 —").FontSize(HeadingSize).Bold();
        col.Item().Text($"δmax = {F(b.MaxDeflection)} mm").FontSize(BodySize);
        ResultLine(col, b.MaxDeflection, b.DeflectionLimit, "mm", "δ", "[δ]");
    }

    #endregion

    #region 9. 结论

    private void RenderConclusionSection(ColumnDescriptor col, GlassCalcBookInput input)
    {
        SectionTitle(col, "八、计算结论");

        var results = new List<(string item, bool pass)>();
        if (input.GlassCalc != null)
        {
            results.Add(("玻璃强度", input.GlassCalc.MaxStress <= input.GlassCalc.DesignStrength));
            results.Add(("玻璃挠度", input.GlassCalc.MaxDeflection <= input.GlassCalc.DeflectionLimit));
        }
        if (input.Column != null)
        {
            results.Add(("立柱强度", input.Column.MaxStress <= input.Column.AllowableStress));
            results.Add(("立柱挠度", input.Column.MaxDeflection <= input.Column.DeflectionLimit));
        }
        if (input.Beam != null)
        {
            results.Add(("横梁强度", input.Beam.MaxStress <= input.Beam.AllowableStress));
            results.Add(("横梁挠度", input.Beam.MaxDeflection <= input.Beam.DeflectionLimit));
        }

        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(40);
                c.RelativeColumn();
                c.ConstantColumn(100);
            });

            table.Header(h =>
            {
                h.Cell().Border(1).Padding(4).Text("序号").FontSize(BodySize).Bold();
                h.Cell().Border(1).Padding(4).Text("验算项目").FontSize(BodySize).Bold();
                h.Cell().Border(1).Padding(4).AlignCenter().Text("结果").FontSize(BodySize).Bold();
            });

            for (int i = 0; i < results.Count; i++)
            {
                var (item, pass) = results[i];
                table.Cell().Border(1).Padding(4).Text($"{i + 1}").FontSize(BodySize);
                table.Cell().Border(1).Padding(4).Text(item).FontSize(BodySize);
                table.Cell().Border(1).Padding(4).AlignCenter()
                    .Text(pass ? "✓ 满足" : "✗ 不满足").FontSize(BodySize).Bold()
                    .FontColor(pass ? Colors.Green.Darken2 : Colors.Red.Darken2);
            }
        });

        var allPass = results.All(r => r.pass);
        col.Item().PaddingTop(15).Text(text =>
        {
            text.Span("综合结论：").FontSize(HeadingSize).Bold();
            text.Span(allPass ? "所有验算项目均满足规范要求，设计安全可靠。" : "部分验算项目不满足要求，请调整设计参数。")
                .FontSize(HeadingSize).Bold()
                .FontColor(allPass ? Colors.Green.Darken2 : Colors.Red.Darken2);
        });
    }

    #endregion

    #region 辅助方法

    private void SectionTitle(ColumnDescriptor col, string title)
    {
        col.Item().PaddingTop(15).PaddingBottom(5)
            .BorderBottom(1).BorderColor(Colors.Grey.Darken1)
            .Text(title).FontSize(SubTitleSize).Bold();
    }

    private void FormulaLine(ColumnDescriptor col, string symbol, string desc, string value)
    {
        col.Item().PaddingLeft(20).Row(r =>
        {
            r.ConstantItem(60).Text(symbol).FontSize(BodySize).Bold();
            r.ConstantItem(10).Text("—").FontSize(BodySize);
            r.RelativeItem().Text(desc).FontSize(BodySize);
            r.ConstantItem(80).AlignRight().Text(value).FontSize(BodySize).Bold();
        });
    }

    private void ResultLine(ColumnDescriptor col, double actual, double limit, string unit, string symActual, string symLimit)
    {
        var pass = actual <= limit;
        col.Item().PaddingTop(4).Text(text =>
        {
            text.Span($"{symActual} = {F(actual)} {unit} ");
            text.Span(pass ? "≤" : ">").FontSize(BodySize);
            text.Span($" {symLimit} = {F(limit)} {unit}  →  ");
            text.Span(pass ? "✓ 满足要求" : "✗ 不满足要求")
                .FontSize(BodySize).Bold()
                .FontColor(pass ? Colors.Green.Darken2 : Colors.Red.Darken2);
        });
    }

    private void ParamTable(ColumnDescriptor col, (string label, string value)[] items)
    {
        col.Item().PaddingTop(5).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2);
                c.RelativeColumn(1);
            });
            foreach (var (label, value) in items)
            {
                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(label).FontSize(BodySize);
                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(value).FontSize(BodySize).Bold();
            }
        });
    }

    private static string F(double v) => double.IsNaN(v) || double.IsInfinity(v) ? "—" : v.ToString("F2");

    #endregion
}

#region 输入模型

public class GlassCalcBookInput
{
    // 项目信息
    public string? ProjectName { get; set; }
    public string? CompanyName { get; set; }
    public string? Designer { get; set; }
    public string? Reviewer { get; set; }
    public string? Approver { get; set; }
    public string? Supervisor { get; set; }
    public string? ReportDate { get; set; }

    // ========== 胶类/硅酮胶参数（新增）==========
    /// <summary>胶缝宽度 (mm)</summary>
    public double SealantWidth { get; set; } = 12;
    /// <summary>胶缝深度 (mm)</summary>
    public double SealantDepth { get; set; } = 6;
    /// <summary>硅酮胶强度设计值 (MPa)</summary>
    public double SealantStrength { get; set; } = 0.2;
    /// <summary>硅酮胶弹性模量 (MPa)</summary>
    public double SealantModulus { get; set; } = 1.0;
    /// <summary>伸缩缝宽度 (mm)</summary>
    public double JointWidth { get; set; } = 15;

    // ========== 压板参数（新增）==========
    /// <summary>压板数量n</summary>
    public int PressurePlateCount { get; set; } = 2;
    /// <summary>压板厚度t (mm)</summary>
    public double PressurePlateThickness { get; set; } = 4;
    /// <summary>压板宽度b (mm)</summary>
    public double PressurePlateWidth { get; set; } = 40;
    /// <summary>压板材料</summary>
    public string? PressurePlateMaterial { get; set; } = "Q235";
    /// <summary>压板材料强度 (MPa)</summary>
    public double PressurePlateStrength { get; set; } = 215;

    // ========== 副框参数（新增）==========
    /// <summary>副框类型</summary>
    public string? SubFrameType { get; set; }
    /// <summary>副框宽度 (mm)</summary>
    public double SubFrameWidth { get; set; }
    /// <summary>副框高度 (mm)</summary>
    public double SubFrameHeight { get; set; }
    /// <summary>副框壁厚 (mm)</summary>
    public double SubFrameThickness { get; set; }
    /// <summary>副框材料</summary>
    public string? SubFrameMaterial { get; set; }

    // ========== 承托件参数（新增）==========
    /// <summary>承托件类型</summary>
    public string? BracketType { get; set; }
    /// <summary>承托高度 (mm)</summary>
    public double BracketHeight { get; set; }
    /// <summary>承托件厚度 (mm)</summary>
    public double BracketThickness { get; set; } = 4;
    /// <summary>承托件宽度 (mm)</summary>
    public double BracketWidth { get; set; } = 50;

    // 基本参数
    public string? City { get; set; }
    public double BuildingHeight { get; set; }
    public double InstallHeight { get; set; }
    public double PanelWidth { get; set; }  // mm
    public double PanelHeight { get; set; } // mm
    public double BasicWindPressure { get; set; } // Pa
    public string? TerrainCategory { get; set; }
    public int SeismicIntensity { get; set; }
    public string? GlassDescription { get; set; }

    // 风荷载
    public double GustFactor { get; set; }
    public double ShapeFactor { get; set; }
    public double HeightFactor { get; set; }
    public double WindPressureDesign { get; set; } // Pa

    // 地震参数（新增）
    /// <summary>水平地震影响系数最大值αmax</summary>
    public double AlphaMax { get; set; }
    /// <summary>场地调整系数Fa，默认1.0</summary>
    public double SiteFactor { get; set; } = 1.0;
    /// <summary>场地类别（如Ⅱ类）</summary>
    public string? SiteCategory { get; set; }

    // SGP夹层参数（新增）
    /// <summary>SGP胶片剪切模量G (MPa)，50℃/1min条件，默认11.3</summary>
    public double SgpShearModulus { get; set; } = 11.3;
    /// <summary>胶片厚度tv (mm)</summary>
    public double InterlayerThickness { get; set; } = 1.52;

    // 玻璃各层厚度（新增，支持多层）
    /// <summary>玻璃第1层厚度t1 (mm)</summary>
    public double GlassThickness1 { get; set; }
    /// <summary>玻璃第2层厚度t2 (mm)</summary>
    public double GlassThickness2 { get; set; }
    /// <summary>玻璃强度设计值fg (MPa)</summary>
    public double GlassDesignStrength { get; set; } = 84;

    // μs1相关参数（新增）
    /// <summary>基准体型系数μs1(1)，一般取-1.0</summary>
    public double Mus1Base { get; set; } = -1.0;
    /// <summary>μs1(25)，从属面积25m²时的体型系数</summary>
    public double Mus1At25 { get; set; } = -0.8;

    // 立柱附加参数（新增）
    /// <summary>立柱左分格宽 (mm)</summary>
    public double ColumnLeftWidth { get; set; }
    /// <summary>立柱右分格宽 (mm)</summary>
    public double ColumnRightWidth { get; set; }
    /// <summary>立柱材质，如6063-T5</summary>
    public string? ColumnMaterial { get; set; }
    /// <summary>安装方式，如偏心受拉</summary>
    public string? ColumnInstallType { get; set; }

    // 双跨梁参数（新增）
    /// <summary>短跨L1 (mm)</summary>
    public double SpanL1 { get; set; }
    /// <summary>长跨L2 (mm)</summary>
    public double SpanL2 { get; set; }

    // 连接件参数（新增）
    /// <summary>螺栓直径d (mm)</summary>
    public double BoltDiameter { get; set; } = 5;
    /// <summary>螺栓单个抗剪承载力Nv (N)</summary>
    public double BoltShearCapacity { get; set; } = 3920;
    /// <summary>螺栓折减系数k</summary>
    public double BoltReductionFactor { get; set; } = 0.8;
    /// <summary>最小壁厚tmin (mm)</summary>
    public double MinWallThickness { get; set; } = 2.5;
    /// <summary>壁承压强度fb (MPa)</summary>
    public double BearingStrength { get; set; } = 230;

    // 预埋件参数（新增）
    /// <summary>锚筋直径d (mm)</summary>
    public double AnchorDiameter { get; set; } = 10;
    /// <summary>锚筋屈服强度fy (MPa)</summary>
    public double AnchorFy { get; set; } = 300;
    /// <summary>混凝土抗拉强度ft (MPa)</summary>
    public double ConcreteFt { get; set; } = 1.43;
    /// <summary>混凝土抗压强度fc (MPa)</summary>
    public double ConcreteFc { get; set; } = 14.3;
    /// <summary>锚筋间距z (mm)</summary>
    public double AnchorSpacing { get; set; } = 60;

    // 焊缝参数（新增）
    /// <summary>焊脚尺寸hf (mm)</summary>
    public double WeldSize { get; set; } = 6;
    /// <summary>焊缝强度ffw (MPa)</summary>
    public double WeldStrength { get; set; } = 160;
    /// <summary>焊缝强度系数βf</summary>
    public double WeldBetaF { get; set; } = 0.7;

    // 转接件参数（新增）
    /// <summary>转接件型号（如10#槽钢）</summary>
    public string? TransferProfile { get; set; }
    /// <summary>转接件截面积A (mm²)</summary>
    public double TransferArea { get; set; }
    /// <summary>转接件惯性矩I (cm⁴)</summary>
    public double TransferIx { get; set; }
    /// <summary>转接件抵抗矩W (cm³)</summary>
    public double TransferWx { get; set; }
    /// <summary>转接件悬臂长度e (mm)</summary>
    public double TransferLength { get; set; } = 100;
    /// <summary>转接件强度设计值f (MPa)</summary>
    public double TransferStrength { get; set; } = 215;

    // 玻璃验算
    public GlassCalcResult? GlassCalc { get; set; }

    // 立柱
    public MemberCalcResult? Column { get; set; }

    // 横梁
    public MemberCalcResult? Beam { get; set; }
}

public class GlassCalcResult
{
    public double ShortSide { get; set; }
    public double LongSide { get; set; }
    public double EquivalentThickness { get; set; }
    public double LoadDesign { get; set; } // Pa
    public double StressCoeff { get; set; }
    public double DesignStrength { get; set; } // MPa
    public double MaxStress { get; set; } // MPa
    public double DeflectionCoeff { get; set; }
    public double ElasticModulus { get; set; } // MPa
    public double MaxDeflection { get; set; } // mm
    public double DeflectionLimit { get; set; } // mm
}

/// <summary>胶类计算结果</summary>
public class SealantCalcResult
{
    public double MaxShearStress { get; set; }   // 最大剪应力 MPa
    public double AllowableStress { get; set; }   // 允许应力 MPa
    public double StressRatio { get; set; }       // 应力比
    public bool Passed { get; set; }
    public double MaxDeflection { get; set; }      // 最大位移 mm
}

/// <summary>压板计算结果</summary>
public class PressurePlateResult
{
    public double BendingStress { get; set; }     // 弯曲应力 MPa
    public double ShearStress { get; set; }       // 剪应力 MPa
    public double AllowableStress { get; set; }   // 允许应力 MPa
    public bool Passed { get; set; }
}

/// <summary>副框计算结果</summary>
public class SubFrameResult
{
    public double MaxStress { get; set; }         // 最大应力 MPa
    public double AllowableStress { get; set; }    // 允许应力 MPa
    public double MaxDeflection { get; set; }     // 最大挠度 mm
    public double DeflectionLimit { get; set; }   // 挠度限值 mm
    public bool Passed { get; set; }
}

/// <summary>承托件计算结果</summary>
public class BracketResult
{
    public double MaxStress { get; set; }          // 最大应力 MPa
    public double AllowableStress { get; set; }    // 允许应力 MPa
    public bool Passed { get; set; }
}

public class MemberCalcResult
{
    public string ProfileCode { get; set; } = "";
    public string SupportType { get; set; } = "简支梁";
    public double Span { get; set; }
    public double Ix { get; set; }  // cm⁴
    public double Iy { get; set; }  // cm⁴（横梁需要）
    public double Wx { get; set; }  // cm³ (Wnx1)
    public double Wnx2 { get; set; } // cm³ (下翼缘抵抗矩)
    public double Wy { get; set; }  // cm³ (Wny)
    public double Area { get; set; } // mm²
    public double LoadWidth { get; set; } // mm
    public double MaxStress { get; set; } // MPa
    public double AllowableStress { get; set; } // MPa
    public double ShearStrength { get; set; } = 49; // 剪切强度设计值fv (MPa)
    public double MaxDeflection { get; set; } // mm
    public double DeflectionLimit { get; set; } // mm
    /// <summary>线密度γg (kg/m)</summary>
    public double LinearDensity { get; set; }
    /// <summary>腹板厚度t (mm)</summary>
    public double WebThickness { get; set; }
    /// <summary>面积矩Sx (cm³)</summary>
    public double Sx { get; set; }
    /// <summary>塑性发展系数γ</summary>
    public double PlasticFactor { get; set; } = 1.0;
    /// <summary>弹性模量E (MPa)</summary>
    public double ElasticModulus { get; set; } = 70000;
    /// <summary>强度设计值fa (MPa)</summary>
    public double DesignStrength { get; set; } = 85;
}

#endregion
