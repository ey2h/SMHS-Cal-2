using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 全玻璃幕墙「设计计算」屏引擎（V31 客户区 903×438）。
///
/// <para><b>算式来源</b>：V31 实机「全玻璃(含吊挂) → 全玻璃幕墙设计计算」屏的
/// Variant 实值轨迹（Frida 钩，59 var + 16 定稿点，<c>fullglass_design_var.json</c>）＋
/// 肋厚度/肋高度/胶缝宽扰动直读。四面对拍见 <c>docs/V31_FULLGLASS_UI.md</c>。</para>
///
/// <para><b>与玻璃幕墙玻璃屏（GlassEngine）的关键差异</b>：</para>
/// <list type="bullet">
/// <item>大面玻璃由<b>玻璃肋支承</b>（两对边简支），弯矩系数 m=0.125、挠度系数 μ=0.013
///   <b>固定</b>，不按短/长边比查表（GlassEngine 查 m/μ 表）；</item>
/// <item>新增<b>玻璃肋截面高度</b>、<b>玻璃肋挠度</b>、<b>硅酮结构胶强度</b>三组校核。</item>
/// </list>
///
/// <para>折减系数 η 仍复用 <see cref="GlassEngine.Eta"/>（θ 查表）。</para>
/// </summary>
public sealed class FullGlassEngine
{
    // ───────────────────────── 系数表 ─────────────────────────

    /// <summary>大面玻璃弯矩系数 m（两对边简支，固定 0.125）。</summary>
    public const double BendingCoef = 0.125;

    /// <summary>大面玻璃挠度系数 μ（两对边简支，固定 0.013）。</summary>
    public const double DeflectionCoef = 0.013;

    /// <summary>玻璃肋强度设计值 [σ]肋（MPa），随肋厚折减。</summary>
    /// <para>来源：**9 档全直读**（2026-10-28 b7_probe27_fullglass_sigma.json）——
    /// fullglass 屏「全玻璃幕墙设计计算」肋厚下拉 6/8/10/12/15/19/20/22/25 逐档切换 + 预演，
    /// frida 抓 `__vbaVarMul`，[σ]肋 本体在 `ra=0xFA0361` 以 `Mul(4, σ)` 出现：
    /// t12→78(=4×19.5)、t15/t19→68(=4×17.0)、t20/22/25→56(=4×14.0)。
    /// 折减档：6/8/10/12→19.5、15/19→17.0、20/22/25→14.0（非反推）。</para>
    private static readonly (double Thickness, double Sigma)[] RibAllowableTable =
    {
        (6, 19.5), (8, 19.5), (10, 19.5), (12, 19.5),
        (15, 17.0), (19, 17.0),
        (20, 14.0), (22, 14.0), (25, 14.0),
    };

    /// <summary>玻璃肋厚度候选（ComboBox，默认 12）。</summary>
    public static readonly double[] RibThicknessOptions = { 6, 8, 10, 12, 15, 19, 20, 22, 25 };

    // ───────────────────────── 输入 ─────────────────────────

    public sealed class Input
    {
        /// <summary>玻璃板短边（玻璃肋间距 a，mm），界面 (59,50) 默认 1500。</summary>
        public double GlassShortSideMm = 1500;

        /// <summary>玻璃板长边 / 玻璃肋跨度 L（mm），界面 (59,71) 默认 4000。</summary>
        public double RibSpanMm = 4000;

        /// <summary>大面玻璃厚度 t（mm），默认 10。单片玻璃 = 单片厚度；中空/夹层 = 外片厚度。</summary>
        public double GlassThicknessMm = 10;

        /// <summary>内片玻璃厚度（mm），默认 10。仅中空/夹层/三玻构型启用（实机界面 (338,224)）。</summary>
        public double InnerThicknessMm = 10;

        /// <summary>中片玻璃厚度（mm），默认 10。中空+夹层外/内、三玻双中空、三玻双夹层构型启用（实机界面 (198,223)）。</summary>
        public double MidThicknessMm = 10;

        /// <summary>外片第二片厚度（mm），默认 10。仅「双夹层-中空」构型启用（实机界面 (89,247)，隐藏副本）。</summary>
        public double Outer2ThicknessMm = 10;

        /// <summary>内片第二片厚度（mm），默认 10。仅「双夹层-中空」构型启用（实机界面 (338,247)，隐藏副本）。</summary>
        public double Inner2ThicknessMm = 10;

        /// <summary>含玻璃及中空层的玻璃面板总厚度（mm），默认 32（外片10+空气层12+内片10）。
        /// <para>★ 纯校验参数，不参与计算：须 &gt; 各片玻璃厚度之和，否则 V31 弹「玻璃厚度参数无效」。
        /// 正确值 = 玻璃厚度和 + 中空层数×12。</para></summary>
        public double TotalThicknessMm = 32;

        /// <summary>玻璃肋厚度 t肋（mm），界面 (467,192) 默认 12。</summary>
        public double RibThicknessMm = 12;

