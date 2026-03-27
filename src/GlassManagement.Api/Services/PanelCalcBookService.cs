using NPOI.XWPF.UserModel;
using NPOI.OpenXmlFormats.Wordprocessing;
using NPOI.WP.UserModel;
using System.IO;

namespace GlassManagement.Api.Services;

/// <summary>
/// GRC/UHPC人造板材幕墙计算书生成服务
/// 
/// 依据JGJ336-2016《人造板材幕墙工程技术规范》
/// </summary>
public class PanelCalcBookService
{
    private const string MainFont = "Times New Roman";
    private const string EastAsiaFont = "宋体";
    private int _h2Counter = 0;
    private int _h3Counter = 0;

    public byte[] GenerateReport(PanelCalcBookInput input)
    {
        _h2Counter = 0;
        _h3Counter = 0;

        var doc = new XWPFDocument();
        CreateCustomStyles(doc);
        SetPageLayout(doc);

        RenderCoverPage(doc, input);
        AddSectionBreak(doc, ST_SectionMark.nextPage);
        RenderTableOfContents(doc);
        AddPageBreak(doc);

        AddMainTitle(doc, $"{input.PanelType ?? "GRC"}板幕墙设计计算书");

        RenderStandardsSection(doc);
        RenderBasicParametersSection(doc, input);
        RenderWindLoadSection(doc, input);
        RenderPanelSection(doc, input);

        if (input.Column != null) RenderColumnSection(doc, input);
        if (input.Beam != null) RenderBeamSection(doc, input);

        RenderConnectionSection(doc, input);
        RenderEmbedSection(doc, input);
        RenderAppendix(doc);

        SetHeaderFooter(doc, input.ProjectName ?? "");

        using var ms = new MemoryStream();
        doc.Write(ms);
        return ms.ToArray();
    }

    #region 样式与辅助方法

    private void CreateCustomStyles(XWPFDocument doc) { }
    private void SetPageLayout(XWPFDocument doc)
    {
        var body = doc.Document.body;
        if (body.sectPr == null) body.sectPr = new CT_SectPr();
        var sectPr = body.sectPr;
        if (sectPr.pgSz == null) sectPr.pgSz = new CT_PageSz();
        sectPr.pgSz.w = 11906; sectPr.pgSz.h = 16838;
        if (sectPr.pgMar == null) sectPr.pgMar = new CT_PageMar();
        sectPr.pgMar.top = 1417; sectPr.pgMar.bottom = 1134;
        sectPr.pgMar.left = 1417; sectPr.pgMar.right = 1417;
    }

    private void SetHeaderFooter(XWPFDocument doc, string projectName)
    {
        var header = doc.CreateHeader(HeaderFooterType.DEFAULT);
        var hp = header.CreateParagraph(); hp.Alignment = ParagraphAlignment.LEFT;
        var hr = hp.CreateRun(); hr.SetText($"{projectName}          人造板材幕墙设计计算书"); SetFont(hr, 9);
        var footer = doc.CreateFooter(HeaderFooterType.DEFAULT);
        var fp = footer.CreateParagraph(); fp.Alignment = ParagraphAlignment.CENTER;
        var fr1 = fp.CreateRun(); fr1.SetText("第 "); SetFont(fr1, 9);
        fp.GetCTP().AddNewFldSimple().instr = " PAGE ";
        var fr2 = fp.CreateRun(); fr2.SetText(" / "); SetFont(fr2, 9);
        fp.GetCTP().AddNewFldSimple().instr = " NUMPAGES ";
        var fr3 = fp.CreateRun(); fr3.SetText(" 页"); SetFont(fr3, 9);
    }

    private void AddSectionBreak(XWPFDocument doc, ST_SectionMark type) { _ = doc; _ = type; }

