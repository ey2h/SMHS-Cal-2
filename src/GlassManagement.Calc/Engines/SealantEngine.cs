using System;

namespace GlassManagement.Calc.Engines;

/// <summary>胶类、伸缩及紧固计算屏输入（V31 客户区 784×537）。</summary>
public sealed record SealantInputs
{
    /// <summary>面板口径风荷载标准值 kPa（UI 只读；**注意不是支承口径**，实机 1.37）。</summary>
    public double WkPanelKpa { get; init; }

    /// <summary>玻璃自重面荷载 N/mm²（轨迹 0xC5416B 的 0.0001536 = 6mm 玻璃）。</summary>
    public double GlassWeight { get; init; } = 0.0001536;

    /// <summary>结构胶宽度公式里的面荷载 N/mm²（轨迹 0xC54835 的 0.0002，来源未定位）。</summary>
    public double SealantLoad { get; init; } = 0.0002;

    /// <summary>地震动力放大系数 × αmax（轨迹 0xC5416B 的 0.8 = 5×0.16）。</summary>
    public double QuakeFactor { get; init; } = 0.8;

    /// <summary>分格宽 mm（轨迹 0xC545A6 的 1100）。</summary>
    public double BayWidth { get; init; } = 1100;

    /// <summary>层高 mm（轨迹 0xC54854 的 1380）。</summary>
    public double StoryHeight { get; init; } = 1380;

    /// <summary>结构胶变位能力 %（轨迹 0xC548C5 的 0.01）。</summary>
    public double SealantMovement { get; init; } = 0.01;

    /// <summary>风荷载分项系数（1.5）。</summary>
    public double WindFactor { get; init; } = 1.5;

    /// <summary>地震组合值系数（0.7）。</summary>
    public double SeismicComboFactor { get; init; } = 0.7;

    // ── 耐候胶与伸缩缝 ──

    /// <summary>耐候胶线膨胀系数（轨迹 0xC596D7 的 1/100000）。</summary>
    public double WeatherLinearExp { get; init; } = 1.0 / 100000.0;
    /// <summary>伸缩缝线膨胀系数（轨迹 0xC5D288 的 2.3/100000）。</summary>
    public double JointLinearExp { get; init; } = 2.3 / 100000.0;
    /// <summary>计算温差 ℃（轨迹的 49）。</summary>
    public double TempDelta { get; init; } = 49;
    /// <summary>耐候胶位移能力（轨迹 0xC59823 的 0.25）。</summary>
    public double WeatherMovement { get; init; } = 0.25;
    /// <summary>构造余量 mm（轨迹 +3）。</summary>
    public double JointMargin1 { get; init; } = 3;
    /// <summary>施工余量 mm（轨迹 +2）。</summary>
    public double JointMargin2 { get; init; } = 2;
    /// <summary>伸缩缝计算跨 mm（轨迹 0xC5D38E 的 3000）。</summary>
    public double JointSpan { get; init; } = 3000;

    // ── 玻璃压板 ──

    /// <summary>侧压系数（轨迹 0xC7796F 的 1.25）。</summary>
    public double SidePressureFactor { get; init; } = 1.25;
    /// <summary>压板间距 mm（轨迹 0xC7798E 的 350）。</summary>
    public double ClampSpacing { get; init; } = 350;
    /// <summary>压板计算力臂系数（轨迹 0xC77B39 的 0.5）。</summary>
    public double ClampLeverFactor { get; init; } = 0.5;
    /// <summary>压板悬臂 mm（轨迹 0xC77B58 的 45）。</summary>
    public double ClampArm { get; init; } = 45;
    /// <summary>压板宽度 mm（轨迹 0xC77DA5 的 6）。</summary>
    public double ClampWidth { get; init; } = 6;
    /// <summary>压板截面模量 mm³（轨迹 0xC77DE3 的 44）。</summary>
    public double ClampSectionModulus { get; init; } = 44;
    /// <summary>每块压板螺栓数（轨迹 0xC7883F 的 2）。</summary>
    public double ClampBoltCount { get; init; } = 2;

    // ── 压板连接螺栓 ──

    /// <summary>螺栓有效直径 mm（轨迹 0xC78725 的 4.249467）。</summary>
    public double BoltEffective { get; init; } = 4.249467;
    /// <summary>螺栓抗拉强度设计值 N/mm²（轨迹 0xC78751 的 170）。</summary>
    public double BoltTension { get; init; } = 170;
    /// <summary>螺栓受拉面数（轨迹 0xC78732 的 3.14 → πd²/4）。</summary>
    public double BoltPi { get; init; } = 3.14;

    // ── 材料容许值（计算书右侧的"设计值"） ──

    /// <summary>压板抗弯强度设计值 MPa（6063-T5 → 90）。</summary>
    public double ClampBendingLimit { get; init; } = 90;
    /// <summary>压板抗剪强度设计值 MPa（55）。</summary>
    public double ClampShearLimit { get; init; } = 55;
}

/// <summary>胶类、伸缩及紧固计算结果（字段顺序＝计算书段落顺序）。</summary>
public readonly record struct SealantResult(
    double WkPanelKpa,          // 面板部分风荷载标准值
    double StructuralWidth,     // 结构胶宽度最小值 mm
    double StructuralThickness, // 结构胶厚度最小值 mm
    double ActualWidth,         // 实际结构胶宽度 mm（UI）
    double ActualThickness,     // 实际结构胶厚度 mm（UI）
    double WeatherWidth,        // 耐候胶胶缝计算宽度 mm
    double WeatherActual,       // 实际胶缝 mm（UI）
    double JointWidth,          // 立柱伸缩缝计算值 mm
    double JointActual,         // 实际缝 mm（UI）
    double ClampBending,        // 压板最大弯曲应力 MPa
    double ClampBendingLimit,
    double ClampShear,          // 压板最大剪应力 MPa
    double ClampShearLimit,
    double BoltCapacity,        // 连接螺栓抗拉承载力设计值 N
    double BoltForce,           // 连接螺栓实际受力 N
    // 中间量
    double QuakeLoad,
    double CombinedLoad,
    double ClampLoad);

