using System.Globalization;
using System.Text;

namespace GlassManagement.Api.Services.StaticCalcEngine;

public static class BeamDiagramGenerator
{
    private const double Width = 980;
    private const double Height = 320;
    private const double MarginLeft = 80;
    private const double MarginRight = 60;

    public static void Attach(BeamCalcRequest request, BeamCalcResult result)
    {
        result.MechanicalDiagramSvg = GenerateMechanicalDiagram(request);
        result.MomentDiagramSvg = GenerateValueDiagram(
            "Bending Moment Diagram",
            "M (kN.m)",
            result.MomentDiagram,
            "#0F766E");
        result.ShearDiagramSvg = GenerateValueDiagram(
            "Shear Diagram",
            "V (kN)",
            result.ShearDiagram,
            "#B45309");
        result.DeflectionDiagramSvg = GenerateValueDiagram(
            "Deflection Diagram",
            "f (mm)",
            result.DeflectionDiagram,
            "#1D4ED8");
    }

    public static string GenerateMechanicalDiagram(BeamCalcRequest request)
    {
        var total = GetTotalLength(request);
        var spanScale = (Width - MarginLeft - MarginRight) / Math.Max(total, 1e-6);
        var beamY = 160.0;

        var sb = StartSvg("Mechanical Diagram");
        var x0 = MarginLeft;
        var xEnd = MarginLeft + total * spanScale;

        sb.AppendLine($"<line x1='{F(x0)}' y1='{F(beamY)}' x2='{F(xEnd)}' y2='{F(beamY)}' stroke='#1F2937' stroke-width='4' />");

        switch (request.Support)
        {
            case SupportCondition.SimpleBeam:
                DrawPinSupport(sb, x0, beamY);
                DrawRollerSupport(sb, xEnd, beamY);
                DrawDimension(sb, x0, xEnd, beamY + 85, "L");
                break;
            case SupportCondition.Cantilever:
                DrawFixedSupport(sb, x0, beamY);
                DrawDimension(sb, x0, xEnd, beamY + 85, "L");
                break;
            case SupportCondition.DoubleSpan:
                {
                    var x1 = x0 + request.L * spanScale;
                    DrawPinSupport(sb, x0, beamY);
                    DrawRollerSupport(sb, x1, beamY);
                    DrawRollerSupport(sb, xEnd, beamY);
                    DrawDimension(sb, x0, x1, beamY + 85, "L1");
                    DrawDimension(sb, x1, xEnd, beamY + 85, "L2");
                    break;
                }
            case SupportCondition.ContinuousThreeSpan:
                {
                    var x1 = x0 + request.L * spanScale;
                    var x2 = x1 + request.L2 * spanScale;
                    DrawPinSupport(sb, x0, beamY);
                    DrawRollerSupport(sb, x1, beamY);
                    DrawRollerSupport(sb, x2, beamY);
                    DrawRollerSupport(sb, xEnd, beamY);
                    DrawDimension(sb, x0, x1, beamY + 85, "L1");
                    DrawDimension(sb, x1, x2, beamY + 85, "L2");
                    DrawDimension(sb, x2, xEnd, beamY + 85, "L3");
                    break;
                }
            case SupportCondition.MultiSpanHingedStatical:
            case SupportCondition.MultiSpanHingedIndeterminate:
                {
                    var l1 = Math.Max(request.L, 1);
                    var l2 = request.L2 > 0 ? request.L2 : l1;
                    var l3 = request.L3 > 0 ? request.L3 : l1;
                    total = l1 + l2 + l3;
                    spanScale = (Width - MarginLeft - MarginRight) / total;
                    x0 = MarginLeft;
                    xEnd = MarginLeft + total * spanScale;
                    var x1 = x0 + l1 * spanScale;
                    var x2 = x1 + l2 * spanScale;
                    sb.AppendLine($"<line x1='{F(x0)}' y1='{F(beamY)}' x2='{F(xEnd)}' y2='{F(beamY)}' stroke='#1F2937' stroke-width='4' />");
                    DrawPinSupport(sb, x0, beamY);
                    DrawRollerSupport(sb, x1, beamY);
                    DrawRollerSupport(sb, x2, beamY);
                    DrawRollerSupport(sb, xEnd, beamY);
                    DrawHinge(sb, x1, beamY);
                    DrawHinge(sb, x2, beamY);
                    DrawDimension(sb, x0, x1, beamY + 85, "L1");
                    DrawDimension(sb, x1, x2, beamY + 85, "L2");
                    DrawDimension(sb, x2, xEnd, beamY + 85, "L3");
                    break;
                }
            case SupportCondition.OverhangBeam:
                {
                    var main = Math.Max(request.L, 1);
                    var overhang = request.Lc > 0 ? request.Lc : Math.Max(main * 0.25, 0.5);
                    total = main + overhang;
                    spanScale = (Width - MarginLeft - MarginRight) / total;
                    x0 = MarginLeft;
                    var xMain = x0 + main * spanScale;
                    xEnd = xMain + overhang * spanScale;
                    sb.AppendLine($"<line x1='{F(x0)}' y1='{F(beamY)}' x2='{F(xEnd)}' y2='{F(beamY)}' stroke='#1F2937' stroke-width='4' />");
                    DrawPinSupport(sb, x0, beamY);
                    DrawRollerSupport(sb, xMain, beamY);
                    DrawDimension(sb, x0, xMain, beamY + 85, "L");
                    DrawDimension(sb, xMain, xEnd, beamY + 85, "Lc");
                    break;
                }
        }

        DrawLoadSymbol(sb, request, x0, xEnd, beamY - 8);
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    public static string GenerateValueDiagram(string title, string valueLabel, IReadOnlyList<DiagramPoint> points, string color)
    {
        var sb = StartSvg(title);
        var xStart = MarginLeft;
        var xEnd = Width - MarginRight;
        var yMid = Height * 0.56;

        sb.AppendLine($"<line x1='{F(xStart)}' y1='{F(yMid)}' x2='{F(xEnd)}' y2='{F(yMid)}' stroke='#9CA3AF' stroke-width='1.5' />");
        sb.AppendLine($"<line x1='{F(xStart)}' y1='35' x2='{F(xStart)}' y2='{F(Height - 24)}' stroke='#9CA3AF' stroke-width='1.5' />");
        sb.AppendLine($"<text x='{F(xStart + 8)}' y='28' font-size='12' fill='#374151'>{valueLabel}</text>");

        if (points.Count > 1)
        {
            var minX = points.Min(p => p.X);
            var maxX = points.Max(p => p.X);
            var minY = points.Min(p => p.Value);
            var maxY = points.Max(p => p.Value);
            var spanX = Math.Max(maxX - minX, 1e-6);
            var amplitude = Math.Max(Math.Abs(maxY), Math.Abs(minY));
            var yScale = amplitude < 1e-9 ? 0 : 95.0 / amplitude;

            var polyline = new StringBuilder();
            foreach (var p in points)
            {
                var x = xStart + (p.X - minX) / spanX * (xEnd - xStart);
                var y = yMid - p.Value * yScale;
                polyline.Append($"{F(x)},{F(y)} ");
            }

            sb.AppendLine($"<polyline fill='none' stroke='{color}' stroke-width='2.2' points='{polyline}' />");
            sb.AppendLine($"<text x='{F(xEnd - 100)}' y='28' font-size='12' fill='#374151'>max: {F(maxY)}</text>");
            sb.AppendLine($"<text x='{F(xEnd - 100)}' y='44' font-size='12' fill='#374151'>min: {F(minY)}</text>");
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static StringBuilder StartSvg(string title)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<svg xmlns='http://www.w3.org/2000/svg' width='{Width}' height='{Height}' viewBox='0 0 {Width} {Height}'>");
        sb.AppendLine($"<rect x='0' y='0' width='{Width}' height='{Height}' fill='white' />");
        sb.AppendLine($"<text x='{F(Width / 2)}' y='26' text-anchor='middle' font-size='18' font-weight='700' fill='#111827'>{title}</text>");
        return sb;
    }

    private static void DrawLoadSymbol(StringBuilder sb, BeamCalcRequest request, double xStart, double xEnd, double y)
    {
        switch (request.Load)
        {
            case LoadType.Uniform:
                DrawUniformLoad(sb, xStart, xEnd, y, request.Q);
                break;
            case LoadType.Triangular:
                DrawTriangularLoad(sb, xStart, xEnd, y, request.Q);
                break;
            case LoadType.Trapezoidal:
                DrawTrapezoidLoad(sb, xStart, xEnd, y, request.Q1, request.Q2);
                break;
            case LoadType.Concentrated:
            case LoadType.SymmetricConcentrated:
            case LoadType.MultipleConcentrated:
                DrawPointLoads(sb, request, xStart, xEnd, y);
                break;
            case LoadType.Moment:
            case LoadType.DoubleMoment:
                DrawMomentLoad(sb, request, xStart, xEnd, y + 6);
                break;
            case LoadType.PartialUniform:
                {
                    var total = Math.Max(GetTotalLength(request), 1e-6);
                    var x1 = xStart + (Math.Clamp(request.LoadStart, 0, total) / total) * (xEnd - xStart);
                    var x2 = xStart + (Math.Clamp(request.LoadEnd <= 0 ? total : request.LoadEnd, 0, total) / total) * (xEnd - xStart);
                    DrawUniformLoad(sb, Math.Min(x1, x2), Math.Max(x1, x2), y, request.Q);
                    break;
                }
            default:
                DrawUniformLoad(sb, xStart, xEnd, y, request.Q);
                break;
        }
    }

    private static void DrawUniformLoad(StringBuilder sb, double x1, double x2, double y, double q)
    {
        sb.AppendLine($"<rect x='{F(x1)}' y='{F(y - 46)}' width='{F(x2 - x1)}' height='40' fill='#FEE2E2' stroke='#DC2626' stroke-width='1' />");
        for (var i = 0; i <= 14; i++)
        {
            var x = x1 + (x2 - x1) * i / 14.0;
            DrawDownArrow(sb, x, y - 46, y - 3, "#DC2626");
        }
        sb.AppendLine($"<text x='{F((x1 + x2) / 2)}' y='{F(y - 54)}' text-anchor='middle' font-size='12' fill='#991B1B'>q={F(q)}</text>");
    }

    private static void DrawTriangularLoad(StringBuilder sb, double x1, double x2, double y, double q)
    {
        sb.AppendLine($"<polygon points='{F(x1)},{F(y - 5)} {F(x2)},{F(y - 46)} {F(x2)},{F(y - 5)}' fill='#FEE2E2' stroke='#DC2626' />");
        for (var i = 0; i <= 12; i++)
        {
            var x = x1 + (x2 - x1) * i / 12.0;
            var t = (x - x1) / Math.Max(x2 - x1, 1e-6);
            var yTop = y - 5 - 41 * t;
            DrawDownArrow(sb, x, yTop, y - 3, "#DC2626");
        }
        sb.AppendLine($"<text x='{F(x2 - 20)}' y='{F(y - 54)}' text-anchor='end' font-size='12' fill='#991B1B'>q={F(q)}</text>");
    }

    private static void DrawTrapezoidLoad(StringBuilder sb, double x1, double x2, double y, double q1, double q2)
    {
        sb.AppendLine($"<polygon points='{F(x1)},{F(y - 20)} {F(x2)},{F(y - 46)} {F(x2)},{F(y - 5)} {F(x1)},{F(y - 5)}' fill='#FEE2E2' stroke='#DC2626' />");
        for (var i = 0; i <= 12; i++)
        {
            var x = x1 + (x2 - x1) * i / 12.0;
            var t = (x - x1) / Math.Max(x2 - x1, 1e-6);
            var yTop = (y - 20) * (1 - t) + (y - 46) * t;
            DrawDownArrow(sb, x, yTop, y - 3, "#DC2626");
        }
        sb.AppendLine($"<text x='{F(x1 + 8)}' y='{F(y - 54)}' font-size='12' fill='#991B1B'>q1={F(q1)}</text>");
        sb.AppendLine($"<text x='{F(x2 - 8)}' y='{F(y - 54)}' text-anchor='end' font-size='12' fill='#991B1B'>q2={F(q2)}</text>");
    }

    private static void DrawPointLoads(StringBuilder sb, BeamCalcRequest request, double x1, double x2, double y)
    {
        var total = Math.Max(GetTotalLength(request), 1e-6);
        if (request.Load == LoadType.SymmetricConcentrated)
        {
            var xa = x1 + 0.25 * (x2 - x1);
            var xb = x1 + 0.75 * (x2 - x1);
            DrawDownArrow(sb, xa, y - 56, y - 3, "#DC2626", true);
            DrawDownArrow(sb, xb, y - 56, y - 3, "#DC2626", true);
            sb.AppendLine($"<text x='{F((x1 + x2) / 2)}' y='{F(y - 62)}' text-anchor='middle' font-size='12' fill='#991B1B'>2P={F(request.P)}</text>");
            return;
        }

        if (request.Load == LoadType.MultipleConcentrated)
        {
            var count = Math.Clamp(request.ConcentratedCount, 2, 10);
            for (var i = 1; i <= count; i++)
            {
                var x = x1 + i * (x2 - x1) / (count + 1.0);
                DrawDownArrow(sb, x, y - 52, y - 3, "#DC2626", true);
            }
            sb.AppendLine($"<text x='{F((x1 + x2) / 2)}' y='{F(y - 62)}' text-anchor='middle' font-size='12' fill='#991B1B'>nP, P={F(request.P)}</text>");
            return;
        }

        var a = request.A > 0 ? request.A : total / 2.0;
        var xLoad = x1 + (Math.Clamp(a, 0, total) / total) * (x2 - x1);
        DrawDownArrow(sb, xLoad, y - 56, y - 3, "#DC2626", true);
        sb.AppendLine($"<text x='{F(xLoad)}' y='{F(y - 62)}' text-anchor='middle' font-size='12' fill='#991B1B'>P={F(request.P)}</text>");
    }

    private static void DrawMomentLoad(StringBuilder sb, BeamCalcRequest request, double x1, double x2, double y)
    {
        if (request.Load == LoadType.DoubleMoment)
        {
            DrawMomentArrow(sb, x1 + 20, y, request.Ma);
            DrawMomentArrow(sb, x2 - 20, y, request.Mb);
            return;
        }

        var total = Math.Max(GetTotalLength(request), 1e-6);
        var a = request.A > 0 ? request.A : total / 2.0;
        var x = x1 + (Math.Clamp(a, 0, total) / total) * (x2 - x1);
        DrawMomentArrow(sb, x, y, request.M0);
    }

    private static void DrawMomentArrow(StringBuilder sb, double x, double y, double m)
    {
        var clockwise = m >= 0;
        var sweep = clockwise ? 1 : 0;
        sb.AppendLine($"<path d='M {F(x - 18)} {F(y)} A 18 18 0 1 {sweep} {F(x + 18)} {F(y)}' fill='none' stroke='#DC2626' stroke-width='2' />");
        var hx = clockwise ? x + 18 : x - 18;
        var dir = clockwise ? 1 : -1;
        sb.AppendLine($"<polygon points='{F(hx)},{F(y)} {F(hx - 6 * dir)},{F(y - 4)} {F(hx - 6 * dir)},{F(y + 4)}' fill='#DC2626' />");
        sb.AppendLine($"<text x='{F(x)}' y='{F(y - 24)}' text-anchor='middle' font-size='12' fill='#991B1B'>M={F(m)}</text>");
    }

    private static void DrawPinSupport(StringBuilder sb, double x, double y)
    {
        sb.AppendLine($"<polygon points='{F(x)},{F(y)} {F(x - 16)},{F(y + 20)} {F(x + 16)},{F(y + 20)}' fill='none' stroke='#111827' stroke-width='2' />");
        sb.AppendLine($"<line x1='{F(x - 20)}' y1='{F(y + 24)}' x2='{F(x + 20)}' y2='{F(y + 24)}' stroke='#111827' stroke-width='1.5' />");
    }

    private static void DrawRollerSupport(StringBuilder sb, double x, double y)
    {
        DrawPinSupport(sb, x, y);
        sb.AppendLine($"<circle cx='{F(x - 7)}' cy='{F(y + 29)}' r='4' fill='none' stroke='#111827' stroke-width='1.5' />");
        sb.AppendLine($"<circle cx='{F(x + 7)}' cy='{F(y + 29)}' r='4' fill='none' stroke='#111827' stroke-width='1.5' />");
    }

    private static void DrawFixedSupport(StringBuilder sb, double x, double y)
    {
        sb.AppendLine($"<line x1='{F(x)}' y1='{F(y - 30)}' x2='{F(x)}' y2='{F(y + 30)}' stroke='#111827' stroke-width='4' />");
        for (var i = -30; i <= 30; i += 8)
        {
            sb.AppendLine($"<line x1='{F(x)}' y1='{F(y + i)}' x2='{F(x - 16)}' y2='{F(y + i + 8)}' stroke='#111827' stroke-width='1.4' />");
        }
    }

    private static void DrawHinge(StringBuilder sb, double x, double y)
    {
        sb.AppendLine($"<circle cx='{F(x)}' cy='{F(y)}' r='6' fill='white' stroke='#111827' stroke-width='2' />");
    }

    private static void DrawDimension(StringBuilder sb, double x1, double x2, double y, string label)
    {
        sb.AppendLine($"<line x1='{F(x1)}' y1='{F(y)}' x2='{F(x2)}' y2='{F(y)}' stroke='#6B7280' stroke-width='1.2' />");
        sb.AppendLine($"<line x1='{F(x1)}' y1='{F(y - 5)}' x2='{F(x1)}' y2='{F(y + 5)}' stroke='#6B7280' stroke-width='1.2' />");
        sb.AppendLine($"<line x1='{F(x2)}' y1='{F(y - 5)}' x2='{F(x2)}' y2='{F(y + 5)}' stroke='#6B7280' stroke-width='1.2' />");
        sb.AppendLine($"<text x='{F((x1 + x2) / 2)}' y='{F(y - 8)}' text-anchor='middle' font-size='12' fill='#4B5563'>{label}</text>");
    }

    private static void DrawDownArrow(StringBuilder sb, double x, double y1, double y2, string color, bool thick = false)
    {
        sb.AppendLine($"<line x1='{F(x)}' y1='{F(y1)}' x2='{F(x)}' y2='{F(y2)}' stroke='{color}' stroke-width='{(thick ? 2.2 : 1.3)}' />");
        sb.AppendLine($"<polygon points='{F(x - 4)},{F(y2 - 7)} {F(x + 4)},{F(y2 - 7)} {F(x)},{F(y2)}' fill='{color}' />");
    }

    private static double GetTotalLength(BeamCalcRequest request)
    {
        var l1 = request.L > 0 ? request.L : 1;
        return request.Support switch
        {
            SupportCondition.DoubleSpan => l1 + Math.Max(request.L2, l1),
            SupportCondition.ContinuousThreeSpan => l1 + Math.Max(request.L2, l1) + Math.Max(request.L3, l1),
            SupportCondition.MultiSpanHingedStatical => l1 + (request.L2 > 0 ? request.L2 : l1) + (request.L3 > 0 ? request.L3 : l1),
            SupportCondition.MultiSpanHingedIndeterminate => l1 + (request.L2 > 0 ? request.L2 : l1) + (request.L3 > 0 ? request.L3 : l1),
            SupportCondition.OverhangBeam => l1 + (request.Lc > 0 ? request.Lc : l1 * 0.25),
            _ => l1
        };
    }

    private static string F(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v))
        {
            return "0";
        }

        return v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
