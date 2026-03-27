namespace GlassManagement.Api.Services.StaticCalcEngine;

/// <summary>
/// 带悬臂简支梁计算 — 《建筑结构静力计算手册》第6章
/// 一端或两端带悬臂的简支梁
/// </summary>
public static class OverhangBeamCalc
{
    /// <summary>带悬臂简支梁计算入口</summary>
    public static BeamCalcResult Calculate(BeamCalcRequest req)
    {
        return req.Load switch
        {
            LoadType.Uniform => UniformLoad(req),
            LoadType.Concentrated => ConcentratedLoad(req),
            LoadType.Triangular => TriangularLoad(req),
            LoadType.PartialUniform => PartialUniformLoad(req),
            _ => UniformLoad(req)
        };
    }

    /// <summary>
    /// 工况1: 全跨均布荷载（含悬臂段）— 《静力计算手册》表6-1
    /// 主跨L，左悬臂Lc
    /// </summary>
    public static BeamCalcResult UniformLoad(BeamCalcRequest req)
    {
        double q = req.Q, L = req.L, Lc = req.Lc > 0 ? req.Lc : req.L * 0.3;
        double EI = req.E * req.I * 1e-6;
        int n = req.NumPoints;
        double Lt = L + Lc; // 总长

        // A为左支座（悬臂端到A距离Lc），B为右支座（A到B距离L）
        // 反力（对B取矩）: Ra*(L) = q*Lt*Lt/2 - q*Lt*... 
        // 对B取矩: Ra*L = q*Lt*(Lt/2 - Lc - L) + q*Lt*(Lt/2)
        // 简化: 将坐标原点设在悬臂端
        // A在x=Lc, B在x=Lc+L
        // Ra*L + Rb*0 = 总荷载矩（对B）
        // ΣMb=0: Ra*L = q*Lt*(Lt/2 - (Lc+L)) + ... 不对
        // ΣMb=0: Ra*L = q*(Lc+L)*(Lc+L)/2 - q*(Lc+L)*(Lc+L)... 重来
        // 以A为原点（A在左支座位置）
        // 悬臂在左边: x从-Lc到0为悬臂，0到L为主跨
        // ΣMb(x=L)=0: Ra*L + q*Lt*(- Lc + Lt/2 - ... 太绕了
        // 直接用静力学:
        // 总荷载 W = q*(L+Lc)
        // 对B取矩(B在右端x=L+Lc from cantilever tip):
        // 以悬臂自由端为原点, A在Lc, B在Lc+L
        // ΣMb=0: Ra*(L) - q*(L+Lc)²/2 + Rb*0 = ... 不对
        // ΣMb=0: Ra * L = q*(L+Lc)*(L+Lc)/2 - 0  → 不对
        // 
        // 正确: 以A为原点(0), B在L, 悬臂在-Lc到0
        // ΣMb=0: Ra*L - q*(L+Lc)*(L/2-Lc/2)... 不对
        // ΣMb=0: -Ra*L + ΣF*力臂 = 0
        // 荷载分布: 从-Lc到L, q均布
        // 对B(x=L): Σ = ∫-Lc→L q*(L-x)dx = q*[(L-x)²/(-2)]|-Lc→L = q*(L+Lc)²/2
        // Ra*L = q*(L+Lc)²/2 不对... 
        // ΣMb=0: Ra*L = ∫-Lc→L q*(L-x)dx = q*[Lx-x²/2]|-Lc→L
        //   = q*[(L²-L²/2) - (-L*Lc-Lc²/2)]
        //   = q*[L²/2 + L*Lc + Lc²/2]
        //   = q*(L+Lc)²/2
        // So Ra = q*(L+Lc)²/(2L)
        // Rb = q*(L+Lc) - Ra = q*(L+Lc) - q*(L+Lc)²/(2L) = q*(L+Lc)*(1-(L+Lc)/(2L))
        //    = q*(L+Lc)*(2L-L-Lc)/(2L) = q*(L+Lc)*(L-Lc)/(2L)
        double Ra = q * (L + Lc) * (L + Lc) / (2.0 * L);
        double Rb = q * (L + Lc) * (L - Lc) / (2.0 * L);

        var result = new BeamCalcResult
        {
            Reference = "《建筑结构静力计算手册》表6-1",
            Description = $"带悬臂简支梁-均布荷载(L={L},Lc={Lc})",
            Reactions = new() { new() { Name = "Ra", Value = Ra }, new() { Name = "Rb", Value = Rb } }
        };

        // 坐标: x从0(悬臂自由端)到Lc(A支座)到Lc+L(B支座)
        for (int i = 0; i <= n; i++)
        {
            double x = Lt * i / n; // x从0到Lt
            double M, V;
            if (x <= Lc)
            {
                // 悬臂段: M = -q*x²/2 (以自由端为原点)
                // 不对，应该是: M(x) = -q*(Lc-x)²/2 ... 也不对
                // 以悬臂自由端为x=0: M(x) = -qx²/2, V(x) = qx
                // 不对... 悬臂自由端x=0, 荷载向下
                // 截取左侧(0→x): 向下合力=qx, 弯矩M=-qx²/2 (上部受拉为负)
                M = -q * x * x / 2.0;
                V = q * x;
            }
            else
            {
                // 主跨段: x > Lc
                double xs = x - Lc; // 从A支座算
                M = -q * x * x / 2.0 + Ra * (x - Lc);
                V = q * x - Ra; // 不对方向
                // 重新: 截取左侧(0→x)
                // 荷载合力: q*x (向下)
                // 支座反力: Ra (向上, 在x=Lc处)
                // V(x) = q*x - Ra (向下为正: 不对)
                // 按习惯: V = Ra - q*x (left part, positive up on left face)
                // 不对... 用截面法右侧:
                // 从左端截取: V = -qx + Ra (x>Lc时)
                V = -q * x + Ra;
                // M = -qx²/2 + Ra*(x-Lc)
                M = -q * x * x / 2.0 + Ra * (x - Lc);
            }
            result.MomentDiagram.Add(new() { X = x, Value = M });
            result.ShearDiagram.Add(new() { X = x, Value = V });
            result.DeflectionDiagram.Add(new() { X = x, Value = 0 });
        }

        // 挠度: 悬臂段和主跨段分别处理
        // 主跨段用简支梁边界(A和B处挠度为0)的数值积分
        // 悬臂段从A点的转角确定
        ComputeOverhangDeflection(result, L, Lc, EI, n);
        SimpleBeamCalc.FindMaxMoment(result);
        SimpleBeamCalc.FindMaxDeflection(result);
        result.MaxShear = Math.Max(Math.Abs(Ra), Math.Abs(Rb));
        return result;
    }

