using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia.Media;
using GlassManagement.Desktop.Services;

namespace GlassManagement.Desktop.ViewModels;

/// <summary>Step info for the wizard step bar</summary>
public class StepInfo : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public bool IsLast { get; set; }

    private string _circleText = "";
    public string CircleText { get => _circleText; set { _circleText = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CircleText))); } }

    private IBrush _circleBg = Brushes.Transparent;
    public IBrush CircleBg { get => _circleBg; set { _circleBg = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CircleBg))); } }

    private IBrush _circleBorder = Brushes.Gray;
    public IBrush CircleBorder { get => _circleBorder; set { _circleBorder = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CircleBorder))); } }

    private IBrush _circleFg = Brushes.Gray;
    public IBrush CircleFg { get => _circleFg; set { _circleFg = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CircleFg))); } }

    private IBrush _labelFg = Brushes.Gray;
    public IBrush LabelFg { get => _labelFg; set { _labelFg = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LabelFg))); } }

    private string _labelWeight = "Normal";
    public string LabelWeight { get => _labelWeight; set { _labelWeight = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LabelWeight))); } }
}

public class MainViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private readonly ApiClient _api = new();

    // ==================== Wizard Step State ====================
    private static readonly string[] StepNames = { "基本参数", "玻璃配置", "立柱计算", "横梁计算", "连接件", "预埋件", "胶/紧固", "汇总", "计算书" };
    public ObservableCollection<StepInfo> Steps { get; } = new();

    private int _currentStep;
    public int CurrentStep
    {
        get => _currentStep;
        set
        {
            if (value < 0 || value > 8) return;
            _currentStep = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanGoPrev));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(IsLastStep));
            UpdateStepVisuals();
        }
    }
    public bool CanGoPrev => CurrentStep > 0;
    public bool CanGoNext => CurrentStep < 8;
    public bool IsLastStep => CurrentStep == 8;

    public void NextStep() { if (CanGoNext) CurrentStep++; }
    public void PrevStep() { if (CanGoPrev) CurrentStep--; }

    // ==================== Theme ====================
    private int _themeIndex;
    public int ThemeIndex
    {
        get => _themeIndex;
        set
        {
            _themeIndex = value;
            OnPropertyChanged();
            ApplyCurrentTheme();
        }
    }

    private void ApplyCurrentTheme()
    {
        var theme = ThemeIndex switch { 1 => "apple", 2 => "xkool", _ => "classic" };
        if (Avalonia.Application.Current is App app &&
            app.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow != null)
        {
            app.ApplyTheme(theme, desktop.MainWindow);
            UpdateStepVisuals(); // refresh step colors
        }
    }

    private void UpdateStepVisuals()
    {
        var doneBrush = new SolidColorBrush(Color.Parse("#10b981"));
        var accentBrush = new SolidColorBrush(Color.Parse(ThemeIndex switch { 1 => "#007aff", 2 => "#8c00ff", _ => "#2f27ce" }));
        var futureBorderBrush = new SolidColorBrush(Color.Parse("#d1d5db"));
        var futureTextBrush = new SolidColorBrush(Color.Parse("#9ca3af"));
        var whiteBrush = Brushes.White;
        var textPrimary = new SolidColorBrush(Color.Parse(ThemeIndex == 1 ? "#e2e8f0" : "#1a1a2e"));

        foreach (var s in Steps)
        {
            if (s.Index < CurrentStep)
            {
                // Done
                s.CircleText = "✓";
                s.CircleBg = doneBrush;
                s.CircleBorder = doneBrush;
                s.CircleFg = whiteBrush;
                s.LabelFg = textPrimary;
                s.LabelWeight = "Normal";
            }
            else if (s.Index == CurrentStep)
            {
                // Current
                s.CircleText = (s.Index + 1).ToString();
                s.CircleBg = accentBrush;
                s.CircleBorder = accentBrush;
                s.CircleFg = whiteBrush;
                s.LabelFg = accentBrush;
                s.LabelWeight = "Bold";
            }
            else
            {
                // Future
                s.CircleText = (s.Index + 1).ToString();
                s.CircleBg = Brushes.Transparent;
                s.CircleBorder = futureBorderBrush;
                s.CircleFg = futureTextBrush;
                s.LabelFg = futureTextBrush;
                s.LabelWeight = "Normal";
            }
        }
    }

    public MainViewModel()
    {
        for (int i = 0; i < StepNames.Length; i++)
            Steps.Add(new StepInfo { Index = i, Name = StepNames[i], IsLast = i == StepNames.Length - 1 });
        UpdateStepVisuals();
    }

    // ==================== Connection ====================
    private string _serverUrl = "http://localhost:5188";
    public string ServerUrl
    {
        get => _serverUrl;
        set { _serverUrl = value; OnPropertyChanged(); _api.BaseUrl = value; }
    }

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        set { _isConnected = value; OnPropertyChanged(); OnPropertyChanged(nameof(ConnectionStatus)); }
    }
    public string ConnectionStatus => IsConnected ? "✅ 已连接" : "❌ 未连接";

    private string _statusMessage = "就绪";
    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    private int _selectedNavIndex;
    public int SelectedNavIndex
    {
        get => _selectedNavIndex;
        set { _selectedNavIndex = value; OnPropertyChanged(); _ = LoadCurrentPageData(); }
    }

    public ObservableCollection<DataItem> CurrentItems { get; } = new();

    // ==================== Step1 基本参数 (对齐Blazor) ====================

    // 幕墙类型
    private int _curtainWallTypeIndex = 0;
    public int CurtainWallTypeIndex { get => _curtainWallTypeIndex; set { _curtainWallTypeIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(Step1Title)); } }
    public List<string> CurtainWallTypes { get; } = new() { "全隐框玻璃", "半隐框玻璃", "明框玻璃", "金属板", "天然石材", "人造板材" };
    public string Step1Title => $"{CurtainWallTypes[CurtainWallTypeIndex]}幕墙计算 - 基本参数输入";

    // 重现期
    private bool _returnPeriod50 = true;
    public bool ReturnPeriod50 { get => _returnPeriod50; set { _returnPeriod50 = value; OnPropertyChanged(); } }
    private bool _returnPeriod100 = false;
    public bool ReturnPeriod100 { get => _returnPeriod100; set { _returnPeriod100 = value; OnPropertyChanged(); } }

    private bool _snowSensitive = false;
    public bool SnowSensitive { get => _snowSensitive; set { _snowSensitive = value; OnPropertyChanged(); } }

    private double _calcElevation = 50;
    public double CalcElevation { get => _calcElevation; set { _calcElevation = value; OnPropertyChanged(); } }

    // 地面类型
    private bool _terrainA = false;
    public bool TerrainA { get => _terrainA; set { if (value) { _terrainA = true; _terrainB = _terrainC = _terrainD = false; NotifyTerrains(); } } }
    private bool _terrainB = true;
    public bool TerrainB { get => _terrainB; set { if (value) { _terrainB = true; _terrainA = _terrainC = _terrainD = false; NotifyTerrains(); } } }
    private bool _terrainC = false;
    public bool TerrainC { get => _terrainC; set { if (value) { _terrainC = true; _terrainA = _terrainB = _terrainD = false; NotifyTerrains(); } } }
    private bool _terrainD = false;
    public bool TerrainD { get => _terrainD; set { if (value) { _terrainD = true; _terrainA = _terrainB = _terrainC = false; NotifyTerrains(); } } }
    private void NotifyTerrains() { OnPropertyChanged(nameof(TerrainA)); OnPropertyChanged(nameof(TerrainB)); OnPropertyChanged(nameof(TerrainC)); OnPropertyChanged(nameof(TerrainD)); }

    // 外部局部体型系数
    private bool _extWallCenter = true;
    public bool ExtWallCenter { get => _extWallCenter; set { if (value) { _extWallCenter = true; _extWallCorner = _extCustom = false; NotifyExt(); } } }
    private bool _extWallCorner = false;
    public bool ExtWallCorner { get => _extWallCorner; set { if (value) { _extWallCorner = true; _extWallCenter = _extCustom = false; NotifyExt(); } } }
    private bool _extCustom = false;
    public bool ExtCustom { get => _extCustom; set { if (value) { _extCustom = true; _extWallCenter = _extWallCorner = false; NotifyExt(); } } }
    private void NotifyExt() { OnPropertyChanged(nameof(ExtWallCenter)); OnPropertyChanged(nameof(ExtWallCorner)); OnPropertyChanged(nameof(ExtCustom)); }
    private double _externalShapeValue = 1.0;
    public double ExternalShapeValue { get => _externalShapeValue; set { _externalShapeValue = value; OnPropertyChanged(); } }

    // 内部局部体型系数
    private bool _intSealed = true;
    public bool IntSealed { get => _intSealed; set { if (value) { _intSealed = true; _intOpen002 = _intOpen010 = _intOpen030 = _intCustom = false; NotifyInt(); } } }
    private bool _intOpen002 = false;
    public bool IntOpen002 { get => _intOpen002; set { if (value) { _intOpen002 = true; _intSealed = _intOpen010 = _intOpen030 = _intCustom = false; NotifyInt(); } } }
    private bool _intOpen010 = false;
    public bool IntOpen010 { get => _intOpen010; set { if (value) { _intOpen010 = true; _intSealed = _intOpen002 = _intOpen030 = _intCustom = false; NotifyInt(); } } }
    private bool _intOpen030 = false;
    public bool IntOpen030 { get => _intOpen030; set { if (value) { _intOpen030 = true; _intSealed = _intOpen002 = _intOpen010 = _intCustom = false; NotifyInt(); } } }
    private bool _intCustom = false;
    public bool IntCustom { get => _intCustom; set { if (value) { _intCustom = true; _intSealed = _intOpen002 = _intOpen010 = _intOpen030 = false; NotifyInt(); } } }
    private void NotifyInt() { OnPropertyChanged(nameof(IntSealed)); OnPropertyChanged(nameof(IntOpen002)); OnPropertyChanged(nameof(IntOpen010)); OnPropertyChanged(nameof(IntOpen030)); OnPropertyChanged(nameof(IntCustom)); }
    private double _internalShapeValue = 0.2;
    public double InternalShapeValue { get => _internalShapeValue; set { _internalShapeValue = value; OnPropertyChanged(); } }

    // 风荷载标准值取值
    private bool _windModeCodeMin = true;
    public bool WindModeCodeMin { get => _windModeCodeMin; set { if (value) { _windModeCodeMin = true; _windModeCustom = _windModeActual = false; NotifyWind(); } } }
    private bool _windModeCustom = false;
    public bool WindModeCustom { get => _windModeCustom; set { if (value) { _windModeCustom = true; _windModeCodeMin = _windModeActual = false; NotifyWind(); } } }
    private bool _windModeActual = false;
    public bool WindModeActual { get => _windModeActual; set { if (value) { _windModeActual = true; _windModeCodeMin = _windModeCustom = false; NotifyWind(); } } }
    private void NotifyWind() { OnPropertyChanged(nameof(WindModeCodeMin)); OnPropertyChanged(nameof(WindModeCustom)); OnPropertyChanged(nameof(WindModeActual)); }
    private double _windLoadCustomValue = 1.0;
    public double WindLoadCustomValue { get => _windLoadCustomValue; set { _windLoadCustomValue = value; OnPropertyChanged(); } }

    // 幕墙结构
    private bool _cwStructComponent = true;
    public bool CwStructComponent { get => _cwStructComponent; set { if (value) { _cwStructComponent = true; _cwStructUnit = false; OnPropertyChanged(); OnPropertyChanged(nameof(CwStructUnit)); } } }
    private bool _cwStructUnit = false;
    public bool CwStructUnit { get => _cwStructUnit; set { if (value) { _cwStructUnit = true; _cwStructComponent = false; OnPropertyChanged(); OnPropertyChanged(nameof(CwStructComponent)); } } }

    // 幕墙横框结构
    private bool _transomNone = true;
    public bool TransomNone { get => _transomNone; set { if (value) { _transomNone = true; _transomHas = false; OnPropertyChanged(); OnPropertyChanged(nameof(TransomHas)); } } }
    private bool _transomHas = false;
    public bool TransomHas { get => _transomHas; set { if (value) { _transomHas = true; _transomNone = false; OnPropertyChanged(); OnPropertyChanged(nameof(TransomNone)); } } }

    // 玻璃配置
    private int _glassConfigIndex = 0;
    public int GlassConfigIndex { get => _glassConfigIndex; set { _glassConfigIndex = value; OnPropertyChanged(); } }
    public List<string> GlassConfigOptions { get; } = new() { "单片玻璃", "中空+夹层玻璃", "中空玻璃", "双中空玻璃", "夹层玻璃", "双夹层-中空玻璃" };

    // 龙骨间距
    private double _leftPanelWidth = 600;
    public double LeftPanelWidth { get => _leftPanelWidth; set { _leftPanelWidth = value; OnPropertyChanged(); } }
    private double _rightPanelWidth = 600;
    public double RightPanelWidth { get => _rightPanelWidth; set { _rightPanelWidth = value; OnPropertyChanged(); } }
    private double _upperPanelHeight = 1500;
    public double UpperPanelHeight { get => _upperPanelHeight; set { _upperPanelHeight = value; OnPropertyChanged(); } }
    private double _lowerPanelHeight = 1500;
    public double LowerPanelHeight { get => _lowerPanelHeight; set { _lowerPanelHeight = value; OnPropertyChanged(); } }
    private double _upperMidTransom = 750;
    public double UpperMidTransom { get => _upperMidTransom; set { _upperMidTransom = value; OnPropertyChanged(); } }
    private double _lowerMidTransom = 750;
    public double LowerMidTransom { get => _lowerMidTransom; set { _lowerMidTransom = value; OnPropertyChanged(); } }

    // 修正参数
    private double _terrainCorrectionFactor = 1.0;
    public double TerrainCorrectionFactor { get => _terrainCorrectionFactor; set { _terrainCorrectionFactor = value; OnPropertyChanged(); } }
    private double _windDirectionFactor = 1.0;
    public double WindDirectionFactor { get => _windDirectionFactor; set { _windDirectionFactor = value; OnPropertyChanged(); } }

    // 抗震设防分类
    private int _seismicCategoryIndex = 0;
    public int SeismicCategoryIndex { get => _seismicCategoryIndex; set { _seismicCategoryIndex = value; OnPropertyChanged(); NotifySeismicCats(); } }
    public List<string> SeismicCategories { get; } = new() { "标准设防类", "重点设防类", "特殊设防类", "适度设防类" };
    public bool SeismicCat0 { get => _seismicCategoryIndex == 0; set { if (value) SeismicCategoryIndex = 0; } }
    public bool SeismicCat1 { get => _seismicCategoryIndex == 1; set { if (value) SeismicCategoryIndex = 1; } }
    public bool SeismicCat2 { get => _seismicCategoryIndex == 2; set { if (value) SeismicCategoryIndex = 2; } }
    public bool SeismicCat3 { get => _seismicCategoryIndex == 3; set { if (value) SeismicCategoryIndex = 3; } }
    private void NotifySeismicCats() { OnPropertyChanged(nameof(SeismicCat0)); OnPropertyChanged(nameof(SeismicCat1)); OnPropertyChanged(nameof(SeismicCat2)); OnPropertyChanged(nameof(SeismicCat3)); }

    // 场地类型
    private int _siteCategoryIndex = 1;
    public int SiteCategoryIndex { get => _siteCategoryIndex; set { _siteCategoryIndex = value; OnPropertyChanged(); } }
    public List<string> SiteCategories { get; } = new() { "I类场地", "II类场地", "III类场地", "IV类场地" };

    // 底部
    private bool _autoGravity = false;
    public bool AutoGravity { get => _autoGravity; set { _autoGravity = value; OnPropertyChanged(); } }
    private bool _tempDiff80 = false;
    public bool TempDiff80 { get => _tempDiff80; set { _tempDiff80 = value; OnPropertyChanged(); } }

    // 地区参数
    private string _regionSearch = "";
    public string RegionSearch { get => _regionSearch; set { _regionSearch = value; OnPropertyChanged(); } }
    private int _standardCodeIndex = 0;
    public int StandardCodeIndex { get => _standardCodeIndex; set { _standardCodeIndex = value; OnPropertyChanged(); } }
    public List<string> StandardCodes { get; } = new() { "全国荷载规范", "建筑荷载规范", "广东省荷载规范(小于45米的低层建筑)", "广东省荷载规范(大于45米高层建筑)" };

    // 地区数据表
    public ObservableCollection<RegionRowVM> RegionRows { get; } = new();
    private RegionRowVM? _selectedRegionRow;
    public RegionRowVM? SelectedRegionRow { get => _selectedRegionRow; set { _selectedRegionRow = value; OnPropertyChanged(); } }

    // ==================== 旧版项目参数（保留兼容） ====================
    private string _ppRegionKeyword = "";
    public string PpRegionKeyword { get => _ppRegionKeyword; set { _ppRegionKeyword = value; OnPropertyChanged(); } }

    private ObservableCollection<string> _ppRegionList = new();
    public ObservableCollection<string> PpRegionList { get => _ppRegionList; set { _ppRegionList = value; OnPropertyChanged(); } }

    private int _ppRegionIndex = -1;
    public int PpRegionIndex { get => _ppRegionIndex; set { _ppRegionIndex = value; OnPropertyChanged(); } }

    private string _ppSelectedRegion = "";
    public string PpSelectedRegion { get => _ppSelectedRegion; set { _ppSelectedRegion = value; OnPropertyChanged(); } }

    private double _ppBasicWindPressure = 0.45;
    public double PpBasicWindPressure { get => _ppBasicWindPressure; set { _ppBasicWindPressure = value; OnPropertyChanged(); } }

    private string _ppSeismicIntensity = "7度";
    public string PpSeismicIntensity { get => _ppSeismicIntensity; set { _ppSeismicIntensity = value; OnPropertyChanged(); } }

    private double _ppAlphaMax = 0.08;
    public double PpAlphaMax { get => _ppAlphaMax; set { _ppAlphaMax = value; OnPropertyChanged(); } }

    private double _ppSnowPressure = 0.3;
    public double PpSnowPressure { get => _ppSnowPressure; set { _ppSnowPressure = value; OnPropertyChanged(); } }

    private double _ppTempDiff = 50;
    public double PpTempDiff { get => _ppTempDiff; set { _ppTempDiff = value; OnPropertyChanged(); } }

    private int _ppTerrainIndex = 1;
    public int PpTerrainIndex { get => _ppTerrainIndex; set { _ppTerrainIndex = value; OnPropertyChanged(); } }

    private double _ppHeight = 50;
    public double PpHeight { get => _ppHeight; set { _ppHeight = value; OnPropertyChanged(); } }

    private int _ppBuildingTypeIndex = 0;
    public int PpBuildingTypeIndex { get => _ppBuildingTypeIndex; set { _ppBuildingTypeIndex = value; OnPropertyChanged(); } }

    private double _ppTributaryArea = 6.0;
    public double PpTributaryArea { get => _ppTributaryArea; set { _ppTributaryArea = value; OnPropertyChanged(); } }

    private int _ppReturnPeriodIndex = 0;
    public int PpReturnPeriodIndex { get => _ppReturnPeriodIndex; set { _ppReturnPeriodIndex = value; OnPropertyChanged(); } }

    private string _ppWindResult = "";
    public string PpWindResult { get => _ppWindResult; set { _ppWindResult = value; OnPropertyChanged(); } }

    private string _ppSeismicResult = "";
    public string PpSeismicResult { get => _ppSeismicResult; set { _ppSeismicResult = value; OnPropertyChanged(); } }

    private List<Services.RegionInfo> _ppRegionDataList = new();
    private List<Services.RegionInfo> _ppAllRegions = new();

    // ==================== SVG简图 (浏览器打开) ====================

    // ==================== 玻璃计算参数 (新版统一配置) ====================
    private double _glassShortSide = 1100;
    public double GlassShortSide { get => _glassShortSide; set { _glassShortSide = value; OnPropertyChanged(); } }
    private double _glassLongSide = 1380;
    public double GlassLongSide { get => _glassLongSide; set { _glassLongSide = value; OnPropertyChanged(); } }
    private double _windLoad = 1.5;
    public double WindLoad { get => _windLoad; set { _windLoad = value; OnPropertyChanged(); } }
    private double _altitudeDifference = 0;
    public double AltitudeDifference { get => _altitudeDifference; set { _altitudeDifference = value; OnPropertyChanged(); } }
    private double _temperatureDifference = 0;
    public double TemperatureDifference { get => _temperatureDifference; set { _temperatureDifference = value; OnPropertyChanged(); } }

    // PanelCategory: 0=非中空, 1=单中空, 2=双中空
    private int _panelCategoryIndex = 0;
    public int PanelCategoryIndex
    {
        get => _panelCategoryIndex;
        set { _panelCategoryIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNonInsulating)); OnPropertyChanged(nameof(IsSingleInsulating)); OnPropertyChanged(nameof(IsDoubleInsulating)); UpdateGlassDisplayName(); }
    }
    public bool IsNonInsulating => PanelCategoryIndex == 0;
    public bool IsSingleInsulating => PanelCategoryIndex == 1;
    public bool IsDoubleInsulating => PanelCategoryIndex == 2;

    // 外片配置
    private bool _outerIsLaminated = false;
    public bool OuterIsLaminated { get => _outerIsLaminated; set { _outerIsLaminated = value; OnPropertyChanged(); UpdateGlassDisplayName(); } }
    private int _outerThicknessIndex = 1; // 默认6mm
    public int OuterThicknessIndex { get => _outerThicknessIndex; set { _outerThicknessIndex = value; OnPropertyChanged(); UpdateGlassDisplayName(); } }
    private int _outerTemperedIndex = 2; // 默认全钢化
    public int OuterTemperedIndex { get => _outerTemperedIndex; set { _outerTemperedIndex = value; OnPropertyChanged(); UpdateGlassDisplayName(); } }
    // 外片夹胶
    private int _outerLamSheetCount = 2;
    public int OuterLamSheetCount { get => _outerLamSheetCount; set { _outerLamSheetCount = value; OnPropertyChanged(); RebuildOuterLamSheets(); UpdateGlassDisplayName(); } }
    public ObservableCollection<LamSheetVM> OuterLamSheets { get; } = new();
    public ObservableCollection<LamLayerVM> OuterLamLayers { get; } = new();

    // 内片配置
    private bool _innerIsLaminated = false;
    public bool InnerIsLaminated { get => _innerIsLaminated; set { _innerIsLaminated = value; OnPropertyChanged(); UpdateGlassDisplayName(); } }
    private int _innerThicknessIndex = 1;
    public int InnerThicknessIndex { get => _innerThicknessIndex; set { _innerThicknessIndex = value; OnPropertyChanged(); UpdateGlassDisplayName(); } }
    private int _innerTemperedIndex = 2;
    public int InnerTemperedIndex { get => _innerTemperedIndex; set { _innerTemperedIndex = value; OnPropertyChanged(); UpdateGlassDisplayName(); } }
    private int _innerLamSheetCount = 2;
    public int InnerLamSheetCount { get => _innerLamSheetCount; set { _innerLamSheetCount = value; OnPropertyChanged(); RebuildInnerLamSheets(); UpdateGlassDisplayName(); } }
    public ObservableCollection<LamSheetVM> InnerLamSheets { get; } = new();
    public ObservableCollection<LamLayerVM> InnerLamLayers { get; } = new();

    // 中间片配置 (双中空)
    private bool _middleIsLaminated = false;
    public bool MiddleIsLaminated { get => _middleIsLaminated; set { _middleIsLaminated = value; OnPropertyChanged(); UpdateGlassDisplayName(); } }
    private int _middleThicknessIndex = 1;
    public int MiddleThicknessIndex { get => _middleThicknessIndex; set { _middleThicknessIndex = value; OnPropertyChanged(); UpdateGlassDisplayName(); } }
    private int _middleTemperedIndex = 2;
    public int MiddleTemperedIndex { get => _middleTemperedIndex; set { _middleTemperedIndex = value; OnPropertyChanged(); UpdateGlassDisplayName(); } }
    private int _middleLamSheetCount = 2;
    public int MiddleLamSheetCount { get => _middleLamSheetCount; set { _middleLamSheetCount = value; OnPropertyChanged(); RebuildMiddleLamSheets(); UpdateGlassDisplayName(); } }
    public ObservableCollection<LamSheetVM> MiddleLamSheets { get; } = new();
    public ObservableCollection<LamLayerVM> MiddleLamLayers { get; } = new();

    // 间隔层1
    private int _gap1ThicknessIndex = 1; // 默认9mm
    public int Gap1ThicknessIndex { get => _gap1ThicknessIndex; set { _gap1ThicknessIndex = value; OnPropertyChanged(); UpdateGlassDisplayName(); } }
    private int _gap1GasIndex = 0; // Air
    public int Gap1GasIndex { get => _gap1GasIndex; set { _gap1GasIndex = value; OnPropertyChanged(); UpdateGlassDisplayName(); } }

    // 间隔层2
    private int _gap2ThicknessIndex = 1;
    public int Gap2ThicknessIndex { get => _gap2ThicknessIndex; set { _gap2ThicknessIndex = value; OnPropertyChanged(); UpdateGlassDisplayName(); } }
    private int _gap2GasIndex = 0;
    public int Gap2GasIndex { get => _gap2GasIndex; set { _gap2GasIndex = value; OnPropertyChanged(); UpdateGlassDisplayName(); } }

    // 选项列表 (供UI绑定)
    public List<int> ThicknessOptions { get; } = new() { 5, 6, 8, 10, 12, 15, 19, 20 };
    public List<string> TemperedOptions { get; } = new() { "浮法", "半钢化", "全钢化" };
    public List<int> InsulatingThicknessOptions { get; } = new() { 6, 9, 12, 15, 16 };
    public List<string> GasOptions { get; } = new() { "Air", "Ar" };
    public List<string> FilmTypeOptions { get; } = new() { "PVB", "SGP" };
    public List<double> FilmThicknessOptionsList { get; } = new() { 0.76, 1.14, 1.52, 2.28 };
    public List<int> SheetCountOptions { get; } = new() { 2, 3, 4, 5 };

    // DisplayName
    private string _glassDisplayName = "";
    public string GlassDisplayName { get => _glassDisplayName; set { _glassDisplayName = value; OnPropertyChanged(); } }

    private string _calcResult = "";
    public string CalcResult { get => _calcResult; set { _calcResult = value; OnPropertyChanged(); } }

    // 兼容旧属性 (简图/云图用)
    public double GlassThickness => IsNonInsulating && !OuterIsLaminated ? ThicknessOptions[OuterThicknessIndex < ThicknessOptions.Count ? OuterThicknessIndex : 0] : 6;

    private void UpdateGlassDisplayName()
    {
        try { GlassDisplayName = BuildGlassConfigDto().DisplayName; }
        catch { GlassDisplayName = ""; }
    }

    private int GetSheetCount(int idx) => SheetCountOptions[idx < SheetCountOptions.Count ? idx : 0];
    private void RebuildOuterLamSheets() => RebuildLamSheets(OuterLamSheets, OuterLamLayers, GetSheetCount(OuterLamSheetCount));
    private void RebuildInnerLamSheets() => RebuildLamSheets(InnerLamSheets, InnerLamLayers, GetSheetCount(InnerLamSheetCount));
    private void RebuildMiddleLamSheets() => RebuildLamSheets(MiddleLamSheets, MiddleLamLayers, GetSheetCount(MiddleLamSheetCount));

    private void RebuildLamSheets(ObservableCollection<LamSheetVM> sheets, ObservableCollection<LamLayerVM> layers, int count)
    {
        sheets.Clear();
        layers.Clear();
        for (int i = 0; i < count; i++)
        {
            sheets.Add(new LamSheetVM { ThicknessIndex = 1, TemperedIndex = 2, Order = i + 1 });
            if (i < count - 1)
                layers.Add(new LamLayerVM { FilmTypeIndex = 0, FilmThicknessIndex = 2, Order = i + 1 });
        }
    }

    // ==================== 铝合金计算参数 ====================
    private double _aluSpan = 1500;
    public double AluSpan { get => _aluSpan; set { _aluSpan = value; OnPropertyChanged(); } }
    private double _aluSpacing = 1200;
    public double AluSpacing { get => _aluSpacing; set { _aluSpacing = value; OnPropertyChanged(); } }
    private string _aluProfileSpec = "100×50×3";
    public string AluProfileSpec { get => _aluProfileSpec; set { _aluProfileSpec = value; OnPropertyChanged(); } }
    private double _aluWindLoad = 1.5;
    public double AluWindLoad { get => _aluWindLoad; set { _aluWindLoad = value; OnPropertyChanged(); } }
    private string _aluCalcResult = "";
    public string AluCalcResult { get => _aluCalcResult; set { _aluCalcResult = value; OnPropertyChanged(); } }

    // ==================== 石材计算参数 ====================
    private double _stoneShortSide = 600;
    public double StoneShortSide { get => _stoneShortSide; set { _stoneShortSide = value; OnPropertyChanged(); } }
    private double _stoneLongSide = 900;
    public double StoneLongSide { get => _stoneLongSide; set { _stoneLongSide = value; OnPropertyChanged(); } }
    private double _stoneThickness = 30;
    public double StoneThickness { get => _stoneThickness; set { _stoneThickness = value; OnPropertyChanged(); } }
    private double _stoneWindLoad = 1.5;
    public double StoneWindLoad { get => _stoneWindLoad; set { _stoneWindLoad = value; OnPropertyChanged(); } }
    private string _stoneCalcResult = "";
    public string StoneCalcResult { get => _stoneCalcResult; set { _stoneCalcResult = value; OnPropertyChanged(); } }

    // ==================== 铝板幕墙参数 ====================
    private double _aluPanelShortSide = 800;
    public double AluPanelShortSide { get => _aluPanelShortSide; set { _aluPanelShortSide = value; OnPropertyChanged(); } }
    private double _aluPanelLongSide = 1200;
    public double AluPanelLongSide { get => _aluPanelLongSide; set { _aluPanelLongSide = value; OnPropertyChanged(); } }
    private double _aluPanelThickness = 3;
    public double AluPanelThickness { get => _aluPanelThickness; set { _aluPanelThickness = value; OnPropertyChanged(); } }
    private double _aluPanelWindLoad = 1.0;
    public double AluPanelWindLoad { get => _aluPanelWindLoad; set { _aluPanelWindLoad = value; OnPropertyChanged(); } }
    private double _aluPanelDesignLoad = 0.0018;
    public double AluPanelDesignLoad { get => _aluPanelDesignLoad; set { _aluPanelDesignLoad = value; OnPropertyChanged(); } }
    private double _aluPanelStiffenerSpacing = 300;
    public double AluPanelStiffenerSpacing { get => _aluPanelStiffenerSpacing; set { _aluPanelStiffenerSpacing = value; OnPropertyChanged(); } }
    private string _aluPanelResult = "";
    public string AluPanelResult { get => _aluPanelResult; set { _aluPanelResult = value; OnPropertyChanged(); } }

    // ==================== GRC板参数 ====================
    private double _grcShortSide = 1000;
    public double GrcShortSide { get => _grcShortSide; set { _grcShortSide = value; OnPropertyChanged(); } }
    private double _grcLongSide = 1500;
    public double GrcLongSide { get => _grcLongSide; set { _grcLongSide = value; OnPropertyChanged(); } }
    private double _grcSupportShort = 900;
    public double GrcSupportShort { get => _grcSupportShort; set { _grcSupportShort = value; OnPropertyChanged(); } }
    private double _grcSupportLong = 1400;
    public double GrcSupportLong { get => _grcSupportLong; set { _grcSupportLong = value; OnPropertyChanged(); } }
    private double _grcEffThickness = 12;
    public double GrcEffThickness { get => _grcEffThickness; set { _grcEffThickness = value; OnPropertyChanged(); } }
    private double _grcTotalThickness = 20;
    public double GrcTotalThickness { get => _grcTotalThickness; set { _grcTotalThickness = value; OnPropertyChanged(); } }
    private double _grcWindLoad = 1.0;
    public double GrcWindLoad { get => _grcWindLoad; set { _grcWindLoad = value; OnPropertyChanged(); } }
    private double _grcDesignLoad = 0.0018;
    public double GrcDesignLoad { get => _grcDesignLoad; set { _grcDesignLoad = value; OnPropertyChanged(); } }
    private string _grcResult = "";
    public string GrcResult { get => _grcResult; set { _grcResult = value; OnPropertyChanged(); } }

    // ==================== UHPC板参数 ====================
    private double _uhpcShortSide = 1000;
    public double UhpcShortSide { get => _uhpcShortSide; set { _uhpcShortSide = value; OnPropertyChanged(); } }
    private double _uhpcLongSide = 1500;
    public double UhpcLongSide { get => _uhpcLongSide; set { _uhpcLongSide = value; OnPropertyChanged(); } }
    private double _uhpcSpan = 1000;
    public double UhpcSpan { get => _uhpcSpan; set { _uhpcSpan = value; OnPropertyChanged(); } }
    private double _uhpcEffThickness = 15;
    public double UhpcEffThickness { get => _uhpcEffThickness; set { _uhpcEffThickness = value; OnPropertyChanged(); } }
    private double _uhpcWindLoad = 1.0;
    public double UhpcWindLoad { get => _uhpcWindLoad; set { _uhpcWindLoad = value; OnPropertyChanged(); } }
    private double _uhpcDesignLoad = 0.0018;
    public double UhpcDesignLoad { get => _uhpcDesignLoad; set { _uhpcDesignLoad = value; OnPropertyChanged(); } }
    private string _uhpcResult = "";
    public string UhpcResult { get => _uhpcResult; set { _uhpcResult = value; OnPropertyChanged(); } }

    // ==================== 点式钢管参数 ====================
    private double _psSpan = 3000;
    public double PsSpan { get => _psSpan; set { _psSpan = value; OnPropertyChanged(); } }
    private double _psGridWidth = 1200;
    public double PsGridWidth { get => _psGridWidth; set { _psGridWidth = value; OnPropertyChanged(); } }
    private double _psWindLoad = 1.0;
    public double PsWindLoad { get => _psWindLoad; set { _psWindLoad = value; OnPropertyChanged(); } }
    private double _psSectionArea = 1131;
    public double PsSectionArea { get => _psSectionArea; set { _psSectionArea = value; OnPropertyChanged(); } }
    private double _psMomentOfInertia = 128000;
    public double PsMomentOfInertia { get => _psMomentOfInertia; set { _psMomentOfInertia = value; OnPropertyChanged(); } }
    private double _psSectionModulus = 5120;
    public double PsSectionModulus { get => _psSectionModulus; set { _psSectionModulus = value; OnPropertyChanged(); } }
    private string _psResult = "";
    public string PsResult { get => _psResult; set { _psResult = value; OnPropertyChanged(); } }

    // ==================== 风荷载参数 ====================
    private double _wlHeight = 50;
    public double WlHeight { get => _wlHeight; set { _wlHeight = value; OnPropertyChanged(); } }
    private double _wlBasicPressure = 0.00045;
    public double WlBasicPressure { get => _wlBasicPressure; set { _wlBasicPressure = value; OnPropertyChanged(); } }
    private int _wlTerrainIndex = 2;
    public int WlTerrainIndex { get => _wlTerrainIndex; set { _wlTerrainIndex = value; OnPropertyChanged(); } }
    private double _wlShapeCoeff = 1.0;
    public double WlShapeCoeff { get => _wlShapeCoeff; set { _wlShapeCoeff = value; OnPropertyChanged(); } }
    private string _wlResult = "";
    public string WlResult { get => _wlResult; set { _wlResult = value; OnPropertyChanged(); } }

    // ==================== 地震参数 ====================
    private double _seAlphaMax = 0.16;
    public double SeAlphaMax { get => _seAlphaMax; set { _seAlphaMax = value; OnPropertyChanged(); } }
    private double _seBetaE = 5.0;
    public double SeBetaE { get => _seBetaE; set { _seBetaE = value; OnPropertyChanged(); } }
    private double _seGravityLoad = 5000;
    public double SeGravityLoad { get => _seGravityLoad; set { _seGravityLoad = value; OnPropertyChanged(); } }
    private double _seArea = 2000000;
    public double SeArea { get => _seArea; set { _seArea = value; OnPropertyChanged(); } }
    private string _seResult = "";
    public string SeResult { get => _seResult; set { _seResult = value; OnPropertyChanged(); } }

    // ==================== 连接件参数 ====================
    private double _connMullionSpacing = 1200;
    public double ConnMullionSpacing { get => _connMullionSpacing; set { _connMullionSpacing = value; OnPropertyChanged(); } }
    private double _connTransomWidth = 1200;
    public double ConnTransomWidth { get => _connTransomWidth; set { _connTransomWidth = value; OnPropertyChanged(); } }
    private double _connTransomHeight = 1500;
    public double ConnTransomHeight { get => _connTransomHeight; set { _connTransomHeight = value; OnPropertyChanged(); } }
    private double _connMullionSpan = 3600;
    public double ConnMullionSpan { get => _connMullionSpan; set { _connMullionSpan = value; OnPropertyChanged(); } }
    private double _connShortSpan = 1500;
    public double ConnShortSpan { get => _connShortSpan; set { _connShortSpan = value; OnPropertyChanged(); } }
    private double _connLongSpan = 2100;
    public double ConnLongSpan { get => _connLongSpan; set { _connLongSpan = value; OnPropertyChanged(); } }
    private double _connWindLoad = 1.0;
    public double ConnWindLoad { get => _connWindLoad; set { _connWindLoad = value; OnPropertyChanged(); } }
    private string _connResult = "";
    public string ConnResult { get => _connResult; set { _connResult = value; OnPropertyChanged(); } }

    // ==================== 焊缝参数 ====================
    private double _weldVertLen = 100;
    public double WeldVertLen { get => _weldVertLen; set { _weldVertLen = value; OnPropertyChanged(); } }
    private double _weldHorizLen = 80;
    public double WeldHorizLen { get => _weldHorizLen; set { _weldHorizLen = value; OnPropertyChanged(); } }
    private double _weldFilletSize = 6;
    public double WeldFilletSize { get => _weldFilletSize; set { _weldFilletSize = value; OnPropertyChanged(); } }
    private double _weldTension = 5000;
    public double WeldTension { get => _weldTension; set { _weldTension = value; OnPropertyChanged(); } }
    private double _weldShear = 3000;
    public double WeldShear { get => _weldShear; set { _weldShear = value; OnPropertyChanged(); } }
    private double _weldMoment = 100000;
    public double WeldMoment { get => _weldMoment; set { _weldMoment = value; OnPropertyChanged(); } }
    private string _weldResult = "";
    public string WeldResult { get => _weldResult; set { _weldResult = value; OnPropertyChanged(); } }

    // ==================== 单元幕墙参数 ====================
    private double _ucwMullionHeight = 3600;
    public double UcwMullionHeight { get => _ucwMullionHeight; set { _ucwMullionHeight = value; OnPropertyChanged(); } }
    private double _ucwMullionSpacing = 1200;
    public double UcwMullionSpacing { get => _ucwMullionSpacing; set { _ucwMullionSpacing = value; OnPropertyChanged(); } }
    private double _ucwMullionShortSpan = 1500;
    public double UcwMullionShortSpan { get => _ucwMullionShortSpan; set { _ucwMullionShortSpan = value; OnPropertyChanged(); } }
    private double _ucwMullionLongSpan = 2100;
    public double UcwMullionLongSpan { get => _ucwMullionLongSpan; set { _ucwMullionLongSpan = value; OnPropertyChanged(); } }
    private double _ucwWindLoad = 1.0;
    public double UcwWindLoad { get => _ucwWindLoad; set { _ucwWindLoad = value; OnPropertyChanged(); } }
    private string _ucwResult = "";
    public string UcwResult { get => _ucwResult; set { _ucwResult = value; OnPropertyChanged(); } }

    // ==================== 门窗参数 ====================
    private double _dwSpan = 1500;
    public double DwSpan { get => _dwSpan; set { _dwSpan = value; OnPropertyChanged(); } }
    private double _dwSpacing = 1200;
    public double DwSpacing { get => _dwSpacing; set { _dwSpacing = value; OnPropertyChanged(); } }
    private double _dwWindLoad = 1.0;
    public double DwWindLoad { get => _dwWindLoad; set { _dwWindLoad = value; OnPropertyChanged(); } }
    private string _dwResult = "";
    public string DwResult { get => _dwResult; set { _dwResult = value; OnPropertyChanged(); } }

    // ==================== 计算书结果 ====================
    private string _reportText = "";
    public string ReportText { get => _reportText; set { _reportText = value; OnPropertyChanged(); } }

    // ==================== Methods ====================

    public async Task TestConnection()
    {
        StatusMessage = "正在测试连接...";
        IsConnected = await _api.TestConnectionAsync();
        StatusMessage = IsConnected ? "连接成功！" : "连接失败，请检查服务地址";
    }

    public async Task LoadCurrentPageData()
    {
        if (!IsConnected) return;
        try
        {
            string endpoint = SelectedNavIndex switch
            {
                15 => "GlassMaterials",
                16 => "AluminumProfiles",
                17 => "StoneMaterials",
                18 => "SiliconeParameters",
                19 => "EmbedPartSpecs",
                20 => "Standards",
                _ => ""
            };
            if (string.IsNullOrEmpty(endpoint)) return;

            StatusMessage = $"正在加载 {endpoint}...";
            var items = await _api.GetAllAsync<DataItem>(endpoint);
            CurrentItems.Clear();
            foreach (var item in items) CurrentItems.Add(item);
            StatusMessage = $"已加载 {items.Count} 条数据";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载失败: {ex.Message}";
        }
    }

    // 旧版辅助 (报告等兼容)
    private static string GlassTypeStr(int idx) => idx switch { 0 => "Tempered", 1 => "SemiTempered", _ => "Float" };
    private static string GlassTypeCN(int idx) => idx switch { 0 => "钢化玻璃", 1 => "半钢化玻璃", _ => "浮法玻璃" };
    private static string TerrainStr(int idx) => idx switch { 0 => "A", 1 => "B", 2 => "C", _ => "D" };

    private static string FormatSteps(List<StepDto>? steps)
    {
        if (steps == null || steps.Count == 0) return "";
        var sb = new StringBuilder();
        sb.AppendLine("\n═══════ 详细计算过程 ═══════");
        foreach (var s in steps)
        {
            sb.AppendLine($"\n步骤 {s.StepNo}: {s.Name}");
            if (!string.IsNullOrEmpty(s.Formula)) sb.AppendLine($"  公式: {s.Formula}");
            if (!string.IsNullOrEmpty(s.Process)) sb.AppendLine($"  过程: {s.Process}");
            if (!string.IsNullOrEmpty(s.Result)) sb.AppendLine($"  结果: {s.Result}");
        }
        return sb.ToString();
    }

    // ==================== 玻璃计算 (新版统一API) ====================

    private object BuildGlassConfigDtoForApi()
    {
        var cfg = BuildGlassConfigDto();
        // 序列化为匿名对象以匹配 JSON 格式
        return new
        {
            panelCategory = (int)cfg.PanelCategory,
            outerPane = PaneDtoToObj(cfg.OuterPane),
            innerPane = cfg.InnerPane != null ? PaneDtoToObj(cfg.InnerPane) : null,
            middlePane = cfg.MiddlePane != null ? PaneDtoToObj(cfg.MiddlePane) : null,
            firstGap = cfg.FirstGap != null ? new { thickness = cfg.FirstGap.Thickness, gasType = (int)cfg.FirstGap.GasType } : null,
            secondGap = cfg.SecondGap != null ? new { thickness = cfg.SecondGap.Thickness, gasType = (int)cfg.SecondGap.GasType } : null,
        };
    }

    private static object PaneDtoToObj(GlassPaneDtoLocal p) => new
    {
        isLaminated = p.IsLaminated,
        thickness = p.Thickness,
        temperedType = (int)p.TemperedType,
        laminatedSheets = p.LaminatedSheets?.Select(s => new { thickness = s.Thickness, temperedType = (int)s.TemperedType }).ToList(),
        laminatedLayers = p.LaminatedLayers?.Select(l => new { filmType = (int)l.FilmType, filmThickness = l.FilmThickness }).ToList()
    };

    // 内部DTO类 (避免引用Core中标记Obsolete的模型)
    internal class GlassConfigDtoLocal { public int PanelCategory; public GlassPaneDtoLocal OuterPane = new(); public GlassPaneDtoLocal? InnerPane; public GlassPaneDtoLocal? MiddlePane; public GapDtoLocal? FirstGap; public GapDtoLocal? SecondGap; public string DisplayName = ""; }
    internal class GlassPaneDtoLocal { public bool IsLaminated; public int Thickness = 6; public int TemperedType = 2; public List<SheetDtoLocal>? LaminatedSheets; public List<LayerDtoLocal>? LaminatedLayers; }
    internal class SheetDtoLocal { public int Thickness = 6; public int TemperedType = 2; }
    internal class LayerDtoLocal { public int FilmType; public double FilmThickness = 1.52; }
    internal class GapDtoLocal { public int Thickness = 12; public int GasType; }

    private GlassConfigDtoLocal BuildGlassConfigDto()
    {
        var dto = new GlassConfigDtoLocal { PanelCategory = PanelCategoryIndex };
        dto.OuterPane = BuildPaneDto(OuterIsLaminated, OuterThicknessIndex, OuterTemperedIndex, OuterLamSheets, OuterLamLayers);

        if (PanelCategoryIndex >= 1)
        {
            dto.InnerPane = BuildPaneDto(InnerIsLaminated, InnerThicknessIndex, InnerTemperedIndex, InnerLamSheets, InnerLamLayers);
            dto.FirstGap = new GapDtoLocal { Thickness = InsulatingThicknessOptions[Gap1ThicknessIndex < InsulatingThicknessOptions.Count ? Gap1ThicknessIndex : 0], GasType = Gap1GasIndex };
        }
        if (PanelCategoryIndex == 2)
        {
            dto.MiddlePane = BuildPaneDto(MiddleIsLaminated, MiddleThicknessIndex, MiddleTemperedIndex, MiddleLamSheets, MiddleLamLayers);
            dto.SecondGap = new GapDtoLocal { Thickness = InsulatingThicknessOptions[Gap2ThicknessIndex < InsulatingThicknessOptions.Count ? Gap2ThicknessIndex : 0], GasType = Gap2GasIndex };
        }

        // Build display name
        string paneName(GlassPaneDtoLocal p)
        {
            string tempText(int t) => t switch { 0 => "浮法", 1 => "半钢化", _ => "全钢化" };
            if (!p.IsLaminated) return $"{p.Thickness}{tempText(p.TemperedType)}";
            if (p.LaminatedSheets == null || p.LaminatedSheets.Count == 0) return $"{p.Thickness}{tempText(p.TemperedType)}";
            var parts = new List<string>();
            for (int i = 0; i < p.LaminatedSheets.Count; i++)
            {
                parts.Add($"{p.LaminatedSheets[i].Thickness}{tempText(p.LaminatedSheets[i].TemperedType)}");
                if (p.LaminatedLayers != null && i < p.LaminatedLayers.Count)
                {
                    string ft = p.LaminatedLayers[i].FilmType == 0 ? "PVB" : "SGP";
                    parts.Add($"{p.LaminatedLayers[i].FilmThickness}{ft}");
                }
            }
            return string.Join("+", parts);
        }
        string gapName(GapDtoLocal g) => $"{g.Thickness}{(g.GasType == 0 ? "Air" : "Ar")}";

        if (PanelCategoryIndex == 0)
            dto.DisplayName = paneName(dto.OuterPane);
        else if (PanelCategoryIndex == 1)
            dto.DisplayName = $"{paneName(dto.OuterPane)}+{gapName(dto.FirstGap!)}+{paneName(dto.InnerPane!)}";
        else
            dto.DisplayName = $"{paneName(dto.OuterPane)}+{gapName(dto.FirstGap!)}+{paneName(dto.MiddlePane!)}+{gapName(dto.SecondGap!)}+{paneName(dto.InnerPane!)}";

        return dto;
    }

    private GlassPaneDtoLocal BuildPaneDto(bool isLam, int thkIdx, int tmpIdx, ObservableCollection<LamSheetVM> sheets, ObservableCollection<LamLayerVM> layers)
    {
        var p = new GlassPaneDtoLocal { IsLaminated = isLam };
        p.Thickness = ThicknessOptions[thkIdx < ThicknessOptions.Count ? thkIdx : 0];
        p.TemperedType = tmpIdx;
        if (isLam && sheets.Count > 0)
        {
            p.LaminatedSheets = sheets.Select(s => new SheetDtoLocal { Thickness = ThicknessOptions[s.ThicknessIndex < ThicknessOptions.Count ? s.ThicknessIndex : 0], TemperedType = s.TemperedIndex }).ToList();
            p.LaminatedLayers = layers.Select(l => new LayerDtoLocal { FilmType = l.FilmTypeIndex, FilmThickness = FilmThicknessOptionsList[l.FilmThicknessIndex < FilmThicknessOptionsList.Count ? l.FilmThicknessIndex : 0] }).ToList();
        }
        return p;
    }

    public async Task CalculateGlass()
    {
        if (!IsConnected) { CalcResult = "请先连接服务器"; return; }
        try
        {
            StatusMessage = "正在计算玻璃...";
            var glassConfig = BuildGlassConfigDtoForApi();
            var configDto = BuildGlassConfigDto();
            string configName = configDto.DisplayName;

            var request = new
            {
                ShortSide = GlassShortSide,
                LongSide = GlassLongSide,
                GlassConfig = glassConfig,
                WindLoad,
                AltitudeDifference,
                TemperatureDifference,
                LoadDuration = 3.0,
                SupportCondition = "FourSides"
            };

            var result = await _api.CalculateAsync<object, CalcResultDto>("GlassCalculation/UnifiedCalculate", request);
            if (result != null)
            {
                CalcResult = $"═══════ {configName} 计算结果 ═══════\n验算结果: {(result.Passed ? "✅ 通过" : "❌ 不通过")}\n最大应力: {result.MaxStress:F2} MPa\n最大挠度: {result.MaxDeflection:F2} mm\n挠度限值: {result.DeflectionLimit:F2} mm\n计算依据: {result.Standard}\n═══════════════════════════════" + FormatSteps(result.Steps);
            }
            StatusMessage = "计算完成";
        }
        catch (Exception ex) { CalcResult = $"计算失败: {ex.Message}"; StatusMessage = "计算失败"; }
    }

    public async Task CalculateAluminumRafter()
    {
        if (!IsConnected) { AluCalcResult = "请先连接服务器"; return; }
        try
        {
            StatusMessage = "正在计算铝合金横梁...";
            var request = new { Span = AluSpan, Spacing = AluSpacing, ProfileSpec = AluProfileSpec, WindLoad = AluWindLoad, AluminumGrade = "6063-T5" };
            var result = await _api.CalculateAsync<object, GenericCalcResult>("AluminumCalculation/Rafter", request);
            if (result != null)
                AluCalcResult = $"═══════ 铝合金横梁计算结果 ═══════\n验算结果: {(result.Passed ? "✅ 通过" : "❌ 不通过")}\n计算依据: {result.Standard}\n═══════════════════════════════" + FormatSteps(result.Steps);
            StatusMessage = "计算完成";
        }
        catch (Exception ex) { AluCalcResult = $"计算失败: {ex.Message}"; }
    }

    public async Task CalculateStonePanel()
    {
        if (!IsConnected) { StoneCalcResult = "请先连接服务器"; return; }
        try
        {
            StatusMessage = "正在计算石材面板...";
            var request = new { ShortSide = StoneShortSide, LongSide = StoneLongSide, Thickness = StoneThickness, StoneType = "Granite", WindLoad = StoneWindLoad, SupportType = "ShortSidePin", BracketCount = 4 };
            var result = await _api.CalculateAsync<object, GenericCalcResult>("StoneCalculation/Panel", request);
            if (result != null)
                StoneCalcResult = $"═══════ 石材面板计算结果 ═══════\n验算结果: {(result.Passed ? "✅ 通过" : "❌ 不通过")}\n计算依据: {result.Standard}\n═════════════════════════════" + FormatSteps(result.Steps);
            StatusMessage = "计算完成";
        }
        catch (Exception ex) { StoneCalcResult = $"计算失败: {ex.Message}"; }
    }

    // ==================== 铝板幕墙 ====================

    public async Task CalculateAluminumPanel()
    {
        if (!IsConnected) { AluPanelResult = "请先连接服务器"; return; }
        try
        {
            StatusMessage = "正在计算铝板幕墙...";
            var request = new { ShortSide = AluPanelShortSide, LongSide = AluPanelLongSide, Thickness = AluPanelThickness, WindLoad = AluPanelWindLoad, DesignLoad = AluPanelDesignLoad, StiffenerSpacing = AluPanelStiffenerSpacing };
            var result = await _api.CalculateAsync<object, GenericCalcResult>("AluminumPanelCalculation/Calculate", request);
            if (result != null)
                AluPanelResult = $"═══════ 铝板幕墙计算结果 ═══════\n验算结果: {(result.Passed ? "✅ 通过" : "❌ 不通过")}\n计算依据: {result.Standard}\n═══════════════════════════════" + FormatSteps(result.Steps);
            StatusMessage = "计算完成";
        }
        catch (Exception ex) { AluPanelResult = $"计算失败: {ex.Message}"; }
    }

    // ==================== GRC板 ====================

    public async Task CalculateGrc()
    {
        if (!IsConnected) { GrcResult = "请先连接服务器"; return; }
        try
        {
            StatusMessage = "正在计算GRC板...";
            var request = new { ShortSide = GrcShortSide, LongSide = GrcLongSide, SupportShortSide = GrcSupportShort, SupportLongSide = GrcSupportLong, EffectiveThickness = GrcEffThickness, TotalThickness = GrcTotalThickness, WindLoad = GrcWindLoad, DesignLoad = GrcDesignLoad };
            var result = await _api.CalculateAsync<object, GenericCalcResult>("GrcPanelCalculation/Calculate", request);
            if (result != null)
                GrcResult = $"═══════ GRC板计算结果 ═══════\n验算结果: {(result.Passed ? "✅ 通过" : "❌ 不通过")}\n计算依据: {result.Standard}\n═══════════════════════════════" + FormatSteps(result.Steps);
            StatusMessage = "计算完成";
        }
        catch (Exception ex) { GrcResult = $"计算失败: {ex.Message}"; }
    }

    // ==================== UHPC板 ====================

    public async Task CalculateUhpc()
    {
        if (!IsConnected) { UhpcResult = "请先连接服务器"; return; }
        try
        {
            StatusMessage = "正在计算UHPC板...";
            var request = new { ShortSide = UhpcShortSide, LongSide = UhpcLongSide, Span = UhpcSpan, EffectiveThickness = UhpcEffThickness, WindLoad = UhpcWindLoad, DesignLoad = UhpcDesignLoad };
            var result = await _api.CalculateAsync<object, GenericCalcResult>("UhpcPanelCalculation/Calculate", request);
            if (result != null)
                UhpcResult = $"═══════ UHPC板计算结果 ═══════\n验算结果: {(result.Passed ? "✅ 通过" : "❌ 不通过")}\n计算依据: {result.Standard}\n═══════════════════════════════" + FormatSteps(result.Steps);
            StatusMessage = "计算完成";
        }
        catch (Exception ex) { UhpcResult = $"计算失败: {ex.Message}"; }
    }

    // ==================== 点式钢管 ====================

    public async Task CalculatePointSteel()
    {
        if (!IsConnected) { PsResult = "请先连接服务器"; return; }
        try
        {
            StatusMessage = "正在计算点式钢管...";
            var request = new { Span = PsSpan, GridWidth = PsGridWidth, WindLoad = PsWindLoad, SectionArea = PsSectionArea, MomentOfInertia = PsMomentOfInertia, SectionModulus = PsSectionModulus, LoadPositions = new List<double> { PsSpan / 3.0, PsSpan * 2 / 3.0 }, SegmentLengths = new List<double> { PsSpan / 3.0, PsSpan / 3.0, PsSpan / 3.0 } };
            var result = await _api.CalculateAsync<object, GenericCalcResult>("PointSteelCalculation/Calculate", request);
            if (result != null)
                PsResult = $"═══════ 点式钢管计算结果 ═══════\n验算结果: {(result.Passed ? "✅ 通过" : "❌ 不通过")}\n计算依据: {result.Standard}\n═══════════════════════════════" + FormatSteps(result.Steps);
            StatusMessage = "计算完成";
        }
        catch (Exception ex) { PsResult = $"计算失败: {ex.Message}"; }
    }

    // ==================== 风荷载 ====================

    public async Task CalculateWindLoad()
    {
        if (!IsConnected) { WlResult = "请先连接服务器"; return; }
        try
        {
            StatusMessage = "正在计算风荷载...";
            var request = new { Height = WlHeight, TerrainCategory = TerrainStr(WlTerrainIndex), BasicWindPressure = WlBasicPressure, ShapeCoefficient = WlShapeCoeff };
            var result = await _api.CalculateAsync<object, GenericCalcResult>("WindLoadCalculation/Calculate", request);
            if (result != null)
                WlResult = $"═══════ 风荷载计算结果 ═══════\n计算依据: {result.Standard}\n═══════════════════════════════" + FormatSteps(result.Steps);
            StatusMessage = "计算完成";
        }
        catch (Exception ex) { WlResult = $"计算失败: {ex.Message}"; }
    }

    // ==================== 地震作用 ====================

    public async Task CalculateSeismic()
    {
        if (!IsConnected) { SeResult = "请先连接服务器"; return; }
        try
        {
            StatusMessage = "正在计算地震作用...";
            var request = new { AlphaMax = SeAlphaMax, BetaE = SeBetaE, GravityLoad = SeGravityLoad, Area = SeArea };
            var result = await _api.CalculateAsync<object, GenericCalcResult>("SeismicCalculation/Calculate", request);
            if (result != null)
                SeResult = $"═══════ 地震作用计算结果 ═══════\n计算依据: {result.Standard}\n═══════════════════════════════" + FormatSteps(result.Steps);
            StatusMessage = "计算完成";
        }
        catch (Exception ex) { SeResult = $"计算失败: {ex.Message}"; }
    }

    // ==================== 连接件 ====================

    public async Task CalculateConnection()
    {
        if (!IsConnected) { ConnResult = "请先连接服务器"; return; }
        try
        {
            StatusMessage = "正在计算连接件...";
            var request = new { MullionSpacing = ConnMullionSpacing, TransomWidth = ConnTransomWidth, TransomHeight = ConnTransomHeight, MullionSpan = ConnMullionSpan, ShortSpan = ConnShortSpan, LongSpan = ConnLongSpan, WindLoad = ConnWindLoad };
            var result = await _api.CalculateAsync<object, GenericCalcResult>("ConnectionCalculation/Calculate", request);
            if (result != null)
                ConnResult = $"═══════ 连接件计算结果 ═══════\n验算结果: {(result.Passed ? "✅ 通过" : "❌ 不通过")}\n计算依据: {result.Standard}\n═══════════════════════════════" + FormatSteps(result.Steps);
            StatusMessage = "计算完成";
        }
        catch (Exception ex) { ConnResult = $"计算失败: {ex.Message}"; }
    }

    // ==================== 焊缝 ====================

    public async Task CalculateWeld()
    {
        if (!IsConnected) { WeldResult = "请先连接服务器"; return; }
        try
        {
            StatusMessage = "正在计算焊缝...";
            var request = new { VerticalWeldLength = WeldVertLen, HorizontalWeldLength = WeldHorizLen, FilletSize = WeldFilletSize, TensionForce = WeldTension, ShearForce = WeldShear, BendingMoment = WeldMoment };
            var result = await _api.CalculateAsync<object, GenericCalcResult>("WeldCalculation/Calculate", request);
            if (result != null)
                WeldResult = $"═══════ 焊缝计算结果 ═══════\n验算结果: {(result.Passed ? "✅ 通过" : "❌ 不通过")}\n计算依据: {result.Standard}\n═══════════════════════════════" + FormatSteps(result.Steps);
            StatusMessage = "计算完成";
        }
        catch (Exception ex) { WeldResult = $"计算失败: {ex.Message}"; }
    }

    // ==================== 单元幕墙 ====================

    public async Task CalculateUcwMullion()
    {
        if (!IsConnected) { UcwResult = "请先连接服务器"; return; }
        try
        {
            StatusMessage = "正在计算单元幕墙立柱...";
            var request = new { Span = UcwMullionHeight, Spacing = UcwMullionSpacing, WindLoad = UcwWindLoad, ProfileSpec = "100×50×3" };
            var result = await _api.CalculateAsync<object, GenericCalcResult>("UnitCurtainWallCalculation/Mullion", request);
            if (result != null)
                UcwResult = $"═══════ 单元幕墙立柱计算结果 ═══════\n验算结果: {(result.Passed ? "✅ 通过" : "❌ 不通过")}\n计算依据: {result.Standard}\n═══════════════════════════════" + FormatSteps(result.Steps);
            StatusMessage = "计算完成";
        }
        catch (Exception ex) { UcwResult = $"计算失败: {ex.Message}"; }
    }

    public async Task CalculateUcwTransom()
    {
        if (!IsConnected) { UcwResult = "请先连接服务器"; return; }
        try
        {
            StatusMessage = "正在计算单元幕墙横梁...";
            var request = new { Span = UcwMullionSpacing, Spacing = UcwMullionHeight / 2, WindLoad = UcwWindLoad, ProfileSpec = "100×50×3" };
            var result = await _api.CalculateAsync<object, GenericCalcResult>("UnitCurtainWallCalculation/Transom", request);
            if (result != null)
                UcwResult = $"═══════ 单元幕墙横梁计算结果 ═══════\n验算结果: {(result.Passed ? "✅ 通过" : "❌ 不通过")}\n计算依据: {result.Standard}\n═══════════════════════════════" + FormatSteps(result.Steps);
            StatusMessage = "计算完成";
        }
        catch (Exception ex) { UcwResult = $"计算失败: {ex.Message}"; }
    }

    // ==================== 门窗 ====================

    public async Task CalculateDwMullion()
    {
        if (!IsConnected) { DwResult = "请先连接服务器"; return; }
        try
        {
            StatusMessage = "正在计算门窗立柱...";
            var request = new { Span = DwSpan, LoadWidth1 = DwSpacing / 2.0, LoadWidth2 = DwSpacing / 2.0, WindLoad = DwWindLoad };
            var result = await _api.CalculateAsync<object, GenericCalcResult>("DoorWindowCalculation/Mullion", request);
            if (result != null)
                DwResult = $"═══════ 门窗立柱计算结果 ═══════\n验算结果: {(result.Passed ? "✅ 通过" : "❌ 不通过")}\n计算依据: {result.Standard}\n═══════════════════════════════" + FormatSteps(result.Steps);
            StatusMessage = "计算完成";
        }
        catch (Exception ex) { DwResult = $"计算失败: {ex.Message}"; }
    }

    public async Task CalculateDwHardware()
    {
        if (!IsConnected) { DwResult = "请先连接服务器"; return; }
        try
        {
            StatusMessage = "正在计算门窗五金...";
            var request = new { WindowWidth = DwSpacing, WindowHeight = DwSpan, WindLoad = DwWindLoad };
            var result = await _api.CalculateAsync<object, GenericCalcResult>("DoorWindowCalculation/Hardware", request);
            if (result != null)
                DwResult = $"═══════ 门窗五金计算结果 ═══════\n验算结果: {(result.Passed ? "✅ 通过" : "❌ 不通过")}\n计算依据: {result.Standard}\n═══════════════════════════════" + FormatSteps(result.Steps);
            StatusMessage = "计算完成";
        }
        catch (Exception ex) { DwResult = $"计算失败: {ex.Message}"; }
    }

    public async Task CalculateDwSash()
    {
        if (!IsConnected) { DwResult = "请先连接服务器"; return; }
        try
        {
            StatusMessage = "正在计算门窗开启扇...";
            var request = new { GlassLongSide = DwSpan, GlassShortSide = DwSpacing, WindLoad = DwWindLoad };
            var result = await _api.CalculateAsync<object, GenericCalcResult>("DoorWindowCalculation/OpeningSash", request);
            if (result != null)
                DwResult = $"═══════ 门窗开启扇计算结果 ═══════\n验算结果: {(result.Passed ? "✅ 通过" : "❌ 不通过")}\n计算依据: {result.Standard}\n═══════════════════════════════" + FormatSteps(result.Steps);
            StatusMessage = "计算完成";
        }
        catch (Exception ex) { DwResult = $"计算失败: {ex.Message}"; }
    }

    // ==================== 计算书生成 ====================

    public async Task GenerateReport(string reportType)
    {
        if (!IsConnected) { ReportText = "请先连接服务器"; return; }
        try
        {
            StatusMessage = $"正在生成 {reportType} 计算书...";
            ReportText = "⏳ 正在调用计算接口获取详细步骤...";

            // 根据报告类型调用对应的计算API获取Steps
            string endpoint;
            object request;

            switch (reportType)
            {
                case "glass":
                case "insulating-glass":
                case "glass-rib":
                    endpoint = "GlassCalculation/UnifiedCalculate";
                    request = new { ShortSide = GlassShortSide, LongSide = GlassLongSide, GlassConfig = BuildGlassConfigDtoForApi(), WindLoad, AltitudeDifference, TemperatureDifference, LoadDuration = 3.0, SupportCondition = "FourSides" };
                    break;
                case "aluminum-rafter":
                    endpoint = "AluminumCalculation/Rafter";
                    request = new { Span = AluSpan, Spacing = AluSpacing, ProfileSpec = AluProfileSpec, WindLoad = AluWindLoad, AluminumGrade = "6063-T5" };
                    break;
                case "aluminum-mullion":
                    endpoint = "AluminumCalculation/Rafter";
                    request = new { Span = AluSpan, Spacing = AluSpacing, ProfileSpec = AluProfileSpec, WindLoad = AluWindLoad, AluminumGrade = "6063-T5" };
                    break;
                case "stone-bracket":
                    endpoint = "StoneCalculation/Bracket";
                    request = new { PanelWidth = StoneLongSide, PanelHeight = StoneShortSide, StoneThickness, StoneDensity = 2700.0, WindLoad = StoneWindLoad, BracketCount = 4 };
                    break;
                case "embed-part":
                    endpoint = "StructureCalculation/EmbedPart";
                    request = new { EmbedType = "Plate", PlateLength = 200.0, PlateWidth = 150.0, PlateThickness = 10.0, AnchorDiameter = 12.0, AnchorCount = 4, AnchorLength = 120.0, ConcreteGrade = "C30" };
                    break;
                case "aluminum-plate":
                    endpoint = "AluminumPanelCalculation/Calculate";
                    request = new { ShortSide = AluPanelShortSide, LongSide = AluPanelLongSide, Thickness = AluPanelThickness, WindLoad = AluPanelWindLoad, DesignLoad = AluPanelDesignLoad, StiffenerSpacing = AluPanelStiffenerSpacing };
                    break;
                case "stone-panel":
                    endpoint = "StoneCalculation/Panel";
                    request = new { ShortSide = StoneShortSide, LongSide = StoneLongSide, Thickness = StoneThickness, StoneType = "Granite", WindLoad = StoneWindLoad, SupportType = "ShortSidePin", BracketCount = 4 };
                    break;
                case "silicone":
                    endpoint = "StructureCalculation/Silicone";
                    request = new { BondLengthShort = GlassShortSide, BondLengthLong = GlassLongSide, SealantWidth = 12.0, SealantThickness = 6.0, WindLoad };
                    break;
                default:
                    ReportText = "未知报告类型";
                    return;
            }

            var result = await _api.CalculateAsync<object, GenericCalcResult>(endpoint, request);
            if (result != null && result.Steps != null && result.Steps.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"╔══════════════════════════════════════════╗");
                sb.AppendLine($"║  {reportType.ToUpper()} 计算书");
                sb.AppendLine($"╠══════════════════════════════════════════╣");
                sb.AppendLine($"║  验算结果: {(result.Passed ? "✅ 通过" : "❌ 不通过")}");
                sb.AppendLine($"║  计算依据: {result.Standard}");
                sb.AppendLine($"╚══════════════════════════════════════════╝");
                sb.AppendLine();

                foreach (var step in result.Steps)
                {
                    sb.AppendLine($"━━━ 步骤 {step.StepNo}: {step.Name} ━━━");
                    if (!string.IsNullOrEmpty(step.Formula))
                        sb.AppendLine($"  公式: {step.Formula}");
                    if (!string.IsNullOrEmpty(step.Process))
                        sb.AppendLine($"  代入: {step.Process}");
                    if (!string.IsNullOrEmpty(step.Result))
                        sb.AppendLine($"  结果: {step.Result}");
                    sb.AppendLine();
                }

                ReportText = sb.ToString();
            }
            else
            {
                ReportText = "计算完成，但未返回详细步骤。请确认后端已实现 Steps 输出。";
            }
            StatusMessage = "计算书生成完成";
        }
        catch (Exception ex)
        {
            ReportText = $"生成失败: {ex.Message}";
            StatusMessage = "计算书生成失败";
        }
    }

    // ==================== 项目参数方法 ====================

    public async Task LoadAllRegions()
    {
        try
        {
            StatusMessage = "正在加载地区列表...";
            _ppAllRegions = await _api.SearchRegionsAsync("");
            FilterRegionList();
            StatusMessage = $"已加载 {_ppAllRegions.Count} 个地区";
        }
        catch (Exception ex) { StatusMessage = $"加载地区失败: {ex.Message}"; }
    }

    public void FilterRegionList()
    {
        var kw = (PpRegionKeyword ?? "").Trim();
        _ppRegionDataList = string.IsNullOrEmpty(kw)
            ? _ppAllRegions
            : _ppAllRegions.Where(r => r.Name.Contains(kw)).ToList();
        PpRegionList.Clear();
        foreach (var r in _ppRegionDataList) PpRegionList.Add(r.Name);
    }

    public async Task SearchRegions()
    {
        if (_ppAllRegions.Count == 0) await LoadAllRegions();
        else FilterRegionList();
    }

    public void SelectRegion(int index)
    {
        if (index < 0 || index >= _ppRegionDataList.Count) return;
        var r = _ppRegionDataList[index];
        PpSelectedRegion = r.Name;
        PpBasicWindPressure = r.WindPressure50 / 1000.0; // Pa → kN/m²
        PpSeismicIntensity = r.SeismicIntensity.ToString();
        PpAlphaMax = r.AlphaMax;
        PpSnowPressure = r.SnowPressure50 / 1000.0; // Pa → kN/m²
        PpTempDiff = r.TemperatureDiff;
        PpSeismicResult = $"抗震烈度: {r.SeismicIntensity}\nαmax = {r.AlphaMax}\nqEAk = αmax × βE × Gk / A";
    }

    public async Task CalculateProjectWindLoad()
    {
        if (!IsConnected) { PpWindResult = "请先连接服务器"; return; }
        try
        {
            StatusMessage = "正在计算风荷载...";
            var request = new
            {
                W0 = PpBasicWindPressure * 0.001, // kN/m² → MPa (后端W0单位是MPa)
                TerrainCategory = TerrainStr(PpTerrainIndex),
                Height = PpHeight,
                BuildingType = PpBuildingTypeIndex == 0 ? "Closed" : "DominantOpening",
                StructureArea = PpTributaryArea,
                ReturnPeriod = PpReturnPeriodIndex == 0 ? 50 : 100
            };
            var result = await _api.CalculateProjectWindLoad(request);
            if (result != null)
            {
                // WkPanel/WkStructure 从后端返回的是 MPa，× 1000 转为 kN/m² 显示
                var wkPanelKN = result.WkPanel * 1000;
                var wkStructKN = result.WkStructure * 1000;
                var sb = new StringBuilder();
                sb.AppendLine("═══════ 风荷载计算结果 ═══════");
                sb.AppendLine($"μz (风压高度变化系数) = {result.MuZ:F4}");
                sb.AppendLine($"βgz (阵风系数) = {result.BetaGz:F4}");
                sb.AppendLine($"β (风振系数) = {result.Beta:F4}");
                sb.AppendLine($"μs1 面板 = {result.Mus1Panel:F2}");
                sb.AppendLine($"μs1 支承 = {result.Mus1Structure:F2}");
                sb.AppendLine($"wk 面板 = {wkPanelKN:F4} kN/m²");
                sb.AppendLine($"wk 支承 = {wkStructKN:F4} kN/m²");
                sb.AppendLine("═══════════════════════════════");
                PpWindResult = sb.ToString();

                // 自动传递wk值到各计算页 (全部统一为 kN/m²)
                WindLoad = wkPanelKN;          // 玻璃页 — kN/m²
                AluWindLoad = wkStructKN;      // 铝合金龙骨 — kN/m²
                StoneWindLoad = wkPanelKN;     // 石材 — kN/m²
                AluPanelWindLoad = wkPanelKN;  // 铝板 — kN/m²
                GrcWindLoad = wkPanelKN;       // GRC — kN/m²
                UhpcWindLoad = wkPanelKN;      // UHPC — kN/m²
                PsWindLoad = wkPanelKN;        // 点式钢管 — kN/m²
                ConnWindLoad = wkStructKN;     // 连接件 — kN/m²
                UcwWindLoad = wkStructKN;      // 单元幕墙 — kN/m²
                DwWindLoad = wkPanelKN;        // 门窗 — kN/m²
                StatusMessage = "风荷载已计算并自动填入各计算页";
            }
        }
        catch (Exception ex) { PpWindResult = $"计算失败: {ex.Message}"; StatusMessage = "风荷载计算失败"; }
    }

    // ==================== 数据管理CRUD ====================

    private static string NavToEndpoint(int idx) => idx switch
    {
        15 => "GlassMaterials", 16 => "AluminumProfiles", 17 => "StoneMaterials",
        18 => "SiliconeParameters", 19 => "EmbedPartSpecs", 20 => "Standards", _ => ""
    };

    private static string NavToLabel(int idx) => idx switch
    {
        15 => "玻璃材质", 16 => "铝型材", 17 => "石材",
        18 => "结构胶", 19 => "预埋件", 20 => "技术规范", _ => "数据"
    };

    private DataItem? _selectedItem;
    public DataItem? SelectedItem { get => _selectedItem; set { _selectedItem = value; OnPropertyChanged(); } }

    private string _editName = "";
    public string EditName { get => _editName; set { _editName = value; OnPropertyChanged(); } }

    private string _editRemark = "";
    public string EditRemark { get => _editRemark; set { _editRemark = value; OnPropertyChanged(); } }

    public async Task AddItem()
    {
        if (!IsConnected) { StatusMessage = "请先连接服务器"; return; }
        var ep = NavToEndpoint(SelectedNavIndex);
        if (string.IsNullOrEmpty(ep) || string.IsNullOrEmpty(EditName)) { StatusMessage = "请输入名称"; return; }
        try
        {
            StatusMessage = $"正在新增{NavToLabel(SelectedNavIndex)}...";
            var item = new DataItem { Name = EditName, Remark = EditRemark, IsActive = true };
            await _api.CreateAsync(ep, item);
            EditName = ""; EditRemark = "";
            await LoadCurrentPageData();
            StatusMessage = "新增成功";
        }
        catch (Exception ex) { StatusMessage = $"新增失败: {ex.Message}"; }
    }

    public async Task DeleteItem()
    {
        if (!IsConnected || SelectedItem == null) { StatusMessage = "请先选中一条数据"; return; }
        var ep = NavToEndpoint(SelectedNavIndex);
        if (string.IsNullOrEmpty(ep)) return;
        try
        {
            StatusMessage = $"正在删除...";
            await _api.DeleteAsync(ep, SelectedItem.Id);
            await LoadCurrentPageData();
            StatusMessage = "删除成功";
        }
        catch (Exception ex) { StatusMessage = $"删除失败: {ex.Message}"; }
    }

    // ==================== 简图/云图方法 ====================

    public async Task LoadDiagram(string endpoint, object request)
    {
        if (!IsConnected) { StatusMessage = "请先连接服务器"; return; }
        try
        {
            StatusMessage = "正在加载简图...";
            var svg = await _api.GetSvgAsync(endpoint, request);
            OpenSvgInBrowser(svg, "简图");
            StatusMessage = "简图已在浏览器中打开";
        }
        catch (Exception ex) { StatusMessage = $"加载失败: {ex.Message}"; }
    }

    private void OpenSvgInBrowser(string svgContent, string title)
    {
        var html = $"<!DOCTYPE html><html><head><meta charset='utf-8'><title>{title}</title><style>body{{margin:20px;background:#f5f5f5;display:flex;justify-content:center;}}svg{{max-width:100%;height:auto;}}</style></head><body>{svgContent}</body></html>";
        var path = Path.Combine(Path.GetTempPath(), $"glass_diagram_{Guid.NewGuid():N}.html");
        File.WriteAllText(path, html);
        var psi = new ProcessStartInfo(path) { UseShellExecute = true };
        Process.Start(psi);
    }

    public async Task LoadGlassDiagram()
    {
        // PlateSupportDiagramRequest: A(短边), B(长边), T(厚度), SupportType
        await LoadDiagram("Diagram/PlateSupport", new { A = GlassShortSide, B = GlassLongSide, T = GlassThickness, SupportType = "四边简支" });
    }

    public async Task LoadAluminumDiagram()
    {
        // MullionDiagramRequest: L, L1, L2, Q, M1, R1, BeamType
        await LoadDiagram("Diagram/MullionBeam", new { L = AluSpan, L1 = AluSpan * 0.4, L2 = AluSpan * 0.6, Q = 0.0, BeamType = "双跨" });
    }

    public async Task LoadContourStress()
    {
        await LoadDiagram("Contour/PlateStress", new { ShortSide = GlassShortSide, LongSide = GlassLongSide, Thickness = GlassThickness, WindLoad, ElasticModulus = 72000.0, PoissonRatio = 0.2 });
    }

    public async Task LoadContourDeflection()
    {
        await LoadDiagram("Contour/PlateDeflection", new { ShortSide = GlassShortSide, LongSide = GlassLongSide, Thickness = GlassThickness, WindLoad, ElasticModulus = 72000.0, PoissonRatio = 0.2 });
    }

    // ==================== 组合玻璃解析 ====================

    public static (List<object> Layers, List<double> AirGaps) ParseCompositeExpression(string expr, string defaultGlassType)
    {
        // Parse expressions like "6+12A+6+1.52PVB+6" or "6+12A+6+1.52PVB+6+12A+6"
        // Tokens: number = glass thickness, numberA = air gap, number+type (PVB/SGP/EVA) = interlayer
        var layers = new List<object>();
        var airGaps = new List<double>();

        var tokens = expr.Split('+', StringSplitOptions.RemoveEmptyEntries);
        var pendingGlass = new List<double>();
        var pendingInterlayers = new List<double>();
        string pendingInterlayerType = "PVB";

        void FlushGlassGroup()
        {
            if (pendingGlass.Count == 0) return;
            if (pendingGlass.Count == 1 && pendingInterlayers.Count == 0)
            {
                layers.Add(new { Type = "Single", Thickness = pendingGlass[0], GlassType = defaultGlassType });
            }
            else if (pendingGlass.Count >= 2 && pendingInterlayers.Count > 0)
            {
                layers.Add(new { Type = "Laminated", GlassThicknesses = pendingGlass.ToArray(), InterlayerThicknesses = pendingInterlayers.ToArray(), InterlayerType = pendingInterlayerType, GlassType = defaultGlassType });
            }
            else
            {
                // multiple glass without interlayer — treat each as single
                foreach (var t in pendingGlass)
                    layers.Add(new { Type = "Single", Thickness = t, GlassType = defaultGlassType });
            }
            pendingGlass.Clear();
            pendingInterlayers.Clear();
        }

        foreach (var raw in tokens)
        {
            var token = raw.Trim();
            // Air gap: "12A" or "16A"
            var airMatch = Regex.Match(token, @"^(\d+(?:\.\d+)?)A$", RegexOptions.IgnoreCase);
            if (airMatch.Success)
            {
                FlushGlassGroup();
                airGaps.Add(double.Parse(airMatch.Groups[1].Value));
                continue;
            }
            // Interlayer: "1.52PVB" or "0.76SGP" or "1.14EVA"
            var interMatch = Regex.Match(token, @"^(\d+(?:\.\d+)?)(PVB|SGP|EVA)$", RegexOptions.IgnoreCase);
            if (interMatch.Success)
            {
                pendingInterlayers.Add(double.Parse(interMatch.Groups[1].Value));
                pendingInterlayerType = interMatch.Groups[2].Value.ToUpper();
                continue;
            }
            // Plain number = glass thickness
            if (double.TryParse(token, out var thickness))
            {
                pendingGlass.Add(thickness);
                continue;
            }
        }
        FlushGlassGroup();

        return (layers, airGaps);
    }

    // ==================== Step2 玻璃配置 (对齐Blazor Step2Glass) ====================
    public string Step2Title => $"{CurtainWallTypes[CurtainWallTypeIndex]}幕墙计算 - 玻璃配置";

    // 玻璃构成
    private bool _gcSingle = true;
    public bool GcSingle { get => _gcSingle; set { if (value) { _gcSingle = true; _gcInsulating = _gcLaminated = _gcInsLam = _gcDoubleIns = false; NotifyGlassComp(); } } }
    private bool _gcInsulating;
    public bool GcInsulating { get => _gcInsulating; set { if (value) { _gcInsulating = true; _gcSingle = _gcLaminated = _gcInsLam = _gcDoubleIns = false; NotifyGlassComp(); } } }
    private bool _gcLaminated;
    public bool GcLaminated { get => _gcLaminated; set { if (value) { _gcLaminated = true; _gcSingle = _gcInsulating = _gcInsLam = _gcDoubleIns = false; NotifyGlassComp(); } } }
    private bool _gcInsLam;
    public bool GcInsLam { get => _gcInsLam; set { if (value) { _gcInsLam = true; _gcSingle = _gcInsulating = _gcLaminated = _gcDoubleIns = false; NotifyGlassComp(); } } }
    private bool _gcDoubleIns;
    public bool GcDoubleIns { get => _gcDoubleIns; set { if (value) { _gcDoubleIns = true; _gcSingle = _gcInsulating = _gcLaminated = _gcInsLam = false; NotifyGlassComp(); } } }
    private void NotifyGlassComp() { OnPropertyChanged(nameof(GcSingle)); OnPropertyChanged(nameof(GcInsulating)); OnPropertyChanged(nameof(GcLaminated)); OnPropertyChanged(nameof(GcInsLam)); OnPropertyChanged(nameof(GcDoubleIns)); OnPropertyChanged(nameof(S2ShowAirGap)); OnPropertyChanged(nameof(S2ShowInterlayer)); }
    public bool S2ShowAirGap => GcInsulating || GcInsLam || GcDoubleIns;
    public bool S2ShowInterlayer => GcLaminated || GcInsLam;

    // 玻璃片层
    private int _s2OuterThicknessIndex = 3;
    public int S2OuterThicknessIndex { get => _s2OuterThicknessIndex; set { _s2OuterThicknessIndex = value; OnPropertyChanged(); } }
    private int _s2OuterTypeIndex = 0;
    public int S2OuterTypeIndex { get => _s2OuterTypeIndex; set { _s2OuterTypeIndex = value; OnPropertyChanged(); } }
    private int _s2InnerThicknessIndex = 3;
    public int S2InnerThicknessIndex { get => _s2InnerThicknessIndex; set { _s2InnerThicknessIndex = value; OnPropertyChanged(); } }
    private int _s2InnerTypeIndex = 0;
    public int S2InnerTypeIndex { get => _s2InnerTypeIndex; set { _s2InnerTypeIndex = value; OnPropertyChanged(); } }
    private int _s2AirGapIndex = 2;
    public int S2AirGapIndex { get => _s2AirGapIndex; set { _s2AirGapIndex = value; OnPropertyChanged(); } }
    private int _s2InterlayerThicknessIndex = 0;
    public int S2InterlayerThicknessIndex { get => _s2InterlayerThicknessIndex; set { _s2InterlayerThicknessIndex = value; OnPropertyChanged(); } }
    private int _s2InterlayerTypeIndex = 0;
    public int S2InterlayerTypeIndex { get => _s2InterlayerTypeIndex; set { _s2InterlayerTypeIndex = value; OnPropertyChanged(); } }

    // 尺寸与支承
    private int _s2SupportTypeIndex = 0;
    public int S2SupportTypeIndex { get => _s2SupportTypeIndex; set { _s2SupportTypeIndex = value; OnPropertyChanged(); } }

    // 验算结果表
    public ObservableCollection<CheckResultRow> S2CheckResults { get; } = new()
    {
        new CheckResultRow { Item = "1# 玻璃强度", CalcValue = "0.0 MPa", LimitValue = "28.0 MPa", Verdict = "—" },
        new CheckResultRow { Item = "2# 玻璃强度", CalcValue = "0.0 MPa", LimitValue = "28.0 MPa", Verdict = "—" },
        new CheckResultRow { Item = "挠度", CalcValue = "0.00 mm", LimitValue = "6.11 mm", Verdict = "—" },
    };

    // 玻璃数据库
    private string _s2GlassSearch = "";
    public string S2GlassSearch { get => _s2GlassSearch; set { _s2GlassSearch = value; OnPropertyChanged(); } }
    public ObservableCollection<GlassDbRow> S2GlassDatabase { get; } = new();

    // ==================== Step3 立柱计算 (对齐Blazor Step3Mullion) ====================
    public string Step3Title => $"{CurtainWallTypes[CurtainWallTypeIndex]}幕墙计算 - 立柱计算";

    // 力学模型
    private bool _s3Model1 = true;
    public bool S3Model1 { get => _s3Model1; set { if (value) { _s3Model1 = true; _s3Model2 = _s3Model3 = _s3Model4 = _s3Model5 = _s3Model6 = false; NotifyS3Models(); } } }
    private bool _s3Model2;
    public bool S3Model2 { get => _s3Model2; set { if (value) { _s3Model2 = true; _s3Model1 = _s3Model3 = _s3Model4 = _s3Model5 = _s3Model6 = false; NotifyS3Models(); } } }
    private bool _s3Model3;
    public bool S3Model3 { get => _s3Model3; set { if (value) { _s3Model3 = true; _s3Model1 = _s3Model2 = _s3Model4 = _s3Model5 = _s3Model6 = false; NotifyS3Models(); } } }
    private bool _s3Model4;
    public bool S3Model4 { get => _s3Model4; set { if (value) { _s3Model4 = true; _s3Model1 = _s3Model2 = _s3Model3 = _s3Model5 = _s3Model6 = false; NotifyS3Models(); } } }
    private bool _s3Model5;
    public bool S3Model5 { get => _s3Model5; set { if (value) { _s3Model5 = true; _s3Model1 = _s3Model2 = _s3Model3 = _s3Model4 = _s3Model6 = false; NotifyS3Models(); } } }
    private bool _s3Model6;
    public bool S3Model6 { get => _s3Model6; set { if (value) { _s3Model6 = true; _s3Model1 = _s3Model2 = _s3Model3 = _s3Model4 = _s3Model5 = false; NotifyS3Models(); } } }
    private void NotifyS3Models() { OnPropertyChanged(nameof(S3Model1)); OnPropertyChanged(nameof(S3Model2)); OnPropertyChanged(nameof(S3Model3)); OnPropertyChanged(nameof(S3Model4)); OnPropertyChanged(nameof(S3Model5)); OnPropertyChanged(nameof(S3Model6)); OnPropertyChanged(nameof(S3ModelHint)); }
    public string S3ModelHint => S3Model1 ? "单跨受拉：幕墙立柱每层一处连接件与主体结构连接" : S3Model2 ? "单跨受压：尽量避免采用此模型" : S3Model3 ? "连续梁：要求连接点具有足够刚度" : S3Model4 ? "多跨铰接静定梁" : S3Model5 ? "双跨梁：立柱每层有两处转接件" : S3Model6 ? "多跨超静定" : "";

    // 跨度
    private double _s3Span = 3600;
    public double S3Span { get => _s3Span; set { _s3Span = value; OnPropertyChanged(); } }
    private double _s3ShortSpan = 1500;
    public double S3ShortSpan { get => _s3ShortSpan; set { _s3ShortSpan = value; OnPropertyChanged(); } }

    // 挠度规范
    private int _s3DeflStdIndex = 0;
    public int S3DeflStdIndex { get => _s3DeflStdIndex; set { _s3DeflStdIndex = value; OnPropertyChanged(); } }
    private bool _s3AbsoluteSpan;
    public bool S3AbsoluteSpan { get => _s3AbsoluteSpan; set { _s3AbsoluteSpan = value; OnPropertyChanged(); } }

    // 玻璃配置
    private int _s3OuterGlassIdx = 3;
    public int S3OuterGlassIdx { get => _s3OuterGlassIdx; set { _s3OuterGlassIdx = value; OnPropertyChanged(); } }
    private int _s3InnerGlassIdx = 3;
    public int S3InnerGlassIdx { get => _s3InnerGlassIdx; set { _s3InnerGlassIdx = value; OnPropertyChanged(); } }

    // 材质选择
    private bool _s3Alu6063T5 = true;
    public bool S3Alu6063T5 { get => _s3Alu6063T5; set { _s3Alu6063T5 = value; OnPropertyChanged(); } }
    private bool _s3Alu6063T6; public bool S3Alu6063T6 { get => _s3Alu6063T6; set { _s3Alu6063T6 = value; OnPropertyChanged(); } }
    private bool _s3Alu6061T4; public bool S3Alu6061T4 { get => _s3Alu6061T4; set { _s3Alu6061T4 = value; OnPropertyChanged(); } }
    private bool _s3Alu6061T6; public bool S3Alu6061T6 { get => _s3Alu6061T6; set { _s3Alu6061T6 = value; OnPropertyChanged(); } }
    private bool _s3Alu6063AT5; public bool S3Alu6063AT5 { get => _s3Alu6063AT5; set { _s3Alu6063AT5 = value; OnPropertyChanged(); } }
    private bool _s3Alu6063AT6; public bool S3Alu6063AT6 { get => _s3Alu6063AT6; set { _s3Alu6063AT6 = value; OnPropertyChanged(); } }
    private bool _s3Q235; public bool S3Q235 { get => _s3Q235; set { _s3Q235 = value; OnPropertyChanged(); } }
    private bool _s3Q355; public bool S3Q355 { get => _s3Q355; set { _s3Q355 = value; OnPropertyChanged(); } }
    private bool _s3OtherMat; public bool S3OtherMat { get => _s3OtherMat; set { _s3OtherMat = value; OnPropertyChanged(); } }
    private bool _s3ThermalBreak; public bool S3ThermalBreak { get => _s3ThermalBreak; set { _s3ThermalBreak = value; OnPropertyChanged(); } }
    private bool _s3ColdFormed; public bool S3ColdFormed { get => _s3ColdFormed; set { _s3ColdFormed = value; OnPropertyChanged(); } }
    private bool _s3SteelAlum; public bool S3SteelAlum { get => _s3SteelAlum; set { _s3SteelAlum = value; OnPropertyChanged(); } }

    // 型材表格
    private string _s3ProfileSearch = "";
    public string S3ProfileSearch { get => _s3ProfileSearch; set { _s3ProfileSearch = value; OnPropertyChanged(); } }
    public ObservableCollection<ProfileRow> S3ProfileList { get; } = new();
    private ProfileRow? _s3SelectedProfile;
    public ProfileRow? S3SelectedProfile { get => _s3SelectedProfile; set { _s3SelectedProfile = value; OnPropertyChanged(); } }

    // 验算结果
    private string _s3WindLoadStd = "1.000";
    public string S3WindLoadStd { get => _s3WindLoadStd; set { _s3WindLoadStd = value; OnPropertyChanged(); } }
    private string _s3MomentRequired = "0";
    public string S3MomentRequired { get => _s3MomentRequired; set { _s3MomentRequired = value; OnPropertyChanged(); } }
    private string _s3Wx1Val = "0";
    public string S3Wx1Val { get => _s3Wx1Val; set { _s3Wx1Val = value; OnPropertyChanged(); } }
    private string _s3BendingMomentVerdict = "—";
    public string S3BendingMomentVerdict { get => _s3BendingMomentVerdict; set { _s3BendingMomentVerdict = value; OnPropertyChanged(); } }
    private string _s3InertiaRequired = "0";
    public string S3InertiaRequired { get => _s3InertiaRequired; set { _s3InertiaRequired = value; OnPropertyChanged(); } }
    private string _s3IxVal = "0";
    public string S3IxVal { get => _s3IxVal; set { _s3IxVal = value; OnPropertyChanged(); } }
    private string _s3InertiaVerdict = "—";
    public string S3InertiaVerdict { get => _s3InertiaVerdict; set { _s3InertiaVerdict = value; OnPropertyChanged(); } }
    private string _s3ActualDeflection = "0.000";
    public string S3ActualDeflection { get => _s3ActualDeflection; set { _s3ActualDeflection = value; OnPropertyChanged(); } }
    private string _s3DeflectionLimit = "0.000";
    public string S3DeflectionLimit { get => _s3DeflectionLimit; set { _s3DeflectionLimit = value; OnPropertyChanged(); } }
    private string _s3DeflectionVerdict = "—";
    public string S3DeflectionVerdict { get => _s3DeflectionVerdict; set { _s3DeflectionVerdict = value; OnPropertyChanged(); } }
    private string _s3BendingStress = "0.000";
    public string S3BendingStress { get => _s3BendingStress; set { _s3BendingStress = value; OnPropertyChanged(); } }
    private string _s3AllowableStress = "85";
    public string S3AllowableStress { get => _s3AllowableStress; set { _s3AllowableStress = value; OnPropertyChanged(); } }
    private string _s3BendingVerdict = "—";
    public string S3BendingVerdict { get => _s3BendingVerdict; set { _s3BendingVerdict = value; OnPropertyChanged(); } }
    private string _s3ShearStress = "0.000";
    public string S3ShearStress { get => _s3ShearStress; set { _s3ShearStress = value; OnPropertyChanged(); } }
    private string _s3AllowableShear = "50";
    public string S3AllowableShear { get => _s3AllowableShear; set { _s3AllowableShear = value; OnPropertyChanged(); } }
    private string _s3ShearVerdict = "—";
    public string S3ShearVerdict { get => _s3ShearVerdict; set { _s3ShearVerdict = value; OnPropertyChanged(); } }
    private string _s3ReactionForce = "0.000";
    public string S3ReactionForce { get => _s3ReactionForce; set { _s3ReactionForce = value; OnPropertyChanged(); } }

    // ==================== Step5 连接件验算 (对齐Blazor Step5Connection) ====================
    public string Step5Title => $"{CurtainWallTypes[CurtainWallTypeIndex]}幕墙计算 - 连接件验算";

    // 连接方式
    private int _s5MullionToMainIdx;
    public int S5MullionToMainIdx { get => _s5MullionToMainIdx; set { _s5MullionToMainIdx = value; OnPropertyChanged(); } }
    public List<string> S5MullionToMainOptions { get; } = new() { "螺栓连接", "焊接" };
    private int _s5TransomToMullionIdx;
    public int S5TransomToMullionIdx { get => _s5TransomToMullionIdx; set { _s5TransomToMullionIdx = value; OnPropertyChanged(); OnPropertyChanged(nameof(S5TransomIsBolt)); OnPropertyChanged(nameof(S5TransomIsWeld)); OnPropertyChanged(nameof(S5TransomIsHalf)); } }
    public List<string> S5TransomToMullionOptions { get; } = new() { "螺栓连接", "焊接", "半焊接半栓接" };
    public bool S5TransomIsBolt => S5TransomToMullionIdx == 0 || S5TransomToMullionIdx == 2;
    public bool S5TransomIsWeld => S5TransomToMullionIdx == 1 || S5TransomToMullionIdx == 2;
    public bool S5TransomIsHalf => S5TransomToMullionIdx == 2;
    public bool S5MainIsBolt => S5MullionToMainIdx == 0;
    public bool S5MainIsWeld => S5MullionToMainIdx == 1;

    // 横框与立柱-角片参数
    private int _s5AngleMaterialIdx;
    public int S5AngleMaterialIdx { get => _s5AngleMaterialIdx; set { _s5AngleMaterialIdx = value; OnPropertyChanged(); } }
    public List<string> S5AngleMaterials { get; } = new() { "6063-T5", "6063-T6", "6063A-T5", "6063A-T6", "6061-T4", "6061-T6" };
    private double _s5AngleThickness = 3.0;
    public double S5AngleThickness { get => _s5AngleThickness; set { _s5AngleThickness = value; OnPropertyChanged(); } }
    private int _s5MullionBoltCount = 2;
    public int S5MullionBoltCount { get => _s5MullionBoltCount; set { _s5MullionBoltCount = value; OnPropertyChanged(); } }
    private int _s5TransomBoltCount = 2;
    public int S5TransomBoltCount { get => _s5TransomBoltCount; set { _s5TransomBoltCount = value; OnPropertyChanged(); } }
    // 横框焊缝参数
    private double _s5TransomWeldHeight = 4;
    public double S5TransomWeldHeight { get => _s5TransomWeldHeight; set { _s5TransomWeldHeight = value; OnPropertyChanged(); } }
    private double _s5TransomWeldLength = 60;
    public double S5TransomWeldLength { get => _s5TransomWeldLength; set { _s5TransomWeldLength = value; OnPropertyChanged(); } }
    // 立柱与主体
    private int _s5MainAngleMaterialIdx;
    public int S5MainAngleMaterialIdx { get => _s5MainAngleMaterialIdx; set { _s5MainAngleMaterialIdx = value; OnPropertyChanged(); } }
    public List<string> S5MainAngleMaterials { get; } = new() { "Q235", "Q355" };
    private double _s5MainAngleThickness = 6;
    public double S5MainAngleThickness { get => _s5MainAngleThickness; set { _s5MainAngleThickness = value; OnPropertyChanged(); } }
    private int _s5MainMullionBoltCount = 2;
    public int S5MainMullionBoltCount { get => _s5MainMullionBoltCount; set { _s5MainMullionBoltCount = value; OnPropertyChanged(); } }
    private int _s5ContactSurfaces = 1;
    public int S5ContactSurfaces { get => _s5ContactSurfaces; set { _s5ContactSurfaces = value; OnPropertyChanged(); } }
    private bool _s5SingleSideBracket;
    public bool S5SingleSideBracket { get => _s5SingleSideBracket; set { _s5SingleSideBracket = value; OnPropertyChanged(); } }
    private bool _s5SingleSideAdapter;
    public bool S5SingleSideAdapter { get => _s5SingleSideAdapter; set { _s5SingleSideAdapter = value; OnPropertyChanged(); } }
    private double _s5MainWeldHeight = 6;
    public double S5MainWeldHeight { get => _s5MainWeldHeight; set { _s5MainWeldHeight = value; OnPropertyChanged(); } }
    private double _s5MainWeldLength = 80;
    public double S5MainWeldLength { get => _s5MainWeldLength; set { _s5MainWeldLength = value; OnPropertyChanged(); } }
    // 验算结果
    private string _s5TransomBoltVerdict = "✅通过";
    public string S5TransomBoltVerdict { get => _s5TransomBoltVerdict; set { _s5TransomBoltVerdict = value; OnPropertyChanged(); } }
    private string _s5TransomUtilization = "0.0%";
    public string S5TransomUtilization { get => _s5TransomUtilization; set { _s5TransomUtilization = value; OnPropertyChanged(); } }
    private string _s5MainBoltVerdict = "✅通过";
    public string S5MainBoltVerdict { get => _s5MainBoltVerdict; set { _s5MainBoltVerdict = value; OnPropertyChanged(); } }
    private string _s5MainUtilization = "0.0%";
    public string S5MainUtilization { get => _s5MainUtilization; set { _s5MainUtilization = value; OnPropertyChanged(); } }

    // ==================== Step6 预埋件 (对齐Blazor Step6Embedded) ====================
    public string Step6Title => $"{CurtainWallTypes[CurtainWallTypeIndex]}幕墙计算 - 预埋件与其他验算";

    // 受力模式
    private int _s6ForceModeIdx = 1;
    public int S6ForceModeIdx { get => _s6ForceModeIdx; set { _s6ForceModeIdx = value; OnPropertyChanged(); OnPropertyChanged(nameof(S6IsHangOrSide)); OnPropertyChanged(nameof(S6IsTopOrBottom)); OnPropertyChanged(nameof(S6ForceModeLabel)); } }
    public List<string> S6ForceModes { get; } = new() { "梁底吊挂", "侧埋", "梁顶埋", "梁底埋" };
    public string S6ForceModeLabel => S6ForceModes[S6ForceModeIdx];
    public bool S6IsHangOrSide => S6ForceModeIdx <= 1;
    public bool S6IsTopOrBottom => S6ForceModeIdx >= 2;
    private double _s6ForceE0 = 50;
    public double S6ForceE0 { get => _s6ForceE0; set { _s6ForceE0 = value; OnPropertyChanged(); } }
    private double _s6ForceE1 = 40;
    public double S6ForceE1 { get => _s6ForceE1; set { _s6ForceE1 = value; OnPropertyChanged(); } }
    private double _s6ForceE2 = 60;
    public double S6ForceE2 { get => _s6ForceE2; set { _s6ForceE2 = value; OnPropertyChanged(); } }
    private double _s6ForceE3 = 40;
    public double S6ForceE3 { get => _s6ForceE3; set { _s6ForceE3 = value; OnPropertyChanged(); } }

    // 埋件类型
    private int _s6EmbedTypeIdx;
    public int S6EmbedTypeIdx { get => _s6EmbedTypeIdx; set { _s6EmbedTypeIdx = value; OnPropertyChanged(); OnPropertyChanged(nameof(S6EmbedTypeLabel)); } }
    public List<string> S6EmbedTypes { get; } = new() { "土建预埋", "后补锚栓", "槽型埋件", "对穿螺栓" };
    public string S6EmbedTypeLabel => S6EmbedTypes[S6EmbedTypeIdx];

    // 锚筋参数 (土建预埋)
    private int _s6AnchorMaterialIdx;
    public int S6AnchorMaterialIdx { get => _s6AnchorMaterialIdx; set { _s6AnchorMaterialIdx = value; OnPropertyChanged(); } }
    public List<string> S6AnchorMaterials { get; } = new() { "HPB300", "HRB335", "HRB400" };
    private int _s6AnchorDiameterIdx = 2;
    public int S6AnchorDiameterIdx { get => _s6AnchorDiameterIdx; set { _s6AnchorDiameterIdx = value; OnPropertyChanged(); } }
    public List<string> S6AnchorDiameters { get; } = new() { "8", "10", "12", "14", "16" };
    private double _s6AnchorLength = 100;
    public double S6AnchorLength { get => _s6AnchorLength; set { _s6AnchorLength = value; OnPropertyChanged(); } }
    private int _s6AnchorCount = 4;
    public int S6AnchorCount { get => _s6AnchorCount; set { _s6AnchorCount = value; OnPropertyChanged(); } }
    private int _s6AnchorPerSide = 2;
    public int S6AnchorPerSide { get => _s6AnchorPerSide; set { _s6AnchorPerSide = value; OnPropertyChanged(); } }
    private int _s6ConcreteGradeIdx = 2;
    public int S6ConcreteGradeIdx { get => _s6ConcreteGradeIdx; set { _s6ConcreteGradeIdx = value; OnPropertyChanged(); } }
    public List<string> S6ConcreteGrades { get; } = new() { "C20","C25","C30","C35","C40","C45","C50","C55","C60" };
    private double _s6AnchorCenterDist = 80;
    public double S6AnchorCenterDist { get => _s6AnchorCenterDist; set { _s6AnchorCenterDist = value; OnPropertyChanged(); } }
    private bool _s6AnchorLHook = true;
    public bool S6AnchorLHook { get => _s6AnchorLHook; set { _s6AnchorLHook = value; OnPropertyChanged(); } }
    private bool _s6AnchorTriWeld;
    public bool S6AnchorTriWeld { get => _s6AnchorTriWeld; set { _s6AnchorTriWeld = value; OnPropertyChanged(); } }
    private bool _s6HasMechanicalAnchor;
    public bool S6HasMechanicalAnchor { get => _s6HasMechanicalAnchor; set { _s6HasMechanicalAnchor = value; OnPropertyChanged(); } }
    // 锚板
    private double _s6PlateLength = 150;
    public double S6PlateLength { get => _s6PlateLength; set { _s6PlateLength = value; OnPropertyChanged(); } }
    private double _s6PlateWidth = 100;
    public double S6PlateWidth { get => _s6PlateWidth; set { _s6PlateWidth = value; OnPropertyChanged(); } }
    private double _s6PlateThickness = 10;
    public double S6PlateThickness { get => _s6PlateThickness; set { _s6PlateThickness = value; OnPropertyChanged(); } }

    // 后补锚栓
    private int _s6PostSpecIdx = 2;
    public int S6PostSpecIdx { get => _s6PostSpecIdx; set { _s6PostSpecIdx = value; OnPropertyChanged(); } }
    public List<string> S6PostSpecs { get; } = new() { "M8","M10","M12","M16","M20" };
    private int _s6PostTypeIdx;
    public int S6PostTypeIdx { get => _s6PostTypeIdx; set { _s6PostTypeIdx = value; OnPropertyChanged(); } }
    public List<string> S6PostTypes { get; } = new() { "扩底型锚栓","扭矩控制膨胀型锚栓","普通化学锚栓","特殊倒锥形化学锚栓(GB50367)","特殊倒锥形化学锚栓(JGJ145)" };
    private int _s6PostMaterialIdx;
    public int S6PostMaterialIdx { get => _s6PostMaterialIdx; set { _s6PostMaterialIdx = value; OnPropertyChanged(); } }
    public List<string> S6PostMaterials { get; } = new() { "碳钢4.8","碳钢5.8","碳钢8.8","不锈钢A2-70" };
    private int _s6PostCount = 4;
    public int S6PostCount { get => _s6PostCount; set { _s6PostCount = value; OnPropertyChanged(); } }
    private double _s6PostDepth = 80;
    public double S6PostDepth { get => _s6PostDepth; set { _s6PostDepth = value; OnPropertyChanged(); } }

    // 对穿螺栓
    private int _s6ThroughSpecIdx = 2;
    public int S6ThroughSpecIdx { get => _s6ThroughSpecIdx; set { _s6ThroughSpecIdx = value; OnPropertyChanged(); } }
    private int _s6ThroughMaterialIdx;
    public int S6ThroughMaterialIdx { get => _s6ThroughMaterialIdx; set { _s6ThroughMaterialIdx = value; OnPropertyChanged(); } }
    public List<string> S6ThroughMaterials { get; } = new() { "碳钢4.8","碳钢8.8","不锈钢A2-70" };
    private int _s6ThroughCount = 4;
    public int S6ThroughCount { get => _s6ThroughCount; set { _s6ThroughCount = value; OnPropertyChanged(); } }
    private double _s6ThroughMinThickness = 6;
    public double S6ThroughMinThickness { get => _s6ThroughMinThickness; set { _s6ThroughMinThickness = value; OnPropertyChanged(); } }

    // 转接件
    private int _s6TransferSpecIdx;
    public int S6TransferSpecIdx { get => _s6TransferSpecIdx; set { _s6TransferSpecIdx = value; OnPropertyChanged(); } }
    public List<string> S6TransferSpecs { get; } = new() { "10#槽钢","12#槽钢","14#槽钢" };
    private int _s6TransferSteelIdx;
    public int S6TransferSteelIdx { get => _s6TransferSteelIdx; set { _s6TransferSteelIdx = value; OnPropertyChanged(); } }
    private bool _s6ChannelSingle = true;
    public bool S6ChannelSingle { get => _s6ChannelSingle; set { _s6ChannelSingle = value; OnPropertyChanged(); } }
    private bool _s6ChannelDouble;
    public bool S6ChannelDouble { get => _s6ChannelDouble; set { _s6ChannelDouble = value; OnPropertyChanged(); } }
    // 焊缝
    private bool _s6WeldL = true;
    public bool S6WeldL { get => _s6WeldL; set { _s6WeldL = value; OnPropertyChanged(); } }
    private bool _s6WeldTri;
    public bool S6WeldTri { get => _s6WeldTri; set { _s6WeldTri = value; OnPropertyChanged(); } }
    private double _s6WeldHeight = 6;
    public double S6WeldHeight { get => _s6WeldHeight; set { _s6WeldHeight = value; OnPropertyChanged(); } }
    private double _s6HorizWeldLen = 80;
    public double S6HorizWeldLen { get => _s6HorizWeldLen; set { _s6HorizWeldLen = value; OnPropertyChanged(); } }
    private double _s6VertWeldLen = 60;
    public double S6VertWeldLen { get => _s6VertWeldLen; set { _s6VertWeldLen = value; OnPropertyChanged(); } }

    // 验算结果
    private string _s6AnchorVerdict = "✅通过";
    public string S6AnchorVerdict { get => _s6AnchorVerdict; set { _s6AnchorVerdict = value; OnPropertyChanged(); } }
    private string _s6AnchorUtilization = "0%";
    public string S6AnchorUtilization { get => _s6AnchorUtilization; set { _s6AnchorUtilization = value; OnPropertyChanged(); } }
    private string _s6WeldVerdict = "✅通过";
    public string S6WeldVerdict { get => _s6WeldVerdict; set { _s6WeldVerdict = value; OnPropertyChanged(); } }
    private string _s6TransferVerdict = "✅通过";
    public string S6TransferVerdict { get => _s6TransferVerdict; set { _s6TransferVerdict = value; OnPropertyChanged(); } }

    // ==================== Step7 胶/紧固 (对齐Blazor Step7Sealant) ====================
    public string Step7Title => $"{CurtainWallTypes[CurtainWallTypeIndex]}幕墙计算 - 胶类、伸缩及紧固计算";

    private int _s7BuildingTypeIdx;
    public int S7BuildingTypeIdx { get => _s7BuildingTypeIdx; set { _s7BuildingTypeIdx = value; OnPropertyChanged(); } }
    public List<string> S7BuildingTypes { get; } = new() { "框架","板柱-剪力墙","框架-剪力墙","框架-核心筒","框筒","筒中筒","剪力墙","框支层","多高层钢结构" };
    private double _s7ExpansionJoint = 15;
    public double S7ExpansionJoint { get => _s7ExpansionJoint; set { _s7ExpansionJoint = value; OnPropertyChanged(); } }
    private bool _s7CalcSubFrame;
    public bool S7CalcSubFrame { get => _s7CalcSubFrame; set { _s7CalcSubFrame = value; OnPropertyChanged(); } }
    private bool _s7NoClampCalc;
    public bool S7NoClampCalc { get => _s7NoClampCalc; set { _s7NoClampCalc = value; OnPropertyChanged(); } }
    private bool _s7OpenStyle;
    public bool S7OpenStyle { get => _s7OpenStyle; set { _s7OpenStyle = value; OnPropertyChanged(); } }
    // 结构胶
    private int _s7FrameSealantThickness = 6;
    public int S7FrameSealantThickness { get => _s7FrameSealantThickness; set { _s7FrameSealantThickness = value; OnPropertyChanged(); } }
    private int _s7FrameSealantWidth = 12;
    public int S7FrameSealantWidth { get => _s7FrameSealantWidth; set { _s7FrameSealantWidth = value; OnPropertyChanged(); } }
    private int _s7HollowSealantThickness = 6;
    public int S7HollowSealantThickness { get => _s7HollowSealantThickness; set { _s7HollowSealantThickness = value; OnPropertyChanged(); } }
    private int _s7HollowSealantWidth = 12;
    public int S7HollowSealantWidth { get => _s7HollowSealantWidth; set { _s7HollowSealantWidth = value; OnPropertyChanged(); } }
    // 耐候胶
    private double _s7WeatherDisplacement = 0.12;
    public double S7WeatherDisplacement { get => _s7WeatherDisplacement; set { _s7WeatherDisplacement = value; OnPropertyChanged(); } }
    private double _s7WeatherGap = 12;
    public double S7WeatherGap { get => _s7WeatherGap; set { _s7WeatherGap = value; OnPropertyChanged(); } }
    // 压板
    private double _s7ClampLength = 100;
    public double S7ClampLength { get => _s7ClampLength; set { _s7ClampLength = value; OnPropertyChanged(); } }
    private double _s7ClampWidth = 30;
    public double S7ClampWidth { get => _s7ClampWidth; set { _s7ClampWidth = value; OnPropertyChanged(); } }
    private double _s7ClampThickness = 3;
    public double S7ClampThickness { get => _s7ClampThickness; set { _s7ClampThickness = value; OnPropertyChanged(); } }
    private double _s7ClampSpacing = 300;
    public double S7ClampSpacing { get => _s7ClampSpacing; set { _s7ClampSpacing = value; OnPropertyChanged(); } }
    private int _s7ClampMaterialIdx;
    public int S7ClampMaterialIdx { get => _s7ClampMaterialIdx; set { _s7ClampMaterialIdx = value; OnPropertyChanged(); } }
    public List<string> S7ClampMaterials { get; } = new() { "6063-T5","6063-T6","6063A-T5","6063A-T6","6061-T4","6061-T6","Q235","304不锈钢" };
    // 验算结果
    private string _s7ExpansionVerdict = "✅通过";
    public string S7ExpansionVerdict { get => _s7ExpansionVerdict; set { _s7ExpansionVerdict = value; OnPropertyChanged(); } }
    private string _s7ExpansionUtil = "0.0%";
    public string S7ExpansionUtil { get => _s7ExpansionUtil; set { _s7ExpansionUtil = value; OnPropertyChanged(); } }
    private string _s7FrameSealantVerdict = "✅通过";
    public string S7FrameSealantVerdict { get => _s7FrameSealantVerdict; set { _s7FrameSealantVerdict = value; OnPropertyChanged(); } }
    private string _s7FrameSealantUtil = "0.0%";
    public string S7FrameSealantUtil { get => _s7FrameSealantUtil; set { _s7FrameSealantUtil = value; OnPropertyChanged(); } }
    private string _s7HollowSealantVerdict = "✅通过";
    public string S7HollowSealantVerdict { get => _s7HollowSealantVerdict; set { _s7HollowSealantVerdict = value; OnPropertyChanged(); } }
    private string _s7HollowSealantUtil = "0.0%";
    public string S7HollowSealantUtil { get => _s7HollowSealantUtil; set { _s7HollowSealantUtil = value; OnPropertyChanged(); } }
    private string _s7WeatherVerdict = "✅通过";
    public string S7WeatherVerdict { get => _s7WeatherVerdict; set { _s7WeatherVerdict = value; OnPropertyChanged(); } }
    private string _s7WeatherUtil = "0.0%";
    public string S7WeatherUtil { get => _s7WeatherUtil; set { _s7WeatherUtil = value; OnPropertyChanged(); } }
    private string _s7ClampVerdict = "✅通过";
    public string S7ClampVerdict { get => _s7ClampVerdict; set { _s7ClampVerdict = value; OnPropertyChanged(); } }
    private string _s7ClampUtil = "0.0%";
    public string S7ClampUtil { get => _s7ClampUtil; set { _s7ClampUtil = value; OnPropertyChanged(); } }
    private string _s7ScrewVerdict = "✅通过";
    public string S7ScrewVerdict { get => _s7ScrewVerdict; set { _s7ScrewVerdict = value; OnPropertyChanged(); } }
    private string _s7ScrewUtil = "0.0%";
    public string S7ScrewUtil { get => _s7ScrewUtil; set { _s7ScrewUtil = value; OnPropertyChanged(); } }

    // ==================== Step8 汇总 (对齐Blazor Step7Summary) ====================
    public ObservableCollection<SummaryCheckRow> SummaryRows { get; } = new();

    public void RefreshSummary()
    {
        SummaryRows.Clear();
        // Step2 面板
        SummaryRows.Add(new SummaryCheckRow { Component = "玻璃", Item = "1#强度", Actual = "—", Limit = "—", Verdict = "—" });
        SummaryRows.Add(new SummaryCheckRow { Component = "玻璃", Item = "2#强度", Actual = "—", Limit = "—", Verdict = "—" });
        SummaryRows.Add(new SummaryCheckRow { Component = "玻璃", Item = "挠度", Actual = "—", Limit = "—", Verdict = "—" });
        // Step3 立柱
        SummaryRows.Add(new SummaryCheckRow { Component = "立柱", Item = "抗弯强度", Actual = S3BendingStress + " MPa", Limit = S3AllowableStress + " MPa", Verdict = S3BendingVerdict });
        SummaryRows.Add(new SummaryCheckRow { Component = "立柱", Item = "挠度", Actual = S3ActualDeflection + " mm", Limit = S3DeflectionLimit + " mm", Verdict = S3DeflectionVerdict });
        SummaryRows.Add(new SummaryCheckRow { Component = "立柱", Item = "抗剪强度", Actual = S3ShearStress + " MPa", Limit = S3AllowableShear + " MPa", Verdict = S3ShearVerdict });
        // Step4 横梁
        SummaryRows.Add(new SummaryCheckRow { Component = "横梁", Item = "双向弯曲", Actual = S4CombinedStress + " MPa", Limit = S4AllowableStress + " MPa", Verdict = S4StressVerdict });
        SummaryRows.Add(new SummaryCheckRow { Component = "横梁", Item = "合成挠度", Actual = S4CombinedDeflection + " mm", Limit = S4DeflectionLimit + " mm", Verdict = S4DeflectionVerdict });
        // Step5 连接
        SummaryRows.Add(new SummaryCheckRow { Component = "横框与立柱", Item = "螺栓/焊缝", Actual = S5TransomUtilization, Limit = "—", Verdict = S5TransomBoltVerdict });
        SummaryRows.Add(new SummaryCheckRow { Component = "立柱与主体", Item = "螺栓/焊缝", Actual = S5MainUtilization, Limit = "—", Verdict = S5MainBoltVerdict });
        // Step6 埋件
        SummaryRows.Add(new SummaryCheckRow { Component = "预埋件", Item = "锚筋验算", Actual = S6AnchorUtilization, Limit = "—", Verdict = S6AnchorVerdict });
        SummaryRows.Add(new SummaryCheckRow { Component = "转接件", Item = "槽钢强度", Actual = "—", Limit = "—", Verdict = S6TransferVerdict });
        // Step7 胶类
        SummaryRows.Add(new SummaryCheckRow { Component = "铝框结构胶", Item = "强度验算", Actual = S7FrameSealantUtil, Limit = "≤100%", Verdict = S7FrameSealantVerdict });
        SummaryRows.Add(new SummaryCheckRow { Component = "中空层结构胶", Item = "强度验算", Actual = S7HollowSealantUtil, Limit = "≤100%", Verdict = S7HollowSealantVerdict });
        SummaryRows.Add(new SummaryCheckRow { Component = "耐候胶", Item = "胶缝验算", Actual = S7WeatherUtil, Limit = "≤100%", Verdict = S7WeatherVerdict });
        SummaryRows.Add(new SummaryCheckRow { Component = "压板", Item = "弯曲验算", Actual = S7ClampUtil, Limit = "≤100%", Verdict = S7ClampVerdict });
        SummaryRows.Add(new SummaryCheckRow { Component = "伸缩缝", Item = "热胀验算", Actual = S7ExpansionUtil, Limit = "≤100%", Verdict = S7ExpansionVerdict });
    }

    // ==================== Step4 横梁计算 (对齐Blazor Step4Transom) ====================
    public string Step4Title => $"{CurtainWallTypes[CurtainWallTypeIndex]}幕墙计算 - 横梁计算";

    // 横梁参数
    private double _s4Span = 1200;
    public double S4Span { get => _s4Span; set { _s4Span = value; OnPropertyChanged(); } }
    private bool _s4LoadTriangle = true;
    public bool S4LoadTriangle { get => _s4LoadTriangle; set { _s4LoadTriangle = value; OnPropertyChanged(); if (value) { _s4LoadUniform = false; OnPropertyChanged(nameof(S4LoadUniform)); } } }
    private bool _s4LoadUniform;
    public bool S4LoadUniform { get => _s4LoadUniform; set { _s4LoadUniform = value; OnPropertyChanged(); if (value) { _s4LoadTriangle = false; OnPropertyChanged(nameof(S4LoadTriangle)); } } }

    // 材质选择
    private bool _s4Alu6063T5 = true;
    public bool S4Alu6063T5 { get => _s4Alu6063T5; set { _s4Alu6063T5 = value; OnPropertyChanged(); } }
    private bool _s4Alu6063T6; public bool S4Alu6063T6 { get => _s4Alu6063T6; set { _s4Alu6063T6 = value; OnPropertyChanged(); } }
    private bool _s4Alu6061T4; public bool S4Alu6061T4 { get => _s4Alu6061T4; set { _s4Alu6061T4 = value; OnPropertyChanged(); } }
    private bool _s4Alu6061T6; public bool S4Alu6061T6 { get => _s4Alu6061T6; set { _s4Alu6061T6 = value; OnPropertyChanged(); } }
    private bool _s4Alu6063AT5; public bool S4Alu6063AT5 { get => _s4Alu6063AT5; set { _s4Alu6063AT5 = value; OnPropertyChanged(); } }
    private bool _s4Alu6063AT6; public bool S4Alu6063AT6 { get => _s4Alu6063AT6; set { _s4Alu6063AT6 = value; OnPropertyChanged(); } }
    private bool _s4Q235; public bool S4Q235 { get => _s4Q235; set { _s4Q235 = value; OnPropertyChanged(); } }
    private bool _s4Q355; public bool S4Q355 { get => _s4Q355; set { _s4Q355 = value; OnPropertyChanged(); } }
    private bool _s4OtherMat; public bool S4OtherMat { get => _s4OtherMat; set { _s4OtherMat = value; OnPropertyChanged(); } }
    private bool _s4ThermalBreak; public bool S4ThermalBreak { get => _s4ThermalBreak; set { _s4ThermalBreak = value; OnPropertyChanged(); } }
    private bool _s4ColdFormed; public bool S4ColdFormed { get => _s4ColdFormed; set { _s4ColdFormed = value; OnPropertyChanged(); } }

    // 横梁选项
    private bool _s4SteelAlum; public bool S4SteelAlum { get => _s4SteelAlum; set { _s4SteelAlum = value; OnPropertyChanged(); } }
    private bool _s4Hanging; public bool S4Hanging { get => _s4Hanging; set { _s4Hanging = value; OnPropertyChanged(); } }
    private bool _s4GlassConcentrated; public bool S4GlassConcentrated { get => _s4GlassConcentrated; set { _s4GlassConcentrated = value; OnPropertyChanged(); } }
    private bool _s4ConcentratedModel; public bool S4ConcentratedModel { get => _s4ConcentratedModel; set { _s4ConcentratedModel = value; OnPropertyChanged(); } }
    private bool _s4CalcTorsion; public bool S4CalcTorsion { get => _s4CalcTorsion; set { _s4CalcTorsion = value; OnPropertyChanged(); } }
    private double _s4ForcePointDist = 50;
    public double S4ForcePointDist { get => _s4ForcePointDist; set { _s4ForcePointDist = value; OnPropertyChanged(); } }
    private bool _s4ShortCantilever; public bool S4ShortCantilever { get => _s4ShortCantilever; set { _s4ShortCantilever = value; OnPropertyChanged(); } }
    private double _s4CantileverSize = 200;
    public double S4CantileverSize { get => _s4CantileverSize; set { _s4CantileverSize = value; OnPropertyChanged(); } }

    // 型材表格
    private string _s4ProfileSearch = "";
    public string S4ProfileSearch { get => _s4ProfileSearch; set { _s4ProfileSearch = value; OnPropertyChanged(); } }
    public ObservableCollection<ProfileRow> S4ProfileList { get; } = new();
    private ProfileRow? _s4SelectedProfile;
    public ProfileRow? S4SelectedProfile { get => _s4SelectedProfile; set { _s4SelectedProfile = value; OnPropertyChanged(); } }

    // 验算结果
    private string _s4CombinedStress = "0.0";
    public string S4CombinedStress { get => _s4CombinedStress; set { _s4CombinedStress = value; OnPropertyChanged(); } }
    private string _s4AllowableStress = "85.0";
    public string S4AllowableStress { get => _s4AllowableStress; set { _s4AllowableStress = value; OnPropertyChanged(); } }
    private string _s4StressVerdict = "—";
    public string S4StressVerdict { get => _s4StressVerdict; set { _s4StressVerdict = value; OnPropertyChanged(); } }
    private string _s4CombinedDeflection = "0.00";
    public string S4CombinedDeflection { get => _s4CombinedDeflection; set { _s4CombinedDeflection = value; OnPropertyChanged(); } }
    private string _s4DeflectionLimit = "0.00";
    public string S4DeflectionLimit { get => _s4DeflectionLimit; set { _s4DeflectionLimit = value; OnPropertyChanged(); } }
    private string _s4DeflectionVerdict = "—";
    public string S4DeflectionVerdict { get => _s4DeflectionVerdict; set { _s4DeflectionVerdict = value; OnPropertyChanged(); } }
    private string _s4ReactionForce = "0.000";
    public string S4ReactionForce { get => _s4ReactionForce; set { _s4ReactionForce = value; OnPropertyChanged(); } }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// ==================== 地区数据行VM ====================

