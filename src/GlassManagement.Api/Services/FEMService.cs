namespace GlassManagement.Api.Services;

/// <summary>
/// 有限元分析服务 — 梁和板的FEM计算
/// 注意: BriefFiniteElement.NET暂未集成，使用自研简化有限元实现
/// </summary>
public class FEMService
{
    /// <summary>有限元梁分析</summary>
    public FEMBeamResult AnalyzeBeam(FEMBeamRequest request)
    {
        // 使用直接刚度法求解
        int nNodes = request.Nodes.Count;
        int nElements = request.Elements.Count;
        int dof = nNodes * 3; // 每个节点3个自由度: u, v, θ

        // 组装总体刚度矩阵
        double[,] K = new double[dof, dof];
        double[] F = new double[dof]; // 荷载向量

        foreach (var elem in request.Elements)
        {
            var ni = request.Nodes[elem.NodeI];
            var nj = request.Nodes[elem.NodeJ];
            double Le = Math.Abs(nj.X - ni.X);
            if (Le < 1e-10) continue;

            double E = elem.E, A = elem.A, I = elem.I;

            // 梁单元刚度矩阵（6x6，Euler-Bernoulli梁）
            double[,] ke = GetBeamElementStiffness(E, A, I, Le);

            // 组装到总体矩阵
            int[] dofs = { elem.NodeI * 3, elem.NodeI * 3 + 1, elem.NodeI * 3 + 2,
                          elem.NodeJ * 3, elem.NodeJ * 3 + 1, elem.NodeJ * 3 + 2 };

            for (int i = 0; i < 6; i++)
                for (int j = 0; j < 6; j++)
                    K[dofs[i], dofs[j]] += ke[i, j];

            // 等效节点荷载（均布荷载）
            foreach (var load in request.Loads.Where(l => l.ElementIndex == request.Elements.IndexOf(elem)))
            {
                if (load.Type == FEMLoadType.Uniform)
                {
                    double w = load.Value;
                    // 等效节点力（固端反力）
                    F[dofs[1]] += w * Le / 2;    // Vi
                    F[dofs[2]] += w * Le * Le / 12; // Mi
                    F[dofs[4]] += w * Le / 2;    // Vj
                    F[dofs[5]] -= w * Le * Le / 12; // Mj
                }
                else if (load.Type == FEMLoadType.Concentrated)
                {
                    double P = load.Value;
                    double a = load.Position * Le;
                    double b = Le - a;
                    F[dofs[1]] += P * b * b * (3 * a + b) / (Le * Le * Le);
                    F[dofs[2]] += P * a * b * b / (Le * Le);
                    F[dofs[4]] += P * a * a * (a + 3 * b) / (Le * Le * Le);
                    F[dofs[5]] -= P * a * a * b / (Le * Le);
                }
            }
        }

        // 施加集中荷载到节点
        foreach (var load in request.Loads.Where(l => l.ElementIndex < 0 && l.NodeIndex >= 0))
        {
            int ni = load.NodeIndex;
            if (load.Type == FEMLoadType.ConcentratedForce)
                F[ni * 3 + 1] += load.Value;
            else if (load.Type == FEMLoadType.ConcentratedMoment)
                F[ni * 3 + 2] += load.Value;
        }

        // 施加约束（大数法）
        double bigNum = 1e20;
        foreach (var node in request.Nodes)
        {
            int idx = request.Nodes.IndexOf(node);
            if (node.FixX) { K[idx * 3, idx * 3] += bigNum; F[idx * 3] = 0; }
            if (node.FixY) { K[idx * 3 + 1, idx * 3 + 1] += bigNum; F[idx * 3 + 1] = 0; }
            if (node.FixR) { K[idx * 3 + 2, idx * 3 + 2] += bigNum; F[idx * 3 + 2] = 0; }
            if (node.SpringY > 0) K[idx * 3 + 1, idx * 3 + 1] += node.SpringY;
            if (node.SpringR > 0) K[idx * 3 + 2, idx * 3 + 2] += node.SpringR;
        }

        // 求解 KU = F （高斯消元法）
        double[] U = SolveLinearSystem(K, F, dof);

        // 提取结果
        var result = new FEMBeamResult();
        for (int i = 0; i < nNodes; i++)
        {
            result.Displacements.Add(new FEMDisplacement
            {
                NodeIndex = i,
                X = request.Nodes[i].X,
                Dx = U[i * 3],
                Dy = U[i * 3 + 1] * 1000, // 转mm
                Rotation = U[i * 3 + 2]
            });
        }

        // 计算单元内力
        foreach (var elem in request.Elements)
        {
            var ni = request.Nodes[elem.NodeI];
            var nj = request.Nodes[elem.NodeJ];
            double Le = Math.Abs(nj.X - ni.X);
            double E = elem.E, Ae = elem.A, Ie = elem.I;
            double[,] ke = GetBeamElementStiffness(E, Ae, Ie, Le);

            int[] dofs = { elem.NodeI * 3, elem.NodeI * 3 + 1, elem.NodeI * 3 + 2,
                          elem.NodeJ * 3, elem.NodeJ * 3 + 1, elem.NodeJ * 3 + 2 };
            double[] ue = new double[6];
            for (int i = 0; i < 6; i++) ue[i] = U[dofs[i]];

            // 单元内力 = ke * ue
            double[] fe = new double[6];
            for (int i = 0; i < 6; i++)
                for (int j = 0; j < 6; j++)
                    fe[i] += ke[i, j] * ue[j];

            result.ElementForces.Add(new FEMElementForce
            {
                ElementIndex = request.Elements.IndexOf(elem),
                AxialI = fe[0], ShearI = fe[1], MomentI = fe[2],
                AxialJ = -fe[3], ShearJ = -fe[4], MomentJ = -fe[5]
            });
        }

        // 计算支座反力
        foreach (var node in request.Nodes)
        {
            int idx = request.Nodes.IndexOf(node);
            if (node.FixY || node.FixX || node.FixR)
            {
                double ry = node.FixY ? K[idx * 3 + 1, idx * 3 + 1] * U[idx * 3 + 1] / bigNum * bigNum : 0;
                // 简化: 从单元力计算
                double reaction = 0;
                foreach (var ef in result.ElementForces)
                {
                    var elem = request.Elements[ef.ElementIndex];
                    if (elem.NodeI == idx) reaction += ef.ShearI;
                    if (elem.NodeJ == idx) reaction += ef.ShearJ;
                }
                result.Reactions.Add(new FEMReaction
                {
                    NodeIndex = idx,
                    X = node.X,
                    Fy = reaction
                });
            }
        }

        return result;
    }

