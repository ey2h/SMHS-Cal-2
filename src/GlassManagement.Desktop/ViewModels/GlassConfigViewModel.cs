using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace GlassManagement.Desktop.ViewModels;

/// <summary>
/// 单片/夹胶玻璃配置 ViewModel (移植自霸总 WPF 设计)
/// 支持两种模式："单片" 与 "夹胶"
/// 配置完成后通过 ConfigResult 输出拼接字符串
/// </summary>
public class GlassConfigViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    // ===== 选项列表 =====
    public List<int> GlassThicknessOptions { get; } = new() { 5, 6, 8, 10, 12, 15, 19, 20 };
    public List<double> FilmThicknessOptions { get; } = new() { 0.76, 1.14, 1.52, 2.28 };
    public List<string> FilmTypeOptions { get; } = new() { "PVB", "SGP" };
    public List<string> TemperedTypeOptions { get; } = new() { "浮法", "半钢化", "全钢化" };
    public List<string> BasicGlassCategoryOptions { get; } = new() { "单片", "夹胶" };
    public List<int> LaminatedSheetCountOptions { get; } = new() { 2, 3, 4, 5 };

    // ===== 基础类型选择 =====
    private string _selectedBasicCategory = "单片";
    public string SelectedBasicCategory
    {
        get => _selectedBasicCategory;
        set
        {
            _selectedBasicCategory = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsSingleMode));
            OnPropertyChanged(nameof(IsLaminatedMode));
            if (value == "单片") IsChineseStandard = true;
            UpdateResult();
        }
    }
    public bool IsSingleMode => SelectedBasicCategory == "单片";
    public bool IsLaminatedMode => SelectedBasicCategory == "夹胶";

    // ===== 单片模式 =====
    private int _singleThicknessIndex = 1; // 默认6mm
    public int SingleThicknessIndex
    {
        get => _singleThicknessIndex;
        set { _singleThicknessIndex = value; OnPropertyChanged(); UpdateResult(); }
    }
    public int SingleThickness => GlassThicknessOptions[_singleThicknessIndex < GlassThicknessOptions.Count ? _singleThicknessIndex : 0];

    private int _singleTemperedIndex = 2; // 默认全钢化
    public int SingleTemperedIndex
    {
        get => _singleTemperedIndex;
        set { _singleTemperedIndex = value; OnPropertyChanged(); UpdateResult(); }
    }

    private bool _isChineseStandard = true;
    public bool IsChineseStandard
    {
        get => _isChineseStandard;
        set
        {
            _isChineseStandard = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanSelectOtherTempered));
            if (value) SingleTemperedIndex = 2; // 强制全钢化
            UpdateResult();
        }
    }
    public bool CanSelectOtherTempered => !IsChineseStandard;

    // ===== 夹胶模式 =====
    private int _lamSheetCountIndex = 0; // 默认2片
    public int LamSheetCountIndex
    {
        get => _lamSheetCountIndex;
        set { _lamSheetCountIndex = value; OnPropertyChanged(); RebuildLamStructure(); UpdateResult(); }
    }
    public int LamSheetCount => LaminatedSheetCountOptions[_lamSheetCountIndex < LaminatedSheetCountOptions.Count ? _lamSheetCountIndex : 0];

    private int _lamFilmTypeIndex = 0; // PVB
    public int LamFilmTypeIndex
    {
        get => _lamFilmTypeIndex;
        set { _lamFilmTypeIndex = value; OnPropertyChanged(); UpdateResult(); }
    }

    public ObservableCollection<LamSheetVM> LamSheets { get; } = new();
    public ObservableCollection<LamLayerVM> LamLayers { get; } = new();

    // ===== 结果 =====
    private string _configResult = "";
    public string ConfigResult
    {
        get => _configResult;
        set { _configResult = value; OnPropertyChanged(); }
    }

    /// <summary>是否已确认（对话框返回值）</summary>
    public bool IsConfirmed { get; set; }

    // ===== 构造 =====
    public GlassConfigViewModel()
    {
        RebuildLamStructure();
        UpdateResult();
    }

    private void RebuildLamStructure()
    {
        LamSheets.Clear();
        LamLayers.Clear();
        int count = LamSheetCount;
        for (int i = 0; i < count; i++)
        {
            string pos = count <= 1 ? "单片" : (i == 0 ? "外侧" : (i == count - 1 ? "内侧" : "中间"));
            LamSheets.Add(new LamSheetVM { Order = i + 1, ThicknessIndex = 1, TemperedIndex = 2, PositionLabel = pos });
            if (i < count - 1)
                LamLayers.Add(new LamLayerVM { Order = i + 1, FilmTypeIndex = LamFilmTypeIndex, FilmThicknessIndex = 2 });
        }
    }

    private void UpdateResult()
    {
        if (IsSingleMode)
        {
            string tempered = TemperedTypeOptions[_singleTemperedIndex < TemperedTypeOptions.Count ? _singleTemperedIndex : 0];
            ConfigResult = $"{SingleThickness}{tempered}";
        }
        else
        {
            var parts = new List<string>();
            var thkOpts = GlassThicknessOptions;
            var filmThkOpts = FilmThicknessOptions;
            for (int i = 0; i < LamSheets.Count; i++)
            {
                var s = LamSheets[i];
                int thk = thkOpts[s.ThicknessIndex < thkOpts.Count ? s.ThicknessIndex : 0];
                string tmp = TemperedTypeOptions[s.TemperedIndex < TemperedTypeOptions.Count ? s.TemperedIndex : 0];
                parts.Add($"{thk}{tmp}");
                if (i < LamLayers.Count)
                {
                    var l = LamLayers[i];
                    double ft = filmThkOpts[l.FilmThicknessIndex < filmThkOpts.Count ? l.FilmThicknessIndex : 0];
                    string filmType = FilmTypeOptions[l.FilmTypeIndex < FilmTypeOptions.Count ? l.FilmTypeIndex : 0];
                    parts.Add($"{ft}{filmType}");
                }
            }
            ConfigResult = string.Join("+", parts);
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
