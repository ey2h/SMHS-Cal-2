using System.Text.Json.Serialization;

namespace GlassManagement.Api.Models;

/// <summary>向导会话</summary>
public class WizardSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
    public int UserId { get; set; }
    public int? ProjectId { get; set; }
    public int CurrentStep { get; set; } = 0;
    public HashSet<int> CompletedSteps { get; set; } = new();
    public Dictionary<string, object?> StepData { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Active";
}

/// <summary>向导会话状态响应</summary>
public class WizardSessionResponse
{
    public string SessionId { get; set; } = "";
    public int CurrentStep { get; set; }
    public List<int> CompletedSteps { get; set; } = new();
    public string Status { get; set; } = "";
    public int? ProjectId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Dictionary<string, object?>? StepData { get; set; }
}

/// <summary>向导步骤结果</summary>
public class WizardStepResult
{
    public bool Success { get; set; }
    public int CurrentStep { get; set; }
    public object? Result { get; set; }
    public object? NextStepTemplate { get; set; }
    public string? Message { get; set; }
}

// ===== Step 1: 基本参数（22个字段 + 自动计算）=====

/// <summary>步骤1：基本参数 — 参照汇宝需求清单</summary>
public class Step1BasicInput
{
    // --- 项目与编制信息 ---
    public string ProjectName { get; set; } = "";
    public string? ProjectCode { get; set; }
    public string? CompanyName { get; set; }
    public string? Designer { get; set; }
    public string? Reviewer { get; set; }
    public string? Approver { get; set; }

    // --- 环境参数 ---
    /// <summary>重现期：50 或 100 年</summary>
    public int ReturnPeriod { get; set; } = 50;

    /// <summary>计算标高 (m)</summary>
    public double CalcElevation { get; set; }

    /// <summary>建筑总高度 (m)</summary>
    public double BuildingHeight { get; set; }

    /// <summary>安装高度 (m) — 兼容旧字段</summary>
    public double InstallHeight { get; set; }

    /// <summary>地面粗糙度类别: A/B/C/D</summary>
    public string TerrainCategory { get; set; } = "B";

    /// <summary>规范依据代码: GB50009-2012 / DBJ/T15-101-2022 / DGJ08-56-2012 / ...</summary>
    public string StandardCode { get; set; } = "GB50009-2012";

    // --- 地区数据（选地区后自动填入，可手改）---
    /// <summary>地区名称</summary>
    public string? City { get; set; }

    /// <summary>基本风压 w₀ (Pa)</summary>
    public double BasicWindPressure { get; set; }

    /// <summary>抗震设防烈度</summary>
    public int SeismicIntensity { get; set; } = 7;

    /// <summary>地震动峰值加速度 (g)</summary>
    public double PeakAcceleration { get; set; }

    /// <summary>地震影响系数最大值 αmax</summary>
    public double AlphaMax { get; set; }

    /// <summary>极限温差 (℃)</summary>
    public double TemperatureDiff { get; set; }

    /// <summary>雪压 (Pa)</summary>
    public double SnowPressure { get; set; }

    // --- 体型系数 ---
    /// <summary>外部局部体型系数取值方式: wall_center(-1.0) / wall_corner(-1.8) / custom</summary>
    public string ExternalShapeMode { get; set; } = "wall_center";

    /// <summary>外部体型系数自定义值</summary>
    public double ExternalShapeValue { get; set; } = -1.0;

    /// <summary>内部局部体型系数取值方式: sealed(±0.2) / dominant_opening / custom</summary>
    public string InternalShapeMode { get; set; } = "sealed";

    /// <summary>内部体型系数自定义值</summary>
    public double InternalShapeValue { get; set; } = 0.2;

    /// <summary>风荷载标准值取值: calc / custom / min1kpa</summary>
    public string WindLoadMode { get; set; } = "calc";

    /// <summary>风荷载自定义值 (Pa)</summary>
    public double WindLoadCustom { get; set; }

    // --- 幕墙构造 ---
    /// <summary>幕墙结构类型: 构件式 / 单元式</summary>
    public string CurtainWallStructure { get; set; } = "构件式";

    /// <summary>横框结构: 无中横 / 有中横</summary>
    public string TransomStructure { get; set; } = "无中横";

    /// <summary>抗震设防分类: 标准 / 重点 / 特殊 / 适度</summary>
    public string SeismicCategory { get; set; } = "标准";