    private void SetFont(XWPFRun run, int fontSize, bool bold = false)
    {
        run.FontFamily = MainFont; run.FontSize = fontSize; run.IsBold = bold;
        var rPr = run.GetCTR().AddNewRPr();
        if (rPr.rFonts == null) rPr.rFonts = new CT_Fonts();
        rPr.rFonts.ascii = MainFont; rPr.rFonts.hAnsi = MainFont; rPr.rFonts.eastAsia = EastAsiaFont;
    }
    private void AddPageBreak(XWPFDocument doc) { var p = doc.CreateParagraph(); p.IsPageBreak = true; }
    private void AddMainTitle(XWPFDocument doc, string text)
    { var p = doc.CreateParagraph(); p.Style = "Heading1"; p.Alignment = ParagraphAlignment.CENTER; var r = p.CreateRun(); r.SetText(text); SetFont(r, 16, true); }
    private void AddHeading2(XWPFDocument doc, string text)
    { _h2Counter++; _h3Counter = 0; var p = doc.CreateParagraph(); p.Style = "Heading2"; var r = p.CreateRun(); r.SetText($"{_h2Counter} {text}"); SetFont(r, 14, true); }
    private void AddHeading3(XWPFDocument doc, string text)
    { _h3Counter++; var p = doc.CreateParagraph(); p.Style = "Heading3"; var r = p.CreateRun(); r.SetText($"{_h2Counter}.{_h3Counter} {text}"); SetFont(r, 12, true); }
    private void AddBody(XWPFDocument doc, string text) { var p = doc.CreateParagraph(); var r = p.CreateRun(); r.SetText(text); SetFont(r, 12); }
    private void AddIndent(XWPFDocument doc, string text) { var p = doc.CreateParagraph(); p.IndentationFirstLine = 480; var r = p.CreateRun(); r.SetText(text); SetFont(r, 12); }
    private void AddFormula(XWPFDocument doc, string formula, string? reference = null)
    { var p = doc.CreateParagraph(); p.IndentationFirstLine = 480; var r = p.CreateRun(); r.SetText(reference != null ? $"{formula}        ……{reference}" : formula); SetFont(r, 12); }
    private void AddCalcLine(XWPFDocument doc, string line) { var p = doc.CreateParagraph(); p.IndentationFirstLine = 720; var r = p.CreateRun(); r.SetText(line); SetFont(r, 12); }
    private void AddVerdict(XWPFDocument doc, bool pass, string actual, string limit, string item)
    { AddBody(doc, $"{actual} {(pass ? "≤" : ">")} {limit}，{item}{(pass ? "满足要求。" : "不满足要求。")}"); }

    private string ToChineseDate(DateTime dt)
    {
        string[] d = { "〇", "一", "二", "三", "四", "五", "六", "七", "八", "九" };
        string year = ""; foreach (char c in dt.Year.ToString()) year += d[c - '0'];
        string month = dt.Month <= 10 ? (dt.Month == 10 ? "十" : d[dt.Month]) : "十" + d[dt.Month - 10];
        string day;
        if (dt.Day <= 10) day = dt.Day == 10 ? "十" : d[dt.Day];
        else if (dt.Day < 20) day = "十" + d[dt.Day - 10];
        else if (dt.Day == 20) day = "二十";
        else if (dt.Day < 30) day = "二十" + d[dt.Day - 20];
        else if (dt.Day == 30) day = "三十";
        else day = "三十" + d[dt.Day - 30];
        return $"{year}年{month}月{day}日";
    }

    #endregion

    private void RenderCoverPage(XWPFDocument doc, PanelCalcBookInput input)
    {
        for (int i = 0; i < 6; i++) doc.CreateParagraph();
        var p1 = doc.CreateParagraph(); p1.Alignment = ParagraphAlignment.CENTER;
        var r1 = p1.CreateRun(); r1.SetText(input.ProjectName ?? "XX幕墙装饰工程"); SetFont(r1, 22);
        var p2 = doc.CreateParagraph(); p2.Alignment = ParagraphAlignment.CENTER;
        var r2 = p2.CreateRun(); r2.SetText($"{input.PanelType ?? "GRC"}板幕墙"); SetFont(r2, 22);
        var p3 = doc.CreateParagraph(); p3.Alignment = ParagraphAlignment.CENTER;
        var r3 = p3.CreateRun(); r3.SetText("设计计算书"); SetFont(r3, 22);
        for (int i = 0; i < 10; i++) { var ep = doc.CreateParagraph(); ep.Alignment = ParagraphAlignment.CENTER; var er = ep.CreateRun(); er.SetText(""); SetFont(er, 16); }
        foreach (var (label, name) in new[] { ("设计：", input.Designer ?? "            "), ("校对：", input.Reviewer ?? "                "), ("审核：", input.Approver ?? "            "), ("批准：", input.Supervisor ?? "                 ") })
        { var sp = doc.CreateParagraph(); var sr = sp.CreateRun(); sr.SetText($"{label}{name}"); SetFont(sr, 16); }
        for (int i = 0; i < 4; i++) doc.CreateParagraph();
        var cp = doc.CreateParagraph(); cp.Alignment = ParagraphAlignment.CENTER;
        var cr = cp.CreateRun(); cr.SetText(input.CompanyName ?? "XX幕墙工程有限公司"); SetFont(cr, 16);
        var dp = doc.CreateParagraph(); dp.Alignment = ParagraphAlignment.CENTER;
        var dr = dp.CreateRun(); dr.SetText(input.ReportDate ?? ToChineseDate(DateTime.Now)); SetFont(dr, 16);
    }

