namespace GlassManagement.Api.Services.RegionalStandard;

/// <summary>
/// 上海市标准 DGJ08-56-2012 《建筑幕墙工程技术规范》
/// 
/// 框架实现 — 占位类，后续版本完善
/// 上海市的主要特点:
/// 1. 基本风压按上海市行政区划细化
/// 2. 沿海/内陆区分更明确
/// 3. 对超高层有专门补充规定
/// </summary>
public class ShanghaiStandard_DGJ0856 : IWindLoadStandard
{
    public string Code => "DGJ08-56-2012";
    public string Name => "上海市标准 DGJ08-56-2012";
    public string Region => "上海市";
    public bool IsFullyImplemented => false;

    /// <summary>上海市基本风压</summary>
    private static readonly Dictionary<string, double> _windData = new(StringComparer.OrdinalIgnoreCase)
    {
        ["上海"] = 0.55, ["浦东"] = 0.55, ["黄浦"] = 0.55, ["静安"] = 0.55,
        ["徐汇"] = 0.55, ["长宁"] = 0.55, ["普陀"] = 0.55, ["虹口"] = 0.55,
        ["杨浦"] = 0.55, ["闵行"] = 0.55, ["宝山"] = 0.55, ["嘉定"] = 0.55,
        ["金山"] = 0.55, ["松江"] = 0.55, ["青浦"] = 0.55, ["奉贤"] = 0.55,
        ["崇明"] = 0.60, // 崇明岛风压偏大
    };

    private readonly NationalStandard_GB50009 _fallback = new();

    public double GetBasicWindPressure(string city, int returnPeriod = 50)
    {
        var trimmed = city?.Trim().TrimEnd('市', '区', '县') ?? "";
        if (_windData.TryGetValue(trimmed, out var w))
            return returnPeriod >= 100 ? w * 1.1 : w;
        // 回退到国标
        return _fallback.GetBasicWindPressure(city ?? "", returnPeriod);
    }

    // 以下方法回退到国标实现
    public double GetGustFactor(double height, string terrainCategory)
        => _fallback.GetGustFactor(height, terrainCategory);

    public double GetHeightFactor(double height, string terrainCategory)
        => _fallback.GetHeightFactor(height, terrainCategory);

    public LocalShapeCoeffResult GetLocalShapeCoefficient(LocalShapeCoeffInput input)
    {
        var result = _fallback.GetLocalShapeCoefficient(input);
        result.StandardClause = $"DGJ08-56-2012 (暂用国标GB50009方法) - {result.StandardClause}";
        return result;
    }

    public double GetInternalPressureCoefficient(BuildingOpeningType openingType)
        => _fallback.GetInternalPressureCoefficient(openingType);

    public WindLoadResult CalculateWindLoad(WindLoadInput input)
    {
        // 替换风压数据后使用国标计算方法
        if (input.BasicWindPressure <= 0)
            input.BasicWindPressure = GetBasicWindPressure(input.City, input.ReturnPeriod);

        var result = _fallback.CalculateWindLoad(input);
        result.Standard = "DGJ08-56-2012 (框架实现，部分回退国标)";
        return result;
    }

    public List<CityWindPressureInfo> GetCityList()
    {
        return _windData.Select(kvp => new CityWindPressureInfo
        {
            City = kvp.Key,
            Prefecture = "上海",
            W0_50 = kvp.Value,
            W0_100 = Math.Round(kvp.Value * 1.1, 2)
        }).OrderBy(c => c.City).ToList();
    }
}
