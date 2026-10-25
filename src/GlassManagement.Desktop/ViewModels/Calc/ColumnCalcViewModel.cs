using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>
/// 「幕墙计算-立柱计算」屏 ViewModel（框架式可见集，V35 实测：
/// 力学模型 frame98 / 立柱选材 frame74 / 玻璃配置 frame91 / 立柱选料 frame94）。
/// 参数全部读写共享 CalcSession，供下游多跨屏沿用。
/// </summary>
public partial class ColumnCalcViewModel : ObservableObject
{
    public CalcSession Session { get; }
    private readonly CalcWizardViewModel _wizard;

    public ColumnCalcViewModel(CalcSession session, CalcWizardViewModel wizard)
    {
        Session = session;
        _wizard = wizard;
        SyncMaterialFromSession();
        SyncAllowTexts();
        // 力学模型切换会改变「预演」是否可用 + 占位提示 + 短跨长可用性
        Session.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(CalcSession.ModelMultiStatic):
                case nameof(CalcSession.ModelMultiHyperStatic):
                    OnPropertyChanged(nameof(PreviewEnabled));
                    OnPropertyChanged(nameof(PreviewBlockedReason));
                    OnPropertyChanged(nameof(PlaceholderText));
                    InvalidateResults("力学模型已切换");
                    break;
                case nameof(CalcSession.ModelSingleTension):
                case nameof(CalcSession.ModelSingleCompression):
                    OnPropertyChanged(nameof(ShortSpanEnabled));
                    break;
                case nameof(CalcSession.WindStandardResolved):
                    OnPropertyChanged(nameof(PreviewEnabled));
                    OnPropertyChanged(nameof(PreviewBlockedReason));
                    OnPropertyChanged(nameof(PlaceholderText));
                    break;
            }
        };
    }

    // ---------------- 预演结果（V35 右侧富文本面板 rel(633,32) 292x352） ----------------
    public ObservableCollection<PreviewLine> Results { get; } = new();
    public bool HasResults => Results.Count > 0;

    private static readonly IBrush ColorNormal = Brushes.Black;
    private static readonly IBrush ColorNote = new SolidColorBrush(Color.Parse("#4B5563"));
    private static readonly IBrush ColorOk = new SolidColorBrush(Color.Parse("#0000C0"));
    private static readonly IBrush ColorFail = new SolidColorBrush(Color.Parse("#C00000"));

    private void Add(string text, IBrush? color = null, bool underline = false)
        => Results.Add(new PreviewLine(text, color ?? ColorNormal, underline));

    // ---------------- 导航 ----------------
    [RelayCommand] private void Prev() => _wizard.GoBasic();
    [RelayCommand] private void Next() => _wizard.StepFromColumnNext();
    [RelayCommand] private void Exit() => _wizard.CloseForm();
    /// <summary>
    /// 「预演」：把结果写进 V35 右侧那块富文本面板（rel(633,32) 292x352）。
    /// 输出条目与顺序 1:1 对齐 V31 实测（local-data/recon/v31runs/v31_preview_result.txt）：
    ///   风荷载标准值 → 抗弯矩预选 → 惯性矩预选 → 实际挠度/规范挠度 → 抗弯强度 → 抗剪强度
    /// 每块之间 42 个 '*'，惯性矩块与挠度块之间 42 个 '-'（V31 原文）。
    /// 多跨静定/超静定分支在 V35 里「预演」置灰，须「下一步」到多跨数据输入屏。
    /// </summary>
    [RelayCommand] private void Preview()
    {
        Results.Clear();
        try
        {
            // ★计算链门禁：风荷载标准值只能由第一屏「基本参数输入」算出并写入。
            //  未走完第一屏时一律拒绝，禁止拿 WindStandardKpa 的初值 1.0 冒充结果。
            if (!Session.WindStandardResolved)
            {
                Add($"尚未完成第一屏「基本参数输入」（{Session.CurrentMenuPath}）：", ColorFail);
                Add("风荷载标准值由第一屏计算，立柱强度/挠度全部以它为根。", ColorNote);
                Add("请点「上一步」回基本参数输入，再点「下一步」进入本屏。", ColorNote);
                _wizard.StatusText = "立柱计算：计算链须从第一屏出发（当前无有效风荷载标准值）";
                return;
            }

            if (Session.ModelMultiStatic || Session.ModelMultiHyperStatic)
            {
                Add("多跨铰接连续（超）静定梁：", ColorNote);
                Add("V35 本分支下「预演」置灰，请点「下一步」进入", ColorNote);
                Add("「多跨数据输入」屏，确认数据后再预演。", ColorNote);
                _wizard.StatusText = "立柱计算：多跨分支请在「下一步」后的多跨数据输入屏预演";
                return;
            }

            var sec = GlassManagement.Calc.Data.ParamLibrary.FindColumn(Session.ColumnCode);
            if (sec is null)
            {
                Add("未选定型材：请在「立柱选料」表里选一行，", ColorFail);
                Add("或用「快速查找型材」按代号定位。", ColorFail);
                _wizard.StatusText = "立柱计算：未选定型材，无法预演";
                return;
            }

            // V31 的 wk 有两份：显示值是 round3 的字符串，内部槽（slot[15]）也是 round3 的 N/mm² 值，
            // 立柱链吃的是**取 3 位之后**的 wk（旧注释曾按「吃未取整 raw」实现，广东档因此差 0.03~0.06%，
            // 2026-10-24 用 slot[15] 直接读出来纠正，见下方 qk 段的证据链）。
            // ★★ 2026-10-24 第一页 40 案对拍修正（B15/B18/B23 三案定案）：
            //   V31 的顺序是「wk 先含 K1/K2 并 round3，再 × bay 取 3 位」，K **不**在 qk 阶段再乘。
            //   三种写法只有这一种能全中（V31 实测值 → 反推 qk）：
            //     K2=0.9   wk=1.663 → qk 1.829（若先乘 bay 后乘 K：round3(2.033×0.9)=1.830 ❌）
            //     K1=1.2   wk=2.218 → qk 2.440（两种写法同值，不构成判别）
            //     风人工2.0 wk=2.000 → qk 2.200（若读"不含 K 的 raw 主式"，会得基准的 2.033 ❌）
            //   故取 WindStandardKpa（= round3(主式×K1×K2)）作为唯一起点。
            double wkMain = Session.WindStandardKpa;
            double wk = Session.WindStandardKpa;                       // 仅用于显示
            double k1 = Session.TerrainFactor > 0 ? Session.TerrainFactor : 1.0;
            double k2 = Session.WindDirFactor > 0 ? Session.WindDirFactor : 1.0;
            double bay = Session.ColumnBayWidthMm;                     // 立柱从属宽度 mm
            double L = Session.ColumnSpanMm;                           // 立柱跨度 mm
            double E = double.TryParse(ElasticModulusText, out double e) && e > 0
                ? e
                : Session.CustomMaterial ? Session.CustomElasticModulus : Session.ElasticModulusMpa;

            // ★★ 2026-10-25 实机定案（Frida 直读 V31 立柱函数 0xD1E9E0）：
            //   qd 这一步是 **double**：qd 输入 = qk(double) × 1.5，实测 VT_R8（slot8=1.414 → 输入 2.121）。
            //   取整用 VbaRound（逐次×10 → 半偶 → 除以 10^d），4 个平局点位模式 4/4 全等。
            //   注意：**qk 这一步仍走十进制半偶**——实测 2.455/1000×1100 = 2.7005000000000003，
            //   V31 给 2.701（十进制半偶），而 VbaRound 的逐次×10 会把它压成恰好 2700.5 → 半偶得 2700（错）。
            //   两步用不同路径不是"挑好看的对"，而是各自被实机数据钉死的：qk 见 B08/B09，
            //   qd 见 B01/B08/B10/B12 四个平局点（见 VbaRoundTests）。
            double wk3 = WindLoadEngine.RoundLikeV35(wkMain, 3);     // = V31 slot[7]×1000
            // ★ 2026-10-25（L=4000 案暴露）：wk 必须回到**十进制**再往下算。
            //   V31 slot[7] = 0.0012650000000000001（比 1.265/1000 的浮点结果**大 1ulp**），
            //   于是 qk 输入 = 1.3915000000000002 → round3 = 1.392；
            //   复刻直接用 double 1.265/1000*1100 = 1.3914999999999998 → round3 = 1.391（错 1 档）。
            //   把 wk 用 decimal 舍入回 1.265 再按十进制做 /1000×bay，即得 1.3915 → 1.392 ✅
            decimal wkDec = Math.Round((decimal)wk3, 3, MidpointRounding.ToEven);
            decimal qk0 = Math.Round(wkDec / 1000m * (decimal)bay, 3, MidpointRounding.ToEven);
            decimal qk = qk0;                                        // K1/K2 已含在 wkMain 里，此处不得再乘
            double qdD = WindLoadEngine.VbaRound((double)qk * WindPartialFactor, 3);  // V31 slot[9]
            decimal qd = (decimal)qdD;
            // ★★ 2026-10-24 四面对拍修正：qk 必须用「已取 3 位的 wk」再按 N/mm² × mm 的**顺序**算，
            //   而且要在 double 域取整（不能转 decimal），否则广东档差 0.03~0.06%。
            //   证据链（7 个 V31 实测案，见 docs/COMPARE_4WAY_GUANGDONG.md）：
            //     V31 内部 wk 存的是 3 位取整值，且单位是 N/mm²（slot[7]=0.001285 可证）：
            //       · 广东 100m：slot=2.146 → 0.002146×1100 = 2.3606000000000003 → 2.361 ✅（V31 qk）
            //       · 广东 60m ：slot=1.895 → 0.001895×1100 = 2.0844999999999999 → 2.084 ✅
            //       · 内压0.6μs1：slot=2.505 → 0.002505×1100 = 2.7554999999999996 → 2.755 ✅
            //   若换成 kPa 路径（wk×1.1=2.0845/2.7555 精确边界），Or 若先转 decimal 抹掉二进制尾巴，
            //   这两个案子会分别落到 2.085 / 2.756，与 V31 的 26050 / 34433.333 不符。
            //   K1/K2 已在 wk 阶段乘入（见 wkMain 注释），qk 阶段不再乘。
            // 抗剪用「显示值 wk × bay × 1.5」——V31 内存 0x1FBE6C0 = 3049.2（= 1.848×1.1×1.5×1000，
            //   注意是 round3 的 1.848，不是 raw 的 1.8480969；用 raw 会得 τ=11.708 而 V31 是 11.707）
            // 与其抗剪结果 11.707 相符；若改用取整后的 3.05 会得 11.710，与 V31 不符。
            // wk 显示值本身已含 K1/K2（WindStandardKpa = round3(wkMain×K1×K2)），这里不能再乘一次
            double qdRaw = wk * bay * WindPartialFactor / 1000.0;

            double I = sec.Ix * 1e4;                                   // cm⁴ → mm⁴
            double W = sec.Wx1 * 1e3;                                  // cm³ → mm³
            double Sx = sec.StaticMoment * 1e3;                        // cm³ → mm³
            double t = sec.ShearWebWidth;                              // mm

            // V35 计算书原文（0x4E8C40 / 0x4E8E9C）：
            //   f  = γ0·(N/An + Mx/(γ·Wnx))   τ = γ0·V·Sx/(Ix·t)
            // γ0 = 结构重要性系数，来自第一屏「构件的安全性能等级-结构重要性系数 → 计算非埋件时」下拉
            // （立柱不属于埋件，走 GradeWhenNoInsert）。一级1.1 / 二级1.0 / 三级0.9（GB50068）。
            double gamma0 = CalcSession.GammaZeroOf(Session.GradeWhenNoInsert);
            // ★ 2026-10-25 V31 代码级（0x18CEFB7–0x18CF035）：
            //   fld 2.0 → fld L → __vbaPowerR8(L²) → fmul q → fdiv 8.0 → fstp → push 3 → rtcRound
            //   ⇒ M = round3(q · L² / 8)，弯矩本身要取 3 位；随后才由 0x18CF05A 打印
            //   「最大弯矩为：M=qL^2/8=」。复刻此前不取整（L=3000 时结果相同，
            //   但其它跨度/非千分位 qd 会差 0.001，故按代码取整）。
            // ★ 「其它材质」：人工填的 [σ]/[τ]/E 直接覆盖材料表（V31 实机：w_pre = M/填入值，σ/τ/df 用填的许用值判定）
            double fAllow = Session.CustomMaterial ? Session.CustomAllowBending
                                                   : MaterialAllowables.Bending(Session.ColumnMaterial);
            double fvAllow = Session.CustomMaterial ? Session.CustomAllowShear
                                                    : MaterialAllowables.Shear(Session.ColumnMaterial);
            double M, V, df, lim, wReq, iReq;
            if (Session.ModelDoubleSpan && !Session.StructUnit)
            {
                // ── 双跨梁：公式全部来自 V31 函数 0xD35D8C（见 DoubleSpanColumnEngine 注释）──
                //   两组实机参数（L2=1500 等跨 / L2=1200 不等跨）逐位复现 13/13。
                var ds = DoubleSpanColumnEngine.Compute(L, Session.ShortSpanMm, (double)qk, (double)qd,
                                                        E, I, fAllow);
                M = ds.Moment;
                V = WindLoadEngine.VbaRound(ds.Shear, 3);       // τ 吃 round3(V)（V31 实机 1989.234）
                df = ds.Deflection;
                lim = WindLoadEngine.VbaRound(ds.Limit, 3);
                wReq = ds.RequiredW;
                iReq = ds.RequiredI;
            }
            else if (Session.ModelContinuous3Span && !Session.StructUnit)
            {
                // ── 连续梁（四点三跨）：公式来自 V31 函数 0xD42762，见 ContinuousBeamColumnEngine ──
                //   M=0.1·qd·L²、V=0.6·qk_raw·L·1.5（剪力吃**未取整** qk）、df=qk·0.00677·L⁴/(EI)
                var cb = ContinuousBeamColumnEngine.Compute(
                    L, wk * bay / 1000.0, (double)qk, (double)qd, E, I, fAllow);
                M = cb.Moment;
                V = cb.Shear;
                df = cb.Deflection;
                lim = WindLoadEngine.VbaRound(cb.Limit, 3);
                wReq = cb.RequiredW;
                iReq = cb.RequiredI;
            }
            else
            {
                M = WindLoadEngine.RoundLikeV35((double)qd * L * L / 8.0, 3);   // N·mm
                V = qdRaw * L / 2.0;                                // N（用未取整 qd，见上）
                df = 5.0 * (double)qk * Math.Pow(L, 4) / (384.0 * E * I);   // mm（标准值）
                // 挠度限值先 round3（V31 内存 0x1FBE570/0x1FBE590 = 16.667，即 3000/180 取整后）
                // iReq 必须用这个 16.667，用未取整的 16.6667 会得 1837868.3，与 V31 的 1837831.547 差 37。
                // ★ 2026-10-25 实测：单跨也受 20mm 绝对上限约束（3600→20 边界、3700→20、4500→20、5000→20；
                //   3400→18.889、3500→19.444 仍按 L/180）。上限常量即 V31 dbl_4019E0=20。
                lim = Math.Round(Math.Min(MultiSpanStaticEngine.DeflectionLimit(L, 0),
                                          DoubleSpanColumnEngine.LimitCapMm), 3, MidpointRounding.ToEven);  // mm
                wReq = M / fAllow;
                iReq = 5.0 * (double)qk * Math.Pow(L, 4) / (384.0 * E * lim);
            }
            // V31 打印位数统一为「round 3 位后去尾随零」：
            //   159.9501→159.95、175.5779→175.578、174.4773→174.477、27725.000→27725
            double sigma = Math.Round(gamma0 * M / W, 3, MidpointRounding.ToEven);          // MPa
            double tau = Math.Round(gamma0 * V * Sx / (I * t), 3, MidpointRounding.ToEven); // MPa

            // ── 单元式幕墙分支（V35 计算书模板 0x4EC890–0x4ED4C4）──────────────────
            //   V35 原文：「由于立柱轴力对计算影响很小，此处轴力分配按均分，
            //              水平荷载则按等刚度原则分配。」
            //   f_1左 = γ0·(N_1/2A_1n + M_1·(I_1x/(I_1x+I_2x))/γW_1nx)
            //   τ_1左 = γ0·V_B1·I_1x/(I_1x+I_2x)·S_1x/(I_1x·t_1)     右框同理
            //
            //   复刻当前只有一套型材参数，故左右按**同一型材**处理（V31 默认场景即如此），
            //   I1=I2 → 分配系数恒为 1/2。
            //
            //   V31 实测（基准参数 / 单跨简支 / 6063-T5）：
            //     左/右抗弯 79.975MPa（= 构件式 159.95 的精确一半 ✅）
            //     左/右抗剪 5.855MPa（构件式 11.707 的一半是 5.8535，差 0.0015
            //       —— 因为单元式的剪力用**取整后的 qd=3.05**，构件式用 qdRaw=3.0492：
            //       11.707×3.05/3.0492 = 11.7101，再 /2 = 5.85505 → 5.855 ✅）
            //     单元组合挠度 17.279mm（按 I1+I2 算得 17.287，差 0.046%，**尚未闭合**）
            //     抗弯/抗剪许用值 90/55 MPa（= 材料表 6063-T5 档，构件式 135/75 是 6063A-T5 档）
            if (Session.StructUnit)
            {
                // ★★ 2026-10-24 改：左右框**两套型材**（V31 立柱屏在单元式下重建为两个独立 Frame，
                //   各带一个「快速查找型材」；V35 公式里 I_1x/I_2x、W_1nx/W_2nx、A_1n/A_2n 全部下标分开）。
                //   空串 = 沿用主型材，等价于旧实现（左右同型材 → ratio 恒 1/2）。
                var secL = Session.UnitLeftProfile ?? sec;
                var secR = Session.UnitRightProfile ?? sec;
                double I1 = secL.Ix * 1e4, I2 = secR.Ix * 1e4;          // cm⁴ → mm⁴
                // 单元表给了 Wx1/Wx2 两个抵抗矩且与构件式表的顺序相反；V31 实测取**较小者**
                // （60/80：min(22.883,21.452)=21.452 → 79.975MPa；60/100：min(29.51,31.843)=29.51 → 73.691MPa）
                double W1 = Math.Min(secL.Wx1, secL.Wx2) * 1e3;         // cm³ → mm³
                double W2 = Math.Min(secR.Wx1, secR.Wx2) * 1e3;
                double S1 = secL.StaticMoment * 1e3, S2 = secR.StaticMoment * 1e3;
                double t1 = secL.ShearWebWidth, t2 = secR.ShearWebWidth;
                double I12 = I1 + I2;
                double ratio1 = I12 > 0 ? I1 / I12 : 0.5;               // 等刚度(EI)分配
                double ratio2 = I12 > 0 ? I2 / I12 : 0.5;

                double Vu = (double)qd * L / 2.0;                // 单元式抗剪用取整后的 qd
                // ★★ 2026-10-24 闭合 0.046%：V35 原文是「q_k左 = q_k·I_1x/(I_1x+I_2x)」——**先把荷载分到左框并取 3 位**，
                //   再拿这个 q_k左 去算 M 和挠度；不是在最后用 5q_kL⁴/(384E(I1+I2)) 等价替换。
                //   差别只在 round3 落在哪一步，但足以解释长期存在的 0.046%：
                //     同型材：q_k左 = round3(2.033×0.5) = 1.016（1.0165 银行家舍入到偶 1.016）
                //             → df = 5×1.016×L⁴/(384E·I) = 17.2788 → **17.279**（V31 实测值）✅
                //             而用 I1+I2 等价式得 17.287（差 0.046%）。
                //     左60/100+右60/80：q_k左 = round3(2.033×0.6337519)=1.288
                //             → df = 5×1.288×L⁴/(384E·I1) = 12.6592 → **12.659**（V31 实测值）✅
                //     σ 同一路数：q_d左 = round3(3.05×0.6337519)=1.933 → M1 = q_d左·L²/8 = 2 174 625
                //             → σ = M1/W1 = 73.6908 → **73.691**（V31）✅
                decimal qdL = WindLoadEngine.RoundDec((decimal)qd * (decimal)ratio1, 3);
                decimal qkL = WindLoadEngine.RoundDec((decimal)qk * (decimal)ratio1, 3);
                decimal qdR = WindLoadEngine.RoundDec((decimal)qd * (decimal)ratio2, 3);
                double M1 = (double)qdL * L * L / 8.0;
                double M2 = (double)qdR * L * L / 8.0;
                double sigmaL = Math.Round(gamma0 * M1 / W1, 3, MidpointRounding.ToEven);
                double sigmaR = Math.Round(gamma0 * M2 / W2, 3, MidpointRounding.ToEven);
                double tauL = Math.Round(gamma0 * Vu * ratio1 * S1 / (I1 * t1), 3, MidpointRounding.ToEven);
                double tauR = Math.Round(gamma0 * Vu * ratio2 * S2 / (I2 * t2), 3, MidpointRounding.ToEven);
                // 挠度：V35 原文「只须计算其中一根框」—— 左框用 q_k左 与 I_1x；右框同构。
                double dfU = Math.Round(5.0 * (double)qkL * Math.Pow(L, 4) / (384.0 * E * I1),
                                        3, MidpointRounding.ToEven);

                Add(Bar);
                Add($"风荷载标准值为:{Fmt(wk)}KPa");
                Add(Bar);

                Add($"左单元框抗弯强度计算值:{Fmt(sigmaL)}MPa");
                Add($"选用铝型材的抗弯强度设计值为:{Fmt(fAllow)}MPa");
                Add(sigmaL <= fAllow ? PassText : FailText, sigmaL <= fAllow ? ColorOk : ColorFail, true);
                Add(DashU1);
                Add($"右单元框抗弯强度计算值:{Fmt(sigmaR)}MPa");
                Add($"选用铝型材的抗弯强度设计值为:{Fmt(fAllow)}MPa");
                Add(sigmaR <= fAllow ? PassText : FailText, sigmaR <= fAllow ? ColorOk : ColorFail, true);
                Add(Bar);

                Add($"单 元组合的挠度计算值:{Fmt(dfU)}mm");     // 原文「单 元」中间有空格
                Add($"按规范的挠度允许值:{Fmt(lim)}mm");
                Add(dfU <= lim ? PassText : FailText, dfU <= lim ? ColorOk : ColorFail, true);
                Add(Bar);

                Add($"左单元框抗剪强度计算值:{Fmt(tauL)}MPa");
                Add($"选用铝型材的抗剪强度设计值为:{Fmt(fvAllow)}MPa");
                Add(tauL <= fvAllow ? PassText : FailText, tauL <= fvAllow ? ColorOk : ColorFail, true);
                Add(DashU2);
                Add($"右单元框抗剪强度计算值:{Fmt(tauR)}MPa");
                Add($"选用铝型材的抗剪强度设计值为:{Fmt(fvAllow)}MPa");
                Add(tauR <= fvAllow ? PassText : FailText, tauR <= fvAllow ? ColorOk : ColorFail, true);
                Add(Bar);

                _wizard.StatusText = $"立柱计算（单元式）·预演完成：wk={Fmt(wk)}KPa qk={Fmt((double)qk)}N/mm "
                                   + $"qd={Fmt((double)qd)}N/mm I1={Fmt(I1)} I2={Fmt(I2)} "
                                   + $"σ左={Fmt(sigmaL)} σ右={Fmt(sigmaR)}MPa τ左={Fmt(tauL)} τ右={Fmt(tauR)}MPa "
                                   + $"df组合={Fmt(dfU)}mm（限值 {Fmt(lim)}mm）";
                return;
            }

            // 选型预选已在上面的模型分支里算好（wReq / iReq）：
            //   单跨简支 W_req = M/f、I_req = 5qk·L⁴/(384E·df_lim)
            //   双跨梁   W_req = M/f、I_req = qk·kf·Lc⁴/(24E·df_lim)（V31 0xD3E8xx 实机链）

            Add(Bar);
            Add($"风荷载标准值为:{Fmt(wk)}KPa");
            Add(Bar);

            Add($"抗弯矩预选:{Fmt(wReq)}mm3");
            Add($"选用型材的抗弯矩设计值为:{Fmt(W)}mm3");
            Add(wReq <= W ? PassText : FailText, wReq <= W ? ColorOk : ColorFail, true);
            Add(Bar);

            Add($"惯性矩预选:{Fmt(iReq)}mm4");
            Add($"选用型材的惯性矩设计值为:{Fmt(I)}mm4");
            Add(Dash);

            // V31 挠度块用全角冒号（原文如此）
            Add($"实际挠度计算值为：{Fmt(df)}mm");
            Add($"规范要求的挠度值为：{Fmt(lim)}mm");
            Add(df <= lim ? PassText : FailText, df <= lim ? ColorOk : ColorFail, true);
            Add(Bar);

            Add($"抗弯强度计算值:{Fmt(sigma)}MPa");
            Add($"选用型材的抗弯强度设计值为:{Fmt(fAllow)}MPa");
            Add(sigma <= fAllow ? PassText : FailText, sigma <= fAllow ? ColorOk : ColorFail, true);
            Add(Bar);

            Add($"抗剪强度计算值:{Fmt(tau)}MPa");
            Add($"选用型材的抗剪强度设计值为:{Fmt(fvAllow)}MPa");
            Add(tau <= fvAllow ? PassText : FailText, tau <= fvAllow ? ColorOk : ColorFail, true);
            Add(Bar);

            _wizard.StatusText = $"立柱计算·预演完成：wk={Fmt(wk)}KPa qk={Fmt((double)qk)}N/mm qd={Fmt((double)qd)}N/mm γ0={Fmt(gamma0)} "
                               + $"σ={Fmt(sigma)}MPa τ={Fmt(tau)}MPa df={Fmt(df)}mm（限值 {Fmt(lim)}mm）";
        }
        catch (Exception ex)
        {
            Add($"预演失败：{ex.Message}", ColorFail);
            _wizard.StatusText = "立柱计算·预演失败：" + ex.Message;
        }
        finally
        {
            Finish();
        }
    }

    // ---------- V31 预演输出常量（原文取证：v31_preview_result.txt） ----------
    private const string Bar = "******************************************";   // 42 个 *
    private const string Dash = "------------------------------------------";  // 42 个 -
    private const string DashU1 = "---------------------------------------";   // 39 个 -（单元式抗弯块，V31 原文）
    private const string DashU2 = "----------------------------------------";  // 40 个 -（单元式抗剪块，V31 原文）
    private const string PassText = "满足要求(√)";
    private const string FailText = "不满足要求(×)";

    /// <summary>
    /// 风荷载分项系数（标准值 → 设计值）。
    /// 取证：V31 单跨简支（受拉）实测 —— 挠度支路 qk=1.848×1.100=2.0329N/mm（无分项），
    /// 强度支路 τ=11.707MPa 精确等于 1.5×7.805，σ=159.95≈1.5×106.6，抗弯矩预选 25416.667=M/135 也对应 1.5 倍 q。
    /// ⚠ V35 侧尚未定位到该系数的常量引用点（0x4022E8 处 1.5 被数百处通用公式引用，无法唯一归属），
    ///   故此处标注为「V31 实测反算值」，不作已确证结论。
    /// </summary>
    public const double WindPartialFactor = 1.5;

    private static string Fmt(double v, int digits = 3)
        => Math.Round(v, digits).ToString("0." + new string('#', digits));

    private void Finish()
    {
        Session.LastColumnSummary = CalcSession.SummaryOf(Results);
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(Results));
    }

    /// <summary>V35 实测：多跨静定 / 多跨一次超静定 两个分支下「预演」均置灰，必须「下一步」。</summary>
    public bool PreviewEnabled => Session.WindStandardResolved
                                   && !(Session.ModelMultiStatic || Session.ModelMultiHyperStatic);

    /// <summary>「预演」置灰的原因（按钮 ToolTip + 结果区占位文案共用，避免写在不可达的命令体里）。</summary>
    public string PreviewBlockedReason => PreviewEnabled
        ? "按当前参数计算并在右侧显示结果"
        : !Session.WindStandardResolved
            ? $"计算链须从「{Session.CurrentMenuPath}」的第一页出发：请先回「基本参数输入」点「下一步」"
            : "多跨（超）静定梁请在「下一步」后的「多跨数据输入」屏预演";

    /// <summary>结果面板空态提示。</summary>
    public string PlaceholderText => PreviewEnabled
        ? "点「预演」查看计算结果"
        : !Session.WindStandardResolved
            ? $"尚未完成第一屏「基本参数输入」。" + Environment.NewLine
              + $"当前类型：{Session.CurrentMenuPath}" + Environment.NewLine
              + "风荷载标准值由第一屏计算，请点「上一步」回去完成后再进入本屏。"
            : "当前为多跨铰接连续（超）静定梁：" + Environment.NewLine
              + "请点「下一步」进入「多跨数据输入」屏预演。";

    /// <summary>单跨简支时「短跨长」无意义（V31 该框置灰）。</summary>
    public bool ShortSpanEnabled => !(Session.ModelSingleTension || Session.ModelSingleCompression);

    // ---------------- 力学模型（frame98 可见套，绑会话） ----------------
    // 直接绑 Session.Model*（互斥在 Session 内实现）

    // 力学模型六选一的权威真源在 CalcSession（V35 实测：单跨简支受拉/受压、双跨梁、
    // 连续梁四点三跨、多跨铰接连续静定梁、多跨铰接连续一次超静定梁）。本 VM 不再另存副本。

    /// <summary>钢铝结合（V35 与多跨模型互斥，实测双向锁定）</summary>
    [ObservableProperty] private bool _steelAluminumJoint;
    partial void OnSteelAluminumJointChanged(bool value)
    {
        if (value)
        {
            if (Session.ModelMultiStatic || Session.ModelMultiHyperStatic)
            {
                SteelAluminumJoint = false;
                _wizard.StatusText = "钢铝结合 与 多跨模型互斥（V35 实测双向锁定），已拒绝勾选";
                return;
            }
            _wizard.StatusText = "钢铝结合为独占分支（预演/快速查找型材置灰），复刻未接该分支";
        }
    }

    // ---------------- 立柱选材（frame74：8 材质单选 + 断热/冷弯薄壁） ----------------
    [ObservableProperty] private bool _mat6061T4;
    [ObservableProperty] private bool _mat6061T6;
    [ObservableProperty] private bool _mat6063T5 = true;
    [ObservableProperty] private bool _mat6063T6;
    [ObservableProperty] private bool _mat6063AT5;
    [ObservableProperty] private bool _mat6063AT6;
    [ObservableProperty] private bool _matQ235;
    [ObservableProperty] private bool _matQ355;
    [ObservableProperty] private bool _matOther;
    [ObservableProperty] private bool _thermalBreak;
    [ObservableProperty] private bool _coldFormedThinWall;   // V35 恒灰（复刻显示为禁用）

    partial void OnMat6061T4Changed(bool value) { if (value) ClearMatExcept(nameof(Mat6061T4), "6061-T4"); }
    partial void OnMat6061T6Changed(bool value) { if (value) ClearMatExcept(nameof(Mat6061T6), "6061-T6"); }
    partial void OnMat6063T5Changed(bool value) { if (value) ClearMatExcept(nameof(Mat6063T5), "6063-T5"); }
    partial void OnMat6063T6Changed(bool value) { if (value) ClearMatExcept(nameof(Mat6063T6), "6063-T6"); }
    partial void OnMat6063AT5Changed(bool value) { if (value) ClearMatExcept(nameof(Mat6063AT5), "6063A-T5"); }
    partial void OnMat6063AT6Changed(bool value) { if (value) ClearMatExcept(nameof(Mat6063AT6), "6063A-T6"); }
    partial void OnMatQ235Changed(bool value) { if (value) ClearMatExcept(nameof(MatQ235), "Q235"); }
    partial void OnMatQ355Changed(bool value) { if (value) ClearMatExcept(nameof(MatQ355), "Q355"); }
    partial void OnMatOtherChanged(bool value) { if (value) ClearMatExcept(nameof(MatOther), "其它材质"); }

    private void ClearMatExcept(string keep, string name)
    {
        if (keep != nameof(Mat6061T4)) Mat6061T4 = false;
        if (keep != nameof(Mat6061T6)) Mat6061T6 = false;
        if (keep != nameof(Mat6063T5)) Mat6063T5 = false;
        if (keep != nameof(Mat6063T6)) Mat6063T6 = false;
        if (keep != nameof(Mat6063AT5)) Mat6063AT5 = false;
        if (keep != nameof(Mat6063AT6)) Mat6063AT6 = false;
        if (keep != nameof(MatQ235)) MatQ235 = false;
        if (keep != nameof(MatQ355)) MatQ355 = false;
        if (keep != nameof(MatOther)) MatOther = false;
        Session.ColumnMaterial = name;
        SyncAllowTexts();                 // 许用值随材质联动（A002 逐材质）
        InvalidateResults("材质已切换为 " + name);
    }

    private void SyncMaterialFromSession()
    {
        switch (Session.ColumnMaterial)
        {
            case "6061-T4": Mat6061T4 = true; break;
            case "6061-T6": Mat6061T6 = true; break;
            case "6063-T5": Mat6063T5 = true; break;
            case "6063-T6": Mat6063T6 = true; break;
            case "6063A-T5": Mat6063AT5 = true; break;
            case "6063A-T6": Mat6063AT6 = true; break;
            case "Q235": MatQ235 = true; break;
            case "Q355": MatQ355 = true; break;
            default: MatOther = true; break;
        }
    }

    // ---------------- 类别（规范）下拉 ----------------
    public IReadOnlyList<string> Standards { get; } = new[]
    {
        "JGJ133-2001", "JGJ102-2003", "JGJ336-2016", "GB/T21086-2007", "其它",
    };

    // ---------------- 立柱选料（V35 参数库「立柱」表 95 行） ----------------
    /// <summary>型材截面特性全表（参数库内嵌，按 Ix 升序即表内原序）。</summary>
    public System.Collections.Generic.IReadOnlyList<GlassManagement.Calc.Data.ParamLibrary.ColumnProfile> Columns
        => GlassManagement.Calc.Data.ParamLibrary.Columns;

    /// <summary>当前选中型材（写回共享会话，下游多跨验算取 I/W/S/t）。</summary>
    public GlassManagement.Calc.Data.ParamLibrary.ColumnProfile? SelectedColumn
    {
        get => Session.ColumnProfile;
        set
        {
            if (value is null) return;
            Session.ColumnCode = value.Code;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedColumnSummary));
            _wizard.StatusText = $"已选型材：{value.Code}（Ix={value.Ix}mm⁴ Wx1={value.Wx1}mm³ S={value.StaticMoment}mm³ t={value.ShearWebWidth}mm）";
        }
    }

    // ── 单元式幕墙：左右框型材选择（V31 在单元式下把「立柱选料」拆成左右两个 Frame）──
    /// <summary>当前是否单元式视图（决定左右框选择器是否显示）。</summary>
    public bool IsUnit => Session.StructUnit;

    /// <summary>单元式左框型材表（参数库「单元立柱一」）。</summary>
    public System.Collections.Generic.IReadOnlyList<GlassManagement.Calc.Data.ParamLibrary.ColumnProfile> UnitColumnsLeft
        => GlassManagement.Calc.Data.ParamLibrary.UnitColumnsLeft;

    /// <summary>单元式右框型材表（参数库「单元立柱二」）。与左框是两张独立的表。</summary>
    public System.Collections.Generic.IReadOnlyList<GlassManagement.Calc.Data.ParamLibrary.ColumnProfile> UnitColumnsRight
        => GlassManagement.Calc.Data.ParamLibrary.UnitColumnsRight;

    /// <summary>单元左框型材（null/空 = 沿用主型材）。</summary>
    public GlassManagement.Calc.Data.ParamLibrary.ColumnProfile? UnitLeftSelected
    {
        get => Session.UnitLeftProfile;
        set
        {
            Session.UnitLeftCode = value?.Code ?? "";
            OnPropertyChanged();
            OnPropertyChanged(nameof(UnitSummary));
        }
    }

    /// <summary>单元右框型材（null/空 = 沿用主型材）。</summary>
    public GlassManagement.Calc.Data.ParamLibrary.ColumnProfile? UnitRightSelected
    {
        get => Session.UnitRightProfile;
        set
        {
            Session.UnitRightCode = value?.Code ?? "";
            OnPropertyChanged();
            OnPropertyChanged(nameof(UnitSummary));
        }
    }

    /// <summary>左右框刚度分配摘要（I1/(I1+I2)）。</summary>
    public string UnitSummary
    {
        get
        {
            var l = Session.UnitLeftProfile; var r = Session.UnitRightProfile;
            if (l is null || r is null || l.Ix + r.Ix <= 0) return "";
            double ratio = l.Ix / (l.Ix + r.Ix);
            return $"左 I={l.Ix}cm⁴ 右 I={r.Ix}cm⁴ → 刚度分配 {ratio:0.0000} / {1 - ratio:0.0000}";
        }
    }

    /// <summary>状态栏/结果区引用的选中型材摘要。</summary>
    public string SelectedColumnSummary => Session.ColumnProfile is { } p
        ? $"{p.Code}：Ix={p.Ix} Iy={p.Iy} Wx1={p.Wx1} Wx2={p.Wx2} t={p.Thickness} A={p.Area} S={p.StaticMoment} tw={p.ShearWebWidth}"
        : "未选型材";

    [RelayCommand]
    private void FindProfile()
    {
        var hit = GlassManagement.Calc.Data.ParamLibrary.FindColumn(ProfileKeyword);
        if (hit is null)
        {
            _wizard.StatusText = $"快速查找型材：参数库「立柱」表 95 行内未找到「{ProfileKeyword}」";
            return;
        }
        SelectedColumn = hit;
    }

    [RelayCommand]
    private void UnitNote()
        => _wizard.StatusText = "单位说明：I/Iy[mm⁴] W[mm³] 壁厚[mm] 截面积[mm²] 面积矩S[mm³] 受剪腹板总宽度[mm]（参数库「立柱」表单位约定）";

    // ---------------- 其余 View 已绑定的项（V35 权威控件，功能复刻未接处如实标注） ----------------
    /// <summary>绝对单跨（力学模型 Frame 内复选）</summary>
    [ObservableProperty] private bool _absoluteSingleSpan;

    /// <summary>力学模型 Frame 内两个跨度输入框（V35 默认 3000 / 700，mm）。</summary>
    [ObservableProperty] private string _spanUpperText = "3000";
    [ObservableProperty] private string _spanLowerText = "700";

    /// <summary>
    /// 立柱跨度 → 会话共享（预演/多跨/计算书都用它）。
    /// 修复前该输入框只写本 VM 字段、从不写 Session.ColumnSpanMm，导致“改跨度后预演数值一字不变”。
    /// </summary>
    partial void OnSpanUpperTextChanged(string value)
    {
        if (double.TryParse(value, out double v) && v > 0)
            Session.ColumnSpanMm = v;
        InvalidateResults("立柱跨度已修改");
    }

    /// <summary>短跨长（单跨简支不参与计算，仅多跨/双跨分支使用）。</summary>
    partial void OnSpanLowerTextChanged(string value)
    {
        if (double.TryParse(value, out double v) && v > 0)
            Session.ShortSpanMm = v;
        InvalidateResults("短跨长已修改");
    }

    /// <summary>参数变更后让上次结果失效，避免用户把旧结果当新结果。</summary>
    private void InvalidateResults(string reason)
    {
        if (Results.Count == 0) return;
        Results.Clear();
        OnPropertyChanged(nameof(HasResults));
        _wizard.StatusText = reason + "，请重新预演";
    }

    // ---------------- 玻璃配置（V31 同位置两个厚度下拉） ----------------
    public IReadOnlyList<string> GlassThicknessOptions { get; } = new[]
    {
        "4", "5", "6", "8", "10", "12", "15", "19", "22",
    };
    [ObservableProperty] private string _outerGlassText = "6";
    [ObservableProperty] private string _innerGlassText = "6";

    // ---------------- 立柱选材右侧：名称 / 类别 / 许用值（V31 截图有标签与 MPa 单位） ----------------
    public IReadOnlyList<string> MaterialCategories { get; } = new[] { "铝型材", "钢型材", "其它" };
    [ObservableProperty] private string _materialCategory = "铝型材";
    [ObservableProperty] private string _materialNameText = "";
    [ObservableProperty] private string _bendingAllowText = "";
    [ObservableProperty] private string _shearAllowText = "";
    partial void OnMaterialNameTextChanged(string value)
        => InvalidateResults("材质名称已修改");

    partial void OnBendingAllowTextChanged(string value) { SyncAllowTexts(); }

    partial void OnShearAllowTextChanged(string value) { SyncAllowTexts(); }

    private void SyncAllowTexts()
    {
        // 许用值真源 = 材质单选（MaterialAllowables，A002 表逐材质实测）；人工输入仅作显示，改材质时覆盖
        BendingAllowText = Fmt(MaterialAllowables.Bending(Session.ColumnMaterial));
        ShearAllowText = Fmt(MaterialAllowables.Shear(Session.ColumnMaterial));
    }

    /// <summary>立柱选材右侧弹性模量输入（V35 默认 70000 MPa）。</summary>
    [ObservableProperty] private string _elasticModulusText = "70000";
    partial void OnElasticModulusTextChanged(string value)
    {
        if (double.TryParse(value, out double e) && e > 0) Session.ElasticModulusMpa = e;
    }

    /// <summary>立柱选料「快速查找型材」关键字。</summary>
    [ObservableProperty] private string _profileKeyword = "";

    [RelayCommand] private void Help()
        => _wizard.StatusText = "帮助：V35 弹出力学模型说明窗口，复刻未接";
}
