using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 连接计算屏（构件式「全隐框」）输入。
///
/// 每个字段都标注了它在 V31 槽数组里的槽号与实机取值（默认那组），
/// 便于逐位复核。字段名是按算式位置起的**标签**，算式本身见 <see cref="ConnectEngine"/>，
/// 全部来自 V31 `0xA8FA83–0xAA0002` 的指令 + `__vbaVar*` 数值轨迹
/// （docs/V31_CONNECTION_CODE_MAP.md、v31runs/connect_trace_vartrace.json）。
/// </summary>
public sealed record ConnectInputs
{
    /// <summary>风荷载标准值 N/mm²（槽13 = 0.001285，即 1.285 kPa）。</summary>
    public double Wk { get; init; }

    /// <summary>横梁计算跨度 mm（槽2 = 1100）。</summary>
    public double BeamSpan { get; init; }

    /// <summary>立柱计算跨（层高）mm（槽3/槽83 = 1380）。</summary>
    public double StoryHeight { get; init; }

    /// <summary>幕墙分格宽 mm（槽80 = 1100）。</summary>
    public double PanelWidth { get; init; }

    /// <summary>主体支点间距 mm（槽4 = 3000）。</summary>
    public double SupportSpan { get; init; }

    /// <summary>地震动力放大系数（程序字面量 5）。</summary>
    public double BetaE { get; init; } = 5.0;

    /// <summary>地震影响系数最大值（槽15 = 0.16）。</summary>
    public double AlphaMax { get; init; } = 0.16;

    /// <summary>横梁段幕墙自重面荷载 N/mm²（槽16 = 0.0003）。</summary>
    public double DeadLoadPanel { get; init; }

    /// <summary>主体段幕墙自重面荷载 N/mm²（槽78 = 0.0004）。</summary>
    public double DeadLoadColumn { get; init; }

    /// <summary>风荷载分项系数（字面量 1.5）。</summary>
    public double WindFactor { get; init; } = 1.5;

    /// <summary>地震分项系数（字面量 1.4）。</summary>
    public double SeismicFactor { get; init; } = 1.4;

    /// <summary>地震组合值系数（字面量 0.5）。</summary>
    public double SeismicComboFactor { get; init; } = 0.5;

    /// <summary>永久荷载分项系数（字面量 1.3，用于自重项）。</summary>
    public double DeadFactor { get; init; } = 1.3;

    /// <summary>单个螺栓抗剪强度设计值 N/mm²（槽21/38/62 = 190）。</summary>
    public double BoltShearMpa { get; init; } = 190.0;

    /// <summary>横梁与角码：螺栓公称直径 mm（槽29 = 6）。</summary>
    public double BeamBoltDiameter { get; init; } = 6.0;

    /// <summary>横梁与角码：螺栓有效直径 mm（槽22 = 5.061833）。</summary>
    public double BeamBoltEffectiveDiameter { get; init; } = 5.061833;

    /// <summary>立柱与主体：螺栓公称直径 mm（槽67 = 12）。</summary>
    public double MainBoltDiameter { get; init; } = 12.0;

    /// <summary>立柱与主体：螺栓有效直径 mm（槽63 = 10.35821）。</summary>
    public double MainBoltEffectiveDiameter { get; init; } = 10.35821;

    /// <summary>立柱与主体：螺栓抗剪强度设计值 N/mm²（槽62 = 190）。</summary>
    public double MainShearMpa { get; init; } = 190.0;

    /// <summary>横梁/立柱与角码：螺栓受剪面数（轨迹 0xA936C4 段无前置 ×2 ⇒ 单剪 = 1）。</summary>
    public double BeamShearPlanes { get; init; } = 1.0;

    /// <summary>立柱与主体：螺栓受剪面数（轨迹 0xA9D117 `Mul(2, 3.14)` ⇒ 双剪 = 2）。</summary>
    public double MainShearPlanes { get; init; } = 2.0;

    /// <summary>螺栓数（槽25/42/66 = 2）。</summary>
    public double BoltCount { get; init; } = 2.0;

    /// <summary>横梁/立柱被承压板厚 mm（槽26/槽43 = 3）。</summary>
    public double WebThickness { get; init; } = 3.0;

    /// <summary>横框与立柱的角码厚度 mm（槽10 = 5）。</summary>
    public double MidBracketThickness { get; init; } = 5.0;

    /// <summary>立柱与主体的角码壁厚 mm（槽7 = 6）。</summary>
    public double MainBracketThickness { get; init; } = 6.0;

