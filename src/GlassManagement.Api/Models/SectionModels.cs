namespace GlassManagement.Api.Models;

public class PointD
{
    public double X { get; set; }
    public double Y { get; set; }
}

public class SectionCalculateRequest
{
    public List<PointD> OuterBoundary { get; set; } = new();
    public List<List<PointD>> Holes { get; set; } = new();
    public bool IsBreakHeat { get; set; }
    public BreakHeatConfig? BreakHeatConfig { get; set; }
}

public class BreakHeatConfig
{
    public List<PointD> Side1Region { get; set; } = new();
    public List<PointD> Side2Region { get; set; } = new();
    public double CompositeElasticity { get; set; } = 80; // 穿条80, 注胶1600
    public double SpanLength { get; set; } // 跨度mm
}

public class SectionResult
{
    public double Area { get; set; }   // mm²
    public double Ix { get; set; }     // mm⁴
    public double Iy { get; set; }     // mm⁴
    public double Wx1 { get; set; }    // mm³ (Ix/|Ymax| 相对质心下侧)
    public double Wx2 { get; set; }    // mm³ (Ix/|Ymin| 相对质心上侧)
    public double Wy1 { get; set; }    // mm³ (Iy/|Xmax|)
    public double Wy2 { get; set; }    // mm³ (Iy/|Xmin|)
    public double Sx { get; set; }     // mm³ 面积矩(静矩)
    public double Sy { get; set; }     // mm³ 面积矩(静矩)
    public double Cx { get; set; }     // mm 质心X
    public double Cy { get; set; }     // mm 质心Y
    public double Ymin { get; set; }   // mm 相对质心
    public double Ymax { get; set; }   // mm 相对质心
    public double Xmin { get; set; }   // mm 相对质心
    public double Xmax { get; set; }   // mm 相对质心
}

public class SectionImportRequest
{
    public string ProfileName { get; set; } = "";
    public string Category { get; set; } = "";
    public SectionResult Result { get; set; } = new();
    public double WallThickness { get; set; }    // 连接面壁厚 mm
    public double ShearWebWidth { get; set; }    // 受剪腹板总宽度 mm
}

public class SectionProfileRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ProfileName { get; set; } = "";
    public string Category { get; set; } = "";
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
    public double WallThickness { get; set; }
    public double ShearWebWidth { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class ProfileCategories
{
    public static readonly string[] All = {
        "立柱", "横框",
        "钢铝结合铝立柱", "钢铝结合铝横框",
        "钢铝结合钢立柱", "钢铝结合钢横框",
        "立梃一(门窗用)", "立梃二(门窗用,含钢芯)",
        "横梃一(门窗用)", "横梃二(门窗用,含钢芯)",
        "单元立柱一", "单元立柱二",
        "单元横框一", "单元横框二", "单元横框三(中横框)",
        "U型玻璃", "百叶叶片",
        "采光顶副龙骨", "采光顶主龙骨",
        "吊顶副龙骨", "吊顶主龙骨",
        "钢管点玻龙骨", "栏杆横龙骨", "栏杆竖龙骨", "雨篷龙骨"
    };
}
