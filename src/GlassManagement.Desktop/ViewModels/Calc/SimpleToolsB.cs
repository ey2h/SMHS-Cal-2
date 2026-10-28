using GlassManagement.Calc.Engines;

namespace GlassManagement.Desktop.ViewModels.Calc;

// 独立工具线第二批（钢销/螺栓群/锚板厚度/植筋/多点锁/转角立柱/焊缝）。
// 见 SimpleToolsA.cs 顶部说明。

/// <summary>钢销 / 螺栓（扭 + 剪）计算。引擎 <see cref="PinBoltEngine"/>。</summary>
public sealed class PinBoltToolViewModel : SimpleToolViewModel
{
    private readonly ToolField _n, _d, _sx, _sy, _t, _fv, _fcb, _na, _vv, _m;

    public PinBoltToolViewModel()
    {
        _n = F("钢销（螺栓）数量 n", "4", "个");
        _d = F("钢销直径 d", "5.0", "mm");
        _sx = F("X 向间距", "60", "mm");
        _sy = F("Y 向间距", "80", "mm");
        _t = F("被连接件壁厚 t", "3.0", "mm");
        _fv = F("钢销抗剪强度设计值 fv", "190", "MPa");
        _fcb = F("孔壁承压强度设计值 fcb", "210", "MPa");
        _na = F("轴力设计值 N", "2300", "N");
        _vv = F("剪力设计值 V", "2500", "N");
        _m = F("弯矩设计值 M", "5000", "N·mm");
    }

    public override string Title => "钢销/螺栓(扭+剪)计算";

    public override string Hint =>
        "销群在「轴力 + 剪力 + 扭矩」共同作用下的受力分配，校验最大单销剪力与孔壁承压。\n" +
        "容许值 = 单销抗剪面积 × fv；承压容许 = d × t × fcb。";

    protected override string DoCalc()
    {
        var r = PinBoltEngine.Compute(new PinBoltEngine.Input
        {
            Count = _n.AsDouble(4),
            DiameterMm = _d.AsDouble(5.0),
            SpacingXMm = _sx.AsDouble(60),
            SpacingYMm = _sy.AsDouble(80),
            WallThicknessMm = _t.AsDouble(3.0),
            PinShearMpa = _fv.AsDouble(190),
            BaseBearingMpa = _fcb.AsDouble(210),
            AxialForceN = _na.AsDouble(2300),
            ShearForceN = _vv.AsDouble(2500),
            MomentNmm = _m.AsDouble(5000),
        });

        return string.Join("\n", new[]
        {
            Sep,
            "钢销/螺栓(扭+剪)计算",
            Sep,
            $"n = {N(_n.AsDouble(4))} 个，d = {N(_d.AsDouble(5.0))} mm，间距 {N(_sx.AsDouble(60))}×{N(_sy.AsDouble(80))} mm，壁厚 t = {N(_t.AsDouble(3.0))} mm",
            $"作用：N = {N(_na.AsDouble(2300))} N，V = {N(_vv.AsDouble(2500))} N，M = {N(_m.AsDouble(5000))} N·mm",
            "",
            $"① 最大单销剪力 = {r.MaxShearN:0.###} N，容许 {r.ShearAllowableN:0.###} N ⇒ {(r.ShearOk ? "满足" : "不满足")}",
            $"② 孔壁承压 = {r.MaxShearN:0.###} N，容许 {r.BearingAllowableN:0.###} N ⇒ {(r.BearingOk ? "满足" : "不满足")}",
            Sep,
            $"结论：{(r.ShearOk && r.BearingOk ? "满足要求" : "不满足，需调整数量或直径")}",
        });
    }
}

/// <summary>螺栓群计算。引擎 <see cref="BoltGroupEngine"/>。</summary>
public sealed class BoltGroupToolViewModel : SimpleToolViewModel
{
    private readonly ToolField _d, _n, _sx, _sy, _na, _vv, _m, _fvb, _fcb, _t;

    public BoltGroupToolViewModel()
    {
        _d = F("螺栓直径 d", "5", "mm");
        _n = F("螺栓数量 n", "4", "个");
        _sx = F("X 向间距", "60", "mm");
        _sy = F("Y 向间距", "80", "mm");
        _na = F("轴力设计值 N", "2300", "N");
        _vv = F("剪力设计值 V", "2500", "N");
        _m = F("扭矩设计值 M", "5000", "N·mm");
        _fvb = F("螺栓抗剪强度设计值 fvb", "140", "MPa");
        _fcb = F("孔壁承压强度设计值 fcb", "185", "MPa");
        _t = F("板件总厚度 Σt", "3", "mm");
    }

