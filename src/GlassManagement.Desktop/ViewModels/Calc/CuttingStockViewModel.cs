using System.Collections.ObjectModel;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>
/// 型材优化（线材套裁，菜单 id=192）视图模型。
/// ⚠ 算法为行业标准 FFD；V31 算法一/二 的私有启发式待后续逐指令还原（见 CuttingStockEngine 注释）。
/// </summary>
public sealed partial class CuttingStockViewModel : ObservableObject
{
    public partial class PartRow : ObservableObject
    {
        [ObservableProperty] private string _length = "";
        [ObservableProperty] private string _qty = "";
    }

    public ObservableCollection<PartRow> Parts { get; } = new();

    [ObservableProperty] private string _stockLengthText = "6000";
    [ObservableProperty] private string _kerfText = "5";
    [ObservableProperty] private string _endTrimText = "50";
    [ObservableProperty] private string _resultText = "";

    public CuttingStockViewModel()
    {
        // 默认填 V31 参数库「线材优化合并后数据」前 4 行
        Parts.Add(new PartRow { Length = "2050", Qty = "180" });
        Parts.Add(new PartRow { Length = "20", Qty = "250" });
        Parts.Add(new PartRow { Length = "3560", Qty = "50" });
        Parts.Add(new PartRow { Length = "3650", Qty = "30" });
        for (int i = 0; i < 4; i++) Parts.Add(new PartRow());
    }

    private static double Parse(string s, double def)
        => double.TryParse(s?.Trim(), out var v) ? v : def;

    [RelayCommand]
    private void Optimize()
    {
        var list = new List<CuttingStockEngine.Part>();
        foreach (var r in Parts)
        {
            double len = Parse(r.Length, 0);
            int qty = (int)Parse(r.Qty, 0);
            if (len > 0 && qty > 0) list.Add(new CuttingStockEngine.Part(len, qty));
        }
        if (list.Count == 0) { ResultText = "请至少输入一行有效的下料长度与数量。"; return; }

        double stock = Parse(StockLengthText, 6000);
        double kerf = Parse(KerfText, 5);
        double trim = Parse(EndTrimText, 50);
        var res = CuttingStockEngine.Optimize(list, stock, kerf, trim);
        ResultText = CuttingStockEngine.ToReport(res, stock);
    }

    [RelayCommand]
    private void Clear()
    {
        foreach (var r in Parts) { r.Length = ""; r.Qty = ""; }
        ResultText = "";
    }
}
