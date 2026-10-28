using System;
using System.Linq;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Desktop.ViewModels.Calc;

// ─────────────────────────────────────────────────────────────────────────────
// 「其它计算」独立工具线（B8）的界面层。
//
// 这些引擎早已完成并有单元测试守护（见 docs/ACCEPTANCE_LEDGER.md §2.2），
// 之前缺的只是桌面接线。这里用 SimpleToolViewModel 基类 + SimpleToolView 一个通用视图承载，
// 每个工具只声明「字段清单 + 调哪个引擎」，不重复写布局。
//
// 默认值全部取自引擎 Input 的默认值（即 V31 实机取证时的算例），保证打开即可算出该工具的基准结果。
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>型材米重计算（菜单「其它计算 / 型材米重计算」）。引擎 <see cref="WeightPerMeterEngine"/>。</summary>
public sealed class WeightPerMeterToolViewModel : SimpleToolViewModel
{
    private readonly ToolField _material;
    private readonly ToolField _area;

    public WeightPerMeterToolViewModel()
    {
        var names = WeightPerMeterEngine.Materials.Select(m => m.Name).ToArray();
        _material = FD("材料种类", names, selected: "铝材");   // 引擎 Input 默认 ρ=2700（铝材）
        _area = F("型材截面积", "1000", "mm²");
    }

    public override string Title => "型材米重计算";

    public override string Hint =>
        "米重 = 截面积(mm²) × 密度(kg/m³) / 10⁶，单位 kg/m，结果保留 3 位小数。\n" +
        "密度取值：钢材 7850 / 铝材 2700 kg/m³（V31 材料下拉切换实测，非参数库表）。";

    protected override string DoCalc()
    {
        var mat = WeightPerMeterEngine.Materials
            .FirstOrDefault(m => m.Name == _material.Text);
        // 下拉未选/文本被清空时回落引擎默认密度（铝材 2700），而非 Materials[0]（钢材）。
        bool matched = mat.Name is not null;
        if (!matched) mat = ("铝材", 2700);

        var r = WeightPerMeterEngine.Compute(new WeightPerMeterEngine.Input
        {
            AreaMm2 = _area.AsDouble(1000),
            DensityKgM3 = mat.Density,
        });

        return string.Join("\n", new[]
        {
            Sep,
            "型材米重计算",
            Sep,
            $"材料种类：{mat.Name}（ρ = {mat.Density} kg/m³）",
            $"型材截面积 A = {N(_area.AsDouble(1000))} mm²",
            "",
            "米重 = A × ρ / 10⁶",
            $"     = {N(_area.AsDouble(1000))} × {mat.Density} / 10⁶",
            $"     = {r.WeightPerMeter:0.###} kg/m",
            Sep,
            $"计算所得型材米重为：{r.WeightPerMeter:0.###} kg/m",
        });
    }
}

/// <summary>百叶窗通风面积（菜单「其它计算 / 百叶窗通风面积」）。引擎 <see cref="LouverVentEngine"/>。</summary>
public sealed class LouverVentToolViewModel : SimpleToolViewModel
{
    private readonly ToolField _l, _a, _b, _n, _c, _w, _h;

    public LouverVentToolViewModel()
    {
        _l = F("边框之间的最小距离 L", "1200", "mm");
        _a = F("上框与最上面叶片最小距离 A", "25", "mm");
        _b = F("下框与最下面叶片最小距离 B", "25", "mm");
        _n = F("中间隔数量 n", "10", "个");
        _c = F("相邻叶片最小间距 C", "75", "mm");
        _w = F("百叶窗实际宽度 W", "1500", "mm");
        _h = F("百叶窗实际高度 H", "1800", "mm");
    }

    public override string Title => "百叶窗通风面积计算";

    public override string Hint =>
        "依据 GB/T 39968-2021 附录 A：Af = L×(A + B + n×C)，通风率 βf = Af /(W×H)。\n" +
        "Af / βf 均按 V31 逐位规则保留 3 位小数。";

