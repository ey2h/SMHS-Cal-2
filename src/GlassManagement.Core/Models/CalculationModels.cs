using System.ComponentModel.DataAnnotations;

namespace GlassManagement.Core.Models;

#region ===== 铝合金计算 =====

/// <summary>铝合金横梁计算请求</summary>
public class AluminumRafterCalculationRequest
{
    /// <summary>横梁跨度 B (mm)</summary>
    [Required] public double Span { get; set; }

    /// <summary>横梁计算间距 H (mm)，即上下分格高</summary>
    [Required] public double Spacing { get; set; }

    /// <summary>型材系列 (如 "40/60系列")</summary>
    [Required] public string ProfileSpec { get; set; } = string.Empty;

    /// <summary>绕X轴惯性矩 Ix (mm⁴)</summary>
    public double? Ix { get; set; }

    /// <summary>绕Y轴惯性矩 Iy (mm⁴)</summary>
    public double? Iy { get; set; }

    /// <summary>绕X轴净截面抵抗矩 Wnx (mm³)，取较小值</summary>
    public double? Wnx { get; set; }

    /// <summary>绕Y轴净截面抵抗矩 Wny (mm³)，取较小值</summary>
    public double? Wny { get; set; }

    /// <summary>截面垂直于X轴腹板的截面总宽度 tx (mm)</summary>
    public double Tx { get; set; } = 5;

    /// <summary>截面垂直于Y轴腹板的截面总宽度 ty (mm)</summary>
    public double Ty { get; set; } = 3;

    /// <summary>型材受力面对中性轴的面积矩(绕X轴) Sx (mm³)</summary>
    public double? Sx { get; set; }

    /// <summary>型材受力面对中性轴的面积矩(绕Y轴) Sy (mm³)</summary>
    public double? Sy { get; set; }

    /// <summary>净截面面积 An (mm²)</summary>
    public double? An { get; set; }

    /// <summary>风荷载标准值 Wk (MPa)，支承结构用</summary>
    [Required] public double WindLoad { get; set; }

    /// <summary>面板风荷载标准值 (MPa)，用于计算地震</summary>
    public double PanelWindLoad { get; set; }

    /// <summary>自重荷载面密度 (MPa)，玻璃+附件</summary>
    public double GravityLoad { get; set; } = 0.0004;

    /// <summary>横梁自重荷载作用高度 H1 (mm)，挂式取下分格高</summary>
    public double GravityHeight { get; set; }

    /// <summary>铝合金牌号 (6061-T6 / 6063-T5 等)</summary>
    public string AluminumGrade { get; set; } = "6063-T5";

    /// <summary>动力放大系数 βE</summary>
    public double BetaE { get; set; } = 5.0;

    /// <summary>水平地震影响系数最大值 αmax</summary>
    public double AlphaMax { get; set; } = 0.16;

    /// <summary>塑性发展系数 γ</summary>
    public double Gamma { get; set; } = 1.0;

    // 兼容旧接口
    public double? MomentOfInertia { get; set; }
    public double? SectionModulus { get; set; }
    public double SelfWeight { get; set; }
    public double GlassWeight { get; set; }
}

/// <summary>铝合金立柱计算请求</summary>
public class AluminumMullionCalculationRequest
{
    /// <summary>立柱跨度 L (mm)</summary>
    [Required] public double Height { get; set; }

    /// <summary>短跨长 L1 (mm)，双跨梁模型</summary>
    [Required] public double ShortSpan { get; set; }

    /// <summary>长跨长 L2 (mm)，双跨梁模型</summary>
    [Required] public double LongSpan { get; set; }

    /// <summary>立柱计算间距 B (mm)</summary>
    [Required] public double Spacing { get; set; }

    /// <summary>型材系列</summary>
    [Required] public string ProfileSpec { get; set; } = string.Empty;

    /// <summary>绕X轴惯性矩 Ix (mm⁴)</summary>
    public double? Ix { get; set; }

    /// <summary>绕Y轴惯性矩 Iy (mm⁴)</summary>
    public double? Iy { get; set; }

    /// <summary>绕X轴净截面抵抗矩 Wnx (mm³)，取较小值</summary>
    public double? Wnx { get; set; }

    /// <summary>净截面面积 An (mm²)</summary>
    public double? An { get; set; }

    /// <summary>型材截面垂直于X轴腹板的截面总宽度 t (mm)</summary>
    public double WebThickness { get; set; } = 6;

    /// <summary>型材受力面对中性轴的面积矩 Sx (mm³)</summary>
    public double? Sx { get; set; }

    /// <summary>风荷载标准值 Wk (MPa)，支承结构用</summary>
    [Required] public double WindLoad { get; set; }

    /// <summary>幕墙单位面积自重标准值 (MPa)</summary>
    public double GravityLoad { get; set; } = 0.0005;

    /// <summary>铝合金牌号</summary>
    public string AluminumGrade { get; set; } = "6063-T5";

    /// <summary>安装方式 (偏心受拉 / 偏心受压)</summary>
    public string InstallationType { get; set; } = "偏心受拉";

    /// <summary>动力放大系数 βE</summary>
    public double BetaE { get; set; } = 5.0;

    /// <summary>水平地震影响系数最大值 αmax</summary>
    public double AlphaMax { get; set; } = 0.16;

    /// <summary>地震作用面密度 (MPa)</summary>
    public double SeismicGravityLoad { get; set; } = 0.0005;

    /// <summary>塑性发展系数 γ</summary>
    public double Gamma { get; set; } = 1.0;

    // 兼容旧接口
    public double? MomentOfInertia { get; set; }
    public double? SectionModulus { get; set; }
    public double FloorHeight { get; set; }
    public string ConnectionType { get; set; } = "Fixed";
}

/// <summary>铝板幕墙计算请求</summary>
public class AluminumPlateCalculationRequest
{
    /// <summary>铝板短边 (mm)</summary>
    [Required] public double ShortSide { get; set; }

    /// <summary>铝板长边 (mm)</summary>
    [Required] public double LongSide { get; set; }

    /// <summary>铝板厚度 (mm)</summary>
    [Required] public double Thickness { get; set; }

    /// <summary>加劲肋间距 (mm)，0表示无加劲肋</summary>
    public double StiffenerSpacing { get; set; }

    /// <summary>风荷载标准值 Wk (kN/m²)</summary>
    [Required] public double WindLoad { get; set; }

    /// <summary>支撑条件 (FourSides / ThreeSides / TwoSides)</summary>
    public string SupportCondition { get; set; } = "FourSides";

    /// <summary>铝板材质 (3003-H24 / 5052-H32 等)</summary>
    public string MaterialGrade { get; set; } = "3003-H24";
}

/// <summary>铝合金计算结果</summary>
public class AluminumCalculationResult
{
    /// <summary>是否通过验算</summary>
    public bool Passed { get; set; }

    /// <summary>强度验算</summary>
    public CheckResult StrengthCheck { get; set; } = new();

    /// <summary>挠度验算</summary>
    public CheckResult DeflectionCheck { get; set; } = new();

    /// <summary>抗剪验算</summary>
    public CheckResult? ShearCheck { get; set; }

    /// <summary>稳定性验算 (立柱适用)</summary>
    public CheckResult? StabilityCheck { get; set; }

    /// <summary>最大弯矩 (N·mm)</summary>
    public double MaxBendingMoment { get; set; }

    /// <summary>最大挠度 (mm)</summary>
    public double MaxDeflection { get; set; }

    /// <summary>挠度限值 (mm)</summary>
    public double DeflectionLimit { get; set; }

    /// <summary>计算依据</summary>
    public string Standard { get; set; } = "JGJ 102-2003";

    /// <summary>详细计算过程</summary>
    public List<CalculationStep> Steps { get; set; } = new();
}

#endregion

#region ===== 石材计算 =====

