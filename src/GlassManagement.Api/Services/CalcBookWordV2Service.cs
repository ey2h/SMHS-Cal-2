using System.Drawing;
using GlassManagement.Api.Services.StaticCalcEngine;
using NPOI.OpenXmlFormats.Wordprocessing;
using NPOI.Util;
using NPOI.WP.UserModel;
using NPOI.XWPF.UserModel;

namespace GlassManagement.Api.Services;

public class CalcBookWordV2Service
{
    private const string MainFont = "Times New Roman";
    private const string EastAsiaFont = "宋体";

    private int _h2Counter;
    private int _h3Counter;

    public byte[] GenerateGlassCurtainWallReport(GlassCalcBookInput input)
    {
        _h2Counter = 0;
        _h3Counter = 0;

        using var doc = new XWPFDocument();
        SetupPage(doc);
        CreateHeadingStyles(doc);

        RenderCover(doc, input);
        AddPageBreak(doc);

        RenderToc(doc, input);
        AddPageBreak(doc);

        var mode = ResolveReportMode(input);
        AddMainTitle(doc, GetReportTitle(mode));

        RenderStandards(doc);
        RenderBasicParameters(doc, input, mode);
        RenderWindLoad(doc, input);

        if (input.GlassCalc != null)
        {
            RenderGlassSection(doc, input);
        }

        if (input.Column != null)
        {
            RenderMemberSection(doc, input, input.Column, true);
        }

        if (input.Beam != null)
        {
            RenderMemberSection(doc, input, input.Beam, false);
        }

        RenderConclusion(doc, input);
        SetHeaderFooter(doc, input.ProjectName ?? string.Empty, GetReportTitle(mode));

        using var ms = new MemoryStream();
        doc.Write(ms);
        return ms.ToArray();
    }

    private static string ResolveReportMode(GlassCalcBookInput input)
    {
        if (input.Column != null && input.Beam != null) return "full";
        if (input.Column != null) return "column";
        if (input.Beam != null) return "beam";
        return "glass";
    }

    private static string GetReportTitle(string mode) => mode switch
    {
        "column" => "立柱专项计算书",
        "beam" => "横梁专项计算书",
        "glass" => "玻璃专项计算书",
        _ => "全隐框玻璃幕墙设计计算书"
    };

    private void CreateHeadingStyles(XWPFDocument doc)
    {
        var styles = doc.GetStyles();
        if (styles == null) return;
        TryCreateStyle(styles, "Heading1", 16, true);
        TryCreateStyle(styles, "Heading2", 14, true);
        TryCreateStyle(styles, "Heading3", 12, true);
    }

    private static void TryCreateStyle(XWPFStyles styles, string styleId, int fontSize, bool bold)
    {
        if (styles.GetStyle(styleId) != null) return;

        var style = new CT_Style
        {
            type = ST_StyleType.paragraph,
            styleId = styleId,
            name = new CT_String { val = styleId }
        };

        var rPr = new CT_RPr
        {
            sz = new CT_HpsMeasure { val = (ulong)(fontSize * 2) },
            szCs = new CT_HpsMeasure { val = (ulong)(fontSize * 2) },
            rFonts = new CT_Fonts { ascii = EastAsiaFont, hAnsi = EastAsiaFont, eastAsia = EastAsiaFont }
        };
        if (bold) rPr.b = new CT_OnOff();
        style.rPr = rPr;
        styles.AddStyle(new XWPFStyle(style));
    }

    private void SetupPage(XWPFDocument doc)
    {
        var body = doc.Document.body;
        body.sectPr ??= new CT_SectPr();
        body.sectPr.pgSz ??= new CT_PageSz();
        body.sectPr.pgMar ??= new CT_PageMar();
        body.sectPr.pgSz.w = 11906;
        body.sectPr.pgSz.h = 16838;
        body.sectPr.pgMar.top = 1350;
        body.sectPr.pgMar.bottom = 1080;
        body.sectPr.pgMar.left = 1350;
        body.sectPr.pgMar.right = 1350;
    }

    private void SetHeaderFooter(XWPFDocument doc, string projectName, string reportTitle)
    {
        var header = doc.CreateHeader(HeaderFooterType.DEFAULT);
        var hp = header.CreateParagraph();
        hp.Alignment = ParagraphAlignment.LEFT;
        var hr = hp.CreateRun();
        hr.SetText($"{projectName}          {reportTitle}");
        SetFont(hr, 9);

        var footer = doc.CreateFooter(HeaderFooterType.DEFAULT);
        var fp = footer.CreateParagraph();
        fp.Alignment = ParagraphAlignment.CENTER;
        var fr1 = fp.CreateRun();
        fr1.SetText("第 ");
        SetFont(fr1, 9);
        fp.GetCTP().AddNewFldSimple().instr = " PAGE ";
        var fr2 = fp.CreateRun();
        fr2.SetText(" / ");
        SetFont(fr2, 9);
        fp.GetCTP().AddNewFldSimple().instr = " NUMPAGES ";
        var fr3 = fp.CreateRun();
        fr3.SetText(" 页");
        SetFont(fr3, 9);
    }

