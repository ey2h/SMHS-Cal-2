using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 采光顶杆件计算引擎（V31「采光顶>单坡」，749×546）。
/// 主龙骨（沿坡长）+ 副龙骨（垂直坡长）双向受力，荷载沿坡度分解。
/// 取证参照 caiguangdanpo_var.json（48 rounds + 163 var，0x120BC03–0x121B6B2）。
/// </summary>
public static class SkylightEngine
{
    public sealed class Input
    {
        /// <summary>坡度角 α（度），默认 30。</summary>
        public double SlopeAngleDeg = 30.0;
        /// <summary>恒载标准值 G（KPa），默认 0.4。</summary>
        public double DeadLoadKpa = 0.4;
        /// <summary>活载标准值 Q（KPa），默认 0.5。</summary>
        public double LiveLoadKpa = 0.5;

        /// <summary>主龙骨跨度（水平投影 mm），默认 3000。</summary>
        public double MainSpanMm = 3000.0;
        /// <summary>主龙骨间距（mm），默认 1500。</summary>
        public double MainSpacingMm = 1500.0;
        /// <summary>主龙骨截面抵抗矩 W（mm³），默认 10400。</summary>
        public double MainSectionModulus = 10400.0;
        /// <summary>主龙骨截面积 A（mm²），默认 692.8。</summary>
        public double MainArea = 692.8;
        /// <summary>主龙骨惯性矩 I（mm⁴），默认 260000。</summary>
        public double MainInertia = 260000.0;

        /// <summary>副龙骨跨度（mm），默认 1500。</summary>
        public double SubSpanMm = 1500.0;
        /// <summary>副龙骨间距（mm），默认 1500。</summary>
        public double SubSpacingMm = 1500.0;
        /// <summary>副龙骨截面抵抗矩 W（mm³），默认 1580。</summary>
        public double SubSectionModulus = 1580.0;
        /// <summary>副龙骨惯性矩 I（mm⁴），默认 51700。</summary>
        public double SubInertia = 51700.0;

        /// <summary>弹性模量 E（MPa），默认 70000。</summary>
        public double ElasticModulus = 70000.0;
    }

    public sealed class Result
    {
        public double MainSigma;              // 376.235
        public double MainCombinedDeflection; // 185.976
        public double MainCombinedLimit;      // 17.321
        public bool MainCombinedOk;
        public double MainDeadDeflection;     // 61.82
        public double MainDeadLimit;          // 6.928
        public bool MainDeadOk;

        public double SubSigma;               // 446.44
        public double SubParaCombinedDeflection;  // 10.237
        public double SubVertCombinedDeflection;  // 21.041
        public double SubParaDeadDeflection;      // 5.464
        public double SubVertDeadDeflection;      // 6.062
        public double SubParaLimit;           // 8.333
        public double SubVertLimit;           // 7.5
        public double SubDeadLimit;           // 3（L2/500）
    }

    private static double R(double v, int d) => WindLoadEngine.VbaRound(v, d);

    public static Result Compute(Input it)
    {
        if (it is null) throw new ArgumentNullException(nameof(it));
        var r = new Result();

        double rad = Math.PI / 180.0;
        double cos = Math.Cos(it.SlopeAngleDeg * rad);        // 精确 cos（荷载分解用）
        double cos4 = R(cos, 4);                              // round4 cos=0.866（斜长用）
        double sin = Math.Sin(it.SlopeAngleDeg * rad);

        double G = it.DeadLoadKpa / 1000.0;   // 0.0004
        double Q = it.LiveLoadKpa / 1000.0;   // 0.0005

        double Gd = R(1.3 * G, 6);   // 0.00052
        double Qd = R(1.5 * Q, 6);   // 0.00075

        // ── 主龙骨法向设计线荷载 ──
        double normal = R(R(Gd + Qd, 6) / cos, 6);        // 0.001466
        double qdNormal = R(R(normal + 0.7 * Qd, 6) * cos, 6);  // 0.001724
        double qMain = R(qdNormal * it.MainSpacingMm, 3); // 2.586

        // 斜长（用 round4 cos = 0.866）
        double L = R(it.MainSpanMm / cos4, 4);              // 3464.2032

        // 主龙骨强度 σ = M/W + V/A；V = q·L·sin/2（切向剪力）
        double Mmain = R(qMain * L * L / 8.0, 3);
        double Vmain = R(qMain * L * sin / 2.0, 3);        // 2239.607
        r.MainSigma = R(Mmain / it.MainSectionModulus + Vmain / it.MainArea, 3);

        // 主龙骨组合挠度
        double qCombinedStd = R(R((G + Q) / cos, 6) + 0.7 * Q, 6);
        double qCombined = R(R(qCombinedStd * cos, 6) * it.MainSpacingMm, 3);  // 1.805
        r.MainCombinedDeflection = R(5.0 * qCombined * Math.Pow(L, 4.0)
                                     / (384.0 * it.ElasticModulus * it.MainInertia), 3);
        r.MainCombinedLimit = R(L / 200.0, 3);
        r.MainCombinedOk = r.MainCombinedDeflection <= r.MainCombinedLimit;

        // 主龙骨自重挠度
        double qDead = R(G * it.MainSpacingMm, 3);   // 0.6
        r.MainDeadDeflection = R(5.0 * qDead * Math.Pow(L, 4.0)
                                 / (384.0 * it.ElasticModulus * it.MainInertia), 3);
        r.MainDeadLimit = R(L / 500.0, 3);
        r.MainDeadOk = r.MainDeadDeflection <= r.MainDeadLimit;

        // ── 副龙骨（双向受弯）──
        double L2 = it.SubSpanMm;
        double E = it.ElasticModulus;
        double I2 = it.SubInertia;

        // 平行组合线荷载（标准）与垂直组合线荷载（设计）
        double qParaComb = R(R(G + 0.7 * Q, 6) * it.SubSpacingMm * sin, 3);   // 0.562
        double qVertComb = R(R(Gd + 0.7 * Qd, 6) * it.SubSpacingMm * sin, 3); // 0.784

        // 平行（简支 /8）用组合线荷载 0.784；垂直（固支 /12）用主龙骨 qMain 2.586
        double Mpara = qVertComb * L2 * L2 / 8.0;   // 220500
        double Mvert = qMain * L2 * L2 / 12.0;      // 484875
        r.SubSigma = R(Mpara / it.SubSectionModulus + Mvert / it.SubSectionModulus, 3);  // 446.44

        // 副龙骨 4 组挠度
        double L24 = Math.Pow(L2, 4.0);
        r.SubParaCombinedDeflection = R(5.0 * qParaComb * L24 / (384.0 * E * I2), 3);
        r.SubVertCombinedDeflection = R(qCombined * L24 / (120.0 * E * I2), 3);

        double qParaDead = R(G * it.SubSpacingMm * sin, 3);   // 0.3
        double qVertDead = R(G * it.SubSpacingMm * cos, 3);   // 0.52
        r.SubParaDeadDeflection = R(5.0 * qParaDead * L24 / (384.0 * E * I2), 3);
        r.SubVertDeadDeflection = R(qVertDead * L24 / (120.0 * E * I2), 3);

        r.SubParaLimit = R(L2 / 180.0, 3);   // 8.333
        r.SubVertLimit = R(L2 / 200.0, 3);   // 7.5
        r.SubDeadLimit = R(L2 / 500.0, 3);   // 3

        return r;
    }
}