        /// <summary>玻璃肋实际截面高度 h（mm），界面 (459,408) 默认 200。</summary>
        public double RibActualHeightMm = 200;

        /// <summary>地震影响系数最大值 αmax（北京 0.16）。</summary>
        public double AlphaMax = 0.16;

        /// <summary>面板口径风荷载标准值 wk（N/mm²）。</summary>
        public double Wk = 0.00137;
    }

    // ───────────────────────── 输出 ─────────────────────────

    public sealed class Result
    {
        public double WindStandardKpa;   // 1.37
        public double GlassSigma;        // 单片 35.571
        public double GlassSigmaAllowable; // 84（钢化）
        public bool GlassSigmaOk;
        public double OuterSigma;        // 中空外片 21.327
        public double OuterSigmaAllowable; // 84（钢化）
        public bool OuterSigmaOk;
        public double InnerSigma;        // 中空内片 19.682
        public double InnerSigmaAllowable; // 84（钢化）
        public bool InnerSigmaOk;
        public double MidSigma;          // 三玻中片 13.382 / 13.989
        public double MidSigmaAllowable; // 84（钢化）
        public bool MidSigmaOk;
        public double Outer2Sigma;       // 双夹层-中空 次外片 11.964
        public double Outer2SigmaAllowable;
        public bool Outer2SigmaOk;
        public double Inner2Sigma;       // 双夹层-中空 最内片 11.07
        public double Inner2SigmaAllowable;
        public bool Inner2SigmaOk;
        public double GlassDeflection;   // 单片 13.892 / 中空 8.413
        public double GlassDeflectionAllowable; // 25
        public bool GlassDeflectionOk;
        public double RibRequiredHeight; // 单片 411.19 / 中空 424.445
        public double RibActualHeight;   // 200
        public bool RibHeightOk;
        public double RibDeflection;     // 11.892
        public double RibDeflectionAllowable; // 20
        public bool RibDeflectionOk;
        public double SealantStress;     // 单片 0.275 / 中空 0.293
        public double SealantAllowable;  // 0.2
        public bool SealantOk;

        // 过程量
        public double SeismicMpa;   // qd' = 0.000205
        public double Qd;           // 单片 0.002198 / 中空整体 0.002342
        public double RibAllowableMpa; // [σ]肋 = 19.5
    }

    // ───────────────────────── 计算 ─────────────────────────

    public static Result Compute(Input it)
    {
        if (it is null) throw new ArgumentNullException(nameof(it));

        var r = new Result
        {
            WindStandardKpa = WindLoadEngine.VbaRound(it.Wk * 1000.0, 2),
        };

        double t = it.GlassThicknessMm;
        double a = it.GlassShortSideMm;
        double L = it.RibSpanMm;
        double E = GlassEngine.E;

        // ① 荷载（0xF9AD1C–0xF9AF1D）
        r.SeismicMpa = WindLoadEngine.VbaRound(
            5.0 * it.AlphaMax * GlassEngine.DensityNmm3 * t, 6);
        r.Qd = WindLoadEngine.VbaRound(1.5 * it.Wk + 0.7 * r.SeismicMpa, 6);

        // ② 玻璃强度 σ（0xF9BDB4–0xF9BE58）：6·0.125·qd·a²/t²·η_σ
        double wD = it.Wk + 0.5 * r.SeismicMpa;                       // 标准组合
        double thetaS = WindLoadEngine.VbaRound(
            wD * Math.Pow(a, 4.0) / (E * Math.Pow(t, 4.0)), 3);
        double etaS = GlassEngine.Eta(thetaS);
        r.GlassSigma = WindLoadEngine.VbaRound(
            6.0 * BendingCoef * r.Qd * a * a * etaS / (t * t), 3);
        r.GlassSigmaAllowable = 84;   // 钢化玻璃（随玻璃种类变化，默认钢化）
        r.GlassSigmaOk = r.GlassSigma <= r.GlassSigmaAllowable;

        // ③ 玻璃挠度 w（0xF9C15E–0xF9C4C5）：η_δ·0.013·wk·a⁴/(E·t³/11.52)
        double thetaD = WindLoadEngine.VbaRound(
            it.Wk * Math.Pow(a, 4.0) / (E * Math.Pow(t, 4.0)), 3);
        double etaD = GlassEngine.Eta(thetaD);
        double D = E * Math.Pow(t, 3.0) / GlassEngine.PlateFactor;
        r.GlassDeflection = WindLoadEngine.VbaRound(
            etaD * DeflectionCoef * it.Wk * Math.Pow(a, 4.0) / D, 3);
        r.GlassDeflectionAllowable = WindLoadEngine.VbaRound(a / 60.0, 3);
        r.GlassDeflectionOk = r.GlassDeflection <= r.GlassDeflectionAllowable;

        // ④ 玻璃肋截面高度（0xFA02F7–0xFA0396）：√(3·qd·a·L²/(4·[σ]肋·t肋))
        double tr = it.RibThicknessMm;
        r.RibAllowableMpa = RibAllowableOf(tr);
        double hReq = Math.Sqrt(3.0 * r.Qd * a * L * L / (4.0 * r.RibAllowableMpa * tr));
        r.RibRequiredHeight = WindLoadEngine.VbaRound(hReq, 3);
        r.RibActualHeight = it.RibActualHeightMm;
        r.RibHeightOk = r.RibActualHeight >= r.RibRequiredHeight;

        // ⑤ 玻璃肋挠度（0xFA0860–0xFA08F9）：5·wk·a·L⁴/(384·E·I)，I = t肋·h³/12
        double h = it.RibActualHeightMm;
        double I = tr * Math.Pow(h, 3.0) / 12.0;
        r.RibDeflection = WindLoadEngine.VbaRound(
            5.0 * it.Wk * a * Math.Pow(L, 4.0) / (384.0 * E * I), 3);
        r.RibDeflectionAllowable = WindLoadEngine.VbaRound(L / 200.0, 3);  // 4000/200=20
        r.RibDeflectionOk = r.RibDeflection <= r.RibDeflectionAllowable;

        // ⑥ 结构胶 τ（0xFA1EE2–0xFA1EF6）：qd·a/玻璃肋厚
        r.SealantStress = WindLoadEngine.VbaRound(r.Qd * a / tr, 3);
        r.SealantAllowable = 0.2;   // 硅酮结构胶强度设计值（界面 (171,405) 默认 0.2）
        r.SealantOk = r.SealantStress <= r.SealantAllowable;

        return r;
    }

