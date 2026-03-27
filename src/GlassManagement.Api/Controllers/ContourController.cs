using Microsoft.AspNetCore.Mvc;
using GlassManagement.Core.Models;
using System.Text;

namespace GlassManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContourController : ControllerBase
{
    private const string FontStyle = "font-family:sans-serif;font-size:12px;";

    /// <summary>板面应力云图</summary>
    [HttpPost("PlateStress")]
    [Produces("image/svg+xml")]
    public IActionResult PlateStress([FromBody] PlateContourRequest req)
    {
        req.ContourType = "应力";
        return GenerateContour(req);
    }

    /// <summary>板面挠度云图</summary>
    [HttpPost("PlateDeflection")]
    [Produces("image/svg+xml")]
    public IActionResult PlateDeflection([FromBody] PlateContourRequest req)
    {
        req.ContourType = "挠度";
        return GenerateContour(req);
    }

    /// <summary>组合玻璃云图 — 返回每层每片的应力/挠度云图(JSON)</summary>
    [HttpPost("CompositeGlass")]
    [ProducesResponseType(typeof(CompositeGlassContourResult), StatusCodes.Status200OK)]
    public ActionResult<CompositeGlassContourResult> CompositeGlassContour([FromBody] CompositeGlassContourRequest req)
    {
        if (req.Layers == null || req.Layers.Count == 0)
            return BadRequest("请提供至少一层玻璃");

        double a = req.ShortSide, b = req.LongSide;
        double wk = req.WindLoad * 0.001; // kN/m² → MPa
        bool isInsulating = req.AirSpaceThicknesses != null && req.AirSpaceThicknesses.Count > 0;
        double E = 72000.0, v = 0.2;
        string contourType = req.ContourType ?? "应力";

        // 气压差附加荷载
        double Wg = 0;
        if (req.AltitudeDifference > 0) Wg += 0.012 * req.AltitudeDifference * 1e-3;
        if (req.TemperatureDifference > 0) Wg += 0.0034 * req.TemperatureDifference * 1e-3;

        // 计算每层的等效厚度（用于荷载分配）
        var layerInfos = new List<(double teDeflection, double teStress, List<(double h, double fg, double teSigma, string desc)> subPanes)>();
        foreach (var layer in req.Layers)
        {
            if (layer.LayerType == "Laminated" && layer.LaminatedThicknesses != null && layer.LaminatedThicknesses.Count >= 2)
            {
                var hs = layer.LaminatedThicknesses.Select(GetMinThickness).ToList();
                var ts = layer.InterlayerThicknesses ?? new List<double> { 1.52 };
                double G = GetInterlayerG(layer.InterlayerType);
                double totalTs = ts.Sum();

                // 截面中心
                double totalH = hs.Sum() + ts.Sum();
                double center = totalH / 2.0;
                var dks = new List<double>();
                double pos = 0;
                for (int k = 0; k < hs.Count; k++)
                {
                    double mid = pos + hs[k] / 2.0;
                    dks.Add(mid - center);
                    pos += hs[k];
                    if (k < ts.Count) pos += ts[k];
                }

                double Is = 0;
                for (int k = 0; k < hs.Count; k++) Is += hs[k] * dks[k] * dks[k];
                double sumH3 = hs.Sum(h => h * h * h);
                double IsV = Is + sumH3 / 12.0;
                double Gamma = 1.0 / (1.0 + 9.6 * E * IsV / (G * totalTs * totalTs * a * a));

                double te = Math.Pow(sumH3 + 12.0 * Gamma * Is, 1.0 / 3.0);

                // te,w for stress
                double sumH2 = hs.Sum(h => h * h);
                double sumHD = 0;
                for (int k = 0; k < hs.Count; k++) sumHD += hs[k] * Math.Abs(dks[k]);
                double teW = Math.Sqrt(sumH2 + 2.0 * Gamma * sumHD);

                var subPanes = new List<(double h, double fg, double teSigma, string desc)>();
                for (int k = 0; k < hs.Count; k++)
                {
                    double teSigma = te * te * te / (hs[k] * teW * teW);
                    double fg = GetDesignStrength(layer.GlassType, layer.LaminatedThicknesses![k]);
                    subPanes.Add((hs[k], fg, teSigma, $"{layer.LaminatedThicknesses[k]}mm{layer.GlassType}"));
                }
                layerInfos.Add((te, te, subPanes));
            }
            else
            {
                double h = GetMinThickness(layer.Thickness);
                double fg = GetDesignStrength(layer.GlassType, layer.Thickness);
                layerInfos.Add((h, h, new() { (h, fg, h, $"{layer.Thickness}mm{layer.GlassType}") }));
            }
        }

        // 荷载分配系数
        double sumT3 = layerInfos.Sum(l => Math.Pow(l.teDeflection, 3));
        var phis = layerInfos.Select(l => Math.Pow(l.teDeflection, 3) / sumT3).ToList();

        var result = new CompositeGlassContourResult();
        var descParts = new List<string>();
        for (int i = 0; i < req.Layers.Count; i++)
            descParts.Add($"第{i + 1}层(φ={phis[i]:F3})");
        result.Summary = $"{a:F0}×{b:F0}mm, {string.Join(" + ", descParts)}, {contourType}云图";

        // 生成每层每片的云图
        int n = Math.Clamp(req.GridSize, 10, 50);
        for (int layerIdx = 0; layerIdx < layerInfos.Count; layerIdx++)
        {
            var (teDefl, _, subPanes) = layerInfos[layerIdx];
            double phi = phis[layerIdx];
            double qLayer = wk * phi + (isInsulating ? Wg * (1.0 - phi) : 0);

            for (int subIdx = 0; subIdx < subPanes.Count; subIdx++)
            {
                var (h, fg, teSigma, desc) = subPanes[subIdx];
                double tForCalc = contourType == "挠度" ? teDefl : teSigma;
                double D = E * Math.Pow(tForCalc, 3) / (12.0 * (1 - v * v));

                double[,] data = new double[n + 1, n + 1];
                double maxVal = double.MinValue, minVal = double.MaxValue;
                double maxX = 0, maxY = 0;

                for (int i = 0; i <= n; i++)
                {
                    for (int j = 0; j <= n; j++)
                    {
                        double x = (i == 0) ? a * 0.01 : (i == n) ? a * 0.99 : a * i / n;
                        double y = (j == 0) ? b * 0.01 : (j == n) ? b * 0.99 : b * j / n;

                        data[i, j] = contourType == "挠度"
                            ? CalcDeflection(x, y, a, b, qLayer, D)
                            : CalcStress(x, y, a, b, tForCalc, qLayer, E, v, D);

                        if (data[i, j] > maxVal) { maxVal = data[i, j]; maxX = x; maxY = y; }
                        if (data[i, j] < minVal) minVal = data[i, j];
                    }
                }

                string layerLabel = subPanes.Count > 1
                    ? $"第{layerIdx + 1}层-子片{subIdx + 1} {desc}"
                    : $"第{layerIdx + 1}层 {desc}";
                string svg = RenderContourSvg(data, n, a, b, maxVal, minVal, maxX, maxY,
                    $"{layerLabel} {contourType}云图 (φ={phi:F3})",
                    $"{a:F0}×{b:F0}mm, t={tForCalc:F2}mm, q={qLayer * 1000:F4}kN/m²",
                    contourType == "挠度" ? "mm" : "MPa");

                result.LayerContours.Add(new ContourLayerResult
                {
                    LayerIndex = layerIdx,
                    SubIndex = subIdx,
                    Description = layerLabel,
                    EquivalentThickness = Math.Round(tForCalc, 2),
                    LoadShareFactor = Math.Round(phi, 4),
                    MaxValue = Math.Round(maxVal, 4),
                    Svg = svg
                });
            }
        }

        return Ok(result);
    }

