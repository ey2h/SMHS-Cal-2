using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace GlassManagement.Calc.Data;

/// <summary>
/// 螺栓参数装载（<see cref="ParamLibrary"/> 的 partial 分支）。
///
/// <para><b>为什么需要它</b>：参数库里 <c>螺栓1.csv … 螺栓7.csv</c> 一直存在，
/// 但 <c>src/</c> 下此前<b>零代码加载</b>（只有 Api / Blazor 向导用过）。
/// 连接屏（P1）与埋件屏（P2）的螺栓承载力必须走这张表，不能在引擎里写死。</para>
///
/// <para><b>7 张表实测只有 3 种不同内容</b>（`md5sum` 与列结构核对）：</para>
/// <list type="table">
/// <item><term>螺栓1 = 2 = 3</term><description>6 数据列（型号/公称直径/有效直径/材质/剪应力/拉应力/备注），48 行，含 M5</description></item>
/// <item><term>螺栓4 = 5 = 6</term><description><b>7 数据列，多一列「孔径」</b>，48 行</description></item>
/// <item><term>螺栓7</term><description>6 数据列，36 行，<b>M8 起</b>（无 M5/M6/M7）</description></item>
/// </list>
/// <para>列数不同，加载时按表头定位，不能按固定下标取。</para>
///
/// <para><b>行的语义</b>：同一型号会因「用途」和「材质」出现多行
/// （备注 = 热轧型钢用 / 冷弯型钢用 / 钢材连接用 / 铝材连接用；
/// 材质 = 普通碳钢（C级）/ 奥氏体不锈钢（A50））。
/// 因此 <see cref="FindBolt"/> 必须带用途与材质筛选，不能只按型号取第一行。</para>
/// </summary>
public static partial class ParamLibrary
{
    /// <summary>一行螺栓参数。</summary>
    public sealed record BoltRow(
        string Model,        // 型号 M5…M24
        double NominalMm,    // 公称直径
        double EffectiveMm,  // 有效直径（受拉面积用）
        string Material,     // 普通碳钢（C级）/ 奥氏体不锈钢（A50）
        double ShearMpa,     // 剪应力设计值
        double TensionMpa,   // 拉应力设计值
        double HoleMm,       // 孔径（仅「螺栓4/5/6」有，无则为 0）
        string Usage);       // 备注：热轧型钢用 / 冷弯型钢用 / 钢材连接用 / 铝材连接用

    private static readonly string[] BoltFiles =
        { "螺栓1.csv", "螺栓4.csv", "螺栓7.csv" };   // 三种不同内容的代表

    private static readonly Dictionary<string, IReadOnlyList<BoltRow>> _boltCache = new();

    /// <summary>
    /// 读某张螺栓表。表不存在或被清空时返回内置快照（`SourceNote` 会标出是快照）。
    /// </summary>
    public static IReadOnlyList<BoltRow> BoltTable(string fileName)
    {
        if (_boltCache.TryGetValue(fileName, out var cached)) return cached;

        var rows = new List<BoltRow>();
        var dir = ParamDir;
        if (dir.Length > 0)
        {
            var path = Path.Combine(dir, fileName);
            try
            {
                if (File.Exists(path))
                {
                    var lines = File.ReadAllLines(path, Encoding.UTF8);
                    if (lines.Length > 1)
                    {
                        var head = SplitCsv(lines[0]);
                        int Idx(string name)
                        {
                            for (int i = 0; i < head.Length; i++)
                                if (head[i].Trim() == name) return i;
                            return -1;
                        }
                        int iModel = Idx("型号"), iNom = Idx("公称直径"), iEff = Idx("有效直径");
                        int iMat = Idx("材质"), iShear = Idx("剪应力"), iTen = Idx("拉应力");
                        int iHole = Idx("孔径"), iUsage = Idx("备注");
                        if (iModel >= 0 && iNom >= 0)
                        {
                            for (int i = 1; i < lines.Length; i++)
                            {
                                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                                var c = SplitCsv(lines[i]);
                                if (c.Length <= iModel || string.IsNullOrWhiteSpace(c[iModel])) continue;
                                string S(int k) => k >= 0 && k < c.Length ? c[k] : "";
                                double D(int k) => double.TryParse(S(k), NumberStyles.Float,
                                                    CultureInfo.InvariantCulture, out var v) ? v : 0;
                                rows.Add(new BoltRow(
                                    S(iModel).Trim(), D(iNom), D(iEff), S(iMat).Trim(),
                                    D(iShear), D(iTen), D(iHole), S(iUsage).Trim()));
                            }
                            if (rows.Count > 0)
                                (_loadedTables ??= new List<string>()).Add($"{fileName}({rows.Count}行)");
                        }
                    }
                }
            }
            catch { /* 读失败走快照 */ }
        }
        if (rows.Count == 0) rows.AddRange(BoltSnapshot);
        _boltCache[fileName] = rows;
        return rows;
    }

