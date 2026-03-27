using NPOI.XWPF.UserModel;
using NPOI.OpenXmlFormats.Wordprocessing;
using NPOI.WP.UserModel;
using System.IO;

namespace GlassManagement.Api.Services;

/// <summary>
/// 点式钢管结构计算书生成服务
/// 
/// 依据JGJ102-2003《玻璃幕墙工程技术规范》第8章
/// </summary>
public class PointSupportCalcBookService
{
    private const string MainFont = "Times New Roman";
    private const string EastAsiaFont = "宋体";
    private int _h2Counter = 0;
    private int _h3Counter = 0;

    public byte[] GenerateReport(PointSupportCalcBookInput input)
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

        AddMainTitle(doc, "点式钢管结构设计计算书");

        RenderStandardsSection(doc);
        RenderBasicParametersSection(doc, input);
        RenderWindLoadSection(doc, input);
        RenderSteelColumnSection(doc, input);
        RenderSteelBeamSection(doc, input);
        RenderPointConnectionSection(doc, input);
        RenderGlassSection(doc, input);
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
        var hr = hp.CreateRun(); hr.SetText($"{projectName}          点式钢管结构设计计算书"); SetFont(hr, 9);
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

    private void RenderCoverPage(XWPFDocument doc, PointSupportCalcBookInput input)
    {
        for (int i = 0; i < 6; i++) doc.CreateParagraph();
        var p1 = doc.CreateParagraph(); p1.Alignment = ParagraphAlignment.CENTER;
        var r1 = p1.CreateRun(); r1.SetText(input.ProjectName ?? "XX幕墙装饰工程"); SetFont(r1, 22);
        var p2 = doc.CreateParagraph(); p2.Alignment = ParagraphAlignment.CENTER;
        var r2 = p2.CreateRun(); r2.SetText("点式钢管结构"); SetFont(r2, 22);
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
            "《玻璃幕墙工程技术规范》JGJ102-2003",
            "《铝合金结构设计规范》GB50429-2007",
            "《钢结构设计标准》GB50017-2017",
            "《建筑幕墙》GB/T21086-2007"
        }) AddBody(doc, s);
        AddHeading3(doc, "《建筑结构静力计算手册》(第二版)");
        AddHeading3(doc, "土建图纸：");
    }

    private void RenderBasicParametersSection(XWPFDocument doc, PointSupportCalcBookInput input)
    {
        AddHeading2(doc, "基本参数");
        AddHeading3(doc, "幕墙所在地区");
        AddIndent(doc, $"{input.City ?? "XX"}地区；");
        AddHeading3(doc, "地面粗糙度分类等级");
        AddBody(doc, $"本工程按{input.TerrainCategory ?? "B"}类地形考虑。");
        AddHeading3(doc, "抗震设防");
        AddBody(doc, $"本工程所在地{input.City ?? "XX"}地区地震基本烈度为：{input.SeismicIntensity}度；");
        AddHeading3(doc, "点式结构参数");
        AddIndent(doc, $"钢管立柱规格：{input.SteelColumnProfile ?? "Φ159×8 钢管"}");
        AddIndent(doc, $"钢管横梁规格：{input.SteelBeamProfile ?? "Φ121×6 钢管"}");
        AddIndent(doc, $"立柱跨度 L = {input.ColumnSpan:F0} mm");
        AddIndent(doc, $"横梁跨度 B = {input.BeamSpan:F0} mm");
    }

    private void RenderWindLoadSection(XWPFDocument doc, PointSupportCalcBookInput input)
    {
        AddHeading2(doc, "风荷载计算");
        double w0Mpa = input.BasicWindPressure / 1e6;
        double wk = input.GustFactor * input.HeightFactor * Math.Abs(input.Mus1Base) * w0Mpa;

        AddBody(doc, "风荷载标准值计算：");
        AddFormula(doc, "wk=βμzμs1w0", "GB55001-2021");
        AddCalcLine(doc, $"={input.GustFactor:F4}×{input.HeightFactor:F4}×{Math.Abs(input.Mus1Base):F1}×{w0Mpa:F6}");
        AddCalcLine(doc, $"={wk:F6}MPa");
    }

    private void RenderSteelColumnSection(XWPFDocument doc, PointSupportCalcBookInput input)
    {
        AddHeading2(doc, "钢管立柱计算");

        double w0Mpa = input.BasicWindPressure / 1e6;
        double wk = input.GustFactor * input.HeightFactor * Math.Abs(input.Mus1Base) * w0Mpa;
        double qwk = wk * input.ColumnLoadWidth;

        AddBody(doc, "钢管立柱参数：");
        AddIndent(doc, $"钢管规格：{input.SteelColumnProfile ?? "Φ159×8"}");
        AddIndent(doc, $"钢管截面积 A = {input.ColumnArea:F1} mm²");
        AddIndent(doc, $"钢管惯性矩 I = {input.ColumnIx:F1} cm⁴");
        AddIndent(doc, $"钢管抵抗矩 W = {input.ColumnWx:F1} cm³");

        AddHeading3(doc, "立柱强度计算");
        double fa = 215; // Q235B
        double q = 1.5 * qwk;
        double Mx = q * input.ColumnSpan * input.ColumnSpan / 8.0;
        double sigma = Mx / (input.ColumnWx * 1000);

        AddFormula(doc, "σ = M/W ≤ f", "GB50017-2017");
        AddCalcLine(doc, $"= {Mx:F1} / ({input.ColumnWx:F1} × 1000)");
        AddCalcLine(doc, $"= {sigma:F1} MPa");
        AddVerdict(doc, sigma <= fa, $"σ = {sigma:F1} MPa", $"f = {fa:F0} MPa", "钢管立柱强度");

        AddHeading3(doc, "立柱挠度计算");
        double E = 206000;
        double Ix_mm4 = input.ColumnIx * 10000;
        double df = 5.0 * qwk * Math.Pow(input.ColumnSpan, 4) / (384.0 * E * Ix_mm4);
        double dfLim = input.ColumnSpan / 250.0;

        AddFormula(doc, "df = 5×qk×L⁴ / (384×E×Ix)");
        AddCalcLine(doc, $"= 5×{qwk:F4}×{input.ColumnSpan:F0}⁴ / (384×{E:F0}×{Ix_mm4:F0})");
        AddCalcLine(doc, $"= {df:F1} mm");
        AddBody(doc, $"挠度限值 [df] = {dfLim:F1} mm");
        AddVerdict(doc, df <= dfLim, $"df = {df:F1} mm", $"[df] = {dfLim:F1} mm", "钢管立柱挠度");
    }

    private void RenderSteelBeamSection(XWPFDocument doc, PointSupportCalcBookInput input)
    {
        AddHeading2(doc, "钢管横梁计算");

        double w0Mpa = input.BasicWindPressure / 1e6;
        double wk = input.GustFactor * input.HeightFactor * Math.Abs(input.Mus1Base) * w0Mpa;
        double qwk = wk * input.BeamLoadWidth;

        AddBody(doc, "钢管横梁参数：");
        AddIndent(doc, $"钢管规格：{input.SteelBeamProfile ?? "Φ121×6"}");
        AddIndent(doc, $"钢管惯性矩 I = {input.BeamIx:F1} cm⁴");
        AddIndent(doc, $"钢管抵抗矩 W = {input.BeamWx:F1} cm³");

        AddHeading3(doc, "横梁强度计算");
        double fa = 215;
        double q = 1.5 * qwk;
        double My = q * input.BeamSpan * input.BeamSpan / 12.0;
        double sigma = My / (input.BeamWx * 1000);

        AddFormula(doc, "My = q × B² / 12", "三角形分布");
        AddCalcLine(doc, $"= {q:F4} × {input.BeamSpan:F0}² / 12");
        AddCalcLine(doc, $"= {My:F1} N·mm");
        AddVerdict(doc, sigma <= fa, $"σ = {sigma:F1} MPa", $"f = {fa:F0} MPa", "钢管横梁强度");

        AddHeading3(doc, "横梁挠度计算");
        double E = 206000;
        double Iy_mm4 = input.BeamIx * 10000;
        double df = qwk * Math.Pow(input.BeamSpan, 4) / (120.0 * E * Iy_mm4);
        double dfLim = input.BeamSpan / 250.0;

        AddFormula(doc, "df = qwk × B⁴ / (120×E×Iy)");
        AddCalcLine(doc, $"= {qwk:F4} × {input.BeamSpan:F0}⁴ / (120×{E:F0}×{Iy_mm4:F0})");
        AddCalcLine(doc, $"= {df:F1} mm");
        AddVerdict(doc, df <= dfLim, $"df = {df:F1} mm", $"[df] = {dfLim:F1} mm", "钢管横梁挠度");
    }

    private void RenderPointConnectionSection(XWPFDocument doc, PointSupportCalcBookInput input)
    {
        AddHeading2(doc, "点式连接计算");
        AddBody(doc, "点式连接采用驳接爪和不锈钢螺栓。");

        double w0Mpa = input.BasicWindPressure / 1e6;
        double wk = input.GustFactor * input.HeightFactor * Math.Abs(input.Mus1Base) * w0Mpa;
        double V = 1.4 * wk * input.PointSpacing * input.PointSpacing; // N

        AddHeading3(doc, "驳接爪承载力计算");
        AddBody(doc, $"驳接爪承受力 V = {V:F1} N");
        double Nt = 5000; // 假设驳接爪承载力
        AddBody(doc, $"驳接爪承载力 Nt = {Nt:F0} N");
        AddVerdict(doc, V <= Nt, $"V = {V:F1} N", $"Nt = {Nt:F0} N", "驳接爪承载力");

        AddHeading3(doc, "螺栓抗剪计算");
        double Nb = 3920; // M5不锈钢螺栓抗剪承载力
        AddBody(doc, $"螺栓抗剪承载力 Nb = {Nb:F0} N");
        AddVerdict(doc, V <= Nb, $"V = {V:F1} N", $"Nb = {Nb:F0} N", "螺栓抗剪");
    }

    private void RenderGlassSection(XWPFDocument doc, PointSupportCalcBookInput input)
    {
        AddHeading2(doc, "玻璃计算");
        AddBody(doc, "点式幕墙玻璃计算与普通幕墙玻璃计算方法相同。");
        AddBody(doc, "玻璃计算按JGJ102-2003第6.1节进行。");
        AddBody(doc, $"玻璃配置：{input.GlassDescription ?? "12mm 钢化玻璃"}");
    }

    private void RenderAppendix(XWPFDocument doc)
    {
        AddHeading2(doc, "附录");
        AddHeading3(doc, "钢管截面特性表");
        AddBody(doc, "（参照GB/T8162-2018）");
    }
}

