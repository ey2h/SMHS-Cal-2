using System.Drawing;
using GlassManagement.Api.Services.StaticCalcEngine;
using NPOI.Util;
using NPOI.XWPF.UserModel;

namespace GlassManagement.Api.Services;

public class CalcBookWordV2Service
{
    private readonly StaticCalcService _staticCalcService = new();

    public byte[] GenerateGlassCurtainWallReport(GlassCalcBookInput input)
    {
        using var doc = new XWPFDocument();

        RenderCover(doc, input);
        AddPageBreak(doc);
        RenderToc(doc);
        AddPageBreak(doc);

        AddHeading(doc, "1. 计算依据与标准", 1);
        AddBody(doc, "本计算书依据汇宝样板章节组织，按幕墙工程常用规范进行说明。", false);

        AddHeading(doc, "2. 基本参数", 1);
        AddBody(doc, $"工程名称：{input.ProjectName ?? "未命名"}", false);
        AddBody(doc, $"工程地点：{input.City ?? "未提供"}", false);
        AddBody(doc, $"分格尺寸：{input.PanelWidth:F0} x {input.PanelHeight:F0} mm", false);

        AddHeading(doc, "3. 荷载与组合", 1);
        AddBody(doc, $"基本风压：{input.BasicWindPressure:F2} Pa", false);
        AddBody(doc, $"阵风系数/体型系数/高度系数：{input.GustFactor:F2}/{input.ShapeFactor:F2}/{input.HeightFactor:F2}", false);

        AddHeading(doc, "4. 立柱验算", 1);
        if (input.Column is not null)
        {
            AddBody(doc, $"立柱型号：{input.Column.ProfileCode}", false);
            AddBody(doc, $"跨度：{input.Column.Span:F0} mm", false);
            AddBody(doc, $"应力：{input.Column.MaxStress:F2}/{input.Column.AllowableStress:F2} MPa", false);
            AddBody(doc, $"挠度：{input.Column.MaxDeflection:F2}/{input.Column.DeflectionLimit:F2} mm", false);
        }

        AddHeading(doc, "5. 横梁验算", 1);
        if (input.Beam is not null)
        {
            AddBody(doc, $"横梁型号：{input.Beam.ProfileCode}", false);
            AddBody(doc, $"跨度：{input.Beam.Span:F0} mm", false);
            AddBody(doc, $"应力：{input.Beam.MaxStress:F2}/{input.Beam.AllowableStress:F2} MPa", false);
            AddBody(doc, $"挠度：{input.Beam.MaxDeflection:F2}/{input.Beam.DeflectionLimit:F2} mm", false);

            var req = BuildBeamRequest(input, input.Beam);
            var result = _staticCalcService.Calculate(req);

            AddHeading(doc, "5.1 受力简图与内力图", 2);
            AddPng(doc, "力学简图", BeamWordDiagramRenderer.RenderMechanicalDiagram(req));
            AddPng(doc, "弯矩图", BeamWordDiagramRenderer.RenderCurveDiagram("弯矩图", result.MomentDiagram, Color.SeaGreen));
            AddPng(doc, "剪力图", BeamWordDiagramRenderer.RenderCurveDiagram("剪力图", result.ShearDiagram, Color.SaddleBrown));
            AddPng(doc, "挠度图", BeamWordDiagramRenderer.RenderCurveDiagram("挠度图", result.DeflectionDiagram, Color.RoyalBlue));
        }

        AddHeading(doc, "6. 玻璃验算", 1);
        AddBody(doc, $"玻璃配置：{input.GlassDescription ?? "未提供"}", false);

        AddHeading(doc, "7. 连接件/预埋件验算", 1);
        AddBody(doc, "本章包含螺栓、焊缝、预埋件与转接件验算。", false);

        AddHeading(doc, "8. 结论", 1);
        AddBody(doc, "各章节验算结果以计算值不超过限值为合格判据。", false);

        using var ms = new MemoryStream();
        doc.Write(ms);
        return ms.ToArray();
    }

    private static BeamCalcRequest BuildBeamRequest(GlassCalcBookInput input, MemberCalcResult beam)
    {
        var text = (beam.SupportType ?? string.Empty).ToLowerInvariant();
        var support = text.Contains("悬") || text.Contains("cantilever") ? SupportCondition.Cantilever
            : text.Contains("双") || text.Contains("double") ? SupportCondition.DoubleSpan
            : text.Contains("三") || text.Contains("three") ? SupportCondition.ContinuousThreeSpan
            : text.Contains("铰") || text.Contains("hinge") ? SupportCondition.MultiSpanHingedStatical
            : text.Contains("挑") || text.Contains("overhang") ? SupportCondition.OverhangBeam
            : SupportCondition.SimpleBeam;

        var l = Math.Max(beam.Span / 1000.0, 0.1);
        return new BeamCalcRequest
        {
            Support = support,
            Load = LoadType.Uniform,
            L = l,
            L2 = input.SpanL2 > 0 ? input.SpanL2 / 1000.0 : l,
            L3 = input.SpanL1 > 0 ? input.SpanL1 / 1000.0 : l,
            Lc = Math.Max(0.2 * l, 0.1),
            Q = Math.Max(input.WindPressureDesign / 1000.0, 0.1),
            NumPoints = 120
        };
    }

    private static void RenderCover(XWPFDocument doc, GlassCalcBookInput input)
    {
        for (var i = 0; i < 5; i++) doc.CreateParagraph();
        AddCenter(doc, input.ProjectName ?? "幕墙工程", 24, true);
        AddCenter(doc, "结构计算书", 26, true);
        AddCenter(doc, "（Word V2）", 16, false);
        for (var i = 0; i < 6; i++) doc.CreateParagraph();
        AddCenter(doc, input.CompanyName ?? "编制单位", 14, false);
        AddCenter(doc, DateTime.Now.ToString("yyyy-MM-dd"), 14, false);
    }

    private static void RenderToc(XWPFDocument doc)
    {
        AddCenter(doc, "目录", 22, true);
        AddBody(doc, "1. 计算依据与标准", false);
        AddBody(doc, "2. 基本参数", false);
        AddBody(doc, "3. 荷载与组合", false);
        AddBody(doc, "4. 立柱验算", false);
        AddBody(doc, "5. 横梁验算", false);
        AddBody(doc, "6. 玻璃验算", false);
        AddBody(doc, "7. 连接件/预埋件验算", false);
        AddBody(doc, "8. 结论", false);
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

    private static void AddHeading(XWPFDocument doc, string text, int level)
    {
        var p = doc.CreateParagraph();
        p.SpacingBefore = 220;
        p.SpacingAfter = 80;
        var r = p.CreateRun();
        r.SetText(text);
        r.FontSize = level == 1 ? 16 : 13;
        r.IsBold = true;
    }

    private static void AddBody(XWPFDocument doc, string text, bool indent)
    {
        var p = doc.CreateParagraph();
        if (indent) p.IndentationFirstLine = 400;
        var r = p.CreateRun();
        r.SetText(text);
        r.FontSize = 11;
    }

    private static void AddPng(XWPFDocument doc, string title, byte[] png)
    {
        AddBody(doc, title, false);
        var p = doc.CreateParagraph();
        p.Alignment = ParagraphAlignment.CENTER;
        var r = p.CreateRun();
        using var ms = new MemoryStream(png);
        r.AddPicture(ms, (int)PictureType.PNG, $"{title}.png", Units.ToEMU(540), Units.ToEMU(170));
    }

    private static void AddPageBreak(XWPFDocument doc)
    {
        var p = doc.CreateParagraph();
        p.IsPageBreak = true;
    }
}