/// <summary>石材面板计算请求</summary>
public class StonePanelCalculationRequest
{
    [Required] public double ShortSide { get; set; }
    [Required] public double LongSide { get; set; }
    [Required] public double Thickness { get; set; }
    [Required] public string StoneType { get; set; } = "Granite";
    public double? Density { get; set; }
    public double? FlexuralStrength { get; set; }
    [Required] public double WindLoad { get; set; }
    public string SupportType { get; set; } = "ShortSidePin";
    public int BracketCount { get; set; } = 4;
}

/// <summary>石材挂件计算请求</summary>
public class StoneBracketCalculationRequest
{
    [Required] public double PanelWidth { get; set; }
    [Required] public double PanelHeight { get; set; }
    [Required] public double StoneThickness { get; set; }
    [Required] public double StoneDensity { get; set; }
    [Required] public double WindLoad { get; set; }
    public string BracketType { get; set; } = "SE";
    public string BracketMaterial { get; set; } = "SS304";
    public int BracketCount { get; set; } = 4;
    public double? SlotDepth { get; set; }
    public double? SlotEdgeDistance { get; set; }
}

/// <summary>石材计算结果</summary>
public class StoneCalculationResult
{
    public bool Passed { get; set; }
    public CheckResult FlexuralCheck { get; set; } = new();
    public CheckResult DeflectionCheck { get; set; } = new();
    public CheckResult? BracketCheck { get; set; }
    public CheckResult? SlotCheck { get; set; }
    public double MaxBendingMoment { get; set; }
    public string Standard { get; set; } = "JGJ 133-2001";
    public List<CalculationStep> Steps { get; set; } = new();
}

#endregion

#region ===== 结构计算 =====

/// <summary>预埋件计算请求</summary>
public class EmbedPartCalculationRequest
{
    /// <summary>预埋件类型 (Channel / Plate / Bolt)</summary>
    [Required] public string EmbedType { get; set; } = "Plate";

    /// <summary>锚板长度 (mm)</summary>
    [Required] public double PlateLength { get; set; }

    /// <summary>锚板宽度 (mm)</summary>
    [Required] public double PlateWidth { get; set; }

    /// <summary>锚板厚度 (mm)</summary>
    [Required] public double PlateThickness { get; set; }

    /// <summary>锚筋直径 (mm)</summary>
    [Required] public double AnchorDiameter { get; set; }

    /// <summary>锚筋数量</summary>
    [Required] public int AnchorCount { get; set; }

    /// <summary>锚筋长度 (mm)</summary>
    [Required] public double AnchorLength { get; set; }

    /// <summary>锚筋材质 (HPB300 / HRB400 / HRB500)</summary>
    public string AnchorMaterial { get; set; } = "HPB300";

    /// <summary>混凝土等级 (C20/C25/C30...)</summary>
    [Required] public string ConcreteGrade { get; set; } = "C30";

    /// <summary>拉力设计值 N (N)</summary>
    public double TensionForce { get; set; }

    /// <summary>剪力设计值 V (N)</summary>
    public double ShearForce { get; set; }

    /// <summary>弯矩设计值 M (N·mm)</summary>
    public double BendingMoment { get; set; }

    /// <summary>钢筋层数 (2/3/4)</summary>
    public int RebarLayers { get; set; } = 2;

    /// <summary>沿剪力方向最外层锚筋中心线距离 z (mm)</summary>
    public double AnchorSpacing { get; set; } = 110;

    /// <summary>是否有抗震设计要求</summary>
    public bool HasSeismicDesign { get; set; } = true;

    /// <summary>锚筋外型 (光圆筋 / 带肋筋)</summary>
    public string AnchorShape { get; set; } = "光圆筋";
}

/// <summary>结构胶计算请求</summary>
public class SiliconeCalculationRequest
{
    /// <summary>玻璃分格短边 a (mm)</summary>
    [Required] public double BondLengthShort { get; set; }

    /// <summary>玻璃分格长边 b (mm)</summary>
    [Required] public double BondLengthLong { get; set; }

    /// <summary>胶缝宽度 cs (mm)</summary>
    [Required] public double SealantWidth { get; set; }

    /// <summary>胶缝厚度 ts (mm)</summary>
    [Required] public double SealantThickness { get; set; }

    /// <summary>面板风荷载标准值 wk (MPa)</summary>
    [Required] public double WindLoad { get; set; }

    /// <summary>地震作用标准值 qEAk (MPa)</summary>
    public double SeismicLoad { get; set; }

    /// <summary>玻璃单位面积重力荷载标准值 (MPa)</summary>
    public double GlassWeight { get; set; } = 0.000307;

    /// <summary>结构胶短期强度允许值 f1 (MPa)</summary>
    public double ShortTermStrength { get; set; } = 0.2;

    /// <summary>结构胶长期强度允许值 f2 (MPa)</summary>
    public double LongTermStrength { get; set; } = 0.01;

    /// <summary>年温温差 Δt (℃)</summary>
    public double TemperatureDifference { get; set; } = 49;

    /// <summary>铝型材线膨胀系数</summary>
    public double AluminumExpansionCoeff { get; set; } = 2.3e-5;

    /// <summary>玻璃线膨胀系数</summary>
    public double GlassExpansionCoeff { get; set; } = 1e-5;

    /// <summary>层间位移角限值 θ</summary>
    public double InterStoryDriftAngle { get; set; } = 1.0 / 550.0;

    /// <summary>结构胶变位承受能力 δ (温度)</summary>
    public double ThermalDeformCapacity { get; set; } = 0.1;

    /// <summary>结构胶变位承受能力 δ (风荷载)</summary>
    public double WindDeformCapacity { get; set; } = 0.1;

    /// <summary>隐框/半隐框 (FullHidden / SemiHidden)</summary>
    public string FrameType { get; set; } = "FullHidden";

    public string SiliconeType { get; set; } = "Structural";
    public double DesignStrength { get; set; } = 0.14;
}

/// <summary>预埋件计算结果</summary>
public class EmbedPartCalculationResult
{
    public bool Passed { get; set; }
    public CheckResult TensionCheck { get; set; } = new();
    public CheckResult ShearCheck { get; set; } = new();
    public CheckResult CombinedCheck { get; set; } = new();
    public CheckResult? PlateBendingCheck { get; set; }
    public CheckResult? AnchorLengthCheck { get; set; }
    public string Standard { get; set; } = "JGJ 102-2003";
    public List<CalculationStep> Steps { get; set; } = new();
}

/// <summary>结构胶计算结果</summary>
public class SiliconeCalculationResult
{
    public bool Passed { get; set; }
    public CheckResult WindTensionCheck { get; set; } = new();
    public CheckResult GravityShearCheck { get; set; } = new();
    public CheckResult WidthCheck { get; set; } = new();
    public CheckResult ThicknessCheck { get; set; } = new();
    public CheckResult AspectRatioCheck { get; set; } = new();
    public double RequiredMinWidth { get; set; }
    public double RequiredMinThickness { get; set; }
    public string Standard { get; set; } = "JGJ 102-2003";
    public List<CalculationStep> Steps { get; set; } = new();
}

#endregion

#region ===== 新版玻璃配置模型 (对齐用户架构) =====

// 注：GlassType, TemperedType, FilmType, GasType, PanelCategory 枚举
// 以及递归 GlassBase 类层次结构定义在 GlassBaseModels.cs 中

/// <summary>玻璃配置DTO (扁平化传输)</summary>
public class GlassConfigDto
{
    public PanelCategory PanelCategory { get; set; }

    /// <summary>外片/主片 (非中空时也用这个)</summary>
    public GlassPaneDto OuterPane { get; set; } = new();

    /// <summary>内片 (单中空/双中空)</summary>
    public GlassPaneDto? InnerPane { get; set; }

    /// <summary>中间片 (双中空)</summary>
    public GlassPaneDto? MiddlePane { get; set; }

    /// <summary>第一间隔层 (单中空:外-内; 双中空:外-中)</summary>
    public InsulatingGapDto? FirstGap { get; set; }

    /// <summary>第二间隔层 (双中空:中-内)</summary>
    public InsulatingGapDto? SecondGap { get; set; }

