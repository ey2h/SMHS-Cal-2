namespace GlassManagement.Calc.Engines.StaticCalc;

/// <summary>
/// 三跨连续梁计算 — 按《建筑结构静力计算手册》第4章
/// 使用三弯矩方程法求解
/// 支承条件：四支座连续梁（A-B-C-D），跨度L1、L2、L3
/// </summary>
public static class ContinuousBeamCalc
{
    /// <summary>三跨连续梁计算</summary>
    public static BeamCalcResult Calculate(BeamCalcRequest req)
    {
        return req.Load switch
        {
            LoadType.Uniform => UniformLoad(req),
            LoadType.Concentrated => ConcentratedLoad(req),
            _ => UniformLoad(req)
        };
    }

    /// <summary>
    /// 三跨连续梁-均布荷载 — 《静力计算手册》第4章
    /// 用三弯矩方程法:
    /// M0*L1 + 2*M1*(L1+L2) + M2*L2 = -q/4*(L1³+L2³)
    /// M1*L2 + 2*M2*(L2+L3) + M3*L3 = -q/4*(L2³+L3³)
    /// 边界: M0=0, M3=0
    /// </summary>
    public static BeamCalcResult UniformLoad(BeamCalcRequest req)
    {
        double q = req.Q;
        double L1 = req.L, L2 = req.L2 > 0 ? req.L2 : req.L, L3 = req.L3 > 0 ? req.L3 : req.L;
        double EI = req.E * req.I * 1e-6;
        int n = req.NumPoints;
        double L = L1 + L2 + L3;

        // 三弯矩方程（两个方程两个未知数M1、M2）
        // 2*M1*(L1+L2) + M2*L2 = -q/4*(L1³+L2³)    ...(1)
        // M1*L2 + 2*M2*(L2+L3) = -q/4*(L2³+L3³)    ...(2)
        double a11 = 2 * (L1 + L2);
        double a12 = L2;
        double b1 = -q / 4.0 * (L1 * L1 * L1 + L2 * L2 * L2);
        double a21 = L2;
        double a22 = 2 * (L2 + L3);
        double b2 = -q / 4.0 * (L2 * L2 * L2 + L3 * L3 * L3);

        // 克莱姆法则求解
        double det = a11 * a22 - a12 * a21;
        double M1 = (b1 * a22 - b2 * a12) / det;
        double M2 = (a11 * b2 - a21 * b1) / det;

        // 各支座反力
        double Ra = q * L1 / 2.0 - M1 / L1;
        double Rd = q * L3 / 2.0 - M2 / L3;
        double Rb = q * L1 / 2.0 + M1 / L1 + q * L2 / 2.0 - (M2 - M1) / L2;
        double Rc = q * L2 / 2.0 + (M2 - M1) / L2 + q * L3 / 2.0 + M2 / L3;

        var result = new BeamCalcResult
        {
            Reference = "《建筑结构静力计算手册》第4章，三弯矩方程法",
            Description = $"三跨连续梁-均布荷载(L1={L1},L2={L2},L3={L3})",
            Reactions = new()
            {
                new() { Name = "Ra", Value = Ra },
                new() { Name = "Rb", Value = Rb },
                new() { Name = "Rc", Value = Rc },
                new() { Name = "Rd", Value = Rd },
                new() { Name = "M1(B支座弯矩)", Value = M1 },
                new() { Name = "M2(C支座弯矩)", Value = M2 }
            }
        };

        // 生成图表
        for (int i = 0; i <= n; i++)
        {
            double x = L * i / n;
            double M, V;

            if (x <= L1)
            {
                M = Ra * x - q * x * x / 2.0;
                V = Ra - q * x;
            }
            else if (x <= L1 + L2)
            {
                double x2 = x - L1;
                // 第二跨: 从B点算，B点左边反力和=Ra+Rb
                double RbLeft = Ra + Rb; // B点以左总反力
                M = Ra * x - q * x * x / 2.0 + Rb * (x - L1) - q * 0; // 不对
                // 直接用: M(x) = Ra*x + Rb*(x-L1) - q*x²/2
                M = Ra * x + Rb * (x - L1) - q * x * x / 2.0;
                V = Ra + Rb - q * x;
            }
            else
            {
                // 第三跨: 从D端算
                double x3 = L - x;
                M = Rd * x3 - q * x3 * x3 / 2.0;
                V = -(Rd - q * x3);
            }

            result.MomentDiagram.Add(new() { X = x, Value = M });
            result.ShearDiagram.Add(new() { X = x, Value = V });
            result.DeflectionDiagram.Add(new() { X = x, Value = 0 });
        }

        // 挠度数值积分（分三跨）
        ComputeThreeSpanDeflection(result, L1, L2, L3, EI, n);
        SimpleBeamCalc.FindMaxMoment(result);
        SimpleBeamCalc.FindMaxDeflection(result);
        result.MaxShear = new[] { Math.Abs(Ra), Math.Abs(Rb), Math.Abs(Rc), Math.Abs(Rd) }.Max();
        return result;
    }

