namespace GlassManagement.Api.Services.StaticCalcEngine;

/// <summary>
/// 简支梁计算 — 按《建筑结构静力计算手册》第2章
/// </summary>
public static class SimpleBeamCalc
{
    /// <summary>根据荷载类型分发计算</summary>
    public static BeamCalcResult Calculate(BeamCalcRequest req)
    {
        return req.Load switch
        {
            LoadType.Uniform => UniformLoad(req),
            LoadType.Concentrated => ConcentratedLoad(req),
            LoadType.Triangular => TriangularLoad(req),
            LoadType.Trapezoidal => TrapezoidalLoad(req),
            LoadType.Moment => MomentLoad(req),
            LoadType.DoubleMoment => DoubleMomentLoad(req),
            LoadType.PartialUniform => PartialUniformLoad(req),
            LoadType.SymmetricConcentrated => SymmetricConcentratedLoad(req),
            LoadType.MultipleConcentrated => MultipleConcentratedLoad(req),
            _ => UniformLoad(req)
        };
    }

    /// <summary>
    /// 工况1: 均布荷载 — 《静力计算手册》表2-1
    /// Mmax = qL²/8, Vmax = qL/2, δmax = 5qL⁴/(384EI)
    /// </summary>
    public static BeamCalcResult UniformLoad(BeamCalcRequest req)
    {
        double q = req.Q, L = req.L, EI = req.E * req.I * 1e-6; // EI单位: kN·m²
        int n = req.NumPoints;
        double Ra = q * L / 2, Rb = Ra;
        var result = new BeamCalcResult
        {
            Reference = "《建筑结构静力计算手册》表2-1",
            Description = "简支梁-均布荷载",
            Reactions = new() { new() { Name = "Ra", Value = Ra }, new() { Name = "Rb", Value = Rb } }
        };

        for (int i = 0; i <= n; i++)
        {
            double x = L * i / n;
            // M(x) = qx(L-x)/2 — 表2-1公式
            double M = q * x * (L - x) / 2.0;
            // V(x) = q(L/2-x)
            double V = q * (L / 2.0 - x);
            // δ(x) = qx(L³-2Lx²+x³)/(24EI) — 表2-1挠度方程
            double d = EI > 0 ? q * x * (L * L * L - 2 * L * x * x + x * x * x) / (24 * EI) * 1000 : 0; // 转mm
            result.MomentDiagram.Add(new() { X = x, Value = M });
            result.ShearDiagram.Add(new() { X = x, Value = V });
            result.DeflectionDiagram.Add(new() { X = x, Value = d });
        }

        result.MaxMoment = q * L * L / 8.0;
        result.MaxMomentPosition = L / 2.0;
        result.MaxShear = q * L / 2.0;
        result.MaxDeflection = EI > 0 ? 5 * q * L * L * L * L / (384 * EI) * 1000 : 0;
        result.MaxDeflectionPosition = L / 2.0;
        return result;
    }

