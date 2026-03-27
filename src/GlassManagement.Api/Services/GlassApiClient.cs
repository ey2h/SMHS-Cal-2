using System.Net.Http.Json;
using System.Text.Json;

namespace GlassManagement.Api.Services;

public class GlassApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public GlassApiClient(HttpClient http)
    {
        _http = http;
    }

    // 健康检查
    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            var resp = await _http.GetAsync("/api/ProjectParameter/Regions?keyword=北京");
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // 项目参数 - 地区搜索
    public async Task<List<RegionDto>> SearchRegionsAsync(string keyword = "")
    {
        return await GetAsync<List<RegionDto>>($"/api/ProjectParameter/Regions?keyword={Uri.EscapeDataString(keyword)}") ?? [];
    }

    // 风荷载计算
    public async Task<CalcResultDto> CalculateWindLoadAsync(object request)
    {
        return await PostAsync<CalcResultDto>("/api/ProjectParameter/WindLoad", request) ?? new();
    }

    // 玻璃统一计算
    public async Task<CalcResultDto> CalcUnifiedGlassAsync(object request)
    {
        return await PostAsync<CalcResultDto>("/api/GlassCalculation/UnifiedCalculate", request) ?? new();
    }

    // 铝合金立柱
    public async Task<CalcResultDto> CalcAluminumMullionAsync(object request)
    {
        return await PostAsync<CalcResultDto>("/api/AluminumCalculation/Mullion", request) ?? new();
    }

    // 铝合金横梁
    public async Task<CalcResultDto> CalcAluminumRafterAsync(object request)
    {
        return await PostAsync<CalcResultDto>("/api/AluminumCalculation/Rafter", request) ?? new();
    }

    // 铝板
    public async Task<CalcResultDto> CalcAluminumPlateAsync(object request)
    {
        return await PostAsync<CalcResultDto>("/api/AluminumPanelCalculation/Calculate", request) ?? new();
    }

    // GRC板
    public async Task<CalcResultDto> CalcGrcPanelAsync(object request)
    {
        return await PostAsync<CalcResultDto>("/api/GrcPanelCalculation/Calculate", request) ?? new();
    }

    // 石材
    public async Task<CalcResultDto> CalcStonePanelAsync(object request)
    {
        return await PostAsync<CalcResultDto>("/api/StoneCalculation/Panel", request) ?? new();
    }

    // 连接件
    public async Task<CalcResultDto> CalcConnectionAsync(object request)
    {
        return await PostAsync<CalcResultDto>("/api/ConnectionCalculation/Calculate", request) ?? new();
    }

    // 焊缝
    public async Task<CalcResultDto> CalcWeldAsync(object request)
    {
        return await PostAsync<CalcResultDto>("/api/WeldCalculation/Calculate", request) ?? new();
    }

    // 计算书生成
    public async Task<ReportResultDto> GenerateReportAsync(string type, object request)
    {
        return await PostAsync<ReportResultDto>($"/api/Report/{type}", request) ?? new();
    }

    // 数据管理 CRUD
    public async Task<List<JsonElement>> GetDataAsync(string resource)
    {
        return await GetAsync<List<JsonElement>>($"/api/{resource}") ?? [];
    }

    public async Task DeleteDataAsync(string resource, string id)
    {
        await _http.DeleteAsync($"/api/{resource}/{id}");
    }

    // 简图
    public async Task<string> DiagramMullionAsync(object request)
    {
        return await PostStringAsync("/api/Diagram/MullionBeam", request);
    }

    public async Task<string> DiagramTransomAsync(object request)
    {
        return await PostStringAsync("/api/Diagram/TransomBeam", request);
    }

    // ===== 材料数据库 =====
    public async Task<List<JsonElement>> GetRegionsAsync()
        => await GetListAsync("/api/MaterialDatabase/Regions");
    public async Task<List<JsonElement>> GetGlassTypesAsync()
        => await GetListAsync("/api/MaterialDatabase/GlassTypes");
    public async Task<List<JsonElement>> GetProfileColumnsAsync()
        => await GetListAsync("/api/MaterialDatabase/ProfileColumns");
    public async Task<List<JsonElement>> GetProfileBeamsAsync()
        => await GetListAsync("/api/MaterialDatabase/beams");
    public async Task<List<JsonElement>> GetBoltsAsync()
        => await GetListAsync("/api/MaterialDatabase/Bolts");
    public async Task<List<JsonElement>> GetSteelPipesAsync()
        => await GetListAsync("/api/MaterialDatabase/SteelPipes");
    public async Task<List<JsonElement>> GetAnchorMaterialsAsync()
        => await GetListAsync("/api/MaterialDatabase/AnchorMaterials");
    public async Task<List<JsonElement>> GetSteelAlumSteelColumnsAsync()
        => await GetListAsync("/api/MaterialDatabase/steel-alum-steel-columns");
    public async Task<List<JsonElement>> GetSteelAlumAluColumnsAsync()
        => await GetListAsync("/api/MaterialDatabase/steel-alum-alu-columns");
    public async Task<List<JsonElement>> GetSteelAlumSteelBeamsAsync()
        => await GetListAsync("/api/MaterialDatabase/steel-alum-steel-beams");
    public async Task<List<JsonElement>> GetSteelAlumAluBeamsAsync()
        => await GetListAsync("/api/MaterialDatabase/steel-alum-alu-beams");

    private async Task<List<JsonElement>> GetListAsync(string url)
    {
        try
        {
            return await _http.GetFromJsonAsync<List<JsonElement>>(url, JsonOpts) ?? new();
        }
        catch { return new(); }
    }

    // 通用
    private async Task<T?> GetAsync<T>(string url)
    {
        try
        {
            return await _http.GetFromJsonAsync<T>(url, JsonOpts);
        }
        catch { return default; }
    }

    public async Task<T?> PostAsync<T>(string url, object body)
    {
        var resp = await _http.PostAsJsonAsync(url, body, JsonOpts);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<T>(JsonOpts);
    }

    private async Task<string> PostStringAsync(string url, object body)
    {
        var resp = await _http.PostAsJsonAsync(url, body, JsonOpts);
        resp.EnsureSuccessStatusCode();
        var ct = resp.Content.Headers.ContentType?.MediaType ?? "";
        if (ct.Contains("json"))
            return await resp.Content.ReadAsStringAsync();
        return await resp.Content.ReadAsStringAsync();
    }
}

