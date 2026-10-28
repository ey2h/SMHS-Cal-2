using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 转角立柱计算引擎（双向受弯，转角处两方向风荷载组合）。
///
/// <para><b>来源</b>：V31 实机「其它计算 → 转角立柱计算」屏预演输出。</para>
///
/// <list type="bullet">
/// <item>转角 90° 分解 cos45；受荷宽度 = 两侧分格和；</item>
/// <item>工况一 σ = Mx/Wx + 0.6·My/Wy + N/A；工况二 σ = My/Wy + 0.6·Mx/Wx + N/A；</item>
/// <item>Y/X 轴挠度 = 5·q标准·L⁴/(384·E·I)。</item>
/// </list>
/// </summary>
public sealed class CornerColumnEngine
{
    public sealed class Input
    {
        /// <summary>风荷载标准值 wk（N/mm²，默认 0.001125）。</summary>
        public double Wk = 0.001125;

        /// <summary>受荷宽度（mm，默认 2250 = 1200+1050）。</summary>
        public double LoadWidth = 2250;

        /// <summary>X 方向跨度 Lx（mm，默认 3600）。</summary>
        public double SpanX = 3600;

        /// <summary>Y 方向跨度 Ly（mm，默认 2500）。</summary>
        public double SpanY = 2500;

        /// <summary>自重线荷载（N/mm²，默认 0.0005）。</summary>
        public double DeadLoad = 0.0005;

        /// <summary>X 向抵抗矩 Wx（mm³，默认 45841）。</summary>
        public double Wx = 45841;

        /// <summary>Y 向抵抗矩 Wy（mm³，默认 27014）。</summary>
        public double Wy = 27014;

        /// <summary>截面面积 A（mm²，默认 1224）。</summary>
        public double AreaA = 1224;

        /// <summary>Y 向惯性矩 Iy（mm⁴，默认 3438070）。</summary>
        public double Iy = 3438070;

        /// <summary>X 向惯性矩 Ix（mm⁴，默认 810430）。</summary>
        public double Ix = 810430;

        /// <summary>弹性模量 E（N/mm²，默认 70000）。</summary>
        public double EMpa = 70000;

        /// <summary>抗弯强度设计值（MPa，默认 90）。</summary>
        public double SigmaAllowable = 90;
    }

    public sealed class Result
    {
        public double QStandard;   // 0.895
        public double Mx;          // 2264760
        public double My;          // 1092187.5
        public double AxialN;      // 2632.5
        public double SigmaCase1;  // 75.814
        public double SigmaCase2;  // 72.224
        public bool SigmaOk;
        public double DeflectionY; // 8.133
        public double DeflectionX; // 8.024
        public double DeflectionAllowable; // 20
        public bool DeflectionOk;
    }

    private const double Cos45 = 0.7071067811865475;

    public static Result Compute(Input it)
    {
        if (it is null) throw new ArgumentNullException(nameof(it));
        var r = new Result();

        // q标准 = wk × (受荷宽度/2) × cos45
        r.QStandard = WindLoadEngine.VbaRound(
            it.Wk * (it.LoadWidth / 2.0) * Cos45, 3);   // 0.895

        // 设计线荷载（1.5wk + 0.7×5αmax·γ·t 的地震项，此处实机 αmax=0.04 → qd'=0.0001）
        double qdSeis = 5.0 * 0.04 * it.DeadLoad;       // 0.0001
        double qd = WindLoadEngine.VbaRound(1.5 * it.Wk + 0.7 * qdSeis, 6); // 0.001757
        double qdLine = WindLoadEngine.VbaRound(
            qd * (it.LoadWidth / 2.0) * Cos45, 3);       // 1.398

        r.Mx = WindLoadEngine.VbaRound(qdLine * it.SpanX * it.SpanX / 8.0, 3); // 2264760
        r.My = WindLoadEngine.VbaRound(qdLine * it.SpanY * it.SpanY / 8.0, 3); // 1092187.5

        r.AxialN = WindLoadEngine.VbaRound(
            1.3 * it.DeadLoad * it.LoadWidth * it.SpanX / 2.0, 3);   // 2632.5

        r.SigmaCase1 = WindLoadEngine.VbaRound(
            r.Mx / it.Wx + 0.6 * r.My / it.Wy + r.AxialN / it.AreaA, 3); // 75.814
        r.SigmaCase2 = WindLoadEngine.VbaRound(
            r.My / it.Wy + 0.6 * r.Mx / it.Wx + r.AxialN / it.AreaA, 3); // 72.224
        r.SigmaOk = r.SigmaCase1 <= it.SigmaAllowable && r.SigmaCase2 <= it.SigmaAllowable;

        r.DeflectionAllowable = WindLoadEngine.VbaRound(it.SpanX / 180.0, 3); // 20
        r.DeflectionY = WindLoadEngine.VbaRound(
            5.0 * r.QStandard * Math.Pow(it.SpanX, 4.0) / (384.0 * it.EMpa * it.Iy), 3); // 8.133
        r.DeflectionX = WindLoadEngine.VbaRound(
            5.0 * r.QStandard * Math.Pow(it.SpanY, 4.0) / (384.0 * it.EMpa * it.Ix), 3); // 8.024
        r.DeflectionOk = r.DeflectionY <= r.DeflectionAllowable && r.DeflectionX <= r.DeflectionAllowable;
        return r;
    }
}
