using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Calc.Data;

/// <summary>
/// 参数库「横框」表（横梁型材截面特性）。
/// <para>
/// 手写 partial（<c>ParamLibrary.cs</c> 是脚本生成的，不改它）。
/// 表头：型材号 / Ix惯性矩 / Iy惯性矩 / Wx1抵抗矩 / Wx2抵抗矩 / Wy1抵抗矩 / Wy2抵抗矩 /
/// 连接面壁厚 / 截面积 / 面积矩Sx / 面积矩Sy / 腹板总宽度tx / 腹板总宽度ty / 备注。
/// </para>
/// <para>
/// 单位换算由 V31 运行期实值钉死：<c>轴惯性矩 11.581 → 115810</c>（<c>0x8B3727</c>）、
/// <c>抗弯矩 3.662 → 3662</c>（<c>0x8B3C15</c>）、<c>面积矩 2.994 → 2994</c>（<c>0x8B4565</c>），
/// 即 cm⁴×10⁴、cm³×10³；腹板总宽度 tx/ty 本身是 mm，不换算（<c>0x8B43A6</c> 读入 5）。
/// </para>
/// </summary>
public static partial class ParamLibrary
{
    /// <summary>横梁型材表（优先读 <c>横框.csv</c>，找不到时用内置快照）。</summary>
    public static IReadOnlyList<BeamProfile> BeamProfiles { get; } =
        LoadBeamProfiles("横框.csv", new[]
        {
            // 40/60系列（cm 单位已换算成 mm；与 «横框.csv» 首行一致）
            new BeamProfile("40/60系列",
                Ix: 115_810, Iy: 123_650,
                Wx1: 4_100, Wx2: 3_662,
                Wy1: 5_763, Wy2: 6_639,
                Sx: 2_994, Sy: 3_546,
                Tx: 5, Ty: 3,
                Area: 449.3),
        });

    /// <summary>按型材号查横梁型材（空关键字 = 表内第一行，与 V31 默认选行一致）。</summary>
    public static BeamProfile? FindBeamProfile(string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return BeamProfiles[0];
        return BeamProfiles.FirstOrDefault(p => p.Code == keyword)
            ?? BeamProfiles.FirstOrDefault(p => p.Code.Contains(keyword!, StringComparison.OrdinalIgnoreCase))
            ?? BeamProfiles[0];
    }

    private static IReadOnlyList<BeamProfile> LoadBeamProfiles(string fileName, BeamProfile[] snapshot)
    {
        var dir = ParamDir;
        if (dir.Length == 0) return snapshot;
        var path = Path.Combine(dir, fileName);
        try
        {
            if (!File.Exists(path)) return snapshot;
            var res = new List<BeamProfile>();
            var lines = File.ReadAllLines(path, Encoding.UTF8);
            for (int i = 1; i < lines.Length; i++)          // 第 0 行是表头
            {
                var c = SplitCsv(lines[i]);
                if (c.Length < 13 || string.IsNullOrWhiteSpace(c[0])) continue;
                double F(int k) => double.TryParse(c[k], NumberStyles.Float,
                                                  CultureInfo.InvariantCulture, out var v) ? v : 0;
                res.Add(new BeamProfile(c[0].Trim(),
                    Ix: F(1) * 1e4, Iy: F(2) * 1e4,
                    Wx1: F(3) * 1e3, Wx2: F(4) * 1e3,
                    Wy1: F(5) * 1e3, Wy2: F(6) * 1e3,
                    Sx: F(9) * 1e3, Sy: F(10) * 1e3,
                    Tx: F(11), Ty: F(12),
                    Area: F(8)));
            }
            if (res.Count > 0)
            {
                (_loadedTables ??= new List<string>()).Add($"{fileName}({res.Count}行)");
                return res;
            }
        }
        catch
        {
            // 读失败回落内置快照（与立柱加载器同策略）
        }
        return snapshot;
    }
}