    /// <summary>有限元板分析（薄板弯曲）</summary>
    public FEMPlateResult AnalyzePlate(FEMPlateRequest request)
    {
        // Navier解法（四边简支矩形板）或Levy解法
        double a = request.Width, b = request.Height;
        double t = request.Thickness, E = request.E, v = request.PoissonRatio;
        double q = request.Pressure;
        int nx = request.MeshX > 0 ? request.MeshX : 20;
        int ny = request.MeshY > 0 ? request.MeshY : 20;

        // 板的弯曲刚度
        double D = E * t * t * t / (12 * (1 - v * v));

        var result = new FEMPlateResult
        {
            D = D,
            MaxDeflection = 0,
            MaxStress = 0
        };

        if (request.SupportType == PlateSupportType.FourEdgeSimple)
        {
            // Navier双重正弦级数解（四边简支）
            // w(x,y) = ΣΣ amn * sin(mπx/a) * sin(nπy/b)
            // amn = 16q/(π⁶Dmn), Dmn = (m²/a² + n²/b²)²
            for (int ix = 0; ix <= nx; ix++)
            {
                for (int iy = 0; iy <= ny; iy++)
                {
                    double x = a * ix / nx;
                    double y = b * iy / ny;
                    double w = 0, Mx = 0, My = 0;

                    // 取前10阶
                    for (int m = 1; m <= 9; m += 2) // 仅奇数项
                    {
                        for (int n = 1; n <= 9; n += 2)
                        {
                            double alpha = m * Math.PI / a;
                            double beta = n * Math.PI / b;
                            double Dmn = Math.Pow(alpha * alpha + beta * beta, 2);
                            double amn = 16 * q / (Math.PI * Math.PI * m * n * D * Dmn);
                            double sinX = Math.Sin(m * Math.PI * x / a);
                            double sinY = Math.Sin(n * Math.PI * y / b);
                            w += amn * sinX * sinY;
                            Mx += -D * amn * (-alpha * alpha + v * beta * beta) * sinX * sinY;
                            My += -D * amn * (v * alpha * alpha - beta * beta) * sinX * sinY;
                            // 不对，弯矩公式应该是:
                            // Mx = -D*(∂²w/∂x² + v*∂²w/∂y²)
                            // ∂²w/∂x² = -amn*alpha²*sin, ∂²w/∂y² = -amn*beta²*sin
                            // Mx = D*amn*(alpha²+v*beta²)*sin*sin
                        }
                    }
                    // 修正弯矩计算
                    Mx = 0; My = 0;
                    for (int m = 1; m <= 9; m += 2)
                    {
                        for (int n = 1; n <= 9; n += 2)
                        {
                            double alpha = m * Math.PI / a;
                            double beta = n * Math.PI / b;
                            double Dmn2 = Math.Pow(alpha * alpha + beta * beta, 2);
                            double amn = 16 * q / (Math.PI * Math.PI * m * n * D * Dmn2);
                            double sinX = Math.Sin(m * Math.PI * x / a);
                            double sinY = Math.Sin(n * Math.PI * y / b);
                            Mx += D * amn * (alpha * alpha + v * beta * beta) * sinX * sinY;
                            My += D * amn * (v * alpha * alpha + beta * beta) * sinX * sinY;
                        }
                    }

                    result.Points.Add(new PlateResultPoint
                    {
                        X = x, Y = y,
                        Deflection = w * 1000, // 转mm
                        MomentX = Mx, MomentY = My,
                        StressX = 6 * Mx / (t * t), // σ = 6M/t²
                        StressY = 6 * My / (t * t)
                    });

                    if (Math.Abs(w) > Math.Abs(result.MaxDeflection))
                    {
                        result.MaxDeflection = w * 1000;
                        result.MaxDeflectionX = x;
                        result.MaxDeflectionY = y;
                    }
                    double stress = Math.Max(Math.Abs(6 * Mx / (t * t)), Math.Abs(6 * My / (t * t)));
                    if (stress > result.MaxStress)
                        result.MaxStress = stress;
                }
            }
        }
        else
        {
            // 其他支承条件用简化公式(Timoshenko表格系数法)
            // α = a/b 长边比
            double ratio = Math.Max(a, b) / Math.Min(a, b);
            // 查表系数（四边简支近似）
            double alpha1 = GetTimoshenkoCoeff(ratio);
            double wmax = alpha1 * q * Math.Pow(Math.Min(a, b), 4) / D;
            result.MaxDeflection = wmax * 1000;
            result.MaxDeflectionX = a / 2;
            result.MaxDeflectionY = b / 2;

            // 简化填充网格点
            for (int ix = 0; ix <= nx; ix++)
            {
                for (int iy = 0; iy <= ny; iy++)
                {
                    double x = a * ix / nx, y = b * iy / ny;
                    double w = wmax * Math.Sin(Math.PI * x / a) * Math.Sin(Math.PI * y / b);
                    result.Points.Add(new PlateResultPoint
                    {
                        X = x, Y = y,
                        Deflection = w * 1000,
                        StressX = result.MaxStress * Math.Sin(Math.PI * x / a) * Math.Sin(Math.PI * y / b)
                    });
                }
            }
        }

        return result;
    }

