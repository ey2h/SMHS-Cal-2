namespace GlassManagement.Api.Services.RegionalStandard;

/// <summary>
/// 风荷载计算规范工厂
/// 根据规范代号获取对应的计算实现，支持动态切换。
/// 
/// 已注册规范:
///   GB50009-2012     — 国家标准（完整实现）
///   DBJ/T15-101-2022 — 广东省标准（完整实现）
///   DGJ08-56-2012    — 上海市标准（框架）
///   DB32/T4065-2021  — 江苏省标准（框架）
///   DBJ61/T161-2019  — 陕西省标准（框架）
/// </summary>
public class WindLoadStandardFactory
{
    /// <summary>已注册的规范实例（线程安全，初始化后只读）</summary>
    private readonly Dictionary<string, IWindLoadStandard> _standards;

    /// <summary>规范代号别名映射（用于模糊匹配）</summary>
    private static readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        // 国标别名
        ["GB50009"] = "GB50009-2012",
        ["GB50009-2012"] = "GB50009-2012",
        ["国标"] = "GB50009-2012",
        ["国家标准"] = "GB50009-2012",
        ["GB55001"] = "GB50009-2012",    // GB55001也路由到同一实现

        // 广东省
        ["DBJ/T15-101"] = "DBJ/T15-101-2022",
        ["DBJ/T15-101-2022"] = "DBJ/T15-101-2022",
        ["DBJT15101"] = "DBJ/T15-101-2022",
        ["广东"] = "DBJ/T15-101-2022",
        ["广东省"] = "DBJ/T15-101-2022",
        ["广东省标准"] = "DBJ/T15-101-2022",

        // 上海市
        ["DGJ08-56"] = "DGJ08-56-2012",
        ["DGJ08-56-2012"] = "DGJ08-56-2012",
        ["DGJ0856"] = "DGJ08-56-2012",
        ["上海"] = "DGJ08-56-2012",
        ["上海市"] = "DGJ08-56-2012",

        // 江苏省
        ["DB32/T4065"] = "DB32/T4065-2021",
        ["DB32/T4065-2021"] = "DB32/T4065-2021",
        ["DB32T4065"] = "DB32/T4065-2021",
        ["江苏"] = "DB32/T4065-2021",
        ["江苏省"] = "DB32/T4065-2021",

        // 陕西省
        ["DBJ61/T161"] = "DBJ61/T161-2019",
        ["DBJ61/T161-2019"] = "DBJ61/T161-2019",
        ["DBJ61T161"] = "DBJ61/T161-2019",
        ["陕西"] = "DBJ61/T161-2019",
        ["陕西省"] = "DBJ61/T161-2019",
    };

    public WindLoadStandardFactory()
    {
        var standards = new IWindLoadStandard[]
        {
            new NationalStandard_GB50009(),
            new GuangdongStandard_DBJT15101(),
            new ShanghaiStandard_DGJ0856(),
            new JiangsuStandard_DB32T4065(),
            new ShaanxiStandard_DBJ61T161(),
        };

        _standards = new Dictionary<string, IWindLoadStandard>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in standards)
        {
            _standards[s.Code] = s;
        }
    }

    /// <summary>
    /// 根据规范代号或别名获取规范实例
    /// </summary>
    /// <param name="code">规范代号、别名或地区名称</param>
    /// <returns>规范实例；未找到时返回null</returns>
    public IWindLoadStandard? GetStandard(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        var trimmed = code.Trim();

        // 直接匹配
        if (_standards.TryGetValue(trimmed, out var std))
            return std;

        // 别名匹配
        if (_aliases.TryGetValue(trimmed, out var realCode))
        {
            if (_standards.TryGetValue(realCode, out var aliasStd))
                return aliasStd;
        }

        // 模糊匹配（包含关系）
        foreach (var alias in _aliases)
        {
            if (alias.Key.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains(alias.Key, StringComparison.OrdinalIgnoreCase))
            {
                if (_standards.TryGetValue(alias.Value, out var fuzzyStd))
                    return fuzzyStd;
            }
        }

        return null;
    }

    /// <summary>
    /// 获取默认规范（国标）
    /// </summary>
    public IWindLoadStandard GetDefault() => _standards["GB50009-2012"];

    /// <summary>
    /// 获取所有可用规范列表
    /// </summary>
    public List<StandardInfo> GetAvailableStandards()
    {
        return _standards.Values.Select(s => new StandardInfo
        {
            Code = s.Code,
            Name = s.Name,
            Region = s.Region,
            Year = ExtractYear(s.Code),
            IsFullyImplemented = s.IsFullyImplemented
        }).OrderByDescending(s => s.IsFullyImplemented)
          .ThenBy(s => s.Code)
          .ToList();
    }

    /// <summary>从规范代号中提取年份</summary>
    private static int ExtractYear(string code)
    {
        // 匹配最后的4位数字
        var match = System.Text.RegularExpressions.Regex.Match(code, @"(\d{4})");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }
}