    /// <summary>自动拼接的显示名称</summary>
    public string DisplayName
    {
        get
        {
            if (PanelCategory == PanelCategory.NonInsulating)
                return OuterPane?.DisplayName ?? "";

            if (PanelCategory == PanelCategory.SingleInsulating)
                return $"{OuterPane?.DisplayName}+{FirstGap?.DisplayName}+{InnerPane?.DisplayName}";

            // DoubleInsulating
            return $"{OuterPane?.DisplayName}+{FirstGap?.DisplayName}+{MiddlePane?.DisplayName}+{SecondGap?.DisplayName}+{InnerPane?.DisplayName}";
        }
    }
}

/// <summary>玻璃片DTO (单片或夹胶)</summary>
public class GlassPaneDto
{
    public bool IsLaminated { get; set; }

    // ==== 单片属性 ====
    public int Thickness { get; set; } = 6;
    public TemperedType TemperedType { get; set; } = TemperedType.FullTempered;

    // ==== 夹胶属性 ====
    public List<GlassSheetDto>? LaminatedSheets { get; set; }
    public List<LaminatedLayerDto>? LaminatedLayers { get; set; }

    /// <summary>自动拼接显示名</summary>
    public string DisplayName
    {
        get
        {
            if (!IsLaminated)
                return $"{Thickness}{TemperedTextCN(TemperedType)}";

            if (LaminatedSheets == null || LaminatedSheets.Count == 0)
                return $"{Thickness}{TemperedTextCN(TemperedType)}";

            var parts = new List<string>();
            for (int i = 0; i < LaminatedSheets.Count; i++)
            {
                parts.Add($"{LaminatedSheets[i].Thickness}{TemperedTextCN(LaminatedSheets[i].TemperedType)}");
                if (LaminatedLayers != null && i < LaminatedLayers.Count)
                    parts.Add($"{LaminatedLayers[i].FilmThickness}{LaminatedLayers[i].FilmType}");
            }
            return string.Join("+", parts);
        }
    }

    private static string TemperedTextCN(TemperedType t) => t switch
    {
        TemperedType.Float => "浮法",
        TemperedType.HalfTempered => "半钢化",
        TemperedType.FullTempered => "全钢化",
        _ => ""
    };
}

/// <summary>夹胶中的单片玻璃</summary>
public class GlassSheetDto
{
    public int Thickness { get; set; } = 6;
    public TemperedType TemperedType { get; set; } = TemperedType.FullTempered;
}

/// <summary>夹胶层(胶片)</summary>
public class LaminatedLayerDto
{
    public FilmType FilmType { get; set; } = FilmType.PVB;
    public double FilmThickness { get; set; } = 1.52;
}

/// <summary>中空间隔层</summary>
public class InsulatingGapDto
{
    public int Thickness { get; set; } = 12;
    public GasType GasType { get; set; } = GasType.Air;

    public string DisplayName => $"{Thickness}{GasType}";
}

/// <summary>统一玻璃计算请求 (新API，支持 GlassConfigDto 扁平传输)</summary>
public class UnifiedGlassCalculationRequest
{
    [Required] public double ShortSide { get; set; }
    [Required] public double LongSide { get; set; }
    [Required] public GlassConfigDto GlassConfig { get; set; } = new();
    [Required] public double WindLoad { get; set; }
    public double AltitudeDifference { get; set; }
    public double TemperatureDifference { get; set; }
    public double LoadDuration { get; set; } = 3;
    public string SupportCondition { get; set; } = "FourSides";
}

/// <summary>统一玻璃计算请求 (递归树版本，支持 GlassBase 多态传输)</summary>
public class UnifiedGlassCalculationRequestV2
{
    [Required] public double ShortSide { get; set; }
    [Required] public double LongSide { get; set; }

    /// <summary>递归玻璃组合配置 (使用 $type discriminator)</summary>
    [Required] public GlassBase Glass { get; set; } = new SingleTemperedGlass { Thickness = 6 };

    [Required] public double WindLoad { get; set; }
    public double AltitudeDifference { get; set; }
    public double TemperatureDifference { get; set; }
    public double LoadDuration { get; set; } = 3;
    public string SupportCondition { get; set; } = "FourSides";

    /// <summary>转为 V1 请求 (内部复用计算逻辑)</summary>
    public UnifiedGlassCalculationRequest ToV1()
    {
        return new UnifiedGlassCalculationRequest
        {
            ShortSide = ShortSide,
            LongSide = LongSide,
            GlassConfig = Glass.ToConfigDto(),
            WindLoad = WindLoad,
            AltitudeDifference = AltitudeDifference,
            TemperatureDifference = TemperatureDifference,
            LoadDuration = LoadDuration,
            SupportCondition = SupportCondition
        };
    }
}

/// <summary>玻璃常量选项列表</summary>
public static class GlassConstants
{
    public static List<int> GlassThicknessOptions { get; } = new() { 5, 6, 8, 10, 12, 15, 19, 20 };
    public static List<double> FilmThicknessOptions { get; } = new() { 0.76, 1.14, 1.52, 2.28 };
    public static List<int> InsulatingThicknessOptions { get; } = new() { 6, 9, 12, 15, 16 };
    public static List<int> LaminatedSheetCountOptions { get; } = new() { 2, 3, 4, 5 };
}

#endregion

#region ===== 玻璃计算请求模型 (旧版, 向后兼容) =====

/// <summary>单片玻璃计算请求</summary>
[Obsolete("请使用 UnifiedGlassCalculationRequest")]
public class SingleGlassCalculationRequest
{
    [Required] public double ShortSide { get; set; }
    [Required] public double LongSide { get; set; }
    [Required] public double Thickness { get; set; }
    [Required] public string GlassType { get; set; } = "Tempered";
    [Required] public double WindLoad { get; set; }
    public string SupportCondition { get; set; } = "FourSides";
}

/// <summary>中空玻璃计算请求</summary>
[Obsolete("请使用 UnifiedGlassCalculationRequest")]
public class InsulatingGlassCalculationRequest
{
    [Required] public double ShortSide { get; set; }
    [Required] public double LongSide { get; set; }
    [Required] public double OuterThickness { get; set; }
    [Required] public double InnerThickness { get; set; }
    [Required] public double AirSpaceThickness { get; set; }
    public string OuterGlassType { get; set; } = "Tempered";
    public string InnerGlassType { get; set; } = "Tempered";
    [Required] public double WindLoad { get; set; }
    public double AltitudeDifference { get; set; }
    public double TemperatureDifference { get; set; }
}

/// <summary>夹胶玻璃计算请求</summary>
[Obsolete("请使用 UnifiedGlassCalculationRequest")]
public class LaminatedGlassCalculationRequest
{
    [Required] public double ShortSide { get; set; }
    [Required] public double LongSide { get; set; }
    [Required] public List<double> GlassThicknesses { get; set; } = new();
    [Required] public List<double> InterlayerThicknesses { get; set; } = new();
    public string GlassType { get; set; } = "Tempered";
    public string InterlayerType { get; set; } = "PVB";
    [Required] public double WindLoad { get; set; }
    public double LoadDuration { get; set; } = 3;

    /// <summary>面板风荷载标准值 wk (MPa)</summary>
    public double PanelWindLoad { get; set; }

    /// <summary>地震作用标准值 qEAk (MPa)</summary>
    public double SeismicLoad { get; set; }

    /// <summary>动力放大系数 βE</summary>
    public double BetaE { get; set; } = 5.0;

    /// <summary>水平地震影响系数最大值 αmax</summary>
    public double AlphaMax { get; set; } = 0.16;

    /// <summary>夹层胶片剪切模量 G (MPa)，SGP默认11.3</summary>
    public double? InterlayerShearModulus { get; set; }
}