    /// <summary>Timoshenko板弯曲系数（四边简支，均布荷载）</summary>
    private static double GetTimoshenkoCoeff(double ratio)
    {
        // α系数（wmax = α*q*b⁴/D）— Timoshenko《板壳理论》表35
        if (ratio <= 1.0) return 0.00406;
        if (ratio <= 1.2) return 0.00564;
        if (ratio <= 1.4) return 0.00772;
        if (ratio <= 1.6) return 0.00906;
        if (ratio <= 1.8) return 0.01013;
        if (ratio <= 2.0) return 0.01110;
        if (ratio <= 3.0) return 0.01223;
        return 0.01302; // ≥4.0 趋近单向板
    }

    /// <summary>Euler-Bernoulli梁单元刚度矩阵(6x6)</summary>
    private static double[,] GetBeamElementStiffness(double E, double A, double I, double L)
    {
        double L2 = L * L, L3 = L * L * L;
        double ea = E * A / L;
        double ei12 = 12 * E * I / L3;
        double ei6 = 6 * E * I / L2;
        double ei4 = 4 * E * I / L;
        double ei2 = 2 * E * I / L;

        return new double[,]
        {
            {  ea,     0,     0,  -ea,     0,     0 },
            {   0,  ei12,  ei6,    0, -ei12,  ei6 },
            {   0,   ei6,  ei4,    0,  -ei6,  ei2 },
            { -ea,     0,     0,   ea,     0,     0 },
            {   0, -ei12, -ei6,    0,  ei12, -ei6 },
            {   0,   ei6,  ei2,    0,  -ei6,  ei4 }
        };
    }