// DTOs
public class RegionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? CityName { get; set; }
    public double WindPressure50 { get; set; }
    public double WindPressure100 { get; set; }
    public int SeismicIntensity { get; set; }
    public double PeakAcceleration { get; set; }
    public double AlphaMax { get; set; }
    public double TemperatureDiff { get; set; }
    public double SnowPressure50 { get; set; }
    public double SnowPressure100 { get; set; }
    /// <summary>基本风压 w₀ (kN/m²) = WindPressure50 / 1000</summary>
    public double W0 => WindPressure50 / 1000.0;
}

public class CheckDto
{
    public double ActualValue { get; set; }
    public double AllowableValue { get; set; }
    public double UtilizationRatio { get; set; }
    public bool Passed { get; set; }
    public string Description { get; set; } = "";
}

public class StepDto
{
    public int StepNo { get; set; }
    public string Name { get; set; } = "";
    public string Result { get; set; } = "";
    public string? Formula { get; set; }
    public string? Detail { get; set; }
}

public class LayerResultDto
{
    public string Description { get; set; } = "";
    public double Stress { get; set; }
    public double AllowableStress { get; set; }
    public double Deflection { get; set; }
    public double LoadShareFactor { get; set; }
    public bool StressOk { get; set; }
    public bool DeflectionOk { get; set; }
}

public class CalcResultDto
{
    public bool Passed { get; set; }
    public CheckDto? StrengthCheck { get; set; }
    public CheckDto? DeflectionCheck { get; set; }
    public CheckDto? FlexuralCheck { get; set; }
    public CheckDto? ShearCheck { get; set; }
    public CheckDto? StabilityCheck { get; set; }
    public CheckDto? TransomAngleCheck { get; set; }
    public CheckDto? AngleMullionCheck { get; set; }
    public CheckDto? MullionStructureCheck { get; set; }
    public List<StepDto>? Steps { get; set; }
    public List<LayerResultDto>? LayerResults { get; set; }
    // 风荷载特有
    public double WkPanel { get; set; }
    public double WkStructure { get; set; }
    public double W0 { get; set; }
}

public class ReportResultDto
{
    public string? DownloadUrl { get; set; }
    public string? Message { get; set; }
}
