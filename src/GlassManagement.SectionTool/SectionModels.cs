using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GlassManagement.SectionTool;

public class PointD
{
    public double X { get; set; }
    public double Y { get; set; }
}

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

public class PointRow : INotifyPropertyChanged
{
    private double _x;
    private double _y;

    public double X
    {
        get => _x;
        set { _x = value; OnPropertyChanged(); }
    }

    public double Y
    {
        get => _y;
        set { _y = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class HoleGroup : INotifyPropertyChanged
{
    private string _name = "内孔";
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public ObservableCollection<PointRow> Points { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class ProfileRecord
{
    public int Id { get; set; }
    public string 名称 { get; set; } = "";
    public double Ix { get; set; }
    public double Iy { get; set; }
    public double Wx1 { get; set; }
    public double Wx2 { get; set; }
    public double Wy1 { get; set; }
    public double Wy2 { get; set; }
    public double Sx { get; set; }
    public double Sy { get; set; }
    public double A { get; set; }
    public double 壁厚 { get; set; }
    public double 腹板宽度 { get; set; }
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