/// <summary>玻璃肋计算请求</summary>
public class GlassRibCalculationRequest
{
    [Required] public double RibHeight { get; set; }
    [Required] public double RibThickness { get; set; }
    [Required] public double Span { get; set; }
    [Required] public double LineLoad { get; set; }
    public string GlassType { get; set; } = "Tempered";
    public bool IsLaminated { get; set; }
    public string SupportType { get; set; } = "Simply";
}

/// <summary>玻璃计算结果</summary>
public class GlassCalculationResult
{
    public bool Passed { get; set; }
    public CheckResult StrengthCheck { get; set; } = new();
    public CheckResult DeflectionCheck { get; set; } = new();
    public double? EquivalentThickness { get; set; }
    public double? LoadShareFactor { get; set; }
    public double MaxStress { get; set; }
    public double MaxDeflection { get; set; }
    public double DeflectionLimit { get; set; }
    public string Standard { get; set; } = "JGJ 102-2003";
    public List<CalculationStep> Steps { get; set; } = new();

    /// <summary>逐片计算结果（CompositeGlass端点使用）</summary>
    public List<GlassLayerResult> LayerResults { get; set; } = new();
}

/// <summary>通用玻璃层定义</summary>
[Obsolete("请使用 GlassConfigDto")]
public class GlassLayer
{
    /// <summary>玻璃类型: Single(单片), Laminated(夹胶)</summary>
    public string LayerType { get; set; } = "Single";

    /// <summary>单片时的厚度(mm)</summary>
    public double Thickness { get; set; }

    /// <summary>夹胶时的各层玻璃厚度(mm)</summary>
    public List<double>? LaminatedThicknesses { get; set; }

    /// <summary>夹胶时的各层胶片厚度(mm)</summary>
    public List<double>? InterlayerThicknesses { get; set; }

    /// <summary>玻璃材质: Float/Tempered/SemiTempered/HeatStrengthened</summary>
    public string GlassType { get; set; } = "Tempered";

    /// <summary>夹胶类型: PVB/SGP/EVA</summary>
    public string InterlayerType { get; set; } = "PVB";
}

/// <summary>通用中空/夹胶/中空夹胶计算请求</summary>
[Obsolete("请使用 UnifiedGlassCalculationRequest")]
public class CompositeGlassCalculationRequest
{
    [Required] public double ShortSide { get; set; }
    [Required] public double LongSide { get; set; }

    /// <summary>玻璃层列表（从外到内）</summary>
    [Required] public List<GlassLayer> Layers { get; set; } = new();

    /// <summary>中空腔厚度列表(mm)，长度 = Layers.Count - 1（无腔时为空或null）</summary>
    public List<double>? AirSpaceThicknesses { get; set; }

    /// <summary>风荷载标准值 (kN/m²)</summary>
    [Required] public double WindLoad { get; set; }

    /// <summary>海拔差 (m)</summary>
    public double AltitudeDifference { get; set; }

    /// <summary>温度差 (℃)</summary>
    public double TemperatureDifference { get; set; }

    /// <summary>荷载持续时间 (秒)</summary>
    public double LoadDuration { get; set; } = 3;
}

/// <summary>逐片玻璃计算结果</summary>
public class GlassLayerResult
{
    public int LayerIndex { get; set; }
    public string Description { get; set; } = string.Empty;
    public double EquivalentThickness { get; set; }
    public double LoadShareFactor { get; set; }
    public double Stress { get; set; }
    public double AllowableStress { get; set; }
    public double Deflection { get; set; }
    public bool StressOk { get; set; }
    public bool DeflectionOk { get; set; }
}

#endregion

#region ===== 风荷载计算 =====

/// <summary>风荷载计算请求</summary>
public class WindLoadCalculationRequest
{
    /// <summary>计算点标高 z (m)</summary>
    [Required] public double Height { get; set; }

    /// <summary>地面粗糙度类别 (A/B/C/D)</summary>
    [Required] public string TerrainCategory { get; set; } = "C";

    /// <summary>基本风压 w0 (MPa)，50年重现期</summary>
    [Required] public double BasicWindPressure { get; set; }

    /// <summary>局部体型系数 μs1(1)，墙面大面位置取1</summary>
    public double ShapeCoefficient { get; set; } = 1.0;

    /// <summary>地形修正系数 K1</summary>
    public double K1 { get; set; } = 1.0;

    /// <summary>风向影响系数 K2</summary>
    public double K2 { get; set; } = 1.0;

    /// <summary>支承结构从属面积 (m²)</summary>
    public double StructureArea { get; set; }

    /// <summary>建筑类型 (封闭式/开洞式)</summary>
    public string BuildingType { get; set; } = "封闭式";

    /// <summary>内压系数，封闭式建筑取0.2</summary>
    public double InternalPressureCoeff { get; set; } = 0.2;
}

/// <summary>风荷载计算结果</summary>
public class WindLoadCalculationResult
{
    public bool Passed { get; set; } = true;

    /// <summary>风压高度变化系数 μz</summary>
    public double MuZ { get; set; }

    /// <summary>阵风系数 βgz</summary>
    public double BetaGz { get; set; }

    /// <summary>实际采用的风荷载放大系数 β</summary>
    public double Beta { get; set; }

    /// <summary>支承结构局部风压体型系数 μs1(结构)</summary>
    public double StructureShapeCoeff { get; set; }

    /// <summary>面板局部风压体型系数 μs1(面板)</summary>
    public double PanelShapeCoeff { get; set; }

    /// <summary>支承结构风荷载标准值 wk (MPa)</summary>
    public double StructureWindLoad { get; set; }

    /// <summary>面板风荷载标准值 wk (MPa)</summary>
    public double PanelWindLoad { get; set; }

    public string Standard { get; set; } = "GB50009-2012, GB55001-2021";
    public List<CalculationStep> Steps { get; set; } = new();
}

#endregion

#region ===== 地震计算 =====

/// <summary>地震计算请求</summary>
public class SeismicCalculationRequest
{
    /// <summary>动力放大系数 βE</summary>
    public double BetaE { get; set; } = 5.0;

    /// <summary>水平地震影响系数最大值 αmax</summary>
    [Required] public double AlphaMax { get; set; }

    /// <summary>幕墙构件重力荷载标准值 Gk (N)</summary>
    [Required] public double GravityLoad { get; set; }

    /// <summary>幕墙构件面积 A (mm²)</summary>
    [Required] public double Area { get; set; }

    /// <summary>重力荷载分项系数 γG</summary>
    public double GammaG { get; set; } = 1.3;

    /// <summary>风荷载分项系数 γw</summary>
    public double GammaW { get; set; } = 1.5;

    /// <summary>地震作用分项系数 γE</summary>
    public double GammaE { get; set; } = 1.4;

    /// <summary>风荷载组合系数 ψw</summary>
    public double PsiW { get; set; } = 1.0;

    /// <summary>地震作用组合系数 ψE</summary>
    public double PsiE { get; set; } = 0.5;

    /// <summary>重力荷载产生的效应标准值 SGk</summary>
    public double SGk { get; set; }

    /// <summary>风荷载产生的效应标准值 Swk</summary>
    public double Swk { get; set; }
}

/// <summary>地震计算结果</summary>
public class SeismicCalculationResult
{
    /// <summary>垂直于幕墙平面的分布水平地震作用标准值 qEk (MPa)</summary>
    public double QEk { get; set; }

    /// <summary>平行于幕墙平面的集中水平地震作用标准值 PEk (N)</summary>
    public double PEk { get; set; }

    /// <summary>地震作用效应标准值 SEk</summary>
    public double SEk { get; set; }

    /// <summary>作用效应组合设计值 S (强度计算)</summary>
    public double StrengthCombination { get; set; }

    /// <summary>作用效应组合设计值 S (挠度计算)</summary>
    public double DeflectionCombination { get; set; }

    public string Standard { get; set; } = "JGJ 102-2003";
    public List<CalculationStep> Steps { get; set; } = new();
}

#endregion

#region ===== 连接件计算 =====

/// <summary>连接件计算请求</summary>
public class ConnectionCalculationRequest
{
    /// <summary>计算点标高 (m)</summary>
    public double Height { get; set; } = 100;

