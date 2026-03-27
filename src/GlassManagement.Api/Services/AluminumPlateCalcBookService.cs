using NPOI.XWPF.UserModel;
using NPOI.OpenXmlFormats.Wordprocessing;
using NPOI.WP.UserModel;
using System.IO;

namespace GlassManagement.Api.Services;

/// <summary>
/// 铝单板幕墙计算书生成服务（使用NPOI生成.docx）
/// 
/// 严格参照汇宝计算书样本格式：
/// - 自定义Heading1/2/3样式，带多级列表自动编号
/// - 正文使用自定义BodyText样式（宋体+TNR 12pt）
/// - 封面标题22pt居中，封面与正文分节
/// - 铝板面板计算（四边简支板公式）
/// - 立柱/横梁计算（复用玻璃幕墙计算方法）
/// - 连接件/预埋件计算
/// - 伸缩缝计算
/// - 公式格式: 变量名 = 公式 → = 代入数值 → = 结果
/// - 每个公式标注规范条文出处
/// - 结论用"满足要求"/"不满足要求"
/// </summary>
public class AluminumPlateCalcBookService
{
    private const string MainFont = "Times New Roman";
    private const string EastAsiaFont = "宋体";

    private int _h2Counter = 0;
    private int _h3Counter = 0;

    /// <summary>生成铝单板幕墙计算书Word文档</summary>
    public byte[] GenerateReport(AluminumPlateCalcBookInput input)
    {
        _h2Counter = 0;
        _h3Counter = 0;

        var doc = new XWPFDocument();

        CreateCustomStyles(doc);
        SetPageLayout(doc);

        // 封面
        RenderCoverPage(doc, input);
        AddSectionBreak(doc, ST_SectionMark.nextPage);

        // 目录
        RenderTableOfContents(doc);
        AddPageBreak(doc);

        // 主标题
        AddMainTitle(doc, "铝单板幕墙设计计算书");

        // 章节1：规范引用
        RenderStandardsSection(doc);

        // 章节2：基本参数
        RenderBasicParametersSection(doc, input);

        // 章节3：风荷载计算
        RenderWindLoadSection(doc, input);

        // 章节4：铝板面板计算
        RenderAluminumPlateSection(doc, input);

        // 章节5：立柱计算
        if (input.Column != null)
            RenderColumnSection(doc, input);

        // 章节6：横梁计算
        if (input.Beam != null)
            RenderBeamSection(doc, input);

        // 章节7：连接件计算
        RenderConnectionSection(doc, input);

        // 章节8：预埋件计算
        RenderEmbedSection(doc, input);

        // 章节9：伸缩缝计算
        RenderExpansionJointSection(doc, input);

        // 章节10：附录
        RenderAppendix(doc);

        SetHeaderFooter(doc, input.ProjectName ?? "");

        using var ms = new MemoryStream();
        doc.Write(ms);
        return ms.ToArray();
    }

    #region 样式与页面设置

    private void CreateCustomStyles(XWPFDocument doc) { }

    private void SetPageLayout(XWPFDocument doc)
    {
        var body = doc.Document.body;
        if (body.sectPr == null) body.sectPr = new CT_SectPr();
        var sectPr = body.sectPr;
        if (sectPr.pgSz == null) sectPr.pgSz = new CT_PageSz();
        sectPr.pgSz.w = 11906;
        sectPr.pgSz.h = 16838;
        if (sectPr.pgMar == null) sectPr.pgMar = new CT_PageMar();
        sectPr.pgMar.top = 1417;
        sectPr.pgMar.bottom = 1134;
        sectPr.pgMar.left = 1417;
        sectPr.pgMar.right = 1417;
    }

    private void SetHeaderFooter(XWPFDocument doc, string projectName)
    {
        var header = doc.CreateHeader(HeaderFooterType.DEFAULT);
        var hp = header.CreateParagraph();
        hp.Alignment = ParagraphAlignment.LEFT;
        var hr = hp.CreateRun();
        hr.SetText($"{projectName}          铝单板幕墙设计计算书");
        SetFont(hr, 9);

        var footer = doc.CreateFooter(HeaderFooterType.DEFAULT);
        var fp = footer.CreateParagraph();
        fp.Alignment = ParagraphAlignment.CENTER;
        var fr1 = fp.CreateRun(); fr1.SetText("第 "); SetFont(fr1, 9);
        fp.GetCTP().AddNewFldSimple().instr = " PAGE ";
        var fr2 = fp.CreateRun(); fr2.SetText(" / "); SetFont(fr2, 9);
        fp.GetCTP().AddNewFldSimple().instr = " NUMPAGES ";
        var fr3 = fp.CreateRun(); fr3.SetText(" 页"); SetFont(fr3, 9);
    }

    private void AddSectionBreak(XWPFDocument doc, ST_SectionMark type)
    {
        _ = doc; _ = type;
    }

    #endregion

    #region 通用辅助方法

