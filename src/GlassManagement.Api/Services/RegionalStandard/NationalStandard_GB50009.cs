namespace GlassManagement.Api.Services.RegionalStandard;

/// <summary>国标 GB 50009-2012 风荷载计算</summary>
public class NationalStandard_GB50009 : IWindLoadStandard
{
    public string Code => "GB50009-2012";
    public string Name => "建筑结构荷载规范";
    public string Region => "全国";
    public bool IsFullyImplemented => true;

    private static readonly Dictionary<string, double> CityWindPressure = new(StringComparer.OrdinalIgnoreCase)
    {
        ["北京"] = 0.45, ["上海"] = 0.55, ["广州"] = 0.50, ["深圳"] = 0.75,
        ["天津"] = 0.50, ["重庆"] = 0.40, ["武汉"] = 0.35, ["成都"] = 0.30,
        ["南京"] = 0.40, ["杭州"] = 0.45, ["西安"] = 0.35, ["沈阳"] = 0.55,
        ["大连"] = 0.65, ["青岛"] = 0.60, ["厦门"] = 0.80, ["福州"] = 0.70,
        ["长沙"] = 0.35, ["郑州"] = 0.40, ["哈尔滨"] = 0.55, ["昆明"] = 0.30,
        ["济南"] = 0.40, ["石家庄"] = 0.35, ["合肥"] = 0.35, ["南昌"] = 0.45,
        ["贵阳"] = 0.35, ["太原"] = 0.40, ["兰州"] = 0.35, ["海口"] = 0.85,
        ["呼和浩特"] = 0.55, ["乌鲁木齐"] = 0.45, ["拉萨"] = 0.30,
    };

    private static readonly Dictionary<string, (double[] heights, double[] values)> HeightCoeffTable = new()
    {
        ["A"] = ([5,10,15,20,30,40,50,60,70,80,90,100,150,200,250,300,350,400,450],
                 [1.17,1.38,1.52,1.63,1.80,1.92,2.03,2.12,2.20,2.27,2.34,2.40,2.64,2.83,2.99,3.12,3.24,3.35,3.45]),
        ["B"] = ([5,10,15,20,30,40,50,60,70,80,90,100,150,200,250,300,350,400,450],
                 [0.80,1.00,1.14,1.25,1.42,1.56,1.67,1.77,1.86,1.93,2.00,2.07,2.33,2.53,2.70,2.84,2.97,3.08,3.19]),
        ["C"] = ([5,10,15,20,30,40,50,60,70,80,90,100,150,200,250,300,350,400,450],
                 [0.54,0.54,0.74,0.84,1.00,1.13,1.25,1.35,1.44,1.52,1.60,1.67,1.95,2.17,2.35,2.51,2.64,2.76,2.88]),
        ["D"] = ([5,10,15,20,30,40,50,60,70,80,90,100,150,200,250,300,350,400,450],
                 [0.30,0.30,0.30,0.45,0.61,0.74,0.86,0.96,1.06,1.14,1.22,1.30,1.61,1.85,2.06,2.23,2.38,2.52,2.64]),
    };

    // βgz 阵风系数表 (GB50009 表8.6.1)
    private static readonly Dictionary<string, (double[] heights, double[] values)> GustFactorTable = new()
    {
        ["A"] = ([5,10,20,40,60,100,200,300,450], [1.69,1.57,1.47,1.39,1.35,1.30,1.25,1.22,1.19]),
        ["B"] = ([5,10,20,40,60,100,200,300,450], [1.95,1.81,1.68,1.58,1.52,1.46,1.38,1.34,1.30]),
        ["C"] = ([5,10,20,40,60,100,200,300,450], [2.38,2.38,2.05,1.89,1.81,1.72,1.60,1.54,1.48]),
        ["D"] = ([5,10,20,40,60,100,200,300,450], [2.80,2.80,2.80,2.29,2.14,1.99,1.82,1.73,1.65]),
    };

    public double GetBasicWindPressure(string city, int returnPeriod = 50)
    {
        if (CityWindPressure.TryGetValue(city, out var w0))
        {
            if (returnPeriod == 100) return w0 * 1.1;
            return w0;
        }
        return -1;
    }

    public double GetGustFactor(double height, string terrainCategory)
    {
        var cat = terrainCategory.ToUpper();
        if (!GustFactorTable.TryGetValue(cat, out var table)) table = GustFactorTable["B"];
        return Interpolate(table.heights, table.values, height);
    }

    public double GetHeightFactor(double height, string terrainCategory)
    {
        var cat = terrainCategory.ToUpper();
        if (!HeightCoeffTable.TryGetValue(cat, out var table)) table = HeightCoeffTable["B"];
        return Interpolate(table.heights, table.values, height);
    }

