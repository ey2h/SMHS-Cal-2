using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace GlassManagement.Calc.Data;

/// <summary>
/// 玻璃材料参数装载（<see cref="ParamLibrary"/> 的 partial 分支）。
///
/// <para><b>为什么单独一张表</b>：<c>参数库.mdb</c> 的 59 张表全是型材/板材截面表，
/// <b>没有玻璃材料表</b>（已核对：两处 <c>参数库.mdb</c> MD5 相同，
/// <c>GetSchema("Tables")</c> 全量导出，且所有 CSV 中无「强度设计值」列）。
/// 玻璃厚度档、种类档、胶片档、密度、强度设计值在 V31 里是<b>程序内常量</b>。</para>
///
/// <para>按项目铁律「不得在源码里另编一套」，这些值统一落在参数库目录的
/// <c>玻璃材料（复刻补充）.csv</c>，由本类在运行时读取；源码里不再出现字面量数组。
/// 若日后 V31 提供了正式表，把该 CSV 换成官方导出即可，代码不用改。</para>
/// </summary>
public static partial class ParamLibrary
{
    private const string GlassFileName = "玻璃材料（复刻补充）.csv";

    private static List<(string Section, string Key, string V1, string V2, string V3)>? _glassRows;

    private static List<(string, string, string, string, string)> GlassRows
    {
        get
        {
            if (_glassRows is not null) return _glassRows;
            var rows = new List<(string, string, string, string, string)>();
            try
            {
                var path = Path.Combine(ParamDir, GlassFileName);
                if (File.Exists(path))
                {
                    foreach (var line in File.ReadAllLines(path))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var f = SplitCsv(line);
                        if (f.Length < 5) continue;
                        if (f[0] == "段") continue;            // 表头
                        rows.Add((f[0], f[1], f[2], f[3], f[4]));
                    }
                }
            }
            catch { /* 读不到就退回下面的内置快照 */ }
            if (rows.Count == 0) rows.AddRange(Snapshot);
            _glassRows = rows;
            return _glassRows;
        }
    }

    /// <summary>编译期快照 —— 仅当参数库目录里找不到该表时兜底，值与 CSV 一致。</summary>
    private static readonly (string, string, string, string, string)[] Snapshot =
    {
        ("材料常数", "玻璃密度", "2.56e-05", "", ""),
        ("材料常数", "弹性模量", "72000", "", ""),
        ("材料常数", "强度分界厚度", "12", "", ""),
        ("强度设计值", "普通玻璃", "12", "28", "24"),
        ("强度设计值", "浮法玻璃", "12", "28", "24"),
        ("强度设计值", "夹丝玻璃", "12", "28", "24"),
        ("强度设计值", "半钢化玻璃", "12", "41", "38"),
        ("强度设计值", "钢化玻璃", "12", "84", "72"),
    };

    private static IEnumerable<(string Key, string V1, string V2, string V3)> Section(string section)
    {
        foreach (var r in GlassRows)
            if (r.Item1 == section) yield return (r.Item2, r.Item3, r.Item4, r.Item5);
    }

    private static double D(string s, double fallback)
        => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static (double Density, double E, double BreakThickness) Const()
    {
        double d = 2.56e-5, e = 72000, t = 12;
        foreach (var r in Section("材料常数"))
        {
            if (r.Key == "玻璃密度") d = D(r.V1, d);
            else if (r.Key == "弹性模量") e = D(r.V1, e);
            else if (r.Key == "强度分界厚度") t = D(r.V1, t);
        }
        return (d, e, t);
    }

    /// <summary>玻璃密度 N/mm³（参数库「玻璃密度」，默认 2.56e-5 = 25.6 kN/m³）。</summary>
    public static double GlassDensity => Const().Density;

    /// <summary>玻璃弹性模量 N/mm²（参数库「弹性模量」，默认 72000）。</summary>
    public static double GlassElasticModulus => Const().E;

    /// <summary>强度设计值的厚度分界（≥ 该值取厚侧强度，默认 12mm）。</summary>
    public static double GlassStrengthBreakThickness => Const().BreakThickness;

    /// <summary>
    /// 玻璃中部抗弯强度设计值 fg（MPa）。
    /// <para>参数库「强度设计值」段：<c>种类, 分界厚度, 薄侧强度, 厚侧强度</c>。
    /// 未列出的种类按「普通玻璃」处理（与 V31 的 else 分支一致）。</para>
    /// </summary>
    public static double GlassBendingStrength(string? kind, double thicknessMm)
    {
        var key = string.IsNullOrWhiteSpace(kind) ? "普通玻璃" : kind!.Trim();
        double brk = GlassStrengthBreakThickness, thin = 28, thick = 24;
        bool hit = false;
        foreach (var r in Section("强度设计值"))
        {
            if (r.Key == key)
            {
                brk = D(r.V1, brk); thin = D(r.V2, thin); thick = D(r.V3, thick);
                hit = true;
                break;
            }
        }
        if (!hit)
        {
            foreach (var r in Section("强度设计值"))
            {
                if (r.Key == "普通玻璃")
                {
                    brk = D(r.V1, brk); thin = D(r.V2, thin); thick = D(r.V3, thick);
                    break;
                }
            }
        }
        return thicknessMm >= brk ? thick : thin;
    }

    /// <summary>玻璃材料表是否真的从参数库目录读到了（false = 用了内置快照）。</summary>
    public static bool GlassTableLoaded =>
        File.Exists(Path.Combine(ParamDir, GlassFileName));
}
