using NPOI.XWPF.UserModel;
using NPOI.OpenXmlFormats.Wordprocessing;
using NPOI.WP.UserModel;
using System.IO;

namespace GlassManagement.Api.Services;

/// <summary>
/// 节能计算书生成服务
/// 
/// 依据GB50189-2015《公共建筑节能设计标准》
/// </summary>
public class EnergySavingCalcBookService
{
    private const string MainFont = "Times New Roman";
    private const string EastAsiaFont = "宋体";
    private int _h2Counter = 0;
    private int _h3Counter = 0;

    public byte[] GenerateReport(EnergySavingCalcBookInput input)
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

        AddMainTitle(doc, "幕墙节能设计计算书");

        RenderStandardsSection(doc);
        RenderBasicParametersSection(doc, input);
        RenderThermalTransmittanceSection(doc, input);
        RenderShadingSection(doc, input);
        RenderConclusion(doc, input);
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
        var hr = hp.CreateRun(); hr.SetText($"{projectName}          幕墙节能设计计算书"); SetFont(hr, 9);
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

    private void RenderCoverPage(XWPFDocument doc, EnergySavingCalcBookInput input)
    {
        for (int i = 0; i < 6; i++) doc.CreateParagraph();
        var p1 = doc.CreateParagraph(); p1.Alignment = ParagraphAlignment.CENTER;
        var r1 = p1.CreateRun(); r1.SetText(input.ProjectName ?? "XX幕墙装饰工程"); SetFont(r1, 22);
        var p2 = doc.CreateParagraph(); p2.Alignment = ParagraphAlignment.CENTER;
        var r2 = p2.CreateRun(); r2.SetText("幕墙节能"); SetFont(r2, 22);
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
        AddHeading3(doc, "节能设计规范：");
        foreach (var s in new[] {
            "《公共建筑节能设计标准》GB50189-2015",
            "《建筑节能与可再生能源利用通用规范》GB55015-2021",
            "《建筑外门窗保温性能检测方法》GB/T8484-2020",
            "《建筑幕墙保温性能检测方法》GB/T29043-2012",
            "《建筑玻璃应用技术规程》JGJ113-2015"
        }) AddBody(doc, s);
        AddHeading3(doc, "土建图纸：");
    }

    private void RenderBasicParametersSection(XWPFDocument doc, EnergySavingCalcBookInput input)
    {
        AddHeading2(doc, "基本参数");
        AddHeading3(doc, "工程所在地区");
        AddIndent(doc, $"{input.City ?? "XX"}地区；");
        AddIndent(doc, $"气候区：{input.ClimateZone ?? "夏热冬冷"}地区");
        AddHeading3(doc, "幕墙类型");
        AddIndent(doc, $"幕墙类型：{input.CurtainWallType ?? "玻璃幕墙"}");
        AddIndent(doc, $"玻璃配置：{input.GlassDescription ?? "6+12A+6 Low-E中空玻璃"}");
        AddBody(doc, "依据GB50189-2015第4.2节，幕墙热工性能应满足以下要求：");
    }

    private void RenderThermalTransmittanceSection(XWPFDocument doc, EnergySavingCalcBookInput input)
    {
        AddHeading2(doc, "传热系数计算（K值）");

        // 玻璃部分K值
        double Ug = input.GlassKValue;
        double Uf = input.FrameKValue;

        AddHeading3(doc, "玻璃传热系数");
        AddBody(doc, $"玻璃传热系数 Ug = {Ug:F2} W/(m²·K)");
        AddBody(doc, "（由玻璃厂家提供或按GB/T22476计算）");

        AddHeading3(doc, "框传热系数");
        AddBody(doc, $"框传热系数 Uf = {Uf:F2} W/(m²·K)");
        AddBody(doc, "（由型材厂家提供或按JGJ/T151计算）");

        // 幕墙整体K值
        double Ag = input.GlassArea; // 玻璃面积 m²
        double Af = input.FrameArea; // 框面积 m²
        double Atotal = Ag + Af;

        double Utotal = (Ug * Ag + Uf * Af) / Atotal;

        AddHeading3(doc, "幕墙整体传热系数");
        AddFormula(doc, "U = (Ug×Ag + Uf×Af) / (Ag + Af)", "GB50189-2015");
        AddCalcLine(doc, $"= ({Ug:F2}×{Ag:F2} + {Uf:F2}×{Af:F2}) / ({Ag:F2} + {Af:F2})");
        AddCalcLine(doc, $"= {Utotal:F2} W/(m²·K)");

        double Ulimit = input.KValueLimit;
        AddBody(doc, $"规范限值 U限 = {Ulimit:F2} W/(m²·K)");
        AddBody(doc, Utotal <= Ulimit ? "满足要求。" : "不满足要求。");
    }