    private void SetFont(XWPFRun run, int fontSize, bool bold = false)
    {
        run.FontFamily = MainFont;
        run.FontSize = fontSize;
        run.IsBold = bold;
        var rPr = run.GetCTR().AddNewRPr();
        rPr.rFonts ??= new CT_Fonts();
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
        p.Alignment = ParagraphAlignment.CENTER;
        p.SpacingBefore = 160;
        p.SpacingAfter = 180;
        p.Style = "Heading1";
        var r = p.CreateRun();
        r.SetText(text);
        SetFont(r, 16, true);
    }

    private void AddHeading2(XWPFDocument doc, string text)
    {
        _h2Counter++;
        _h3Counter = 0;
        var p = doc.CreateParagraph();
        p.SpacingBefore = 180;
        p.SpacingAfter = 80;
        p.Style = "Heading2";
        var r = p.CreateRun();
        r.SetText($"{_h2Counter} {text}");
        SetFont(r, 14, true);
    }

    private void AddHeading3(XWPFDocument doc, string text)
    {
        _h3Counter++;
        var p = doc.CreateParagraph();
        p.SpacingBefore = 80;
        p.SpacingAfter = 50;
        p.Style = "Heading3";
        var r = p.CreateRun();
        r.SetText($"{_h2Counter}.{_h3Counter} {text}");
        SetFont(r, 12, true);
    }

    private void AddBody(XWPFDocument doc, string text, bool indent = false)
    {
        var p = doc.CreateParagraph();
        if (indent) p.IndentationFirstLine = 420;
        var r = p.CreateRun();
        r.SetText(text);
        SetFont(r, 11);
    }

    private void AddFormula(XWPFDocument doc, string formula, string? reference = null)
    {
        var p = doc.CreateParagraph();
        p.IndentationFirstLine = 420;
        var r = p.CreateRun();
        r.SetText(reference == null ? formula : $"{formula}    〔{reference}〕");
        SetFont(r, 11);
    }

    private void AddCalcLine(XWPFDocument doc, string text)
    {
        var p = doc.CreateParagraph();
        p.IndentationFirstLine = 680;
        var r = p.CreateRun();
        r.SetText(text);
        SetFont(r, 11);
    }

    private void AddVerdict(XWPFDocument doc, bool pass, string actual, string limit, string item)
    {
        AddBody(doc, $"{actual} {(pass ? "≤" : ">")} {limit}，{item}{(pass ? "满足要求" : "不满足要求")}。");
    }

    private void AddImage(XWPFDocument doc, byte[] imageBytes, string caption)
    {
        var p = doc.CreateParagraph();
        p.Alignment = ParagraphAlignment.CENTER;
        p.SpacingBefore = 80;
        p.SpacingAfter = 40;

        var run = p.CreateRun();
        using var ms = new MemoryStream(imageBytes);
        run.AddPicture(ms, (int)PictureType.PNG, $"{caption}.png", Units.ToEMU(520), Units.ToEMU(170));

        var cp = doc.CreateParagraph();
        cp.Alignment = ParagraphAlignment.CENTER;
        cp.SpacingAfter = 80;
        var cr = cp.CreateRun();
        cr.SetText(caption);
        SetFont(cr, 10);
    }

    private void RenderCover(XWPFDocument doc, GlassCalcBookInput input)
    {
        for (var i = 0; i < 6; i++) doc.CreateParagraph();
        AddCenter(doc, input.ProjectName ?? "幕墙工程", 22, true);
        AddCenter(doc, GetReportTitle(ResolveReportMode(input)), 26, true);
        AddCenter(doc, "（结构计算书）", 14, false);
        for (var i = 0; i < 7; i++) doc.CreateParagraph();
        AddCenter(doc, input.CompanyName ?? "编制单位", 14, false);
        AddCenter(doc, DateTime.Now.ToString("yyyy-MM-dd"), 13, false);
    }

    private void RenderToc(XWPFDocument doc, GlassCalcBookInput input)
    {
        AddCenter(doc, "目录", 20, true);
        var mode = ResolveReportMode(input);
        foreach (var line in BuildToc(mode))
        {
            AddBody(doc, line);
        }
    }