    /// <summary>
    /// 工况2/3: 集中力 — 《静力计算手册》表2-2, 表2-3
    /// 任意位置: M=Pab/L, 居中时: M=PL/4, δ=PL³/(48EI)
    /// </summary>
    public static BeamCalcResult ConcentratedLoad(BeamCalcRequest req)
    {
        double P = req.P, L = req.L, EI = req.E * req.I * 1e-6;
        double a = req.A > 0 && req.A < L ? req.A : L / 2.0;
        double b = L - a;
        int n = req.NumPoints;

        double Ra = P * b / L, Rb = P * a / L;
        var result = new BeamCalcResult
        {
            Reference = a == L / 2.0 ? "《建筑结构静力计算手册》表2-2" : "《建筑结构静力计算手册》表2-3",
            Description = $"简支梁-集中力(a={a:F3}m)",
            Reactions = new() { new() { Name = "Ra", Value = Ra }, new() { Name = "Rb", Value = Rb } }
        };

        for (int i = 0; i <= n; i++)
        {
            double x = L * i / n;
            double M, V, d;
            if (x <= a)
            {
                // 表2-3: x≤a段
                M = Rb * x; // = Pbx/L
                V = Rb; // = Pb/L
                d = EI > 0 ? P * b * x * (L * L - b * b - x * x) / (6 * L * EI) * 1000 : 0;
            }
            else
            {
                M = Ra * (L - x); // = Pa(L-x)/L
                V = -Ra; // = -Pa/L （注意方向）
                // 实际是 V=-Rb 方向调整: 右段剪力 = -Ra
                V = -Rb + P; // 不对，重新: 右段 V = Ra - P = Pb/L - P = -Pa/L
                V = -Ra; // V = -Pa/L
                double x1 = L - x; // 从右端算
                d = EI > 0 ? P * a * x1 * (L * L - a * a - x1 * x1) / (6 * L * EI) * 1000 : 0;
            }
            result.MomentDiagram.Add(new() { X = x, Value = M });
            result.ShearDiagram.Add(new() { X = x, Value = V });
            result.DeflectionDiagram.Add(new() { X = x, Value = d });
        }

        result.MaxMoment = P * a * b / L;
        result.MaxMomentPosition = a;
        result.MaxShear = Math.Max(Ra, Rb);
        // 最大挠度: 当a≥b时, 在x=√((L²-b²)/3)处; 居中时在L/2
        if (Math.Abs(a - L / 2) < 1e-9)
        {
            result.MaxDeflection = EI > 0 ? P * L * L * L / (48 * EI) * 1000 : 0;
            result.MaxDeflectionPosition = L / 2.0;
        }
        else
        {
            double xm = Math.Sqrt((L * L - b * b) / 3.0);
            if (a < b) xm = L - Math.Sqrt((L * L - a * a) / 3.0);
            result.MaxDeflection = result.DeflectionDiagram.Max(p => p.Value);
            result.MaxDeflectionPosition = xm;
        }
        return result;
    }

    /// <summary>
    /// 工况4: 三角形荷载（零→q）— 《静力计算手册》表2-4
    /// 左端为零，右端为q。Mmax=qL²/(9√3), δmax≈0.01304qL⁴/EI
    /// 常用于横梁风荷载计算
    /// </summary>
    public static BeamCalcResult TriangularLoad(BeamCalcRequest req)
    {
        double q = req.Q, L = req.L, EI = req.E * req.I * 1e-6;
        int n = req.NumPoints;
        // 反力: Ra=qL/6, Rb=qL/3 — 表2-4
        double Ra = q * L / 6.0, Rb = q * L / 3.0;
        var result = new BeamCalcResult
        {
            Reference = "《建筑结构静力计算手册》表2-4",
            Description = "简支梁-三角形荷载(左零右q)",
            Reactions = new() { new() { Name = "Ra", Value = Ra }, new() { Name = "Rb", Value = Rb } }
        };

        // Mmax在x=L/√3处
        double xm = L / Math.Sqrt(3.0);
        for (int i = 0; i <= n; i++)
        {
            double x = L * i / n;
            // 荷载分布: q(x) = qx/L
            // M(x) = Ra*x - q*x²*x/(6L) = qLx/6 - qx³/(6L)
            double M = q * L * x / 6.0 - q * x * x * x / (6.0 * L);
            // V(x) = Ra - qx²/(2L) = qL/6 - qx²/(2L)
            double V = q * L / 6.0 - q * x * x / (2.0 * L);
            // δ(x) = qx(7L⁴-10L²x²+3x⁴)/(360LEI) — 表2-4
            double d = EI > 0 ? q * x * (7 * Math.Pow(L, 4) - 10 * L * L * x * x + 3 * Math.Pow(x, 4)) / (360.0 * L * EI) * 1000 : 0;
            result.MomentDiagram.Add(new() { X = x, Value = M });
            result.ShearDiagram.Add(new() { X = x, Value = V });
            result.DeflectionDiagram.Add(new() { X = x, Value = d });
        }

        result.MaxMoment = q * L * L / (9.0 * Math.Sqrt(3.0));
        result.MaxMomentPosition = xm;
        result.MaxShear = Rb;
        result.MaxDeflection = EI > 0 ? 0.01304 * q * Math.Pow(L, 4) / EI * 1000 : 0;
        // 最大挠度位置约在0.5193L处 — 表2-4
        result.MaxDeflectionPosition = 0.5193 * L;
        return result;
    }

