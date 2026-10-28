using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GlassManagement.Calc.Data;

/// <summary>
/// 参数库装载器（手写，与生成文件 <c>ParamLibrary.cs</c> 同为一个 partial 类）。
///
/// <para><b>★ 铁律（用户 2026-10-24 明确要求）</b>：所有型材数据一律<b>从参数库读</b>，
/// 不得在源码里另编一套、也不得静默使用"安慰值"。本文件负责把
/// <c>参数库.mdb</c> 导出的表（<c>local-data/hb_mdb/*.csv</c>）在运行时读进来；
/// 生成文件里的内置数组只是<b>同一份参数库的编译期快照</b>，仅当找不到库目录时才兜底，
/// 且 <see cref="SourceNote"/> 会明确标出"内置快照（未找到参数库）"。</para>
///
/// <para>涉及的表（列：型材代号 / Ix / Iy / Wx1 / Wx2 / 壁厚 / 截面积 / 面积矩 / 受剪腹板总宽度 / 备注）：</para>
/// <list type="bullet">
/// <item><c>立柱.csv</c> —— 95 行，构件式立柱选料</item>
/// <item><c>单元立柱一.csv</c> / <c>单元立柱二.csv</c> —— 各 7 行，单元式左框 / 右框
/// （两表内容相同；与「立柱」表相比 Wx1/Wx2 两列相反）</item>
/// </list>
/// </summary>
public static partial class ParamLibrary
{
    private static string? _paramDir;
    private static List<string>? _loadedTables;

    /// <summary>
    /// 参数库导出目录。查找顺序：
    /// ① 环境变量 <c>SMHS_HB_MDB_DIR</c>；
    /// ② exe 同目录下的 <c>hb_mdb\</c>；
    /// ③ 从 exe 目录向上 6 级找 <c>local-data\hb_mdb</c>（开发期直接从仓库跑）。
    /// 找不到返回空串。
    /// </summary>
    public static string ParamDir => _paramDir ??= ResolveParamDir();

    /// <summary>本次实际数据来源，供界面/状态栏显示。未找到参数库时明确写"内置快照"。</summary>
    public static string SourceNote
    {
        get
        {
            var t = _loadedTables;
            if (t is null || t.Count == 0) return "内置快照（未找到参数库）";
            return $"参数库: {string.Join(" + ", t)} @ {ParamDir}";
        }
    }

    private static string ResolveParamDir()
    {
        var list = new List<string?>();
        var env = Environment.GetEnvironmentVariable("SMHS_HB_MDB_DIR");
        if (!string.IsNullOrWhiteSpace(env)) list.Add(env);
        try
        {
            list.Add(Path.Combine(AppContext.BaseDirectory, "hb_mdb"));
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 6 && d is not null; i++)
            {
                list.Add(Path.Combine(d.FullName, "local-data", "hb_mdb"));
                d = d.Parent;
            }
        }
        catch { /* 目录探测失败按未找到处理 */ }

        foreach (var c in list)
        {
            if (c is null) continue;
            try
            {
                if (Directory.Exists(c) && File.Exists(Path.Combine(c, "立柱.csv"))) return c;
            }
            catch { }
        }
        return "";
    }

    /// <summary>
    /// 从参数库读一张型材表。成功时登记到 <see cref="SourceNote"/>；失败/缺失时返回编译期快照。
    /// 注意：这里**不做任何数值修补**——库里是什么就传什么，避免"看起来对"的自编数据。
    /// </summary>
    private static IReadOnlyList<ColumnProfile> LoadColumns(string fileName, ColumnProfile[] snapshot)
    {
        var dir = ParamDir;
        if (dir.Length == 0) return snapshot;
        var path = Path.Combine(dir, fileName);
        try
        {
            if (!File.Exists(path)) return snapshot;
            var res = new List<ColumnProfile>();
            var lines = File.ReadAllLines(path, Encoding.UTF8);
            for (int i = 1; i < lines.Length; i++)          // 第 0 行是表头
            {
                var c = SplitCsv(lines[i]);
                if (c.Length < 10 || string.IsNullOrWhiteSpace(c[0])) continue;
                double F(int k) => double.TryParse(c[k], NumberStyles.Float,
                                                  CultureInfo.InvariantCulture, out var v) ? v : 0;
                res.Add(new ColumnProfile(c[0].Trim(), F(1), F(2), F(3), F(4), F(5), F(6), F(7), F(8),
                                          c[9].Trim()));
            }
            if (res.Count == 0) return snapshot;
            (_loadedTables ??= new List<string>()).Add($"{fileName}({res.Count}行)");
            return res;
        }
        catch
        {
            return snapshot;
        }
    }

    /// <summary>极简 CSV 切分（支持双引号包裹与 "" 转义；参数库这些表没有换行内嵌字段）。</summary>
    private static string[] SplitCsv(string line)
    {
        var res = new List<string>();
        var sb = new StringBuilder();
        bool q = false;
        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (q)
            {
                if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else if (ch == '"') q = false;
                else sb.Append(ch);
            }
            else
            {
                if (ch == '"') q = true;
                else if (ch == ',') { res.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(ch);
            }
        }
        res.Add(sb.ToString());
        return res.ToArray();
    }
}
