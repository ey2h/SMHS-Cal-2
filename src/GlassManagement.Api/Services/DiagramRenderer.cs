using System;
using SkiaSharp;

namespace GlassManagement.Api.Services
{
    // Simple diagram renderer using SkiaSharp. Produces PNG bytes.
    public static class DiagramRenderer
    {
        public class DiagramParams
        {
            public int Width { get; set; } = 800;
            public int Height { get; set; } = 400;
            public string Title { get; set; } = "";
        }

        public static byte[] RenderToPng(string kind, DiagramParams p)
        {
            int w = Math.Max(1, p.Width);
            int h = Math.Max(1, p.Height);
            using var bitmap = new SKBitmap(w, h);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.White);

            // simple axes grid
            var axis = new SKPaint { Color = SKColors.Black, StrokeWidth = 2, IsAntialias = true };
            canvas.DrawLine(40, h - 40, w - 40, h - 40, axis);
            canvas.DrawLine(40, 20, 40, h - 40, axis);
            var grid = new SKPaint { Color = SKColors.LightGray, StrokeWidth = 1, IsAntialias = true };
            for (int x = 60; x < w - 40; x += Math.Max(40, (w - 100) / 6)) canvas.DrawLine(x, 20, x, h - 40, grid);
            for (int y = 60; y < h - 40; y += Math.Max(40, (h - 80) / 4)) canvas.DrawLine(40, y, w - 40, y, grid);

            // Title
            var textPaint = new SKPaint { Color = SKColors.Black, TextSize = 20, IsAntialias = true };
            canvas.DrawText(p.Title ?? kind, 60, 28, textPaint);

            // simple decorative curve depending on kind
            var curve = new SKPath();
            int steps = 20;
            for (int i = 0; i <= steps; i++)
            {
                float x = 40 + i * (w - 80) / steps;
                float y = h / 2f + (float)Math.Sin(i * Math.PI / 6) * (h / 6f);
                if (i == 0) curve.MoveTo(x, y); else curve.LineTo(x, y);
            }
            var curvePaint = new SKPaint { Color = SKColors.Blue, StrokeWidth = 2, Style = SKPaintStyle.Stroke, IsAntialias = true };
            canvas.DrawPath(curve, curvePaint);

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 90);
            return data.ToArray();
        }
    }
}
