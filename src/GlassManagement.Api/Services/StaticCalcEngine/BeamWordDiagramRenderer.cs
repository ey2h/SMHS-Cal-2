using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace GlassManagement.Api.Services.StaticCalcEngine;

[SupportedOSPlatform("windows")]
public static class BeamWordDiagramRenderer
{
    public static byte[] RenderMechanicalDiagram(BeamCalcRequest request, int width = 900, int height = 280)
    {
        using var bmp = new Bitmap(width, height);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.White);

        var left = 70f;
        var right = width - 50f;
        var y = height * 0.55f;
        var span = right - left;

        using var beamPen = new Pen(Color.FromArgb(31, 41, 55), 4);
        g.DrawLine(beamPen, left, y, right, y);

        switch (request.Support)
        {
            case SupportCondition.Cantilever:
                DrawFixed(g, left, y);
                break;
            case SupportCondition.DoubleSpan:
                DrawPin(g, left, y);
                DrawRoller(g, left + span * 0.5f, y);
                DrawRoller(g, right, y);
                break;
            case SupportCondition.ContinuousThreeSpan:
                DrawPin(g, left, y);
                DrawRoller(g, left + span / 3f, y);
                DrawRoller(g, left + 2f * span / 3f, y);
                DrawRoller(g, right, y);
                break;
            case SupportCondition.MultiSpanHingedStatical:
            case SupportCondition.MultiSpanHingedIndeterminate:
                DrawPin(g, left, y);
                DrawRoller(g, left + span / 3f, y);
                DrawRoller(g, left + 2f * span / 3f, y);
                DrawRoller(g, right, y);
                DrawHinge(g, left + span / 3f, y);
                DrawHinge(g, left + 2f * span / 3f, y);
                break;
            case SupportCondition.OverhangBeam:
                DrawPin(g, left, y);
                DrawRoller(g, left + span * 0.7f, y);
                break;
            default:
                DrawPin(g, left, y);
                DrawRoller(g, right, y);
                break;
        }

        DrawLoad(g, request.Load, left, right, y - 8);

        using var titleFont = new Font("Microsoft YaHei", 12, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.FromArgb(17, 24, 39));
        g.DrawString("受力简图", titleFont, textBrush, new PointF(width / 2f - 35, 12));

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    public static byte[] RenderCurveDiagram(string title, IReadOnlyList<DiagramPoint> points, Color curveColor, int width = 900, int height = 280)
    {
        using var bmp = new Bitmap(width, height);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.White);

        var left = 70f;
        var right = width - 40f;
        var top = 44f;
        var bottom = height - 36f;
        var yMid = (top + bottom) / 2f;

        using var axisPen = new Pen(Color.FromArgb(156, 163, 175), 1.5f);
        g.DrawLine(axisPen, left, yMid, right, yMid);
        g.DrawLine(axisPen, left, top, left, bottom);

        using var titleFont = new Font("Microsoft YaHei", 11, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.FromArgb(17, 24, 39));
        g.DrawString(title, titleFont, textBrush, new PointF(width / 2f - 45, 12));

        if (points.Count > 1)
        {
            var minX = points.Min(p => p.X);
            var maxX = points.Max(p => p.X);
            var minY = points.Min(p => p.Value);
            var maxY = points.Max(p => p.Value);
            var spanX = Math.Max(maxX - minX, 1e-6);
            var amp = Math.Max(Math.Abs(minY), Math.Abs(maxY));
            if (amp < 1e-9) amp = 1;

            var pts = points
                .Select(p => new PointF(
                    left + (float)((p.X - minX) / spanX) * (right - left),
                    yMid - (float)(p.Value / amp) * (bottom - top) * 0.42f))
                .ToArray();

            using var curvePen = new Pen(curveColor, 2.2f);
            g.DrawLines(curvePen, pts);
        }

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    private static void DrawLoad(Graphics g, LoadType load, float left, float right, float y)
    {
        using var pen = new Pen(Color.FromArgb(220, 38, 38), 1.4f);
        using var brush = new SolidBrush(Color.FromArgb(220, 38, 38));

        int n = 12;
        for (int i = 0; i <= n; i++)
        {
            float x = left + (right - left) * i / n;
            float yTop = load switch
            {
                LoadType.Triangular => y - 38f * (i / (float)n),
                LoadType.Trapezoidal => y - (14f + 24f * (i / (float)n)),
                _ => y - 34f
            };
            g.DrawLine(pen, x, yTop, x, y);
            g.FillPolygon(brush, new[] { new PointF(x - 3, y - 6), new PointF(x + 3, y - 6), new PointF(x, y) });
        }
    }

    private static void DrawPin(Graphics g, float x, float y)
    {
        using var pen = new Pen(Color.FromArgb(17, 24, 39), 2);
        g.DrawPolygon(pen, new[] { new PointF(x, y), new PointF(x - 14, y + 18), new PointF(x + 14, y + 18) });
        g.DrawLine(pen, x - 18, y + 23, x + 18, y + 23);
    }

    private static void DrawRoller(Graphics g, float x, float y)
    {
        DrawPin(g, x, y);
        using var pen = new Pen(Color.FromArgb(17, 24, 39), 1.6f);
        g.DrawEllipse(pen, x - 9, y + 24, 6, 6);
        g.DrawEllipse(pen, x + 3, y + 24, 6, 6);
    }

    private static void DrawFixed(Graphics g, float x, float y)
    {
        using var pen = new Pen(Color.FromArgb(17, 24, 39), 3);
        g.DrawLine(pen, x, y - 28, x, y + 28);
        using var hatch = new Pen(Color.FromArgb(17, 24, 39), 1.2f);
        for (int i = -28; i <= 26; i += 8)
        {
            g.DrawLine(hatch, x, y + i, x - 14, y + i + 8);
        }
    }

    private static void DrawHinge(Graphics g, float x, float y)
    {
        using var pen = new Pen(Color.FromArgb(17, 24, 39), 1.8f);
        using var brush = new SolidBrush(Color.White);
        g.FillEllipse(brush, x - 5, y - 5, 10, 10);
        g.DrawEllipse(pen, x - 5, y - 5, 10, 10);
    }
}
