using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

using GlassManagement.Desktop.ViewModels;

namespace GlassManagement.Desktop.Services;

/// <summary>WebAPI客户端</summary>
public class ApiClient : IDisposable
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string BaseUrl { get; set; }

    public ApiClient(string baseUrl = "http://localhost:5188")
    {
        BaseUrl = baseUrl;
        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    // ===== 通用CRUD =====

    public async Task<List<T>> GetAllAsync<T>(string endpoint)
    {
        var resp = await _http.GetAsync($"/api/{endpoint}");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<T>>(JsonOpts) ?? new();
    }

    public async Task<T?> GetByIdAsync<T>(string endpoint, int id)
    {
        var resp = await _http.GetAsync($"/api/{endpoint}/{id}");
        if (!resp.IsSuccessStatusCode) return default;
        return await resp.Content.ReadFromJsonAsync<T>(JsonOpts);
    }

    public async Task<T?> CreateAsync<T>(string endpoint, T entity)
    {
        var resp = await _http.PostAsJsonAsync($"/api/{endpoint}", entity, JsonOpts);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<T>(JsonOpts);
    }

    public async Task UpdateAsync<T>(string endpoint, int id, T entity)
    {
        var resp = await _http.PutAsJsonAsync($"/api/{endpoint}/{id}", entity, JsonOpts);
        resp.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(string endpoint, int id)
    {
        var resp = await _http.DeleteAsync($"/api/{endpoint}/{id}");
        resp.EnsureSuccessStatusCode();
    }

    // ===== 计算API =====

    public async Task<TResult?> CalculateAsync<TRequest, TResult>(string endpoint, TRequest request)
    {
        var resp = await _http.PostAsJsonAsync($"/api/{endpoint}", request, JsonOpts);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<TResult>(JsonOpts);
    }

    // ===== 计算书API =====

    public async Task<ReportResult?> GenerateReportAsync<TRequest>(string reportType, TRequest request)
    {
        var resp = await _http.PostAsJsonAsync($"/api/report/{reportType}", request, JsonOpts);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ReportResult>(JsonOpts);
    }

    // ===== 连通性测试 =====

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var resp = await _http.GetAsync("/swagger/v1/swagger.json");
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ===== SVG获取 =====

    public async Task<string> GetSvgAsync(string endpoint, object request)
    {
        var resp = await _http.PostAsJsonAsync($"/api/{endpoint}", request, JsonOpts);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync();
    }

    // ===== 地区查询 =====

    public async Task<List<RegionInfo>> SearchRegionsAsync(string keyword)
    {
        var resp = await _http.GetAsync($"/api/ProjectParameter/Regions?keyword={Uri.EscapeDataString(keyword ?? "")}");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<RegionInfo>>(JsonOpts) ?? new();
    }

    // ===== 风荷载计算(项目参数) =====

    public async Task<WindLoadResult?> CalculateProjectWindLoad(object request)
    {
        var resp = await _http.PostAsJsonAsync("/api/ProjectParameter/WindLoad", request, JsonOpts);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<WindLoadResult>(JsonOpts);
    }

    public void Dispose() => _http.Dispose();
}

public class RegionInfo
{
    public string Name { get; set; } = "";
    /// <summary>50年基本风压 (Pa)</summary>
    public double WindPressure50 { get; set; }
    /// <summary>100年基本风压 (Pa)</summary>
    public double WindPressure100 { get; set; }
    /// <summary>抗震烈度</summary>
    public int SeismicIntensity { get; set; }
    /// <summary>基本地震动峰值加速度 (g)</summary>
    public double PeakAcceleration { get; set; }
    public double AlphaMax { get; set; }
    /// <summary>50年基本雪压 (Pa)</summary>
    public double SnowPressure50 { get; set; }
    public double TemperatureDiff { get; set; }
}

public class WindLoadResult
{
    public double MuZ { get; set; }
    public double BetaGz { get; set; }
    public double Beta { get; set; }
    public double Mus1Panel { get; set; }
    /// <summary>支承结构体型系数 (后端字段: mus1Structure)</summary>
    public double Mus1Structure { get; set; }
    /// <summary>面板风荷载标准值 wk (MPa)</summary>
    public double WkPanel { get; set; }
    /// <summary>支承结构风荷载标准值 wk (MPa)</summary>
    public double WkStructure { get; set; }
    public List<StepDto>? Steps { get; set; }
}

public class ReportResult
{
    public string ReportId { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public DateTime GeneratedAt { get; set; }
    public long FileSize { get; set; }
}