    public override string Title => "螺栓群计算";

    public override string Hint =>
        "按弹性分配（Σr²）求最大单栓剪力：V_max = V/n + M·r_max/Σr²，再校验抗剪与承压。\n" +
        "半间距 X/Y 用于计算各栓到形心的距离平方和。";

    protected override string DoCalc()
    {
        var r = BoltGroupEngine.Compute(new BoltGroupEngine.Input
        {
            DiameterMm = _d.AsDouble(5),
            Count = _n.AsInt(4),
            SpacingX = _sx.AsDouble(60),
            SpacingY = _sy.AsDouble(80),
            AxialN = _na.AsDouble(2300),
            ShearV = _vv.AsDouble(2500),
            TorqueM = _m.AsDouble(5000),
            Fvb = _fvb.AsDouble(140),
            Fcb = _fcb.AsDouble(185),
            SumThickness = _t.AsDouble(3),
        });

        return string.Join("\n", new[]
        {
            Sep,
            "螺栓群计算",
            Sep,
            $"d = {N(_d.AsDouble(5))} mm，n = {N(_n.AsDouble(4))} 个，间距 {N(_sx.AsDouble(60))}×{N(_sy.AsDouble(80))} mm",
            $"作用：N = {N(_na.AsDouble(2300))} N，V = {N(_vv.AsDouble(2500))} N，M = {N(_m.AsDouble(5000))} N·mm",
            "",
            $"形心半间距：X = {r.X:0.###} mm，Y = {r.Y:0.###} mm；Σr² = {r.SumR2:0.###} mm²",
            $"竖向分量 = {r.VerticalComponent:0.###} N，水平分量 = {r.HorizontalComponent:0.###} N",
            $"最大单栓剪力 = {r.MaxBoltShear:0.###} N",
            $"① 抗剪容许 = {r.ShearAllowable:0.###} N ⇒ {(r.ShearOk ? "满足" : "不满足")}",
            $"② 承压应力 {r.BearingStress:0.###} MPa / 容许 {r.BearingAllowable:0.###} MPa ⇒ {(r.BearingOk ? "满足" : "不满足")}",
            Sep,
            $"结论：{(r.ShearOk && r.BearingOk ? "满足要求" : "不满足，需加大规格")}",
        });
    }
}

/// <summary>埋件厚度计算（锚板厚度）。引擎 <see cref="AnchorPlateEngine"/>。</summary>
public sealed class AnchorPlateToolViewModel : SimpleToolViewModel
{
    private readonly ToolField _na, _m, _b, _fc, _aw, _d;

    public AnchorPlateToolViewModel()
    {
        _na = F("轴力设计值 N", "4000", "N");
        _m = F("弯矩设计值 M", "200000", "N·mm");
        _b = F("锚板宽度 B", "200", "mm");
        _fc = F("混凝土轴心抗压强度设计值 fc", "11.9", "MPa");
        _aw = F("焊缝计算截面积 Aw", "530.66", "mm²");
        _d = F("锚栓直径 d", "12", "mm");
    }

    public override string Title => "埋件厚度计算";

    public override string Hint =>
        "锚板所需承压面积 Areq = N/(0.85·fc)，与实配面积比较；锚板最小厚度按三边支承/悬挑弯矩估算。\n" +
        "αb = 0.85 为局部受压提高系数（引擎常量）。";

    protected override string DoCalc()
    {
        var r = AnchorPlateEngine.Compute(new AnchorPlateEngine.Input
        {
            AxialN = _na.AsDouble(4000),
            MomentM = _m.AsDouble(200000),
            PlateWidthB = _b.AsDouble(200),
            Fc = _fc.AsDouble(11.9),
            WeldAreaAw = _aw.AsDouble(530.66),
            AnchorDiameterD = _d.AsDouble(12),
        });

        return string.Join("\n", new[]
        {
            Sep,
            "埋件厚度计算（锚板）",
            Sep,
            $"N = {N(_na.AsDouble(4000))} N，M = {N(_m.AsDouble(200000))} N·mm，B = {N(_b.AsDouble(200))} mm，fc = {N(_fc.AsDouble(11.9))} MPa",
            "",
            $"所需承压面积 Areq = N/(αb·fc) = {r.AreaRequired:0.###} mm²",
            $"实际承压面积 A = {r.AreaActual:0.###} mm² ⇒ {(r.AreaOk ? "满足" : "不满足")}",
            $"锚板最小厚度 = {r.MinThickness:0.###} mm，实配 {r.ActualThickness:0.###} mm ⇒ {(r.ThicknessOk ? "满足" : "不满足")}",
            Sep,
            $"结论：{(r.AreaOk && r.ThicknessOk ? "埋件规格满足要求" : "不满足，需加大埋件")}",
        });
    }
}

