using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GlassManagement.Calc.Api;

/// <summary>
/// 引擎反射分发器：把「计算类型名 + JSON 参数」映射到已复刻的计算引擎并执行。
///
/// <para><b>为什么用反射而不是手写 if/switch</b>：<c>GlassManagement.Calc</c> 里目前有 50+ 个引擎，
/// 且仍在按对拍进度增加。引擎约定统一为 <c>public static Result Compute(Input)</c>，
/// 所以扫描一次程序集就能全部暴露出去，新增引擎时本服务**无需改动**。</para>
///
/// <para><b>命名</b>：类型名 = 引擎类名去掉 <c>Engine</c> 后缀并转小写（<c>GlassEngine</c> → <c>glass</c>）；
/// 同时接受类全名（<c>GlassEngine</c>）和不区分大小写。重名时以别名表为准。</para>
/// </summary>
public sealed class CalcDispatcher
{
    /// <summary>一个可调用的计算入口。</summary>
    public sealed record Entry(
        string Key,
        string EngineName,
        Type InputType,
        Type ResultType,
        MethodInfo Compute);

    /// <summary>友好别名 → 引擎类名。给 Revit 端用短名，避免拼长类名。</summary>
    private static readonly Dictionary<string, string> Alias = new(StringComparer.OrdinalIgnoreCase)
    {
        ["wind"] = "WindLoadEngine",              // 风荷载
        ["combination"] = "LoadCombinationEngine", // 荷载组合
        ["glass"] = "GlassEngine",                // 玻璃面板
        ["glasspanel"] = "GlassPanelEngine",
        ["column"] = "ColumnSpanEngine",          // 立柱（单跨）
        ["doublespan"] = "DoubleSpanColumnEngine", // 立柱（双跨）
        ["indeterminate"] = "IndeterminateColumnEngine",
        ["beam"] = "HorizontalBeamEngine",        // 横梁
        ["unitbeam"] = "UnitBeamEngine",          // 单元板块横梁
        ["alloy"] = "AlloyColumnEngine",          // 铝合金立柱
        ["connect"] = "ConnectEngine",            // 连接件
        ["embed"] = "EmbedEngine",                // 埋件
        ["sealant"] = "SealantEngine",            // 胶类
        ["plate"] = "PlateEngine",                // 金属板
        ["stone"] = "StonePlateEngine",           // 石材
        ["manmade"] = "ManmadePlateEngine",       // 人造板
        ["backbolt"] = "BackboltPlateEngine",     // 背栓点挂
        ["fullglass"] = "FullGlassEngine",        // 全玻璃幕墙
        ["dianshi"] = "DianshiGlassEngine",       // 点式肋
        ["uglass"] = "UGlassEngine",
        ["floorglass"] = "FloorGlassEngine",
        ["skylight"] = "SkylightEngine",          // 采光顶
        ["awning"] = "AwningEngine",              // 雨篷
        ["thermalglass"] = "ThermalGlassEngine",  // 透明幕墙热工
        ["thermalwall"] = "ThermalWallEngine",    // 非透明幕墙热工
        ["shading"] = "ExternalShadingEngine",    // 外遮阳
        ["multispan"] = "MultiSpanStaticEngine",  // 多跨静力
        ["weld"] = "WeldEngine",
        ["boltgroup"] = "BoltGroupEngine",
        ["anchorplate"] = "AnchorPlateEngine",
        ["rebar"] = "RebarAnchorEngine",
        ["pinbolt"] = "PinBoltEngine",
        ["thread"] = "ThreadEngine",
        ["screw"] = "SelfTappingScrewEngine",
        ["smoke"] = "SmokeVentEngine",
        ["opening"] = "OpeningVentEngine",
        ["louver"] = "LouverVentEngine",
        ["weight"] = "WeightPerMeterEngine",
        ["thermalstress"] = "ThermalStressEngine",
        ["expansion"] = "ThermalExpansionEngine",
        ["multipointlock"] = "MultiPointLockEngine",
        ["cornercolumn"] = "CornerColumnEngine",
    };

