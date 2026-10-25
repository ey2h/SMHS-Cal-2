using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 埋件与焊缝计算屏输入。
///
/// 字段名是**标签**，算式见 <see cref="EmbedEngine"/>；每个量的来源都写在注释里：
/// 「轨迹」= V31 `__vbaVar*` 数值轨迹实测（v31runs/embed_trace_vartrace.json），
/// 「参数库」= 参数库.mdb 导出表，「UI」= 该屏控件。
/// </summary>
public sealed record EmbedInputs
{
    /// <summary>风荷载标准值 kPa（UI 只读，来自支承口径 wk）。</summary>
    public double WkKpa { get; init; }

    /// <summary>幕墙自重面荷载 N/mm²（轨迹 0x19201D5 的 0.0004）。</summary>
    public double DeadLoad { get; init; } = 0.0004;

    /// <summary>分格宽 mm（轨迹 0x19204D0 的 1100）。</summary>
    public double BayWidth { get; init; } = 1100;

    /// <summary>支点间距 / 立柱跨 mm（轨迹 0x19204EF 的 3000）。</summary>
    public double SupportSpan { get; init; } = 3000;

    /// <summary>地震动力放大系数（轨迹 0x19201B6 的 5）。</summary>
    public double BetaE { get; init; } = 5.0;

    /// <summary>地震影响系数最大值（轨迹 0x19201B6 的 0.16）。</summary>
    public double AlphaMax { get; init; } = 0.16;

    /// <summary>风荷载分项系数（轨迹 0x1920384 的 1.5）。</summary>
    public double WindFactor { get; init; } = 1.5;

    /// <summary>地震组合值系数（轨迹 0x19203AA 的 0.7）。</summary>
    public double SeismicComboFactor { get; init; } = 0.7;

    /// <summary>永久荷载分项系数（轨迹 0x192172A 的 1.3）。</summary>
    public double DeadFactor { get; init; } = 1.3;

    /// <summary>剪力对埋件的力臂 mm（轨迹 0x19221F2 的 80）。</summary>
    public double MomentArmMm { get; init; } = 80;

    // ── 锚筋（土建预埋参数 Frame） ──

    /// <summary>锚筋根数（UI 4）。</summary>
    public int RebarCount { get; init; } = 4;
    /// <summary>锚筋直径 mm（UI 12）。</summary>
    public double RebarDiameter { get; init; } = 12;
    /// <summary>锚筋抗拉强度设计值 N/mm²（HPB300 → 270，轨迹 0x1923425 的 270）。</summary>
    public double RebarFy { get; init; } = 270;
    /// <summary>混凝土轴心抗拉/抗压强度（C25 fc=11.9 / ft=1.27，轨迹 0x1923435 与 0x19268E7）。</summary>
    public double ConcreteFc { get; init; } = 11.9;
    public double ConcreteFt { get; init; } = 1.27;
    /// <summary>锚板厚度 mm（UI 8，进 α_b）。</summary>
    public double PlateThickness { get; init; } = 8;
    /// <summary>锚筋排数系数（UI 2 层，轨迹 0x19268B6 的 1.1 由抗震等级得来）。</summary>
    public double SeismicAnchorFactor { get; init; } = 1.1;
    /// <summary>锚固长度公式系数 α（光圆 0.16，轨迹 0x19268B6 的 0.16）。</summary>
    public double AnchorAlpha { get; init; } = 0.16;
    /// <summary>锚筋合力点到锚板底的距离 z mm（UI 110）。</summary>
    public double AnchorZ { get; init; } = 110;
    /// <summary>锚板面积 mm²（UI 60000 → 0.5fcA = 357000）。</summary>
    public double PlateArea { get; init; } = 60000;
    /// <summary>所选锚筋实际长度 mm（UI 200）。</summary>
    public double RebarLengthMm { get; init; } = 200;

    // ── 转接件（10# 槽钢 / 钢板）截面特性 ──
    //  以下四个值 V31 由"焊缝和转接件参数"帧里的型钢规格算出（轨迹里直接作为除数出现），
    //  复刻版先作为输入；接截面特性模块后改为由其提供（见 docs/V31_EMBED_CODE_MAP.md）。

    /// <summary>转接件毛截面面积 mm²（轨迹 0x1A66594 的 1274.8）。</summary>
    public double TransferArea { get; init; } = 1274.8;
    /// <summary>转接件截面模量 mm³（轨迹 0x1A665D9 的 39700）。</summary>
    public double TransferSectionModulus { get; init; } = 39700;
    /// <summary>转接件惯性矩 mm⁴（轨迹 0x1A67672 的 1980000）。</summary>
    public double TransferInertia { get; init; } = 1980000;
    /// <summary>截面塑性发展系数 γx（轨迹 0x1A665BA 的 1.05）。</summary>
    public double GammaX { get; init; } = 1.05;
    /// <summary>钢材弹性模量 N/mm²（轨迹 0x1A67653 的 206000）。</summary>
    public double SteelE { get; init; } = 206000;
    /// <summary>转接件悬臂长度 mm（轨迹 0x1A6761E 的 80）。</summary>
    public double CantileverMm { get; init; } = 80;

