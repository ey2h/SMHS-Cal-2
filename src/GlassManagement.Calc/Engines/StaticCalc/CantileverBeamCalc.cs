namespace GlassManagement.Calc.Engines.StaticCalc;

/// <summary>
/// 悬臂梁计算 — 按《建筑结构静力计算手册》第3章（悬臂梁部分）
/// 坐标系：x从固定端(x=0)到自由端(x=L)
/// </summary>
public static class CantileverBeamCalc
{
    /// <summary>根据荷载类型分发计算</summary>
    public static BeamCalcResult Calculate(BeamCalcRequest req)
    {
        return req.Load switch
        {
            LoadType.Concentrated => ConcentratedLoad(req),
            LoadType.Uniform => UniformLoad(req),
            LoadType.Triangular => TriangularLoad(req),
            LoadType.Moment => MomentLoad(req),
            LoadType.PartialUniform => PartialUniformLoad(req),
            LoadType.Trapezoidal => TrapezoidalLoad(req),
            _ => UniformLoad(req)
        };
    }

    /// <summary>
    /// 工况1: 自由端集中力P — 《静力计算手册》表3-1
    /// Mmax = -PL (固定端), δmax = PL³/(3EI) (自由端)
    /// </summary>
    public static BeamCalcResult ConcentratedLoad(BeamCalcRequest req)
    {
        double P = req.P, L = req.L, EI = req.E * req.I * 1e-6;
        double a = req.A > 0 && req.A <= L ? req.A : L; // 力作用位置（从固定端算）
        int n = req.NumPoints;

        // 固定端反力
        double R = P, Mf = -P * a;
        var result = new BeamCalcResult
        {
            Reference = "《建筑结构静力计算手册》表3-1",
            Description = $"悬臂梁-集中力(a={a:F3}m)",
            Reactions = new() {
                new() { Name = "R(固定端)", Value = R },
                new() { Name = "M(固定端)", Value = Mf }
            }
        };

        for (int i = 0; i <= n; i++)
        {
            double x = L * i / n;
            double M, V, d;
            if (x <= a)
            {
                M = -P * (a - x); // 固定端处M=-Pa
                V = P;
                // δ(x) = Px²(3a-x)/(6EI) — 表3-1
                d = EI > 0 ? P * x * x * (3 * a - x) / (6 * EI) * 1000 : 0;
            }
            else
            {
                M = 0;
                V = 0;
                // δ(x) = Pa²(3x-a)/(6EI)
                d = EI > 0 ? P * a * a * (3 * x - a) / (6 * EI) * 1000 : 0;
            }
            result.MomentDiagram.Add(new() { X = x, Value = M });
            result.ShearDiagram.Add(new() { X = x, Value = V });
            result.DeflectionDiagram.Add(new() { X = x, Value = d });
        }

        result.MaxMoment = 0;
        result.MinMoment = -P * a;
        result.MinMomentPosition = 0;
        result.MaxShear = P;
        result.MaxDeflection = EI > 0 ? P * a * a * (3 * L - a) / (6 * EI) * 1000 : 0;
        result.MaxDeflectionPosition = L;
        return result;
    }

    /// <summary>
    /// 工况2: 均布荷载 — 《静力计算手册》表3-2
    /// Mmax = -qL²/2, δmax = qL⁴/(8EI)
    /// </summary>
    public static BeamCalcResult UniformLoad(BeamCalcRequest req)
    {
        double q = req.Q, L = req.L, EI = req.E * req.I * 1e-6;
        int n = req.NumPoints;

        double R = q * L, Mf = -q * L * L / 2.0;
        var result = new BeamCalcResult
        {
            Reference = "《建筑结构静力计算手册》表3-2",
            Description = "悬臂梁-均布荷载",
            Reactions = new() {
                new() { Name = "R(固定端)", Value = R },
                new() { Name = "M(固定端)", Value = Mf }
            }
        };

        for (int i = 0; i <= n; i++)
        {
            double x = L * i / n;
            // M(x) = -q(L-x)²/2
            double M = -q * (L - x) * (L - x) / 2.0;
            // V(x) = q(L-x)
            double V = q * (L - x);
            // δ(x) = qx²(6L²-4Lx+x²)/(24EI) — 表3-2
            double d = EI > 0 ? q * x * x * (6 * L * L - 4 * L * x + x * x) / (24 * EI) * 1000 : 0;
            result.MomentDiagram.Add(new() { X = x, Value = M });
            result.ShearDiagram.Add(new() { X = x, Value = V });
            result.DeflectionDiagram.Add(new() { X = x, Value = d });
        }

        result.MinMoment = Mf;
        result.MinMomentPosition = 0;
        result.MaxShear = q * L;
        result.MaxDeflection = EI > 0 ? q * Math.Pow(L, 4) / (8 * EI) * 1000 : 0;
        result.MaxDeflectionPosition = L;
        return result;
    }