/// <summary>建筑植筋计算。引擎 <see cref="RebarAnchorEngine"/>。</summary>
public sealed class RebarAnchorToolViewModel : SimpleToolViewModel
{
    private readonly ToolField _d, _n, _s, _na, _m, _fy, _ft;

    public RebarAnchorToolViewModel()
    {
        _d = F("植筋直径 d", "12", "mm");
        _n = F("植筋数量 n", "4", "根");
        _s = F("植筋间距", "110", "mm");
        _na = F("轴力设计值 N", "5000", "N");
        _m = F("弯矩设计值 M", "200000", "N·mm");
        _fy = F("钢筋抗拉强度设计值 fy", "300", "MPa");
        _ft = F("混凝土抗拉强度设计值 ft", "2.3", "MPa");
    }

    public override string Title => "建筑植筋计算";

    public override string Hint =>
        "按群锚弹性分配求最大单根拉力，再取「钢筋抗拉承载力」与「混凝土锚固承载力」的较小值作为设计承载力。";

    protected override string DoCalc()
    {
        var r = RebarAnchorEngine.Compute(new RebarAnchorEngine.Input
        {
            DiameterD = _d.AsDouble(12),
            Count = _n.AsInt(4),
            Spacing = _s.AsDouble(110),
            AxialN = _na.AsDouble(5000),
            MomentM = _m.AsDouble(200000),
            Fy = _fy.AsDouble(300),
            Ft = _ft.AsDouble(2.3),
        });

        return string.Join("\n", new[]
        {
            Sep,
            "建筑植筋计算",
            Sep,
            $"d = {N(_d.AsDouble(12))} mm，n = {N(_n.AsDouble(4))} 根，间距 {N(_s.AsDouble(110))} mm",
            $"N = {N(_na.AsDouble(5000))} N，M = {N(_m.AsDouble(200000))} N·mm",
            "",
            $"群锚半径 R = {r.RadiusR:0.###} mm，Σr² = {r.SumR2:0.###} mm²",
            $"最大单根拉力 = {r.MaxBoltTension:0.###} N",
            $"植筋截面积 = {r.AnchorArea:0.###} mm²，钢筋抗拉承载力 = {r.TensionAllowable:0.###} N",
            $"混凝土锚固承载力：基本 {r.BaseAnchorage:0.###} N → 设计 {r.FinalAnchorage:0.###} N",
            Sep,
            $"结论：设计承载力取 {r.FinalAnchorage:0.###} N，" +
            (r.MaxBoltTension <= r.FinalAnchorage ? "大于最大单根拉力，满足要求" : "小于最大单根拉力，不满足"),
        });
    }
}

/// <summary>多点锁及开启扇扇框计算。引擎 <see cref="MultiPointLockEngine"/>。</summary>
public sealed class MultiPointLockToolViewModel : SimpleToolViewModel
{
    private readonly ToolField _h, _b, _c, _lock, _d, _fv, _n;
    private readonly ToolField _wk, _q, _gamma, _w, _i, _e, _sigma;

    public MultiPointLockToolViewModel()
    {
        _h = F("开启扇高度 H", "750", "mm");
        _b = F("开启扇宽度 B", "1650", "mm");
        _c = F("悬挑长度 c", "280", "mm");
        _lock = F("锁点间距", "450", "mm");
        _d = F("锁点销直径 d", "5.0", "mm");
        _fv = F("销抗剪强度设计值 fv", "100", "MPa");
        _n = F("配置锁点数量 n", "2", "个");
        _wk = F("风荷载标准值 wk", "0.00137", "N/mm²");
        _q = F("荷载设计值 q", "0.00212", "N/mm²");
        _gamma = F("荷载分项系数 γ", "1.35", "");
        _w = F("扇框截面抵抗矩 Wx", "5300", "mm³");
        _i = F("扇框截面惯性矩 Ix", "150500", "mm⁴");
        _e = F("弹性模量 E", "70000", "MPa");
        _sigma = F("强度设计值 [σ]", "90", "MPa");
    }

    public override string Title => "多点锁及开启扇扇框计算";

    public override string Hint =>
        "校验 4 项：扇框强度 σ、悬挑段挠度、跨中挠度、所需锁点数量（与配置数量比较）。\n" +
        "挠度限值按 V31 规则：悬挑取 c/90、跨中取 L/180 一类的分段值。";

