using NPOI.XWPF.UserModel;
using NPOI.OpenXmlFormats.Wordprocessing;
using NPOI.WP.UserModel;
using System.IO;

namespace GlassManagement.Api.Services;

/// <summary>
/// 门窗计算书生成服务
/// 
/// 依据GB/T7106-2019《建筑外门窗气密、水密、抗风压性能分级及检测方法》
/// </summary>
public class DoorWindowCalcBookService
{
    private const string MainFont = "Times New Roman";
    private const string EastAsiaFont = "宋体";
    private int _h2Counter = 0;
    private int _h3Counter = 0;

    public byte[] GenerateReport(DoorWindowCalcBookInput input)
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

        AddMainTitle(doc, "门窗设计计算书");

        RenderStandardsSection(doc);
        RenderBasicParametersSection(doc, input);
        RenderWindLoadSection(doc, input);
        RenderFrameSection(doc, input);
        RenderGlassSection(doc, input);
        RenderHardwareSection(doc, input);
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
        var hr = hp.CreateRun(); hr.SetText($"{projectName}          门窗设计计算书"); SetFont(hr, 9);
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

    private void RenderCoverPage(XWPFDocument doc, DoorWindowCalcBookInput input)
    {
        for (int i = 0; i < 6; i++) doc.CreateParagraph();
        var p1 = doc.CreateParagraph(); p1.Alignment = ParagraphAlignment.CENTER;
        var r1 = p1.CreateRun(); r1.SetText(input.ProjectName ?? "XX幕墙装饰工程"); SetFont(r1, 22);
        var p2 = doc.CreateParagraph(); p2.Alignment = ParagraphAlignment.CENTER;
        var r2 = p2.CreateRun(); r2.SetText("门窗"); SetFont(r2, 22);
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
        AddHeading3(doc, "门窗规范：");
        foreach (var s in new[] {
            "《建筑外门窗气密、水密、抗风压性能分级及检测方法》GB/T7106-2019",
            "《建筑门窗术语》GB/T5823-2008",
            "《建筑门窗洞口尺寸系列》GB/T5824-2008",
            "《铝合金门窗》GB/T8478-2020"
        }) AddBody(doc, s);
        AddHeading3(doc, "《建筑结构静力计算手册》(第二版)");
        AddHeading3(doc, "土建图纸：");
    }

    private void RenderBasicParametersSection(XWPFDocument doc, DoorWindowCalcBookInput input)
    {
        AddHeading2(doc, "基本参数");
        AddHeading3(doc, "门窗所在地区");
        AddIndent(doc, $"{input.City ?? "XX"}地区；");
        AddHeading3(doc, "地面粗糙度分类等级");
        AddBody(doc, $"本工程按{input.TerrainCategory ?? "B"}类地形考虑。");
        AddHeading3(doc, "抗震设防");
        AddBody(doc, $"本工程所在地{input.City ?? "XX"}地区地震基本烈度为：{input.SeismicIntensity}度；");
        AddHeading3(doc, "门窗参数");
        AddIndent(doc, $"门窗类型：{input.WindowType ?? "铝合金平开窗"}");
        AddIndent(doc, $"门窗宽度 W = {input.WindowWidth:F0} mm");
        AddIndent(doc, $"门窗高度 H = {input.WindowHeight:F0} mm");
        AddIndent(doc, $"开启方式：{input.OpeningType ?? "平开"}");
        AddIndent(doc, $"玻璃配置：{input.GlassDescription ?? "6+12A+6 中空玻璃"}");
    }

    private void RenderWindLoadSection(XWPFDocument doc, DoorWindowCalcBookInput input)
    {
        AddHeading2(doc, "风荷载计算");
        double w0Mpa = input.BasicWindPressure / 1e6;
        double wk = input.GustFactor * input.HeightFactor * Math.Abs(input.Mus1Base) * w0Mpa;

        AddBody(doc, "风荷载标准值计算：");
        AddFormula(doc, "wk=βμzμs1w0", "GB55001-2021");
        AddCalcLine(doc, $"={input.GustFactor:F4}×{input.HeightFactor:F4}×{Math.Abs(input.Mus1Base):F1}×{w0Mpa:F6}");
        AddCalcLine(doc, $"={wk:F6}MPa");

        AddBody(doc, $"风荷载标准值 wk = {wk * 1000:F1} kPa");
        AddBody(doc, $"抗风压性能分级应不低于 {GetWindPressureGrade(wk * 1000)} 级");
    }