    private void RenderTableOfContents(XWPFDocument doc)
    {
        doc.CreateParagraph(); doc.CreateParagraph();
        var tp = doc.CreateParagraph(); tp.Alignment = ParagraphAlignment.CENTER;
        var tr = tp.CreateRun(); tr.SetText("目录"); SetFont(tr, 36);
        doc.CreateParagraph();
        AddBody(doc, "（请在Word中更新域：全选→右键→更新域，即可生成自动目录）");
    }

    private void RenderStandardsSection(XWPFDocument doc)
    {
        AddHeading2(doc, "计算引用的规范、标准及资料");
        AddHeading3(doc, "幕墙设计规范：");
        foreach (var s in new[] {
            "《人造板材幕墙工程技术规范》JGJ336-2016",
            "《铝合金结构设计规范》GB50429-2007",
            "《建筑幕墙》GB/T21086-2007",
            "《建筑幕墙工程技术标准》DG/TJ08-56-2019"
        }) AddBody(doc, s);
        AddHeading3(doc, "板材规范：");
        foreach (var s in new[] {
            "《纤维增强水泥板》JC/T412-2006",
            "《人造石材》JC/T908-2013",
            "《建筑装饰用搪瓷钢板》JG/T234-2008",
            "《建筑用陶板》JG/T324-2011"
        }) AddBody(doc, s);
        AddHeading3(doc, "《建筑结构静力计算手册》(第二版)");
        AddHeading3(doc, "土建图纸：");
    }

    private void RenderBasicParametersSection(XWPFDocument doc, PanelCalcBookInput input)
    {
        AddHeading2(doc, "基本参数");
        AddHeading3(doc, "幕墙所在地区");
        AddIndent(doc, $"{input.City ?? "XX"}地区；");
        AddHeading3(doc, "地面粗糙度分类等级");
        AddBody(doc, $"本工程按{input.TerrainCategory ?? "B"}类地形考虑。");
        AddHeading3(doc, "抗震设防");
        AddBody(doc, $"本工程所在地{input.City ?? "XX"}地区地震基本烈度为：{input.SeismicIntensity}度；");
        AddHeading3(doc, "板材参数");
        AddIndent(doc, $"板材类型：{input.PanelType ?? "GRC"}");
        AddIndent(doc, $"板材厚度 t = {input.PlateThickness:F0} mm");
        AddIndent(doc, $"板材短边 a = {input.PanelWidth:F0} mm，长边 b = {input.PanelHeight:F0} mm");
        AddIndent(doc, $"板材密度 ρ = {input.PanelDensity:F0} kg/m³");
        AddIndent(doc, $"板材强度设计值 fg = {input.PanelDesignStrength:F1} MPa");
        AddIndent(doc, $"板材弹性模量 E = {input.PanelElasticModulus:F0} MPa");
    }

    private void RenderWindLoadSection(XWPFDocument doc, PanelCalcBookInput input)
    {
        AddHeading2(doc, "风荷载计算");
        double w0Mpa = input.BasicWindPressure / 1e6;
        double wk = input.GustFactor * input.HeightFactor * Math.Abs(input.Mus1Base) * w0Mpa;

        AddBody(doc, "风荷载标准值计算：");
        AddFormula(doc, "wk=βμzμs1w0", "GB55001-2021");
        AddCalcLine(doc, $"={input.GustFactor:F4}×{input.HeightFactor:F4}×{Math.Abs(input.Mus1Base):F1}×{w0Mpa:F6}");
        AddCalcLine(doc, $"={wk:F6}MPa");
    }