    protected override string DoCalc()
    {
        var r = MultiPointLockEngine.Compute(new MultiPointLockEngine.Input
        {
            HeightMm = _h.AsDouble(750),
            WidthMm = _b.AsDouble(1650),
            CantileverMm = _c.AsDouble(280),
            LockSpanMm = _lock.AsDouble(450),
            PinDiameterMm = _d.AsDouble(5.0),
            PinShearMpa = _fv.AsDouble(100),
            PinCount = _n.AsDouble(2),
            WkNmm2 = _wk.AsDouble(0.00137),
            DesignLoadNmm2 = _q.AsDouble(0.00212),
            LoadFactor = _gamma.AsDouble(1.35),
            SectionModulus = _w.AsDouble(5300),
            Inertia = _i.AsDouble(150500),
            ElasticModulus = _e.AsDouble(70000),
            AllowableSigma = _sigma.AsDouble(90),
        });

        return string.Join("\n", new[]
        {
            Sep,
            "多点锁及开启扇扇框计算",
            Sep,
            $"扇框 {N(_b.AsDouble(1650))}×{N(_h.AsDouble(750))} mm，悬挑 c = {N(_c.AsDouble(280))} mm，锁点间距 {N(_lock.AsDouble(450))} mm",
            $"wk = {_wk.AsDouble(0.00137):0.#####} N/mm²，q = {_q.AsDouble(0.00212):0.#####} N/mm²，γ = {N(_gamma.AsDouble(1.35))}",
            "",
            $"① 扇框强度 σ = {r.FrameSigma:0.###} MPa ≤ [σ] {N(_sigma.AsDouble(90))} ⇒ {(r.FrameSigmaOk ? "满足" : "不满足")}",
            $"② 悬挑挠度 = {r.CantileverDeflection:0.###} mm ≤ 限值 {r.CantileverLimit:0.###} mm ⇒ {(r.CantileverOk ? "满足" : "不满足")}",
            $"③ 跨中挠度 = {r.MidSpanDeflection:0.###} mm ≤ 限值 {r.MidSpanLimit:0.###} mm ⇒ {(r.MidSpanOk ? "满足" : "不满足")}",
            $"④ 所需锁点数量 = {r.RequiredPins:0.###} 个，配置 {N(_n.AsDouble(2))} 个 ⇒ {(r.PinsOk ? "满足" : "不满足")}",
            $"⑤ 最大单锁点剪力 = {r.MaxPinShearN:0.###} N，容许 {r.PinShearAllowableN:0.###} N ⇒ {(r.PinShearOk ? "满足" : "不满足")}",
            Sep,
            $"结论：{(r.FrameSigmaOk && r.CantileverOk && r.MidSpanOk && r.PinsOk && r.PinShearOk
                ? "全部校验项满足要求" : "存在不满足项，需调整锁点数量或扇框截面")}",
        });
    }
}

/// <summary>转角立柱计算。引擎 <see cref="CornerColumnEngine"/>。</summary>
public sealed class CornerColumnToolViewModel : SimpleToolViewModel
{
    private readonly ToolField _wk, _lw, _lx, _ly, _gk, _wx, _wy, _a, _iy, _ix, _e, _sigma;

    public CornerColumnToolViewModel()
    {
        _wk = F("风荷载标准值 wk", "0.001125", "N/mm²");
        _lw = F("立柱受荷宽度", "2250", "mm");
        _lx = F("X 向跨度 Lx", "3600", "mm");
        _ly = F("Y 向跨度 Ly", "2500", "mm");
        _gk = F("自重面荷载 Gk", "0.0005", "N/mm²");
        _wx = F("X 向截面抵抗矩 Wx", "45841", "mm³");
        _wy = F("Y 向截面抵抗矩 Wy", "27014", "mm³");
        _a = F("截面积 A", "1224", "mm²");
        _iy = F("绕 Y 轴惯性矩 Iy", "3438070", "mm⁴");
        _ix = F("绕 X 轴惯性矩 Ix", "810430", "mm⁴");
        _e = F("弹性模量 E", "70000", "MPa");
        _sigma = F("强度设计值 [σ]", "90", "MPa");
    }

    public override string Title => "转角立柱计算";

    public override string Hint =>
        "转角立柱承受两个方向的风荷载（双向受弯）+ 自重轴力，按两种组合分别验算正应力，并验算双向挠度。\n" +
        "挠度限值按跨度/180 取用（V31 规则）。";