    /// <summary>
    /// 中空玻璃（外片+内片，1 个中空层）计算。V31 实机「全玻璃幕墙设计计算」屏切「中空玻璃」构型，
    /// 荷载按 JGJ102 厚度立方分配（外片×1.1、内片×1.0），挠度用等效厚度 te=0.95·(Σt³)^(1/3)。
    /// 参照 <c>fullglass_zhongkong_var.json</c>（101 var + 27 定稿点）。
    /// </summary>
    public static Result ComputeInsulated(Input it)
    {
        if (it is null) throw new ArgumentNullException(nameof(it));

        var r = new Result
        {
            WindStandardKpa = WindLoadEngine.VbaRound(it.Wk * 1000.0, 2),
        };

        double to = it.GlassThicknessMm;   // 外片 t外
        double ti = it.InnerThicknessMm;   // 内片 t内
        double a = it.GlassShortSideMm;
        double L = it.RibSpanMm;
        double E = GlassEngine.E;
        double dens = GlassEngine.DensityNmm3;

        // ① 各片地震荷载 qd' = 5·αmax·(γ/1e6)·t片（0xFA7D33 / 0xFA864F）
        double qdO = WindLoadEngine.VbaRound(5.0 * it.AlphaMax * dens * to, 6);
        double qdI = WindLoadEngine.VbaRound(5.0 * it.AlphaMax * dens * ti, 6);

        // ② 荷载分配（JGJ102 中空玻璃，外片×1.1）
        //    qd外 = 1.1·wk·t外³/(t外³+t内³)，qd内 = wk·t内³/(t外³+t内³)
        double to3 = to * to * to;
        double ti3 = ti * ti * ti;
        double sum3 = to3 + ti3;
        double qAllocO = WindLoadEngine.VbaRound(1.1 * it.Wk * to3 / sum3, 6);  // 0.000754
        double qAllocI = WindLoadEngine.VbaRound(it.Wk * ti3 / sum3, 6);        // 0.000685

        // ③ 各片强度 σ = 6·m·(1.5·qd片+0.7·qd'片)·a²·η(θ片)/t片²
        //    θ 用标准组合 wD片 = qd片 + 0.5·qd'片
        double wDO = WindLoadEngine.VbaRound(qAllocO + 0.5 * qdO, 6);  // 0.000856
        double qdStrengthO = WindLoadEngine.VbaRound(1.5 * qAllocO + 0.7 * qdO, 6);  // 0.001274
        double thetaO = WindLoadEngine.VbaRound(
            wDO * Math.Pow(a, 4.0) / (E * Math.Pow(to, 4.0)), 3);     // 6.019
        double etaO = GlassEngine.Eta(thetaO);                          // 0.992
        r.OuterSigma = WindLoadEngine.VbaRound(
            6.0 * BendingCoef * qdStrengthO * a * a * etaO / (to * to), 3);  // 21.327
        r.OuterSigmaAllowable = 84;   // 钢化玻璃
        r.OuterSigmaOk = r.OuterSigma <= r.OuterSigmaAllowable;

        double wDI = WindLoadEngine.VbaRound(qAllocI + 0.5 * qdI, 6);  // 0.000788
        double qdStrengthI = WindLoadEngine.VbaRound(1.5 * qAllocI + 0.7 * qdI, 6);  // 0.001171
        double thetaI = WindLoadEngine.VbaRound(
            wDI * Math.Pow(a, 4.0) / (E * Math.Pow(ti, 4.0)), 3);     // 5.541
        double etaI = GlassEngine.Eta(thetaI);                          // 0.996
        r.InnerSigma = WindLoadEngine.VbaRound(
            6.0 * BendingCoef * qdStrengthI * a * a * etaI / (ti * ti), 3);  // 19.682
        r.InnerSigmaAllowable = 84;   // 钢化玻璃
        r.InnerSigmaOk = r.InnerSigma <= r.InnerSigmaAllowable;

        // ④ 挠度（等效厚度 te = 0.95·(Σt³)^(1/3)，0xFAA6CE–0xFAAD37，无 ηδ 折减）
        double te = WindLoadEngine.VbaRound(0.95 * Math.Pow(sum3, 1.0 / 3.0), 3);  // 11.969
        double D = E * Math.Pow(te, 3.0) / GlassEngine.PlateFactor;
        r.GlassDeflection = WindLoadEngine.VbaRound(
            DeflectionCoef * it.Wk * Math.Pow(a, 4.0) / D, 3);   // 8.413
        r.GlassDeflectionAllowable = WindLoadEngine.VbaRound(a / 60.0, 3);  // 25
        r.GlassDeflectionOk = r.GlassDeflection <= r.GlassDeflectionAllowable;

        // ⑤ 整体 qd = 1.5·wk + 0.7·(qd'外+qd'内)（0xFA8D61–0xFA8DB3，肋高/结构胶用）
        r.SeismicMpa = WindLoadEngine.VbaRound(qdO + qdI, 6);   // 0.00041（两片 qd' 之和）
        r.Qd = WindLoadEngine.VbaRound(1.5 * it.Wk + 0.7 * r.SeismicMpa, 6);  // 0.002342

        // ⑥ 玻璃肋截面高度（同单片，qd 用整体）
        double tr = it.RibThicknessMm;
        r.RibAllowableMpa = RibAllowableOf(tr);
        double hReq = Math.Sqrt(3.0 * r.Qd * a * L * L / (4.0 * r.RibAllowableMpa * tr));
        r.RibRequiredHeight = WindLoadEngine.VbaRound(hReq, 3);  // 424.445
        r.RibActualHeight = it.RibActualHeightMm;
        r.RibHeightOk = r.RibActualHeight >= r.RibRequiredHeight;

        // ⑦ 玻璃肋挠度（同单片，只与 wk/肋参数有关）
        double h = it.RibActualHeightMm;
        double I = tr * Math.Pow(h, 3.0) / 12.0;
        r.RibDeflection = WindLoadEngine.VbaRound(
            5.0 * it.Wk * a * Math.Pow(L, 4.0) / (384.0 * E * I), 3);  // 11.892
        r.RibDeflectionAllowable = WindLoadEngine.VbaRound(L / 200.0, 3);
        r.RibDeflectionOk = r.RibDeflection <= r.RibDeflectionAllowable;

        // ⑧ 结构胶 τ = qd整体·a/玻璃肋厚（0xFB08CB–0xFB08DF）
        r.SealantStress = WindLoadEngine.VbaRound(r.Qd * a / tr, 3);  // 0.293
        r.SealantAllowable = 0.2;
        r.SealantOk = r.SealantStress <= r.SealantAllowable;

        return r;
    }