    /// <summary>铝型材 6063-T5 承压强度设计值 N/mm²（槽28/49/77/81 = 185）。</summary>
    public double AluminumBearingMpa { get; init; } = 185.0;

    /// <summary>钢 Q235 承压强度设计值 N/mm²（槽72 = 305）。</summary>
    public double SteelBearingMpa { get; init; } = 305.0;

    /// <summary>单侧角码开关（未勾选 = 2，勾选 = 1；槽85）。</summary>
    public double BracketSides { get; init; } = 2.0;

    /// <summary>立柱壁承压的壁面系数（槽84 = 2）。</summary>
    public double ColumnWallFactor { get; init; } = 2.0;
}

/// <summary>连接计算屏结果，字段顺序与计算书段落一致。</summary>
public readonly record struct ConnectResult(
    // 横梁与角码
    double BeamBoltDesign,      // 螺栓设计值 个（槽24）
    double BeamForce,           // 实际型材受力 N（槽19）
    double BeamWebBearing,      // 横梁壁抗承压 N（槽30）
    // 立柱与角码
    double ColumnBoltDesign,    // 螺栓设计值 个（槽41）
    double ColumnForce,         // 实际受力 N（槽36）
    double ColumnWebBearing,    // 立柱壁抗承压 N（槽45）
    double BracketBearing,      // 角码壁抗承压 N（槽50）
    // 立柱与主体
    double MainBoltDesign,      // 螺栓设计值 个（槽65）
    double MainForce,           // 实际受力 N（槽60）
    double MainColumnBearing,   // 立柱壁抗承压 N（槽68）
    double MainBracketBearing,  // 角码壁抗承压 N（槽73）
    // 中间量（计算书不直接显示，但参与判定与下一屏）
    double WindMomentTerm,      // 槽14
    double QuakeMomentTerm,     // 槽17
    double BeamBoltCapacity,    // 槽23
    double MainBoltCapacity,    // 槽64
    double WkKpa);              // 槽13 → kPa，计算书首行

/// <summary>
/// V31 连接计算引擎（构件式全隐框）。
///
/// 结论来源：`0xA8FA83–0xAA0002` 反汇编 + `__vbaVarMul/Div/Pow/Add` 数值轨迹
/// + 槽数组直读 + 三组扰动实验。每个数字都能追到指令地址与实参。
///
/// ★ 两个容易算错的点（都已由轨迹确证）：
///   1) 圆面积用 **π = 3.14**（程序字面量），不是 Math.PI —— 单栓承载力差 0.045%；
///   2) 取整是**逐定稿点 round3，且后半段用取整后的值**（如槽23 → 槽24）。
/// </summary>
public static class ConnectEngine
{
    /// <summary>程序里的 π 字面量（`0xA93EF6 Mul(3.14, d²)`、`0xA9D117 Mul(2, 3.14)`）。</summary>
    public const double ProgramPi = 3.14;

    /// <summary>全部定稿点都是 3 位（rtcRound digits=3）。</summary>
    public const int Digits = 3;

    private static double R(double v) => WindLoadEngine.RoundLikeV35(v, Digits);

    /// <summary>
    /// 单栓承载力设计值 = n_v·3.14·d_eff²·f_v/4。
    ///
    /// 轨迹证据：槽23（横梁/立柱与角码）是 `3.14 × d² × 190 / 4`，无前置系数 ⇒ n_v = 1；
    /// 槽64（立柱与主体）是 `Mul(2, 3.14) → ×d² → ×190 → /4` ⇒ n_v = 2（双剪）。
    /// </summary>
    public static double BoltCapacity(double effectiveDiameterMm, double shearMpa,
                                      double shearPlanes = 1.0)
        => R(shearPlanes * ProgramPi * effectiveDiameterMm * effectiveDiameterMm * shearMpa / 4.0);

    /// <summary>抗承压能力设计值 = 螺栓数 × 公称直径 × 板厚 × 承压强度（槽30/45/50/68/73）。</summary>
    public static double BearingCapacity(double boltCount, double nominalDiameterMm,
                                         double thicknessMm, double bearingMpa)
        => R(boltCount * nominalDiameterMm * thicknessMm * bearingMpa);

