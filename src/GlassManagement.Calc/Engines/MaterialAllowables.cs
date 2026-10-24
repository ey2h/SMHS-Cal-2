using System;
using System.Collections.Generic;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 型材材料许用值 —— 取自 V35 的 A002 材料表（框架式：抗弯 A002(18)、抗剪 A002(19)）。
/// 表中数值是**逐个材质让程序自己算一遍并打印**得到的（`local-data/recon/v31_mats.ps1` → `sweep_mats.csv`），
/// 不是拟合：同一材质下「抗弯预选 × f」恒等于 M，反证了 f 确实是程序在用的值。
/// 折算强度 = f × 1.1（程序行为，6063A-T5 → 148.5 对得上）。
/// </summary>
public static class MaterialAllowables
{
    public sealed record Allow(double Bending, double Shear)
    {
        public double Equivalent => Bending * 1.1;
    }

    private static readonly Dictionary<string, Allow> Table = new(StringComparer.OrdinalIgnoreCase)
    {
        ["6063-T5"] = new(90, 55),
        ["6063-T6"] = new(150, 85),
        ["6061-T4"] = new(90, 55),
        ["6061-T6"] = new(200, 115),
        ["6063A-T5"] = new(135, 75),
        ["6063A-T6"] = new(160, 90),
        ["Q235"] = new(215, 125),
        ["Q355"] = new(315, 180),
    };

    /// <summary>其它材质：V35 允许人工填弹性模量/强度，缺省按 Q235 档给（界面会显示具体值）。</summary>
    private static readonly Allow Fallback = new(90, 55);

    public static Allow Get(string? material)
        => material is not null && Table.TryGetValue(material, out var a) ? a : Fallback;

    public static double Bending(string? material) => Get(material).Bending;
    public static double Shear(string? material) => Get(material).Shear;
    public static double Equivalent(string? material) => Get(material).Equivalent;
}