    /// <summary>中空玻璃「含玻璃及中空层的玻璃面板总厚度」校验：须 &gt; 各片玻璃厚度之和。</summary>
    public static bool TotalThicknessInvalid(double totalThicknessMm, params double[] plyThicknessMm)
    {
        double sum = 0;
        foreach (var t in plyThicknessMm) sum += t;
        return totalThicknessMm <= sum;
    }

    /// <summary>夹层玻璃（外片+内片，等片夹层，PVB 胶片）计算。
    /// <para>荷载两片平分（每片 0.5·wk），强度组合 0.75·wk+0.35·qd'总，等效厚度 te=(Σt³)^(1/3)（无 0.95）。
    /// 参照 <c>fullglass_laminated_var.json</c>。</para></summary>
    public static Result ComputeLaminated(Input it)
    {
        if (it is null) throw new ArgumentNullException(nameof(it));
        var r = new Result { WindStandardKpa = WindLoadEngine.VbaRound(it.Wk * 1000.0, 2) };

        double to = it.GlassThicknessMm;
        double ti = it.InnerThicknessMm;
        double a = it.GlassShortSideMm;
        double L = it.RibSpanMm;
        double E = GlassEngine.E;
        double dens = GlassEngine.DensityNmm3;

        // qd'总 = 5·αmax·(γ/1e6)·(t外+t内)（两片作为一个整体，0xF7077E–0xF70925）
        double qdTotal = SumPlySeis(dens, it.AlphaMax, to, ti);  // 0.00041
        // 每片强度 qd = 0.75·wk + 0.35·qd'总（0xF70C3F–0xF70C72）
        double qdStrength = WindLoadEngine.VbaRound(0.75 * it.Wk + 0.35 * qdTotal, 6);    // 0.001171
        // wD = 0.5·wk + 0.25·qd'总（θ 用，0xF70AD4–0xF70B07）
        double wD = WindLoadEngine.VbaRound(0.5 * it.Wk + 0.25 * qdTotal, 6);             // 0.000788

        double theta = WindLoadEngine.VbaRound(wD * Math.Pow(a, 4.0) / (E * Math.Pow(to, 4.0)), 3);  // 5.541
        double eta = GlassEngine.Eta(theta);                                          // 0.996
        // 内片/外片强度（等片相等；非等片各自按 t 算）
        r.InnerSigma = WindLoadEngine.VbaRound(6.0 * BendingCoef * qdStrength * a * a * eta / (ti * ti), 3);
        r.OuterSigma = WindLoadEngine.VbaRound(6.0 * BendingCoef * qdStrength * a * a * eta / (to * to), 3);
        r.InnerSigmaAllowable = 84;
        r.OuterSigmaAllowable = 84;
        r.InnerSigmaOk = r.InnerSigma <= r.InnerSigmaAllowable;
        r.OuterSigmaOk = r.OuterSigma <= r.OuterSigmaAllowable;

        // 等效厚度 te = (Σt³)^(1/3)（无 0.95），0xF720FE–0xF7211F
        double te = WindLoadEngine.VbaRound(Math.Pow(to * to * to + ti * ti * ti, 1.0 / 3.0), 3);  // 12.599
        FinishCommon(it, r, a, L, E, te, qdTotal);
        return r;
    }