    protected override string DoCalc()
    {
        var r = LouverVentEngine.Compute(new LouverVentEngine.Input
        {
            FrameSpanMm = _l.AsDouble(1200),
            TopGapMm = _a.AsDouble(25),
            BottomGapMm = _b.AsDouble(25),
            BladeCount = _n.AsDouble(10),
            BladeGapMm = _c.AsDouble(75),
            WidthMm = _w.AsDouble(1500),
            HeightMm = _h.AsDouble(1800),
        });

        return string.Join("\n", new[]
        {
            Sep,
            "百叶窗通风面积计算（GB/T 39968-2021 附录 A）",
            Sep,
            $"边框之间的最小距离 L = {N(_l.AsDouble(1200))} mm",
            $"上框与最上面叶片最小距离 A = {N(_a.AsDouble(25))} mm",
            $"下框与最下面叶片最小距离 B = {N(_b.AsDouble(25))} mm",
            $"中间隔数量 n = {N(_n.AsDouble(10))} 个，相邻叶片最小间距 C = {N(_c.AsDouble(75))} mm",
            "",
            "Af = L × (A + B + n × C)",
            $"   = {N(_l.AsDouble(1200))} × ({N(_a.AsDouble(25))} + {N(_b.AsDouble(25))} + {N(_n.AsDouble(10))} × {N(_c.AsDouble(75))})",
            $"   = {r.VentAreaM2:0.000} m²",
            "",
            $"βf = Af /(W × H) = {r.VentAreaM2:0.000} /({N(_w.AsDouble(1500))} × {N(_h.AsDouble(1800))} /10⁶)",
            $"   = {r.VentRatio:0.000}",
            Sep,
            $"计算所得百叶窗通风面积为：{r.VentAreaM2:0.000} m²，通风率 βf = {r.VentRatio:0.000}",
        });
    }
}

/// <summary>开启扇通风面积（菜单「其它计算 / 开启扇通风面积」）。引擎 <see cref="OpeningVentEngine"/>。</summary>
public sealed class OpeningVentToolViewModel : SimpleToolViewModel
{
    private readonly ToolField _b, _a, _ang;

    public OpeningVentToolViewModel()
    {
        _b = F("开启扇宽度 b", "1500", "mm");
        _a = F("开启扇高度 a", "1800", "mm");
        _ang = F("开启角度 α", "30", "°");
    }

    public override string Title => "开启扇通风面积计算";

    public override string Hint =>
        "算法一（GB 50352-2019）：Fp = d×(h + B)，取 min(Fp, h×B)；\n" +
        "算法二（SJG 44-2018）：S1 = a²×sinα/2、S2 = b×d，通风面积取两算法较小值。\n" +
        "d 为下缘框扇间距，sin 值按 V31 规则先保留 3 位小数。";

    protected override string DoCalc()
    {
        var r = OpeningVentEngine.Compute(new OpeningVentEngine.Input
        {
            WidthMm = _b.AsDouble(1500),
            HeightMm = _a.AsDouble(1800),
            AngleDeg = _ang.AsDouble(30),
        });

        return string.Join("\n", new[]
        {
            Sep,
            "开启扇通风面积计算",
            Sep,
            $"开启扇宽度 b = {N(_b.AsDouble(1500))} mm，高度 a = {N(_a.AsDouble(1800))} mm，开启角度 α = {N(_ang.AsDouble(30))}°",
            "",
            $"下缘框扇间距 d = {r.D:0.####} m",
            "",
            "【算法一 GB 50352-2019】",
            $"Fp = d × (h + B) = {r.D:0.####} × (h + B) = {r.Method1Fp:0.####} m²",
            $"取 min(Fp, h×B) ⇒ 通风面积 = {r.Method1Area:0.####} m²",
            "",
            "【算法二 SJG 44-2018】",
            $"S1 = a²·sinα/2 = {r.Method2S1:0.####} m²，S2 = b×d = {r.Method2S2:0.####} m²",
            $"空气流通面积 = 2×S1 + S2 = {r.Method2AirArea:0.####} m²",
            $"取 min(2S1+S2, a×b={r.Method2OpenArea:0.####}) ⇒ 通风面积 = {r.Method2Area:0.####} m²",
            Sep,
            $"最终通风面积取两算法较小值：{Math.Min(r.Method1Area, r.Method2Area):0.####} m²",
        });
    }
}

