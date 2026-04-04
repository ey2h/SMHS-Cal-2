using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace GlassManagement.Api.Services.StaticCalcEngine;

public static class StrengthDiagramRenderer
{
    public static byte[] Render(string title, double actual, double limit, string unit, int width = 900, int height = 220)
    {
        using var bmp = new Bitmap(width, height);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.White);

        using var titleFont = new Font("Microsoft YaHei", 13, FontStyle.Bold);
        using var bodyFont = new Font("Microsoft YaHei", 10.5f, FontStyle.Regular);
        using var axisPen = new Pen(Color.FromArgb(156, 163, 175), 1.4f);
        using var linePen = new Pen(Color.FromArgb(37, 99, 235), 16);
        using var passBrush = new SolidBrush(Color.FromArgb(22, 163, 74));
        using var failBrush = new SolidBrush(Color.FromArgb(220, 38, 38));
        using var textBrush = new SolidBrush(Color.FromArgb(17, 24, 39));

        g.DrawString(title, titleFont, textBrush, new PointF(18, 12));

        var left = 70f;
        var right = width - 70f;
        var midY = 106f;
        g.DrawLine(axisPen, left, midY, right, midY);

        var ratio = limit <= 0 ? 0 : actual / limit;
        var barWidth = (right - left) * (float)Math.Min(Math.Max(ratio, 0), 1.0);
        var barBrush = actual <= limit ? passBrush : failBrush;
        g.FillRectangle(barBrush, left, midY - 16, Math.Max(4, barWidth), 32);

        var limitX = left + (right - left) * 0.88f;
        using var limitPen = new Pen(Color.FromArgb(245, 158, 11), 2);
        g.DrawLine(limitPen, limitX, midY - 28, limitX, midY + 28);
        g.DrawString("限值", bodyFont, textBrush, new PointF(limitX - 12, midY + 34));

        var ratioText = $"{actual:F2} / {limit:F2} {unit}";
        g.DrawString(ratioText, bodyFont, textBrush, new PointF(18, 56));
        g.DrawString($"利用率：{(limit <= 0 ? 0 : ratio):F2}", bodyFont, textBrush, new PointF(18, 74));
        g.DrawString(actual <= limit ? "满足要求" : "不满足要求", bodyFont, actual <= limit ? passBrush : failBrush, new PointF(18, 92));

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}