    public static ConnectResult Compute(ConnectInputs x)
    {
        // ── 横梁与角码 ──
        // 0xA9348A: 1.5×wk → 0xA934BD: ×L² → 0xA934D1: /4
        double windTerm = R(x.WindFactor * x.Wk * x.BeamSpan * x.BeamSpan / 4.0);      // 槽14
        // 0xA9367F: 5×αmax → 0.00024 → ×L² → /4
        double quakeTerm = R(x.BetaE * x.AlphaMax * x.DeadLoadPanel
                             * x.BeamSpan * x.BeamSpan / 4.0);                          // 槽17
        // 0xA937C4: 1.4×地震项（槽18），0xA938DE: 0.5×，0xA938EB: 加
        double beamForce = R(windTerm + x.SeismicComboFactor * R(x.SeismicFactor * quakeTerm)); // 槽19

        double beamCap = BoltCapacity(x.BeamBoltEffectiveDiameter, x.BoltShearMpa,
                                      x.BeamShearPlanes);                                // 槽23
        double beamBolt = R(beamForce / beamCap);                                        // 槽24
        double beamBearing = BearingCapacity(x.BoltCount, x.BeamBoltDiameter,
                                             x.WebThickness, x.AluminumBearingMpa);      // 槽30

        // ── 立柱与角码 ──
        // 0xA961EF: 自重×层高 → ×分格宽 → /2
        double deadReaction = R(x.DeadLoadPanel * x.StoryHeight * x.PanelWidth / 2.0);   // 槽33
        double deadTerm = R(x.DeadFactor * deadReaction);                                // 槽35
        double columnForce = R(Math.Sqrt(beamForce * beamForce + deadTerm * deadTerm));  // 槽36

        double columnCap = beamCap;                                                      // 槽40 同槽23
        double columnBolt = R(columnForce / columnCap);                                  // 槽41
        double columnBearing = BearingCapacity(x.BoltCount, x.BeamBoltDiameter,
                                               x.WebThickness, x.AluminumBearingMpa);    // 槽45
        double bracketBearing = BearingCapacity(x.BoltCount, x.BeamBoltDiameter,
                                                x.MidBracketThickness, x.AluminumBearingMpa); // 槽50

        // ── 立柱与主体 ──
        // 0xA9BC22: wk×分格宽×支点间距；0xA9BD77: 1.5×
        double mainWindBase = R(x.Wk * x.PanelWidth * x.SupportSpan);                    // 槽53
        double mainWind = R(x.WindFactor * mainWindBase);                                // 槽54
        // 0xA9C2CD: 5×αmax×自重 → ×分格宽 → ×支点间距
        double mainQuakeBase = R(x.BetaE * x.AlphaMax * x.DeadLoadColumn
                                 * x.PanelWidth * x.SupportSpan);                        // 槽55
        double mainQuake = R(x.SeismicFactor * mainQuakeBase);                           // 槽56
        double mainWindCombo = R(mainWind + x.SeismicComboFactor * mainQuake);           // 槽57
        // 0xA9C63D: 自重×分格宽×支点间距
        double mainDeadBase = R(x.DeadLoadColumn * x.PanelWidth * x.SupportSpan);        // 槽58
        double mainDead = R(x.DeadFactor * mainDeadBase);                                // 槽59
        double mainForce = R(Math.Sqrt(mainWindCombo * mainWindCombo + mainDead * mainDead)); // 槽60

        double mainCap = BoltCapacity(x.MainBoltEffectiveDiameter, x.MainShearMpa,
                                      x.MainShearPlanes);                                // 槽64
        double mainBolt = R(mainForce / mainCap);                                        // 槽65
        // 0xA9D65A: 2×2 → ×d → ×t(3) → ×185
        double mainColumnBearing = BearingCapacity(x.ColumnWallFactor * x.BoltCount,
                                                   x.MainBoltDiameter, x.WebThickness,
                                                   x.AluminumBearingMpa);                // 槽68
        // 0xA9DD63: 2(单侧系数)×2 → ×d → ×角码壁厚 → ×305
        double mainBracketBearing = BearingCapacity(x.BracketSides * x.BoltCount,
                                                    x.MainBoltDiameter, x.MainBracketThickness,
                                                    x.SteelBearingMpa);                  // 槽73

        return new ConnectResult(
            beamBolt, beamForce, beamBearing,
            columnBolt, columnForce, columnBearing, bracketBearing,
            mainBolt, mainForce, mainColumnBearing, mainBracketBearing,
            windTerm, quakeTerm, beamCap, mainCap, R(x.Wk * 1000.0));
    }

    /// <summary>V31 默认参数组（构件式全隐框，用于逐位对拍）。</summary>
    public static ConnectInputs DefaultInputs() => new()
    {
        Wk = 0.001285,
        BeamSpan = 1100,
        StoryHeight = 1380,
        PanelWidth = 1100,
        SupportSpan = 3000,
        DeadLoadPanel = 0.0003,
        DeadLoadColumn = 0.0004,
    };
}
