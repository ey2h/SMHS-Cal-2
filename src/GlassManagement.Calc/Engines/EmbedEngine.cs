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
    //  ★ 焊缝几何（周长 / 形心 / 惯性矩 / 最外缘）**全部由 hf + Lh + Lv + 焊接形式算出**，
    //    不再抄轨迹常量。两个分支在 V31 里是两段独立代码：
    //      L 型焊   0x1A63DCF–0x1A65000（`embed_trace_vartrace.json`）
    //      三边围焊 0x1A6D340–0x1A6E100（`embed3side_vartrace.json`）
    //    实机扰动（`v31runs/embed_weld_probe.txt`）：Lv 100→120 焊缝强度 12.452→9.596、
    //    Lh 50→60 →11.761、hf 6→8 →10.112、切三边围焊 →7.287，四组全部由本算式复现。

    /// <summary>焊脚尺寸 hf mm（UI 下拉 `(680,142)`，候选 4..10，实机 6）。</summary>
    public double WeldLeg { get; init; } = 6;
    /// <summary>水平焊缝长 Lh mm（UI 文本框 `(680,164)`，实机 50）。</summary>
    public double WeldHorizontalMm { get; init; } = 50;
    /// <summary>竖向焊缝长 Lv mm（UI 文本框 `(680,185)`，实机 100）。</summary>
    public double WeldVerticalMm { get; init; } = 100;
    /// <summary>焊接形式（UI 两个 OptionButton：L 型焊默认 chk=1 / 三边围焊）。</summary>
    public WeldType WeldType { get; init; } = WeldType.LShape;
    /// <summary>角焊缝强度增大系数（GB50017 的 1.22）。</summary>
    public double WeldBetaF { get; init; } = 1.22;
}

/// <summary>焊接形式：V31 侧是两个独立单选钮，代码也分成两段。</summary>
public enum WeldType
{
    /// <summary>L 型焊（默认）：竖向 + 顶部水平各一条，周长 = (Lv−2hf) + (Lh−2hf)。</summary>
    LShape = 0,
    /// <summary>三边围焊：竖向 + 上下两条水平，周长 = Lv + 2·Lh − 6hf。</summary>
    ThreeSide = 1,
}

/// <summary>
/// 焊缝群几何（三面围焊 / L 型焊）。全部取自 V31 运行期 Variant 轨迹：
/// 每个中间量都能在 <c>v31runs/embed_trace_vartrace.json</c>（L 型）或
/// <c>v31runs/embed3side_vartrace.json</c>（三边）里找到对应的 `__vbaVar*` 运算。
/// </summary>
public readonly record struct WeldGeometry(
    double Throat,      // he = 0.7·hf
    double LegV,        // Lv' = Lv − 2hf
    double LegH,        // Lh' = Lh − 2hf
    double Perimeter,   // 计算周长
    double Area,        // he × 周长
    double Centroid,    // 形心到最远纤维（= 计算 W 时用的 ymax）
    double Inertia,     // 焊缝群惯性矩 mm⁴
    double SectionModulus);

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
    double AlphaR,              // 锚筋层数（排数）影响系数，2 层时 = 1.0
    double AlphaV,              // 锚筋抗剪影响系数 αv = (4−0.08d)·√(fc/fy)
    double AlphaB,              // 锚板厚度影响系数 αb = 0.6 + 0.25t/d
    double WeldArea,            // 焊缝计算面积 mm²
    double WeldSectionModulus); // 焊缝截面模量 mm³