    private string GetWindPressureGrade(double wk_kPa)
    {
        if (wk_kPa <= 1.0) return "1";
        if (wk_kPa <= 1.5) return "2";
        if (wk_kPa <= 2.0) return "3";
        if (wk_kPa <= 2.5) return "4";
        if (wk_kPa <= 3.0) return "5";
        if (wk_kPa <= 3.5) return "6";
        if (wk_kPa <= 4.0) return "7";
        if (wk_kPa <= 4.5) return "8";
        return "9";
    }

    private void RenderFrameSection(XWPFDocument doc, DoorWindowCalcBookInput input)
    {
        AddHeading2(doc, "门窗框计算");

        double w0Mpa = input.BasicWindPressure / 1e6;
        double wk = input.GustFactor * input.HeightFactor * Math.Abs(input.Mus1Base) * w0Mpa;
        double qwk = wk * input.FrameLoadWidth;

        AddHeading3(doc, "窗框强度计算");
        AddBody(doc, "窗框型材规格：铝合金型材 6063-T5");
        AddBody(doc, "窗框承受风荷载线荷载：");
        AddFormula(doc, "qwk=wk×B", "GB55001-2021");
        AddCalcLine(doc, $"={wk:F6}×{input.FrameLoadWidth:F0}");
        AddCalcLine(doc, $"={qwk:F4}N/mm");

        double q = 1.5 * qwk;
        double Mx = q * input.FrameSpan * input.FrameSpan / 8.0;
        double fa = 85; // 6063-T5
        double Wx = 1.5; // 假设截面模量 cm³
        double sigma = Mx / (Wx * 1000);

        AddFormula(doc, "σ = M/(γ·Wn) ≤ f");
        AddCalcLine(doc, $"= {Mx:F1} / (1.0 × {Wx:F1} × 1000)");
        AddCalcLine(doc, $"= {sigma:F1} MPa");
        AddVerdict(doc, sigma <= fa, $"σ = {sigma:F1} MPa", $"fa = {fa:F0} MPa", "窗框强度");

        AddHeading3(doc, "窗框挠度计算");
        double df = 5.0 * qwk * Math.Pow(input.FrameSpan, 4) / (384.0 * 70000 * 3.0 * 10000);
        double dfLim = input.FrameSpan / 180.0;

        AddFormula(doc, "df = 5×qk×L⁴ / (384×E×Ix)");
        AddCalcLine(doc, $"= 5×{qwk:F4}×{input.FrameSpan:F0}⁴ / (384×70000×3.0×10000)");
        AddCalcLine(doc, $"= {df:F1} mm");
        AddBody(doc, $"挠度限值 [df] = {dfLim:F1} mm");
        AddVerdict(doc, df <= dfLim, $"df = {df:F1} mm", $"[df] = {dfLim:F1} mm", "窗框挠度");
    }

    private void RenderGlassSection(XWPFDocument doc, DoorWindowCalcBookInput input)
    {
        AddHeading2(doc, "玻璃计算");
        AddBody(doc, "门窗玻璃计算方法与幕墙玻璃计算相同。");
        AddBody(doc, "玻璃计算按JGJ113-2015《建筑玻璃应用技术规程》进行。");
        AddBody(doc, $"玻璃配置：{input.GlassDescription ?? "6+12A+6 中空玻璃"}");
    }

    private void RenderHardwareSection(XWPFDocument doc, DoorWindowCalcBookInput input)
    {
        AddHeading2(doc, "五金件计算");
        AddBody(doc, "门窗五金件包括铰链、锁点、执手等。");
        AddBody(doc, "五金件应满足门窗抗风压性能要求。");

        double w0Mpa = input.BasicWindPressure / 1e6;
        double wk = input.GustFactor * input.HeightFactor * Math.Abs(input.Mus1Base) * w0Mpa;
        double V = 1.4 * wk * input.WindowWidth * input.WindowHeight / 2.0;

        AddBody(doc, $"单个锁点承受剪力 V = {V:F1} N");
        AddBody(doc, "五金件承载力应大于风荷载产生的力。");
    }

    private void RenderAppendix(XWPFDocument doc)
    {
        AddHeading2(doc, "附录");
        AddHeading3(doc, "门窗性能分级表");
        AddBody(doc, "（参照GB/T7106-2019分级标准）");
    }
}

/// <summary>门窗计算书输入模型</summary>
public class DoorWindowCalcBookInput
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

    // 门窗参数
    public string? WindowType { get; set; } = "铝合金平开窗";
    public double WindowWidth { get; set; } = 1500; // mm
    public double WindowHeight { get; set; } = 1500; // mm
    public string? OpeningType { get; set; } = "平开";
    public string? GlassDescription { get; set; } = "6+12A+6 中空玻璃";

    // 窗框参数
    public double FrameSpan { get; set; } = 1500; // mm
    public double FrameLoadWidth { get; set; } = 750; // mm
}
