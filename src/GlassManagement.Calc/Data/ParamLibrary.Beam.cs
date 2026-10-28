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

    /// <summary>单元横框三表共用的内置快照（40/60系列，2 位小数口径）。</summary>
    private static readonly BeamProfile UnitSnapshot = new BeamProfile("40/60系列",
        Ix: 115_800, Iy: 123_700,
        Wx1: 4_100, Wx2: 3_660,
        Wy1: 5_760, Wy2: 6_640,
        Sx: 2_994, Sy: 3_546,
        Tx: 5, Ty: 3,
        Area: 449.3);

    /// <summary>
    /// 单元式横梁：上横框（参数库「单元横框一」）。
    /// 注意该表只有 2 位小数 → Ix 11.58 cm⁴ → **115800** mm⁴（与「横框」表 11.581→115810 不同），
    /// 实机定稿点 <c>0xA02839</c> 读入 115800 可证。
    /// </summary>
    public static IReadOnlyList<BeamProfile> UnitBeamTop { get; } =
        LoadBeamProfiles("单元横框一.csv", new BeamProfile[] { UnitSnapshot });

    /// <summary>单元式横梁：下横框（参数库「单元横框二」）。</summary>
    public static IReadOnlyList<BeamProfile> UnitBeamBottom { get; } =
        LoadBeamProfiles("单元横框二.csv", new BeamProfile[] { UnitSnapshot });

    /// <summary>单元式横梁：中横框（参数库「单元横框三（中横框）」）。</summary>
    public static IReadOnlyList<BeamProfile> UnitBeamMid { get; } =
        LoadBeamProfiles("单元横框三（中横框）.csv", new BeamProfile[] { UnitSnapshot });

    /// <summary>按型材号查单元式上横框。</summary>
    public static BeamProfile? FindUnitBeamTop(string? kw) => FindInBeam(UnitBeamTop, kw);

    /// <summary>按型材号查单元式下横框。</summary>
    public static BeamProfile? FindUnitBeamBottom(string? kw) => FindInBeam(UnitBeamBottom, kw);

    /// <summary>按型材号查单元式中横框。</summary>
    public static BeamProfile? FindUnitBeamMid(string? kw) => FindInBeam(UnitBeamMid, kw);

    private static BeamProfile FindInBeam(IReadOnlyList<BeamProfile> table, string? kw)
    {
        if (string.IsNullOrWhiteSpace(kw)) return table[0];
        return table.FirstOrDefault(p => p.Code == kw)
            ?? table.FirstOrDefault(p => p.Code.Contains(kw!, StringComparison.OrdinalIgnoreCase))
            ?? table[0];
    }

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

                // ★ 两张表列位不同（实测表头）：
                //   「横框.csv」14 列：… 连接面壁厚(7) 截面积(8) Sx(9) Sy(10) tx(11) ty(12) 备注(13)
                //   「单元横框一/二/三」13 列（**没有连接面壁厚**）：截面积(7) Sx(8) Sy(9) tx(10) ty(11) 备注(12)
                //   按列数自适应，否则单元表会把 ty 读成备注（→0）导致 τx 除零得 ∞。
                bool wide = c.Length >= 14;
                int iArea = wide ? 8 : 7;
                int iSx = wide ? 9 : 8;
                int iSy = wide ? 10 : 9;
                int iTx = wide ? 11 : 10;
                int iTy = wide ? 12 : 11;

                res.Add(new BeamProfile(c[0].Trim(),
                    Ix: F(1) * 1e4, Iy: F(2) * 1e4,
                    Wx1: F(3) * 1e3, Wx2: F(4) * 1e3,
                    Wy1: F(5) * 1e3, Wy2: F(6) * 1e3,
                    Sx: F(iSx) * 1e3, Sy: F(iSy) * 1e3,
                    Tx: F(iTx), Ty: F(iTy),
                    Area: F(iArea)));
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
