using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassManagement.Calc.Data;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>
/// CW27「公共建筑--非透明幕墙热工计算」屏 ViewModel（client 995×485）。
/// 控件布局来自 b7_inv_cw27.json 实机盘点（材料参数帧含 Adodc 层 DataGrid）；
/// 计算链走 <see cref="ThermalWallEngine"/>（b7_probe11/12 frida 逐位闭合）：
///   默认 3 层 = 参数库「热工数值」表行 1-3（千丝板/钢筋混凝土/聚苯乙烯）；
///   采暖房间→t_i=18、非采暖→12（probe12 heat_alt 运行期直读）；
///   湿度 CB「30/60」→φ=0.3/0.6（只进露点）；城市 te 同 CW26 固定权重 0.3/0.7。
/// V31 行为：预演前必须选城市（未选弹「请选择城市」）；已选城市后重选行为特殊（probe12 FAIL 登记）。
/// </summary>
public partial class ThermalWallCalcViewModel : ObservableObject
{
    public CalcSession Session { get; }
    private readonly CalcWizardViewModel _wizard;

    public ThermalWallCalcViewModel(CalcSession session, CalcWizardViewModel wizard,
        BuildingType building = BuildingType.公共建筑)
    {
        Session = session;
        _wizard = wizard;
        Building = building;
        // 默认 3 层 = 参数库「热工数值」表行 1-3（Adodc 数据源，probe11 fp 位实证）
        Layers.Add(new WallLayerRow("千丝板", 10, 0.3, this));
        Layers.Add(new WallLayerRow("钢筋混凝土", 240, 1.74, this));
        Layers.Add(new WallLayerRow("聚苯乙烯泡沫塑料", 100, 0.033, this));
    }

    /// <summary>建筑类别（公共/居住/工业；菜单 id 126/129/132 → 本屏，限值/文案/标题分档）。</summary>
    public BuildingType Building { get; }
    /// <summary>标题（V31 菜单逐字：公共/居住/工业建筑--非透明幕墙热工计算）。</summary>
    public string Title => Building switch
    {
        BuildingType.居住建筑 => "居住建筑--非透明幕墙热工计算",
        BuildingType.工业建筑 => "工业建筑--非透明幕墙热工计算",
        _ => "公共建筑--非透明幕墙热工计算",
    };

    // ══ 城市选择（同 CW26：省/市 ListBox + 确定；条目=GB50176 行）══
    /// <summary>V31「选择」按钮打开隐藏城市帧；确定/返回关闭。</summary>
    [ObservableProperty] private bool _cityFrameOpen;
    /// <summary>城市帧关系 CB@915,545。</summary>
    public static IReadOnlyList<string> CityRelationOptions { get; } =
        new[] { "工程所在地为左侧选中城市", "工程所在地参考左侧选中城市" };
    [ObservableProperty] private int _cityRelationIndex;
    public static IReadOnlyList<string> ProvinceOptions => ThermalCityTable.Provinces;
    [ObservableProperty] private int _provinceIndex = -1;
    [ObservableProperty] private int _cityIndex = -1;
    [ObservableProperty] private string _cityNote = "未选择城市（V31 预演将提示「请选择城市」）";
    public ThermalCityTable.City? ChosenCity { get; private set; }

    public IReadOnlyList<ThermalCityTable.City> CityList
    {
        get
        {
            if (ProvinceIndex < 0 || ProvinceIndex >= ProvinceOptions.Count)
                return Array.Empty<ThermalCityTable.City>();
            var list = new List<ThermalCityTable.City>();
            foreach (var c in ThermalCityTable.Of(ProvinceOptions[ProvinceIndex])) list.Add(c);
            return list;
        }
    }

    partial void OnProvinceIndexChanged(int value)
    {
        CityIndex = -1;
        OnPropertyChanged(nameof(CityList));
    }

    partial void OnCityIndexChanged(int value)
    {
        var list = CityList;
        if (value >= 0 && value < list.Count)
            CityNote = "城市名：" + list[value].Name + "（点「确定」写入地区参数，同 V31）";
    }

    /// <summary>「选择」按钮：打开隐藏城市帧（V31 行为）。</summary>
    [RelayCommand] private void OpenCityFrame() => CityFrameOpen = true;
    /// <summary>城市帧「返回」。</summary>
    [RelayCommand] private void BackFromCity() => CityFrameOpen = false;

    [RelayCommand]
    private void ConfirmCity()
    {
        var list = CityList;
        if (CityIndex < 0 || CityIndex >= list.Count)
        {
            StatusIsWarning = true;
            StatusText = "未选中城市条目（V31：条目必须双击选中）";
            return;
        }
        ChosenCity = list[CityIndex];
        CityNote = "已选城市：" + ChosenCity.Value.ItemText;
        CityFrameOpen = false;
        Invalidate("城市已改");
    }