    // --- 分格尺寸 ---
    /// <summary>立柱左分格宽 (mm)</summary>
    public double LeftPanelWidth { get; set; } = 1200;

    /// <summary>立柱右分格宽 (mm)</summary>
    public double RightPanelWidth { get; set; } = 1200;

    /// <summary>横框上分格高 (mm)</summary>
    public double UpperPanelHeight { get; set; } = 1500;

    /// <summary>横框下分格高 (mm)</summary>
    public double LowerPanelHeight { get; set; } = 1500;
}

/// <summary>Step1自动计算结果</summary>
public class Step1CalcResult
{
    /// <summary>构件从属面积 (m²)</summary>
    public double TributaryArea { get; set; }
    /// <summary>风压高度变化系数 μz</summary>
    public double MuZ { get; set; }
    /// <summary>阵风系数 βgz</summary>
    public double BetaGz { get; set; }
    /// <summary>局部体型系数 μs1</summary>
    public double MuS1 { get; set; }
    /// <summary>面板风荷载 wk,panel (kPa)</summary>
    public double WkPanel { get; set; }
    /// <summary>构件风荷载 wk,structure (kPa)</summary>
    public double WkStructure { get; set; }
    /// <summary>地震线荷载 qEAk (kN/m)</summary>
    public double SeismicLineLoad { get; set; }
    /// <summary>荷载组合值</summary>
    public double LoadCombination { get; set; }
}

// ===== Step 2: 玻璃配置与验算 =====

/// <summary>步骤2：玻璃配置与验算</summary>
public class Step2GlassInput
{
    /// <summary>玻璃构成: 单片/中空/夹层/中空夹层/双中空</summary>
    public string GlassComposition { get; set; } = "中空";

    /// <summary>外片厚度 (mm)</summary>
    public double OuterThickness { get; set; } = 6;

    /// <summary>外片类型: 普通/钢化/半钢化/夹层</summary>
    public string OuterType { get; set; } = "钢化";

    /// <summary>中空层厚度 (mm)</summary>
    public double AirGapThickness { get; set; } = 12;

    /// <summary>内片厚度 (mm)</summary>
    public double InnerThickness { get; set; } = 6;

    /// <summary>内片类型</summary>
    public string InnerType { get; set; } = "钢化";

    /// <summary>SGP/PVB胶片厚度 (mm)</summary>
    public double InterlayerThickness { get; set; } = 1.52;

    /// <summary>胶片类型: SGP/PVB</summary>
    public string InterlayerType { get; set; } = "PVB";

    /// <summary>玻璃短边a (mm)</summary>
    public double ShortSide { get; set; } = 1200;

    /// <summary>玻璃长边b (mm)</summary>
    public double LongSide { get; set; } = 1500;

    /// <summary>四边支承方式: 四边简支/两对边简支</summary>
    public string SupportType { get; set; } = "四边简支";

    // --- 兼容旧字段 ---
    public string? GlassDescription { get; set; }
    public double EquivalentThickness { get; set; }
    public double DesignStrength { get; set; }
    public double ElasticModulus { get; set; } = 72000;
}

/// <summary>玻璃验算结果</summary>
public class Step2CalcResult
{
    /// <summary>等效挠度厚度 te,w (mm)</summary>
    public double EquivDeflThickness { get; set; }
    /// <summary>1#等效应力厚度 t1e,σ (mm)</summary>
    public double EquivStress1Thickness { get; set; }
    /// <summary>2#等效应力厚度 t2e,σ (mm)</summary>
    public double EquivStress2Thickness { get; set; }
    /// <summary>剪力传递系数 Γ</summary>
    public double GammaShear { get; set; }
    /// <summary>1#玻璃应力 σ₁ (MPa)</summary>
    public double Stress1 { get; set; }
    /// <summary>2#玻璃应力 σ₂ (MPa)</summary>
    public double Stress2 { get; set; }
    /// <summary>玻璃强度设计值 fg (MPa)</summary>
    public double AllowableStress { get; set; }
    /// <summary>挠度 df (mm)</summary>
    public double Deflection { get; set; }
    /// <summary>挠度限值 (mm)</summary>
    public double DeflectionLimit { get; set; }
    /// <summary>强度1通过</summary>
    public bool Stress1Pass { get; set; }
    /// <summary>强度2通过</summary>
    public bool Stress2Pass { get; set; }
    /// <summary>挠度通过</summary>
    public bool DeflectionPass { get; set; }
}

