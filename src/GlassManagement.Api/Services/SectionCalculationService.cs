using GlassManagement.Api.Models;

namespace GlassManagement.Api.Services;

/// <summary>截面特性计算引擎 — 基于Green定理的纯数学计算</summary>
public class SectionCalculationService
{
    /// <summary>常规截面计算</summary>
    public SectionResult CalculateSection(List<PointD> outerBoundary, List<List<PointD>>? holes)
    {
        // 外轮廓
        var (areaOuter, cxOuter, cyOuter) = ComputeAreaAndCentroid(outerBoundary);

        // 确保面积为正（逆时针）
        if (areaOuter < 0)
        {
            outerBoundary.Reverse();
            areaOuter = -areaOuter;
            cxOuter = ComputeAreaAndCentroid(outerBoundary).cx;
            cyOuter = ComputeAreaAndCentroid(outerBoundary).cy;
        }

        // 计算含孔的总质心
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

        // 相对质心的惯性矩
        var (ixOuter, iyOuter) = ComputeInertia(outerBoundary);
        // 平行轴定理转到整体质心
        double ix = ixOuter - areaOuter * cy * cy + areaOuter * (cyOuter - cy) * (cyOuter - cy);
        // 简化: Ix_centroid = Ix_origin - A*Cy² 然后用平行轴
        // 直接用: Ix关于整体质心 = Ix关于自身质心 + A*(自身质心cy - 整体质心cy)²
        // Ix关于自身质心 = Ix_origin - A_outer * cyOuter²
        double ixOuterCentroid = ixOuter - areaOuter * cyOuter * cyOuter;
        double iyOuterCentroid = iyOuter - areaOuter * cxOuter * cxOuter;

        ix = ixOuterCentroid + areaOuter * (cyOuter - cy) * (cyOuter - cy);
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

        // 相对质心的极值坐标
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

        // 抗弯矩
        double wx1 = Math.Abs(ymax) > 1e-10 ? ix / Math.Abs(ymax) : 0;
        double wx2 = Math.Abs(ymin) > 1e-10 ? ix / Math.Abs(ymin) : 0;
        double wy1 = Math.Abs(xmax) > 1e-10 ? iy / Math.Abs(xmax) : 0;
        double wy2 = Math.Abs(xmin) > 1e-10 ? iy / Math.Abs(xmin) : 0;

        // 面积矩（静矩）— 半截面对质心轴的静矩
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
        // 分别计算三个区域的截面特性
        var s1 = CalculateSection(side1Region, null);
        var s2 = CalculateSection(side2Region, null);
        var full = CalculateSection(fullRegion, null);

        // 组合系数 α (JG175-2011 表5.3.2)
        // α = 1 / (1 + a * (L/D)²)
        // a 与连接方式有关: 穿条=0.025, 注胶=0.00025
        // D 为截面特征尺寸，简化取 compositeElasticity
        // 这里用简化公式: α ≈ compositeElasticity / (compositeElasticity + spanLength²/12000)
        double alpha;
        if (compositeElasticity >= 1000)
        {
            // 注胶式，α较大，接近1
            alpha = 1.0 / (1.0 + 0.00025 * spanLength * spanLength / (compositeElasticity > 0 ? compositeElasticity : 1));
        }
        else
        {
            // 穿条式，α较小
            alpha = 1.0 / (1.0 + 0.025 * spanLength * spanLength / (compositeElasticity > 0 ? compositeElasticity : 1));
        }
        alpha = Math.Clamp(alpha, 0, 1);

        // 有效惯性矩
        // Ie,x = Is1,x + Is2,x + α * (I_full,x - Is1,x - Is2,x)
        double ieX = s1.Ix + s2.Ix + alpha * (full.Ix - s1.Ix - s2.Ix);
        double ieY = s1.Iy + s2.Iy + alpha * (full.Iy - s1.Iy - s2.Iy);

        // 有效面积
        double aeA = s1.Area + s2.Area + alpha * (full.Area - s1.Area - s2.Area);

        // 使用完整截面的质心和极值
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

    /// <summary>计算多边形面积和质心 (Green定理)</summary>
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

    /// <summary>计算多边形关于原点的惯性矩 (Green定理)</summary>
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

    /// <summary>计算半截面对质心轴的面积矩（静矩）</summary>
    /// <param name="outer">外轮廓</param>
    /// <param name="holes">内孔列表</param>
    /// <param name="cx">质心X</param>
    /// <param name="cy">质心Y</param>
    /// <param name="aboutX">true=对X轴(取y>cy的半截面), false=对Y轴(取x>cx的半截面)</param>
    private static double ComputeFirstMoment(List<PointD> outer, List<List<PointD>> holes, double cx, double cy, bool aboutX)
    {
        // 用数值积分方法: 将截面沿轴切片，积分 y*dA 或 x*dA
        // 简化方法: 对多边形用解析公式
        // Sx = Σ 1/12 * (yi² + yi*yj + yj²) * cross 然后取半截面
        // 这里用简化近似: Sx ≈ |Ix / (2 * max(|ymax|, |ymin|))| * 1.5 (矩形近似系数)
        // 精确计算需要切割多边形，这里用近似值

        // 更精确的方法: 对于质心轴一侧的部分进行积分
        // 简化处理: 用 Green 定理计算整个截面关于质心的一阶矩积分
        // 半截面静矩 Sx = ∫(y>0) y dA

        // 使用多边形裁剪后积分
        var clipped = aboutX
            ? ClipPolygonAbove(outer, cy)
            : ClipPolygonRight(outer, cx);

        double sx = 0;
        if (clipped.Count >= 3)
        {
            // S = ∫ y' dA (y' = y - cy) 对于 aboutX
            // S = ∫ x' dA (x' = x - cx) 对于 aboutY
            sx = ComputeFirstMomentIntegral(clipped, aboutX ? cy : cx, aboutX);
        }

        // 扣除孔
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

    /// <summary>计算多边形关于参考轴的一阶矩</summary>
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
            {
                // ∫ (y - ref0) dA
                s += ((yi - ref0) + (yj - ref0)) * cross;
            }
            else
            {
                s += ((xi - ref0) + (xj - ref0)) * cross;
            }
        }
        return s / 6.0;
    }

    /// <summary>裁剪多边形，保留 y >= threshold 的部分 (Sutherland-Hodgman)</summary>
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
                {
                    // 出去，加交点
                    output.Add(Intersect(cur, nxt, threshold, true));
                }
            }
            else if (nxtIn)
            {
                // 进来，加交点
                output.Add(Intersect(cur, nxt, threshold, true));
            }
        }
        return output;
    }

    /// <summary>裁剪多边形，保留 x >= threshold 的部分</summary>
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

    /// <summary>线段与水平/垂直线的交点</summary>
    private static PointD Intersect(PointD a, PointD b, double threshold, bool horizontal)
    {
        if (horizontal)
        {
            // y = threshold
            double t = (threshold - a.Y) / (b.Y - a.Y);
            return new PointD { X = a.X + t * (b.X - a.X), Y = threshold };
        }
        else
        {
            // x = threshold
            double t = (threshold - a.X) / (b.X - a.X);
            return new PointD { X = threshold, Y = a.Y + t * (b.Y - a.Y) };
        }
    }

    #endregion
}