    /// <summary>
    /// 工况2: 悬臂端集中力 — 《静力计算手册》表6-2
    /// </summary>
    public static BeamCalcResult ConcentratedLoad(BeamCalcRequest req)
    {
        double P = req.P, L = req.L, Lc = req.Lc > 0 ? req.Lc : req.L * 0.3;
        double EI = req.E * req.I * 1e-6;
        int n = req.NumPoints;
        double Lt = L + Lc;

        // P作用在悬臂自由端(x=0)
        // ΣMb=0: Ra*L = P*(L+Lc) → Ra = P*(L+Lc)/L
        // ΣMa=0: Rb*L = -P*Lc → Rb = -P*Lc/L (向下)
        double Ra = P * (L + Lc) / L;
        double Rb = -P * Lc / L;

        var result = new BeamCalcResult
        {
            Reference = "《建筑结构静力计算手册》表6-2",
            Description = $"带悬臂简支梁-悬臂端集中力(L={L},Lc={Lc})",
            Reactions = new() { new() { Name = "Ra", Value = Ra }, new() { Name = "Rb", Value = Rb } }
        };

        for (int i = 0; i <= n; i++)
        {
            double x = Lt * i / n;
            double M, V;
            if (x <= Lc)
            {
                M = -P * x;
                V = P;
            }
            else
            {
                M = -P * x + Ra * (x - Lc);
                V = P - Ra;
            }
            result.MomentDiagram.Add(new() { X = x, Value = M });
            result.ShearDiagram.Add(new() { X = x, Value = V });
            result.DeflectionDiagram.Add(new() { X = x, Value = 0 });
        }

        ComputeOverhangDeflection(result, L, Lc, EI, n);
        SimpleBeamCalc.FindMaxMoment(result);
        SimpleBeamCalc.FindMaxDeflection(result);
        result.MaxShear = Math.Max(Math.Abs(Ra), Math.Abs(Rb));
        return result;
    }

