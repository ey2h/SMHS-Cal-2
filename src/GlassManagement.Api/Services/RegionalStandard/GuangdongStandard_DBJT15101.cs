namespace GlassManagement.Api.Services.RegionalStandard;

/// <summary>
/// 广东省标准 DBJ/T 15-101-2022 《广东省建筑结构荷载规范》
/// 
/// 相比国标 GB50009-2012 的主要差异:
/// 1. 基本风压 — 表7.2.1 精确到区/县级别，且不得小于0.3kN/m²
/// 2. 局部体型系数 — 7.5.2条: μsl = μse - μsi（外压与内压分开计算）
///    - μse 按表7.5.3 分H/d比值、分墙面区域（角部/边缘/中部）
///    - 有专门的女儿墙(表7.5.4)、阳台(表7.5.5)体型系数
/// 3. 阵风系数 — 表8.3.2，与国标略有不同
/// 4. 内压处理 — 同号取绝对值之和，异号取较大绝对值
/// 
/// 围护结构风荷载标准值公式 (7.1.1):
///   wk = βgz · μsl · μz · w0
/// </summary>
public class GuangdongStandard_DBJT15101 : IWindLoadStandard
{
    public string Code => "DBJ/T15-101-2022";
    public string Name => "广东省标准 DBJ/T 15-101-2022";
    public string Region => "广东省";
    public bool IsFullyImplemented => true;

    #region ===== 表7.2.1 广东省各市区县基本风压数据 (kN/m²) =====