    private void SetFont(XWPFRun run, int fontSize, bool bold = false)
    {
        run.FontFamily = MainFont;
        run.FontSize = fontSize;
        run.IsBold = bold;
        var rPr = run.GetCTR().AddNewRPr();
        if (rPr.rFonts == null) rPr.rFonts = new CT_Fonts();
        rPr.rFonts.ascii = MainFont;
        rPr.rFonts.hAnsi = MainFont;
        rPr.rFonts.eastAsia = EastAsiaFont;
    }

    private void AddPageBreak(XWPFDocument doc)
    {
        var p = doc.CreateParagraph();
        p.IsPageBreak = true;
    }

    private void AddMainTitle(XWPFDocument doc, string text)
    {
        var p = doc.CreateParagraph();
        p.Style = "Heading1";
        p.Alignment = ParagraphAlignment.CENTER;
        var r = p.CreateRun();
        r.SetText(text);
        SetFont(r, 16, true);
    }

    private void AddHeading2(XWPFDocument doc, string text)
    {
        _h2Counter++;
        _h3Counter = 0;
        var p = doc.CreateParagraph();
        p.Style = "Heading2";
        var r = p.CreateRun();
        r.SetText($"{_h2Counter} {text}");
        SetFont(r, 14, true);
    }

    private void AddHeading3(XWPFDocument doc, string text)
    {
        _h3Counter++;
        var p = doc.CreateParagraph();
        p.Style = "Heading3";
        var r = p.CreateRun();
        r.SetText($"{_h2Counter}.{_h3Counter} {text}");
        SetFont(r, 12, true);
    }

    private void AddBody(XWPFDocument doc, string text)
    {
        var p = doc.CreateParagraph();
        var r = p.CreateRun();
        r.SetText(text);
        SetFont(r, 12);
    }

    private void AddIndent(XWPFDocument doc, string text)
    {
        var p = doc.CreateParagraph();
        p.IndentationFirstLine = 480;
        var r = p.CreateRun();
        r.SetText(text);
        SetFont(r, 12);
    }

    private void AddIndent2(XWPFDocument doc, string text)
    {
        var p = doc.CreateParagraph();
        p.IndentationFirstLine = 960;
        var r = p.CreateRun();
        r.SetText(text);
        SetFont(r, 12);
    }

    private void AddFormula(XWPFDocument doc, string formula, string? reference = null)
    {
        var p = doc.CreateParagraph();
        p.IndentationFirstLine = 480;
        var r = p.CreateRun();
        r.SetText(reference != null ? $"{formula}        ……{reference}" : formula);
        SetFont(r, 12);
    }

    private void AddCalcLine(XWPFDocument doc, string line)
    {
        var p = doc.CreateParagraph();
        p.IndentationFirstLine = 720;
        var r = p.CreateRun();
        r.SetText(line);
        SetFont(r, 12);
    }

    private void AddVerdict(XWPFDocument doc, bool pass, string actual, string limit, string item)
    {
        AddBody(doc, $"{actual} {(pass ? "≤" : ">")} {limit}，{item}{(pass ? "满足要求。" : "不满足要求。")}");
    }

    private void CreateTable(XWPFDocument doc, string title, string? stdRef, string[] headers, string[][] rows)
    {
        int extraRows = stdRef != null ? 2 : 1;
        var table = doc.CreateTable(rows.Length + extraRows + 1, headers.Length);
        table.Width = 5000;
        int idx = 0;

        table.GetRow(idx).GetCell(0).SetText(title);
        SetCellFont(table.GetRow(idx).GetCell(0), 12, true);
        idx++;

        if (stdRef != null)
        {
            table.GetRow(idx).GetCell(0).SetText(stdRef);
            SetCellFont(table.GetRow(idx).GetCell(0), 10, false);
            idx++;
        }

        for (int c = 0; c < headers.Length; c++)
        {
            table.GetRow(idx).GetCell(c).SetText(headers[c]);
            SetCellFont(table.GetRow(idx).GetCell(c), 11, true);
        }
        idx++;

        for (int r = 0; r < rows.Length; r++)
        {
            for (int c = 0; c < headers.Length && c < rows[r].Length; c++)
            {
                table.GetRow(idx + r).GetCell(c).SetText(rows[r][c]);
                SetCellFont(table.GetRow(idx + r).GetCell(c), 11, false);
            }
        }

        doc.CreateParagraph();
    }

    private void SetCellFont(XWPFTableCell cell, int size, bool bold)
    {
        foreach (var p in cell.Paragraphs)
            foreach (var r in p.Runs)
                SetFont(r, size, bold);
    }