    /// <summary>三玻双中空（外+中+内，2 个中空层）计算。
    /// <para>分母 = t外³ + 0.857375·(t中³+t内³)；外片 1.1·wk·t外³/分母，中/内 wk·0.857375·t³/分母；
    /// te = 0.95·(分母)^(1/3)。参照 <c>fullglass_sanbo_shuangzhongkong_var.json</c>。</para></summary>
    public static Result ComputeTripleInsul(Input it)
    {
        if (it is null) throw new ArgumentNullException(nameof(it));
        var r = new Result { WindStandardKpa = WindLoadEngine.VbaRound(it.Wk * 1000.0, 2) };

        double to = it.GlassThicknessMm;
        double tm = it.MidThicknessMm;
        double ti = it.InnerThicknessMm;
        double a = it.GlassShortSideMm;
        double L = it.RibSpanMm;
        double E = GlassEngine.E;
        double dens = GlassEngine.DensityNmm3;

        const double AirCoef = 0.857375;   // 双中空空气层折减
        double denom = to * to * to + AirCoef * (tm * tm * tm + ti * ti * ti);  // 2714.75

        double qdO = WindLoadEngine.VbaRound(1.1 * it.Wk * (to * to * to) / denom, 6);
        double qdM = WindLoadEngine.VbaRound(it.Wk * AirCoef * (tm * tm * tm) / denom, 6);
        double qdI = WindLoadEngine.VbaRound(it.Wk * AirCoef * (ti * ti * ti) / denom, 6);

        r.OuterSigma = PlySigma(a, E, to, qdO, dens, it.AlphaMax);
        r.OuterSigmaAllowable = 84;
        r.OuterSigmaOk = r.OuterSigma <= r.OuterSigmaAllowable;

        double midSigma = PlySigma(a, E, tm, qdM, dens, it.AlphaMax);
        double inSigma = PlySigma(a, E, ti, qdI, dens, it.AlphaMax);
        r.GlassSigma = midSigma;   // 复用 GlassSigma 存中片强度（临时；VM 读专用字段）
        r.InnerSigma = inSigma;
        r.InnerSigmaAllowable = 84;
        r.InnerSigmaOk = r.InnerSigma <= r.InnerSigmaAllowable;
        r.MidSigma = midSigma;
        r.MidSigmaAllowable = 84;
        r.MidSigmaOk = midSigma <= 84;

        double te = WindLoadEngine.VbaRound(0.95 * Math.Pow(denom, 1.0 / 3.0), 3);  // 13.253
        // 整体 qd = 1.5·wk + 0.7·Σqd'片（每片 round6 后相加）
        double qdSeisSum = SumPlySeis(dens, it.AlphaMax, to, tm, ti);   // 0.000615
        FinishCommon(it, r, a, L, E, te, qdSeisSum);
        return r;
    }