/// <summary>型（板）材热应力伸缩量（菜单「其它计算 / 型（板）材热应力伸缩量计算」）。引擎 <see cref="ThermalExpansionEngine"/>。</summary>
public sealed class ThermalExpansionToolViewModel : SimpleToolViewModel
{
    private readonly ToolField _alpha, _len, _dt;

    public ThermalExpansionToolViewModel()
    {
        _alpha = F("线膨胀系数 α", "0.000023", "1/℃");
        _len = F("材料长度 L", "3000", "mm");
        _dt = F("温差 ΔT", "80", "℃");
    }

    public override string Title => "型（板）材热应力伸缩量计算";

    public override string Hint =>
        "伸缩量 ΔL = α × L × ΔT，结果保留 3 位小数。\n" +
        "α 默认取铝材 2.3×10⁻⁵ /℃；缺资料时温差按 80℃ 计。";

    protected override string DoCalc()
    {
        var r = ThermalExpansionEngine.Compute(new ThermalExpansionEngine.Input
        {
            Alpha = _alpha.AsDouble(0.000023),
            LengthMm = _len.AsDouble(3000),
            DeltaT = _dt.AsDouble(80),
        });

        return string.Join("\n", new[]
        {
            Sep,
            "型（板）材热应力伸缩量计算",
            Sep,
            $"线膨胀系数 α = {_alpha.AsDouble(0.000023):0.######00} /℃",
            $"材料长度 L = {N(_len.AsDouble(3000))} mm",
            $"温差 ΔT = {N(_dt.AsDouble(80))} ℃",
            "",
            "ΔL = α × L × ΔT",
            $"   = {_alpha.AsDouble(0.000023):0.######00} × {N(_len.AsDouble(3000))} × {N(_dt.AsDouble(80))}",
            $"   = {r.ExpansionMm:0.###} mm",
            Sep,
            $"计算所得热应力伸缩量为：{r.ExpansionMm:0.###} mm",
        });
    }
}

/// <summary>型材热应力（菜单「其它计算 / 型材热应力计算」）。引擎 <see cref="ThermalStressEngine"/>。</summary>
public sealed class ThermalStressToolViewModel : SimpleToolViewModel
{
    private readonly ToolField _alpha, _e, _dt;

    public ThermalStressToolViewModel()
    {
        _alpha = F("线膨胀系数 α", "0.000023", "1/℃");
        _e = F("弹性模量 E", "70000", "MPa");
        _dt = F("温差 ΔT", "80", "℃");
    }

    public override string Title => "型材热应力计算";

    public override string Hint =>
        "热应力 σt = α × E × ΔT（完全约束时），结果保留 3 位小数。\n" +
        "α 默认铝材 2.3×10⁻⁵ /℃、E 默认 70000 MPa；温差缺资料按 80℃ 取。";

    protected override string DoCalc()
    {
        var r = ThermalStressEngine.Compute(new ThermalStressEngine.Input
        {
            Alpha = _alpha.AsDouble(0.000023),
            EMpa = _e.AsDouble(70000),
            DeltaT = _dt.AsDouble(80),
        });

        return string.Join("\n", new[]
        {
            Sep,
            "型材热应力计算",
            Sep,
            $"线膨胀系数 α = {_alpha.AsDouble(0.000023):0.######00} /℃",
            $"弹性模量 E = {N(_e.AsDouble(70000))} MPa",
            $"温差 ΔT = {N(_dt.AsDouble(80))} ℃",
            "",
            "σt = α × E × ΔT",
            $"   = {_alpha.AsDouble(0.000023):0.######00} × {N(_e.AsDouble(70000))} × {N(_dt.AsDouble(80))}",
            $"   = {r.SigmaT:0.###} MPa",
            Sep,
            $"计算所得型材热应力为：{r.SigmaT:0.###} MPa",
        });
    }
}

/// <summary>排烟面积（菜单「其它计算 / 排烟面积计算」）。引擎 <see cref="SmokeVentEngine"/>。</summary>
public sealed class SmokeVentToolViewModel : SimpleToolViewModel
{
    private readonly ToolField _w, _h, _ang;

    public SmokeVentToolViewModel()
    {
        _w = F("开启扇宽度", "1.5", "m");
        _h = F("开启扇高度", "1.8", "m");
        _ang = F("开启角度", "30", "°");
    }

