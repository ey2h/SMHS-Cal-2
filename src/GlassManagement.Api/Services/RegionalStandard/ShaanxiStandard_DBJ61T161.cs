namespace GlassManagement.Api.Services.RegionalStandard;

/// <summary>
/// 陕西省标准 DBJ 61/T 161-2019 《建筑幕墙工程技术规范》
/// 
/// 框架实现 — 占位类，后续版本完善
/// 陕西省的主要特点:
/// 1. 关中平原与陕南、陕北风压差异较大
/// 2. 高海拔地区有修正
/// </summary>
public class ShaanxiStandard_DBJ61T161 : IWindLoadStandard
{
    public string Code => "DBJ61/T161-2019";
    public string Name => "陕西省标准 DBJ 61/T 161-2019";
    public string Region => "陕西省";
    public bool IsFullyImplemented => false;

    /// <summary>陕西省主要城市基本风压 (kN/m²)</summary>
    private static readonly Dictionary<string, double> _windData = new(StringComparer.OrdinalIgnoreCase)
    {
        ["西安"] = 0.35, ["咸阳"] = 0.35, ["宝鸡"] = 0.30, ["渭南"] = 0.35,
        ["铜川"] = 0.35, ["延安"] = 0.35, ["榆林"] = 0.45, ["汉中"] = 0.25,
        ["安康"] = 0.30, ["商洛"] = 0.30,
    };

    private readonly NationalStandard_GB50009 _fallback = new();

    public double GetBasicWindPressure(string city, int returnPeriod = 50)
    {
        var trimmed = city?.Trim().TrimEnd('市', '区', '县') ?? "";
        if (_windData.TryGetValue(trimmed, out var w))
            return returnPeriod >= 100 ? w * 1.1 : w;
        return _fallback.GetBasicWindPressure(city ?? "", returnPeriod);
    }

    public double GetGustFactor(double height, string terrainCategory)
        => _fallback.GetGustFactor(height, terrainCategory);

    public double GetHeightFactor(double height, string terrainCategory)
        => _fallback.GetHeightFactor(height, terrainCategory);

    public LocalShapeCoeffResult GetLocalShapeCoefficient(LocalShapeCoeffInput input)
    {
        var result = _fallback.GetLocalShapeCoefficient(input);
        result.StandardClause = $"DBJ61/T161-2019 (暂用国标方法) - {result.StandardClause}";
        return result;
    }

    public double GetInternalPressureCoefficient(BuildingOpeningType openingType)
        => _fallback.GetInternalPressureCoefficient(openingType);

    public WindLoadResult CalculateWindLoad(WindLoadInput input)
    {
        if (input.BasicWindPressure <= 0)
            input.BasicWindPressure = GetBasicWindPressure(input.City, input.ReturnPeriod);
        var result = _fallback.CalculateWindLoad(input);
        result.Standard = "DBJ61/T161-2019 (框架实现，部分回退国标)";
        return result;
    }

    public List<CityWindPressureInfo> GetCityList()
    {
        return _windData.Select(kvp => new CityWindPressureInfo
        {
            City = kvp.Key,
            Prefecture = "陕西",
            W0_50 = kvp.Value,
            W0_100 = Math.Round(kvp.Value * 1.1, 2)
        }).OrderBy(c => c.City).ToList();
    }
}