public class RegionRowVM : INotifyPropertyChanged
{
#pragma warning disable CS0067
    public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067
    public string Name { get; set; } = "";
    public double WindPressure50 { get; set; }
    public double WindPressure100 { get; set; }
    public int SeismicIntensity { get; set; }
    public double PeakAcceleration { get; set; }
    public double AlphaMax { get; set; }
    public double TemperatureDiff { get; set; }
    public double SnowPressure50 { get; set; }
    public double SnowPressure100 { get; set; }
}

// ==================== 夹胶配置子VM ====================

public class LamSheetVM : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public int Order { get; set; }
    public string PositionLabel { get; set; } = string.Empty;
    private int _thicknessIndex = 1;
    public int ThicknessIndex { get => _thicknessIndex; set { _thicknessIndex = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThicknessIndex))); } }
    private int _temperedIndex = 2;
    public int TemperedIndex { get => _temperedIndex; set { _temperedIndex = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TemperedIndex))); } }
}

public class LamLayerVM : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public int Order { get; set; }
    private int _filmTypeIndex = 0;
    public int FilmTypeIndex { get => _filmTypeIndex; set { _filmTypeIndex = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FilmTypeIndex))); } }
    private int _filmThicknessIndex = 2;
    public int FilmThicknessIndex { get => _filmThicknessIndex; set { _filmThicknessIndex = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FilmThicknessIndex))); } }
}