// ===== Step 3: 立柱计算 =====

/// <summary>步骤3：立柱计算</summary>
public class Step3MullionInput
{
    /// <summary>力学模型: 1-单跨简支受拉 2-单跨简支受压 3-双跨连续 4-三跨连续 5-多跨铰接静定 6-多跨铰接超静定</summary>
    public int MechanicalModel { get; set; } = 1;

    /// <summary>立柱跨度L (mm)</summary>
    public double Span { get; set; } = 3600;

    /// <summary>短跨长L1 (mm)，双跨/多跨时</summary>
    public double ShortSpan { get; set; }

    /// <summary>立柱材质: 铝合金/钢材</summary>
    public string Material { get; set; } = "铝合金";

    /// <summary>铝合金牌号: 6063-T5/6063-T6/6061-T4/6061-T6</summary>
    public string AluminumGrade { get; set; } = "6063-T5";

    /// <summary>钢材牌号: Q235B/Q345B</summary>
    public string SteelGrade { get; set; } = "Q235B";

    /// <summary>是否断热铝</summary>
    public bool ThermalBreak { get; set; }

    /// <summary>选中的型材代号（从ProfileColumns表）</summary>
    public string? ProfileCode { get; set; }

    /// <summary>型材惯性矩 Ix (cm⁴)</summary>
    public double Ix { get; set; }

    /// <summary>型材惯性矩 Iy (cm⁴)</summary>
    public double Iy { get; set; }

    /// <summary>抗弯截面模量 Wx1 (cm³)</summary>
    public double Wx1 { get; set; }

    /// <summary>抗弯截面模量 Wx2 (cm³)</summary>
    public double Wx2 { get; set; }

    /// <summary>壁厚 (mm)</summary>
    public double WallThickness { get; set; }

    /// <summary>截面积 (mm²)</summary>
    public double SectionArea { get; set; }

    /// <summary>面积矩 Sx (cm³)</summary>
    public double Sx { get; set; }

    /// <summary>腹板总宽 tw (mm)</summary>
    public double WebWidth { get; set; }

    /// <summary>荷载宽度/从属宽度 (mm)</summary>
    public double LoadWidth { get; set; }
}

/// <summary>立柱验算结果</summary>
public class Step3CalcResult
{
    /// <summary>风荷载线荷载 qw (kN/m)</summary>
    public double WindLineLoad { get; set; }
    /// <summary>地震线荷载 qE (kN/m)</summary>
    public double SeismicLineLoad { get; set; }
    /// <summary>设计荷载组合 q (kN/m)</summary>
    public double DesignLoad { get; set; }
    /// <summary>最大弯矩 M (kN·m)</summary>
    public double MaxMoment { get; set; }
    /// <summary>抗弯应力 σ (MPa)</summary>
    public double BendingStress { get; set; }
    /// <summary>许用应力 f (MPa)</summary>
    public double AllowableStress { get; set; }
    /// <summary>需要惯性矩 Ix,需要 (cm⁴)</summary>
    public double RequiredIx { get; set; }
    /// <summary>挠度 df (mm)</summary>
    public double Deflection { get; set; }
    /// <summary>挠度限值 L/180 (mm)</summary>
    public double DeflectionLimit { get; set; }
    /// <summary>最大剪力 V (kN)</summary>
    public double MaxShear { get; set; }
    /// <summary>剪应力 τ (MPa)</summary>
    public double ShearStress { get; set; }
    /// <summary>许用剪应力 fv (MPa)</summary>
    public double AllowableShear { get; set; }
    /// <summary>强度通过</summary>
    public bool BendingPass { get; set; }
    /// <summary>惯性矩通过</summary>
    public bool IxPass { get; set; }
    /// <summary>挠度通过</summary>
    public bool DeflectionPass { get; set; }
    /// <summary>抗剪通过</summary>
    public bool ShearPass { get; set; }
}

// ===== Step 4: 横梁计算 =====

/// <summary>步骤4：横梁计算</summary>
public class Step4TransomInput
{
    /// <summary>横梁跨度L (mm) — 自动=左分格宽+右分格宽</summary>
    public double Span { get; set; }