    /// <summary>
    /// 工况3: 三角形荷载（固定端q，自由端0）— 《静力计算手册》表3-3
    /// Mmax = -qL²/6, δmax = qL⁴/(30EI)
    /// </summary>
    public static BeamCalcResult TriangularLoad(BeamCalcRequest req)
    {
        double q = req.Q, L = req.L, EI = req.E * req.I * 1e-6;
        int n = req.NumPoints;

        double R = q * L / 2.0, Mf = -q * L * L / 6.0;
        var result = new BeamCalcResult
        {
            Reference = "《建筑结构静力计算手册》表3-3",
            Description = "悬臂梁-三角形荷载(固定端q,自由端0)",
            Reactions = new() {
                new() { Name = "R(固定端)", Value = R },
                new() { Name = "M(固定端)", Value = Mf }
            }
        };

        for (int i = 0; i <= n; i++)
        {
            double x = L * i / n;
            // 荷载分布: q(x) = q*(L-x)/L
            // M(x) = -q(L-x)³/(6L)
            double M = -q * Math.Pow(L - x, 3) / (6.0 * L);
            // V(x) = q(L-x)²/(2L)
            double V = q * (L - x) * (L - x) / (2.0 * L);
            // δ(x) = qx²(10L³-10L²x+5Lx²-x³)/(120LEI) — 表3-3
            double d = EI > 0 ? q * x * x * (10 * L * L * L - 10 * L * L * x + 5 * L * x * x - x * x * x) / (120 * L * EI) * 1000 : 0;
            result.MomentDiagram.Add(new() { X = x, Value = M });
            result.ShearDiagram.Add(new() { X = x, Value = V });
            result.DeflectionDiagram.Add(new() { X = x, Value = d });
        }

        result.MinMoment = Mf;
        result.MinMomentPosition = 0;
        result.MaxShear = q * L / 2.0;
        result.MaxDeflection = EI > 0 ? q * Math.Pow(L, 4) / (30 * EI) * 1000 : 0;
        result.MaxDeflectionPosition = L;
        return result;
    }

    /// <summary>
    /// 工况4: 自由端集中力矩M0 — 《静力计算手册》表3-4
    /// </summary>
    public static BeamCalcResult MomentLoad(BeamCalcRequest req)
    {
        double M0 = req.M0, L = req.L, EI = req.E * req.I * 1e-6;
        int n = req.NumPoints;

        var result = new BeamCalcResult
        {
            Reference = "《建筑结构静力计算手册》表3-4",
            Description = "悬臂梁-自由端集中力矩",
            Reactions = new() {
                new() { Name = "R(固定端)", Value = 0 },
                new() { Name = "M(固定端)", Value = -M0 }
            }
        };

        for (int i = 0; i <= n; i++)
        {
            double x = L * i / n;
            // M(x) = M0 (全梁等弯矩，注意符号约定)
            double M = M0;
            double V = 0;
            // δ(x) = M0*x²/(2EI)
            double d = EI > 0 ? M0 * x * x / (2 * EI) * 1000 : 0;
            result.MomentDiagram.Add(new() { X = x, Value = M });
            result.ShearDiagram.Add(new() { X = x, Value = V });
            result.DeflectionDiagram.Add(new() { X = x, Value = d });
        }

        result.MaxMoment = M0;
        result.MaxMomentPosition = 0;
        result.MaxDeflection = EI > 0 ? M0 * L * L / (2 * EI) * 1000 : 0;
        result.MaxDeflectionPosition = L;
        return result;
    }

