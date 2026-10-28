using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 其它玻璃计算引擎（V31「其它计算>其它玻璃计算」，目前只支持四边简支）。
/// 四玻外夹层双中空构型：外夹层(2片)+中空+内夹层(2片)。
/// 取证参照 qitaboli_var.json（0x167317C–0x1679DF9）。
/// </summary>
public static class OtherGlassEngine
{
    public sealed class Input
    {
        /// <summary>外片厚度 t1（mm），默认 6。</summary>
        public double OuterThicknessMm = 6.0;
        /// <summary>次外片厚度 t2（mm），默认 6。</summary>
        public double Outer2ThicknessMm = 6.0;
        /// <summary>次内片厚度 t3（mm），默认 6。</summary>
        public double Inner2ThicknessMm = 6.0;
        /// <summary>内片厚度 t4（mm），默认 6。</summary>
        public double InnerThicknessMm = 6.0;

        /// <summary>玻璃短边 a（mm），默认 1000。</summary>
        public double ShortSideMm = 1000.0;
        /// <summary>玻璃长边 b（mm），默认 2000。</summary>
        public double LongSideMm = 2000.0;

        /// <summary>地震影响系数 αmax，默认 0.16。</summary>
        public double AlphaMax = 0.16;
        /// <summary>风荷载标准值 wk（N/mm²），默认 0.001（=1KPa）。</summary>
        public double WkNmm2 = 0.001;
    }

    public sealed class Result
    {
        public double OuterSigma;      // 外片 8.833
        public double Outer2Sigma;     // 次外片 8.833
        public double Inner2Sigma;     // 次内片 7.217
        public double InnerSigma;      // 内片 7.217
        public double SigmaAllowable = 84;
        public bool OuterOk, Outer2Ok, Inner2Ok, InnerOk;
        public double Deflection;      // 2.356
        public double DeflectionAllowable; // 16.667
        public bool DeflectionOk;
    }

    private static double R(double v, int d) => WindLoadEngine.VbaRound(v, d);

    public static Result Compute(Input it)
    {
        if (it is null) throw new ArgumentNullException(nameof(it));
        var r = new Result();

        double a = Math.Min(it.ShortSideMm, it.LongSideMm);
        double b = Math.Max(it.ShortSideMm, it.LongSideMm);
        double ratio = a / b;   // 0.5

        // 四边简支 m/μ（r=0.5 精确断点）
        double m = 0.1000;
        double mu = 0.01013;

        double t1 = it.OuterThicknessMm, t2 = it.Outer2ThicknessMm;
        double t3 = it.Inner2ThicknessMm, t4 = it.InnerThicknessMm;

        // 夹层单元等效厚度：外夹层×1、内夹层×0.95（四玻外夹层双中空）
        double teOuter = R(Math.Pow(t1 * t1 * t1 + t2 * t2 * t2, 1.0 / 3.0), 3);      // 7.56
        double teInner = R(0.95 * Math.Pow(t3 * t3 * t3 + t4 * t4 * t4, 1.0 / 3.0), 3); // 7.182

        // 荷载分配（夹层单元级，外×1.1）
        double dOuter = teOuter * teOuter * teOuter;   // 432.081
        double dInner = teInner * teInner * teInner;   // 370.456
        double sum = dOuter + dInner;
        double qdOuter = 1.1 * it.WkNmm2 * dOuter / sum;   // 0.000592
        double qdInner = 1.0 * it.WkNmm2 * dInner / sum;   // 0.000462

        // 地震项 qd' = 5·αmax·(γ/1e6)·t（γ=25.6 kN/m³）
        double qe = R(5.0 * it.AlphaMax * GlassEngine.DensityNmm3 * t1, 6);  // 0.000123

        // 各片强度（单片平分夹层单元荷载）
        r.OuterSigma = PlySigma(a, t1, qdOuter, t1, t2, qe, m);
        r.Outer2Sigma = PlySigma(a, t2, qdOuter, t1, t2, qe, m);
        r.Inner2Sigma = PlySigma(a, t3, qdInner, t3, t4, qe, m);
        r.InnerSigma = PlySigma(a, t4, qdInner, t3, t4, qe, m);

        r.OuterOk = r.OuterSigma <= r.SigmaAllowable;
        r.Outer2Ok = r.Outer2Sigma <= r.SigmaAllowable;
        r.Inner2Ok = r.Inner2Sigma <= r.SigmaAllowable;
        r.InnerOk = r.InnerSigma <= r.SigmaAllowable;

        // 整体等效厚度 + 挠度
        double te = 0.95 * Math.Pow(sum, 1.0 / 3.0);    // 8.828
        double stiff = GlassEngine.E * Math.Pow(te, 3.0) / GlassEngine.PlateFactor;
        r.Deflection = R(mu * it.WkNmm2 * Math.Pow(a, 4.0) / stiff, 3);
        r.DeflectionAllowable = R(a / GlassEngine.DeflectionDivisor, 3);  // 16.667
        r.DeflectionOk = r.Deflection <= r.DeflectionAllowable;

        return r;
    }

    private static double PlySigma(double a, double t, double qdUnit, double tA, double tB, double qe, double m)
    {
        // 单片 qd = qdUnit × t³/(tA³+tB³)
        double share = t * t * t / (tA * tA * tA + tB * tB * tB);
        double qd = R(qdUnit * share, 6);
        double qdStr = R(1.5 * qd + 0.7 * qe, 6);
        // η：默认 6mm 玻璃 θ 很小 → η=1（实机未显示折减）
        return R(6.0 * m * qdStr * a * a / (t * t), 3);
    }
}