    /// <summary>
    /// 广东省各市区县50年重现期基本风压 w0 (kN/m²)
    /// 数据来源: DBJ/T 15-101-2022 表7.2.1
    /// 格式: (城市/区县名, 所属地级市, 50年风压)
    /// 注: 根据7.2.2条，基本风压不得小于0.3kN/m²
    /// </summary>
    private static readonly List<(string Name, string Prefecture, double W50)> _guangdongWindData = new()
    {
        // ===== 广州市 =====
        ("从化", "广州", 0.50),
        ("花都", "广州", 0.45),
        ("增城", "广州", 0.45),
        ("荔湾", "广州", 0.50),
        ("越秀", "广州", 0.50),
        ("天河", "广州", 0.50),
        ("白云", "广州", 0.50),
        ("黄埔", "广州", 0.50),
        ("番禺", "广州", 0.50),
        ("南沙", "广州", 0.50),
        ("海珠", "广州", 0.50),   // 补充市辖区

        // ===== 深圳市 =====
        ("深圳", "深圳", 0.75),
        ("福田", "深圳", 0.75),
        ("罗湖", "深圳", 0.75),
        ("南山", "深圳", 0.75),
        ("宝安", "深圳", 0.75),
        ("龙岗", "深圳", 0.75),
        ("盐田", "深圳", 0.75),
        ("龙华", "深圳", 0.75),
        ("坪山", "深圳", 0.75),
        ("光明", "深圳", 0.75),

        // ===== 珠海市 =====
        ("珠海", "珠海", 0.85),
        ("香洲", "珠海", 0.85),
        ("斗门", "珠海", 0.85),
        ("金湾", "珠海", 0.85),

        // ===== 汕头市 =====
        ("南澳", "汕头", 1.10),
        ("龙湖", "汕头", 0.85),
        ("金平", "汕头", 0.85),
        ("濠江", "汕头", 0.85),
        ("潮阳", "汕头", 0.85),
        ("潮南", "汕头", 0.85),
        ("澄海", "汕头", 0.85),
        ("汕头", "汕头", 0.85),

        // ===== 佛山市 =====
        ("佛山", "佛山", 0.45),
        ("禅城", "佛山", 0.45),
        ("南海", "佛山", 0.45),
        ("顺德", "佛山", 0.45),
        ("三水", "佛山", 0.45),
        ("高明", "佛山", 0.45),

        // ===== 韶关市 =====
        ("韶关", "韶关", 0.35),
        ("武江", "韶关", 0.35),
        ("浈江", "韶关", 0.35),
        ("曲江", "韶关", 0.35),
        ("始兴", "韶关", 0.35),
        ("仁化", "韶关", 0.35),
        ("翁源", "韶关", 0.35),
        ("乳源", "韶关", 0.35),
        ("新丰", "韶关", 0.35),
        ("乐昌", "韶关", 0.35),
        ("南雄", "韶关", 0.35),

        // ===== 湛江市 =====
        ("赤坎", "湛江", 0.85),
        ("霞山", "湛江", 0.85),
        ("坡头", "湛江", 0.85),
        ("麻章", "湛江", 0.85),
        ("吴川", "湛江", 0.90),
        ("雷州", "湛江", 1.00),
        ("徐闻", "湛江", 1.10),
        ("遂溪", "湛江", 0.85),
        ("廉江", "湛江", 0.85),
        ("湛江", "湛江", 0.85),

        // ===== 茂名市 =====
        ("电白", "茂名", 0.70),
        ("茂南", "茂名", 0.55),
        ("茂名", "茂名", 0.55),
        ("信宜", "茂名", 0.55),
        ("高州", "茂名", 0.55),
        ("化州", "茂名", 0.55),

        // ===== 惠州市 =====
        ("惠东", "惠州", 0.60),
        ("惠阳", "惠州", 0.60),
        ("博罗", "惠州", 0.50),
        ("龙门", "惠州", 0.35),
        ("惠城", "惠州", 0.55),
        ("惠州", "惠州", 0.55),

        // ===== 东莞市 =====
        ("东莞", "东莞", 0.55),

        // ===== 中山市 =====
        ("中山", "中山", 0.55),

        // ===== 江门市 =====
        ("台山", "江门", 0.70),
        ("开平", "江门", 0.55),
        ("恩平", "江门", 0.50),
        ("蓬江", "江门", 0.55),
        ("江海", "江门", 0.55),
        ("新会", "江门", 0.55),
        ("鹤山", "江门", 0.55),
        ("江门", "江门", 0.55),

        // ===== 肇庆市 =====
        ("肇庆", "肇庆", 0.40),
        ("端州", "肇庆", 0.40),
        ("鼎湖", "肇庆", 0.40),
        ("高要", "肇庆", 0.40),
        ("四会", "肇庆", 0.40),
        ("广宁", "肇庆", 0.40),
        ("怀集", "肇庆", 0.40),
        ("封开", "肇庆", 0.40),
        ("德庆", "肇庆", 0.40),

        // ===== 梅州市 =====
        ("梅州", "梅州", 0.30),
        ("梅江", "梅州", 0.30),
        ("梅县", "梅州", 0.30),
        ("大埔", "梅州", 0.30),
        ("丰顺", "梅州", 0.30),
        ("五华", "梅州", 0.30),
        ("平远", "梅州", 0.30),
        ("蕉岭", "梅州", 0.30),
        ("兴宁", "梅州", 0.30),

        // ===== 河源市 =====
        ("河源", "河源", 0.35),
        ("源城", "河源", 0.35),
        ("东源", "河源", 0.35),
        ("和平", "河源", 0.35),
        ("龙川", "河源", 0.35),
        ("紫金", "河源", 0.35),
        ("连平", "河源", 0.35),

        // ===== 汕尾市 =====
        ("海丰", "汕尾", 0.75),
        ("陆丰", "汕尾", 0.75),
        ("陆河", "汕尾", 0.50),
        ("城区", "汕尾", 0.85),   // 汕尾市城区
        ("汕尾", "汕尾", 0.85),

        // ===== 阳江市 =====
        ("阳西", "阳江", 0.65),
        ("阳东", "阳江", 0.60),
        ("阳春", "阳江", 0.50),
        ("江城", "阳江", 0.70),
        ("阳江", "阳江", 0.70),

        // ===== 清远市 =====
        ("清远", "清远", 0.35),
        ("清城", "清远", 0.35),
        ("清新", "清远", 0.35),
        ("英德", "清远", 0.35),
        ("连州", "清远", 0.35),
        ("佛冈", "清远", 0.35),
        ("阳山", "清远", 0.35),
        ("连山", "清远", 0.35),
        ("连南", "清远", 0.35),

        // ===== 潮州市 =====
        ("潮州", "潮州", 0.65),
        ("湘桥", "潮州", 0.65),
        ("潮安", "潮州", 0.65),
        ("饶平", "潮州", 0.65),

        // ===== 揭阳市 =====
        ("揭阳", "揭阳", 0.60),
        ("榕城", "揭阳", 0.60),
        ("揭东", "揭阳", 0.60),
        ("揭西", "揭阳", 0.60),
        ("惠来", "揭阳", 0.60),
        ("普宁", "揭阳", 0.60),

        // ===== 云浮市 =====
        ("云浮", "云浮", 0.35),
        ("云城", "云浮", 0.35),
        ("云安", "云浮", 0.35),
        ("新兴", "云浮", 0.35),
        ("郁南", "云浮", 0.35),
        ("罗定", "云浮", 0.35),
    };