    /// <summary>三玻双夹层（外+中+内，双夹层）计算。3 片按厚度立方分配（无 1.1 外片系数、无空气层折减），
    /// 强度 1.5·qd片+0.7·qd'片，te=(Σt³)^(1/3)。参照 <c>fullglass_sanbo_shuangjiaceng_var.json</c>。</para></summary>
    public static Result ComputeTripleLam(Input it)
    {
        if (it is null) throw new ArgumentNullException(nameof(it));
        var r = new Result { WindStandardKpa = WindLoadEngine.VbaRound(it.Wk * 1000.0, 2) };

        double to = it.GlassThicknessMm;
        double tm = it.MidThicknessMm;
        double ti = it.InnerThicknessMm;
        double a = it.GlassShortSideMm;
        double L = it.RibSpanMm;
        double E = GlassEngine.E;
        double dens = GlassEngine.DensityNmm3;

        double sum3 = to * to * to + tm * tm * tm + ti * ti * ti;   // 3000
        double qdO = WindLoadEngine.VbaRound(it.Wk * (to * to * to) / sum3, 6);
        double qdM = WindLoadEngine.VbaRound(it.Wk * (tm * tm * tm) / sum3, 6);
        double qdI = WindLoadEngine.VbaRound(it.Wk * (ti * ti * ti) / sum3, 6);

        r.OuterSigma = PlySigma(a, E, to, qdO, dens, it.AlphaMax);
        r.MidSigma = PlySigma(a, E, tm, qdM, dens, it.AlphaMax);
        r.InnerSigma = PlySigma(a, E, ti, qdI, dens, it.AlphaMax);
        r.OuterSigmaAllowable = r.MidSigmaAllowable = r.InnerSigmaAllowable = 84;
        r.OuterSigmaOk = r.OuterSigma <= 84;
        r.MidSigmaOk = r.MidSigma <= 84;
        r.InnerSigmaOk = r.InnerSigma <= 84;

        double te = WindLoadEngine.VbaRound(Math.Pow(sum3, 1.0 / 3.0), 3);  // 14.422
        double qdSeisSum = SumPlySeis(dens, it.AlphaMax, to, tm, ti);       // 0.000615
        FinishCommon(it, r, a, L, E, te, qdSeisSum);
        return r;
    }

    /// <summary>双夹层-中空（外夹层2片 + 中空 + 内夹层2片 = 4片）计算。
    /// <para>外片(1#2#) 1.1·wk·t³/Σt³、内片(3#4#) wk·t³/Σt³，te=0.95·(Σt³)^(1/3)。
    /// 参照 <c>fullglass_doublelam_zhongkong_var.json</c>。</para></summary>
    public static Result ComputeDoubleLamInsul(Input it)
    {
        if (it is null) throw new ArgumentNullException(nameof(it));
        var r = new Result { WindStandardKpa = WindLoadEngine.VbaRound(it.Wk * 1000.0, 2) };

        double to1 = it.GlassThicknessMm;   // 最外片 1#
        double to2 = it.Outer2ThicknessMm;  // 次外片 2#
        double ti1 = it.InnerThicknessMm;   // 次内片 3#
        double ti2 = it.Inner2ThicknessMm;  // 最内片 4#
        double a = it.GlassShortSideMm;
        double L = it.RibSpanMm;
        double E = GlassEngine.E;
        double dens = GlassEngine.DensityNmm3;

        double sum3 = to1*to1*to1 + to2*to2*to2 + ti1*ti1*ti1 + ti2*ti2*ti2;  // 4000
        double qdO1 = WindLoadEngine.VbaRound(1.1 * it.Wk * (to1*to1*to1) / sum3, 6);
        double qdO2 = WindLoadEngine.VbaRound(1.1 * it.Wk * (to2*to2*to2) / sum3, 6);
        double qdI1 = WindLoadEngine.VbaRound(it.Wk * (ti1*ti1*ti1) / sum3, 6);
        double qdI2 = WindLoadEngine.VbaRound(it.Wk * (ti2*ti2*ti2) / sum3, 6);

        r.OuterSigma = PlySigma(a, E, to1, qdO1, dens, it.AlphaMax);   // 1# 11.964
        r.Outer2Sigma = PlySigma(a, E, to2, qdO2, dens, it.AlphaMax);  // 2# 11.964
        r.InnerSigma = PlySigma(a, E, ti1, qdI1, dens, it.AlphaMax);   // 3# 11.07
        r.Inner2Sigma = PlySigma(a, E, ti2, qdI2, dens, it.AlphaMax);  // 4# 11.07
        r.OuterSigmaAllowable = r.Outer2SigmaAllowable = r.InnerSigmaAllowable = r.Inner2SigmaAllowable = 84;
        r.OuterSigmaOk = r.OuterSigma <= 84;
        r.Outer2SigmaOk = r.Outer2Sigma <= 84;
        r.InnerSigmaOk = r.InnerSigma <= 84;
        r.Inner2SigmaOk = r.Inner2Sigma <= 84;

        // 等效厚度：每个夹层单元先算 te夹层=round3((t1³+t2³)^(1/3))，
        // 再整体 te=0.95·round3((te外³+te内³)^(1/3))（te 最终不 round，0x1008552–0x1008906）
        double teOuter = WindLoadEngine.VbaRound(Math.Pow(to1*to1*to1 + to2*to2*to2, 1.0 / 3.0), 3);
        double teInner = WindLoadEngine.VbaRound(Math.Pow(ti1*ti1*ti1 + ti2*ti2*ti2, 1.0 / 3.0), 3);
        double te = 0.95 * WindLoadEngine.VbaRound(
            Math.Pow(teOuter*teOuter*teOuter + teInner*teInner*teInner, 1.0 / 3.0), 3);  // 15.0803
        double qdSeisSum = SumPlySeis(dens, it.AlphaMax, to1, to2, ti1, ti2);
        FinishCommon(it, r, a, L, E, te, qdSeisSum, mu: 0.01302);   // 双夹层-中空 μ=0.01302（μ表 ratio=0）
        return r;
    }

