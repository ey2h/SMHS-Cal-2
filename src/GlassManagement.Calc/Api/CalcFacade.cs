using System.Text.Json;
using System.Text.Json.Serialization;

namespace GlassManagement.Calc.Api;

/// <summary>
/// 计算引擎对外门面 —— Revit / Rhino / 其它宿主的唯一入口。
///
/// <para><b>两种接法</b>：</para>
/// <list type="number">
///   <item><b>进程内直接引用</b>（推荐，零延迟）：宿主的 .NET 版本与 net10.0 兼容时，
///     直接引用 <c>GlassManagement.Calc.dll</c>，调 <see cref="Run(string, string?)"/>。</item>
///   <item><b>跨进程 JSON</b>（宿主是 .NET Framework 4.8 之类老运行时，或非 .NET 语言）：
///     用 <c>GlassManagement.Calc.Demo.exe --calc-api</c> 做管道/文件调用，
///     协议就是下面这套 JSON，与本类完全一致。</item>
/// </list>
///
/// <para><b>为什么不暴露强类型 API</b>：引擎有 50+ 个，Input 结构各不相同且随对拍进度增加。
/// 用「类型名 + 同名字段 JSON」可以零维护地把新引擎暴露出去，宿主侧也不必为每类构件写 DTO。
/// 需要强类型时，宿主按 <see cref="Catalog"/> 返回的字段清单自己生成 DTO 即可。</para>
/// </summary>
public static class CalcFacade
{
    private static readonly CalcDispatcher Dispatcher = new();

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // ★ 引擎的 Input/Result 全是 public 字段（不是属性），不开这个开关会序列化成 {}
        IncludeFields = true,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>全部可计算的构件类型 + 每个类型的输入字段与默认值（JSON 数组）。</summary>
    public static string Catalog()
        => JsonSerializer.Serialize(Dispatcher.Catalog(), Json);

    /// <summary>
    /// 执行一次计算。
    /// </summary>
    /// <param name="type">构件类型，如 <c>glass</c> / <c>column</c> / <c>wind</c>；
    /// 也接受引擎类名如 <c>GlassEngine</c>。</param>
    /// <param name="inputJson">参数 JSON；null 或空串表示全部使用引擎默认值（即 V31 取证算例）。</param>
    /// <returns>结果 JSON：<c>{ ok, type, engine, result, input }</c> 或 <c>{ ok:false, error }</c>。</returns>
    public static string Run(string type, string? inputJson)
    {
        JsonElement? payload = null;
        if (!string.IsNullOrWhiteSpace(inputJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(inputJson);
                payload = doc.RootElement.Clone();
            }
            catch (JsonException ex)
            {
                return JsonSerializer.Serialize(
                    CalcResponse.Fail($"请求 JSON 格式错误：{ex.Message}"), Json);
            }
        }

        var r = Dispatcher.Run(type, payload, Json);
        return JsonSerializer.Serialize(r, Json);
    }

    /// <summary>批量计算：<c>[{ "type": "...", "params": { ... } }, ...]</c>。</summary>
    public static string RunBatch(string batchJson)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(batchJson);
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(CalcResponse.Fail($"批量请求 JSON 格式错误：{ex.Message}"), Json);
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return JsonSerializer.Serialize(
                    CalcResponse.Fail("批量请求必须是数组：[{type, params}, ...]"), Json);
            }

            var items = new List<object>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var type = item.TryGetProperty("type", out var t) ? t.GetString() : null;
                JsonElement? p = item.TryGetProperty("params", out var pp) ? pp.Clone() : null;
                var r = Dispatcher.Run(type ?? "", p, Json);
                items.Add(new { type, ok = r.Ok, engine = r.Engine, result = r.Result, error = r.Error });
            }

            return JsonSerializer.Serialize(new { ok = true, count = items.Count, items }, Json);
        }
    }

    /// <summary>该类型是否可算（宿主启动时自检用）。</summary>
    public static bool Supports(string type)
        => Dispatcher.Supports(type);
}
