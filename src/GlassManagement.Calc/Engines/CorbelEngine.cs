using System;

namespace GlassManagement.Calc.Engines
{
    /// <summary>
    /// 牛腿结构计算引擎（菜单 id=170，窗体「牛腿结构计算」）。
    /// <para>公式来源：V31 运行期 frida 抓 `__vbaVar*` 链（recon/v31runs/b8_probe_corbel.json，
    /// 默认算例 73 条记录逐条还原），不是反推。</para>
    /// <para>默认算例：L=2000 H=1600 θ=30° 荷载 2000/3500/1428/2916 截面 L50*5
    /// （Ix=11.21cm⁴ Wx=3.13cm³ A=480.3mm²）→ 主梁A 200.08 / 主梁C 430.149 /
    /// 斜梁 46.075 / 长细比 120.916 / 挠度 10.878（限 16）。</para>
    /// </summary>
    public static class CorbelEngine
    {
        /// <summary>★ V31 用 π=3.14（不是 Math.PI）——链 ra=0x1B668AA `94.2/180`，cos(30°) 得 0.866158。</summary>
        public const double Pi = 3.14;

        public sealed class Input
        {
            public double L = 2000;        // 尺寸参数1
            public double H = 1600;        // 尺寸参数2
            public double AngleDeg = 30;   // 尺寸参数3（斜梁角度）
            public double AxialN = 2000;   // 荷载参数1（轴向力 N）
            public double P = 3500;        // 荷载参数2（竖向力 N）
            public double Q = 1428;        // 荷载参数3（本样式未进链，保留）
            public double M0 = 2916;       // 荷载参数4
            public double Ix = 112100;     // mm⁴（11.21 cm⁴）
            public double Wx = 3130;       // mm³（3.13 cm³）
            public double Area = 480.3;    // mm²
            public double E = 206000;      // MPa（钢）
            public double Gamma = 1.05;    // 塑性发展系数
            public double AllowBending = 215;   // 强度设计值 MPa（Q235）
            public double AllowSlenderness = 200;
        }

        public sealed class Result
        {
            public double B;                  // L−H
            public double Cos, Sin;           // 三角函数
            public double K1;                 // 2−3b/L+(b/L)³
            public double ThrustR;            // 斜梁水平推力 R
            public double MomentA;            // 主梁A点弯矩
            public double AxialA;             // 主梁A点轴力（N0+Ndiag·cosθ）
            public double StressA;            // 主梁A点强度
            public double StressC;            // 主梁C点强度
            public double Ndiag;              // 斜梁轴力
            public double Gyration;            // 回转半径 i=√(Ix/A)
            public double DiagLength;          // 斜梁长度 H/cosθ
            public double Slenderness;         // 长细比 λ
            public double Phi;                 // 稳定系数
            public double StressDiag;          // 斜梁强度
            public double DeflectD1, DeflectD2, Deflection;
            public double DeflectionLimit;
        }

        // ============================================================
        // 轴心受压稳定系数 φ（b 类截面）—— 31 个 (λ, φ) 点，全部为 V31 运行期直读
        // （frida 读 ra=0x1B6829B 的 Div 除数，改 H 扫 λ）。
        // 来源 b8_corbel_phi_table.json；λ∈[37.79,151.15]，区间外按端点斜率外推。
        // ============================================================
        private static readonly (double Lambda, double Phi)[] PhiTable =
        {
            (37.786, 0.908), (41.565, 0.893), (45.344, 0.876), (49.122, 0.859),
            (52.901, 0.842), (56.679, 0.825), (60.458, 0.807), (64.237, 0.785),
            (68.015, 0.762), (71.794, 0.739), (75.573, 0.717), (79.351, 0.694),
            (83.130, 0.668), (86.909, 0.642), (90.687, 0.616), (94.466, 0.593),
            (98.244, 0.571), (102.023, 0.546), (105.802, 0.519), (109.580, 0.493),
            (113.359, 0.473), (117.138, 0.454), (120.916, 0.435), (124.695, 0.417),
            (128.474, 0.398), (136.031, 0.366), (139.809, 0.351), (143.588, 0.336),
            (147.367, 0.321), (151.145, 0.310),
        };

        /// <summary>按长细比取稳定系数 φ（线性插值；两端按端点斜率外推）。</summary>
        public static double PhiOf(double lambda)
        {
            var t = PhiTable;
            if (lambda <= t[0].Lambda)
            {
                // 低端外推：λ→0 时 φ→1（斜率取首两点）
                double k0 = (t[1].Phi - t[0].Phi) / (t[1].Lambda - t[0].Lambda);
                return Math.Min(1.0, t[0].Phi + k0 * (lambda - t[0].Lambda));
            }
            int last = t.Length - 1;
            if (lambda >= t[last].Lambda)
            {
                double kn = (t[last].Phi - t[last - 1].Phi) / (t[last].Lambda - t[last - 1].Lambda);
                return Math.Max(0.0, t[last].Phi + kn * (lambda - t[last].Lambda));
            }
            for (int i = 0; i < last; i++)
            {
                if (lambda >= t[i].Lambda && lambda <= t[i + 1].Lambda)
                {
                    double f = (lambda - t[i].Lambda) / (t[i + 1].Lambda - t[i].Lambda);
                    return t[i].Phi + (t[i + 1].Phi - t[i].Phi) * f;
                }
            }
            return t[last].Phi;
        }