    private static double GetMinThickness(double nominal) => nominal switch
    {
        3 => 2.8, 4 => 3.6, 5 => 4.6, 6 => 5.6, 8 => 7.6, 10 => 9.6, 12 => 11.6,
        15 => 14.6, 19 => 18.6, 22 => 21.6, 25 => 24.6, _ => nominal - 0.4
    };

    private static double GetDesignStrength(string glassType, double thickness) => glassType switch
    {
        "Tempered" => thickness >= 12 ? 72 : 84,
        "SemiTempered" or "HeatStrengthened" => thickness >= 12 ? 38 : 41,
        _ => thickness >= 12 ? 24 : 28 // Float
    };

    private static double GetInterlayerG(string type) => type?.ToUpper() switch
    {
        "SGP" => 11.3,
        "EVA" => 0.3,
        _ => 0.4 // PVB
    };

    private IActionResult GenerateContour(PlateContourRequest req)
    {
        double a = req.ShortSide; // mm
        double b = req.LongSide;  // mm
        double t = req.Thickness;  // mm
        double q = req.WindLoad * 0.001; // kN/m² → MPa (N/mm²)
        double E = req.ElasticModulus;    // MPa
        double v = req.PoissonRatio;
        int n = Math.Clamp(req.GridSize, 10, 60);

        // 抗弯刚度 D = Et³/[12(1-v²)]
        double D = E * t * t * t / (12.0 * (1 - v * v));

        // 计算网格数据
        double[,] data = new double[n + 1, n + 1];
        double maxVal = double.MinValue, minVal = double.MaxValue;
        double maxX = 0, maxY = 0;

        for (int i = 0; i <= n; i++)
        {
            for (int j = 0; j <= n; j++)
            {
                double x = a * (i + 0.5) / (n + 1); // 避免边界0
                double y = b * (j + 0.5) / (n + 1);
                if (i == 0) x = a * 0.02;
                if (j == 0) y = b * 0.02;
                if (i == n) x = a * 0.98;
                if (j == n) y = b * 0.98;

                // 实际网格点
                x = a * i / n;
                y = b * j / n;
                if (x < 1e-6) x = a * 0.01;
                if (y < 1e-6) y = b * 0.01;
                if (Math.Abs(x - a) < 1e-6) x = a * 0.99;
                if (Math.Abs(y - b) < 1e-6) y = b * 0.99;

                if (req.ContourType == "挠度")
                {
                    data[i, j] = CalcDeflection(x, y, a, b, q, D);
                }
                else
                {
                    data[i, j] = CalcStress(x, y, a, b, t, q, E, v, D);
                }

                if (data[i, j] > maxVal) { maxVal = data[i, j]; maxX = x; maxY = y; }
                if (data[i, j] < minVal) minVal = data[i, j];
            }
        }

        string title = req.ContourType == "挠度" ? "板面挠度云图" : "板面应力云图";
        string unit = req.ContourType == "挠度" ? "mm" : "MPa";
        string subtitle = $"{a:F0}×{b:F0}mm, t={t:F1}mm, q={req.WindLoad:F4}kN/m²";
        string svg = RenderContourSvg(data, n, a, b, maxVal, minVal, maxX, maxY, title, subtitle, unit);
        return Content(svg, "image/svg+xml");
    }

