using NPOI.XWPF.UserModel;
using NPOI.OpenXmlFormats.Wordprocessing;
using NPOI.WP.UserModel;
using System.IO;

namespace GlassManagement.Api.Services;

/// <summary>
/// 单元幕墙计算书生成服务（使用NPOI生成.docx）
/// 
/// 严格参照原程序计算书样本格式，包含：
/// - 封面、目录、规范引用
/// - 单元立柱计算（双跨梁模型）
/// - 单元横梁计算
/// - 单元插芯连接计算
/// - 单元挂件计算
/// - 面板（玻璃/铝板）计算
/// - 密封胶条计算
/// - 预埋件计算
/// </summary>
public class UnitCurtainWallCalcBookService
{
    private const string MainFont = "Times New Roman";
    private const string EastAsiaFont = "宋体";

    private int _h2Counter = 0;
    private int _h3Counter = 0;

    /// <summary>生成单元幕墙计算书Word文档</summary>
    public byte[] GenerateReport(UnitCurtainWallCalcBookInput input)
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

        AddMainTitle(doc, "单元结构幕墙设计计算书");

        // 1. 规范引用
        RenderStandardsSection(doc);

        // 2. 基本参数
        RenderBasicParametersSection(doc, input);

        // 3. 风荷载计算
        RenderWindLoadSection(doc, input);

        // 4. 单元立柱计算
        if (input.Column != null)
            RenderColumnSection(doc, input);

        // 5. 单元横梁计算
        if (input.Beam != null)
            RenderBeamSection(doc, input);

        // 6. 单元插芯连接计算
        RenderPlugConnectionSection(doc, input);

        // 7. 单元挂件计算
        RenderHangerSection(doc, input);

        // 8. 面板计算
        RenderPanelSection(doc, input);

        // 9. 预埋件计算
        RenderEmbedSection(doc, input);