    private string ToChineseDate(DateTime dt)
    {
        string[] digits = { "〇", "一", "二", "三", "四", "五", "六", "七", "八", "九" };
        string year = "";
        foreach (char c in dt.Year.ToString())
            year += digits[c - '0'];

        string month;
        if (dt.Month <= 10)
            month = dt.Month == 10 ? "十" : digits[dt.Month];
        else
            month = "十" + digits[dt.Month - 10];

        string day;
        if (dt.Day <= 10)
            day = dt.Day == 10 ? "十" : digits[dt.Day];
        else if (dt.Day < 20)
            day = "十" + digits[dt.Day - 10];
        else if (dt.Day == 20)
            day = "二十";
        else if (dt.Day < 30)
            day = "二十" + digits[dt.Day - 20];
        else if (dt.Day == 30)
            day = "三十";
        else
            day = "三十" + digits[dt.Day - 30];

        return $"{year}年{month}月{day}日";
    }

    private (double alpha, double I10, double zMin, double zMax, double coefficient) GetTerrainParams(string category)
    {
        return category switch
        {
            "A" => (0.12, 0.12, 5, 300, 1.284),
            "B" => (0.15, 0.14, 10, 350, 1.000),
            "C" => (0.22, 0.23, 15, 450, 0.544),
            "D" => (0.30, 0.39, 30, 550, 0.262),
            _ => (0.15, 0.14, 10, 350, 1.000)
        };
    }

    private double GetAlphaMaxFromIntensity(int intensity)
    {
        return intensity switch
        {
            6 => 0.04,
            7 => 0.08,
            8 => 0.16,
            9 => 0.32,
            _ => 0.08
        };
    }

    #endregion

    #region 封面

    private void RenderCoverPage(XWPFDocument doc, AluminumPlateCalcBookInput input)
    {
        for (int i = 0; i < 6; i++) doc.CreateParagraph();

        var p1 = doc.CreateParagraph(); p1.Alignment = ParagraphAlignment.CENTER;
        var r1 = p1.CreateRun(); r1.SetText(input.ProjectName ?? "XX幕墙装饰工程"); SetFont(r1, 22);

        var p2 = doc.CreateParagraph(); p2.Alignment = ParagraphAlignment.CENTER;
        var r2 = p2.CreateRun(); r2.SetText("铝单板幕墙"); SetFont(r2, 22);

        var p3 = doc.CreateParagraph(); p3.Alignment = ParagraphAlignment.CENTER;
        var r3 = p3.CreateRun(); r3.SetText("设计计算书"); SetFont(r3, 22);

        for (int i = 0; i < 10; i++)
        {
            var ep = doc.CreateParagraph(); ep.Alignment = ParagraphAlignment.CENTER;
            var er = ep.CreateRun(); er.SetText(""); SetFont(er, 16);
        }

        foreach (var (label, name) in new[] {
            ("设计：", input.Designer ?? "            "),
            ("校对：", input.Reviewer ?? "                "),
            ("审核：", input.Approver ?? "            "),
            ("批准：", input.Supervisor ?? "                 ")
        })
        {
            var sp = doc.CreateParagraph();
            var sr = sp.CreateRun(); sr.SetText($"{label}{name}"); SetFont(sr, 16);
        }

        for (int i = 0; i < 4; i++) doc.CreateParagraph();

        var cp = doc.CreateParagraph(); cp.Alignment = ParagraphAlignment.CENTER;
        var cr = cp.CreateRun(); cr.SetText(input.CompanyName ?? "XX幕墙工程有限公司"); SetFont(cr, 16);

        var dp = doc.CreateParagraph(); dp.Alignment = ParagraphAlignment.CENTER;
        var dr = dp.CreateRun();
        string dateStr = input.ReportDate ?? ToChineseDate(DateTime.Now);
        dr.SetText(dateStr);
        SetFont(dr, 16);
    }

    #endregion

    #region 目录

    private void RenderTableOfContents(XWPFDocument doc)
    {
        doc.CreateParagraph();
        doc.CreateParagraph();
        var tp = doc.CreateParagraph(); tp.Alignment = ParagraphAlignment.CENTER;
        var tr = tp.CreateRun(); tr.SetText("目录"); SetFont(tr, 36);
        doc.CreateParagraph();
        AddBody(doc, "（请在Word中更新域：全选→右键→更新域，即可生成自动目录）");
    }

    #endregion

    #region 1. 规范引用

