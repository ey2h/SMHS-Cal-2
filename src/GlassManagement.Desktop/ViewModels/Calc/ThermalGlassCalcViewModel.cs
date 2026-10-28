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
/// CW26「公共建筑--透明幕墙（门窗）热工计算」屏 ViewModel（client 896×550）。
/// 控件布局逐控件来自 b7_inv_cw26.json 实机盘点（25 帧/23 combo/15 option/13 button）；
/// 计算链全部走 <see cref="ThermalGlassEngine"/>（b7_probe7/9/10 frida 逐位闭合）。
/// V31 行为铁律（b7_probe4/6 实采）：
///   ① 预演前必须「选择」城市（否则弹「请选择城市」中断、0 输出）——城市行 5 元组写
///      te 链（CityWinterRh/CityTeMin，市框条目=GB50176 行「上海,3A,3,0.5,-3.0」）；
///   ② 湿度 combo 只进露点 φ（30→0.3/60→0.6，probe8e 湿度 60 后 tsi 仍 5.008 实证）；
///   ③ 框材质 combo 驱动 Kf（程序内置九项，FrameK 表）；
///   ④ 气体=氩气、LOW-E、幕墙角度、体形系数、计算高度对默认几何主输出零差异
///      （probe8e tbdiff 实证）——控件保留但不进链。
/// </summary>
public partial class ThermalGlassCalcViewModel : ObservableObject
{
    public CalcSession Session { get; }
    private readonly CalcWizardViewModel _wizard;

    public ThermalGlassCalcViewModel(CalcSession session, CalcWizardViewModel wizard,
        BuildingType building = BuildingType.公共建筑)
    {
        Session = session;
        _wizard = wizard;
        Building = building;
    }

    /// <summary>建筑类别（公共/居住/工业；菜单 id 125/128/131 → 本屏，限值/文案/标题分档）。</summary>
    public BuildingType Building { get; }
    /// <summary>标题（V31 菜单逐字：公共/居住/工业建筑--透明幕墙（门窗）热工计算）。</summary>
    public string Title => Building switch
    {
        BuildingType.居住建筑 => "居住建筑--透明幕墙（门窗）热工计算",
        BuildingType.工业建筑 => "工业建筑--透明幕墙（门窗）热工计算",
        _ => "公共建筑--透明幕墙（门窗）热工计算",
    };

    // ══ 城市选择（V31 隐藏帧「节能计算-工程所在城市选择」：省/市两 ListBox+确定）══
    /// <summary>V31「选择」按钮打开隐藏城市帧；确定/返回关闭。</summary>
    [ObservableProperty] private bool _cityFrameOpen;
    /// <summary>城市帧关系 CB@963,526（工程所在地为/参考左侧选中城市）。</summary>
    public static IReadOnlyList<string> CityRelationOptions { get; } =
        new[] { "工程所在地为左侧选中城市", "工程所在地参考左侧选中城市" };
    [ObservableProperty] private int _cityRelationIndex;
    public static IReadOnlyList<string> ProvinceOptions => ThermalCityTable.Provinces;
    [ObservableProperty] private int _provinceIndex = -1;
    [ObservableProperty] private int _cityIndex = -1;
    [ObservableProperty] private string _cityNote = "未选择城市（V31 预演将提示「请选择城市」）";
    /// <summary>已确定城市（null=未选，预演被 V31 行为拦住）。</summary>
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

    /// <summary>「确定」：把市条目 5 元组写入 te 链（V31 确定后 tw/te,min 文本框 0.5/-3.0）。</summary>
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

    /// <summary>测试/脚本快捷：直接选「上海」（probe6 基线链）。</summary>
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

