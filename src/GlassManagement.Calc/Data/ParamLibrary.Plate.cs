using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GlassManagement.Calc.Data;

/// <summary>
/// 板材参数装载（<see cref="ParamLibrary"/> 的 partial 分支）。
///
/// <para><b>数据来源</b>：<c>参数库.mdb</c> 的「铝单板」表（导出为
/// <c>local-data/hb_mdb/铝单板.csv</c>，26 行，列：型号/厚度/抗弯强度/抗剪强度/备注）。
/// 不做任何数值修补——库里是什么就用什么。</para>
///
/// <para><b>实机印证</b>：V31 板块屏默认选中「1060 H14、H24 / t=2.5」，
/// 屏上「A板强度设计值:54MPa」与本表该行抗弯强度 54 一致。</para>
/// </summary>
public static partial class ParamLibrary
{
    private const string PlateFileName = "铝单板.csv";

    /// <summary>铝单板参数行（DataGrid 一行）。</summary>
    public sealed record AluminumPlateRow(
        string Model, double ThicknessMm, double BendingMpa, double ShearMpa, string Note);

    private static List<AluminumPlateRow>? _plateRows;

    /// <summary>铝单板参数表（型号 / 厚度 / 抗弯强度 / 抗剪强度 / 备注）。</summary>
    public static IReadOnlyList<AluminumPlateRow> AluminumPlates
    {
        get
        {
            if (_plateRows is not null) return _plateRows;
            var rows = new List<AluminumPlateRow>();
            var dir = ParamDir;
            if (dir.Length > 0)
            {
                var path = Path.Combine(dir, PlateFileName);
                try
                {
                    if (File.Exists(path))
                    {
                        var lines = File.ReadAllLines(path, Encoding.UTF8);
                        for (int i = 1; i < lines.Length; i++)
                        {
                            if (string.IsNullOrWhiteSpace(lines[i])) continue;
                            var c = SplitCsv(lines[i]);
                            if (c.Length < 4 || string.IsNullOrWhiteSpace(c[0])) continue;
                            double F(int k) => double.TryParse(c[k], NumberStyles.Float,
                                CultureInfo.InvariantCulture, out var v) ? v : 0;
                            rows.Add(new AluminumPlateRow(c[0].Trim(), F(1), F(2), F(3),
                                                          c.Length > 4 ? c[4].Trim() : ""));
                        }
                    }
                }
                catch { /* 读不到就留空，由界面提示参数库缺失 */ }
            }
            _plateRows = rows;
            return _plateRows;
        }
    }

    /// <summary>按型号 + 厚度取一行；找不到返回 null（不猜、不回落）。</summary>
    public static AluminumPlateRow? FindAluminumPlate(string? model, double thicknessMm)
    {
        foreach (var r in AluminumPlates)
        {
            if (Math.Abs(r.ThicknessMm - thicknessMm) > 1e-9) continue;
            if (string.IsNullOrWhiteSpace(model) || string.Equals(r.Model, model, StringComparison.Ordinal))
                return r;
        }
        return null;
    }

    /// <summary>铝单板型号去重列表（保持库里出现顺序）。</summary>
    public static IReadOnlyList<string> AluminumPlateModels
    {
        get
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var list = new List<string>();
            foreach (var r in AluminumPlates)
                if (seen.Add(r.Model)) list.Add(r.Model);
            return list;
        }
    }
}