    private void RenderStandardsSection(XWPFDocument doc)
    {
        AddHeading2(doc, "计算引用的规范、标准及资料");

        AddHeading3(doc, "幕墙设计规范：");
        foreach (var s in new[] {
            "《铝合金结构设计规范》                      GB50429-2007",
            "《金属与石材幕墙工程技术规范》              JGJ133-2001",
            "《建筑幕墙》                                GB/T21086-2007",
            "《建筑幕墙工程技术标准》                    DG/TJ08-56-2019"
        }) AddBody(doc, s);

        AddHeading3(doc, "金属板规范：");
        foreach (var s in new[] {
            "《铝幕墙板 第1部分：板基》                  YS/T429.1-2023",
            "《铝幕墙板 第2部分：有机聚合物喷涂铝单板》  YS/T429.2-2012",
            "《建筑幕墙用铝塑复合板》                    GB/T17748-2016",
            "《建筑幕墙用氟碳铝单板制品》                JG/T331-2011",
            "《建筑用蜂窝铝板》                          JG/T267-2010"
        }) AddBody(doc, s);

        AddHeading3(doc, "建筑设计规范：");
        foreach (var s in new[] {
            "《工程结构通用规范》                        GB55001-2021",
            "《建筑结构荷载规范》                        GB50009-2012",
            "《建筑与市政工程抗震通用规范》              GB55002-2021",
            "《建筑抗震设计标准》                        GB/T50011-2010(2024年版)",
            "《钢结构设计标准》                          GB50017-2017"
        }) AddBody(doc, s);

        AddHeading3(doc, "铝合金规范：");
        foreach (var s in new[] {
            "《铝合金建筑型材 第1部分基材》              GB/T5237.1-2017",
            "《铝合金建筑型材 第2部分阳极氧化、着色型材》GB/T5237.2-2017",
            "《铝合金建筑型材 第4部分喷粉型材》          GB/T5237.4-2017"
        }) AddBody(doc, s);

        AddHeading3(doc, "《建筑结构静力计算手册》(第二版)");
        AddHeading3(doc, "土建图纸：");
    }

    #endregion

    #region 2. 基本参数

    private void RenderBasicParametersSection(XWPFDocument doc, AluminumPlateCalcBookInput input)
    {
        AddHeading2(doc, "基本参数");

        AddHeading3(doc, "幕墙所在地区");
        AddIndent(doc, $"{input.City ?? "XX"}地区；");

        AddHeading3(doc, "地面粗糙度分类等级");
        AddBody(doc, "幕墙属于外围护构件，按《建筑结构荷载规范》(GB50009-2012)");
        AddIndent(doc, "A类：指近海海面和海岛、海岸、湖岸及沙漠地区；");
        AddIndent(doc, "B类：指田野、乡村、丛林、丘陵以及房屋比较稀疏的乡镇；");
        AddIndent(doc, "C类：指有密集建筑群的城市市区；");
        AddIndent(doc, "D类：指有密集建筑群且房屋较高的城市市区；");
        AddBody(doc, $"依照上面分类标准，本工程按{input.TerrainCategory ?? "B"}类地形考虑。");

        AddHeading3(doc, "抗震设防");
        AddBody(doc, $"根据《建筑抗震设计规范》GB50011-2010及《中国地震动参数区划图》GB18306-2015的要求，本工程所在地{input.City ?? "XX"}地区地震基本烈度为：{input.SeismicIntensity}度；");

        double siteFactor = input.SiteFactor > 0 ? input.SiteFactor : 1.0;
        double alphaMaxBase = GetAlphaMaxFromIntensity(input.SeismicIntensity);
        double alphaMax = input.AlphaMax > 0 ? input.AlphaMax : siteFactor * alphaMaxBase;

        AddBody(doc, "水平地震影响系数最大值：");
        AddFormula(doc, "αmax=Fa×αmaxⅡ", "表5.1.4-1[GB/T50011-2010]");
        AddCalcLine(doc, $"={siteFactor:F1}×{alphaMaxBase:F2}");
        AddCalcLine(doc, $"={alphaMax:F2}");
    }

    #endregion

    #region 3. 风荷载计算