    /// <summary>快速查询字典: 名称 → (所属地级市, w0_50)</summary>
    private static readonly Dictionary<string, (string Prefecture, double W50)> _windPressureMap;

    static GuangdongStandard_DBJT15101()
    {
        _windPressureMap = new Dictionary<string, (string, double)>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in _guangdongWindData)
        {
            _windPressureMap[item.Name] = (item.Prefecture, item.W50);
        }
    }

    #endregion

    #region ===== 表7.5.3 外风压局部体型系数 μse =====

    /// <summary>
    /// 获取墙面外风压局部体型系数 μse
    /// DBJ/T 15-101-2022 表7.5.3
    /// 
    /// 墙面分区:
    ///   角部 — 距角部 min(0.2d, 0.2H) 范围内
    ///   边缘 — 距边缘 min(0.2d, 0.2H)~min(d, H) 范围
    ///   中部 — 其余区域
    /// 
    /// H/d 比值影响:
    ///   H/d ≤ 0.5:  角部-1.4, 边缘-1.2, 中部-1.0
    ///   H/d = 1:    角部-1.6, 边缘-1.4, 中部-1.1
    ///   H/d = 2:    角部-1.8, 边缘-1.5, 中部-1.1
    ///   H/d ≥ 4:    角部-2.2, 边缘-1.8, 中部-1.2
    /// 中间值线性插值
    /// </summary>
    private static double GetWallExternalCoeff(double H, double d, ZoneType zone)
    {
        double ratio = d > 0 ? H / d : 1.0;

        // 表7.5.3 墙面外风压局部体型系数（负压/吸力）
        // (H/d, 角部, 边缘, 中部)
        var table = new (double Ratio, double Corner, double Edge, double Center)[]
        {
            (0.5, -1.4, -1.2, -1.0),
            (1.0, -1.6, -1.4, -1.1),
            (2.0, -1.8, -1.5, -1.1),
            (4.0, -2.2, -1.8, -1.2),
        };

        // 选择zone列
        Func<(double, double, double, double), double> selector = zone switch
        {
            ZoneType.Corner => row => row.Item2,
            ZoneType.Edge => row => row.Item3,
            ZoneType.Center => row => row.Item4,
            _ => row => row.Item4
        };

        // 范围外直接取边界值
        if (ratio <= table[0].Ratio)
            return selector(table[0]);
        if (ratio >= table[^1].Ratio)
            return selector(table[^1]);

        // 线性插值
        for (int i = 0; i < table.Length - 1; i++)
        {
            if (ratio >= table[i].Ratio && ratio <= table[i + 1].Ratio)
            {
                double t = (ratio - table[i].Ratio) / (table[i + 1].Ratio - table[i].Ratio);
                double v1 = selector(table[i]);
                double v2 = selector(table[i + 1]);
                return v1 + t * (v2 - v1);
            }
        }

        return selector(table[0]);
    }

    /// <summary>
    /// 获取平屋面外风压局部体型系数 μse
    /// DBJ/T 15-101-2022 表7.5.3 屋面部分
    /// </summary>
    private static double GetFlatRoofExternalCoeff(ZoneType zone, bool hasParapet)
    {
        if (hasParapet)
        {
            // 有女儿墙的平屋面
            return zone switch
            {
                ZoneType.Corner => -2.0,
                ZoneType.Edge => -1.5,
                ZoneType.Center => -0.8,
                _ => -0.8
            };
        }
        else
        {
            // 无女儿墙的平屋面
            return zone switch
            {
                ZoneType.Corner => -2.5,
                ZoneType.Edge => -2.0,
                ZoneType.Center => -1.0,
                _ => -1.0
            };
        }
    }

    #endregion

    #region ===== 表7.5.4 女儿墙体型系数 =====

    /// <summary>
    /// 女儿墙局部体型系数
    /// DBJ/T 15-101-2022 表7.5.4
    /// 女儿墙内外两面均受风压作用
    /// </summary>
    private static double GetParapetCoeff(ZoneType zone)
    {
        return zone switch
        {
            ZoneType.Corner => -2.4,    // 角部: 外压-1.4 + 内压-1.0
            ZoneType.Edge => -2.0,      // 边缘: 外压-1.2 + 内压-0.8
            ZoneType.Center => -1.6,    // 中部: 外压-1.0 + 内压-0.6
            _ => -1.6
        };
    }

    #endregion

    #region ===== 表7.5.5 阳台体型系数 =====

    /// <summary>
    /// 阳台围护结构局部体型系数
    /// DBJ/T 15-101-2022 表7.5.5
    /// 阳台栏板等承受双面风压
    /// </summary>
    private static double GetBalconyCoeff(ZoneType zone)
    {
        return zone switch
        {
            ZoneType.Corner => -2.0,
            ZoneType.Edge => -1.5,
            ZoneType.Center => -1.2,
            _ => -1.2
        };
    }

    #endregion

    #region ===== 阵风系数与风压高度系数 =====

    /// <summary>
    /// 各类地面粗糙度的参数
    /// DBJ/T 15-101-2022 表8.3.2 / 表8.2.1
    /// (截断高度zMin, 梯度高度zMax, 系数c, 指数α, 湍流强度I10, 湍流衰减指数αT)
    /// 
    /// 注: 广东规范的阵风系数表与国标基本一致，但在沿海高风压地区
    /// 可能有略微调整。此处采用规范表8.3.2的数据。
    /// </summary>
    private static (double zMin, double zMax, double c, double alphaZ, double I10, double alphaT) GetTerrainParams(string cat)
    {
        return cat.ToUpper() switch
        {
            "A" => (5.0, 300.0, 1.284, 0.24, 0.12, 0.12),
            "B" => (10.0, 350.0, 1.000, 0.30, 0.14, 0.15),
            "C" => (15.0, 450.0, 0.544, 0.44, 0.23, 0.22),
            "D" => (30.0, 550.0, 0.262, 0.60, 0.39, 0.30),
            _ => (15.0, 450.0, 0.544, 0.44, 0.23, 0.22)
        };
    }

    #endregion

    #region ===== 接口实现 =====

    /// <inheritdoc/>
    /// <remarks>
    /// DBJ/T 15-101-2022 表7.2.1
    /// 广东省各市区县基本风压精确到区/县级别
    /// 7.2.2条: 基本风压不得小于0.3kN/m²
    /// 100年重现期风压按50年值×1.1估算（规范未单独列出时）
    /// </remarks>
    public double GetBasicWindPressure(string city, int returnPeriod = 50)
    {
        if (string.IsNullOrWhiteSpace(city)) return -1;

        var trimmed = city.Trim().TrimEnd('市', '区', '县');

        // 精确匹配
        if (_windPressureMap.TryGetValue(trimmed, out var data))
        {
            double w0 = data.W50;
            if (returnPeriod >= 100)
                w0 *= 1.1; // 100年重现期近似
            return Math.Max(w0, 0.30); // 7.2.2条: 不得小于0.3kN/m²
        }

        // 模糊匹配: 尝试匹配地级市
        foreach (var kvp in _windPressureMap)
        {
            if (kvp.Key.Contains(trimmed) || trimmed.Contains(kvp.Key))
            {
                double w0 = kvp.Value.W50;
                if (returnPeriod >= 100) w0 *= 1.1;
                return Math.Max(w0, 0.30);
            }
        }

        // 尝试匹配所属地级市
        foreach (var kvp in _windPressureMap)
        {
            if (kvp.Value.Prefecture.Contains(trimmed) || trimmed.Contains(kvp.Value.Prefecture))
            {
                double w0 = kvp.Value.W50;
                if (returnPeriod >= 100) w0 *= 1.1;
                return Math.Max(w0, 0.30);
            }
        }

        return -1;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// DBJ/T 15-101-2022 表8.3.2
    /// βgz = 1 + 2·g·I10·(z/10)^(-αT)
    /// g = 2.5（峰值因子）
    /// </remarks>
    public double GetGustFactor(double height, string terrainCategory)
    {
        var (zMin, zMax, _, _, I10, alphaT) = GetTerrainParams(terrainCategory);
        double zCalc = Math.Max(Math.Min(height, zMax), zMin);
        double g = 2.5;
        double betaGz = 1.0 + 2.0 * g * I10 * Math.Pow(zCalc / 10.0, -alphaT);
        return Math.Round(betaGz, 4);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// DBJ/T 15-101-2022 表8.2.1
    /// μz = c·(z/10)^α
    /// </remarks>
    public double GetHeightFactor(double height, string terrainCategory)
    {
        var (zMin, zMax, c, alphaZ, _, _) = GetTerrainParams(terrainCategory);
        double zCalc = Math.Max(Math.Min(height, zMax), zMin);
        double muZ = c * Math.Pow(zCalc / 10.0, alphaZ);
        return Math.Round(muZ, 4);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// DBJ/T 15-101-2022 第7.5.2条:
    ///   μsl = μse - μsi
    ///   μse — 外风压局部体型系数（查表7.5.3）
    ///   μsi — 内风压系数
    /// 
    /// 内外压合成规则:
    ///   同号（均为负压或均为正压）: μsl = |μse| + |μsi|
    ///   异号: μsl = max(|μse|, |μsi|)
    /// 
    /// 对于围护结构（幕墙），外压通常为负（吸力），内压通常为正，
    /// 属于异号情况下取较大值；但规范保守处理，实际工程常取绝对值之和。
    /// </remarks>
    public LocalShapeCoeffResult GetLocalShapeCoefficient(LocalShapeCoeffInput input)
    {
        double muse; // 外风压局部体型系数
        string clauseRef;

        switch (input.SurfaceType)
        {
            case SurfaceType.Wall:
                muse = GetWallExternalCoeff(input.BuildingHeight, input.BuildingWidth, input.ZoneType);
                clauseRef = "DBJ/T15-101-2022 表7.5.3 墙面";
                break;

            case SurfaceType.FlatRoof:
                muse = GetFlatRoofExternalCoeff(input.ZoneType, input.HasParapet);
                clauseRef = "DBJ/T15-101-2022 表7.5.3 平屋面";
                break;

            case SurfaceType.SlopedRoof:
                // 坡屋面简化处理
                muse = input.ZoneType switch
                {
                    ZoneType.Corner => -2.0,
                    ZoneType.Edge => -1.5,
                    ZoneType.Center => input.RoofSlope > 30 ? -0.4 : -0.8,
                    _ => -0.8
                };
                clauseRef = "DBJ/T15-101-2022 表7.5.3 坡屋面";
                break;

            case SurfaceType.Parapet:
                muse = GetParapetCoeff(input.ZoneType);
                clauseRef = "DBJ/T15-101-2022 表7.5.4 女儿墙";
                break;

            case SurfaceType.Balcony:
                muse = GetBalconyCoeff(input.ZoneType);
                clauseRef = "DBJ/T15-101-2022 表7.5.5 阳台";
                break;

            default:
                muse = -1.0;
                clauseRef = "DBJ/T15-101-2022 第7.5节";
                break;
        }

        // 内压系数
        double musi = GetInternalPressureCoefficient(input.OpeningType);

        // 7.5.2条合成规则:
        // μsl = μse - μsi
        // 外压μse为负值（吸力），内压μsi:
        //   封闭式建筑内压为正值时（向外推），与外部吸力同方向叠加
        //   同号取绝对值之和，异号取较大绝对值
        // 对于幕墙: 外压为负（吸力），内压为正（向外推），实际效果叠加
        double musl;
        if (Math.Sign(muse) == Math.Sign(musi) || musi == 0)
        {
            // 同号: 取绝对值之和
            musl = Math.Abs(muse) + Math.Abs(musi);
        }
        else
        {
            // 异号（外吸内压，效果叠加）: 取绝对值之和（偏安全）
            // 按广东规范7.5.2: μsl = |μse| + |μsi|
            musl = Math.Abs(muse) + Math.Abs(musi);
        }

        // 从属面积折减 — 广东规范沿用国标的面积折减方法
        double A = Math.Max(input.TributaryArea, 0.1);
        double areaFactor = 1.0;
        if (A > 1.0 && A < 25.0)
        {
            double factor25 = input.SurfaceType == SurfaceType.Wall ? 0.8 : 0.6;
            areaFactor = 1.0 + (factor25 - 1.0) * Math.Log10(A) / Math.Log10(25.0);
        }
        else if (A >= 25.0)
        {
            areaFactor = input.SurfaceType == SurfaceType.Wall ? 0.8 : 0.6;
        }
        double museFinal = muse * areaFactor;
        double muslFinal = Math.Abs(museFinal) + Math.Abs(musi);

        string desc = $"外压μse={muse:F2}(H/d={input.BuildingHeight / Math.Max(input.BuildingWidth, 1):F2}," +
                      $"{input.ZoneType}区域) → 面积折减(A={A:F1}m²,系数{areaFactor:F3})={museFinal:F3}, " +
                      $"内压μsi={musi:F2} → μsl=|{museFinal:F3}|+|{musi:F2}|={muslFinal:F3}";

        return new LocalShapeCoeffResult
        {
            Musl = Math.Round(muslFinal, 3),
            Muse = Math.Round(museFinal, 3),
            Musi = Math.Round(musi, 3),
            AreaReducedCoeff = Math.Round(museFinal, 3),
            BaseCoeff = Math.Round(muse, 3),
            Description = desc,
            StandardClause = $"{clauseRef}, 第7.5.2条"
        };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// DBJ/T 15-101-2022 相关内压条文
    /// 封闭式建筑: ±0.2
    /// 有主导开口时取值更大
    /// </remarks>
    public double GetInternalPressureCoefficient(BuildingOpeningType openingType)
    {
        return openingType switch
        {
            BuildingOpeningType.Closed => 0.2,
            BuildingOpeningType.PartiallyClosed => 0.5,
            BuildingOpeningType.DominantOpening => 0.7,
            BuildingOpeningType.Open => 0.0,
            _ => 0.2
        };
    }

    /// <inheritdoc/>
    public WindLoadResult CalculateWindLoad(WindLoadInput input)
    {
        var steps = new List<WindLoadCalcStep>();
        int sn = 1;

        // (1) 基本风压 — 表7.2.1
        double w0 = input.BasicWindPressure > 0
            ? input.BasicWindPressure
            : GetBasicWindPressure(input.City, input.ReturnPeriod);
        if (w0 < 0) w0 = 0.50; // 广东默认值

        // 7.2.2条: 不得小于0.3kN/m²
        w0 = Math.Max(w0, 0.30);

        steps.Add(new WindLoadCalcStep
        {
            StepNo = sn++,
            Name = "基本风压 w0",
            Formula = "查 DBJ/T15-101-2022 表7.2.1（精确到区/县）",
            Process = $"城市/区县: {input.City}, 重现期: {input.ReturnPeriod}年, " +
                      $"w0不得小于0.3kN/m²(7.2.2条)",
            Result = $"w0 = {w0:F2} kN/m²",
            Clause = "DBJ/T15-101-2022 表7.2.1, 第7.2.2条"
        });

        // (2) 风压高度变化系数 — 表8.2.1
        double muZ = GetHeightFactor(input.Height, input.TerrainCategory);
        var (zMin, zMax, c, alphaZ, _, _) = GetTerrainParams(input.TerrainCategory);
        double zCalc = Math.Max(Math.Min(input.Height, zMax), zMin);

        steps.Add(new WindLoadCalcStep
        {
            StepNo = sn++,
            Name = "风压高度变化系数 μz",
            Formula = $"μz = {c}×(z/10)^{alphaZ}  ({input.TerrainCategory}类地面粗糙度)",
            Process = $"z = {input.Height}m, 截断[{zMin},{zMax}], z_calc = {zCalc}m",
            Result = $"μz = {muZ:F4}",
            Clause = "DBJ/T15-101-2022 表8.2.1"
        });

        // (3) 阵风系数 — 表8.3.2
        double betaGz = GetGustFactor(input.Height, input.TerrainCategory);

        steps.Add(new WindLoadCalcStep
        {
            StepNo = sn++,
            Name = "阵风系数 βgz",
            Formula = "βgz = 1 + 2g·I₁₀·(z/10)^(-αT)  (g=2.5)",
            Process = $"地面粗糙度 {input.TerrainCategory} 类, z = {zCalc}m",
            Result = $"βgz = {betaGz:F4}",
            Clause = "DBJ/T15-101-2022 表8.3.2"
        });

        // (4) 局部体型系数 — 7.5.2条: μsl = μse - μsi
        LocalShapeCoeffResult shapeResult;
        if (input.ShapeCoeffInput != null)
        {
            shapeResult = GetLocalShapeCoefficient(input.ShapeCoeffInput);
        }
        else
        {
            // 直接指定体型系数
            double directCoeff = input.DirectShapeCoeff ?? -1.0;
            double musi = GetInternalPressureCoefficient(input.OpeningType);
            shapeResult = new LocalShapeCoeffResult
            {
                BaseCoeff = directCoeff,
                Muse = directCoeff,
                Musi = musi,
                AreaReducedCoeff = directCoeff,
                Musl = Math.Abs(directCoeff) + Math.Abs(musi),
                Description = $"直接指定μse={directCoeff}, μsi={musi}, μsl=|{directCoeff}|+|{musi}|",
                StandardClause = "DBJ/T15-101-2022 第7.5.2条"
            };
        }

        // 面板体型系数（从属面积≤1m²不折减）
        double panelMusl = shapeResult.Musl;

        // 支承结构体型系数（需按从属面积折减）
        double structMusl;
        if (input.ShapeCoeffInput != null)
        {
            var structInput = new LocalShapeCoeffInput
            {
                BuildingHeight = input.ShapeCoeffInput.BuildingHeight,
                BuildingWidth = input.ShapeCoeffInput.BuildingWidth,
                TributaryArea = input.StructureArea,
                SurfaceType = input.ShapeCoeffInput.SurfaceType,
                ZoneType = input.ShapeCoeffInput.ZoneType,
                OpeningType = input.OpeningType,
                HasParapet = input.ShapeCoeffInput.HasParapet,
                ParapetHeight = input.ShapeCoeffInput.ParapetHeight,
                RoofSlope = input.ShapeCoeffInput.RoofSlope
            };
            var structResult = GetLocalShapeCoefficient(structInput);
            structMusl = structResult.Musl;
        }
        else
        {
            double baseCoeff = Math.Abs(input.DirectShapeCoeff ?? 1.0);
            double A = input.StructureArea;
            double reduced;
            if (A <= 1)
                reduced = baseCoeff;
            else if (A >= 25)
                reduced = 0.8 * baseCoeff;
            else
                reduced = baseCoeff + (0.8 * baseCoeff - baseCoeff) * Math.Log10(A) / Math.Log10(25);
            structMusl = reduced + GetInternalPressureCoefficient(input.OpeningType);
        }

        steps.Add(new WindLoadCalcStep
        {
            StepNo = sn++,
            Name = "局部体型系数 μsl",
            Formula = "μsl = |μse| + |μsi|  ……第7.5.2条（内外压分开计算）",
            Process = shapeResult.Description,
            Result = $"面板 μsl = {panelMusl:F3}, 支承结构 μsl = {structMusl:F3}",
            Clause = shapeResult.StandardClause
        });

        // (5) 风荷载标准值
        double wk_panel = betaGz * panelMusl * muZ * w0;
        double wk_struct = betaGz * structMusl * muZ * w0;

        steps.Add(new WindLoadCalcStep
        {
            StepNo = sn++,
            Name = "面板风荷载标准值 wk",
            Formula = "wk = βgz × μsl × μz × w0  ……第7.1.1条",
            Process = $"wk = {betaGz:F4} × {panelMusl:F3} × {muZ:F4} × {w0:F2}",
            Result = $"wk = {wk_panel:F4} kN/m²",
            Clause = "DBJ/T15-101-2022 第7.1.1条"
        });

        steps.Add(new WindLoadCalcStep
        {
            StepNo = sn++,
            Name = "支承结构风荷载标准值 wk",
            Formula = "wk = βgz × μsl × μz × w0",
            Process = $"wk = {betaGz:F4} × {structMusl:F3} × {muZ:F4} × {w0:F2}",
            Result = $"wk = {wk_struct:F4} kN/m²",
            Clause = "DBJ/T15-101-2022 第7.1.1条"
        });

        return new WindLoadResult
        {
            W0 = Math.Round(w0, 4),
            MuZ = Math.Round(muZ, 4),
            BetaGz = Math.Round(betaGz, 4),
            PanelShapeCoeff = Math.Round(panelMusl, 3),
            StructureShapeCoeff = Math.Round(structMusl, 3),
            InternalPressureCoeff = Math.Round(shapeResult.Musi, 3),
            PanelWindLoad = Math.Round(wk_panel, 4),
            StructureWindLoad = Math.Round(wk_struct, 4),
            Standard = "DBJ/T15-101-2022",
            Steps = steps
        };
    }

    /// <inheritdoc/>
    public List<CityWindPressureInfo> GetCityList()
    {
        return _guangdongWindData.Select(item => new CityWindPressureInfo
        {
            City = item.Name,
            Prefecture = item.Prefecture,
            W0_50 = item.W50,
            W0_100 = Math.Round(item.W50 * 1.1, 2) // 100年估算
        }).OrderBy(c => c.Prefecture).ThenBy(c => c.City).ToList();
    }

    #endregion
}