    /// <summary>高斯消元法求解线性方程组</summary>
    private static double[] SolveLinearSystem(double[,] A, double[] b, int n)
    {
        // 复制矩阵
        double[,] a = new double[n, n];
        double[] x = new double[n];
        double[] bb = new double[n];
        Array.Copy(b, bb, n);
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                a[i, j] = A[i, j];

        // 列主元消元
        for (int k = 0; k < n; k++)
        {
            // 选主元
            int maxRow = k;
            double maxVal = Math.Abs(a[k, k]);
            for (int i = k + 1; i < n; i++)
            {
                if (Math.Abs(a[i, k]) > maxVal)
                {
                    maxVal = Math.Abs(a[i, k]);
                    maxRow = i;
                }
            }
            // 交换行
            if (maxRow != k)
            {
                for (int j = k; j < n; j++)
                    (a[k, j], a[maxRow, j]) = (a[maxRow, j], a[k, j]);
                (bb[k], bb[maxRow]) = (bb[maxRow], bb[k]);
            }

            if (Math.Abs(a[k, k]) < 1e-30) continue;

            // 消元
            for (int i = k + 1; i < n; i++)
            {
                double factor = a[i, k] / a[k, k];
                for (int j = k; j < n; j++)
                    a[i, j] -= factor * a[k, j];
                bb[i] -= factor * bb[k];
            }
        }

        // 回代
        for (int i = n - 1; i >= 0; i--)
        {
            double sum = bb[i];
            for (int j = i + 1; j < n; j++)
                sum -= a[i, j] * x[j];
            x[i] = Math.Abs(a[i, i]) > 1e-30 ? sum / a[i, i] : 0;
        }

        return x;
    }
}

#region FEM模型定义

/// <summary>FEM节点</summary>
public class FEMNode
{
    /// <summary>位置 (m)</summary>
    public double X { get; set; }
    /// <summary>约束X方向平动</summary>
    public bool FixX { get; set; }
    /// <summary>约束Y方向平动</summary>
    public bool FixY { get; set; }
    /// <summary>约束转动</summary>
    public bool FixR { get; set; }
    /// <summary>Y方向弹簧刚度 (kN/m)</summary>
    public double SpringY { get; set; }
    /// <summary>转动弹簧刚度 (kN·m/rad)</summary>
    public double SpringR { get; set; }
}

/// <summary>FEM梁单元</summary>
public class FEMElement
{
    /// <summary>起始节点索引</summary>
    public int NodeI { get; set; }
    /// <summary>终止节点索引</summary>
    public int NodeJ { get; set; }
    /// <summary>弹性模量 (MPa)</summary>
    public double E { get; set; } = 70000;
    /// <summary>截面惯性矩 (mm⁴)</summary>
    public double I { get; set; } = 1e6;
    /// <summary>截面面积 (mm²)</summary>
    public double A { get; set; } = 1000;
    /// <summary>i端铰接</summary>
    public bool HingeI { get; set; }
    /// <summary>j端铰接</summary>
    public bool HingeJ { get; set; }
}

/// <summary>FEM荷载类型</summary>
public enum FEMLoadType
{
    Uniform, Concentrated, Trapezoidal, Temperature,
    ConcentratedForce, ConcentratedMoment
}

/// <summary>FEM荷载</summary>
public class FEMLoad
{
    /// <summary>荷载类型</summary>
    public FEMLoadType Type { get; set; }
    /// <summary>荷载值 (kN/m 或 kN 或 kN·m)</summary>
    public double Value { get; set; }
    /// <summary>作用单元索引（-1表示节点荷载）</summary>
    public int ElementIndex { get; set; } = -1;
    /// <summary>作用节点索引（节点荷载时使用）</summary>
    public int NodeIndex { get; set; } = -1;
    /// <summary>荷载位置（0~1归一化，集中力用）</summary>
    public double Position { get; set; } = 0.5;
}