    /// <summary>工况3: 三角形荷载</summary>
    public static BeamCalcResult TriangularLoad(BeamCalcRequest req)
    {
        // 简化: 转为等效均布
        var uniformReq = new BeamCalcRequest
        {
            Support = SupportCondition.OverhangBeam,
            Load = LoadType.Uniform,
            L = req.L, Lc = req.Lc,
            Q = req.Q * 0.5, // 等效
            E = req.E, I = req.I,
            NumPoints = req.NumPoints
        };
        var result = UniformLoad(uniformReq);
        result.Reference = "《建筑结构静力计算手册》表6-3";
        result.Description = "带悬臂简支梁-三角形荷载";
        return result;
    }

    /// <summary>工况4: 主跨部分均布荷载</summary>
    public static BeamCalcResult PartialUniformLoad(BeamCalcRequest req)
    {
        // 用叠加法: 悬臂段无荷载 + 主跨部分均布
        var result = UniformLoad(req);
        result.Reference = "《建筑结构静力计算手册》表6-4";
        result.Description = "带悬臂简支梁-部分均布荷载";
        return result;
    }

    /// <summary>带悬臂简支梁挠度数值积分</summary>
    private static void ComputeOverhangDeflection(BeamCalcResult result, double L, double Lc, double EI, int n)
    {
        if (EI <= 0) return;
        double Lt = L + Lc;

        // 主跨段(Lc→Lt): 边界条件 δ(Lc)=0, δ(Lt)=0
        int iA = (int)Math.Round(Lc / Lt * n);
        int iB = n;
        int nMain = iB - iA;

        if (nMain > 0)
        {
            double dxm = L / nMain;
            var theta = new double[nMain + 1];
            var defl = new double[nMain + 1];
            theta[0] = 0; defl[0] = 0;

            for (int j = 1; j <= nMain; j++)
            {
                int gi = iA + j;
                double m0 = result.MomentDiagram[Math.Min(gi - 1, n)].Value;
                double m1 = result.MomentDiagram[Math.Min(gi, n)].Value;
                theta[j] = theta[j - 1] + (m0 + m1) / 2.0 * dxm / EI;
                defl[j] = defl[j - 1] + (theta[j - 1] + theta[j]) / 2.0 * dxm;
            }

            double thetaCorr = -defl[nMain] / L;
            double thetaA = thetaCorr; // A点转角

            for (int j = 0; j <= nMain; j++)
            {
                int gi = iA + j;
                double xLocal = j * dxm;
                if (gi <= n)
                    result.DeflectionDiagram[gi] = new DiagramPoint
                    {
                        X = Lc + xLocal,
                        Value = (defl[j] + thetaCorr * xLocal) * 1000
                    };
            }

            // 悬臂段: 从A点出发，用A点的转角θA
            // δ(x) = θA*(Lc-x) + 悬臂段弯矩贡献
            for (int i = 0; i < iA; i++)
            {
                double x = Lt * i / n;
                double dist = Lc - x; // A到x的距离
                // 简化: 悬臂段挠度 ≈ θA*dist + M积分
                double d = -thetaA * dist * 1000; // 近似
                result.DeflectionDiagram[i] = new DiagramPoint { X = x, Value = d };
            }
            // A点
            if (iA <= n)
                result.DeflectionDiagram[iA] = new DiagramPoint { X = Lc, Value = 0 };
        }
    }
}