    private void RenderPanelSection(XWPFDocument doc, PanelCalcBookInput input)
    {
        AddHeading2(doc, "人造板材面板计算");

        double a = input.PanelWidth;
        double b = input.PanelHeight;
        double t = input.PlateThickness;
        double w0Mpa = input.BasicWindPressure / 1e6;
        double wk = input.GustFactor * input.HeightFactor * Math.Abs(input.Mus1Base) * w0Mpa;
        double alphaMax = 0.08;

        // 板材自重
        double qGk = input.PanelDensity * t / 1000.0 * 9.81 / 1000.0;
        double qEAk = 5.0 * alphaMax * qGk / 1000.0;
        double qDesign = 1.4 * wk + 0.5 * 1.3 * qEAk;

        AddBody(doc, "荷载组合：");
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

        // θ参数和η折减系数
        double Eg = input.PanelElasticModulus;
        double nu = 0.2;
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
        AddFormula(doc, "σ = 6×m×q×a²×η / t²", "JGJ336-2016 6.2.1");
        AddCalcLine(doc, $"= 6×{m_coeff:F4}×{qDesign:F6}×{a:F0}²×{eta:F4} / {t:F0}²");
        AddCalcLine(doc, $"= {sigma:F2} MPa");

        double fg = input.PanelDesignStrength;
        AddVerdict(doc, sigma <= fg, $"σ = {sigma:F2} MPa", $"fg = {fg:F1} MPa", "板材强度");

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

        AddFormula(doc, "df = η×md×wk×a⁴ / D", "JGJ336-2016 6.2.1");
        AddCalcLine(doc, $"= {eta:F4}×{m_d:F5}×{wk:F6}×{a:F0}⁴ / {D:F2}");
        AddCalcLine(doc, $"= {df:F4} mm");
        AddBody(doc, $"挠度限值：a/180 = {a:F0}/180 = {dfLimit:F2} mm");
        AddVerdict(doc, df <= dfLimit, $"df = {df:F4} mm", $"[df] = {dfLimit:F2} mm", "板材挠度");
    }

    private void RenderColumnSection(XWPFDocument doc, PanelCalcBookInput input)
    {
        AddHeading2(doc, "幕墙立柱计算");
        var col = input.Column!;
        AddBody(doc, "立柱计算按JGJ336-2016第6.1节进行。");
        AddIndent(doc, $"立柱型材规格：{col.ProfileCode}");
        AddIndent(doc, $"跨度 L = {col.Span:F0} mm");
        AddIndent(doc, $"计算间距 B = {col.LoadWidth:F0} mm");
        AddBody(doc, "（详细计算方法参见玻璃幕墙立柱计算章节）");
    }

    private void RenderBeamSection(XWPFDocument doc, PanelCalcBookInput input)
    {
        AddHeading2(doc, "幕墙横梁计算");
        var beam = input.Beam!;
        AddBody(doc, "横梁计算按JGJ336-2016第6.1节进行。");
        AddIndent(doc, $"横梁型材规格：{beam.ProfileCode}");
        AddIndent(doc, $"跨度 B = {beam.Span:F0} mm");
        AddBody(doc, "（详细计算方法参见玻璃幕墙横梁计算章节）");
    }

    private void RenderConnectionSection(XWPFDocument doc, PanelCalcBookInput input)
    {
        AddHeading2(doc, "连接件计算");
        AddBody(doc, "人造板材幕墙连接件通常采用不锈钢插销或挂件连接。");
        AddBody(doc, "连接件计算按JGJ336-2016第6.3节进行。");
    }

    private void RenderEmbedSection(XWPFDocument doc, PanelCalcBookInput input)
    {
        AddHeading2(doc, "预埋件计算");
        AddBody(doc, "预埋件计算按JGJ336-2016附录A进行。");
        AddBody(doc, "（详细计算方法参见玻璃幕墙预埋件计算章节）");
    }

    private void RenderAppendix(XWPFDocument doc)
    {
        AddHeading2(doc, "附录");
        AddHeading3(doc, "板材材料力学性能参数");
        AddBody(doc, "（根据具体板材类型查规范确定）");
    }
}

/// <summary>GRC/UHPC人造板材幕墙计算书输入模型</summary>
public class PanelCalcBookInput
{
    public string? ProjectName { get; set; }
    public string? CompanyName { get; set; }
    public string? Designer { get; set; }
    public string? Reviewer { get; set; }
    public string? Approver { get; set; }
    public string? Supervisor { get; set; }
    public string? ReportDate { get; set; }

    public string? City { get; set; }
    public double InstallHeight { get; set; }
    public double PanelWidth { get; set; } = 1200;
    public double PanelHeight { get; set; } = 1500;
    public double BasicWindPressure { get; set; }
    public string? TerrainCategory { get; set; }
    public int SeismicIntensity { get; set; }

    public double GustFactor { get; set; }
    public double HeightFactor { get; set; }
    public double Mus1Base { get; set; } = -1.0;

    // 板材参数
    public string? PanelType { get; set; } = "GRC"; // GRC, UHPC, 陶板
    public double PlateThickness { get; set; } = 30; // mm
    public double PanelDensity { get; set; } = 1800; // kg/m³
    public double PanelDesignStrength { get; set; } = 7.0; // MPa
    public double PanelElasticModulus { get; set; } = 18000; // MPa

    public string? ColumnMaterial { get; set; }
    public MemberCalcResult? Column { get; set; }
    public MemberCalcResult? Beam { get; set; }
}
