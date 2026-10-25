using System;
using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassManagement.Calc.Data;
using GlassManagement.Calc.Engines;
using GlassManagement.Desktop.ViewModels.Calc;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>
/// 「其它玻璃计算」——V31 主菜单「其它计算 → 其它玻璃计算」（命令 id=168）的独立计算器。
///
/// <para><b>与常规幕墙玻璃屏的区别</b>：这是一个**独立的四边简支计算器**，
/// 输入（尺寸 / 风荷载 / 4 片厚度）全部在本面板填，<b>不依赖第一页</b>；
/// 输出也不是幕墙流程的一环。</para>
///
/// <para><b>实机取证</b>（2026-10-25，窗体 763×500）：</para>
/// <code>
/// Frame 其它玻璃计算（目前只支持四边简支形式） (11,26) 745x465
///   Frame 基本参数 (19,69) 177x169
///     ComboBox (83,113) 73x20        ← 未定位含义（待补）
///     ComboBox (126,138) 57x20       ← 未定位含义（待补）
///     TextBox  αmax 0.16 (83,162)
///     TextBox  宽   1000 (83,187)
///     TextBox  高   2000 (83,210)
///   Frame 玻璃配置 (203,63) 153x81
///     ○ 四玻外夹层双中空 (211,79)   ○ 四玻内夹层双中空 (211,99)   ○ 多片夹层 (211,119)
///   Frame 支撑情况 (363,63) 97x81   ○ 四边简支 (371,100)
///   Frame 夹层配置 (203,146) 242x97  ComboBox 胶片类型(303,166) / 厚度(303,191) en=0
///   Frame 外片(19,259) / 次外片(131,259) / 次内片(243,259) / 内片(355,259)  各 110x167
///     每框 厚度 ComboBox (x+40,279) 57x20 + 5 档种类单选 y=306/328/351/373/396
///   输出 RichText (475,50) 277x377      按钮 预演(611,434) 下一步(671,458) 退出(611,458)
/// </code>
///
/// <para><b>实测输出</b>（四玻外夹层双中空、1000×2000、4×6mm、wk=1.0KPa、αmax=0.16）：</para>
/// <code>
/// ************************************   ← 36 星
/// 玻璃部分风荷载标准值为:1KPa
/// ************************************
/// 外片玻璃强度计算值:8.833MPa / 次外片 8.833 / 次内片 7.217 / 内片 7.217
/// ************************************
/// 玻璃挠度计算值:2.356mm
/// 玻璃挠度容许值:16.667mm(四边简支)
/// ************************************
/// </code>
/// </summary>
public partial class OtherGlassCalcViewModel : ObservableObject
{
    private const string Sep36 = "************************************";
    private const string Dash = "———————————————";

    private static readonly IBrush ColorNormal = Brushes.Black;
    private static readonly IBrush ColorRule = new SolidColorBrush(Color.Parse("#808080"));
    private static readonly IBrush ColorOk = new SolidColorBrush(Color.Parse("#007A00"));
    private static readonly IBrush ColorFail = new SolidColorBrush(Color.Parse("#C00000"));

    /// <summary>厚度下拉 10 档（与 V31 一致，默认索引 3 = 6mm）。</summary>
    public static readonly double[] ThicknessOptions = { 3.2, 4, 5, 6, 8, 10, 12, 15, 19, 20 };

    public OtherGlassCalcViewModel()
    {
        Rows = new ObservableCollection<PreviewLine>();
        PreviewCommand = new RelayCommand(Preview);
        ResetCommand = new RelayCommand(Reset);
    }

    // ---------------- 输入 ----------------

    [ObservableProperty] private double _widthMm = 1000;
    [ObservableProperty] private double _heightMm = 2000;

    /// <summary>风荷载标准值 kPa（本面板独立输入；实测显示 1KPa）。</summary>
    [ObservableProperty] private double _windKpa = 1.0;

    /// <summary>地震影响系数最大值 αmax（实测 0.16）。</summary>
    [ObservableProperty] private double _alphaMax = 0.16;

    [ObservableProperty] private double _t1 = 6;
    [ObservableProperty] private double _t2 = 6;
    [ObservableProperty] private double _t3 = 6;
    [ObservableProperty] private double _t4 = 6;

    [ObservableProperty] private string _kind1 = "钢化玻璃";
    [ObservableProperty] private string _kind2 = "钢化玻璃";
    [ObservableProperty] private string _kind3 = "钢化玻璃";
    [ObservableProperty] private string _kind4 = "钢化玻璃";

    /// <summary>构型：<c>true</c>=四玻外夹层双中空（实测）；<c>false</c>=四玻内夹层双中空（未实测，暂禁用）。</summary>
    [ObservableProperty] private bool _isOuterLaminated = true;
    [ObservableProperty] private bool _isInnerLaminated;
    [ObservableProperty] private bool _isMultiLaminated;

    /// <summary>夹层胶片类型（PVB/SGP）—— 本模块实测只跑过默认档，暂不参与计算。</summary>
    [ObservableProperty] private int _interlayerIndex;

    partial void OnIsOuterLaminatedChanged(bool value)
    {
        if (value) { IsInnerLaminated = false; IsMultiLaminated = false; }
    }

    partial void OnIsInnerLaminatedChanged(bool value)
    {
        if (value) { IsOuterLaminated = false; IsMultiLaminated = false; }
    }