    protected override string DoCalc()
    {
        var r = CornerColumnEngine.Compute(new CornerColumnEngine.Input
        {
            Wk = _wk.AsDouble(0.001125),
            LoadWidth = _lw.AsDouble(2250),
            SpanX = _lx.AsDouble(3600),
            SpanY = _ly.AsDouble(2500),
            DeadLoad = _gk.AsDouble(0.0005),
            Wx = _wx.AsDouble(45841),
            Wy = _wy.AsDouble(27014),
            AreaA = _a.AsDouble(1224),
            Iy = _iy.AsDouble(3438070),
            Ix = _ix.AsDouble(810430),
            EMpa = _e.AsDouble(70000),
            SigmaAllowable = _sigma.AsDouble(90),
        });

        return string.Join("\n", new[]
        {
            Sep,
            "转角立柱计算",
            Sep,
            $"wk = {_wk.AsDouble(0.001125):0.######} N/mm²，受荷宽度 {N(_lw.AsDouble(2250))} mm，跨度 Lx = {N(_lx.AsDouble(3600))} / Ly = {N(_ly.AsDouble(2500))} mm",
            "",
            $"线荷载标准值 q = {r.QStandard:0.###} N/mm",
            $"弯矩 Mx = {r.Mx:0.###} N·mm，My = {r.My:0.###} N·mm，轴力 N = {r.AxialN:0.###} N",
            "",
            $"组合一 σ = {r.SigmaCase1:0.###} MPa",
            $"组合二 σ = {r.SigmaCase2:0.###} MPa",
            $"≤ [σ] {N(_sigma.AsDouble(90))} MPa ⇒ {(r.SigmaOk ? "满足" : "不满足")}",
            "",
            $"Y 向挠度 = {r.DeflectionY:0.###} mm，X 向挠度 = {r.DeflectionX:0.###} mm，限值 {r.DeflectionAllowable:0.###} mm ⇒ {(r.DeflectionOk ? "满足" : "不满足")}",
            Sep,
            $"结论：{(r.SigmaOk && r.DeflectionOk ? "强度与挠度均满足要求" : "存在不满足项")}",
        });
    }
}

/// <summary>焊缝强度计算。引擎 <see cref="WeldEngine"/>。</summary>
public sealed class WeldToolViewModel : SimpleToolViewModel
{
    private readonly ToolField _hf, _h, _b, _n, _m, _v, _ffw;

    public WeldToolViewModel()
    {
        _hf = F("焊脚尺寸 hf", "6", "mm");
        _h = F("构件截面高 h", "100", "mm");
        _b = F("构件截面宽 b", "50", "mm");
        _n = F("轴力设计值 N", "5000", "N");
        _m = F("弯矩设计值 M", "4000", "N·mm");
        _v = F("剪力设计值 V", "0", "N");
        _ffw = F("角焊缝强度设计值 ffw", "160", "MPa");
    }

    public override string Title => "焊缝强度计算";

    public override string Hint =>
        "角焊缝按有效厚度 he = 0.7·hf 计算截面特性，正面角焊缝强度增大系数 βf = 1.22。\n" +
        "σf = N/(βf·A) + M/(βf·W)，τf = V/A，验算 √((σf/βf)² + τf²) ≤ ffw。";

    protected override string DoCalc()
    {
        var r = WeldEngine.Compute(new WeldEngine.Input
        {
            Hf = _hf.AsDouble(6),
            H = _h.AsDouble(100),
            B = _b.AsDouble(50),
            AxialN = _n.AsDouble(5000),
            MomentM = _m.AsDouble(4000),
            ShearV = _v.AsDouble(0),
            Ffw = _ffw.AsDouble(160),
        });

        return string.Join("\n", new[]
        {
            Sep,
            "焊缝强度计算（角焊缝）",
            Sep,
            $"hf = {N(_hf.AsDouble(6))} mm，构件 {N(_b.AsDouble(50))}×{N(_h.AsDouble(100))} mm，βf = {WeldEngine.BetaF:0.##}",
            $"N = {N(_n.AsDouble(5000))} N，M = {N(_m.AsDouble(4000))} N·mm，V = {N(_v.AsDouble(0))} N",
            "",
            $"有效厚度 he = {r.He:0.###} mm",
            $"形心距离 = {r.NeutralAxisD:0.###} mm，惯性矩 I = {r.InertiaI:0.###} mm⁴，抵抗矩 W = {r.SectionW:0.###} mm³，有效面积 A = {r.AreaA:0.###} mm²",
            "",
            $"σf = {r.StressSigmaF:0.###} MPa，τf = {r.StressTauF:0.###} MPa",
            $"验算值 = {r.CheckValue:0.###} MPa ≤ ffw {r.Allowable:0.###} MPa ⇒ {(r.Ok ? "满足" : "不满足")}",
            Sep,
            $"结论：焊缝强度{(r.Ok ? "满足要求" : "不满足，需加大焊脚尺寸")}",
        });
    }
}