    /// <summary>三跨连续梁-集中力（第一跨跨中）</summary>
    public static BeamCalcResult ConcentratedLoad(BeamCalcRequest req)
    {
        // 简化: 转为均布等效后调用，或用三弯矩方程
        double P = req.P;
        double L1 = req.L, L2 = req.L2 > 0 ? req.L2 : req.L, L3 = req.L3 > 0 ? req.L3 : req.L;
        double a = req.A > 0 ? req.A : L1 / 2.0;
        double b1 = L1 - a;
        double EI = req.E * req.I * 1e-6;
        int n = req.NumPoints;
        double L = L1 + L2 + L3;

        // 三弯矩方程（集中力P在第一跨a处，其他跨无荷载）
        // 2M1(L1+L2) + M2*L2 = -P*a*(L1²-a²)/L1
        // M1*L2 + 2M2(L2+L3) = 0
        double a11 = 2 * (L1 + L2);
        double a12 = L2;
        double bb1 = -P * a * (L1 * L1 - a * a) / L1;
        double a21 = L2;
        double a22 = 2 * (L2 + L3);
        double bb2 = 0;

        double det = a11 * a22 - a12 * a21;
        double M1 = (bb1 * a22 - bb2 * a12) / det;
        double M2 = (a11 * bb2 - a21 * bb1) / det;

        double Ra = P * b1 / L1 - M1 / L1;
        double Rd = -M2 / L3;
        double Rb = P * a / L1 + M1 / L1 - (M2 - M1) / L2;
        double Rc = (M2 - M1) / L2 + M2 / L3;

        var result = new BeamCalcResult
        {
            Reference = "《建筑结构静力计算手册》第4章",
            Description = $"三跨连续梁-集中力(a={a})",
            Reactions = new()
            {
                new() { Name = "Ra", Value = Ra },
                new() { Name = "Rb", Value = Rb },
                new() { Name = "Rc", Value = Rc },
                new() { Name = "Rd", Value = Rd },
                new() { Name = "M1", Value = M1 },
                new() { Name = "M2", Value = M2 }
            }
        };

        for (int i = 0; i <= n; i++)
        {
            double x = L * i / n;
            double M, V;
            if (x <= a)
            {
                M = Ra * x; V = Ra;
            }
            else if (x <= L1)
            {
                M = Ra * x - P * (x - a); V = Ra - P;
            }
            else if (x <= L1 + L2)
            {
                M = Ra * x + Rb * (x - L1) - P * (x - a);
                V = Ra + Rb - P;
            }
            else
            {
                double x3 = L - x;
                M = Rd * x3; V = -Rd;
            }
            result.MomentDiagram.Add(new() { X = x, Value = M });
            result.ShearDiagram.Add(new() { X = x, Value = V });
            result.DeflectionDiagram.Add(new() { X = x, Value = 0 });
        }

        ComputeThreeSpanDeflection(result, L1, L2, L3, EI, n);
        SimpleBeamCalc.FindMaxMoment(result);
        SimpleBeamCalc.FindMaxDeflection(result);
        result.MaxShear = new[] { Math.Abs(Ra), Math.Abs(Rb), Math.Abs(Rc), Math.Abs(Rd) }.Max();
        return result;
    }

    /// <summary>三跨挠度数值积分</summary>
    private static void ComputeThreeSpanDeflection(BeamCalcResult result, double L1, double L2, double L3, double EI, int n)
    {
        if (EI <= 0) return;
        double L = L1 + L2 + L3;

        // 分三段积分，每段边界挠度为0
        double[] spanLengths = { L1, L2, L3 };
        double cumLen = 0;

        foreach (double Lk in spanLengths)
        {
            int iStart = (int)Math.Round(cumLen / L * n);
            int iEnd = (int)Math.Round((cumLen + Lk) / L * n);
            int nk = iEnd - iStart;
            if (nk <= 0) { cumLen += Lk; continue; }

            double dxk = Lk / nk;
            var theta = new double[nk + 1];
            var defl = new double[nk + 1];
            theta[0] = 0; defl[0] = 0;

            for (int j = 1; j <= nk; j++)
            {
                int gi = iStart + j;
                double m0 = result.MomentDiagram[Math.Min(gi - 1, n)].Value;
                double m1 = result.MomentDiagram[Math.Min(gi, n)].Value;
                theta[j] = theta[j - 1] + (m0 + m1) / 2.0 * dxk / EI;
                defl[j] = defl[j - 1] + (theta[j - 1] + theta[j]) / 2.0 * dxk;
            }

            double thetaCorr = -defl[nk] / Lk;
            for (int j = 0; j <= nk; j++)
            {
                int gi = iStart + j;
                double xLocal = j * dxk;
                if (gi <= n)
                    result.DeflectionDiagram[gi] = new DiagramPoint
                    {
                        X = cumLen + xLocal,
                        Value = (defl[j] + thetaCorr * xLocal) * 1000
                    };
            }
            cumLen += Lk;
        }
    }
}
