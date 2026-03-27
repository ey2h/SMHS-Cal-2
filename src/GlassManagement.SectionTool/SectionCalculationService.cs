using System;
using System.Collections.Generic;
using System.Linq;
using static System.Math;
namespace GlassManagement.SectionTool;

/// <summary>截面特性计算引擎 — 基于Green定理的纯数学计算</summary>
public class SectionCalculationService
{
    /// <summary>常规截面计算</summary>
    public SectionResult CalculateSection(List<PointD> outerBoundary, List<List<PointD>>? holes)
    {
        var (areaOuter, cxOuter, cyOuter) = ComputeAreaAndCentroid(outerBoundary);

        if (areaOuter < 0)
        {
            outerBoundary.Reverse();
            areaOuter = -areaOuter;
            cxOuter = ComputeAreaAndCentroid(outerBoundary).cx;
            cyOuter = ComputeAreaAndCentroid(outerBoundary).cy;
        }

        double totalArea = areaOuter;
        double totalCxA = cxOuter * areaOuter;
        double totalCyA = cyOuter * areaOuter;

        var holesList = holes ?? new List<List<PointD>>();
        var holeProps = new List<(double area, double cx, double cy)>();
        foreach (var hole in holesList)
        {
            var (ah, chx, chy) = ComputeAreaAndCentroid(hole);
            ah = Math.Abs(ah);
            holeProps.Add((ah, chx, chy));
            totalArea -= ah;
            totalCxA -= chx * ah;
            totalCyA -= chy * ah;
        }

        if (totalArea <= 0)
            throw new ArgumentException("截面净面积为零或负值，请检查内孔是否超出外轮廓");

        double cx = totalCxA / totalArea;
        double cy = totalCyA / totalArea;

        var (ixOuter, iyOuter) = ComputeInertia(outerBoundary);
        double ixOuterCentroid = ixOuter - areaOuter * cyOuter * cyOuter;
        double iyOuterCentroid = iyOuter - areaOuter * cxOuter * cxOuter;

        double ix = ixOuterCentroid + areaOuter * (cyOuter - cy) * (cyOuter - cy);
        double iy = iyOuterCentroid + areaOuter * (cxOuter - cx) * (cxOuter - cx);

        for (int i = 0; i < holesList.Count; i++)
        {
            var (ah, chx, chy) = holeProps[i];
            var (ixh, iyh) = ComputeInertia(holesList[i]);
            double ixhCentroid = ixh - ah * chy * chy;
            double iyhCentroid = iyh - ah * chx * chx;
            ix -= ixhCentroid + ah * (chy - cy) * (chy - cy);
            iy -= iyhCentroid + ah * (chx - cx) * (chx - cx);
        }

        double ymin = double.MaxValue, ymax = double.MinValue;
        double xmin = double.MaxValue, xmax = double.MinValue;
        foreach (var p in outerBoundary)
        {
            double py = p.Y - cy, px = p.X - cx;
            if (py < ymin) ymin = py;
            if (py > ymax) ymax = py;
            if (px < xmin) xmin = px;
            if (px > xmax) xmax = px;
        }

        double wx1 = Math.Abs(ymax) > 1e-10 ? ix / Math.Abs(ymax) : 0;
        double wx2 = Math.Abs(ymin) > 1e-10 ? ix / Math.Abs(ymin) : 0;
        double wy1 = Math.Abs(xmax) > 1e-10 ? iy / Math.Abs(xmax) : 0;
        double wy2 = Math.Abs(xmin) > 1e-10 ? iy / Math.Abs(xmin) : 0;

        double sx = ComputeFirstMoment(outerBoundary, holesList, cx, cy, true);
        double sy = ComputeFirstMoment(outerBoundary, holesList, cx, cy, false);

        return new SectionResult
        {
            Area = totalArea,
            Ix = ix, Iy = iy,
            Wx1 = wx1, Wx2 = wx2,
            Wy1 = wy1, Wy2 = wy2,
            Sx = sx, Sy = sy,
            Cx = cx, Cy = cy,
            Ymin = ymin, Ymax = ymax,
            Xmin = xmin, Xmax = xmax
        };
    }

    /// <summary>断热截面计算 — JG175-2011 / EN14024-2004</summary>
    public SectionResult CalculateBreakHeatSection(
        List<PointD> side1Region,
        List<PointD> side2Region,
        List<PointD> fullRegion,
        double compositeElasticity,
        double spanLength)
    {
        var s1 = CalculateSection(side1Region, null);
        var s2 = CalculateSection(side2Region, null);
        var full = CalculateSection(fullRegion, null);

        double alpha;
        if (compositeElasticity >= 1000)
        {
            alpha = 1.0 / (1.0 + 0.00025 * spanLength * spanLength / (compositeElasticity > 0 ? compositeElasticity : 1));
        }
        else
        {
            alpha = 1.0 / (1.0 + 0.025 * spanLength * spanLength / (compositeElasticity > 0 ? compositeElasticity : 1));
        }
        alpha = Math.Clamp(alpha, 0, 1);

        double ieX = s1.Ix + s2.Ix + alpha * (full.Ix - s1.Ix - s2.Ix);
        double ieY = s1.Iy + s2.Iy + alpha * (full.Iy - s1.Iy - s2.Iy);
        double aeA = s1.Area + s2.Area + alpha * (full.Area - s1.Area - s2.Area);

        double wx1 = Math.Abs(full.Ymax) > 1e-10 ? ieX / Math.Abs(full.Ymax) : 0;
        double wx2 = Math.Abs(full.Ymin) > 1e-10 ? ieX / Math.Abs(full.Ymin) : 0;
        double wy1 = Math.Abs(full.Xmax) > 1e-10 ? ieY / Math.Abs(full.Xmax) : 0;
        double wy2 = Math.Abs(full.Xmin) > 1e-10 ? ieY / Math.Abs(full.Xmin) : 0;

        return new SectionResult
        {
            Area = aeA,
            Ix = ieX, Iy = ieY,
            Wx1 = wx1, Wx2 = wx2,
            Wy1 = wy1, Wy2 = wy2,
            Sx = full.Sx, Sy = full.Sy,
            Cx = full.Cx, Cy = full.Cy,
            Ymin = full.Ymin, Ymax = full.Ymax,
            Xmin = full.Xmin, Xmax = full.Xmax
        };
    }