    /// <summary>
    /// 工况5: 部分均布荷载 — 《静力计算手册》表3-5
    /// 从自由端起算，荷载长度d
    /// </summary>
    public static BeamCalcResult PartialUniformLoad(BeamCalcRequest req)
    {
        double q = req.Q, L = req.L, EI = req.E * req.I * 1e-6;
        double c = req.LoadStart, e = req.LoadEnd;
        if (e <= c) e = L;
        int n = req.NumPoints;

        double d = e - c;
        double R = q * d;
        double Mf = -q * d * (c + d / 2.0);
        var result = new BeamCalcResult
        {
            Reference = "《建筑结构静力计算手册》表3-5",
            Description = $"悬臂梁-部分均布荷载[{c:F2},{e:F2}]",
            Reactions = new() {
                new() { Name = "R(固定端)", Value = R },
                new() { Name = "M(固定端)", Value = Mf }
            }
        };

        for (int i = 0; i <= n; i++)
        {
            double x = L * i / n;
            double M, V;
            if (x <= c)
            {
                M = -q * d * (x + d / 2.0 - x); // 不对，重新算
                // 从固定端算，荷载在[c, e]段
                // 对于x < c: M(x) = R*(x) + Mf - q*d*(... 用积分法
                // 直接用截面法: x处左侧合力
                M = R * 0 + Mf; // 不对
                // 用截面法（右侧）: x右侧的荷载
                // 当x < c: 右侧包含全部荷载 → M = -q*d*(c + d/2 - x)... 不对
                // 用力矩平衡: M(x) = -q*d*((c+d/2) - x) = -q*d*(c+d/2-x)
                // 当x=0: M = -q*d*(c+d/2) = Mf ✓
                M = -q * d * (c + d / 2.0 - x);
                V = q * d;
            }
            else if (x <= e)
            {
                // x在荷载范围内，右侧荷载为q*(e-x)
                M = -q * (e - x) * (e - x) / 2.0;
                V = q * (e - x);
            }
            else
            {
                M = 0;
                V = 0;
            }
            result.MomentDiagram.Add(new() { X = x, Value = M });
            result.ShearDiagram.Add(new() { X = x, Value = V });
            result.DeflectionDiagram.Add(new() { X = x, Value = 0 });
        }

        // 挠度用数值积分
        ComputeDeflectionCantilever(result, L, EI, n);
        result.MinMoment = Mf;
        result.MinMomentPosition = 0;
        result.MaxShear = q * d;
        SimpleBeamCalc.FindMaxDeflection(result);
        return result;
    }

    /// <summary>
    /// 工况6: 梯形荷载 — 叠加法
    /// </summary>
    public static BeamCalcResult TrapezoidalLoad(BeamCalcRequest req)
    {
        double q1 = req.Q1, q2 = req.Q2, L = req.L, EI = req.E * req.I * 1e-6;
        int n = req.NumPoints;

        // 分解为均布(min)+三角形
        double R = (q1 + q2) * L / 2.0;
        double Mf = -(q1 * L * L / 2.0 + (q2 - q1) * L * L / 6.0);
        var result = new BeamCalcResult
        {
            Reference = "《建筑结构静力计算手册》表3-6",
            Description = $"悬臂梁-梯形荷载(q1={q1},q2={q2})",
            Reactions = new() {
                new() { Name = "R(固定端)", Value = R },
                new() { Name = "M(固定端)", Value = Mf }
            }
        };

        for (int i = 0; i <= n; i++)
        {
            double x = L * i / n;
            // q(x) = q1 + (q2-q1)*(L-x)/L (固定端q1, 自由端q2)
            // 改为: 固定端q1, 自由端q2 → q(x) = q1 + (q2-q1)*x/L
            // M(x) = -∫x→L q(t)(t-x) dt
            // 数值计算M
            double qx_func(double t) => q1 + (q2 - q1) * t / L;
            // M(x) = -∫x→L q(t)*(t-x) dt
            int ns = 50;
            double dt2 = (L - x) / ns;
            double M = 0;
            for (int j = 0; j < ns; j++)
            {
                double t0 = x + j * dt2, t1 = x + (j + 1) * dt2;
                double tm = (t0 + t1) / 2.0;
                M -= qx_func(tm) * (tm - x) * dt2;
            }
            double V = 0;
            // V(x) = ∫x→L q(t) dt = q1*(L-x) + (q2-q1)*(L²-x²)/(2L)
            V = q1 * (L - x) + (q2 - q1) * (L * L - x * x) / (2 * L);

            result.MomentDiagram.Add(new() { X = x, Value = M });
            result.ShearDiagram.Add(new() { X = x, Value = V });
            result.DeflectionDiagram.Add(new() { X = x, Value = 0 });
        }

        ComputeDeflectionCantilever(result, L, EI, n);
        result.MinMoment = Mf;
        result.MinMomentPosition = 0;
        result.MaxShear = R;
        SimpleBeamCalc.FindMaxDeflection(result);
        return result;
    }

    /// <summary>悬臂梁挠度数值积分：从固定端(x=0)开始，δ(0)=0, θ(0)=0</summary>
    internal static void ComputeDeflectionCantilever(BeamCalcResult result, double L, double EI, int n)
    {
        if (EI <= 0) return;
        double dx = L / n;
        var theta = new double[n + 1];
        var defl = new double[n + 1];
        theta[0] = 0; defl[0] = 0;

        for (int i = 1; i <= n; i++)
        {
            double m0 = result.MomentDiagram[i - 1].Value;
            double m1 = result.MomentDiagram[i].Value;
            theta[i] = theta[i - 1] + (m0 + m1) / 2.0 * dx / EI;
            defl[i] = defl[i - 1] + (theta[i - 1] + theta[i]) / 2.0 * dx;
        }

        for (int i = 0; i <= n; i++)
        {
            double x = L * i / n;
            result.DeflectionDiagram[i] = new DiagramPoint { X = x, Value = defl[i] * 1000 };
        }
    }
}