    // ── 焊缝 ──

    /// <summary>焊脚尺寸 mm（UI 6 → 有效厚度 0.7×6 = 4.2）。</summary>
    public double WeldLeg { get; init; } = 6;
    /// <summary>焊缝计算周长 mm（轨迹 0x1A642FE 的 88+38 = 126）。</summary>
    public double WeldPerimeter { get; init; } = 126;
    /// <summary>焊缝群惯性矩 mm⁴（轨迹 0x1A64D99 的 475639.887）。</summary>
    public double WeldInertia { get; init; } = 475639.887;
    /// <summary>焊缝最外缘到中和轴距离 mm（轨迹 0x1A64F45 的 57.903）。</summary>
    public double WeldYMax { get; init; } = 57.903;
    /// <summary>角焊缝强度增大系数（GB50017 的 1.22）。</summary>
    public double WeldBetaF { get; init; } = 1.22;
}

/// <summary>埋件与焊缝计算结果（字段顺序＝计算书段落顺序）。</summary>
public readonly record struct EmbedResult(
    double WkKpa,               // 风荷载标准值
    double ShearDesign,         // 剪力设计值 N
    double AxialDesign,         // 轴力设计值 N
    double MomentDesign,        // 弯矩设计值 N·mm
    double RebarAreaRequired,   // 锚筋总截面积计算值 mm²
    double RebarAreaActual,     // 实际锚筋总截面积 mm²
    double ConcreteBearing,     // 0.5·fc·A N
    double AnchorLab,           // 基本锚固长度 mm
    double AnchorLa,            // 折减后锚固长度 mm
    double TransferStress,      // 转接件强度计算值 MPa
    double TransferStressLimit, // 转接件强度设计值 MPa
    double TransferDeflection,  // 转接件挠度 mm
    double TransferDeflLimit,   // 转接件挠度设计值 mm
    double WeldStress,          // 焊缝强度计算值 MPa
    double WeldStressLimit,     // 焊缝强度设计值 MPa
    // 中间量（计算书不显示，但下游与判定要用）
    double QuakeLoad,           // 地震作用 qe N/mm²
    double CombinedLoad,        // 1.5wk + 0.7qe N/mm²
    double AlphaR,              // 锚筋层数/排数影响系数
    double AlphaB,              // 锚板厚度影响系数
    double WeldArea,            // 焊缝计算面积 mm²
    double WeldSectionModulus); // 焊缝截面模量 mm³

/// <summary>
/// V31 埋件与焊缝计算引擎（土建预埋 + L 型转接件 + 三面围焊）。
///
/// 全部算式来自 `0x191EEC6–0x1928377`（荷载与锚筋段）与 `0x1A63DCF–0x1A687FD`
/// （转接件与焊缝段）的 `__vbaVar*` 数值轨迹，每个除数/系数都能在
/// <c>v31runs/embed_trace_vartrace.json</c> 里找到对应实数。
///
/// ★ 两处「除以 2」：转接件强度与焊缝强度都再除 2（`0x1A66CF7`、`0x1A67D98`、`0x1A65B66`）。
///   数值上确证无疑，物理上应是「两侧对称连接件各担一半」，但界面没有对应开关，
///   来源**未定位**，按算式原样保留并标注。
/// </summary>
public static class EmbedEngine
{
    public const int Digits = 3;
    /// <summary>挠度定稿点用 6 位（`0x1A67DBA` 实机 0.000276）。</summary>
    public const int DigitsDeflection = 6;

    private static double R(double v) => WindLoadEngine.RoundLikeV35(v, Digits);
    private static double R6(double v) => WindLoadEngine.RoundLikeV35(v, DigitsDeflection);