    /// <summary>默认查表用「螺栓1」（6 列版，含 M5）。</summary>
    public static IReadOnlyList<BoltRow> Bolts => BoltTable(BoltFiles[0]);

    /// <summary>
    /// 按型号查螺栓行。
    /// <para>同一型号有多行（不同材质/用途），优先级：材质精确 → 用途精确 → 第一行。</para>
    /// </summary>
    /// <param name="model">如 "M12"（大小写不敏感，允许只给 "12"）</param>
    /// <param name="material">材质关键字，如 "碳钢" / "不锈钢"；空则不限</param>
    /// <param name="usage">用途关键字，如 "铝材连接"；空则不限</param>
    /// <param name="fileName">查哪张表，默认螺栓1</param>
    public static BoltRow? FindBolt(string? model, string? material = null,
                                    string? usage = null, string? fileName = null)
    {
        var table = BoltTable(fileName ?? BoltFiles[0]);
        if (table.Count == 0) return null;
        if (string.IsNullOrWhiteSpace(model)) return table[0];

        var m = model!.Trim();
        if (!m.StartsWith("M", StringComparison.OrdinalIgnoreCase)) m = "M" + m;

        bool MatOK(BoltRow r) => string.IsNullOrWhiteSpace(material)
                                 || r.Material.Contains(material!, StringComparison.OrdinalIgnoreCase);
        bool UseOK(BoltRow r) => string.IsNullOrWhiteSpace(usage)
                                 || r.Usage.Contains(usage!, StringComparison.OrdinalIgnoreCase);

        var same = table.Where(r => string.Equals(r.Model, m, StringComparison.OrdinalIgnoreCase)).ToList();
        if (same.Count == 0) return null;

        var hit = same.FirstOrDefault(r => MatOK(r) && UseOK(r))
                  ?? same.FirstOrDefault(MatOK)
                  ?? same.FirstOrDefault(UseOK)
                  ?? same[0];
        return hit;
    }

    /// <summary>螺栓表是否真的从参数库目录读到了（false = 用了内置快照）。</summary>
    public static bool BoltTableLoaded => File.Exists(Path.Combine(ParamDir, BoltFiles[0]));

    // ---------------- 内置快照（仅当参数库目录找不到表时兜底）----------------

    private static readonly BoltRow[] BoltSnapshot =
    {
        new("M5", 5, 4.25314, "普通碳钢（C级）", 140, 170, 0, "热轧型钢用"),
        new("M6", 6, 5.06128, "普通碳钢（C级）", 140, 170, 0, "热轧型钢用"),
        new("M8", 8, 6.82674, "普通碳钢（C级）", 140, 170, 0, "热轧型钢用"),
        new("M10", 10, 8.58746, "普通碳钢（C级）", 140, 170, 0, "热轧型钢用"),
        new("M12", 12, 10.10519, "普通碳钢（C级）", 140, 170, 0, "热轧型钢用"),
        new("M16", 16, 13.83459, "普通碳钢（C级）", 140, 170, 0, "热轧型钢用"),
        new("M20", 20, 17.29417, "普通碳钢（C级）", 140, 170, 0, "热轧型钢用"),
        new("M24", 24, 20.75264, "普通碳钢（C级）", 140, 170, 0, "热轧型钢用"),
    };
}