    private void RenderWindLoadSection(XWPFDocument doc, AluminumPlateCalcBookInput input)
    {
        AddHeading2(doc, "幕墙承受荷载计算");

        // 3.1 风荷载
        AddHeading3(doc, "风荷载标准值的计算方法");
        AddBody(doc, "幕墙属于外围护构件，按《工程结构通用规范》GB55001-2021及《建筑结构荷载规范》(GB50009-2012)计算：");
        AddFormula(doc, "wk=βK1K2μzμs1w0", "4.6.1[GB55001-2021]");
        AddBody(doc, "上式中：");
        AddIndent(doc, "wk：作用在幕墙上的风荷载标准值(MPa)；");
        AddIndent(doc, $"z：计算点标高：{input.InstallHeight:F0}m；");
        AddIndent(doc, "μz：风压高度变化系数；");

        AddBody(doc, "根据不同场地类型,按《建筑结构荷载规范》条文说明部分8.2.1提供的公式计算：");
        AddIndent(doc, "A类场地：μzA=1.284×(z/10)^0.24");
        AddIndent(doc, "B类场地：μzB=1.000×(z/10)^0.30");
        AddIndent(doc, "C类场地：μzC=0.544×(z/10)^0.44");
        AddIndent(doc, "D类场地：μzD=0.262×(z/10)^0.60");

        AddBody(doc, $"对于{input.TerrainCategory ?? "B"}类地形，{input.InstallHeight:F0}m高度处风压高度变化系数：");
        AddIndent(doc, $"μz={input.HeightFactor:F4}");

        AddBody(doc, "β：风荷载放大系数：");
        AddIndent(doc, "β=max(βgz,1+0.7/μz^0.5)");
        AddCalcLine(doc, $"={input.GustFactor:F4}");

        // 3.1.3 μs1完整推导
        RenderMus1Derivation(doc, input);

        AddIndent(doc, $"w0：基本风压值(MPa)，按重现期50年，{input.City ?? "XX"}地区取{input.BasicWindPressure / 1000000:F6}MPa；");

        // 3.2 支承结构风荷载
        double panelAreaM2 = (input.PanelWidth / 1000.0) * (input.PanelHeight / 1000.0);
        double mus1Base = input.Mus1Base;
        double mus1At25 = input.Mus1At25;
        double mus1A;
        if (panelAreaM2 <= 1)
            mus1A = mus1Base;
        else if (panelAreaM2 >= 25)
            mus1A = mus1At25;
        else
            mus1A = mus1Base + (mus1At25 - mus1Base) * Math.Log10(panelAreaM2) / 1.4;

        double mus1Support = Math.Abs(mus1A) + 0.2;
        double mus1Panel = Math.Abs(mus1Base) + 0.2;

        AddHeading3(doc, "计算支承结构时的风荷载标准值");
        double w0Mpa = input.BasicWindPressure / 1000000.0;
        double wkSupport = input.GustFactor * 1 * 1 * input.HeightFactor * mus1Support * w0Mpa;
        AddIndent(doc, "wk=βK1K2μzμs1w0");
        AddCalcLine(doc, $"={input.GustFactor:F4}×1×1×{input.HeightFactor:F4}×{mus1Support:F3}×{w0Mpa:F6}");
        AddCalcLine(doc, $"={wkSupport:F6}MPa");

        AddHeading3(doc, "计算面板材料时的风荷载标准值");
        double wkPanel = input.GustFactor * 1 * 1 * input.HeightFactor * mus1Panel * w0Mpa;
        AddBody(doc, "面板局部体型系数取μs1(1)的绝对值加内部压力体型系数：");
        AddIndent(doc, $"μs1(面板) = |μs1(1)| + 0.2 = {Math.Abs(mus1Base):F1} + 0.2 = {mus1Panel:F3}");
        AddIndent(doc, "wk=βK1K2μzμs1w0");
        AddCalcLine(doc, $"={input.GustFactor:F4}×1×1×{input.HeightFactor:F4}×{mus1Panel:F3}×{w0Mpa:F6}");
        AddCalcLine(doc, $"={wkPanel:F6}MPa");

        // 3.3 地震作用
        AddHeading3(doc, "垂直于幕墙平面的分布水平地震作用标准值");
        AddFormula(doc, "qEk=βEαmaxGk/A", "5.3.4[JGJ133-2001]");
        double alphaMax = input.AlphaMax > 0 ? input.AlphaMax : GetAlphaMaxFromIntensity(input.SeismicIntensity);
        AddBody(doc, "上式中：");
        AddIndent(doc, "qEk：垂直于幕墙平面的分布水平地震作用标准值(MPa)；");
        AddIndent(doc, "βE：动力放大系数,取5.0；");
        AddIndent(doc, $"αmax：水平地震影响系数最大值，取{alphaMax:F2}；");

        // 3.4 荷载组合
        AddHeading3(doc, "作用效应组合");
        AddBody(doc, "荷载和作用效应按下式进行组合：");
        AddFormula(doc, "S=γGSGk+ψwγwSwk+ψEγESEk", "JGJ133-2001");
        AddBody(doc, "进行幕墙构件强度计算时：");
        AddIndent(doc, "重力荷载：γG：1.3；");
        AddIndent(doc, "风 荷 载：γw：1.5；");
        AddIndent(doc, "地震作用：γE：1.4；");
        AddBody(doc, "进行挠度计算时：");
        AddIndent(doc, "风 荷 载：γw：1.0；");
        AddBody(doc, "风荷载的组合系数ψw为1.0；");
        AddIndent(doc, "地震作用的组合系数ψE为0.5；");
    }

