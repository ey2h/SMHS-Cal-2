using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>
/// 民用建筑节能计算加权计算器（菜单 id=136）视图模型。
/// 每行 3 列：列1=权重(面积)、列2=值1、列3=值2。输出：Σ权重、值1 加权平均、值2 加权平均。
/// </summary>
public sealed partial class WeightedAverageViewModel : ObservableObject
{
    public partial class Row : ObservableObject
    {
        [ObservableProperty] private string _weight = "";
        [ObservableProperty] private string _valueA = "";
        [ObservableProperty] private string _valueB = "";
    }

    public ObservableCollection<Row> Rows { get; } = new();

    [ObservableProperty] private string _sumWeight = "";
    [ObservableProperty] private string _weightedA = "";
    [ObservableProperty] private string _weightedB = "";

    public WeightedAverageViewModel()
    {
        for (int i = 0; i < 15; i++) Rows.Add(new Row());
    }

    private static double Parse(string s)
        => double.TryParse(s?.Trim(), out var v) ? v : 0.0;

    [RelayCommand]
    private void Calc()
    {
        var list = new System.Collections.Generic.List<WeightedAverageEngine.Row>();
        foreach (var r in Rows)
        {
            double w = Parse(r.Weight), a = Parse(r.ValueA), b = Parse(r.ValueB);
            if (w == 0 && a == 0 && b == 0) continue;
            list.Add(new WeightedAverageEngine.Row(w, a, b));
        }
        var res = WeightedAverageEngine.Compute(list);
        SumWeight = WeightedAverageEngine.FormatSumWeight(res.SumWeight);
        WeightedA = WeightedAverageEngine.FormatWeighted(res.WeightedA);
        WeightedB = WeightedAverageEngine.FormatWeighted(res.WeightedB);
    }

    [RelayCommand]
    private void Clear()
    {
        foreach (var r in Rows) { r.Weight = ""; r.ValueA = ""; r.ValueB = ""; }
        SumWeight = ""; WeightedA = ""; WeightedB = "";
    }
}