    private readonly Dictionary<string, Entry> _byKey = new(StringComparer.OrdinalIgnoreCase);

    public CalcDispatcher()
    {
        var asm = typeof(Engines.GlassEngine).Assembly;

        foreach (var type in asm.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract) continue;

            // 约定：public static Result Compute(Input)，且 Input 是该引擎自己的嵌套类型。
            var compute = type
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m =>
                {
                    if (m.Name != "Compute") return false;
                    var ps = m.GetParameters();
                    if (ps.Length != 1) return false;
                    var p = ps[0].ParameterType;
                    return p.IsClass && p.DeclaringType == type;
                });
            if (compute is null) continue;

            var inputType = compute.GetParameters()[0].ParameterType;
            var key = type.Name.EndsWith("Engine", StringComparison.Ordinal)
                ? type.Name[..^"Engine".Length].ToLowerInvariant()
                : type.Name.ToLowerInvariant();

            var entry = new Entry(key, type.Name, inputType, compute.ReturnType, compute);
            _byKey[key] = entry;
            _byKey[type.Name] = entry;   // 同时按类全名注册，便于排查
        }

        // 别名覆盖（短名优先）
        foreach (var (k, engineName) in Alias)
        {
            if (_byKey.TryGetValue(engineName, out var e))
            {
                _byKey[k] = e with { Key = k };
            }
        }
    }

    /// <summary>全部可调用的计算类型（供 Revit 端自省）。</summary>
    public IEnumerable<object> Catalog() =>
        _byKey.Values
            .DistinctBy(e => e.EngineName)
            .OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
            .Select(e => new
            {
                type = e.Key,
                engine = e.EngineName,
                input = e.InputType.FullName,
                fields = e.InputType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Select(f => new
                    {
                        name = f.Name,
                        type = f.FieldType.Name,
                        @default = f.GetValue(Activator.CreateInstance(e.InputType)),
                    })
                    .ToArray(),
            });

    /// <summary>该类型是否可算（宿主启动自检用）。</summary>
    public bool Supports(string type) => type is not null && _byKey.ContainsKey(type);

    /// <summary>按类型名 + JSON 参数执行计算。</summary>
    public CalcResponse Run(string type, JsonElement? payload, JsonSerializerOptions json)
    {
        if (!_byKey.TryGetValue(type ?? "", out var entry))
        {
            return CalcResponse.Fail($"未知计算类型「{type}」。可调用 GET /calc/types 查看全部类型。");
        }

        object input;
        try
        {
            // 允许空 body：全部字段用引擎默认值（= V31 取证算例）
            input = payload is null || payload.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                ? Activator.CreateInstance(entry.InputType)!
                : payload.Value.Deserialize(entry.InputType, json)!;
        }
        catch (JsonException ex)
        {
            return CalcResponse.Fail($"参数解析失败：{ex.Message}");
        }

        try
        {
            var result = entry.Compute.Invoke(null, new[] { input });
            return new CalcResponse
            {
                Ok = true,
                Type = entry.Key,
                Engine = entry.EngineName,
                Result = result,
                Input = input,
            };
        }
        catch (TargetInvocationException ex)
        {
            var inner = ex.InnerException ?? ex;
            return CalcResponse.Fail($"{entry.EngineName} 计算异常：{inner.GetType().Name}: {inner.Message}");
        }
    }
}

/// <summary>统一响应。成功时 <see cref="Ok"/>=true 并带 result/input，失败时只有 error。</summary>
public sealed class CalcResponse
{
    public bool Ok { get; set; }
    public string? Type { get; set; }
    public string? Engine { get; set; }
    public object? Result { get; set; }
    public object? Input { get; set; }
    public string? Error { get; set; }

    public static CalcResponse Fail(string msg) => new() { Ok = false, Error = msg };
}
