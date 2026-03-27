namespace GlassManagement.Api.Services.RegionalStandard;

/// <summary>
/// 江苏省标准 DB32/T 4065-2021 《建筑幕墙工程技术标准》
/// 
/// 框架实现 — 占位类，后续版本完善
/// 江苏省的主要特点:
/// 1. 沿海城市（南通、盐城、连云港等）风压较高
/// 2. 对台风多发地区有专门补充
/// </summary>
public class JiangsuStandard_DB32T4065 : IWindLoadStandard
{
    public string Code => "DB32/T4065-2021";
    public string Name => "江苏省标准 DB32/T 4065-2021";
    public string Region => "江苏省";
    public bool IsFullyImplemented => false;

    /// <summary>江苏省主要城市基本风压 (kN/m²)</summary>
    private static readonly Dictionary<string, double> _windData = new(StringComparer.OrdinalIgnoreCase)
    {
        ["南京"] = 0.40, ["无锡"] = 0.45, ["徐州"] = 0.35, ["常州"] = 0.45,
        ["苏州"] = 0.45, ["南通"] = 0.55, ["连云港"] = 0.55, ["淮安"] = 0.40,
        ["盐城"] = 0.55, ["扬州"] = 0.40, ["镇江"] = 0.40, ["泰州"] = 0.45,
        ["宿迁"] = 0.35,
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
        result.StandardClause = $"DB32/T4065-2021 (暂用国标方法) - {result.StandardClause}";
        return result;
    }

    public double GetInternalPressureCoefficient(BuildingOpeningType openingType)
        => _fallback.GetInternalPressureCoefficient(openingType);

    public WindLoadResult CalculateWindLoad(WindLoadInput input)
    {
        if (input.BasicWindPressure <= 0)
            input.BasicWindPressure = GetBasicWindPressure(input.City, input.ReturnPeriod);
        var result = _fallback.CalculateWindLoad(input);
        result.Standard = "DB32/T4065-2021 (框架实现，部分回退国标)";
        return result;
    }

    public List<CityWindPressureInfo> GetCityList()
    {
        return _windData.Select(kvp => new CityWindPressureInfo
        {
            City = kvp.Key,
            Prefecture = "江苏",
            W0_50 = kvp.Value,
            W0_100 = Math.Round(kvp.Value * 1.1, 2)
        }).OrderBy(c => c.City).ToList();
    }
}