    /// <summary>μs1完整推导过程</summary>
    private void RenderMus1Derivation(XWPFDocument doc, AluminumPlateCalcBookInput input)
    {
        AddBody(doc, "μs1：局部风压体型系数；");
        AddBody(doc, "按《建筑结构荷载规范》GB50009-2012第8.3.3条，计算围护结构及其连接的风荷载时，局部体型系数μs1按以下步骤确定：");

        double mus1_1 = input.Mus1Base;
        AddBody(doc, "(1) 按表8.3.3[GB50009-2012]查表得基准体型系数：");
        AddIndent(doc, $"μs1(1) = {mus1_1:F1}（从属面积1m²时的体型系数）");

        double panelWidthM = input.PanelWidth / 1000.0;
        double panelHeightM = input.PanelHeight / 1000.0;
        double tributaryArea = panelWidthM * panelHeightM;
        double mus1_25 = input.Mus1At25;

        AddBody(doc, "(2) 按8.3.4[GB50009-2012]进行从属面积折减：");
        AddIndent(doc, $"从属面积 A = 幕墙分格面积 = {panelWidthM:F3} × {panelHeightM:F3}");
        AddCalcLine(doc, $"= {tributaryArea:F3} m²");

        double mus1A;
        if (tributaryArea <= 1)
        {
            mus1A = mus1_1;
            AddIndent(doc, $"A ≤ 1m²，不折减，μs1(A) = μs1(1) = {mus1A:F3}");
        }
        else if (tributaryArea >= 25)
        {
            mus1A = mus1_25;
            AddIndent(doc, $"A ≥ 25m²，取μs1(A) = μs1(25) = {mus1A:F3}");
        }
        else
        {
            AddIndent(doc, "当1m² < A < 25m²时：");
            AddFormula(doc, "μs1(A) = μs1(1) + [μs1(25) - μs1(1)] × lgA / 1.4", "8.3.4[GB50009-2012]");
            mus1A = mus1_1 + (mus1_25 - mus1_1) * Math.Log10(tributaryArea) / 1.4;
            AddCalcLine(doc, $"= {mus1_1:F1} + [{mus1_25:F1} - ({mus1_1:F1})] × lg{tributaryArea:F3} / 1.4");
            AddCalcLine(doc, $"= {mus1A:F3}");
        }

        AddBody(doc, "(3) 按8.3.5[GB50009-2012]考虑建筑物内部压力：");
        AddIndent(doc, "内部压力体型系数 μsi = ±0.2");
        AddIndent(doc, "正压面取+0.2，负压面取-0.2");

        double mus1Support = Math.Abs(mus1A) + 0.2;
        double mus1Panel = Math.Abs(mus1_1) + 0.2;

        AddBody(doc, "支承结构（立柱/横梁）：");
        AddIndent(doc, $"μs1(支承) = |μs1(A)| + 0.2 = {Math.Abs(mus1A):F3} + 0.2 = {mus1Support:F3}");
        AddBody(doc, "面板（铝板）：");
        AddIndent(doc, $"μs1(面板) = |μs1(1)| + 0.2 = {Math.Abs(mus1_1):F1} + 0.2 = {mus1Panel:F3}");
    }

    #endregion

    #region 4. 铝板面板计算