        public static Result Compute(Input it)
        {
            var o = new Result();
            o.B = it.L - it.H;

            double th = it.AngleDeg * Pi / 180.0;
            o.Cos = Math.Cos(th);
            o.Sin = Math.Sin(th);

            // ---- 斜梁水平推力 R（链 5~23）----
            double bl = o.B / it.L;
            o.K1 = 2.0 - 3.0 * bl + Math.Pow(bl, 3);
            double num = it.M0 * it.Area * Math.Pow(it.L, 3) * o.Cos * o.K1;
            double den = 2.0 * it.Area * Math.Pow(it.H, 3) * o.Cos + 6.0 * it.Ix * it.H;
            // ★ V31 把 R 存槽位时显式 round3（链 23→31：out=4008.234302 但 31 的 op1=4008.234）
            o.ThrustR = WindLoadEngine.VbaRound(den != 0 ? num / den : 0, 3);

            // ---- 主梁 A 点（链 30~40）----
            o.MomentA = it.P * it.L - o.ThrustR * it.H;

            // ---- 斜梁轴力（链 26~28）：用 round3 后的 R；结果同样 round3（54 的 op1=9626.387）----
            o.Ndiag = WindLoadEngine.VbaRound(
                o.Sin != 0 && it.M0 != 0 ? o.ThrustR * it.P / it.M0 / o.Sin : 0, 3);
            o.AxialA = it.AxialN + o.Ndiag * o.Cos;

            // σA = (N0+Ndiag·cosθ)/A + MA/(γ·Wx) —— 两个分项**不单独 round**，只对合计 round3
            //   （链 40: op1=21.523991 + op2=178.556397 → 200.080388）
            o.StressA = WindLoadEngine.VbaRound(o.AxialA / it.Area + o.MomentA / it.Gamma / it.Wx, 3);

            // ---- 主梁 C 点（链 29 / 41~44）----
            o.StressC = WindLoadEngine.VbaRound(
                it.AxialN / it.Area + it.P * o.B / it.Gamma / it.Wx, 3);

            // ---- 长细比（链 48~53）：i、Ldiag、λ 均 round3（链 49→53：op2=15.277、op1=1847.238）----
            o.Gyration = WindLoadEngine.VbaRound(
                it.Area > 0 ? Math.Sqrt(it.Ix / it.Area) : 0, 3);
            o.DiagLength = WindLoadEngine.VbaRound(o.Cos != 0 ? it.H / o.Cos : 0, 3);
            o.Slenderness = WindLoadEngine.VbaRound(
                o.Gyration > 0 ? o.DiagLength / o.Gyration : 0, 3);

            // ---- 斜梁强度（链 54~55）：N/(φ·A)，φ 查表 ----
            o.Phi = PhiOf(o.Slenderness);
            o.StressDiag = o.Phi > 0 && it.Area > 0
                ? WindLoadEngine.VbaRound(o.Ndiag / o.Phi / it.Area, 3) : 0;

            // ---- 主梁挠度（链 56~70）：d1、d2·(3−H/L) 各自 round3 后相减 ----
            o.DeflectD1 = WindLoadEngine.VbaRound(it.E != 0 && it.Ix != 0
                ? it.M0 * Math.Pow(it.L, 3) / 3.0 / it.E / it.Ix : 0, 3);
            o.DeflectD2 = WindLoadEngine.VbaRound(it.E != 0 && it.Ix != 0
                ? o.ThrustR * it.H * it.H * it.L / 6.0 / it.E / it.Ix : 0, 3);
            double term2 = WindLoadEngine.VbaRound(o.DeflectD2 * (3.0 - it.H / it.L), 3);
            o.Deflection = WindLoadEngine.VbaRound(o.DeflectD1 - term2, 3);

            // ---- 挠度设计值（链 71~72）：2L/250 ----
            o.DeflectionLimit = WindLoadEngine.VbaRound(2.0 * it.L / 250.0, 3);
            return o;
        }

        /// <summary>V31 输出正文（逐字对齐 RichText，含原文笔误「A点强度不满足」用于 C 点）。</summary>
        public static string FormatOutput(Input it, Result o)
        {
            const string bar = "***********************************************";
            const string sep = "————————————————————————";
            string F(double v) => v.ToString("0.###");
            string J(bool pass) => pass ? "满足要求(√)" : "不满足要求(×)";
            return
                $"{bar}      " +
                $"主梁A点强度计算值:{F(o.StressA)}MPa      " +
                $"主梁强度设计值:{F(it.AllowBending)}MPa      " +
                $"{J(o.StressA <= it.AllowBending)}  " +
                $"{sep}      " +
                $"主梁C点强度计算值:{F(o.StressC)}MPa      " +
                $"主梁强度设计值:{F(it.AllowBending)}MPa      " +
                (o.StressC <= it.AllowBending ? "满足要求(√)" : "A点强度不满足要求(×)") + "  " +
                $"{bar}      " +
                $"斜梁强度计算值:{F(o.StressDiag)}MPa      " +
                $"斜梁强度设计值:{F(it.AllowBending)}MPa      " +
                $"{J(o.StressDiag <= it.AllowBending)}  " +
                $"{sep}      " +
                $"斜梁长细比计算值:{F(o.Slenderness)}      " +
                $"规范要求长细比不大于:{F(it.AllowSlenderness)}      " +
                $"{J(o.Slenderness <= it.AllowSlenderness)}  " +
                $"{bar}      " +
                $"主梁挠度计算值:{F(o.Deflection)}mm      " +
                $"主梁挠度设计值:{F(o.DeflectionLimit)}mm      " +
                $"{J(o.Deflection <= o.DeflectionLimit)}  " +
                $"{bar}  ";
        }
    }
}