// ==================== DTOs ====================

public class DataItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Remark { get; set; }
    public bool IsActive { get; set; } = true;
}

public class StepDto
{
    public int StepNo { get; set; }
    public string Name { get; set; } = "";
    public string Formula { get; set; } = "";
    public string Process { get; set; } = "";
    public string Result { get; set; } = "";
}

public class CalcResultDto
{
    public bool Passed { get; set; }
    public double MaxStress { get; set; }
    public double MaxDeflection { get; set; }
    public double DeflectionLimit { get; set; }
    public string Standard { get; set; } = "";
    public List<StepDto>? Steps { get; set; }
}

// ==================== Step2/3/4 表格行VM ====================

public class CheckResultRow
{
    public string Item { get; set; } = "";
    public string CalcValue { get; set; } = "";
    public string LimitValue { get; set; } = "";
    public string Verdict { get; set; } = "";
}

public class GlassDbRow
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Thickness { get; set; } = "";
    public string DesignStrength { get; set; } = "";
    public string ElasticModulus { get; set; } = "";
}

public class ProfileRow : INotifyPropertyChanged
{
#pragma warning disable CS0067
    public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067
    public string Code { get; set; } = "";
    public string Ix { get; set; } = "";
    public string Iy { get; set; } = "";
    public string Wx1 { get; set; } = "";
    public string Wx2 { get; set; } = "";
    public string Wy1 { get; set; } = "";
    public string Wy2 { get; set; } = "";
    public string WallThickness { get; set; } = "";
    public string Area { get; set; } = "";
}

public class SummaryCheckRow
{
    public string Component { get; set; } = "";
    public string Item { get; set; } = "";
    public string Actual { get; set; } = "";
    public string Limit { get; set; } = "";
    public string Verdict { get; set; } = "";
}

/// <summary>通用计算结果DTO，兼容所有计算接口</summary>
public class GenericCalcResult
{
    public bool Passed { get; set; }
    public string Standard { get; set; } = "";
    public List<StepDto>? Steps { get; set; }
}