    private static IEnumerable<string> BuildToc(string mode)
    {
        var list = new List<string>
        {
            "1. 计算依据与标准",
            "2. 基本参数",
            "3. 荷载与组合"
        };

        if (mode is "glass" or "full") list.Add("4. 玻璃验算");
        if (mode is "column" or "full") list.Add("5. 立柱验算");
        if (mode is "beam" or "full") list.Add("6. 横梁验算");
        list.Add("7. 结论");
        return list;
    }

    private void RenderStandards(XWPFDocument doc)
    {
        AddHeading2(doc, "计算依据与规范");
        AddBody(doc, "幕墙与结构设计采用常用国家及行业标准，公式引用在各小节中单独标注。");
        AddBody(doc, "主要依据：JGJ 102-2003、GB 50009-2012、GB 50017-2017、GB/T 21086-2007、GB/T 7106-2019。");
    }

    private void RenderBasicParameters(XWPFDocument doc, GlassCalcBookInput input, string mode)
    {
        AddHeading2(doc, "基本参数");
        AddBody(doc, $"工程名称：{input.ProjectName ?? "未命名"}");
        AddBody(doc, $"工程地点：{input.City ?? "未提供"}");
        AddBody(doc, $"计算模式：{GetReportTitle(mode)}");
        AddBody(doc, $"分格尺寸：{input.PanelWidth:F0} × {input.PanelHeight:F0} mm");
        AddBody(doc, $"基本风压：{input.BasicWindPressure:F2} Pa");
        AddBody(doc, $"地面粗糙度：{input.TerrainCategory ?? "未提供"}");
        AddBody(doc, $"抗震设防烈度：{input.SeismicIntensity} 度");
        AddBody(doc, $"玻璃配置：{input.GlassDescription ?? "未提供"}");
    }

    private void RenderWindLoad(XWPFDocument doc, GlassCalcBookInput input)
    {
        AddHeading2(doc, "荷载计算");
        var w0 = input.BasicWindPressure / 1000.0;
        var wk = input.GustFactor * input.ShapeFactor * input.HeightFactor * w0;
        AddFormula(doc, "wk = βgz × μs1 × μz × w0", "GB 50009-2012");
        AddCalcLine(doc, $"= {input.GustFactor:F2} × {input.ShapeFactor:F2} × {input.HeightFactor:F2} × {w0:F3} kPa");
        AddCalcLine(doc, $"= {wk:F3} kPa");
    }

    private void RenderGlassSection(XWPFDocument doc, GlassCalcBookInput input)
    {
        AddHeading2(doc, "玻璃验算");
        var g = input.GlassCalc!;
        AddBody(doc, "本章按玻璃板块强度与挠度控制值进行判定。");
        AddFormula(doc, "σmax = αq · q · a² / t²", "JGJ 102-2003");
        AddCalcLine(doc, $"= {g.StressCoeff:F4} × {g.LoadDesign / 1000.0:F3} × {g.ShortSide:F0}² / {g.EquivalentThickness:F2}²");
        AddCalcLine(doc, $"= {g.MaxStress:F2} MPa");
        AddVerdict(doc, g.MaxStress <= g.DesignStrength, $"σmax = {g.MaxStress:F2} MPa", $"fg = {g.DesignStrength:F2} MPa", "玻璃强度");

        AddFormula(doc, "fmax = αd · q · a⁴ / (E · t³)", "JGJ 102-2003");
        AddCalcLine(doc, $"= {g.DeflectionCoeff:F4} × {g.LoadDesign / 1000.0:F3} × ...");
        AddCalcLine(doc, $"= {g.MaxDeflection:F2} mm");
        AddVerdict(doc, g.MaxDeflection <= g.DeflectionLimit, $"fmax = {g.MaxDeflection:F2} mm", $"[f] = {g.DeflectionLimit:F2} mm", "玻璃挠度");
    }

