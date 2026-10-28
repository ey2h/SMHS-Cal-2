using System;
using System.Collections.Generic;
using System.Linq;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 雨篷（雨蓬）计算引擎（V31「雨蓬计算」，CW28，893×513，4 Tab）。
/// 5 大模块：荷载组合 → 悬臂梁（集中荷载）→ 焊缝 → 玻璃（点式支承）→ 埋件锚固。
/// 取证参照 yupeng_var.json（52 rounds + 180 var）。
///
/// 关键口径（运行期实值）：
///  - 正压风 = round6(μs+ · μz · μs · w0)；负压风 = round6(μs− · μz · μs · w0)，
///    其中 μs+=0.5 / μs−=2.0（雨篷体型系数），μz=1.6929 / μs=1.4983 为第一页 round4 值。
///  - 龙骨标准组合 = 负压风 − 恒；设计组合 = 1.5·负压风 − 恒（负压为控制工况，恒取 1.0 折减）。
///  - 悬臂梁 σ = M / (γx·W)，γx=1.05；挠度 = Σ P_i·a_i²·(3L−a_i)/(6EI)。
///  - 焊缝 √((M/1.22W)² + (V/A)²)，V = q设计·L·B。
///  - 玻璃：点式支承 m=0.154 / μ=0.02603，自重 = γ·t/1e6，qd = 1.5·负压 − 自重，
///    wk = 负压 − 自重，σ = 6·m·qd·a²·η/t²，挠度 = ηδ·μ·wk·a⁴/(E·t³/11.52)。
///  - 埋件：asReq = M/(0.4·αb·fy·z)（分支2，不含剪力项），lab = α·fy·d，la = asReq/As·lab。
/// </summary>
public static class AwningEngine
{
    public sealed class ConcentratedLoad
    {
        /// <summary>距固定端距离 a（mm）。</summary>
        public double PositionMm;
        /// <summary>受荷宽（mm）。</summary>
        public double TribWidthMm;
        /// <summary>受荷长（mm）。</summary>
        public double TribLengthMm;

        public ConcentratedLoad() { }
        public ConcentratedLoad(double pos, double w, double l)
        { PositionMm = pos; TribWidthMm = w; TribLengthMm = l; }
    }

    public sealed class Input
    {
        // ── 风荷载系数（第一页 round4 下传）──
        public double MuZ = 1.6929;            // 风压高度变化系数 μz
        public double MuS = 1.4983;            // 体型系数 μs（墙面）
        public double PosShapeCoeff = 0.5;     // 正压体型系数 μs+
        public double NegShapeCoeff = 2.0;     // 负压体型系数 μs−
        public double BasicWindKpa = 0.45;     // 基本风压 w0（KPa）

        // ── 荷载标准值（KPa）──
        public double SnowKpa = 0.8;
        public double LiveKpa = 0.5;
        public double DeadKpa = 0.4;

        // ── 几何（mm）──
        public double EccentricityMm = 100;    // 偏心
        public double PanelWidthMm = 1500;     // 板宽 B
        public double PanelLengthMm = 1400;    // 板长

        // ── 悬臂主梁（槽钢[12.6）──
        public double BeamSectionModulus = 62100;   // W mm³
        public double BeamInertia = 3910000;        // I mm⁴
        public double BeamElasticModulus = 206000;  // E MPa
        public double BeamPlasticFactor = 1.05;     // γx

        // ── 集中荷载（DataGrid，默认 3 行）──
        public List<ConcentratedLoad> ConcentratedLoads = new()
        {
            new(100, 1500, 1500),   // 位置100，受荷 1500×1500
            new(3000, 1500, 1400),  // 位置3000，受荷 1500×1400
            new(1600, 1500, 2900),  // 位置1600，受荷 1500×2900
        };

        // ── 焊缝 ──
        public double WeldLegMm = 4;               // 焊脚 hf
        public double WeldSectionModulus = 76970;  // W_weld mm³
        public double WeldArea = 2532.4;           // A_weld mm²