/// <summary>
/// V31 埋件与焊缝计算引擎（土建预埋 + L 型转接件 + 焊缝：L 型焊 / 三边围焊）。
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
        // ★ 命名订正（2026-10-26 轨迹实证）：`(4−0.08d)·√(fc/fy)` 是**抗剪**影响系数 αv
        //   （V 项分母里那个 0.638 / 0.605，0x1924378），不是 αr；
        //   αr（锚筋层数影响）恒为 1.0（2 层，0x1924359 里的 Div 1716/1）。
        double alphaV = R((4.0 - 0.08 * x.RebarDiameter)
                          * Math.Sqrt(x.ConcreteFc / x.RebarFy));                  // 0.638
        double alphaR = 1.0;                                                       // 2 层锚筋
        // α_b = 0.6 + 0.25·t/d
        double alphaB = R(0.6 + 0.25 * x.PlateThickness / x.RebarDiameter);        // 0.767

        double asV = shear / (alphaR * alphaV * x.RebarFy);                        // 9.9617
        double asN = axial / (0.8 * alphaR * alphaB * x.RebarFy);                  // 42.8455
        // ★ 两个分支的构成不同（轨迹 0x1924408 与 0x1924A7D）：
        //   分支1 = V项 + N项 + M/(1.3·αr·αb·fy·z) = 9.9617+42.8455+4.6357 = 57.443  (0x19244C4)
        //   分支2 =        N项 + M/(0.4·αr·αb·fy·z) =     42.8455+15.0659 = 57.911   (0x1924A8A)
        //   分支2 **不含剪力项** —— 不是笔误，代码就是这么加的两个 Add。
        //   **屏幕显示取 max**（0x1925120 定稿点），**但 la 恒用分支2**（见下）。
        double asM13 = moment / 1.3 / alphaR / alphaB / x.RebarFy / x.AnchorZ;     // 4.6357
        double asM04 = moment / 0.4 / alphaR / alphaB / x.RebarFy / x.AnchorZ;     // 15.0659
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
        // la = lab × (round3(分支2) / As实配)（0x1926B20 / 0x1926B3F）
        // ★ 两处都不能用 max：HRB400 实测 0x1926B20 的分子是 **52.120**（分支2 取 3 位），
        //   而屏幕/1.4 判断用的是 52.188（分支1）。HPB300 时分支2（57.911）恰是较大值，
        //   所以旧实现用 max 也能对上，只在 HRB400 暴露 0.066 的差。
        // ★ 分子必须先 round3：用原始 branch2（57.91141532…）会得 57.508，实机是 57.507。
        double asReqForLa = R(branch2);                                            // 57.911 / 52.120
        double la = R(lab * (asReqForLa / asActual));                              // HPB300 57.507 / HRB400 50.319

        // ── 转接件（0x1A66594–0x1A66CF7 / 0x1A67492–0x1A67D98） ──
        double sigmaTransfer = axial / x.TransferArea
                               + moment / (x.GammaX * x.TransferSectionModulus);   // 8.8614
        double transferStress = R(R(sigmaTransfer) / 2.0);                         // 4.43
        double transferDefl = R6(R6(shear / x.DeadFactor * Math.Pow(x.CantileverMm, 3)
                                    / (3.0 * x.SteelE * x.TransferInertia)) / 2.0); // 0.000276
        double deflLimit = R(x.CantileverMm / 125.0);                              // 0.64

        // ── 焊缝（几何全部由 hf/Lh/Lv/焊接形式 算出，见 ComputeWeldGeometry） ──
        var weld = ComputeWeldGeometry(x);
        double weldArea = weld.Area;                                               // L 型 529.2 / 三边 688.8
        double weldW = weld.SectionModulus;                                        // L 型 8214.426 / 三边 19032.188
        double sN = axial / (x.WeldBetaF * weldArea);
        double sM = moment / (x.WeldBetaF * weldW);
        double sSum = sN + sM;
        double tau = shear / weldArea;
        double weldStress = R(Math.Sqrt(sSum * sSum + tau * tau) / 2.0);           // 12.452 / 7.287

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
            AlphaV: alphaV,
            AlphaB: alphaB,
            WeldArea: weldArea,
            WeldSectionModulus: weldW);
    }

    /// <summary>V31 默认工程（构件式全隐框，用于逐位对拍）。</summary>
    public static EmbedInputs DefaultInputs() => new() { WkKpa = 1.285 };

    /// <summary>
    /// 焊缝群几何。算式逐条来自运行期 Variant 轨迹，**不是**按教科书重写：
    ///
    /// <para><b>L 型焊</b>（`embed_trace_vartrace.json`，默认参数 hf=6/Lh=50/Lv=100）：</para>
    /// <code>
    /// he = 0.7·hf = 4.2                     0x1A63F39
    /// Lv' = Lv−2hf = 88   Lh' = Lh−2hf = 38 0x1A6429F / 0x1A642E4（Sub 未被 hook，88/38 为结果）
    /// P  = Lv'+Lh' = 126                    0x1A642FE   ← L 型只有两条焊缝
    /// A  = he·P = 529.2                     0x1A6430B
    /// yTop = (Lv'² − Lh'·he)/2/P = 30.0968  0x1A64580(Pow) 0x1A645E4(38×4.2) 0x1A64605(/2) 0x1A64676(/P)
    ///      ★ 分母是**另算的一条**，不复用上面的 P：
    ///        Add(Lv,Lh)=150  0x1A64636 → Mul(4,hf)=24  0x1A6465C → Sub=126  0x1A64664
    ///        即源码写的是 `/ (Lh + Lv − 4·hf)`，与 Lv'+Lh' 代数恒等（v31runs 轨迹实值验证）。
    /// yBar = Lv' − yTop = 57.903            取 3 位（0x1A64F45 的除数就是 57.903）
    /// d1   = yBar − Lv'/2 = 13.903         0x1A64C75(/2) → 0x1A64CA8(Pow)
    /// d2   = yTop + he/2  = 32.197         0x1A64D5E(/2) 0x1A64D6B（加的是**取 3 位后**的 30.097）
    /// I    = he·Lv'³/12 + Lh'·he³/12 + he·Lv'·d1² + he·Lh'·d2²   0x1A64D99 = 475639.887
    /// W    = I / yBar = 8214.426            0x1A64F45
    /// </code>
    /// <para>几何含义：竖向焊缝在下、水平焊缝在**顶**（所以 d2 = 46.1 是 90.1−57.903），
    /// ymax 取下侧最远纤维 57.903。</para>
    ///
    /// <para><b>三边围焊</b>（`embed3side_vartrace.json`，同一参数）：</para>
    /// <code>
    /// P  = 2·Lh + Lv − 6hf = 164            0x1A6D7E9(2×50) 0x1A6D7F6(+100) 0x1A6D81C(6×6) 0x1A6D836
    /// A  = he·P = 688.8                     0x1A6D836
    /// I  = (he/12)·Lv'³ + Lh'·he³/6 + 2·he·Lh'·(Lv'/2+he/2)²     0x1A6DDD3 = 917351.456
    ///      ↑ 水平焊缝两条 ⇒ 自身惯性矩除以 6（不是 12），面积 2·he·Lh' = 319.2
    /// ymax = Lv'/2 + he = 48.2              0x1A6DF6B
    /// W  = I / ymax = 19032.188             0x1A6DF78
    /// </code>
    /// <para>三边围焊上下对称 ⇒ 形心在中和轴，故平行轴距离是 (Lv'/2 + he/2)。</para>
    /// </summary>
    public static WeldGeometry ComputeWeldGeometry(EmbedInputs x)
    {
        double hf = x.WeldLeg;
        double he = 0.7 * hf;
        double lv = x.WeldVerticalMm - 2.0 * hf;
        double lh = x.WeldHorizontalMm - 2.0 * hf;

        if (x.WeldType == WeldType.ThreeSide)
        {
            double p3 = 2.0 * x.WeldHorizontalMm + x.WeldVerticalMm - 6.0 * hf;   // 0x1A6D7F6 / 0x1A6D81C
            double a3 = R(he * p3);
            double own3 = he / 12.0 * Math.Pow(lv, 3) + lh * Math.Pow(he, 3) / 6.0;
            double d3 = lv / 2.0 + he / 2.0;                                      // 46.1
            double i3 = R(own3 + 2.0 * he * lh * d3 * d3);                        // 0x1A6DDD3
            double ymax3 = lv / 2.0 + he;                                         // 48.2
            return new WeldGeometry(he, lv, lh, p3, a3, ymax3, i3, R(i3 / ymax3));
        }

        double p = lv + lh;                                                       // 0x1A642FE
        double a = R(he * p);                                                     // 0x1A6430B
        double yTopRaw = (lv * lv - lh * he) / 2.0 / p;                           // 0x1A64676 → 30.0968
        double yTop = R(yTopRaw);                                                 // 30.097
        double yBar = R(lv - yTopRaw);                                            // 57.903
        double d1 = R(yBar - lv / 2.0);                                           // 13.903
        double d2 = yTop + he / 2.0;                                              // 32.197
        double iw = R(he * Math.Pow(lv, 3) / 12.0
                      + lh * Math.Pow(he, 3) / 12.0
                      + he * lv * d1 * d1
                      + he * lh * d2 * d2);                                       // 0x1A64D99
        return new WeldGeometry(he, lv, lh, p, a, yBar, iw, R(iw / yBar));        // 8214.426
    }
}