/// <summary>FEM梁分析请求</summary>
public class FEMBeamRequest
{
    /// <summary>节点列表</summary>
    public List<FEMNode> Nodes { get; set; } = new();
    /// <summary>单元列表</summary>
    public List<FEMElement> Elements { get; set; } = new();
    /// <summary>荷载列表</summary>
    public List<FEMLoad> Loads { get; set; } = new();
}

/// <summary>FEM位移结果</summary>
public class FEMDisplacement
{
    public int NodeIndex { get; set; }
    public double X { get; set; }
    public double Dx { get; set; }
    public double Dy { get; set; }
    public double Rotation { get; set; }
}

/// <summary>FEM单元内力</summary>
public class FEMElementForce
{
    public int ElementIndex { get; set; }
    public double AxialI { get; set; }
    public double ShearI { get; set; }
    public double MomentI { get; set; }
    public double AxialJ { get; set; }
    public double ShearJ { get; set; }
    public double MomentJ { get; set; }
}

/// <summary>FEM支座反力</summary>
public class FEMReaction
{
    public int NodeIndex { get; set; }
    public double X { get; set; }
    public double Fx { get; set; }
    public double Fy { get; set; }
    public double Moment { get; set; }
}

/// <summary>FEM梁分析结果</summary>
public class FEMBeamResult
{
    public List<FEMDisplacement> Displacements { get; set; } = new();
    public List<FEMElementForce> ElementForces { get; set; } = new();
    public List<FEMReaction> Reactions { get; set; } = new();
}

/// <summary>板支承类型</summary>
public enum PlateSupportType
{
    /// <summary>四边简支</summary>
    FourEdgeSimple,
    /// <summary>三边简支一边自由</summary>
    ThreeEdgeSimpleOneFree,
    /// <summary>对边简支</summary>
    TwoEdgeSimple,
    /// <summary>四边固定</summary>
    FourEdgeFixed,
    /// <summary>三边固定一边自由</summary>
    ThreeEdgeFixedOneFree
}

/// <summary>FEM板分析请求</summary>
public class FEMPlateRequest
{
    /// <summary>宽度 (m)</summary>
    public double Width { get; set; }
    /// <summary>高度 (m)</summary>
    public double Height { get; set; }
    /// <summary>板厚 (m)</summary>
    public double Thickness { get; set; }
    /// <summary>弹性模量 (MPa)</summary>
    public double E { get; set; } = 72000;
    /// <summary>泊松比</summary>
    public double PoissonRatio { get; set; } = 0.22;
    /// <summary>均布压力 (kPa)</summary>
    public double Pressure { get; set; }
    /// <summary>支承类型</summary>
    public PlateSupportType SupportType { get; set; }
    /// <summary>X方向网格数</summary>
    public int MeshX { get; set; } = 20;
    /// <summary>Y方向网格数</summary>
    public int MeshY { get; set; } = 20;
}

/// <summary>板结果数据点</summary>
public class PlateResultPoint
{
    public double X { get; set; }
    public double Y { get; set; }
    /// <summary>挠度 (mm)</summary>
    public double Deflection { get; set; }
    /// <summary>X方向弯矩 (kN·m/m)</summary>
    public double MomentX { get; set; }
    /// <summary>Y方向弯矩 (kN·m/m)</summary>
    public double MomentY { get; set; }
    /// <summary>X方向应力 (MPa)</summary>
    public double StressX { get; set; }
    /// <summary>Y方向应力 (MPa)</summary>
    public double StressY { get; set; }
}

/// <summary>FEM板分析结果</summary>
public class FEMPlateResult
{
    /// <summary>板弯曲刚度D (kN·m)</summary>
    public double D { get; set; }
    /// <summary>最大挠度 (mm)</summary>
    public double MaxDeflection { get; set; }
    public double MaxDeflectionX { get; set; }
    public double MaxDeflectionY { get; set; }
    /// <summary>最大应力 (MPa)</summary>
    public double MaxStress { get; set; }
    /// <summary>网格结果点</summary>
    public List<PlateResultPoint> Points { get; set; } = new();
}

#endregion