        // ── 玻璃（点式支承）──
        public double GlassShortSideMm = 1250;     // 短边 a
        public double GlassThicknessMm = 10;       // 厚度 t
        public double GlassM = 0.154;              // 弯矩系数 m（点式支承）
        public double GlassMu = 0.02603;           // 挠度系数 μ（点式支承）
        public double GlassEta = 0.985;            // 折减系数 η / ηδ
        public double GlassDensity = 25.6;         // 玻璃容重 γ（kN/m³）
        public double GlassElasticModulus = 72000; // E MPa

        // ── 埋件 ──
        public double AnchorAxialN = 0;            // 轴力 N
        public double AnchorBarDiaMm = 12;         // 锚筋直径 d
        public double AnchorBarCount = 4;          // 锚筋根数
        public double AnchorFy = 270;              // 锚筋 fy
        public double ConcreteFc = 9.6;            // 混凝土 fc（C20）
        public double ConcreteFt = 1.1;            // 混凝土 ft（C20）
        public double AnchorZ = 110;               // 内力臂 z
        public double AnchorPlateThicknessMm = 8;  // 锚板厚度 t
    }

    public sealed class Result
    {
        // 荷载组合（KPa）
        public double PosWindKpa;   // 0.571
        public double NegWindKpa;   // 2.283
        public double StdCombineKpa; // 1.883
        public double DsnCombineKpa; // 3.024

        // 悬臂梁
        public double CantileverSpanMm; // 3000
        public double BeamSigma;        // 312.696
        public double BeamDeflection;   // 38.185
        public double BeamDeflLimit;    // 24
        public bool BeamOk;
        public double TotalMoment;      // 20389320
        public double TotalShear;       // 13608

        // 焊缝
        public double WeldSigma;        // 217.197

        // 玻璃
        public double GlassSigma;       // 45.052
        public double GlassDeflection;  // 20.301
        public double GlassDeflLimit;   // 20.833
        public bool GlassOk;

        // 埋件
        public double AnchorAreaReq;    // 2237.644
        public double AnchorAreaActual; // 452.16
        public double AnchorLab;        // 518.4
        public double AnchorLa;         // 2565.452
    }

    private static double R(double v, int d) => WindLoadEngine.VbaRound(v, d);