    public override string Title => "排烟面积计算";

    public override string Hint =>
        "按开启角度折算有效排烟面积（自然排烟，GB 51251-2017）。\n" +
        "宽度/高度以 m 输入；结果保留 3 位小数。";

    protected override string DoCalc()
    {
        var r = SmokeVentEngine.Compute(new SmokeVentEngine.Input
        {
            WidthM = _w.AsDouble(1.5),
            HeightM = _h.AsDouble(1.8),
            AngleDeg = _ang.AsDouble(30),
        });

        return string.Join("\n", new[]
        {
            Sep,
            "排烟面积计算（GB 51251-2017 自然排烟）",
            Sep,
            $"开启扇宽度 = {N(_w.AsDouble(1.5))} m，高度 = {N(_h.AsDouble(1.8))} m，开启角度 = {N(_ang.AsDouble(30))}°",
            "",
            "有效排烟面积按开启角度折减计算：",
            $"   排烟面积 = {r.VentArea:0.###} m²",
            Sep,
            $"计算所得排烟面积为：{r.VentArea:0.###} m²",
        });
    }
}

/// <summary>自攻钉及自攻自钻螺钉（菜单「其它计算 / 自攻钉及自攻自钻螺钉计算」）。引擎 <see cref="SelfTappingScrewEngine"/>。</summary>
public sealed class SelfTappingScrewToolViewModel : SimpleToolViewModel
{
    private readonly ToolField _d, _len, _p, _fy, _fu, _d1, _ft, _n;

    public SelfTappingScrewToolViewModel()
    {
        _d = F("螺钉公称直径 d", "3.5", "mm");
        _len = F("旋入长度（板厚）L", "3.5", "mm");
        _p = F("螺距 P", "1.3", "mm");
        _fy = F("母材屈服强度 fy", "110", "MPa");
        _fu = F("母材抗拉强度 fu", "160", "MPa");
        _d1 = F("螺钉内径 d1", "2.51", "mm");
        _ft = F("螺钉抗拉强度设计值 ft", "170", "MPa");
        _n = F("拉力设计值 N", "950", "N");
    }

    public override string Title => "自攻钉及自攻自钻螺钉计算";

    public override string Hint =>
        "校验两项：①与母材咬合的抗拉承载力 Rn；②螺钉自身抗拉承载力 Ns。均需 ≥ 拉力设计值 N。\n" +
        "母材分段按旋入长度与螺距比确定（Segment = L/P 所在区间）。";

    protected override string DoCalc()
    {
        var r = SelfTappingScrewEngine.Compute(new SelfTappingScrewEngine.Input
        {
            DiameterMm = _d.AsDouble(3.5),
            EngageLengthMm = _len.AsDouble(3.5),
            PitchMm = _p.AsDouble(1.3),
            BaseYieldMpa = _fy.AsDouble(110),
            BaseUltimateMpa = _fu.AsDouble(160),
            InnerDiameterMm = _d1.AsDouble(2.51),
            ScrewTensileMpa = _ft.AsDouble(170),
            TensileForceN = _n.AsDouble(950),
        });

        return string.Join("\n", new[]
        {
            Sep,
            "自攻钉及自攻自钻螺钉计算",
            Sep,
            $"螺钉 d = {N(_d.AsDouble(3.5))} mm，旋入长度 L = {N(_len.AsDouble(3.5))} mm，螺距 P = {N(_p.AsDouble(1.3))} mm",
            $"母材 fy = {N(_fy.AsDouble(110))} MPa，fu = {N(_fu.AsDouble(160))} MPa；螺钉 ft = {N(_ft.AsDouble(170))} MPa",
            "",
            $"有效受拉面积 As = {r.EffectiveAreaMm2:0.###} mm²（分段 Segment {r.Segment}）",
            $"① 与母材咬合抗拉承载力 Rn = {r.Rn:0.###} N  {(r.RnOk ? "≥ N，满足" : "< N，不满足")}",
            $"② 螺钉自身抗拉承载力 Ns = {r.Ns:0.###} N  {(r.NsOk ? "≥ N，满足" : "< N，不满足")}",
            $"拉力设计值 N = {N(_n.AsDouble(950))} N",
            Sep,
            $"结论：{(r.RnOk && r.NsOk ? "两项均满足要求" : "存在不满足项，需调整规格")}",
        });
    }
}