    public LocalShapeCoeffResult GetLocalShapeCoefficient(LocalShapeCoeffInput input)
    {
        double baseCoeff = (input.SurfaceType, input.ZoneType) switch
        {
            (SurfaceType.Wall, ZoneType.Corner) => -2.0,
            (SurfaceType.Wall, ZoneType.Edge) => -1.8,
            (SurfaceType.Wall, _) => -1.2,
            (SurfaceType.FlatRoof, ZoneType.Corner) => -2.2,
            (SurfaceType.FlatRoof, ZoneType.Edge) => -2.0,
            (SurfaceType.FlatRoof, _) => -1.4,
            _ => -1.2,
        };

        double reduced = baseCoeff * AreaReductionFactor(input.TributaryArea);
        double musi = GetInternalPressureCoefficient(input.OpeningType);
        double musl = Math.Abs(reduced) + Math.Abs(musi);

        return new LocalShapeCoeffResult
        {
            BaseCoeff = baseCoeff,
            AreaReducedCoeff = reduced,
            Muse = reduced,
            Musi = musi,
            Musl = musl,
            Description = $"国标体型系数: 基础={baseCoeff}, 面积折减后={reduced:F3}, 内压={musi:F2}, μsl={musl:F3}",
            StandardClause = "GB50009-2012 表8.3.1"
        };
    }

    public double GetInternalPressureCoefficient(BuildingOpeningType openingType)
    {
        return openingType switch
        {
            BuildingOpeningType.Closed => 0.2,
            BuildingOpeningType.PartiallyClosed => 0.5,
            BuildingOpeningType.Open => 0.0,
            BuildingOpeningType.DominantOpening => 0.6,
            _ => 0.2,
        };
    }

    public WindLoadResult CalculateWindLoad(WindLoadInput input)
    {
        var w0 = input.BasicWindPressure > 0 ? input.BasicWindPressure : GetBasicWindPressure(input.City, input.ReturnPeriod);
        if (w0 < 0) throw new ArgumentException($"未找到城市 {input.City} 的风压数据");

        var betaGz = GetGustFactor(input.Height, input.TerrainCategory);
        var muZ = GetHeightFactor(input.Height, input.TerrainCategory);

        double panelMusl, structMusl;
        var steps = new List<WindLoadCalcStep>();

        if (input.ShapeCoeffInput != null)
        {
            var r = GetLocalShapeCoefficient(input.ShapeCoeffInput);
            panelMusl = r.Musl;
        }
        else
        {
            panelMusl = input.DirectShapeCoeff ?? 1.4;
        }
        structMusl = panelMusl * AreaReductionFactor(input.StructureArea);

        var panelWk = betaGz * panelMusl * muZ * w0;
        var structWk = betaGz * structMusl * muZ * w0;

        steps.Add(new WindLoadCalcStep { StepNo = 1, Name = "基本风压", Formula = "w0", Process = $"查表得 w0={w0} kN/m²", Result = $"{w0:F2} kN/m²", Clause = "GB50009 附录E" });
        steps.Add(new WindLoadCalcStep { StepNo = 2, Name = "风压高度变化系数", Formula = "μz", Process = $"z={input.Height}m, {input.TerrainCategory}类", Result = $"{muZ:F4}", Clause = "GB50009 表8.2.1" });
        steps.Add(new WindLoadCalcStep { StepNo = 3, Name = "阵风系数", Formula = "βgz", Process = $"z={input.Height}m, {input.TerrainCategory}类", Result = $"{betaGz:F4}", Clause = "GB50009 表8.6.1" });
        steps.Add(new WindLoadCalcStep { StepNo = 4, Name = "风荷载标准值", Formula = "wk = βgz·μsl·μz·w0", Process = $"{betaGz:F4}×{panelMusl:F4}×{muZ:F4}×{w0:F2}", Result = $"{panelWk:F4} kN/m²", Clause = "GB50009 §8.1.1" });

        return new WindLoadResult
        {
            W0 = w0, MuZ = Math.Round(muZ, 4), BetaGz = Math.Round(betaGz, 4),
            PanelShapeCoeff = Math.Round(panelMusl, 4), StructureShapeCoeff = Math.Round(structMusl, 4),
            InternalPressureCoeff = GetInternalPressureCoefficient(input.OpeningType),
            PanelWindLoad = Math.Round(panelWk, 4), StructureWindLoad = Math.Round(structWk, 4),
            Standard = Code, Steps = steps
        };
    }

    public List<CityWindPressureInfo> GetCityList()
    {
        return CityWindPressure.Select(kv => new CityWindPressureInfo
        {
            City = kv.Key, W0_50 = kv.Value, W0_100 = Math.Round(kv.Value * 1.1, 2)
        }).ToList();
    }

    internal static double AreaReductionFactor(double area)
    {
        if (area <= 1) return 1.0;
        if (area <= 2) return 0.9 + 0.1 * (2 - area);
        if (area <= 5) return 0.8 + 0.1 * (5 - area) / 3;
        if (area <= 10) return 0.7 + 0.1 * (10 - area) / 5;
        if (area <= 25) return 0.6 + 0.1 * (25 - area) / 15;
        return 0.6;
    }

    internal static double Interpolate(double[] xs, double[] vs, double x)
    {
        if (x <= xs[0]) return vs[0];
        if (x >= xs[^1]) return vs[^1];
        for (int i = 0; i < xs.Length - 1; i++)
        {
            if (x <= xs[i + 1])
            {
                var t = (x - xs[i]) / (xs[i + 1] - xs[i]);
                return vs[i] + t * (vs[i + 1] - vs[i]);
            }
        }
        return vs[^1];
    }
}