    /// <summary>立柱计算间距 B (mm)</summary>
    [Required] public double MullionSpacing { get; set; }

    /// <summary>横梁分格宽 B (mm)</summary>
    [Required] public double TransomWidth { get; set; }

    /// <summary>横梁分格高 H (mm)</summary>
    [Required] public double TransomHeight { get; set; }

    /// <summary>立柱跨度 L (mm)</summary>
    [Required] public double MullionSpan { get; set; }

    /// <summary>短跨 L1 (mm)</summary>
    [Required] public double ShortSpan { get; set; }

    /// <summary>长跨 L2 (mm)</summary>
    [Required] public double LongSpan { get; set; }

    /// <summary>支承结构风荷载标准值 wk (MPa)</summary>
    [Required] public double WindLoad { get; set; }

    /// <summary>面板风荷载标准值 wk (MPa)</summary>
    public double PanelWindLoad { get; set; }

    /// <summary>幕墙单位面积自重 (MPa)</summary>
    public double GravityLoad { get; set; } = 0.0005;

    /// <summary>面板单位面积自重 (MPa)</summary>
    public double PanelGravityLoad { get; set; } = 0.0004;

    /// <summary>βE 动力放大系数</summary>
    public double BetaE { get; set; } = 5.0;

    /// <summary>αmax 水平地震影响系数最大值</summary>
    public double AlphaMax { get; set; } = 0.16;

    /// <summary>横梁与角码连接螺栓直径 (mm)</summary>
    public double TransomBoltDiameter { get; set; } = 6;

    /// <summary>角码与立柱连接螺栓直径 (mm)</summary>
    public double AngleBoltDiameter { get; set; } = 6;

    /// <summary>立柱与主结构连接螺栓直径 (mm)</summary>
    public double MullionBoltDiameter { get; set; } = 12;

    /// <summary>横梁壁厚 (mm)</summary>
    public double TransomWallThickness { get; set; } = 3;

    /// <summary>立柱壁厚 (mm)</summary>
    public double MullionWallThickness { get; set; } = 3;

    /// <summary>铝角码厚度 (mm)</summary>
    public double AngleThickness { get; set; } = 5;

    /// <summary>钢角码壁厚 (mm)</summary>
    public double SteelAngleThickness { get; set; } = 6;

    /// <summary>螺栓类型材料强度 (MPa)，不锈钢A50取190</summary>
    public double BoltShearStrength { get; set; } = 190;

    /// <summary>铝型材承压强度 (MPa)，6063-T5取185</summary>
    public double AluminumBearingStrength { get; set; } = 185;

    /// <summary>钢角码承压强度 (MPa)，Q235取305</summary>
    public double SteelBearingStrength { get; set; } = 305;

    /// <summary>立柱力学模型 (双跨/单跨)</summary>
    public string MullionModel { get; set; } = "双跨";
}

/// <summary>连接件计算结果</summary>
public class ConnectionCalculationResult
{
    public bool Passed { get; set; }

    /// <summary>横梁与角码连接验算</summary>
    public CheckResult TransomAngleCheck { get; set; } = new();

    /// <summary>角码与立柱连接验算</summary>
    public CheckResult AngleMullionCheck { get; set; } = new();

    /// <summary>立柱与主结构连接验算</summary>
    public CheckResult MullionStructureCheck { get; set; } = new();

    public string Standard { get; set; } = "JGJ 102-2003, GB50429-2007";
    public List<CalculationStep> Steps { get; set; } = new();
}

#endregion

#region ===== 焊缝计算 =====

/// <summary>焊缝计算请求</summary>
public class WeldCalculationRequest
{
    /// <summary>竖向焊缝长度 Lv (mm)</summary>
    [Required] public double VerticalWeldLength { get; set; }

    /// <summary>横向焊缝长度 Lh (mm)</summary>
    [Required] public double HorizontalWeldLength { get; set; }

    /// <summary>焊角高度 hf (mm)</summary>
    [Required] public double FilletSize { get; set; }

    /// <summary>轴向拉力 N (N)</summary>
    public double TensionForce { get; set; }

    /// <summary>剪力 V (N)</summary>
    public double ShearForce { get; set; }

    /// <summary>弯矩 M (N·mm)</summary>
    public double BendingMoment { get; set; }

    /// <summary>是否双转接件</summary>
    public bool IsDualTransfer { get; set; } = true;

    /// <summary>正面角焊缝强度增大系数 βf</summary>
    public double BetaF { get; set; } = 1.22;

    /// <summary>角焊缝强度设计值 ffw (MPa)</summary>
    public double WeldStrength { get; set; } = 160;

    /// <summary>构件钢材牌号</summary>
    public string SteelGrade { get; set; } = "Q235";
}

/// <summary>焊缝计算结果</summary>
public class WeldCalculationResult
{
    public bool Passed { get; set; }

    /// <summary>焊缝有效厚度 he (mm)</summary>
    public double EffectiveThickness { get; set; }

    /// <summary>焊缝总面积 A (mm²)</summary>
    public double WeldArea { get; set; }

    /// <summary>焊缝截面惯性矩 I (mm⁴)</summary>
    public double MomentOfInertia { get; set; }

    /// <summary>焊缝截面抵抗矩 W (mm³)</summary>
    public double SectionModulus { get; set; }

    /// <summary>强度验算</summary>
    public CheckResult StrengthCheck { get; set; } = new();

    public string Standard { get; set; } = "GB50017-2017";
    public List<CalculationStep> Steps { get; set; } = new();
}

#endregion

#region ===== 转接件计算 =====

/// <summary>转接件计算请求</summary>
public class TransferPieceCalculationRequest
{
    /// <summary>转接件型号 (如 10#槽钢)</summary>
    public string Specification { get; set; } = "10#槽钢";

    /// <summary>断面面积 A (mm²)</summary>
    [Required] public double SectionArea { get; set; }

    /// <summary>断面抵抗矩 W (mm³)</summary>
    [Required] public double SectionModulus { get; set; }

    /// <summary>断面惯性矩 I (mm⁴)</summary>
    [Required] public double MomentOfInertia { get; set; }

    /// <summary>轴向拉力 N (N)</summary>
    public double TensionForce { get; set; }

    /// <summary>剪力 V (N)</summary>
    public double ShearForce { get; set; }

    /// <summary>弯矩 M (N·mm)</summary>
    public double BendingMoment { get; set; }

    /// <summary>偏心矩 e (mm)，剪力作用点到埋板距离</summary>
    public double Eccentricity { get; set; } = 80;

    /// <summary>是否双转接件</summary>
    public bool IsDualTransfer { get; set; } = true;

    /// <summary>塑性发展系数 γ</summary>
    public double Gamma { get; set; } = 1.05;

    /// <summary>转接件抗弯强度设计值 f (MPa)</summary>
    public double AllowableStress { get; set; } = 215;

    /// <summary>弹性模量 E (MPa)</summary>
    public double ElasticModulus { get; set; } = 206000;
}

/// <summary>转接件计算结果</summary>
public class TransferPieceCalculationResult
{
    public bool Passed { get; set; }
    public CheckResult StrengthCheck { get; set; } = new();
    public CheckResult DeflectionCheck { get; set; } = new();
    public string Standard { get; set; } = "GB50017-2017";
    public List<CalculationStep> Steps { get; set; } = new();
}

#endregion

#region ===== 压板计算 =====

/// <summary>压板计算请求</summary>
public class PressurePlateCalculationRequest
{
    /// <summary>板块短边 a (mm)</summary>
    [Required] public double PanelShortSide { get; set; }

    /// <summary>板块长边 (mm)</summary>
    public double PanelLongSide { get; set; }

    /// <summary>压板宽度 Byb (mm)</summary>
    [Required] public double PlateWidth { get; set; }

    /// <summary>压板长度 Lyb (mm)</summary>
    [Required] public double PlateLength { get; set; }

