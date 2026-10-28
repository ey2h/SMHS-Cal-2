using System;

namespace GlassManagement.Calc.Engines
{
    /// <summary>
    /// 插值计算器引擎（菜单 id=190，窗体「插值计算器」）。
    /// 两点线性插值：y = y1 + (x-x1)/(x2-x1)·(y2-y1)，结果 round5。
    /// V31 运行期实测：x1=125,y1=0.1235,x2=280,y2=0.1358,x=200 → y=0.12945。
    /// </summary>
    public sealed class InterpolationEngine
    {
        public static double Linear(double x, double x1, double y1, double x2, double y2)
        {
            if (Math.Abs(x2 - x1) < 1e-12)
                throw new ArgumentException("插值区间 x1==x2，无法插值");
            double y = y1 + (x - x1) / (x2 - x1) * (y2 - y1);
            return WindLoadEngine.VbaRound(y, 5);
        }
    }

    /// <summary>
    /// 方程求解引擎（菜单 id=191，窗体「方程求解」）。
    /// 一元二次 ax²+bx+c=0：求根公式 x=(-b±√(b²-4ac))/(2a)，round5。
    /// 一元三次 ax³+bx²+cx+d=0：卡尔丹公式，输出实根（15 位小数）。
    /// V31 运行期实测：5x²+2x-15=0 → 1.54356 / -1.94356；500x³+45x²+25x+15=0 → -0.284632890979535。
    /// </summary>
    public sealed class EquationEngine
    {
        public readonly record struct Quadratic(double X1, double X2, bool HasRealRoots);

        /// <summary>一元二次方程求根（round5）。判别式&lt;0 返回 HasRealRoots=false。</summary>
        public static Quadratic SolveQuadratic(double a, double b, double c)
        {
            double delta = b * b - 4 * a * c;
            if (delta < 0)
                return new Quadratic(0, 0, false);
            double sq = Math.Sqrt(delta);
            double x1 = WindLoadEngine.VbaRound((-b + sq) / (2 * a), 5);
            double x2 = WindLoadEngine.VbaRound((-b - sq) / (2 * a), 5);
            return new Quadratic(x1, x2, true);
        }

        /// <summary>一元三次方程实根（卡尔丹公式，返回实根列表，未 round）。</summary>
        public static double[] SolveCubic(double a, double b, double c, double d)
        {
            if (Math.Abs(a) < 1e-15)
            {
                // 退化为二次
                var q2 = SolveQuadratic(b, c, d);
                return q2.HasRealRoots ? new[] { q2.X1, q2.X2 } : Array.Empty<double>();
            }
            // 归一化 x³ + Bx² + Cx + D = 0
            double B = b / a, C = c / a, D = d / a;
            // 代换 x = y - B/3 → y³ + py + q = 0
            double p = C - B * B / 3.0;
            double q = 2.0 * B * B * B / 27.0 - B * C / 3.0 + D;
            double shift = -B / 3.0;

            double disc = q * q / 4.0 + p * p * p / 27.0;
            var roots = new System.Collections.Generic.List<double>();
            if (disc > 0)
            {
                // 一个实根
                double u = Cbrt(-q / 2.0 + Math.Sqrt(disc));
                double v = Cbrt(-q / 2.0 - Math.Sqrt(disc));
                roots.Add(u + v + shift);
            }
            else if (Math.Abs(disc) < 1e-15)
            {
                // 两个实根（一个二重根）
                double u = Cbrt(-q / 2.0);
                roots.Add(2 * u + shift);
                roots.Add(-u + shift);
            }
            else
            {
                // 三个实根（卡尔丹三角形式）：x_k = r·cos(θ - 2πk/3) + shift
                double r = 2.0 * Math.Sqrt(-p / 3.0);
                double arg = 3.0 * q / (2.0 * p) * Math.Sqrt(-3.0 / p);
                arg = Math.Clamp(arg, -1.0, 1.0);
                double theta = Math.Acos(arg) / 3.0;
                for (int k = 0; k < 3; k++)
                {
                    double y = r * Math.Cos(theta - 2.0 * Math.PI * k / 3.0);
                    roots.Add(y + shift);
                }
            }
            roots.Sort();
            return roots.ToArray();
        }

        private static double Cbrt(double x) => x < 0 ? -Math.Pow(-x, 1.0 / 3.0) : Math.Pow(x, 1.0 / 3.0);
    }
}