    public void ChooseCityByName(string name)
    {
        foreach (var p in ProvinceOptions)
            foreach (var c in ThermalCityTable.Of(p))
                if (c.Name == name)
                {
                    ChosenCity = c;
                    CityNote = "已选城市：" + c.ItemText;
                    return;
                }
        throw new ArgumentException("城市表中无 " + name, nameof(name));
    }

    // ══ 房间采暖情况（CB@623,431：采暖→t_i=18 / 非采暖→12，probe12 e_i 链直读）══
    public static IReadOnlyList<string> HeatingOptions { get; } =
        new[] { "采暖房间", "非采暖房间" };
    [ObservableProperty] private int _heatingIndex;
    public double TIndoor => Building == BuildingType.工业建筑 ? 16 : (HeatingIndex == 0 ? 18 : 12);

    // ══ 室内环境湿度（CB@508,423「30/60」→φ）══
    public static IReadOnlyList<string> HumidityOptions { get; } = new[] { "30", "60" };
    [ObservableProperty] private int _humidityIndex;
    public double PhiIndoor => HumidityIndex == 0 ? 0.3 : 0.6;

    // ══ 空气层参数帧（CB@619,350 类型 + CB@619,371 热流方向；默认=一般空气间层 + 垂直空气层）══
    // ★ 2026-10-27 P4 已取证闭环（b7_probe22_airgap.json）：3 类型 × 3 热流共 9 档 frida 逐档直读，
    //   档位热阻查表落在 ThermalWallEngine.AirGapRsiTable（默认 一般+垂直 = 0.18，与旧固定值同）；
    //   同批实测 R_air（ra=0x16D35C8 的 op1）9 档恒为 0.11 ⇒ RAirGap 保持单值即可。
    //   旧注释「其余档位未逐位取证 → 链值恒 0.11」已作废。
    public static IReadOnlyList<string> AirGapTypeOptions { get; } =
        new[] { "一般空气间层", "单面铝箔空气间层", "双面铝箔空气间层" };
    public static IReadOnlyList<string> AirGapFlowOptions { get; } =
        new[] { "热流向下（水平、倾斜）", "热流向上（水平、倾斜）", "垂直空气层" };
    [ObservableProperty] private int _airGapTypeIndex;
    [ObservableProperty] private int _airGapFlowIndex = 2;   // 默认「垂直空气层」
    public double RAirGap { get; set; } = 0.11;

    // ══ 材料参数（Adodc 层表 = 参数库「热工数值」；默认 3 行，厚度/λ 可编辑）══
    public ObservableCollection<WallLayerRow> Layers { get; } = new();

    [RelayCommand]
    private void AddLayer()
    {
        Layers.Add(new WallLayerRow("（新层：材料名/厚/λ）", 0, 1, this));
        Invalidate("层表已改");
    }

    internal void MoveUp(WallLayerRow row)
    {
        int i = Layers.IndexOf(row);
        if (i > 0) { Layers.Move(i, i - 1); Invalidate("层序已改"); }
    }

    internal void MoveDown(WallLayerRow row)
    {
        int i = Layers.IndexOf(row);
        if (i >= 0 && i < Layers.Count - 1) { Layers.Move(i, i + 1); Invalidate("层序已改"); }
    }

    internal void RemoveLayer(WallLayerRow row)
    {
        if (Layers.Count > 1) { Layers.Remove(row); Invalidate("层表已改"); }
    }