    private void RenderMemberSection(XWPFDocument doc, GlassCalcBookInput input, MemberCalcResult member, bool isColumn)
    {
        var title = isColumn ? "立柱验算" : "横梁验算";
        AddHeading2(doc, title);
        AddBody(doc, $"型材：{member.ProfileCode}");
        AddBody(doc, $"跨长：{member.Span:F0} mm");
        AddBody(doc, $"允许应力：{member.AllowableStress:F2} MPa");
        AddBody(doc, $"允许挠度：{member.DeflectionLimit:F2} mm");

        var req = BuildBeamRequest(input, member, isColumn);
        var res = new StaticCalcService().Calculate(req);
        AddHeading3(doc, "受力简图与内力图");
        AddImage(doc, BeamWordDiagramRenderer.RenderMechanicalDiagram(req), $"{title}受力简图");
        AddImage(doc, BeamWordDiagramRenderer.RenderCurveDiagram("弯矩图", res.MomentDiagram, Color.SeaGreen), $"{title}弯矩图");
        AddImage(doc, BeamWordDiagramRenderer.RenderCurveDiagram("剪力图", res.ShearDiagram, Color.SaddleBrown), $"{title}剪力图");
        AddImage(doc, BeamWordDiagramRenderer.RenderCurveDiagram("挠度图", res.DeflectionDiagram, Color.RoyalBlue), $"{title}挠度图");

        AddHeading3(doc, "强度利用率");
        AddImage(doc, StrengthDiagramRenderer.Render("强度利用率图", member.MaxStress, member.AllowableStress, "MPa"), $"{title}强度利用率图");

        AddFormula(doc, "σ = M / W ≤ [σ]", "GB 50017-2017");
        AddCalcLine(doc, $"= {member.MaxStress:F2} / {member.AllowableStress:F2}");
        AddVerdict(doc, member.MaxStress <= member.AllowableStress, $"σ = {member.MaxStress:F2} MPa", $"[σ] = {member.AllowableStress:F2} MPa", $"{title}强度");

        AddFormula(doc, "f ≤ [f]", "JGJ 102-2003");
        AddCalcLine(doc, $"= {member.MaxDeflection:F2} / {member.DeflectionLimit:F2}");
        AddVerdict(doc, member.MaxDeflection <= member.DeflectionLimit, $"f = {member.MaxDeflection:F2} mm", $"[f] = {member.DeflectionLimit:F2} mm", $"{title}挠度");
    }

    private void RenderConclusion(XWPFDocument doc, GlassCalcBookInput input)
    {
        AddHeading2(doc, "结论");
        var checks = new List<string>();
        if (input.GlassCalc != null)
        {
            checks.Add(input.GlassCalc.MaxStress <= input.GlassCalc.DesignStrength ? "玻璃强度满足要求" : "玻璃强度不满足要求");
            checks.Add(input.GlassCalc.MaxDeflection <= input.GlassCalc.DeflectionLimit ? "玻璃挠度满足要求" : "玻璃挠度不满足要求");
        }
        if (input.Column != null)
        {
            checks.Add(input.Column.MaxStress <= input.Column.AllowableStress ? "立柱强度满足要求" : "立柱强度不满足要求");
            checks.Add(input.Column.MaxDeflection <= input.Column.DeflectionLimit ? "立柱挠度满足要求" : "立柱挠度不满足要求");
        }
        if (input.Beam != null)
        {
            checks.Add(input.Beam.MaxStress <= input.Beam.AllowableStress ? "横梁强度满足要求" : "横梁强度不满足要求");
            checks.Add(input.Beam.MaxDeflection <= input.Beam.DeflectionLimit ? "横梁挠度满足要求" : "横梁挠度不满足要求");
        }

        foreach (var line in checks)
        {
            AddBody(doc, line);
        }
    }

    private static BeamCalcRequest BuildBeamRequest(GlassCalcBookInput input, MemberCalcResult member, bool isColumn)
    {
        var supportType = (member.SupportType ?? string.Empty).ToLowerInvariant();
        var support = supportType.Contains("悬") || supportType.Contains("cantilever")
            ? SupportCondition.Cantilever
            : supportType.Contains("双") || supportType.Contains("double")
                ? SupportCondition.DoubleSpan
                : supportType.Contains("三") || supportType.Contains("three")
                    ? SupportCondition.ContinuousThreeSpan
                    : supportType.Contains("铰") || supportType.Contains("hinge")
                        ? SupportCondition.MultiSpanHingedStatical
                        : SupportCondition.SimpleBeam;

        var length = Math.Max(member.Span / 1000.0, 0.1);
        return new BeamCalcRequest
        {
            Support = support,
            Load = LoadType.Uniform,
            L = length,
            L2 = input.SpanL2 > 0 ? input.SpanL2 / 1000.0 : length,
            L3 = input.SpanL1 > 0 ? input.SpanL1 / 1000.0 : length,
            Lc = isColumn ? Math.Max(0.2 * length, 0.1) : 0.0,
            Q = Math.Max(input.WindPressureDesign / 1000.0, 0.1),
            E = member.ElasticModulus,
            I = Math.Max(member.Ix, 1e-6),
            NumPoints = 120
        };
    }

    private static void AddCenter(XWPFDocument doc, string text, int size, bool bold)
    {
        var p = doc.CreateParagraph();
        p.Alignment = ParagraphAlignment.CENTER;
        var r = p.CreateRun();
        r.SetText(text);
        r.FontSize = size;
        r.IsBold = bold;
    }
}