    /// <summary>荷载模型: 三角形分布/均布</summary>
    public string LoadModel { get; set; } = "三角形分布";

    /// <summary>横梁材质: 铝合金/钢材</summary>
    public string Material { get; set; } = "铝合金";

    /// <summary>牌号</summary>
    public string Grade { get; set; } = "6063-T5";

    /// <summary>选中型材代号（从ProfileBeams表）</summary>
    public string? ProfileCode { get; set; }

    /// <summary>Ixx (cm⁴)</summary>
    public double Ixx { get; set; }
    /// <summary>Iyy (cm⁴)</summary>
    public double Iyy { get; set; }
    /// <summary>Wxx (cm³)</summary>
    public double Wxx { get; set; }
    /// <summary>Wyy (cm³)</summary>
    public double Wyy { get; set; }

    /// <summary>玻璃自重 (kN/m) — 从Step2自动计算</summary>
    public double GlassWeight { get; set; }
}

/// <summary>横梁验算结果</summary>
public class Step4CalcResult
{
    /// <summary>水平向弯矩 Mx (kN·m)</summary>
    public double MxMax { get; set; }
    /// <summary>竖向弯矩 My (kN·m)</summary>
    public double MyMax { get; set; }
    /// <summary>水平挠度 dfx (mm)</summary>
    public double DeflectionX { get; set; }
    /// <summary>竖向挠度 dfy (mm)</summary>
    public double DeflectionY { get; set; }
    /// <summary>双向弯曲应力 σ (MPa)</summary>
    public double CombinedStress { get; set; }
    /// <summary>许用应力 f (MPa)</summary>
    public double AllowableStress { get; set; }
    /// <summary>合挠度 (mm)</summary>
    public double CombinedDeflection { get; set; }
    /// <summary>挠度限值 L/180 (mm)</summary>
    public double DeflectionLimit { get; set; }
    /// <summary>强度通过</summary>
    public bool StressPass { get; set; }
    /// <summary>挠度通过</summary>
    public bool DeflectionPass { get; set; }
}

// ===== Step 5: 连接件计算 =====

/// <summary>步骤5：连接件计算 — 三个节点</summary>
public class Step5ConnectionInput
{
    /// <summary>横梁-角码连接</summary>
    public BoltConnectionNode TransomToBracket { get; set; } = new();
    /// <summary>角码-立柱连接</summary>
    public BoltConnectionNode BracketToMullion { get; set; } = new();
    /// <summary>立柱-主体连接</summary>
    public MainConnectionNode MullionToStructure { get; set; } = new();

    // --- 兼容旧字段 ---
    public string? ConnectionType { get; set; }
    public double DesignLoad { get; set; }
    public double BoltDiameter { get; set; }
    public int BoltCount { get; set; }
    public double BoltStrength { get; set; }
}

/// <summary>螺栓连接节点</summary>
public class BoltConnectionNode
{
    /// <summary>螺栓型号: M5/M6/M8/M10/M12</summary>
    public string BoltSpec { get; set; } = "M6";
    /// <summary>螺栓材质: 碳钢4.8/碳钢8.8/不锈钢A2-70</summary>
    public string BoltMaterial { get; set; } = "不锈钢A2-70";
    /// <summary>螺栓数量</summary>
    public int BoltCount { get; set; } = 4;
    /// <summary>最薄壁厚 (mm)</summary>
    public double MinWallThickness { get; set; } = 3;
}

/// <summary>立柱-主体连接节点</summary>
public class MainConnectionNode
{
    /// <summary>连接方式: 螺栓/焊接</summary>
    public string ConnectionMethod { get; set; } = "螺栓";
    /// <summary>螺栓参数（螺栓连接时）</summary>
    public BoltConnectionNode? BoltParams { get; set; }
    /// <summary>焊缝高度 hf (mm)</summary>
    public double WeldHeight { get; set; }
    /// <summary>焊缝有效长度 (mm)</summary>
    public double WeldEffectiveLength { get; set; }
}

// ===== Step 6: 预埋件与其他验算 =====