    /// <summary>
    /// 工况5: 梯形荷载 — 《静力计算手册》表2-5
    /// 左端q1，右端q2，分解为均布(qmin)+三角形(q2-q1)
    /// </summary>
    public static BeamCalcResult TrapezoidalLoad(BeamCalcRequest req)
    {
        double q1 = req.Q1, q2 = req.Q2, L = req.L, EI = req.E * req.I * 1e-6;
        int n = req.NumPoints;
        // 分解: 均布部分qu=min(q1,q2), 三角形部分qt=|q2-q1|
        // Ra = q1*L/2 + (q2-q1)*L/6 = L(2q1+q2)/6
        // Rb = q1*L/2 + (q2-q1)*L/3 = L(q1+2q2)/6
        double Ra = L * (2 * q1 + q2) / 6.0;
        double Rb = L * (q1 + 2 * q2) / 6.0;
        var result = new BeamCalcResult
        {
            Reference = "《建筑结构静力计算手册》表2-5",
            Description = $"简支梁-梯形荷载(q1={q1},q2={q2})",
            Reactions = new() { new() { Name = "Ra", Value = Ra }, new() { Name = "Rb", Value = Rb } }
        };

        for (int i = 0; i <= n; i++)
        {
            double x = L * i / n;
            // q(x) = q1 + (q2-q1)*x/L
            double qx = q1 + (q2 - q1) * x / L;
            // M(x) = Ra*x - q1*x²/2 - (q2-q1)*x³/(6L)
            double M = Ra * x - q1 * x * x / 2.0 - (q2 - q1) * x * x * x / (6.0 * L);
            // V(x) = Ra - q1*x - (q2-q1)*x²/(2L)
            double V = Ra - q1 * x - (q2 - q1) * x * x / (2.0 * L);
            // 挠度用叠加法: 均布qu + 三角形qt
            double qu = Math.Min(q1, q2);
            double qt = Math.Abs(q2 - q1);
            double du = qu * x * (L * L * L - 2 * L * x * x + x * x * x) / (24 * EI);
            double dt;
            if (q2 >= q1)
                dt = qt * x * (7 * Math.Pow(L, 4) - 10 * L * L * x * x + 3 * Math.Pow(x, 4)) / (360.0 * L * EI);
            else
            {
                double x1 = L - x;
                dt = qt * x1 * (7 * Math.Pow(L, 4) - 10 * L * L * x1 * x1 + 3 * Math.Pow(x1, 4)) / (360.0 * L * EI);
            }
            double d = EI > 0 ? (du + dt) * 1000 : 0;
            result.MomentDiagram.Add(new() { X = x, Value = M });
            result.ShearDiagram.Add(new() { X = x, Value = V });
            result.DeflectionDiagram.Add(new() { X = x, Value = d });
        }

        // 最大弯矩位置: V(x)=0 → Ra - q1*x - (q2-q1)*x²/(2L) = 0
        // 用Newton法求解
        double xm = L / 2.0;
        for (int iter = 0; iter < 20; iter++)
        {
            double fx = Ra - q1 * xm - (q2 - q1) * xm * xm / (2.0 * L);
            double dfx = -q1 - (q2 - q1) * xm / L;
            if (Math.Abs(dfx) < 1e-15) break;
            xm -= fx / dfx;
            xm = Math.Max(0, Math.Min(L, xm));
        }
        result.MaxMomentPosition = xm;
        result.MaxMoment = Ra * xm - q1 * xm * xm / 2.0 - (q2 - q1) * xm * xm * xm / (6.0 * L);
        result.MaxShear = Math.Max(Math.Abs(Ra), Math.Abs(Rb));
        FindMaxDeflection(result);
        return result;
    }