    /// <summary>压板厚度 tyb (mm)</summary>
    [Required] public double PlateThickness { get; set; }

    /// <summary>压板间距 Syb (mm)</summary>
    [Required] public double PlateSpacing { get; set; }

    /// <summary>压板上螺栓孔径 d (mm)</summary>
    public double BoltHoleDiameter { get; set; } = 6;

    /// <summary>水平荷载设计值 qyb (MPa)，1.5wk+0.5×1.4qEAk</summary>
    [Required] public double DesignLoad { get; set; }

    /// <summary>压板材质抗弯强度设计值 (MPa)</summary>
    public double AllowableBendingStress { get; set; } = 90;

    /// <summary>压板材质抗剪强度设计值 (MPa)</summary>
    public double AllowableShearStress { get; set; } = 55;

    /// <summary>螺栓抗拉强度设计值 ftb (MPa)</summary>
    public double BoltTensileStrength { get; set; } = 170;

    /// <summary>螺栓有效直径 de (mm)</summary>
    public double BoltEffectiveDiameter { get; set; } = 4.249467;
}

/// <summary>压板计算结果</summary>
public class PressurePlateCalculationResult
{
    public bool Passed { get; set; }
    public CheckResult BendingCheck { get; set; } = new();
    public CheckResult ShearCheck { get; set; } = new();
    public CheckResult BoltCheck { get; set; } = new();
    public string Standard { get; set; } = "JGJ 102-2003";
    public List<CalculationStep> Steps { get; set; } = new();
}

#endregion

#region ===== 玻璃副框计算 =====

/// <summary>玻璃副框计算请求</summary>
public class SubFrameCalculationRequest
{
    /// <summary>玻璃分格宽 B (mm)</summary>
    [Required] public double PanelWidth { get; set; }

    /// <summary>玻璃分格高 H (mm)</summary>
    [Required] public double PanelHeight { get; set; }

    /// <summary>组成玻璃板块的玻璃总厚度 t1 (mm)</summary>
    [Required] public double TotalGlassThickness { get; set; }

    /// <summary>副框总周长 S (mm)</summary>
    [Required] public double FramePerimeter { get; set; }

    /// <summary>副框计算部分总厚度 a (mm)</summary>
    [Required] public double FrameThickness { get; set; }

    /// <summary>玻璃板块荷载组合设计值 q (MPa)</summary>
    [Required] public double DesignLoad { get; set; }

    /// <summary>副框材料抗弯强度设计值 f (MPa)</summary>
    public double AllowableBendingStress { get; set; } = 90;

    /// <summary>副框材料抗剪强度设计值 fv (MPa)</summary>
    public double AllowableShearStress { get; set; } = 55;

    /// <summary>玻璃体积密度 ρ (kN/m³)</summary>
    public double GlassDensity { get; set; } = 25.6;
}

/// <summary>玻璃副框计算结果</summary>
public class SubFrameCalculationResult
{
    public bool Passed { get; set; }
    public CheckResult TensionCheck { get; set; } = new();
    public CheckResult ShearCheck { get; set; } = new();
    public string Standard { get; set; } = "JGJ 102-2003";
    public List<CalculationStep> Steps { get; set; } = new();
}

#endregion

#region ===== 承托件计算 =====

/// <summary>承托件计算请求</summary>
public class BearingPieceCalculationRequest
{
    /// <summary>玻璃分格宽 B (mm)</summary>
    [Required] public double PanelWidth { get; set; }

    /// <summary>玻璃分格高 H (mm)</summary>
    [Required] public double PanelHeight { get; set; }

    /// <summary>承托件承载的玻璃总厚度 t2 (mm)</summary>
    [Required] public double TotalGlassThickness { get; set; }

    /// <summary>承托件厚度 t (mm)</summary>
    [Required] public double PieceThickness { get; set; }

    /// <summary>单个承托件总长度 L (mm)</summary>
    [Required] public double PieceLength { get; set; }

    /// <summary>受力点到根部距离 e (mm)</summary>
    [Required] public double Eccentricity { get; set; }

    /// <summary>承托件总悬臂尺寸 w (mm)</summary>
    [Required] public double CantileverLength { get; set; }

    /// <summary>每板块下承托件数量 n</summary>
    public int PieceCount { get; set; } = 2;

    /// <summary>承托件材料抗弯强度设计值 f (MPa)</summary>
    public double AllowableStress { get; set; } = 180;

    /// <summary>承托件弹性模量 E (MPa)</summary>
    public double ElasticModulus { get; set; } = 206000;

    /// <summary>玻璃体积密度 ρ (kN/m³)</summary>
    public double GlassDensity { get; set; } = 25.6;
}

/// <summary>承托件计算结果</summary>
public class BearingPieceCalculationResult
{
    public bool Passed { get; set; }
    public CheckResult BendingCheck { get; set; } = new();
    public CheckResult DeflectionCheck { get; set; } = new();
    public string Standard { get; set; } = "JGJ 102-2003";
    public List<CalculationStep> Steps { get; set; } = new();
}

#endregion

#region ===== 伸缩缝+耐候胶计算 =====

/// <summary>伸缩缝及耐候胶计算请求</summary>
public class ExpansionJointCalculationRequest
{
    /// <summary>立柱材料线膨胀系数 α</summary>
    public double MullionExpansionCoeff { get; set; } = 2.3e-5;

    /// <summary>板块材料线膨胀系数 α</summary>
    public double PanelExpansionCoeff { get; set; } = 1e-5;

    /// <summary>温度变化 Δt (℃)</summary>
    [Required] public double TemperatureDifference { get; set; }

    /// <summary>立柱跨度 L (mm)</summary>
    [Required] public double MullionSpan { get; set; }

    /// <summary>板块长边长度 b (mm)</summary>
    [Required] public double PanelLongSide { get; set; }

    /// <summary>施工误差 d1 (mm)</summary>
    public double ConstructionTolerance { get; set; } = 3;

    /// <summary>其它预留量 d2 (mm)</summary>
    public double OtherAllowance { get; set; } = 2;

    /// <summary>耐候胶变位承受能力 δ</summary>
    public double WeatherSealantCapacity { get; set; } = 0.25;

    /// <summary>耐候胶施工偏差 dc (mm)</summary>
    public double SealantConstructionTolerance { get; set; } = 3;

    /// <summary>耐候胶其它预留量 dE (mm)</summary>
    public double SealantOtherAllowance { get; set; } = 2;

    /// <summary>实际伸缩缝宽度 (mm)</summary>
    public double ActualExpansionJoint { get; set; }

    /// <summary>实际耐候胶缝宽度 (mm)</summary>
    public double ActualSealantWidth { get; set; }
}

/// <summary>伸缩缝及耐候胶计算结果</summary>
public class ExpansionJointCalculationResult
{
    public bool Passed { get; set; }

    /// <summary>伸缩缝计算值 d (mm)</summary>
    public double RequiredExpansionJoint { get; set; }

    /// <summary>耐候胶缝宽度计算值 ws (mm)</summary>
    public double RequiredSealantWidth { get; set; }

    public CheckResult ExpansionJointCheck { get; set; } = new();
    public CheckResult SealantWidthCheck { get; set; } = new();
    public string Standard { get; set; } = "JGJ 102-2003";
    public List<CalculationStep> Steps { get; set; } = new();
}

#endregion

#region ===== 计算书模型 =====