/// <summary>步骤6：预埋件与其他验算</summary>
public class Step6EmbeddedInput
{
    // --- 预埋件 ---
    /// <summary>锚筋直径 (mm): 8/10/12/14/16</summary>
    public double AnchorDiameter { get; set; } = 12;
    /// <summary>锚筋根数</summary>
    public int AnchorCount { get; set; } = 4;
    /// <summary>锚板宽 (mm)</summary>
    public double AnchorPlateWidth { get; set; } = 150;
    /// <summary>锚板高 (mm)</summary>
    public double AnchorPlateHeight { get; set; } = 200;
    /// <summary>锚板厚 (mm)</summary>
    public double AnchorPlateThickness { get; set; } = 10;

    // --- 转接件 ---
    /// <summary>槽钢型号: 8#/10#/12#/14#</summary>
    public string ChannelSpec { get; set; } = "10#";
    /// <summary>布置方式: 单片/双片</summary>
    public string ChannelLayout { get; set; } = "双片";

    // --- 结构胶 ---
    /// <summary>胶缝宽度 Cs (mm)</summary>
    public double SealantWidth { get; set; } = 12;
    /// <summary>胶缝厚度 ts (mm)</summary>
    public double SealantDepth { get; set; } = 6;
    /// <summary>胶的设计强度 (MPa)</summary>
    public double SealantStrength { get; set; } = 0.14;

    // --- 压板 ---
    /// <summary>压板宽度 (mm)</summary>
    public double PressPlateWidth { get; set; } = 25;
    /// <summary>压板厚度 (mm)</summary>
    public double PressPlateThickness { get; set; } = 2;

    // --- 兼容旧字段 ---
    public double GlueWidth { get; set; }
    public double GlueDepth { get; set; }
    public double GlueLength { get; set; }
    public double DesignStrength { get; set; } = 0.14;
    public double WindLoad { get; set; }
    public double SelfWeight { get; set; }
}

// ===== Step 7: 胶类/伸缩/紧固计算 =====

/// <summary>步骤7：胶类、伸缩及紧固计算</summary>
public class Step7SealantData
{
    // 公用参数
    public string BuildingType { get; set; } = "框架";
    public double ExpansionJoint { get; set; } = 20; // mm
    public bool CalcSubFrame { get; set; } = false;
    public bool NoClampCalc { get; set; } = false;
    public bool OpenStyle { get; set; } = false;

    // 全隐框参数
    public bool NoGravityOnSealant { get; set; } = false;
    public string StandardChoice { get; set; } = "按JGJ102规范";

    // 耐候胶
    public double WeatherSealantDisplacement { get; set; } = 0.25;
    public double WeatherSealantGap { get; set; } = 16;

    // 结构胶 - 与铝框连接
    public int FrameSealantThickness { get; set; } = 6;
    public int FrameSealantWidth { get; set; } = 7;
    // 结构胶 - 中空层
    public int HollowSealantThickness { get; set; } = 6;
    public int HollowSealantWidth { get; set; } = 7;
    // 结构胶其他
    public double SealantDisplacement { get; set; } = 0.125;
    public double LongTermStrength { get; set; } = 0.01;
    public double ShortTermStrength { get; set; } = 0.2;

    // 板块对接
    public string JointMethod { get; set; } = "玻璃对接";
    // 中空玻璃形式
    public string HollowGlassForm { get; set; } = "等片";

    // 压板
    public double ClampLength { get; set; } = 50;
    public double ClampWidth { get; set; } = 45;
    public double ClampThickness { get; set; } = 6;
    public double ClampSpacing { get; set; } = 350;
    public string ClampMaterial { get; set; } = "6063-T5";

    // 螺钉选择
    public int SelectedScrewIndex { get; set; } = 0;

    // 验算结果
    public double FrameSealantUtilization { get; set; }
    public bool FrameSealantPass { get; set; } = true;
    public double HollowSealantUtilization { get; set; }
    public bool HollowSealantPass { get; set; } = true;
    public double WeatherSealantUtilization { get; set; }
    public bool WeatherSealantPass { get; set; } = true;
    public double ClampUtilization { get; set; }
    public bool ClampPass { get; set; } = true;
    public double ScrewUtilization { get; set; }
    public bool ScrewPass { get; set; } = true;
    public double ExpansionJointUtilization { get; set; }
    public bool ExpansionJointPass { get; set; } = true;
}

// ===== Step 8: 计算结果汇总（无输入，只显示）=====