    /// <summary>中空+夹层外（外夹层2片 + 中空 + 内单片 = 3片）计算。
    /// <para>外片/中片(外夹层) 1.1·wk·t³/Σt³、内片 wk·t³/Σt³，te=0.95·(Σt³)^(1/3)。
    /// 参照 <c>fullglass_zhongkong_jiaceng_wai_var.json</c>。</para></summary>
    public static Result ComputeInsulLamOuter(Input it)
    {
        if (it is null) throw new ArgumentNullException(nameof(it));
        var r = new Result { WindStandardKpa = WindLoadEngine.VbaRound(it.Wk * 1000.0, 2) };

        double to = it.GlassThicknessMm;   // 外片（外夹层第1片）
        double tm = it.MidThicknessMm;     // 中片（外夹层第2片）
        double ti = it.InnerThicknessMm;   // 内片（内单片）
        double a = it.GlassShortSideMm;
        double L = it.RibSpanMm;
        double E = GlassEngine.E;
        double dens = GlassEngine.DensityNmm3;

        double sum3 = to*to*to + tm*tm*tm + ti*ti*ti;   // 3000
        double qdO = WindLoadEngine.VbaRound(1.1 * it.Wk * (to*to*to) / sum3, 6);
        double qdM = WindLoadEngine.VbaRound(1.1 * it.Wk * (tm*tm*tm) / sum3, 6);
        double qdI = WindLoadEngine.VbaRound(it.Wk * (ti*ti*ti) / sum3, 6);

        r.OuterSigma = PlySigma(a, E, to, qdO, dens, it.AlphaMax);   // 15.12
        r.MidSigma = PlySigma(a, E, tm, qdM, dens, it.AlphaMax);     // 15.12
        r.InnerSigma = PlySigma(a, E, ti, qdI, dens, it.AlphaMax);   // 13.989
        r.OuterSigmaAllowable = r.MidSigmaAllowable = r.InnerSigmaAllowable = 84;
        r.OuterSigmaOk = r.OuterSigma <= 84;
        r.MidSigmaOk = r.MidSigma <= 84;
        r.InnerSigmaOk = r.InnerSigma <= 84;

        double te = WindLoadEngine.VbaRound(0.95 * Math.Pow(sum3, 1.0 / 3.0), 3);  // 13.701
        double qdSeisSum = SumPlySeis(dens, it.AlphaMax, to, tm, ti);
        FinishCommon(it, r, a, L, E, te, qdSeisSum);
        return r;
    }

    /// <summary>中空+夹层内（外单片 + 中空 + 内夹层2片 = 3片）计算。
    /// <para>外片 1.1·wk·t³/Σt³、中片/内片(内夹层) wk·t³/Σt³，te=0.95·(Σt³)^(1/3)。
    /// 参照 <c>fullglass_zhongkong_jiaceng_nei_var.json</c>。</para></summary>
    public static Result ComputeInsulLamInner(Input it)
    {
        if (it is null) throw new ArgumentNullException(nameof(it));
        var r = new Result { WindStandardKpa = WindLoadEngine.VbaRound(it.Wk * 1000.0, 2) };

        double to = it.GlassThicknessMm;   // 外片（外单片）
        double tm = it.MidThicknessMm;     // 中片（内夹层第1片）
        double ti = it.InnerThicknessMm;   // 内片（内夹层第2片）
        double a = it.GlassShortSideMm;
        double L = it.RibSpanMm;
        double E = GlassEngine.E;
        double dens = GlassEngine.DensityNmm3;

        double sum3 = to*to*to + tm*tm*tm + ti*ti*ti;   // 3000
        double qdO = WindLoadEngine.VbaRound(1.1 * it.Wk * (to*to*to) / sum3, 6);
        double qdM = WindLoadEngine.VbaRound(it.Wk * (tm*tm*tm) / sum3, 6);
        double qdI = WindLoadEngine.VbaRound(it.Wk * (ti*ti*ti) / sum3, 6);

        r.OuterSigma = PlySigma(a, E, to, qdO, dens, it.AlphaMax);   // 15.12
        r.MidSigma = PlySigma(a, E, tm, qdM, dens, it.AlphaMax);     // 13.989
        r.InnerSigma = PlySigma(a, E, ti, qdI, dens, it.AlphaMax);   // 13.989
        r.OuterSigmaAllowable = r.MidSigmaAllowable = r.InnerSigmaAllowable = 84;
        r.OuterSigmaOk = r.OuterSigma <= 84;
        r.MidSigmaOk = r.MidSigma <= 84;
        r.InnerSigmaOk = r.InnerSigma <= 84;

        double te = WindLoadEngine.VbaRound(0.95 * Math.Pow(sum3, 1.0 / 3.0), 3);  // 13.701
        double qdSeisSum = SumPlySeis(dens, it.AlphaMax, to, tm, ti);
        FinishCommon(it, r, a, L, E, te, qdSeisSum);
        return r;
    }

