namespace GlassManagement.Calc.Engines.SectionCalc;

/// <summary>截面点（mm 坐标）。</summary>
public class PointD
{
    public double X { get; set; }
    public double Y { get; set; }
}

/// <summary>截面特性结果（面积/惯性矩/抵抗矩/静矩/形心/边界）。</summary>
public class SectionResult
{
    public double Area { get; set; }
    public double Ix { get; set; }
    public double Iy { get; set; }
    public double Wx1 { get; set; }
    public double Wx2 { get; set; }
    public double Wy1 { get; set; }
    public double Wy2 { get; set; }
    public double Sx { get; set; }
    public double Sy { get; set; }
    public double Cx { get; set; }
    public double Cy { get; set; }
    public double Ymin { get; set; }
    public double Ymax { get; set; }
    public double Xmin { get; set; }
    public double Xmax { get; set; }
}