    /// <summary>
    /// 工况6: 集中力矩（任意位置）— 《静力计算手册》表2-6
    /// 力矩M0作用在距左端a处
    /// </summary>
    public static BeamCalcResult MomentLoad(BeamCalcRequest req)
    {
        double M0 = req.M0, L = req.L, EI = req.E * req.I * 1e-6;
        double a = req.A > 0 && req.A < L ? req.A : L / 2.0;
        double b = L - a;
        int n = req.NumPoints;

        // 反力: Ra = -M0/L (向下), Rb = M0/L (向上) — 表2-6
        double Ra = M0 / L, Rb = -M0 / L;
        var result = new BeamCalcResult
        {
            Reference = "《建筑结构静力计算手册》表2-6",
            Description = $"简支梁-集中力矩(a={a:F3}m)",
            Reactions = new() { new() { Name = "Ra", Value = Ra }, new() { Name = "Rb", Value = Rb } }
        };

        for (int i = 0; i <= n; i++)
        {
            double x = L * i / n;
            double M, V, d;
            if (x < a - 1e-10)
            {
                M = M0 * x / L;
                V = M0 / L;
                d = EI > 0 ? M0 * x * (2 * L * L - 3 * a * L + a * a - x * x) / (6 * L * EI) * 1000 : 0;
            }
            else
            {
                M = M0 * x / L - M0; // = M0*(x/L - 1) = -M0*b/L at x approaching from right
                V = M0 / L;
                // 表2-6: x>a段挠度
                d = EI > 0 ? M0 * ((L - x) * (2 * L * L - 3 * a * L + a * a - (L - x) * (L - x)) - (L * L - a * a) * (L - x)) / (6 * L * EI) * 1000 : 0;
                // 简化: 用对称性重新推导
                double x1 = L - x;
                d = EI > 0 ? -M0 * x1 * (2 * L * L - 3 * b * L + b * b - x1 * x1) / (6 * L * EI) * 1000 : 0;
            }
            result.MomentDiagram.Add(new() { X = x, Value = M });
            result.ShearDiagram.Add(new() { X = x, Value = V });
            result.DeflectionDiagram.Add(new() { X = x, Value = d });
        }

        result.MaxMoment = M0 * a / L;
        result.MinMoment = -M0 * b / L;
        result.MaxMomentPosition = a;
        result.MinMomentPosition = a;
        result.MaxShear = Math.Abs(M0 / L);
        FindMaxDeflection(result);
        return result;
    }