/// <summary>计算书生成请求（通用）</summary>
public class ReportGenerationRequest
{
    [Required] public string ProjectName { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;
    public string Calculator { get; set; } = string.Empty;
    public string Reviewer { get; set; } = string.Empty;
    public string OutputFormat { get; set; } = "PDF";
    public string? TemplateId { get; set; }
}

#pragma warning disable CS0618 // 报告请求引用旧版计算请求类型以保持兼容
public class GlassReportRequest : ReportGenerationRequest
{
    [Required] public SingleGlassCalculationRequest? SingleGlass { get; set; }
    public InsulatingGlassCalculationRequest? InsulatingGlass { get; set; }
    public LaminatedGlassCalculationRequest? LaminatedGlass { get; set; }
}

public class InsulatingGlassReportRequest : ReportGenerationRequest
{
    [Required] public InsulatingGlassCalculationRequest Calculation { get; set; } = new();
}
#pragma warning restore CS0618

public class GlassRibReportRequest : ReportGenerationRequest
{
    [Required] public GlassRibCalculationRequest Calculation { get; set; } = new();
}

public class AluminumRafterReportRequest : ReportGenerationRequest
{
    [Required] public AluminumRafterCalculationRequest Calculation { get; set; } = new();
}

public class AluminumMullionReportRequest : ReportGenerationRequest
{
    [Required] public AluminumMullionCalculationRequest Calculation { get; set; } = new();
}

public class StoneBracketReportRequest : ReportGenerationRequest
{
    [Required] public StoneBracketCalculationRequest Calculation { get; set; } = new();
}

public class EmbedPartReportRequest : ReportGenerationRequest
{
    [Required] public EmbedPartCalculationRequest Calculation { get; set; } = new();
}

public class AluminumPlateReportRequest : ReportGenerationRequest
{
    [Required] public AluminumPlateCalculationRequest Calculation { get; set; } = new();
}

public class StonePanelReportRequest : ReportGenerationRequest
{
    [Required] public StonePanelCalculationRequest Calculation { get; set; } = new();
}

public class SiliconeReportRequest : ReportGenerationRequest
{
    [Required] public SiliconeCalculationRequest Calculation { get; set; } = new();
}

public class ReportGenerationResult
{
    public string ReportId { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public long FileSize { get; set; }
}

public class ReportTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

#endregion

#region ===== 通用模型 =====

/// <summary>验算结果</summary>
public class CheckResult
{
    public bool Passed { get; set; }
    public double ActualValue { get; set; }
    public double AllowableValue { get; set; }
    public double UtilizationRatio => AllowableValue != 0 ? Math.Round(ActualValue / AllowableValue, 4) : 0;
    public string Description { get; set; } = string.Empty;
}

/// <summary>计算步骤</summary>
public class CalculationStep
{
    public int StepNo { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Formula { get; set; } = string.Empty;
    public string Process { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
}

#endregion

#region ===== 铝单板面板计算 =====

/// <summary>铝单板面板计算请求 (JGJ133-2001)</summary>
public class AluminumPanelCalcRequest
{
    /// <summary>面板短边 a (mm)</summary>
    [Required] public double ShortSide { get; set; }

    /// <summary>面板长边 b (mm)</summary>
    [Required] public double LongSide { get; set; }

    /// <summary>铝单板厚度 t (mm)</summary>
    [Required] public double Thickness { get; set; }

    /// <summary>面板风荷载标准值 wk (MPa)</summary>
    [Required] public double WindLoad { get; set; }

    /// <summary>面板水平荷载设计值组合 q (MPa)，1.5wk+0.5×1.4qEAk</summary>
    [Required] public double DesignLoad { get; set; }

    /// <summary>面板荷载组合标准值 qk (MPa)，wk+0.5qEAk</summary>
    public double? StandardLoad { get; set; }

    /// <summary>铝单板材质 (3003-H24 / 5005-H14 等)</summary>
    public string MaterialGrade { get; set; } = "3003-H24";

    /// <summary>铝单板抗弯强度设计值 f (MPa)，3003-H24取54</summary>
    public double? AllowableBendingStress { get; set; }

    /// <summary>弹性模量 E (MPa)，默认70000</summary>
    public double ElasticModulus { get; set; } = 70000;

    /// <summary>泊松比 υ，默认0.3</summary>
    public double PoissonRatio { get; set; } = 0.3;

    /// <summary>加劲肋截面抵抗矩 W (mm³)，0表示无加劲肋</summary>
    public double StiffenerW { get; set; }

    /// <summary>加劲肋截面惯性矩 I (mm⁴)</summary>
    public double StiffenerI { get; set; }

    /// <summary>加劲肋材质抗弯强度设计值 (MPa)，6063-T5取90</summary>
    public double StiffenerAllowableStress { get; set; } = 90;

    /// <summary>加劲肋弹性模量 (MPa)</summary>
    public double StiffenerE { get; set; } = 70000;

    /// <summary>加劲肋塑性发展系数 γ</summary>
    public double StiffenerGamma { get; set; } = 1.0;

    /// <summary>加劲肋间距 Lx (mm)，即板格宽度</summary>
    public double StiffenerSpacing { get; set; }
}

/// <summary>铝单板面板计算结果</summary>
public class AluminumPanelCalcResult
{
    public bool Passed { get; set; }
    public CheckResult PanelStrengthCheck { get; set; } = new();
    public CheckResult PanelDeflectionCheck { get; set; } = new();
    public CheckResult? StiffenerStrengthCheck { get; set; }
    public CheckResult? StiffenerDeflectionCheck { get; set; }
    public double Theta { get; set; }
    public double Eta { get; set; }
    public string Standard { get; set; } = "JGJ 133-2001";
    public List<CalculationStep> Steps { get; set; } = new();
}

#endregion

#region ===== GRC板面板计算 =====

/// <summary>GRC板面板计算请求 (JGJ336-2016, 背栓支承连接)</summary>
public class GrcPanelCalcRequest
{
    [Required] public double ShortSide { get; set; }
    [Required] public double LongSide { get; set; }
    [Required] public double SupportShortSide { get; set; }
    [Required] public double SupportLongSide { get; set; }
    [Required] public double EffectiveThickness { get; set; }
    [Required] public double TotalThickness { get; set; }
    [Required] public double WindLoad { get; set; }
    [Required] public double DesignLoad { get; set; }
    public double AllowableBendingStress { get; set; } = 11.5;
    public double AllowableShearStress { get; set; } = 2.3;
    public double ElasticModulus { get; set; } = 14000;
    public double PoissonRatio { get; set; } = 0.25;
    public double MaterialDensity { get; set; } = 0.0000167;
    public string ConnectionType { get; set; } = "背栓支承";
    public int BackBoltCount { get; set; } = 4;
    public int GravityBoltCount { get; set; } = 2;
    public double BackBoltDiameter { get; set; } = 6;
    public double BackBoltDepth { get; set; } = 15;
    public double BackBoltPulloutStrength { get; set; } = 8000;
    public double BackBoltSafetyFactor { get; set; } = 2.15;
    public double StressAdjustFactor { get; set; } = 1.25;
}

/// <summary>GRC板面板计算结果</summary>
public class GrcPanelCalcResult
{
    public bool Passed { get; set; }
    public CheckResult PanelStrengthCheck { get; set; } = new();
    public CheckResult PanelDeflectionCheck { get; set; } = new();
    public CheckResult? BackBoltShearCheck { get; set; }
    public CheckResult? BackBoltTensionCheck { get; set; }
    public CheckResult? BackBoltShearForceCheck { get; set; }
    public string Standard { get; set; } = "JGJ 336-2016";
    public List<CalculationStep> Steps { get; set; } = new();
}

#endregion

#region ===== UHPC板面板计算 =====

/// <summary>UHPC板面板计算请求 (JGJ336-2016, 通长挂件通槽式)</summary>
public class UhpcPanelCalcRequest
{
    [Required] public double ShortSide { get; set; }
    [Required] public double LongSide { get; set; }
    [Required] public double Span { get; set; }
    [Required] public double EffectiveThickness { get; set; }
    [Required] public double WindLoad { get; set; }
    [Required] public double DesignLoad { get; set; }
    public double AllowableBendingStress { get; set; } = 18;
    public double AllowableShearStress { get; set; } = 3.6;
    public double ElasticModulus { get; set; } = 14000;
    public double PoissonRatio { get; set; } = 0.25;
    public double SlotDepth { get; set; } = 15;
    public double SlotThickness { get; set; } = 6;
    public double StressConcentrationFactor { get; set; } = 1.5;
    public double HangerThickness { get; set; } = 3;
    public double HangerShearStrength { get; set; } = 100;
}

/// <summary>UHPC板面板计算结果</summary>
public class UhpcPanelCalcResult
{
    public bool Passed { get; set; }
    public CheckResult PanelStrengthCheck { get; set; } = new();
    public CheckResult PanelDeflectionCheck { get; set; } = new();
    public CheckResult? SlotBendingCheck { get; set; }
    public CheckResult? SlotShearCheck { get; set; }
    public CheckResult? HangerShearCheck { get; set; }
    public string Standard { get; set; } = "JGJ 336-2016";
    public List<CalculationStep> Steps { get; set; } = new();
}

#endregion

#region ===== 点式钢管结构计算 =====

/// <summary>点式钢管结构计算请求</summary>
public class PointSteelCalcRequest
{
    [Required] public double Span { get; set; }
    [Required] public double GridWidth { get; set; }
    [Required] public double WindLoad { get; set; }
    public double PanelWindLoad { get; set; }
    public double GravityLoad { get; set; } = 0.0005;
    public double BetaE { get; set; } = 5.0;
    public double AlphaMax { get; set; } = 0.16;
    public string SteelGrade { get; set; } = "Q235";
    [Required] public double SectionArea { get; set; }
    [Required] public double MomentOfInertia { get; set; }
    [Required] public double SectionModulus { get; set; }
    public double Gamma { get; set; } = 1.05;
    public double ElasticModulus { get; set; } = 206000;
    /// <summary>各集中力作用点位置 (mm)，从A支座起算</summary>
    [Required] public List<double> LoadPositions { get; set; } = new();
    /// <summary>各分格沿梁方向长度 (mm)，含首末段</summary>
    [Required] public List<double> SegmentLengths { get; set; } = new();
    public string GlassConfig { get; set; } = string.Empty;
    public string ConnectionType { get; set; } = "四点驳接";
}

/// <summary>点式钢管结构计算结果</summary>
public class PointSteelCalcResult
{
    public bool Passed { get; set; }
    public CheckResult StrengthCheck { get; set; } = new();
    public CheckResult DeflectionCheck { get; set; } = new();
    public double MaxBendingMoment { get; set; }
    public double MaxMomentPosition { get; set; }
    public double MaxDeflection { get; set; }
    public double MaxDeflectionPosition { get; set; }
    public List<double> DesignForces { get; set; } = new();
    public List<double> StandardForces { get; set; } = new();
    public string Standard { get; set; } = "JGJ 102-2003, GB50017-2017";
    public List<CalculationStep> Steps { get; set; } = new();
}

#endregion

#region ===== 简图请求模型 =====

/// <summary>立柱力学简图请求</summary>
public class MullionDiagramRequest
{
    /// <summary>总跨度 L (mm)</summary>
    public double L { get; set; }
    /// <summary>短跨 L1 (mm)</summary>
    public double L1 { get; set; }
    /// <summary>长跨 L2 (mm)</summary>
    public double L2 { get; set; }
    /// <summary>均布荷载 q (kN/m)</summary>
    public double Q { get; set; }
    /// <summary>中支座弯矩 M1 (kN·m)</summary>
    public double M1 { get; set; }
    /// <summary>中支座反力 R1 (kN)</summary>
    public double R1 { get; set; }
    /// <summary>梁类型: "双跨"/"单跨"/"悬臂"</summary>
    public string BeamType { get; set; } = "双跨";
}

/// <summary>横梁力学简图请求</summary>
public class TransomDiagramRequest
{
    /// <summary>跨度 B (mm)</summary>
    public double B { get; set; }
    /// <summary>荷载类型: "三角"/"矩形"/"梯形"</summary>
    public string LoadType { get; set; } = "矩形";
    /// <summary>荷载值 q (kN/m)</summary>
    public double Q { get; set; }
    /// <summary>绕Y轴弯矩 My (kN·m)</summary>
    public double My { get; set; }
    /// <summary>绕X轴弯矩 Mx (kN·m)</summary>
    public double Mx { get; set; }
}

/// <summary>板块支承简图请求</summary>
public class PlateSupportDiagramRequest
{
    /// <summary>短边 a (mm)</summary>
    public double A { get; set; }
    /// <summary>长边 b (mm)</summary>
    public double B { get; set; }
    /// <summary>支承类型: "四边简支"/"两对边简支"/"四点支承"</summary>
    public string SupportType { get; set; } = "四边简支";
    /// <summary>厚度 t (mm)</summary>
    public double T { get; set; }
}

/// <summary>连接节点简图请求</summary>
public class ConnectionDiagramRequest
{
    /// <summary>连接类型: "螺栓"/"焊接"/"角码"</summary>
    public string ConnectionType { get; set; } = "螺栓";
    /// <summary>螺栓直径 (mm)</summary>
    public double BoltDiameter { get; set; } = 12;
    /// <summary>板厚 (mm)</summary>
    public double PlateThickness { get; set; } = 6;
    /// <summary>角码尺寸 (mm)</summary>
    public double BracketSize { get; set; } = 80;
    /// <summary>焊缝长度 (mm)</summary>
    public double WeldLength { get; set; } = 60;
    /// <summary>焊缝厚度 (mm)</summary>
    public double WeldThickness { get; set; } = 4;
}

#endregion

#region ===== 云图请求模型 =====

/// <summary>板面云图（应力/挠度）请求</summary>
public class PlateContourRequest
{
    /// <summary>短边 a (mm)</summary>
    public double ShortSide { get; set; }
    /// <summary>长边 b (mm)</summary>
    public double LongSide { get; set; }
    /// <summary>厚度 t (mm)</summary>
    public double Thickness { get; set; }
    /// <summary>风荷载标准值 (kN/m²)</summary>
    public double WindLoad { get; set; }
    /// <summary>弹性模量 E (MPa)</summary>
    public double ElasticModulus { get; set; } = 72000;
    /// <summary>泊松比 v</summary>
    public double PoissonRatio { get; set; } = 0.2;
    /// <summary>支承类型: "四边简支"</summary>
    public string SupportType { get; set; } = "四边简支";
    /// <summary>云图类型: "应力" 或 "挠度"</summary>
    public string ContourType { get; set; } = "应力";
    /// <summary>网格精度</summary>
    public int GridSize { get; set; } = 30;
}

/// <summary>组合玻璃云图请求 — 生成每层每片的应力/挠度云图</summary>
public class CompositeGlassContourRequest
{
    public double ShortSide { get; set; }
    public double LongSide { get; set; }
    public double WindLoad { get; set; }
    #pragma warning disable CS0618
    public List<GlassLayer> Layers { get; set; } = new();
    #pragma warning restore CS0618
    public List<double>? AirSpaceThicknesses { get; set; }
    public double AltitudeDifference { get; set; }
    public double TemperatureDifference { get; set; }
    public string ContourType { get; set; } = "应力";
    public int GridSize { get; set; } = 25;
}

/// <summary>组合玻璃云图请求 V2 — 使用 GlassConfigDto</summary>
public class CompositeGlassContourRequestV2
{
    public double ShortSide { get; set; }
    public double LongSide { get; set; }
    public double WindLoad { get; set; }
    public GlassConfigDto GlassConfig { get; set; } = new();
    public double AltitudeDifference { get; set; }
    public double TemperatureDifference { get; set; }
    public string ContourType { get; set; } = "应力";
    public int GridSize { get; set; } = 25;
}

public class CompositeGlassContourResult
{
    public string Summary { get; set; } = "";
    public List<ContourLayerResult> LayerContours { get; set; } = new();
}

public class ContourLayerResult
{
    public int LayerIndex { get; set; }
    public int SubIndex { get; set; }
    public string Description { get; set; } = "";
    public double EquivalentThickness { get; set; }
    public double LoadShareFactor { get; set; }
    public double MaxValue { get; set; }
    public string Svg { get; set; } = "";
}

#endregion