    /// <summary>单片的强度：σ = 6·m·(1.5·qd片+0.7·qd'片)·a²·η(θ片)/t²。</summary>
    private static double PlySigma(double a, double E, double t, double qdPly, double dens, double alphaMax)
    {
        double qdSeis = WindLoadEngine.VbaRound(5.0 * alphaMax * dens * t, 6);
        double wD = WindLoadEngine.VbaRound(qdPly + 0.5 * qdSeis, 6);
        double qdStrength = WindLoadEngine.VbaRound(1.5 * qdPly + 0.7 * qdSeis, 6);
        double theta = WindLoadEngine.VbaRound(wD * Math.Pow(a, 4.0) / (E * Math.Pow(t, 4.0)), 3);
        double eta = GlassEngine.Eta(theta);
        return WindLoadEngine.VbaRound(6.0 * BendingCoef * qdStrength * a * a * eta / (t * t), 3);
    }

    /// <summary>Σqd' = Σ(5·αmax·(γ/1e6)·t片)，每片 round6 后相加。</summary>
    private static double SumPlySeis(double dens, double alphaMax, params double[] thicknesses)
    {
        double sum = 0;
        foreach (var t in thicknesses)
            sum += WindLoadEngine.VbaRound(5.0 * alphaMax * dens * t, 6);
        return WindLoadEngine.VbaRound(sum, 6);
    }

    /// <summary>公共收尾：挠度（te 已定）+ 肋高 + 肋挠度 + 结构胶。mu 默认 0.013，双夹层-中空 0.01302。</summary>
    private static void FinishCommon(Input it, Result r, double a, double L, double E, double te, double qdSeisSum, double mu = DeflectionCoef)
    {
        double D = E * Math.Pow(te, 3.0) / GlassEngine.PlateFactor;
        r.GlassDeflection = WindLoadEngine.VbaRound(
            mu * it.Wk * Math.Pow(a, 4.0) / D, 3);
        r.GlassDeflectionAllowable = WindLoadEngine.VbaRound(a / 60.0, 3);
        r.GlassDeflectionOk = r.GlassDeflection <= r.GlassDeflectionAllowable;

        r.SeismicMpa = WindLoadEngine.VbaRound(qdSeisSum, 6);
        r.Qd = WindLoadEngine.VbaRound(1.5 * it.Wk + 0.7 * qdSeisSum, 6);

        double tr = it.RibThicknessMm;
        r.RibAllowableMpa = RibAllowableOf(tr);
        r.RibRequiredHeight = WindLoadEngine.VbaRound(
            Math.Sqrt(3.0 * r.Qd * a * L * L / (4.0 * r.RibAllowableMpa * tr)), 3);
        r.RibActualHeight = it.RibActualHeightMm;
        r.RibHeightOk = r.RibActualHeight >= r.RibRequiredHeight;

        double h = it.RibActualHeightMm;
        double I = tr * Math.Pow(h, 3.0) / 12.0;
        r.RibDeflection = WindLoadEngine.VbaRound(
            5.0 * it.Wk * a * Math.Pow(L, 4.0) / (384.0 * E * I), 3);
        r.RibDeflectionAllowable = WindLoadEngine.VbaRound(L / 200.0, 3);
        r.RibDeflectionOk = r.RibDeflection <= r.RibDeflectionAllowable;

        r.SealantStress = WindLoadEngine.VbaRound(r.Qd * a / tr, 3);
        r.SealantAllowable = 0.2;
        r.SealantOk = r.SealantStress <= r.SealantAllowable;
    }

    // ───────────────────────── 辅助 ─────────────────────────

    /// <summary>玻璃肋强度设计值 [σ]肋（MPa），按肋厚折减。未知厚度回落 19.5。</summary>
    private static double RibAllowableOf(double thickness)
    {
        foreach (var (tk, sg) in RibAllowableTable)
            if (Math.Abs(tk - thickness) < 1e-9)
                return sg;
        return 19.5;
    }
}