    public static EmbedResult Compute(EmbedInputs x)
    {
        // ── 荷载组合（0x19201B6 / 0x1920384 / 0x19203AA / 0x19203B7） ──
        // ★ 荷载类（N/mm² 量级的小数）定稿点是 **6 位**：0x19203C4 实机 0.002151，
        //   若按 3 位取整会得 0.002，轴力随之从 7098.3 掉到 6600 —— 这条被测试逮住过。
        double quake = R6(x.BetaE * x.AlphaMax * x.DeadLoad);                      // 0.00032
        double combined = R6(x.WindFactor * x.WkKpa / 1000.0
                             + x.SeismicComboFactor * quake);                      // 0.002151

        // 剪力 = 1.3 × 自重×分格宽×支点间距（0x19204D0 / 0x19204EF / 0x192172A）
        double dead = R(x.DeadLoad * x.BayWidth * x.SupportSpan);                  // 1320
        double shear = R(x.DeadFactor * dead);                                     // 1716
        // 轴力 = 组合荷载 × 分格宽 × 支点间距（0x1921F96 / 0x1921FB5）
        double axial = R(combined * x.BayWidth * x.SupportSpan);                   // 7098.3
        // 弯矩 = 力臂 × 剪力（0x19221F2）
        double moment = R(x.MomentArmMm * shear);                                  // 137280

        // ── 锚筋（0x1923425 / 0x1923435 / 0x1923478 等） ──
        // α_r = (4 − 0.08·d)·√(fc/fy)
        double alphaR = R((4.0 - 0.08 * x.RebarDiameter)
                          * Math.Sqrt(x.ConcreteFc / x.RebarFy));                  // 0.638
        // α_b = 0.6 + 0.25·t/d
        double alphaB = R(0.6 + 0.25 * x.PlateThickness / x.RebarDiameter);        // 0.767

        double asV = shear / (alphaR * x.RebarFy);                                 // 9.9617
        double asN = axial / (0.8 * alphaB * x.RebarFy);                           // 42.8455
        // ★ 两个分支的构成不同（轨迹 0x1924408 与 0x1924A7D）：
        //   分支1 = V项 + N项 + M/(1.3·αb·fy·z) = 9.9617+42.8455+4.6357 = 57.443  (0x19244C4)
        //   分支2 =        N项 + M/(0.4·αb·fy·z) =     42.8455+15.0659 = 57.911   (0x1924A8A)
        //   分支2 **不含剪力项** —— 不是笔误，代码就是这么加的两个 Add。
        //   最终取两者较大值（实机 57.911）。
        double asM13 = moment / 1.3 / 1.0 / alphaB / x.RebarFy / x.AnchorZ;        // 4.6357
        double asM04 = moment / 0.4 / 1.0 / alphaB / x.RebarFy / x.AnchorZ;        // 15.0659
        double branch1 = asV + asN + asM13;                                        // 57.443
        double branch2 = asN + asM04;                                              // 57.911
        double asReq = R(Math.Max(branch1, branch2));                              // 57.911

        // 实配 = n·3.14·d²/4（程序 π 仍取 3.14，0x1923A9F）
        double asActual = R(ConnectEngine.ProgramPi * x.RebarDiameter * x.RebarDiameter
                            * x.RebarCount / 4.0);                                 // 452.16

        // 0.5·fc·A（0x19250DF / 0x19250FE）
        double concreteBearing = R(0.5 * x.ConcreteFc * x.PlateArea);              // 357000

        // ── 锚固长度（GB50010 9.7.4） ──
        // lab = 1.1·α·(fy/ft)·d（0x19268B6 / 0x19268E7 / 0x19268F4 / 0x1926913）
        double lab = R(x.SeismicAnchorFactor * x.AnchorAlpha
                       * (x.RebarFy / x.ConcreteFt) * x.RebarDiameter);            // 449.008
        // la = lab × (As计算 / As实配)（0x1926B20 / 0x1926B3F）
        double la = R(lab * (asReq / asActual));                                   // 57.507

        // ── 转接件（0x1A66594–0x1A66CF7 / 0x1A67492–0x1A67D98） ──
        double sigmaTransfer = axial / x.TransferArea
                               + moment / (x.GammaX * x.TransferSectionModulus);   // 8.8614
        double transferStress = R(R(sigmaTransfer) / 2.0);                         // 4.43
        double transferDefl = R6(R6(shear / x.DeadFactor * Math.Pow(x.CantileverMm, 3)
                                    / (3.0 * x.SteelE * x.TransferInertia)) / 2.0); // 0.000276
        double deflLimit = R(x.CantileverMm / 125.0);                              // 0.64

        // ── 焊缝（三面围焊，0x1A642FE–0x1A65B66） ──
        double weldArea = R(0.7 * x.WeldLeg * x.WeldPerimeter);                    // 529.2
        double weldW = R(x.WeldInertia / x.WeldYMax);                              // 8214.426
        double sN = axial / (x.WeldBetaF * weldArea);
        double sM = moment / (x.WeldBetaF * weldW);
        double sSum = sN + sM;
        double tau = shear / weldArea;
        double weldStress = R(Math.Sqrt(sSum * sSum + tau * tau) / 2.0);           // 12.452

        return new EmbedResult(
            WkKpa: R(x.WkKpa),
            ShearDesign: shear,
            AxialDesign: axial,
            MomentDesign: moment,
            RebarAreaRequired: asReq,
            RebarAreaActual: asActual,
            ConcreteBearing: concreteBearing,
            AnchorLab: lab,
            AnchorLa: la,
            TransferStress: transferStress,
            TransferStressLimit: 215,
            TransferDeflection: transferDefl,
            TransferDeflLimit: deflLimit,
            WeldStress: weldStress,
            WeldStressLimit: 160,
            QuakeLoad: quake,
            CombinedLoad: combined,
            AlphaR: alphaR,
            AlphaB: alphaB,
            WeldArea: weldArea,
            WeldSectionModulus: weldW);
    }

    /// <summary>V31 默认工程（构件式全隐框，用于逐位对拍）。</summary>
    public static EmbedInputs DefaultInputs() => new() { WkKpa = 1.285 };
}