    partial void OnIsMultiLaminatedChanged(bool value)
    {
        if (value) { IsOuterLaminated = false; IsInnerLaminated = false; }
    }

    partial void OnWidthMmChanged(double value) => Invalidate();
    partial void OnHeightMmChanged(double value) => Invalidate();
    partial void OnWindKpaChanged(double value) => Invalidate();
    partial void OnAlphaMaxChanged(double value) => Invalidate();
    partial void OnT1Changed(double value) => Invalidate();
    partial void OnT2Changed(double value) => Invalidate();
    partial void OnT3Changed(double value) => Invalidate();
    partial void OnT4Changed(double value) => Invalidate();

    // ---------------- 输出 ----------------

    public ObservableCollection<PreviewLine> Results => Rows;
    public ObservableCollection<PreviewLine> Rows { get; }

    public bool HasResults => Rows.Count > 0;
    public string PlaceholderText => HasResults ? "" : "点「预演」按当前玻璃配置计算强度与挠度。";

    [ObservableProperty] private string _statusText = "四边简支形式；输入尺寸、风荷载与四片厚度后点「预演」。";
    [ObservableProperty] private bool _statusIsWarning;

    public string ConfigCaption => IsOuterLaminated ? "四玻外夹层双中空"
                                  : IsInnerLaminated ? "四玻内夹层双中空"
                                  : "多片夹层";

    public bool PreviewEnabled => WidthMm > 0 && HeightMm > 0 && WindKpa > 0 && !IsMultiLaminated;
    public string PreviewBlockedReason => IsMultiLaminated
        ? "「多片夹层」构型尚未复刻（只做了四玻外/内夹层双中空）"
        : WidthMm <= 0 || HeightMm <= 0 ? "玻璃尺寸无效"
        : WindKpa <= 0 ? "请填写风荷载标准值" : "";

    // ---------------- 命令 ----------------

    public IRelayCommand PreviewCommand { get; }
    public IRelayCommand ResetCommand { get; }

    private void Reset()
    {
        WidthMm = 1000; HeightMm = 2000; WindKpa = 1.0; AlphaMax = 0.16;
        T1 = T2 = T3 = T4 = 6;
        Kind1 = Kind2 = Kind3 = Kind4 = "钢化玻璃";
        IsOuterLaminated = true;
        Rows.Clear();
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(PlaceholderText));
        StatusIsWarning = false;
        StatusText = "已恢复默认值。";
    }

    private void Invalidate()
    {
        OnPropertyChanged(nameof(PreviewEnabled));
        OnPropertyChanged(nameof(PreviewBlockedReason));
        OnPropertyChanged(nameof(ConfigCaption));
        if (Rows.Count == 0) return;
        Rows.Clear();
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(PlaceholderText));
        StatusIsWarning = true;
        StatusText = "输入已变化，请重新预演";
    }

    private void Preview()
    {
        if (!PreviewEnabled) { StatusIsWarning = true; StatusText = PreviewBlockedReason; return; }

        double a = Math.Min(WidthMm, HeightMm);
        double wk = WindKpa / 1000.0;
        Rows.Clear();
        try
        {
            if (IsMultiLaminated)
            {
                StatusIsWarning = true;
                StatusText = PreviewBlockedReason;
                return;
            }

            var r = GlassEngine.ComputeFourPlyLaminatedInsulated(
                wk, WidthMm, HeightMm, T1, T2, T3, T4, AlphaMax, IsOuterLaminated);

            var name = new[] { "外片", "次外片", "次内片", "内片" };
            var kinds = new[] { Kind1, Kind2, Kind3, Kind4 };
            var ths = new[] { T1, T2, T3, T4 };

            Add(Sep36, ColorRule);
            Add($"玻璃部分风荷载标准值为:{WindKpa:0.###}KPa", ColorNormal);
            Add(Sep36, ColorRule);
            for (int i = 0; i < r.Plies.Length; i++)
            {
                Add($"{name[i]}玻璃强度计算值:{r.Plies[i].Sigma:0.###}MPa", ColorNormal);
                Add($"选用的玻璃强度设计值:{GlassAllowables.Caption(kinds[i], ths[i])}", ColorNormal);
                Judge(r.Plies[i].Sigma <= GlassAllowables.Bending(kinds[i], ths[i]));
                if (i < r.Plies.Length - 1) Add(Dash, ColorRule);
            }
            Add(Sep36, ColorRule);
            Add($"玻璃挠度计算值:{r.Deflection:0.###}mm", ColorNormal);
            // ⚠ 这里文案是「玻璃挠度容许值」，比常规幕墙屏少了「选用」二字，且后缀是 (四边简支)
            Add($"玻璃挠度容许值:{r.Limit:0.###}mm(四边简支)", ColorNormal);
            Judge(r.Deflection <= r.Limit);
            Add(Sep36, ColorRule);

            _ = a;
        }
        finally
        {
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(PlaceholderText));
        }

        StatusIsWarning = false;
        StatusText = $"预演完成：{ConfigCaption}（四边简支）";
    }

    private void Judge(bool ok) => Add(ok ? "满足要求(√)" : "不满足要求(×)", ok ? ColorOk : ColorFail);

    private void Add(string text, IBrush color) => Rows.Add(new PreviewLine(text, color));
}