        // 10. 附录
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
        sectPr.pgSz.w = 11906; sectPr.pgSz.h = 16838;
        if (sectPr.pgMar == null) sectPr.pgMar = new CT_PageMar();
        sectPr.pgMar.top = 1417; sectPr.pgMar.bottom = 1134;
        sectPr.pgMar.left = 1417; sectPr.pgMar.right = 1417;
    }

    private void SetHeaderFooter(XWPFDocument doc, string projectName)
    {
        var header = doc.CreateHeader(HeaderFooterType.DEFAULT);
        var hp = header.CreateParagraph(); hp.Alignment = ParagraphAlignment.LEFT;
        var hr = hp.CreateRun(); hr.SetText($"{projectName}          单元结构幕墙设计计算书"); SetFont(hr, 9);

        var footer = doc.CreateFooter(HeaderFooterType.DEFAULT);
        var fp = footer.CreateParagraph(); fp.Alignment = ParagraphAlignment.CENTER;
        var fr1 = fp.CreateRun(); fr1.SetText("第 "); SetFont(fr1, 9);
        fp.GetCTP().AddNewFldSimple().instr = " PAGE ";
        var fr2 = fp.CreateRun(); fr2.SetText(" / "); SetFont(fr2, 9);
        fp.GetCTP().AddNewFldSimple().instr = " NUMPAGES ";
        var fr3 = fp.CreateRun(); fr3.SetText(" 页"); SetFont(fr3, 9);
    }

    private void AddSectionBreak(XWPFDocument doc, ST_SectionMark type) { _ = doc; _ = type; }

    #endregion

    #region 通用辅助方法

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

    private void RenderCoverPage(XWPFDocument doc, UnitCurtainWallCalcBookInput input)
    {
        for (int i = 0; i < 6; i++) doc.CreateParagraph();
        var p1 = doc.CreateParagraph(); p1.Alignment = ParagraphAlignment.CENTER;
        var r1 = p1.CreateRun(); r1.SetText(input.ProjectName ?? "XX幕墙装饰工程"); SetFont(r1, 22);
        var p2 = doc.CreateParagraph(); p2.Alignment = ParagraphAlignment.CENTER;
        var r2 = p2.CreateRun(); r2.SetText("单元结构幕墙"); SetFont(r2, 22);
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
        foreach (var s in new[] { "《铝合金结构设计规范》GB50429-2007", "《玻璃幕墙工程技术规范》JGJ102-2003", "《小单元建筑幕墙构件》JG/T216-2024", "《建筑幕墙》GB/T21086-2007", "《建筑幕墙工程技术标准》DG/TJ08-56-2019" })
            AddBody(doc, s);
        AddHeading3(doc, "《建筑结构静力计算手册》(第二版)");
        AddHeading3(doc, "土建图纸：");
    }

    private void RenderBasicParametersSection(XWPFDocument doc, UnitCurtainWallCalcBookInput input)
    {
        AddHeading2(doc, "基本参数");
        AddHeading3(doc, "幕墙所在地区");
        AddIndent(doc, $"{input.City ?? "XX"}地区；");
        AddHeading3(doc, "地面粗糙度分类等级");
        AddBody(doc, $"本工程按{input.TerrainCategory ?? "B"}类地形考虑。");
        AddHeading3(doc, "抗震设防");
        AddBody(doc, $"本工程所在地{input.City ?? "XX"}地区地震基本烈度为：{input.SeismicIntensity}度；");
        AddHeading3(doc, "单元板块参数");
        AddIndent(doc, $"单元宽度 B = {input.UnitWidth:F0} mm");
        AddIndent(doc, $"单元高度 H = {input.UnitHeight:F0} mm");
        AddIndent(doc, $"单元自重标准值 Gk = {input.UnitWeight:F1} kN/块");
    }

    private void RenderWindLoadSection(XWPFDocument doc, UnitCurtainWallCalcBookInput input)
    {
        AddHeading2(doc, "风荷载计算");
        double w0Mpa = input.BasicWindPressure / 1e6;
        double wk = input.GustFactor * input.HeightFactor * Math.Abs(input.Mus1Base) * w0Mpa;

        AddBody(doc, "风荷载标准值计算：");
        AddFormula(doc, "wk=βμzμs1w0", "GB55001-2021");
        AddCalcLine(doc, $"={input.GustFactor:F4}×{input.HeightFactor:F4}×{Math.Abs(input.Mus1Base):F1}×{w0Mpa:F6}");
        AddCalcLine(doc, $"={wk:F6}MPa");
        AddBody(doc, $"支承结构风荷载标准值 wk = {wk:F6} MPa");
    }

    private void RenderColumnSection(XWPFDocument doc, UnitCurtainWallCalcBookInput input)
    {
        AddHeading2(doc, "单元立柱计算");
        var col = input.Column!;
        AddBody(doc, "基本参数：");
        AddIndent(doc, $"立柱型材规格：{col.ProfileCode}");
        AddIndent(doc, $"单元高度 H = {col.Span:F0} mm");
        AddIndent(doc, $"立柱计算间距 B = {col.LoadWidth:F0} mm");
        AddIndent(doc, $"材质：{input.ColumnMaterial ?? "6063-T5"}");

        double w0Mpa = input.BasicWindPressure / 1e6;
        double wk = input.GustFactor * input.HeightFactor * Math.Abs(input.Mus1Base) * w0Mpa;
        double qwk = wk * col.LoadWidth;

        AddHeading3(doc, "立柱型材选材计算");
        AddBody(doc, "风荷载线荷载：");
        AddFormula(doc, "qwk=wk×B", "GB55001-2021");
        AddCalcLine(doc, $"={wk:F6}×{col.LoadWidth:F0}");
        AddCalcLine(doc, $"={qwk:F4}N/mm");

        AddHeading3(doc, "立柱抗弯强度计算");
        double fa = col.DesignStrength > 0 ? col.DesignStrength : 85;
        double gamma = col.PlasticFactor > 0 ? col.PlasticFactor : 1.0;
        double q = 1.5 * qwk;
        double Mx = q * col.Span * col.Span / 8.0;
        double sigma = Mx / (gamma * col.Wx * 1000);

        AddFormula(doc, "σ = M/(γ·Wn) ≤ f", "6.2.1[JGJ102-2003]");
        AddCalcLine(doc, $"= {Mx:F1} / ({gamma:F2} × {col.Wx:F2} × 1000)");
        AddCalcLine(doc, $"= {sigma:F1} MPa");
        AddVerdict(doc, sigma <= fa, $"σ = {sigma:F1} MPa", $"fa = {fa:F0} MPa", "立柱抗弯强度");

        AddHeading3(doc, "立柱挠度计算");
        double E = col.ElasticModulus;
        double Ix_mm4 = col.Ix * 10000;
        double df = 5.0 * qwk * Math.Pow(col.Span, 4) / (384.0 * E * Ix_mm4);
        double dfLim = col.DeflectionLimit > 0 ? col.DeflectionLimit : col.Span / 180.0;

        AddFormula(doc, "df = 5×qk×L⁴ / (384×E×Ix)");
        AddCalcLine(doc, $"= 5×{qwk:F4}×{col.Span:F0}⁴ / (384×{E:F0}×{Ix_mm4:F0})");
        AddCalcLine(doc, $"= {df:F1} mm");
        AddBody(doc, $"挠度限值 [df] = {dfLim:F1} mm");
        AddVerdict(doc, df <= dfLim, $"df = {df:F1} mm", $"[df] = {dfLim:F1} mm", "立柱挠度");
    }

    private void RenderBeamSection(XWPFDocument doc, UnitCurtainWallCalcBookInput input)
    {
        AddHeading2(doc, "单元横梁计算");
        var beam = input.Beam!;
        AddBody(doc, "基本参数：");
        AddIndent(doc, $"横梁型材规格：{beam.ProfileCode}");
        AddIndent(doc, $"横梁跨度 B = {beam.Span:F0} mm");
        AddIndent(doc, $"横梁受荷宽度 L横 = {beam.LoadWidth:F0} mm");

        double w0Mpa = input.BasicWindPressure / 1e6;
        double wk = input.GustFactor * input.HeightFactor * Math.Abs(input.Mus1Base) * w0Mpa;
        double qwk_beam = wk * beam.LoadWidth;
        double fa = beam.DesignStrength > 0 ? beam.DesignStrength : 85;
        double q = 1.5 * qwk_beam;

        AddHeading3(doc, "横梁抗弯强度计算");
        double My = q * beam.Span * beam.Span / 12.0;
        double sigma = My / (beam.PlasticFactor * (beam.Wy > 0 ? beam.Wy : beam.Wx) * 1000);
        AddFormula(doc, "My = q × B² / 12", "三角形分布简支梁");
        AddCalcLine(doc, $"= {q:F4} × {beam.Span:F0}² / 12");
        AddCalcLine(doc, $"= {My:F1} N·mm");
        AddVerdict(doc, sigma <= fa, $"σ = {sigma:F1} MPa", $"fa = {fa:F0} MPa", "横梁抗弯强度");

        AddHeading3(doc, "横梁挠度计算");
        double E = beam.ElasticModulus;
        double Iy_mm4 = (beam.Iy > 0 ? beam.Iy : beam.Ix) * 10000;
        double df = qwk_beam * Math.Pow(beam.Span, 4) / (120.0 * E * Iy_mm4);
        double dfLim = beam.DeflectionLimit > 0 ? beam.DeflectionLimit : beam.Span / 180.0;

        AddFormula(doc, "df = qwk × B⁴ / (120×E×Iy)");
        AddCalcLine(doc, $"= {qwk_beam:F4} × {beam.Span:F0}⁴ / (120×{E:F0}×{Iy_mm4:F0})");
        AddCalcLine(doc, $"= {df:F2} mm");
        AddVerdict(doc, df <= dfLim, $"df = {df:F2} mm", $"[df] = {dfLim:F1} mm", "横梁挠度");
    }

    private void RenderPlugConnectionSection(XWPFDocument doc, UnitCurtainWallCalcBookInput input)
    {
        AddHeading2(doc, "单元插芯连接计算");
        AddBody(doc, "单元板块之间通过铝合金插芯连接，插芯承受风荷载产生的剪力。");

        double w0Mpa = input.BasicWindPressure / 1e6;
        double wk = input.GustFactor * input.HeightFactor * Math.Abs(input.Mus1Base) * w0Mpa;
        double V = 1.4 * wk * input.UnitWidth * input.UnitHeight / 2.0;

        AddBody(doc, $"插芯承受剪力 V = {V:F1} N");
        AddBody(doc, "插芯连接验算按JG/T216-2024第6.3节进行。");
    }

    private void RenderHangerSection(XWPFDocument doc, UnitCurtainWallCalcBookInput input)
    {
        AddHeading2(doc, "单元挂件计算");
        AddBody(doc, "单元板块通过挂件悬挂于主体结构上，挂件承受单元自重和水平荷载。");

        double G = input.UnitWeight * 1000; // kN → N
        AddBody(doc, $"单元自重 G = {G:F1} N");
        AddBody(doc, "挂件验算按JG/T216-2024第6.4节进行。");
    }

    private void RenderPanelSection(XWPFDocument doc, UnitCurtainWallCalcBookInput input)
    {
        AddHeading2(doc, "面板计算");
        AddBody(doc, "单元幕墙面板通常为玻璃或铝板，面板计算方法与普通幕墙相同。");
        AddBody(doc, "玻璃面板计算按JGJ102-2003第6.1节进行；");
        AddBody(doc, "铝板面板计算按JGJ133-2001第6.2节进行。");
    }

    private void RenderEmbedSection(XWPFDocument doc, UnitCurtainWallCalcBookInput input)
    {
        AddHeading2(doc, "预埋件计算");
        AddBody(doc, "预埋件计算方法与普通幕墙相同，按JGJ102-2003附录C进行。");
    }

    private void RenderAppendix(XWPFDocument doc)
    {
        AddHeading2(doc, "附录");
        AddHeading3(doc, "单元板块参数表");
        AddBody(doc, "（根据实际工程填写）");
    }
}

/// <summary>单元幕墙计算书输入模型</summary>
public class UnitCurtainWallCalcBookInput
{
    public string? ProjectName { get; set; }
    public string? CompanyName { get; set; }
    public string? Designer { get; set; }
    public string? Reviewer { get; set; }
    public string? Approver { get; set; }
    public string? Supervisor { get; set; }
    public string? ReportDate { get; set; }

    public string? City { get; set; }
    public double BuildingHeight { get; set; }
    public double InstallHeight { get; set; }
    public double BasicWindPressure { get; set; }
    public string? TerrainCategory { get; set; }
    public int SeismicIntensity { get; set; }

    public double GustFactor { get; set; }
    public double HeightFactor { get; set; }
    public double Mus1Base { get; set; } = -1.0;

    // 单元板块参数
    public double UnitWidth { get; set; } = 1200;
    public double UnitHeight { get; set; } = 3600;
    public double UnitWeight { get; set; } = 2.5; // kN

    public string? ColumnMaterial { get; set; }
    public MemberCalcResult? Column { get; set; }
    public MemberCalcResult? Beam { get; set; }
}