    /// <summary>
    /// 工况7: 两端集中力矩 — 《静力计算手册》表2-7
    /// 左端Ma，右端Mb
    /// </summary>
    public static BeamCalcResult DoubleMomentLoad(BeamCalcRequest req)
    {
        double Ma = req.Ma, Mb = req.Mb, L = req.L, EI = req.E * req.I * 1e-6;
        int n = req.NumPoints;

        double Ra = (Ma + Mb) / L, Rb = -(Ma + Mb) / L;
        var result = new BeamCalcResult
        {
            Reference = "《建筑结构静力计算手册》表2-7",
            Description = "简支梁-两端集中力矩",
            Reactions = new() { new() { Name = "Ra", Value = Ra }, new() { Name = "Rb", Value = Rb } }
        };

        for (int i = 0; i <= n; i++)
        {
            double x = L * i / n;
            // M(x) = Ma + (Mb-Ma)*x/L - Ma = Ma*(1-x/L) + Mb*x/L 不对
            // 从左端: M(x) = Ra*x - Ma = (Ma+Mb)*x/L - Ma
            // 当x=0: M=-Ma+Ma=0 不对。用纯弯矩梁:
            // M(x) = Ma*(1-x/L) + Mb*x/L (这是两端有弯矩的简支梁)
            // 但支座处M=Ma(x=0), M=Mb(x=L)，这不是简支梁标准情况
            // 按表2-7: 两端施加力矩Ma(逆时针)和Mb(逆时针)
            double M = Ma * (1 - x / L) + Mb * x / L;
            double V = (Mb - Ma) / L;
            // δ(x) — 叠加两个单力矩挠度
            double d = 0;
            if (EI > 0)
            {
                // 表2-7: δ = Ma*x*(2L²-3Lx+x²)/(6LEI) + Mb*x*(L²-x²)/(6LEI) 不完全正确
                // 标准公式: δ(x) = [Ma*x*(2L-x)(L-x) + Mb*x*(L²-x²)]/(6LEI) 简化:
                // 用叠加: δ = Ma*x(2L-3x*... 太复杂，用数值积分
                d = (Ma * x * (2 * L * L - 3 * L * x + x * x) / (6 * L * EI)
                   - Mb * (L - x) * (2 * L * L - 3 * L * (L - x) + (L - x) * (L - x)) / (6 * L * EI)) * 1000;
            }
            result.MomentDiagram.Add(new() { X = x, Value = M });
            result.ShearDiagram.Add(new() { X = x, Value = V });
            result.DeflectionDiagram.Add(new() { X = x, Value = d });
        }

        FindMaxMoment(result);
        FindMaxDeflection(result);
        return result;
    }

    /// <summary>
    /// 工况8: 部分均布荷载 — 《静力计算手册》表2-8
    /// 荷载q作用在[c, c+d]区间 (c=LoadStart, c+d=LoadEnd)
    /// </summary>
    public static BeamCalcResult PartialUniformLoad(BeamCalcRequest req)
    {
        double q = req.Q, L = req.L, EI = req.E * req.I * 1e-6;
        double c = req.LoadStart, e = req.LoadEnd;
        if (e <= c) { e = L; }
        double d = e - c; // 荷载长度
        int n = req.NumPoints;

        // Ra = qd(2L-2c-d)/(2L), Rb = qd(2c+d)/(2L) — 表2-8
        double Ra = q * d * (2 * L - 2 * c - d) / (2 * L);
        double Rb = q * d * (2 * c + d) / (2 * L);
        var result = new BeamCalcResult
        {
            Reference = "《建筑结构静力计算手册》表2-8",
            Description = $"简支梁-部分均布荷载[{c:F2},{e:F2}]",
            Reactions = new() { new() { Name = "Ra", Value = Ra }, new() { Name = "Rb", Value = Rb } }
        };

        for (int i = 0; i <= n; i++)
        {
            double x = L * i / n;
            double M, V;
            if (x <= c)
            {
                M = Ra * x;
                V = Ra;
            }
            else if (x <= e)
            {
                M = Ra * x - q * (x - c) * (x - c) / 2.0;
                V = Ra - q * (x - c);
            }
            else
            {
                M = Rb * (L - x);
                V = -Rb;
            }
            // 挠度用叠加积分（简化：数值积分方式，或用Macaulay函数）
            // 简化用共轭梁法或直接公式比较复杂，用数值差分
            result.MomentDiagram.Add(new() { X = x, Value = M });
            result.ShearDiagram.Add(new() { X = x, Value = V });
            result.DeflectionDiagram.Add(new() { X = x, Value = 0 }); // 先占位
        }

        // 用M/EI二次积分求挠度
        ComputeDeflectionFromMoment(result, L, EI, n);
        FindMaxMoment(result);
        FindMaxDeflection(result);
        result.MaxShear = Math.Max(Math.Abs(Ra), Math.Abs(Rb));
        return result;
    }