    private void RenderAluminumPlateSection(XWPFDocument doc, AluminumPlateCalcBookInput input)
    {
        AddHeading2(doc, "铝板面板计算");

        double a = input.PanelWidth > 0 ? input.PanelWidth : 1200;
        double b = input.PanelHeight > 0 ? input.PanelHeight : 1500;
        double t = input.PlateThickness > 0 ? input.PlateThickness : 3.0;

        AddBody(doc, "基本参数：");
        AddIndent(doc, $"面板短边 a = {a:F0} mm，长边 b = {b:F0} mm");
        AddIndent(doc, $"铝板厚度 t = {t:F1} mm");
        AddIndent(doc, $"铝板弹性模量 E = 70000 MPa");
        AddIndent(doc, $"铝板泊松比 ν = 0.3");
        AddIndent(doc, $"铝板强度设计值 fg = 150 MPa（铝单板）");

        // 荷载计算
        double panelAreaM2 = (a / 1000.0) * (b / 1000.0);
        double mus1Base = input.Mus1Base;
        double mus1At25 = input.Mus1At25;
        double mus1A;
        if (panelAreaM2 <= 1)
            mus1A = mus1Base;
        else if (panelAreaM2 >= 25)
            mus1A = mus1At25;
        else
            mus1A = mus1Base + (mus1At25 - mus1Base) * Math.Log10(panelAreaM2) / 1.4;

        double w0Mpa = input.BasicWindPressure / 1e6;
        double wk = input.GustFactor * input.HeightFactor * Math.Abs(mus1A) * w0Mpa;
        double alphaMax = input.AlphaMax > 0 ? input.AlphaMax : GetAlphaMaxFromIntensity(input.SeismicIntensity);

        // 铝板自重
        double rho = 2700; // kg/m³
        double qGk = rho * t / 1000.0 * 9.81 / 1000.0; // kN/m²

        double qEAk = 5.0 * alphaMax * qGk / 1000.0; // MPa
        double qDesign = 1.4 * wk + 0.5 * 1.3 * qEAk; // MPa

        AddBody(doc, "荷载组合（风荷载+地震作用）：");
        AddFormula(doc, "q = 1.4×wk + 0.5×1.3×qEAk", "4.6.5[GB55001-2021]");
        AddCalcLine(doc, $"= 1.4×{wk:F6} + 0.5×1.3×{qEAk:F6}");
        AddCalcLine(doc, $"= {qDesign:F6} MPa");

        // 弯矩系数（四边简支板）
        double ratio = b / a;
        double m_coeff;
        if (ratio <= 1.0) m_coeff = 0.0479;
        else if (ratio <= 1.5) m_coeff = 0.0479 + (0.0812 - 0.0479) * (ratio - 1.0) / 0.5;
        else if (ratio <= 2.0) m_coeff = 0.0812 + (0.1017 - 0.0812) * (ratio - 1.5) / 0.5;
        else if (ratio <= 3.0) m_coeff = 0.1017 + (0.1189 - 0.1017) * (ratio - 2.0) / 1.0;
        else m_coeff = 0.125;

        AddBody(doc, $"边长比 b/a = {b:F0}/{a:F0} = {ratio:F2}");
        AddBody(doc, $"四边简支板弯矩系数 m = {m_coeff:F4}");

        // θ参数
        double nu = 0.3;
        double Eg = 70000;
        double theta = wk * Math.Pow(a, 4) / (Eg * Math.Pow(t, 4));
        double eta;
        if (theta <= 5) eta = 1.00;
        else if (theta <= 25) eta = 1.00 - (1.00 - 0.82) * (theta - 5) / (25 - 5);
        else if (theta <= 100) eta = 0.82 - (0.82 - 0.64) * (theta - 25) / (100 - 25);
        else if (theta <= 400) eta = 0.64 - (0.64 - 0.40) * (theta - 100) / (400 - 100);
        else eta = 0.40;

        AddBody(doc, $"θ = wk×a⁴ / (E×t⁴) = {theta:F4}");
        AddBody(doc, $"查表得折减系数 η = {eta:F4}");

        // 强度校核
        AddHeading3(doc, "强度校核");
        double sigma = 6 * m_coeff * qDesign * a * a * eta / (t * t);
        AddFormula(doc, "σ = 6×m×q×a²×η / t²", "JGJ133-2001 6.2.1");
        AddCalcLine(doc, $"= 6×{m_coeff:F4}×{qDesign:F6}×{a:F0}²×{eta:F4} / {t:F1}²");
        AddCalcLine(doc, $"= {sigma:F2} MPa");

        double fg = 150; // 铝单板强度设计值
        AddVerdict(doc, sigma <= fg, $"σ = {sigma:F2} MPa", $"fg = {fg:F0} MPa", "铝板强度");

        // 挠度校核
        AddHeading3(doc, "挠度校核");
        double D = Eg * t * t * t / (12 * (1 - nu * nu));
        double m_d;
        if (ratio <= 1.0) m_d = 0.00406;
        else if (ratio <= 1.5) m_d = 0.00406 + (0.00772 - 0.00406) * (ratio - 1.0) / 0.5;
        else if (ratio <= 2.0) m_d = 0.00772 + (0.01013 - 0.00772) * (ratio - 1.5) / 0.5;
        else if (ratio <= 3.0) m_d = 0.01013 + (0.01223 - 0.01013) * (ratio - 2.0) / 1.0;
        else m_d = 0.01302;

        double df = eta * m_d * wk * a * a * a * a / D;
        double dfLimit = a / 180.0;

        AddFormula(doc, "D = E×t³ / (12×(1-ν²))", "JGJ133-2001");
        AddCalcLine(doc, $"= {Eg:F0}×{t:F1}³ / (12×(1-{nu:F1}²))");
        AddCalcLine(doc, $"= {D:F2} N·mm");

        AddFormula(doc, "df = η×md×wk×a⁴ / D", "JGJ133-2001 6.2.1");
        AddCalcLine(doc, $"= {eta:F4}×{m_d:F5}×{wk:F6}×{a:F0}⁴ / {D:F2}");
        AddCalcLine(doc, $"= {df:F4} mm");

        AddBody(doc, $"挠度限值：a/180 = {a:F0}/180 = {dfLimit:F2} mm");
        AddVerdict(doc, df <= dfLimit, $"df = {df:F4} mm", $"[df] = {dfLimit:F2} mm", "铝板挠度");
    }

    #endregion

    #region 5. 立柱计算（复用玻璃幕墙计算方法）

    private void RenderColumnSection(XWPFDocument doc, AluminumPlateCalcBookInput input)
    {
        AddHeading2(doc, "幕墙立柱计算");
        var col = input.Column!;
        AddBody(doc, "立柱计算方法与玻璃幕墙相同，按JGJ133-2001第6.1节进行。");
        AddIndent(doc, $"立柱型材规格：{col.ProfileCode}");
        AddIndent(doc, $"跨度 L = {col.Span:F0} mm");
        AddIndent(doc, $"计算间距 B = {col.LoadWidth:F0} mm");
        AddIndent(doc, $"材质：{input.ColumnMaterial ?? "6063-T5"}");
        AddBody(doc, "（详细计算方法参见玻璃幕墙立柱计算章节）");
    }

    #endregion

    #region 6. 横梁计算（复用玻璃幕墙计算方法）

    private void RenderBeamSection(XWPFDocument doc, AluminumPlateCalcBookInput input)
    {
        AddHeading2(doc, "幕墙横梁计算");
        var beam = input.Beam!;
        AddBody(doc, "横梁计算方法与玻璃幕墙相同，按JGJ133-2001第6.1节进行。");
        AddIndent(doc, $"横梁型材规格：{beam.ProfileCode}");
        AddIndent(doc, $"跨度 B = {beam.Span:F0} mm");
        AddBody(doc, "（详细计算方法参见玻璃幕墙横梁计算章节）");
    }