/// <summary>型材螺纹计算（菜单「其它计算 / 型材螺纹计算」）。引擎 <see cref="ThreadEngine"/>。</summary>
public sealed class ThreadToolViewModel : SimpleToolViewModel
{
    private readonly ToolField _d, _p, _as, _ft, _fv, _fcb, _fvb, _len, _n, _v;

    public ThreadToolViewModel()
    {
        _d = F("螺钉公称直径 d", "6.0", "mm");
        _p = F("螺距 P", "1.0", "mm");
        _as = F("螺钉有效截面积 As", "20.1", "mm²");
        _ft = F("螺钉抗拉强度设计值 ft", "230", "MPa");
        _fv = F("螺钉抗剪强度设计值 fv", "175", "MPa");
        _fcb = F("母材承压强度设计值 fcb", "185", "MPa");
        _fvb = F("母材牙根切出强度设计值", "55", "MPa");
        _len = F("旋入长度（型材壁厚）L", "4", "mm");
        _n = F("拉力设计值 N", "3500", "N");
        _v = F("剪力设计值 V", "3500", "N");
    }

    public override string Title => "型材螺纹计算（先在母材上制出螺纹）";

    public override string Hint =>
        "校验 6 项：螺钉抗拉 / 抗剪 / 拉剪复合 / 型材侧壁承压 / 螺纹挤压 / 牙根切出。\n" +
        "Default 值 = 不锈钢 A1-50 螺钉拧入 6063-T5 铝合金型材的取证算例。";

    protected override string DoCalc()
    {
        double n = _n.AsDouble(3500), v = _v.AsDouble(3500);
        var r = ThreadEngine.Compute(new ThreadEngine.Input
        {
            DiameterMm = _d.AsDouble(6.0),
            PitchMm = _p.AsDouble(1.0),
            EffectiveAreaMm2 = _as.AsDouble(20.1),
            ScrewTensileMpa = _ft.AsDouble(230),
            ScrewShearMpa = _fv.AsDouble(175),
            BaseBearingMpa = _fcb.AsDouble(185),
            BaseShearMpa = _fvb.AsDouble(55),
            EngageLengthMm = _len.AsDouble(4),
            TensileForceN = n,
            ShearForceN = v,
        });

        string Ok(bool b) => b ? "满足" : "不满足";
        return string.Join("\n", new[]
        {
            Sep,
            "型材螺纹计算（螺钉直接旋入型材）",
            Sep,
            $"螺钉 M{N(_d.AsDouble(6.0))}，P = {N(_p.AsDouble(1.0))} mm，As = {N(_as.AsDouble(20.1))} mm²，旋入 L = {N(_len.AsDouble(4))} mm",
            $"拉力设计值 N = {N(n)} N，剪力设计值 V = {N(v)} N",
            "",
            $"① 螺钉抗拉承载力 = {r.TensileAllowableN:0.###} N  ≥ N ? {Ok(r.TensileOk)}",
            $"② 螺钉抗剪承载力 = {r.ShearAllowableN:0.###} N  ≥ V ? {Ok(r.ShearOk)}",
            $"③ 拉剪复合应力比 = {r.CombinedStress:0.####} ≤ 1 ? {Ok(r.CombinedOk)}",
            $"④ 型材侧壁承压承载力 = {r.WallBearingAllowableN:0.###} N ? {Ok(r.WallBearingOk)}",
            $"⑤ 螺纹挤压承载力 = {r.ThreadBearingAllowableN:0.###} N ? {Ok(r.ThreadBearingOk)}",
            $"⑥ 牙根切出承载力 = {r.ThreadShearOutAllowableN:0.###} N ? {Ok(r.ThreadShearOutOk)}",
            Sep,
            $"结论：{(r.TensileOk && r.ShearOk && r.CombinedOk && r.WallBearingOk && r.ThreadBearingOk && r.ThreadShearOutOk
                ? "全部校验项满足要求" : "存在不满足项，需加大规格或旋入长度")}",
        });
    }
}
