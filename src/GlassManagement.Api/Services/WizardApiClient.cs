using System.Net.Http.Json;
using System.Text.Json;

namespace GlassManagement.Api.Services;

/// <summary>向导API客户端 — 对齐原程序8步</summary>
public class WizardApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public WizardApiClient(HttpClient http) => _http = http;

    /// <summary>新8步步骤名映射</summary>
    private static readonly string[] StepNames =
    {
        "",                 // 0: unused
        "step1-basic",      // 1: 基本参数
        "step2-glass",      // 2: 玻璃配置与验算
        "step3-mullion",    // 3: 立柱计算
        "step4-transom",    // 4: 横梁计算
        "step5-connection", // 5: 连接件计算
        "step6-embedded",   // 6: 预埋件与其他
        "step7-summary",    // 7: 计算结果汇总
        "step8-report"      // 8: 计算书输出
    };

    public async Task<string> CreateSessionAsync()
    {
        var resp = await _http.PostAsync("/api/Wizard/sessions", null);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return json.GetProperty("sessionId").GetString() ?? "";
    }

    public async Task<JsonElement?> GetSessionAsync(string sessionId)
    {
        var resp = await _http.GetAsync($"/api/Wizard/sessions/{sessionId}");
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
    }

    public async Task<(bool Ok, string? Error, JsonElement? Data)> SubmitStepAsync(string sessionId, int step, object input)
    {
        if (step < 1 || step > 8) return (false, "无效步骤", null);

        var resp = await _http.PutAsJsonAsync($"/api/Wizard/sessions/{sessionId}/{StepNames[step]}", input, JsonOpts);
        if (resp.IsSuccessStatusCode)
        {
            var data = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
            return (true, null, data);
        }
        var err = await resp.Content.ReadAsStringAsync();
        return (false, err, null);
    }

    public async Task<byte[]?> DownloadAsync(string sessionId, string format)
    {
        var resp = await _http.GetAsync($"/api/Wizard/sessions/{sessionId}/download/{format}");
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadAsByteArrayAsync();
    }

    // Static Calc
    public async Task<JsonElement?> GetModelsAsync()
    {
        var resp = await _http.GetAsync("/api/StaticCalc/models");
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
    }

    public async Task<JsonElement?> CalcBeamAsync(object input)
    {
        var resp = await _http.PostAsJsonAsync("/api/StaticCalc/beam", input, JsonOpts);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
    }
}
