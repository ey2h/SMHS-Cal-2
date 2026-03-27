namespace GlassManagement.Api.Services.RegionalStandard;

/// <summary>
/// 风荷载计算规范接口
/// 统一抽象国标与各地方规范的风荷载计算差异，
/// 使上层业务代码可透明切换不同规范进行计算。
/// </summary>
public interface IWindLoadStandard
{
    /// <summary>规范代号，如 "GB50009-2012"、"DBJ/T15-101-2022"</summary>
    string Code { get; }

    /// <summary>规范名称，如 "国家标准"、"广东省标准"</summary>
    string Name { get; }

    /// <summary>适用地区描述</summary>
    string Region { get; }

    /// <summary>是否为完整实现（false表示仅框架/占位）</summary>
    bool IsFullyImplemented { get; }

    /// <summary>
    /// 获取基本风压 w0 (kN/m²)
    /// </summary>
    /// <param name="city">城市/区县名称</param>
    /// <param name="returnPeriod">重现期（年），默认50</param>
    /// <returns>基本风压值 (kN/m²)；城市未找到时返回-1</returns>
    double GetBasicWindPressure(string city, int returnPeriod = 50);

    /// <summary>
    /// 获取阵风系数 βgz
    /// </summary>
    /// <param name="height">计算点标高 z (m)</param>
    /// <param name="terrainCategory">地面粗糙度类别 A/B/C/D</param>
    /// <returns>阵风系数</returns>
    double GetGustFactor(double height, string terrainCategory);

    /// <summary>
    /// 获取风压高度变化系数 μz
    /// </summary>
    /// <param name="height">计算点标高 z (m)</param>
    /// <param name="terrainCategory">地面粗糙度类别 A/B/C/D</param>
    /// <returns>风压高度变化系数</returns>
    double GetHeightFactor(double height, string terrainCategory);

    /// <summary>
    /// 获取围护结构局部体型系数
    /// </summary>
    /// <param name="input">体型系数计算输入参数</param>
    /// <returns>局部体型系数计算结果（含内外压分项）</returns>
    LocalShapeCoeffResult GetLocalShapeCoefficient(LocalShapeCoeffInput input);

    /// <summary>
    /// 获取内压系数
    /// </summary>
    /// <param name="openingType">建筑开洞类型</param>
    /// <returns>内压系数值</returns>
    double GetInternalPressureCoefficient(BuildingOpeningType openingType);

    /// <summary>
    /// 完整的风荷载标准值计算
    /// wk = βgz · μsl · μz · w0
    /// </summary>
    /// <param name="input">计算输入</param>
    /// <returns>完整计算结果（含步骤）</returns>
    WindLoadResult CalculateWindLoad(WindLoadInput input);

    /// <summary>
    /// 获取该规范下支持的城市列表及风压数据
    /// </summary>
    /// <returns>城市风压信息列表</returns>
    List<CityWindPressureInfo> GetCityList();
}
