using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace GlassManagement.Desktop.ViewModels;

/// <summary>
/// 中空玻璃配置 ViewModel (移植自霸总 WPF 设计)
/// 支持单中空(2片)和双中空(3片)
/// 每片可通过弹出 GlassConfigWindow 配置为单片或夹胶
/// </summary>
public class IGUConfigViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    // ===== 选项列表 =====
    public List<int> GlassThicknessOptions { get; } = new() { 5, 6, 8, 10, 12, 15, 19, 20 };
    public List<int> InsulatingThicknessOptions { get; } = new() { 6, 9, 12, 15, 16 };
    public List<string> GasTypeOptions { get; } = new() { "Air", "Ar" };
    public List<int> IGUSheetCountOptions { get; } = new() { 2, 3 };

    // ===== 玻璃片数 =====
    private int _sheetCountIndex = 0; // 默认2片
    public int SheetCountIndex
    {
        get => _sheetCountIndex;
        set { _sheetCountIndex = value; OnPropertyChanged(); BuildStructure(); }
    }
    public int SheetCount => IGUSheetCountOptions[_sheetCountIndex < IGUSheetCountOptions.Count ? _sheetCountIndex : 0];

    // ===== 气体类型 =====
    private int _gasTypeIndex = 0;
    public int GasTypeIndex
    {
        get => _gasTypeIndex;
        set { _gasTypeIndex = value; OnPropertyChanged(); UpdateResult(); }
    }
    public string GasTypeName => GasTypeOptions[_gasTypeIndex < GasTypeOptions.Count ? _gasTypeIndex : 0];

    // ===== 结构集合 =====
    public ObservableCollection<IGUSheetItemVM> Sheets { get; } = new();
    public ObservableCollection<IGUGapItemVM> Gaps { get; } = new();

    /// <summary>顺序展示集合（片和间隔层交替），用于 UI ItemsControl</summary>
    public ObservableCollection<object> Sequence { get; } = new();

    // ===== 结果 =====
    private string _configResult = "";
    public string ConfigResult
    {
        get => _configResult;
        set { _configResult = value; OnPropertyChanged(); }
    }

    public bool IsConfirmed { get; set; }

    // ===== 构造 =====
    public IGUConfigViewModel()
    {
        BuildStructure();
    }

    private void BuildStructure()
    {
        // 取消订阅
        foreach (var s in Sheets) s.PropertyChanged -= OnItemChanged;
        foreach (var g in Gaps) g.PropertyChanged -= OnItemChanged;

        Sheets.Clear();
        Gaps.Clear();
        Sequence.Clear();

        int count = SheetCount;
        for (int i = 0; i < count; i++)
        {
            string pos = count == 2
                ? (i == 0 ? "外层" : "内层")
                : (i == 0 ? "外层" : (i == 1 ? "中间" : "内层"));

            var sheet = new IGUSheetItemVM
            {
                Order = i + 1,
                PositionLabel = pos,
                ThicknessIndex = 1, // 6mm
            };
            sheet.PropertyChanged += OnItemChanged;
            Sheets.Add(sheet);
            Sequence.Add(sheet);

            if (i < count - 1)
            {
                var gap = new IGUGapItemVM
                {
                    Order = i + 1,
                    ThicknessIndex = 1, // 9mm
                };
                gap.PropertyChanged += OnItemChanged;
                Gaps.Add(gap);
                Sequence.Add(gap);
            }
        }
        UpdateResult();
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IGUSheetItemVM.ThicknessIndex) ||
            e.PropertyName == nameof(IGUSheetItemVM.GlassConfigResult) ||
            e.PropertyName == nameof(IGUSheetItemVM.IsLowE) ||
            e.PropertyName == nameof(IGUGapItemVM.ThicknessIndex))
        {
            UpdateResult();
        }
    }

    public void UpdateResult()
    {
        var parts = new List<string>();
        foreach (var item in Sequence)
        {
            if (item is IGUSheetItemVM s)
            {
                if (!string.IsNullOrWhiteSpace(s.GlassConfigResult))
                    parts.Add(s.GlassConfigResult + (s.IsLowE ? "Low-E" : ""));
                else
                {
                    int thk = GlassThicknessOptions[s.ThicknessIndex < GlassThicknessOptions.Count ? s.ThicknessIndex : 0];
                    parts.Add($"{thk}全钢化" + (s.IsLowE ? "Low-E" : ""));
                }
            }
            else if (item is IGUGapItemVM g)
            {
                int gapThk = InsulatingThicknessOptions[g.ThicknessIndex < InsulatingThicknessOptions.Count ? g.ThicknessIndex : 0];
                parts.Add($"{gapThk}{GasTypeName}");
            }
        }
        ConfigResult = string.Join("+", parts);
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>中空玻璃中的一片玻璃</summary>
public class IGUSheetItemVM : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public int Order { get; set; }
    public string PositionLabel { get; set; } = "";

    private int _thicknessIndex = 1;
    public int ThicknessIndex
    {
        get => _thicknessIndex;
        set { _thicknessIndex = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThicknessIndex))); }
    }

    private bool _isLowE;
    public bool IsLowE
    {
        get => _isLowE;
        set { _isLowE = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLowE))); }
    }

    /// <summary>通过子窗口配置的结果字符串（如 "12全钢化+1.52PVB+12全钢化"）</summary>
    private string _glassConfigResult = "";
    public string GlassConfigResult
    {
        get => _glassConfigResult;
        set { _glassConfigResult = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GlassConfigResult))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayText))); }
    }

    /// <summary>显示文本：有配置结果显示结果，否则显示厚度</summary>
    public string DisplayText => string.IsNullOrWhiteSpace(GlassConfigResult) ? "" : GlassConfigResult;
}

/// <summary>中空玻璃间隔层</summary>
public class IGUGapItemVM : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public int Order { get; set; }

    private int _thicknessIndex = 1;
    public int ThicknessIndex
    {
        get => _thicknessIndex;
        set { _thicknessIndex = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThicknessIndex))); }
    }
}