/// <summary>
/// V31 胶类、伸缩及紧固计算引擎。
///
/// 算式来自 `0xC51197–0xC60814`（结构胶/耐候胶/伸缩缝）与 `0xC735A1–0xC78B67`
/// （压板与螺栓）的 `__vbaVar*` 数值轨迹（v31runs/sealant_trace_vartrace.json）。
///
/// ★ 本屏 wk 用**面板口径**（1.37 kPa），不是支承口径（1.285）——与玻璃屏同源。
/// </summary>
public static class SealantEngine
{
    public const int Digits = 3;

    private static double R(double v) => WindLoadEngine.RoundLikeV35(v, Digits);
    private static double R6(double v) => WindLoadEngine.RoundLikeV35(v, 6);

    public static SealantResult Compute(SealantInputs x)
    {
        double wk = x.WkPanelKpa / 1000.0;

        // ── 荷载组合（0xC5416B / 0xC54554 / 0xC5457A / 0xC54587） ──
        double quake = R6(x.QuakeFactor * x.GlassWeight);                           // 0.000123
        double combined = R6(x.WindFactor * wk + x.SeismicComboFactor * quake);     // 0.002141

        // ── 结构胶宽度（0xC54835–0xC548C5） ──
        //   W = 面荷载 × 分格宽 × 层高 / 2 ； 再 /(分格宽+层高) / 变位能力
        double wLine = x.SealantLoad * x.BayWidth * x.StoryHeight;                 // 303.6
        double structuralWidth = R(wLine / 2.0 / (x.BayWidth + x.StoryHeight)
                                   / x.SealantMovement);                            // 6.121

        // ── 结构胶厚度（0xC59177 / 0xC5941D–0xC5944B） ──
        //   t = (2/分格宽)×层高 / √(变位×(变位+2))
        double thickNum = (2.0 / x.BayWidth) * x.StoryHeight;                      // 2.50909
        double thickDen = Math.Sqrt(0.1 * (2.0 + 0.1));                            // 0.458258
        double structuralThickness = R(thickNum / thickDen);                       // 5.475

        // ── 耐候胶宽度（0xC596D7–0xC5984B） ──
        double weatherWidth = R(x.WeatherLinearExp * x.TempDelta * x.StoryHeight
                                / x.WeatherMovement + x.JointMargin1 + x.JointMargin2);  // 7.705

        // ── 立柱伸缩缝（0xC5D288–0xC5D3B6） ──
        double jointWidth = R(x.JointLinearExp * x.TempDelta * x.JointSpan
                              + x.JointMargin1 + x.JointMargin2);                  // 8.381

        // ── 压板（0xC776C9–0xC78039） ──
        // ★ 组合荷载是 **6 位**取整（0xC776FC 实机 0.002141），
        //   而「侧压系数×组合」这一乘**不取整**（0xC7796F 用 0.00267625 直接往下乘）。
        //   若在这里顺手 round3 成 0.002，压板应力会从 43.907 掉到 32.812 —— 被测试逮住过。
        double q2 = R6(x.WindFactor * wk + x.SeismicComboFactor * quake);          // 0.002141
        double clampLoad = x.SidePressureFactor * q2;                              // 0.00267625
        double boltForce = R(clampLoad * x.ClampSpacing * x.BayWidth / 2.0);       // 515.178
        double momentClamp = R(boltForce * x.ClampLeverFactor * x.ClampArm
                               * x.ClampWidth);                                   // 69549.03
        double clampBending = R(momentClamp / x.ClampSectionModulus
                                / (x.ClampWidth * x.ClampWidth));                  // 43.907
        double clampShear = R(x.WindFactor * boltForce / x.ClampSectionModulus
                              / x.ClampWidth);                                     // 2.927

        // ── 压板连接螺栓（0xC78725–0xC7883F） ──
        double boltCapacity = R(x.BoltPi * x.BoltEffective * x.BoltEffective
                                * x.BoltTension / 4.0);                            // 2409.836
        double boltForceTotal = R(x.ClampBoltCount * boltForce);                   // 1030.356

        return new SealantResult(
            WkPanelKpa: R(x.WkPanelKpa),
            StructuralWidth: structuralWidth,
            StructuralThickness: structuralThickness,
            ActualWidth: 8,
            ActualThickness: 6,
            WeatherWidth: weatherWidth,
            WeatherActual: 16,
            JointWidth: jointWidth,
            JointActual: 20,
            ClampBending: clampBending,
            ClampBendingLimit: x.ClampBendingLimit,
            ClampShear: clampShear,
            ClampShearLimit: x.ClampShearLimit,
            BoltCapacity: boltCapacity,
            BoltForce: boltForceTotal,
            QuakeLoad: quake,
            CombinedLoad: combined,
            ClampLoad: boltForce);
    }

    /// <summary>V31 默认工程（全隐框，面板口径 wk=1.37 kPa）。</summary>
    public static SealantInputs DefaultInputs() => new() { WkPanelKpa = 1.37 };
}