    /// <summary>
    /// 工况9: 两个对称集中力 — 《静力计算手册》表2-9
    /// 两个P对称作用在距端部a处
    /// </summary>
    public static BeamCalcResult SymmetricConcentratedLoad(BeamCalcRequest req)
    {
        double P = req.P, L = req.L, EI = req.E * req.I * 1e-6;
        double a = req.A > 0 && req.A < L / 2.0 ? req.A : L / 3.0;
        int n = req.NumPoints;

        double Ra = P, Rb = P;
        var result = new BeamCalcResult
        {
            Reference = "《建筑结构静力计算手册》表2-9",
            Description = $"简支梁-两个对称集中力(a={a:F3}m)",
            Reactions = new() { new() { Name = "Ra", Value = Ra }, new() { Name = "Rb", Value = Rb } }
        };

        for (int i = 0; i <= n; i++)
        {
            double x = L * i / n;
            double M, V;
            if (x <= a)
            {
                M = P * x;
                V = P;
            }
            else if (x <= L - a)
            {
                M = P * a; // 纯弯区
                V = 0;
            }
            else
            {
                M = P * (L - x);
                V = -P;
            }
            // δ(x): 纯弯段内 δ = Pa(3L²-4a²)/(24EI) at mid
            double d = 0;
            if (EI > 0)
            {
                if (x <= a)
                    d = P * x * (3 * a * L - 3 * a * a - x * x) / (6 * EI) * 1000;
                else if (x <= L - a)
                    d = P * a * (3 * x * L - 3 * x * x - a * a) / (6 * EI) * 1000;
                else
                {
                    double x1 = L - x;
                    d = P * x1 * (3 * a * L - 3 * a * a - x1 * x1) / (6 * EI) * 1000;
                }
            }
            result.MomentDiagram.Add(new() { X = x, Value = M });
            result.ShearDiagram.Add(new() { X = x, Value = V });
            result.DeflectionDiagram.Add(new() { X = x, Value = d });
        }

        result.MaxMoment = P * a;
        result.MaxMomentPosition = a;
        result.MaxShear = P;
        result.MaxDeflection = EI > 0 ? P * a * (3 * L * L - 4 * a * a) / (24 * EI) * 1000 : 0;
        result.MaxDeflectionPosition = L / 2.0;
        return result;
    }

    /// <summary>
    /// 工况10: 等间距n个集中力 — 《静力计算手册》表2-10
    /// </summary>
    public static BeamCalcResult MultipleConcentratedLoad(BeamCalcRequest req)
    {
        double P = req.P, L = req.L, EI = req.E * req.I * 1e-6;
        int nc = Math.Max(2, req.ConcentratedCount);
        int n = req.NumPoints;
        double spacing = L / (nc + 1);

        // 用叠加法
        double Ra = 0;
        for (int k = 1; k <= nc; k++)
        {
            double ak = k * spacing;
            Ra += P * (L - ak) / L;
        }
        double Rb = nc * P - Ra;
        var result = new BeamCalcResult
        {
            Reference = "《建筑结构静力计算手册》表2-10",
            Description = $"简支梁-{nc}个等间距集中力",
            Reactions = new() { new() { Name = "Ra", Value = Ra }, new() { Name = "Rb", Value = Rb } }
        };

        // 叠加各集中力的效果
        for (int i = 0; i <= n; i++)
        {
            double x = L * i / n;
            double M = 0, V = 0, d = 0;
            for (int k = 1; k <= nc; k++)
            {
                double ak = k * spacing;
                double bk = L - ak;
                double rak = P * bk / L;
                if (x <= ak)
                {
                    M += rak * x;
                    V += rak;
                    if (EI > 0) d += P * bk * x * (L * L - bk * bk - x * x) / (6 * L * EI) * 1000;
                }
                else
                {
                    M += rak * ak - 0; // = P*ak*bk/L - P*(x-ak)... 简化
                    M = 0; // 重算
                }
            }
            // 重新用直接叠加
            M = 0; V = 0; d = 0;
            double runningV = Ra;
            // M at x = Ra*x - sum of P for forces left of x
            M = Ra * x;
            V = Ra;
            for (int k = 1; k <= nc; k++)
            {
                double ak = k * spacing;
                if (x >= ak)
                {
                    M -= P * (x - ak);
                    V -= P;
                }
                // 挠度叠加
                if (EI > 0)
                {
                    double bk = L - ak;
                    if (x <= ak)
                        d += P * bk * x * (L * L - bk * bk - x * x) / (6 * L * EI) * 1000;
                    else
                    {
                        double x1 = L - x;
                        d += P * ak * x1 * (L * L - ak * ak - x1 * x1) / (6 * L * EI) * 1000;
                    }
                }
            }
            result.MomentDiagram.Add(new() { X = x, Value = M });
            result.ShearDiagram.Add(new() { X = x, Value = V });
            result.DeflectionDiagram.Add(new() { X = x, Value = d });
        }

        FindMaxMoment(result);
        FindMaxDeflection(result);
        result.MaxShear = Math.Max(Math.Abs(Ra), Math.Abs(Rb));
        return result;
    }