    // ══ 地区参数帧 ══
    public static IReadOnlyList<string> ClimateZoneOptions { get; } =
        new[] { "严寒地区A区", "严寒地区B区", "严寒地区C区", "寒冷地区A区", "寒冷地区B区",
                "夏热冬冷地区A区", "夏热冬冷地区B区", "夏热冬暖地区A区", "夏热冬暖地区B区",
                "温和地区A区", "温和地区B区" };
    public static IReadOnlyList<string> HeightOptions { get; } =
        new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "≥14" };
    [ObservableProperty] private int _classIndex;      // 0=甲类公共建筑,1=乙类（CB@780,325）
    [ObservableProperty] private int _zoneIndex;       // CB@780,304（装饰：主链用城市行）
    [ObservableProperty] private int _heightIndex;     // CB@590,325（装饰：probe8e 零 diff）
    public static IReadOnlyList<string> ClassOptions { get; } =
        new[] { "甲类公共建筑", "乙类公共建筑" };

    // ══ 室内环境湿度（CB@1005,362「30/60」→ φ=0.3/0.6，probe9 运行期直读）══
    public static IReadOnlyList<string> HumidityOptions { get; } = new[] { "30", "60" };
    [ObservableProperty] private int _humidityIndex;
    public double PhiIndoor => HumidityIndex == 0 ? 0.3 : 0.6;

    // ══ 玻璃K值帧（人工给出/计算得到 option + 2.5 文本框：主链 K_glass 由计算得到，装饰）══
    [ObservableProperty] private bool _kByCalc = true;
    public bool KByManual { get => !KByCalc; set { KByCalc = !value; if (value) Invalidate("K值方式已改"); } }

    // ══ 框的参数帧（材质 CB@586,449 九项 → Kf=FrameK，probe10 运行期直读）══
    public static IReadOnlyList<string> FrameMaterialOptions { get; } =
        new[] { "普通铝合金型材", "断热铝合金型材", "聚氨酯塑钢带筋", "PVC两腔无金属筋",
                "PVC两腔有金属筋", "PVC三腔无金属筋", "木框", "木-金属框", "钢框" };
    [ObservableProperty] private int _frameMaterialIndex;
    public double KFrame => ThermalGlassEngine.FrameK(FrameMaterialOptions[
        Math.Clamp(FrameMaterialIndex, 0, 8)]);

    // ══ 玻璃参数帧 ══
    public static IReadOnlyList<string> GlassThkOptions { get; } =
        new[] { "3", "4", "5", "6", "8", "10", "12", "15", "19", "20" };
    /// <summary>外片厚度（CB@626,631 默认 cursel=3 → 6mm，链②输入）。</summary>
    [ObservableProperty] private int _glassThkIndex = 3;
    public double GlassThkMm => double.Parse(GlassThkOptions[
        Math.Clamp(GlassThkIndex, 0, 9)], CultureInfo.InvariantCulture);
    public static IReadOnlyList<string> GlassTypeOptions { get; } =
        new[] { "普通玻璃", "镀膜玻璃", "LOW-E镀膜" };
    /// <summary>外片类型（CB@598,672）——LOW-E 只写发射率框、不进 K/SHGC 主链（probe9 实证）。</summary>
    [ObservableProperty] private int _glassTypeIndex;
    /// <summary>中片/内片厚度与类型（CB@767,633 / 965,631…，中空结构才参与；装饰保留）。</summary>
    [ObservableProperty] private int _midThkIndex = 3;
    [ObservableProperty] private int _midTypeIndex;
    [ObservableProperty] private int _inThkIndex = 3;
    [ObservableProperty] private int _inTypeIndex;
    /// <summary>PVB 夹层厚度下拉（CB@598,711 默认 items=['2']，夹层结构用；装饰保留）。</summary>
    public static IReadOnlyList<string> PvbInterlayerOptions { get; } = new[] { "2", "3", "4" };
    [ObservableProperty] private int _pvbInterlayerIndex;
    [ObservableProperty] private int _gasIndex;        // CB@962,755 空气/氩/氪/氙（probe8e 零 diff）
    public static IReadOnlyList<string> GasOptions { get; } = new[] { "空气", "氩气", "氪气", "氙气" };

    /// <summary>玻璃结构 7 option（V31 OptionButton 组；默认单片玻璃=已逐位闭合链）。
    /// 成对 bool 包装（同 DoorWindow Vm 模式，避免 Converter）。</summary>
    [ObservableProperty] private int _glassStructureIndex;
    public bool StructSingle { get => GlassStructureIndex == 0; set { if (value) GlassStructureIndex = 0; } }
    public bool StructInsulated { get => GlassStructureIndex == 1; set { if (value) GlassStructureIndex = 1; } }
    public bool StructLaminated { get => GlassStructureIndex == 2; set { if (value) GlassStructureIndex = 2; } }
    public bool StructDoubleInsulated { get => GlassStructureIndex == 3; set { if (value) GlassStructureIndex = 3; } }
    public bool StructDoubleLamInsulated { get => GlassStructureIndex == 4; set { if (value) GlassStructureIndex = 4; } }
    public bool StructInsulInnerLam { get => GlassStructureIndex == 5; set { if (value) GlassStructureIndex = 5; } }
    public bool StructInsulOuterLam { get => GlassStructureIndex == 6; set { if (value) GlassStructureIndex = 6; } }

    /// <summary>VM 玻璃结构索引 → 引擎构型枚举（按 V31 OptionButton 语义显式映射，不依赖 int 顺序）。</summary>
    private static ThermalGlassEngine.GlassStructure MapStructure(int idx) => idx switch
    {
        0 => ThermalGlassEngine.GlassStructure.单片玻璃,     // StructSingle
        1 => ThermalGlassEngine.GlassStructure.中空玻璃,     // StructInsulated
        2 => ThermalGlassEngine.GlassStructure.夹层玻璃,     // StructLaminated
        3 => ThermalGlassEngine.GlassStructure.双中空玻璃,   // StructDoubleInsulated
        4 => ThermalGlassEngine.GlassStructure.双夹层中空,   // StructDoubleLamInsulated
        5 => ThermalGlassEngine.GlassStructure.中空加内夹层, // StructInsulInnerLam
        6 => ThermalGlassEngine.GlassStructure.中空加外夹层, // StructInsulOuterLam
        _ => ThermalGlassEngine.GlassStructure.单片玻璃,
    };

    partial void OnGlassStructureIndexChanged(int value)
    {
        foreach (var n in new[] { nameof(StructSingle), nameof(StructInsulated), nameof(StructLaminated),
                                  nameof(StructDoubleInsulated), nameof(StructDoubleLamInsulated),
                                  nameof(StructInsulInnerLam), nameof(StructInsulOuterLam) })
            OnPropertyChanged(n);
    }

    /// <summary>框K 方式（CB@607,479 计算所得/人工给出，装饰：主链按 FrameK 表）。</summary>
    public static IReadOnlyList<string> KFrameModeOptions { get; } = new[] { "计算所得", "人工给出" };
    [ObservableProperty] private int _kFrameModeIndex;
    [ObservableProperty] private string _alphaFrameText = "0.4";   // 框太阳吸收比（链⑥ α_f 输入）
    public double AlphaFrame => Parse(AlphaFrameText, 0.4);
    /// <summary>发射率（0.837 输入框；LOW-E 切换写 0.1 但主链不进——probe9 tbdiff 实证）。</summary>
    [ObservableProperty] private string _emissivityText = "0.837";
    /// <summary>多层玻璃系统层间参数 0.76 文本框（链② λ 输入）。</summary>
    [ObservableProperty] private string _lambdaGlassText = "0.76";
    public double LambdaGlass => Parse(LambdaGlassText, 0.76);

    // ══ 透射比参数帧 ══
    [ObservableProperty] private string _shgcText = "0.81";   // 玻璃太阳得热系数（输入框）
    [ObservableProperty] private string _tauText = "0.75";    // 玻璃可见光透射比（输入框）
    public double ShgcGlass => Parse(ShgcText, 0.81);
    public double TauGlass => Parse(TauText, 0.75);

    // ══ 尺寸参数帧（加权面积，输入框）══
    [ObservableProperty] private string _areaTotalText = "9";
    [ObservableProperty] private string _areaGlassText = "7.783";
    [ObservableProperty] private string _areaFrameText = "1.217";
    [ObservableProperty] private string _areaShadowText = "1.385";
    [ObservableProperty] private string _areaSolidText = "27.12";
    [ObservableProperty] private string _height3000Text = "3000";   // 建筑高度 m（装饰，零 diff）
    public double AreaTotal => Parse(AreaTotalText, 9);
    public double AreaGlass => Parse(AreaGlassText, 7.783);
    public double AreaFrame => Parse(AreaFrameText, 1.217);
    public double AreaFrameShadow => Parse(AreaShadowText, 1.385);
    public double AreaSpandrel => Parse(AreaSolidText, 27.12);

    // ══ 建筑参数帧 ══
    [ObservableProperty] private string _wwrText = "0.32";    // 窗墙比（输入框，判定分档 0.40）
    public double WindowWallRatio => Parse(WwrText, 0.32);
    public static IReadOnlyList<string> OrientationOptions { get; } =
        new[] { "东", "南", "西", "北", "采光顶" };
    [ObservableProperty] private int _orientationIndex;       // CB@611,361（装饰，零 diff）
    public static IReadOnlyList<string> WwrBandOptions { get; } = new[] { "300<A≤800", "A>800" };
    [ObservableProperty] private int _wwrBandIndex;           // CB@611,401（装饰）
    [ObservableProperty] private string _shapeCoefText = "0.15";  // 体形系数帧文本框（装饰组）
    /// <summary>「玻璃K值(人工)」与「围护结构热惰性指标」两个 2.5 文本框（装饰，主链 K_glass 计算得到）。</summary>
    [ObservableProperty] private string _kManualText = "2.5";

    /// <summary>结构类型 option（门窗系统默认 / 幕墙系统）——输出文案联动未取证，保留选择。</summary>
    [ObservableProperty] private bool _isDoorSystem = true;
    public bool IsCurtainSystem { get => !IsDoorSystem; set { IsDoorSystem = !value; if (value) Invalidate("结构类型已改"); } }

    // ══ 规范限值分档（probe8e cls_B / wwr0.5 case 逐位实录；居住/工业 b7_probe28 直读）══
    public double KLimb => Building switch
    {
        BuildingType.居住建筑 => 2.5,
        BuildingType.工业建筑 => 3.4,
        _ => ClassIndex == 0 ? 2.2 : 3,
    };
    public double ShgcSummerLimit => Building switch
    {
        BuildingType.居住建筑 => 0.4,
        BuildingType.工业建筑 => 0.6,
        _ => ClassIndex == 1 ? 0.45 : (WindowWallRatio < 0.40 ? 0.35 : 0.3),
    };
    /// <summary>室内计算温度：公共/居住 20、工业 16（b7_probe28 结露链直读）。</summary>
    public double TIndoor => Building == BuildingType.工业建筑 ? 16 : 20;

    // ══ 输出 ══
    public ObservableCollection<PreviewLine> Results { get; } = new();
    public bool HasResults => Results.Count > 0;
    public string PlaceholderText => HasResults ? "" :
        "先在「城市选择」页选省→市→点「确定」，再点「预演」（同 V31：未选城市会弹「请选择城市」）。";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _statusIsWarning;

    private void Invalidate(string reason)
    {
        OnPropertyChanged(nameof(PhiIndoor));
        OnPropertyChanged(nameof(KFrame));
        OnPropertyChanged(nameof(GlassThkMm));
        OnPropertyChanged(nameof(AlphaFrame));
        OnPropertyChanged(nameof(KLimb));
        OnPropertyChanged(nameof(ShgcSummerLimit));
        OnPropertyChanged(nameof(KByManual));
        OnPropertyChanged(nameof(IsCurtainSystem));
        if (Results.Count == 0) return;
        Results.Clear();
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(PlaceholderText));
        StatusIsWarning = true;
        StatusText = reason + "，请重新预演";
    }

    private static double Parse(string? s, double fallback)
        => double.TryParse((s ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v : fallback;

    private ThermalGlassEngine.Input BuildInput()
    {
        var city = ChosenCity ?? new ThermalCityTable.City("sh", "上海", "3A", "3", 0.5, -3.0);
        return new ThermalGlassEngine.Input
        {
            Structure = MapStructure(GlassStructureIndex),
            Building = Building,
            TIndoor = TIndoor,
            GlassThkMm = GlassThkMm,
            LambdaGlass = LambdaGlass,
            KFrame = KFrame,
            AreaGlass = AreaGlass,
            AreaFrame = AreaFrame,
            AreaSpandrel = AreaSpandrel,
            AreaTotal = AreaTotal,
            AreaFrameShadow = AreaFrameShadow,
            AlphaFrame = AlphaFrame,
            ShgcGlass = ShgcGlass,
            TauGlass = TauGlass,
            WindowWallRatio = WindowWallRatio,
            PhiIndoor = PhiIndoor,
            CityWinterRh = city.Rh,
            CityTeMin = city.TeMin,
            KLimb = KLimb,
            ShgcSummerLimit = ShgcSummerLimit,
        };
    }

    /// <summary>「预演」：城市未选 → V31 弹「请选择城市」0 输出；已选 → 四段逐位。</summary>
    [RelayCommand]
    private void Preview()
    {
        if (ChosenCity is null)
        {
            // V31 实采（b7_probe4）：未选城市点预演 → MsgBox「请选择城市」，输出框不写
            Results.Clear();
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(PlaceholderText));
            StatusIsWarning = true;
            StatusText = "请选择城市";
            return;
        }
        var it = BuildInput();
        var o = ThermalGlassEngine.Compute(it);
        var raw = ThermalGlassEngine.FormatOutput(it, o);
        Results.Clear();
        const string Sep = "********************************************";
        foreach (var seg in raw.Split(Sep, StringSplitOptions.None))
            Add(seg.Trim().Length == 0 ? "" : seg, ColorNormal);
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(PlaceholderText));
        StatusIsWarning = false;
        StatusText = $"预演完成：K玻璃 {o.KGlassWinter:0.###}、系统K {o.KSystemWinter:0.###}、" +
                     $"SHGC {o.ShgcSummer:0.###}/{o.ShgcWinter:0.###}、tsi {o.Tsi:0.###}℃、露点 {o.DewPoint:0.###}℃";
        LastRawOutput = raw;
    }

    /// <summary>最近一次预演的 V31 逐字全文（测试对拍用）。</summary>
    public string LastRawOutput { get; private set; } = "";

    private static readonly IBrush ColorNormal = Brushes.Black;
    private void Add(string text, IBrush brush) => Results.Add(new PreviewLine(text, brush));

    [RelayCommand] private void NextStep() => _wizard.GoBasic();
    [RelayCommand] private void Exit() => _wizard.CloseForm();
}