    /// <summary>通用SVG云图渲染</summary>
    private static string RenderContourSvg(double[,] data, int n, double a, double b,
        double maxVal, double minVal, double maxX, double maxY,
        string title, string subtitle, string unit)
    {
        int svgW = 800, svgH = 500;
        int margin = 60, legendW = 80;
        double plotW = svgW - 2 * margin - legendW;
        double plotH = svgH - 2 * margin;
        double scaleX = plotW / b, scaleY = plotH / a;
        double scale = Math.Min(scaleX, scaleY);
        double drawW = b * scale, drawH = a * scale;
        double ox = margin + (plotW - drawW) / 2;
        double oy = margin + (plotH - drawH) / 2;

        var sb = new StringBuilder();
        sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{svgW}' height='{svgH}' viewBox='0 0 {svgW} {svgH}'>");
        sb.Append($"<rect width='{svgW}' height='{svgH}' fill='white'/>");
        sb.Append($"<text x='{svgW / 2}' y='25' text-anchor='middle' style='{FontStyle}font-size:16px;font-weight:bold;'>{title}</text>");
        sb.Append($"<text x='{svgW / 2}' y='42' text-anchor='middle' style='{FontStyle}font-size:12px;fill:#666;'>{subtitle}</text>");

        double cellW = drawW / n, cellH = drawH / n;
        double range = maxVal - minVal;
        if (range < 1e-12) range = 1;

        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                double val = (data[i, j] + data[i + 1, j] + data[i, j + 1] + data[i + 1, j + 1]) / 4.0;
                string color = ValueToColor((val - minVal) / range);
                sb.Append($"<rect x='{ox + j * cellW:F1}' y='{oy + i * cellH:F1}' width='{cellW + 0.5:F1}' height='{cellH + 0.5:F1}' fill='{color}' stroke='none'/>");
            }

        sb.Append($"<rect x='{ox:F1}' y='{oy:F1}' width='{drawW:F1}' height='{drawH:F1}' fill='none' stroke='#333' stroke-width='1.5'/>");

        double markerX = ox + maxY / b * drawW, markerY = oy + maxX / a * drawH;
        sb.Append($"<circle cx='{markerX:F1}' cy='{markerY:F1}' r='4' fill='none' stroke='white' stroke-width='2'/>");
        sb.Append($"<circle cx='{markerX:F1}' cy='{markerY:F1}' r='4' fill='none' stroke='black' stroke-width='1'/>");
        sb.Append($"<text x='{markerX + 8:F1}' y='{markerY - 5:F1}' style='{FontStyle}font-weight:bold;'>最大值: {maxVal:F3}{unit}</text>");