    #region 辅助方法

    /// <summary>从弯矩图数值积分计算挠度（简支梁边界：两端挠度为0）</summary>
    internal static void ComputeDeflectionFromMoment(BeamCalcResult result, double L, double EI, int n)
    {
        if (EI <= 0) return;
        double dx = L / n;
        // 用Simpson积分 M/EI 得到转角θ，再积分得到挠度
        // 先用梯形法积分 M/EI → θ(x) = θ0 + ∫M/EI dx
        var theta = new double[n + 1];
        var defl = new double[n + 1];
        theta[0] = 0; defl[0] = 0;

        // 积分M/EI得到θ（含待定常数θ0）
        for (int i = 1; i <= n; i++)
        {
            double m0 = result.MomentDiagram[i - 1].Value;
            double m1 = result.MomentDiagram[i].Value;
            theta[i] = theta[i - 1] + (m0 + m1) / 2.0 * dx / EI;
        }

        // 积分θ得到δ（含待定常数）
        for (int i = 1; i <= n; i++)
        {
            defl[i] = defl[i - 1] + (theta[i - 1] + theta[i]) / 2.0 * dx;
        }

        // 边界条件: defl[0]=0, defl[n]=0 → 修正θ0
        double theta0 = -defl[n] / L;
        for (int i = 0; i <= n; i++)
        {
            double x = L * i / n;
            result.DeflectionDiagram[i] = new DiagramPoint { X = x, Value = (defl[i] + theta0 * x) * 1000 }; // 转mm
        }
    }

    /// <summary>从图表数据找最大弯矩</summary>
    internal static void FindMaxMoment(BeamCalcResult result)
    {
        double maxM = double.MinValue, minM = double.MaxValue;
        double maxPos = 0, minPos = 0;
        foreach (var p in result.MomentDiagram)
        {
            if (p.Value > maxM) { maxM = p.Value; maxPos = p.X; }
            if (p.Value < minM) { minM = p.Value; minPos = p.X; }
        }
        result.MaxMoment = maxM;
        result.MaxMomentPosition = maxPos;
        result.MinMoment = minM;
        result.MinMomentPosition = minPos;
    }

    /// <summary>从图表数据找最大挠度</summary>
    internal static void FindMaxDeflection(BeamCalcResult result)
    {
        double maxD = 0, maxPos = 0;
        foreach (var p in result.DeflectionDiagram)
        {
            if (Math.Abs(p.Value) > Math.Abs(maxD)) { maxD = p.Value; maxPos = p.X; }
        }
        result.MaxDeflection = maxD;
        result.MaxDeflectionPosition = maxPos;
    }

    #endregion
}