    public static Result Compute(Input it)
    {
        if (it is null) throw new ArgumentNullException(nameof(it));
        if (it.ConcentratedLoads is null || it.ConcentratedLoads.Count == 0)
            throw new ArgumentException("集中荷载不能为空", nameof(it));
        var r = new Result();

        // ═══ 1. 荷载组合 ═══
        double w0 = it.BasicWindKpa / 1000.0;   // 0.00045 N/mm²
        double mzms = it.MuZ * it.MuS;         // μz·μs = 2.53647207（中间不 round）

        double posWindNmm = R(mzms * it.PosShapeCoeff * w0, 6);   // 0.000571
        double negWindNmm = R(mzms * it.NegShapeCoeff * w0, 6);   // 0.002283
        r.PosWindKpa = posWindNmm * 1000.0;   // 0.571
        r.NegWindKpa = negWindNmm * 1000.0;   // 2.283

        double dead = it.DeadKpa / 1000.0;     // 0.0004
        double qStd = R(negWindNmm - dead, 6);   // 0.001883（负压−恒）
        double qDsn = R(1.5 * negWindNmm - dead, 6); // 0.003024（1.5·负压−恒）
        r.StdCombineKpa = qStd * 1000.0;  // 1.883
        r.DsnCombineKpa = qDsn * 1000.0;  // 3.024

        // ═══ 2. 悬臂梁（集中荷载）═══
        double L = it.EccentricityMm + it.PanelWidthMm + it.PanelLengthMm; // 3000
        r.CantileverSpanMm = L;
        double E = it.BeamElasticModulus;
        double I = it.BeamInertia;
        double W = it.BeamSectionModulus;

        double M = 0.0;
        double defl = 0.0;
        foreach (var ld in it.ConcentratedLoads)
        {
            double vStd = R(qStd * ld.TribWidthMm * ld.TribLengthMm / 2.0, 3); // 标准 V
            double vDsn = R(qDsn * ld.TribWidthMm * ld.TribLengthMm / 2.0, 3); // 设计 V
            double a = ld.PositionMm;
            // 弯矩（设计）
            M += R(vDsn * a, 3);
            // 悬臂梁挠度 f = P·a²·(3L−a)/(6EI)（用标准 V）
            double a2 = a * a;
            double la = R(a / L, 6);
            double f = R(vStd * a2 * L * (3.0 - la) / 6.0 / E / I, 3);
            defl += f;
        }
        r.TotalMoment = R(M, 3);
        r.BeamSigma = R(r.TotalMoment / it.BeamPlasticFactor / W, 3);   // 312.696
        r.BeamDeflection = R(defl, 3);                                  // 38.185
        r.BeamDeflLimit = R(2.0 * L / 250.0, 3);                        // 24
        r.BeamOk = r.BeamSigma <= 215.0 && r.BeamDeflection <= r.BeamDeflLimit;

        // 总剪力（焊缝用）V = q设计·L·B
        r.TotalShear = R(qDsn * L * it.PanelWidthMm, 3);                // 13608

        // ═══ 3. 焊缝 ═══
        // he = 0.707·hf = 2.828（焊缝有效高度，仅展示，σ 用 W_weld/A_weld 直接计算）
        double mW = r.TotalMoment / 1.22 / it.WeldSectionModulus;
        double vA = r.TotalShear / it.WeldArea;
        r.WeldSigma = R(Math.Sqrt(mW * mW + vA * vA), 3);               // 217.197

        // ═══ 4. 玻璃（点式支承）═══
        double gDead = (it.GlassDensity / 1e6) * it.GlassThicknessMm;     // 0.000256（γ/1e6·t）
        double gStd = R(negWindNmm - gDead, 6);              // wk = 负压−自重 0.002027
        double gDsn = R(1.5 * negWindNmm - gDead, 6);        // qd = 1.5·负压−自重 0.003168
        double ga = it.GlassShortSideMm;
        double t = it.GlassThicknessMm;
        double eta = it.GlassEta;
        double Ge = it.GlassElasticModulus;

        r.GlassSigma = R(6.0 * it.GlassM * gDsn * ga * ga * eta / (t * t), 3);   // 45.052
        double D = Ge * t * t * t / 11.52;                                     // 6250000
        r.GlassDeflection = R(eta * it.GlassMu * gStd * Math.Pow(ga, 4.0) / D, 3); // 20.301
        r.GlassDeflLimit = R(ga / 60.0, 3);                                     // 20.833
        r.GlassOk = r.GlassSigma <= 84.0 && r.GlassDeflection <= r.GlassDeflLimit;

        // ═══ 5. 埋件锚固 ═══
        double d = it.AnchorBarDiaMm;
        double n = it.AnchorBarCount;
        double fy = it.AnchorFy;
        double fc = it.ConcreteFc;
        double ft = it.ConcreteFt;
        double z = it.AnchorZ;

        double alphaV = R((4.0 - 0.08 * d) * Math.Sqrt(fc / fy), 3);   // 0.573
        double alphaB = R(0.6 + 0.25 * it.AnchorPlateThicknessMm / d, 3); // 0.767
        double AsActual = R(n * 3.14 * d * d / 4.0, 3);                 // 452.16
        r.AnchorAreaActual = AsActual;

        // asReq 分支2：N/(0.8·αb·fy) + M/(0.4·αb·fy·z)（无剪力项，取大）
        double termN = it.AnchorAxialN / (0.8 * alphaB * fy);
        double termM2 = r.TotalMoment / (0.4 * alphaB * fy * z);
        r.AnchorAreaReq = R(termN + termM2, 3);                          // 2237.644

        // lab = α·ft · (fy/ft) · d（α=0.16 光圆，ft 相消 → α·fy·d）
        double alpha = 0.16;
        r.AnchorLab = R(alpha * ft * (fy / ft) * d, 3);                 // 518.4
        r.AnchorLa = R(r.AnchorAreaReq / AsActual * r.AnchorLab, 3);    // 2565.452

        return r;
    }
}