    #region Green定理核心计算

    private static (double area, double cx, double cy) ComputeAreaAndCentroid(List<PointD> polygon)
    {
        int n = polygon.Count;
        if (n < 3) return (0, 0, 0);

        double area = 0, cx = 0, cy = 0;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            double cross = polygon[i].X * polygon[j].Y - polygon[j].X * polygon[i].Y;
            area += cross;
            cx += (polygon[i].X + polygon[j].X) * cross;
            cy += (polygon[i].Y + polygon[j].Y) * cross;
        }
        area *= 0.5;
        if (Math.Abs(area) < 1e-15) return (0, 0, 0);
        cx /= (6.0 * area);
        cy /= (6.0 * area);
        return (area, cx, cy);
    }

    private static (double ix, double iy) ComputeInertia(List<PointD> polygon)
    {
        int n = polygon.Count;
        if (n < 3) return (0, 0);

        double ix = 0, iy = 0;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            double xi = polygon[i].X, yi = polygon[i].Y;
            double xj = polygon[j].X, yj = polygon[j].Y;
            double cross = xi * yj - xj * yi;
            ix += (yi * yi + yi * yj + yj * yj) * cross;
            iy += (xi * xi + xi * xj + xj * xj) * cross;
        }
        ix /= 12.0;
        iy /= 12.0;
        return (ix, iy);
    }

    private static double ComputeFirstMoment(List<PointD> outer, List<List<PointD>> holes, double cx, double cy, bool aboutX)
    {
        var clipped = aboutX
            ? ClipPolygonAbove(outer, cy)
            : ClipPolygonRight(outer, cx);

        double sx = 0;
        if (clipped.Count >= 3)
        {
            sx = ComputeFirstMomentIntegral(clipped, aboutX ? cy : cx, aboutX);
        }

        foreach (var hole in holes)
        {
            var hClipped = aboutX
                ? ClipPolygonAbove(hole, cy)
                : ClipPolygonRight(hole, cx);
            if (hClipped.Count >= 3)
            {
                sx -= Math.Abs(ComputeFirstMomentIntegral(hClipped, aboutX ? cy : cx, aboutX));
            }
        }

        return Math.Abs(sx);
    }

    private static double ComputeFirstMomentIntegral(List<PointD> polygon, double ref0, bool aboutX)
    {
        int n = polygon.Count;
        if (n < 3) return 0;
        double s = 0;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            double xi = polygon[i].X, yi = polygon[i].Y;
            double xj = polygon[j].X, yj = polygon[j].Y;
            double cross = xi * yj - xj * yi;
            if (aboutX)
                s += ((yi - ref0) + (yj - ref0)) * cross;
            else
                s += ((xi - ref0) + (xj - ref0)) * cross;
        }
        return s / 6.0;
    }

    private static List<PointD> ClipPolygonAbove(List<PointD> polygon, double threshold)
    {
        var output = new List<PointD>();
        int n = polygon.Count;
        if (n < 3) return output;

        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            var cur = polygon[i];
            var nxt = polygon[j];
            bool curIn = cur.Y >= threshold;
            bool nxtIn = nxt.Y >= threshold;

            if (curIn)
            {
                output.Add(cur);
                if (!nxtIn)
                    output.Add(Intersect(cur, nxt, threshold, true));
            }
            else if (nxtIn)
            {
                output.Add(Intersect(cur, nxt, threshold, true));
            }
        }
        return output;
    }

    private static List<PointD> ClipPolygonRight(List<PointD> polygon, double threshold)
    {
        var output = new List<PointD>();
        int n = polygon.Count;
        if (n < 3) return output;

        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            var cur = polygon[i];
            var nxt = polygon[j];
            bool curIn = cur.X >= threshold;
            bool nxtIn = nxt.X >= threshold;

            if (curIn)
            {
                output.Add(cur);
                if (!nxtIn)
                    output.Add(Intersect(cur, nxt, threshold, false));
            }
            else if (nxtIn)
            {
                output.Add(Intersect(cur, nxt, threshold, false));
            }
        }
        return output;
    }

    private static PointD Intersect(PointD a, PointD b, double threshold, bool horizontal)
    {
        if (horizontal)
        {
            double t = (threshold - a.Y) / (b.Y - a.Y);
            return new PointD { X = a.X + t * (b.X - a.X), Y = threshold };
        }
        else
        {
            double t = (threshold - a.X) / (b.X - a.X);
            return new PointD { X = threshold, Y = a.Y + t * (b.Y - a.Y) };
        }
    }

    #endregion
}