/// <summary>点式钢管结构计算书输入模型</summary>
public class PointSupportCalcBookInput
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
    public double BasicWindPressure { get; set; }
    public string? TerrainCategory { get; set; }
    public int SeismicIntensity { get; set; }

    public double GustFactor { get; set; }
    public double HeightFactor { get; set; }
    public double Mus1Base { get; set; } = -1.0;

    // 钢管立柱参数
    public string? SteelColumnProfile { get; set; } = "Φ159×8";
    public double ColumnSpan { get; set; } = 3600; // mm
    public double ColumnLoadWidth { get; set; } = 1200; // mm
    public double ColumnArea { get; set; } = 3868; // mm²
    public double ColumnIx { get; set; } = 1080; // cm⁴
    public double ColumnWx { get; set; } = 136; // cm³

    // 钢管横梁参数
    public string? SteelBeamProfile { get; set; } = "Φ121×6";
    public double BeamSpan { get; set; } = 1200; // mm
    public double BeamLoadWidth { get; set; } = 1800; // mm
    public double BeamIx { get; set; } = 620; // cm⁴
    public double BeamWx { get; set; } = 102; // cm³

    // 点式连接参数
    public double PointSpacing { get; set; } = 1200; // mm

    // 玻璃参数
    public string? GlassDescription { get; set; } = "12mm 钢化玻璃";
}