        double barX = svgW - margin - legendW + 20, barY = oy, barW = 20, barH = drawH;
        int nBar = 50;
        for (int i = 0; i < nBar; i++)
        {
            string color = ValueToColor(1.0 - (double)i / nBar);
            sb.Append($"<rect x='{barX}' y='{barY + barH * i / nBar:F1}' width='{barW}' height='{barH / nBar + 1:F1}' fill='{color}' stroke='none'/>");
        }
        sb.Append($"<rect x='{barX}' y='{barY:F1}' width='{barW}' height='{barH:F1}' fill='none' stroke='#333' stroke-width='1'/>");

        for (int i = 0; i <= 5; i++)
        {
            double val = minVal + (1.0 - (double)i / 5) * range;
            double ly = barY + barH * i / 5.0;
            sb.Append($"<line x1='{barX + barW}' y1='{ly:F1}' x2='{barX + barW + 5}' y2='{ly:F1}' stroke='#333' stroke-width='1'/>");
            sb.Append($"<text x='{barX + barW + 8}' y='{ly + 4:F1}' style='{FontStyle}font-size:10px;'>{val:F3}</text>");
        }
        sb.Append($"<text x='{barX + barW / 2}' y='{barY - 8}' text-anchor='middle' style='{FontStyle}font-size:11px;'>{unit}</text>");
        sb.Append($"<text x='{ox + drawW / 2}' y='{oy + drawH + 20}' text-anchor='middle' style='{FontStyle}fill:#666;'>b={b:F0}mm</text>");
        sb.Append($"<text x='{ox - 10}' y='{oy + drawH / 2}' text-anchor='end' style='{FontStyle}fill:#666;' transform='rotate(-90,{ox - 10},{oy + drawH / 2})'>a={a:F0}mm</text>");
        sb.Append("</svg>");
        return sb.ToString();
    }

    /// <summary>Navier级数解 - 挠度 w(x,y)</summary>
    private static double CalcDeflection(double x, double y, double a, double b, double q, double D)
    {
        double w = 0;
        for (int m = 1; m <= 19; m += 2)
        {
            for (int nn = 1; nn <= 19; nn += 2)
            {
                double sinX = Math.Sin(m * Math.PI * x / a);
                double sinY = Math.Sin(nn * Math.PI * y / b);
                double denom = m * nn * Math.Pow(m * m / (a * a) + nn * nn / (b * b), 2);
                w += sinX * sinY / denom;
            }
        }
        w *= 16.0 * q / (Math.Pow(Math.PI, 6) * D);
        return w;
    }

    /// <summary>Navier级数解 - 弯曲应力 σ(x,y) (取表面最大)</summary>
    private static double CalcStress(double x, double y, double a, double b, double t,
        double q, double E, double v, double D)
    {
        double d2wdx2 = 0, d2wdy2 = 0;
        for (int m = 1; m <= 19; m += 2)
        {
            for (int nn = 1; nn <= 19; nn += 2)
            {
                double sinX = Math.Sin(m * Math.PI * x / a);
                double sinY = Math.Sin(nn * Math.PI * y / b);
                double denom = m * nn * Math.Pow(m * m / (a * a) + nn * nn / (b * b), 2);
                double coeff = sinX * sinY / denom;

                d2wdx2 += -Math.Pow(m * Math.PI / a, 2) * coeff;
                d2wdy2 += -Math.Pow(nn * Math.PI / b, 2) * coeff;
            }
        }
        double factor = 16.0 * q / (Math.Pow(Math.PI, 6) * D);
        d2wdx2 *= factor;
        d2wdy2 *= factor;

        // σ = -E*z/(1-v²) * (∂²w/∂x² + v*∂²w/∂y²), z = t/2
        double z = t / 2.0;
        double sigma = -E * z / (1 - v * v) * (d2wdx2 + v * d2wdy2);
        return Math.Abs(sigma); // 取绝对值
    }

    /// <summary>数值比例 [0,1] → 颜色 (蓝→绿→黄→红)</summary>
    private static string ValueToColor(double ratio)
    {
        ratio = Math.Clamp(ratio, 0, 1);
        // HSL: 蓝(240)→绿(120)→黄(60)→红(0)
        double hue = (1 - ratio) * 240;
        // Convert HSL to RGB (S=100%, L=50%)
        double h = hue / 60.0;
        double c = 1.0;
        double x2 = c * (1 - Math.Abs(h % 2 - 1));
        double r, g, bl;
        if (h < 1) { r = c; g = x2; bl = 0; }
        else if (h < 2) { r = x2; g = c; bl = 0; }
        else if (h < 3) { r = 0; g = c; bl = x2; }
        else if (h < 4) { r = 0; g = x2; bl = c; }
        else { r = x2; g = 0; bl = c; }

        int ri = (int)(r * 255), gi = (int)(g * 255), bi = (int)(bl * 255);
        return $"#{ri:X2}{gi:X2}{bi:X2}";
    }
}