/// <summary>步骤7验算汇总行</summary>
public class VerificationSummaryItem
{
    /// <summary>构件名称</summary>
    public string Component { get; set; } = "";
    /// <summary>验算项目</summary>
    public string CheckItem { get; set; } = "";
    /// <summary>计算值</summary>
    public double CalcValue { get; set; }
    /// <summary>限值</summary>
    public double LimitValue { get; set; }
    /// <summary>利用率 (%)</summary>
    public double Utilization { get; set; }
    /// <summary>单位</summary>
    public string Unit { get; set; } = "";
    /// <summary>是否通过</summary>
    public bool Pass { get; set; }
    /// <summary>对应步骤编号（点击跳回）</summary>
    public int StepNumber { get; set; }
}

// ===== Step 8: 计算书输出 =====

/// <summary>步骤8：计算书输出</summary>
public class Step8ReportInput
{
    /// <summary>工程名称（封面）</summary>
    public string? EngineeringName { get; set; }
    /// <summary>设计单位（封面）</summary>
    public string? DesignCompany { get; set; }
    /// <summary>编制人</summary>
    public string? Author { get; set; }
    /// <summary>审核人</summary>
    public string? Reviewer { get; set; }
    /// <summary>批准人</summary>
    public string? Approver { get; set; }
    /// <summary>审定</summary>
    public string? Supervisor { get; set; }
    /// <summary>日期</summary>
    public string? ReportDate { get; set; }
    /// <summary>输出格式列表: ["pdf", "word"]</summary>
    public List<string> Formats { get; set; } = new() { "word" };
    /// <summary>输出内容: all / summary / chapters</summary>
    public string OutputScope { get; set; } = "all";
    /// <summary>选择的章节列表（OutputScope=chapters时）</summary>
    public List<string>? SelectedChapters { get; set; }
}

// ===== 兼容旧模型（标记过期但保留编译）=====

/// <summary>旧步骤2输入 — 已合并到Step1</summary>
[Obsolete("风荷载参数已合并到Step1BasicInput")]
public class Step2WindInput
{
    public double PanelWidth { get; set; }
    public double PanelHeight { get; set; }
    public double GustFactor { get; set; }
    public double ShapeFactor { get; set; }
    public double HeightFactor { get; set; }
}

/// <summary>旧步骤3输入 — 已合并到Step1</summary>
[Obsolete("地震参数已合并到Step1BasicInput")]
public class Step3SeismicInput
{
    public double MaxInfluenceFactor { get; set; }
    public double CharacteristicPeriod { get; set; }
    public double DampingRatio { get; set; } = 0.04;
    public double StructurePeriod { get; set; }
    public double FunctionCoeff { get; set; } = 1.0;
}

/// <summary>旧步骤4输入 — 改为Step2GlassInput</summary>
[Obsolete("使用Step2GlassInput")]
public class Step4GlassInput
{
    public string? GlassDescription { get; set; }
    public double EquivalentThickness { get; set; }
    public double DesignStrength { get; set; }
    public double ElasticModulus { get; set; } = 72000;
}

/// <summary>旧步骤5输入 — 拆分为Step3+Step4</summary>
[Obsolete("使用Step3MullionInput和Step4TransomInput")]
public class Step5FrameInput
{
    public MemberInput? Column { get; set; }
    public MemberInput? Beam { get; set; }
}

/// <summary>构件输入（保留兼容）</summary>
public class MemberInput
{
    public string ProfileCode { get; set; } = "";
    public string SupportType { get; set; } = "简支梁";
    public double Span { get; set; }
    public double Ix { get; set; }
    public double Wx { get; set; }
    public double Area { get; set; }
    public double LoadWidth { get; set; }
    public double AllowableStress { get; set; } = 100;
}

/// <summary>旧步骤6输入</summary>
[Obsolete("使用Step5ConnectionInput")]
public class Step6ConnectionInput
{
    public string? ConnectionType { get; set; }
    public double DesignLoad { get; set; }
    public double BoltDiameter { get; set; }
    public int BoltCount { get; set; }
    public double BoltStrength { get; set; }
}

/// <summary>旧步骤7输入</summary>
[Obsolete("使用Step6EmbeddedInput")]
public class Step7SiliconeInput
{
    public double GlueWidth { get; set; }
    public double GlueDepth { get; set; }
    public double GlueLength { get; set; }
    public double DesignStrength { get; set; } = 0.14;
    public double WindLoad { get; set; }
    public double SelfWeight { get; set; }
}