    private void RenderShadingSection(XWPFDocument doc, EnergySavingCalcBookInput input)
    {
        AddHeading2(doc, "遮阳系数计算（SC值）");

        double SC = input.ShadingCoefficient;
        double SHGC = input.SolarHeatGainCoefficient;

        AddHeading3(doc, "玻璃遮阳系数");
        AddBody(doc, $"玻璃遮阳系数 SC = {SC:F2}");
        AddBody(doc, $"太阳得热系数 SHGC = {SHGC:F2}");
        AddBody(doc, "SC = SHGC / 0.87");

        AddHeading3(doc, "综合遮阳系数");
        double SCw = SC * input.ExternalShadingFactor * input.InternalShadingFactor;
        AddFormula(doc, "SCw = SC × C外 × C内", "GB50189-2015");
        AddCalcLine(doc, $"= {SC:F2} × {input.ExternalShadingFactor:F2} × {input.InternalShadingFactor:F2}");
        AddCalcLine(doc, $"= {SCw:F2}");

        double SClimit = input.SCValueLimit;
        AddBody(doc, $"规范限值 SC限 = {SClimit:F2}");
        AddBody(doc, SCw <= SClimit ? "满足要求。" : "不满足要求。");

        // 可见光透射比
        AddHeading3(doc, "可见光透射比");
        double VLT = input.VisibleLightTransmittance;
        AddBody(doc, $"可见光透射比 τv = {VLT:F2}");
        double VLTlimit = 0.4;
        AddBody(doc, $"规范限值 τv ≥ {VLTlimit:F1}");
        AddBody(doc, VLT >= VLTlimit ? "满足要求。" : "不满足要求。");
    }

    private void RenderConclusion(XWPFDocument doc, EnergySavingCalcBookInput input)
    {
        AddHeading2(doc, "节能达标判定");

        double Ug = input.GlassKValue;
        double Uf = input.FrameKValue;
        double Ag = input.GlassArea;
        double Af = input.FrameArea;
        double Utotal = (Ug * Ag + Uf * Af) / (Ag + Af);
        double SCw = input.ShadingCoefficient * input.ExternalShadingFactor * input.InternalShadingFactor;

        bool kPass = Utotal <= input.KValueLimit;
        bool scPass = SCw <= input.SCValueLimit;
        bool vltPass = input.VisibleLightTransmittance >= 0.4;

        AddBody(doc, "节能性能判定结果：");
        AddIndent(doc, $"传热系数 U = {Utotal:F2} W/(m²·K) {(kPass ? "≤" : ">")} {input.KValueLimit:F2} W/(m²·K)，{(kPass ? "满足要求" : "不满足要求")}");
        AddIndent(doc, $"综合遮阳系数 SCw = {SCw:F2} {(scPass ? "≤" : ">")} {input.SCValueLimit:F2}，{(scPass ? "满足要求" : "不满足要求")}");
        AddIndent(doc, $"可见光透射比 τv = {input.VisibleLightTransmittance:F2} {(vltPass ? "≥" : "<")} 0.4，{(vltPass ? "满足要求" : "不满足要求")}");

        bool allPass = kPass && scPass && vltPass;
        AddBody(doc, allPass ? "幕墙热工性能满足节能设计标准要求。" : "幕墙热工性能不满足节能设计标准要求，需优化设计。");
    }

    private void RenderAppendix(XWPFDocument doc)
    {
        AddHeading2(doc, "附录");
        AddHeading3(doc, "节能参数表");
        AddBody(doc, "（参照GB50189-2015附录）");
    }
}

/// <summary>节能计算书输入模型</summary>
public class EnergySavingCalcBookInput
{
    public string? ProjectName { get; set; }
    public string? CompanyName { get; set; }
    public string? Designer { get; set; }
    public string? Reviewer { get; set; }
    public string? Approver { get; set; }
    public string? Supervisor { get; set; }
    public string? ReportDate { get; set; }

    public string? City { get; set; }
    public string? ClimateZone { get; set; } = "夏热冬冷";
    public string? CurtainWallType { get; set; } = "玻璃幕墙";
    public string? GlassDescription { get; set; } = "6+12A+6 Low-E中空玻璃";

    // 热工参数
    public double GlassKValue { get; set; } = 1.8; // W/(m²·K)
    public double FrameKValue { get; set; } = 3.0; // W/(m²·K)
    public double GlassArea { get; set; } = 2.0; // m²
    public double FrameArea { get; set; } = 0.5; // m²
    public double KValueLimit { get; set; } = 2.4; // W/(m²·K)

    // 遮阳参数
    public double ShadingCoefficient { get; set; } = 0.55; // SC
    public double SolarHeatGainCoefficient { get; set; } = 0.48; // SHGC
    public double ExternalShadingFactor { get; set; } = 1.0;
    public double InternalShadingFactor { get; set; } = 1.0;
    public double SCValueLimit { get; set; } = 0.5;

    // 可见光透射比
    public double VisibleLightTransmittance { get; set; } = 0.65;
}