    // ══ 地区参数帧（气候区/高度/体型系数——城市 te 链由城市行驱动；probe12 tb(850,412) te 手改语义待细究）══
    public static IReadOnlyList<string> ClimateZoneOptions { get; } =
        new[] { "严寒地区A区", "严寒地区B区", "严寒地区C区", "寒冷地区A区", "寒冷地区B区",
                "夏热冬冷地区A区", "夏热冬冷地区B区", "夏热冬暖地区A区", "夏热冬暖地区B区",
                "温和地区A区", "温和地区B区" };
    public static IReadOnlyList<string> HeightOptions { get; } =
        new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "≥14" };
    public static IReadOnlyList<string> ClassOptions { get; } =
        new[] { "甲类公共建筑", "乙类公共建筑" };
    [ObservableProperty] private int _zoneIndex = 2;          // CB@827,347 默认「严寒地区C区」
    [ObservableProperty] private int _heightIndex;            // CB@827,390
    [ObservableProperty] private int _classIndex;             // CB@827,369
    [ObservableProperty] private string _teManualText = "3.0"; // tb(850,412)（V31 te 手改语义未归因，保留）
    [ObservableProperty] private string _shapeCoefText = "0.103"; // 体型系数帧

    // ══ 组合结构帧 ══
    public static IReadOnlyList<string> OrientationOptions { get; } =
        new[] { "东", "东南", "南", "西南", "西", "西北", "北", "东北" };
    public static IReadOnlyList<string> RegionTypeOptions { get; } =
        new[] { "寒冷地区", "夏热冬冷地区", "夏热冬暖地区" };
    [ObservableProperty] private int _orientationIndex;        // CB@554,383
    [ObservableProperty] private int _regionTypeIndex;         // CB@540,362
    [ObservableProperty] private string _combStructText = "2.58"; // tb@548,356

    public double KLimb => ThermalLimits.WallKLimb(Building);   // 公共 0.8 / 居住 1.0 / 工业 1.1（b7_probe28 直读）

    // ══ 计算限制取值依据（CB@1136,335）══
    public static IReadOnlyList<string> LimitBasisOptions { get; } =
        new[] { "按对应的节能计算规范", "按设计院给出的要求" };
    [ObservableProperty] private int _limitBasisIndex;

    // ══ 输出 ══
    public ObservableCollection<PreviewLine> Results { get; } = new();
    public bool HasResults => Results.Count > 0;
    public string PlaceholderText => HasResults ? "" :
        "先在「城市选择」页选省→市→点「确定」，再点「预演」（同 V31：未选城市会弹「请选择城市」）。";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _statusIsWarning;

    private void Invalidate(string reason)
    {
        OnPropertyChanged(nameof(TIndoor));
        OnPropertyChanged(nameof(PhiIndoor));
        OnPropertyChanged(nameof(RAirGap));
        if (Results.Count == 0) return;
        Results.Clear();
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(PlaceholderText));
        StatusIsWarning = true;
        StatusText = reason + "，请重新预演";
    }

    private ThermalWallEngine.Input BuildInput()
    {
        var city = ChosenCity ?? new ThermalCityTable.City("sh", "上海", "3A", "3", 0.5, -3.0);
        var it = new ThermalWallEngine.Input
        {
            Building = Building,
            RAirGap = RAirGap,
            AirGapTypeIndex = AirGapTypeIndex,
            AirGapFlowIndex = AirGapFlowIndex,
            TIndoor = TIndoor,
            PhiIndoor = PhiIndoor,
            CityWinterRh = city.Rh,
            CityTeMin = city.TeMin,
            KLimb = KLimb,
        };
        it.Layers.Clear();
        foreach (var r in Layers)
            it.Layers.Add(new ThermalWallEngine.Layer(r.ThkMm, r.Lambda));
        return it;
    }

    [RelayCommand]
    private void Preview()
    {
        if (ChosenCity is null)
        {
            Results.Clear();
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(PlaceholderText));
            StatusIsWarning = true;
            StatusText = "请选择城市";
            return;
        }
        var it = BuildInput();
        var o = ThermalWallEngine.Compute(it);
        var raw = ThermalWallEngine.FormatOutput(it, o);
        Results.Clear();
        const string Sep = "********************************************";
        foreach (var seg in raw.Split(Sep, StringSplitOptions.None))
            Add(seg.Trim().Length == 0 ? "" : seg, ColorNormal);
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(PlaceholderText));
        StatusIsWarning = false;
        StatusText = $"预演完成：K {o.KWinter:0.###}、tsi {o.Tsi:0.###}℃、露点 {o.DewPoint:0.###}℃";
        LastRawOutput = raw;
    }

    public string LastRawOutput { get; private set; } = "";

    private static readonly IBrush ColorNormal = Brushes.Black;
    private void Add(string text, IBrush brush) => Results.Add(new PreviewLine(text, brush));

    /// <summary>材料层行（厚度 mm / 导热系数；改后重预演即进链）。行内命令回调父 VM。</summary>
    public partial class WallLayerRow : ObservableObject
    {
        private readonly ThermalWallCalcViewModel _owner;
        [ObservableProperty] private string _name;
        [ObservableProperty] private string _thkText;
        [ObservableProperty] private string _lambdaText;
        public WallLayerRow(string name, double thk, double lambda, ThermalWallCalcViewModel owner)
        {
            _owner = owner;
            Name = name;
            ThkText = thk.ToString("0.###", CultureInfo.InvariantCulture);
            LambdaText = lambda.ToString("0.####", CultureInfo.InvariantCulture);
            MoveUpCommand = new RelayCommand(() => _owner.MoveUp(this));
            MoveDownCommand = new RelayCommand(() => _owner.MoveDown(this));
            RemoveSelfCommand = new RelayCommand(() => _owner.RemoveLayer(this));
        }
        public RelayCommand MoveUpCommand { get; }
        public RelayCommand MoveDownCommand { get; }
        public RelayCommand RemoveSelfCommand { get; }
        public double ThkMm => double.TryParse(ThkText, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
        public double Lambda => double.TryParse(LambdaText, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 1;
        /// <summary>层热阻 R=t/1000/λ（链②不 round，原值显示供核对参数库）。</summary>
        public string RText => (ThkMm / 1000.0 / Lambda)
            .ToString("0.00000000000000", CultureInfo.InvariantCulture);
        partial void OnThkTextChanged(string value) => OnPropertyChanged(nameof(RText));
        partial void OnLambdaTextChanged(string value) => OnPropertyChanged(nameof(RText));
    }

    [RelayCommand] private void NextStep() => _wizard.GoBasic();
    [RelayCommand] private void Exit() => _wizard.CloseForm();
}