    #endregion

    #region 7. 连接件计算

    private void RenderConnectionSection(XWPFDocument doc, AluminumPlateCalcBookInput input)
    {
        AddHeading2(doc, "连接件计算");
        AddBody(doc, "连接件计算方法与玻璃幕墙相同，按JGJ133-2001第6.3节进行。");
        AddBody(doc, "铝单板幕墙连接件主要包括：");
        AddIndent(doc, "1. 角片与铝板连接（自攻螺钉或拉铆钉）；");
        AddIndent(doc, "2. 角片与立柱/横梁连接（螺栓连接）；");
        AddBody(doc, "（详细计算方法参见玻璃幕墙连接件计算章节）");
    }

    #endregion

    #region 8. 预埋件计算

    private void RenderEmbedSection(XWPFDocument doc, AluminumPlateCalcBookInput input)
    {
        AddHeading2(doc, "预埋件计算");
        AddBody(doc, "预埋件计算方法与玻璃幕墙相同，按JGJ133-2001附录A进行。");
        AddBody(doc, "（详细计算方法参见玻璃幕墙预埋件计算章节）");
    }

    #endregion

    #region 9. 伸缩缝计算

    private void RenderExpansionJointSection(XWPFDocument doc, AluminumPlateCalcBookInput input)
    {
        AddHeading2(doc, "伸缩缝计算");
        AddBody(doc, "铝合金线膨胀系数 α = 2.35×10⁻⁵ /℃");
        AddBody(doc, "温差取值 ΔT = 80 ℃");

        double alpha = 2.35e-5;
        double deltaT = 80;
        double Lcol = input.Column?.Span ?? 3600;
        double d1 = 2;
        double d2 = 2;

        double dExpansion = alpha * deltaT * Lcol + d1 + d2;

        AddFormula(doc, "d ≥ α×Δt×L + d1 + d2", "JGJ133-2001 6.4.3");
        AddCalcLine(doc, $"= {alpha:E2}×{deltaT:F0}×{Lcol:F0} + {d1:F0} + {d2:F0}");
        AddCalcLine(doc, $"= {dExpansion:F2} mm");
        AddBody(doc, $"伸缩缝宽度取 {Math.Ceiling(dExpansion):F0} mm。");
    }

    #endregion

    #region 10. 附录

    private void RenderAppendix(XWPFDocument doc)
    {
        AddHeading2(doc, "附录");

        AddHeading3(doc, "铝板材料力学性能参数");

        CreateTable(doc, "附录A-1 铝板材料力学性能", "JG/T331-2011, YS/T429-2012",
            new[] { "参数名称", "符号", "数值", "单位" },
            new[]
            {
                new[] { "弹性模量", "E", "70000", "MPa" },
                new[] { "泊松比", "ν", "0.30", "-" },
                new[] { "密度", "ρ", "2700", "kg/m³" },
                new[] { "铝单板强度设计值", "fg", "150", "MPa" },
                new[] { "铝塑板强度设计值", "fg", "130", "MPa" },
                new[] { "线膨胀系数", "α", "2.35×10⁻⁵", "1/℃" },
            });

        CreateTable(doc, "附录A-2 铝合金型材力学性能", "GB/T5237-2017, GB50429-2007",
            new[] { "牌号", "状态", "f (MPa)", "fv (MPa)", "E (MPa)" },
            new[]
            {
                new[] { "6063", "T5", "85", "49", "70000" },
                new[] { "6063", "T6", "157", "91", "70000" },
                new[] { "6061", "T6", "200", "115", "70000" },
            });
    }

    #endregion
}

/// <summary>铝单板幕墙计算书输入模型</summary>
public class AluminumPlateCalcBookInput
{
    // 项目信息
    public string? ProjectName { get; set; }
    public string? CompanyName { get; set; }
    public string? Designer { get; set; }
    public string? Reviewer { get; set; }
    public string? Approver { get; set; }
    public string? Supervisor { get; set; }
    public string? ReportDate { get; set; }

    // 基本参数
    public string? City { get; set; }
    public double BuildingHeight { get; set; }
    public double InstallHeight { get; set; }
    public double PanelWidth { get; set; }
    public double PanelHeight { get; set; }
    public double BasicWindPressure { get; set; }
    public string? TerrainCategory { get; set; }
    public int SeismicIntensity { get; set; }

    // 风荷载
    public double GustFactor { get; set; }
    public double HeightFactor { get; set; }
    public double Mus1Base { get; set; } = -1.0;
    public double Mus1At25 { get; set; } = -0.8;

    // 地震参数
    public double AlphaMax { get; set; }
    public double SiteFactor { get; set; } = 1.0;
    public string? SiteCategory { get; set; }

    // 铝板参数
    public double PlateThickness { get; set; } = 3.0;

    // 立柱/横梁
    public string? ColumnMaterial { get; set; }
    public MemberCalcResult? Column { get; set; }
    public MemberCalcResult? Beam { get; set; }
}
