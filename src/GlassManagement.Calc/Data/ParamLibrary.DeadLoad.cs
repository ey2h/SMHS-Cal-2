// 参数库「幕墙自重参数」表（C:\code\Glass\参数库.mdb → local-data/hb_mdb/幕墙自重参数.csv，17 行 5 列）。
// 用途：基本参数输入屏「自重参数预置」按钮 + 立柱轴力 N = 1.3·Gk/A·B·(A1+L1)（V35 计算书 0x4E90C4）。
// 单位：N/m²（幕墙单位面积自重）。「立柱及主体自重」用于立柱/主体结构，「计算横梁时自重」用于横梁。
using System;
using System.Collections.Generic;
using System.Linq;
#nullable enable

namespace GlassManagement.Calc.Data;

public static partial class ParamLibrary
{
    /// <summary>幕墙自重参数表一行。</summary>
    public sealed record DeadLoadRow(int Id, string Category, double ColumnWeight, double TransomWeight, string Scope)
    {
        /// <summary>立柱及主体自重（N/m²）。</summary>
        public double GkColumn => ColumnWeight;
        /// <summary>计算横梁时自重（N/m²）。</summary>
        public double GkTransom => TransomWeight;
    }

    public static IReadOnlyList<DeadLoadRow> DeadLoads { get; } = new DeadLoadRow[]
    {
        new(1,  "单玻璃幕墙",                       400,  300,  "面板厚度不大于10mm时"),
        new(2,  "中空或夹层玻璃幕墙",                500,  400,  "面板总厚度不大于14mm时"),
        new(3,  "中空夹层或双中空玻璃幕墙",          650,  550,  "面板总厚度不大于20mm时"),
        new(4,  "双夹层中空（共四片玻璃）玻璃幕墙",  750,  650,  "面板总厚度不大于24mm时"),
        new(5,  "金属板、蜂窝石材板、木纤维板幕墙",  400,  300,  "常规金属板及木纤维板厚度不大于20mm时"),
        new(6,  "常规石材幕墙",                     1100, 1000, "面板厚度不大于35mm时"),
        new(7,  "砂岩、石灰石",                     900,  800,  "面板厚度不大于25mm时"),
        new(8,  "瓷板、陶板、纤维水泥板幕墙",        650,  550,  "面板总厚度不大于20mm时"),
        new(9,  "微晶玻璃",                         800,  700,  "面板厚度不大于25mm时"),
        new(10, "单玻璃索、杆幕墙",                  400,  0,    "面板厚度不大于10mm时"),
        new(11, "双玻璃索、杆幕墙",                  500,  0,    "面板总厚度不大于14mm时"),
        new(12, "单玻璃栏杆",                        400,  0,    "面板厚度不大于15mm时"),
        new(13, "双玻璃栏杆",                        500,  0,    "面板总厚度不大于16mm时"),
        new(14, "单玻璃门窗",                        400,  300,  "面板厚度不大于10mm时"),
        new(15, "中空或夹层门窗",                    500,  400,  "面板总厚度不大于14mm时"),
        new(16, "中空夹层(或三玻双中空)门窗",        650,  550,  "面板总厚度不大于20mm时"),
        new(17, "双夹层中空（共四片玻璃）门窗",      750,  650,  "面板总厚度不大于24mm时"),
    };

    /// <summary>
    /// 基本参数屏「玻璃配置」六个单选 → 自重表行号（按 V35 界面语义：面板片数越多自重越大）。
    ///   单片玻璃                 → 1  单玻璃幕墙                     400 / 300
    ///   中空玻璃 / 夹层玻璃       → 2  中空或夹层玻璃幕墙             500 / 400
    ///   中空+夹层 / 双中空玻璃    → 3  中空夹层或双中空玻璃幕墙       650 / 550
    ///   双夹层-中空玻璃           → 4  双夹层中空（共四片玻璃）       750 / 650
    /// </summary>
    public static DeadLoadRow DeadLoadForGlass(string glassConfig)
        => glassConfig switch
        {
            "单片玻璃" or "单玻" => DeadLoads[0],
            "中空玻璃" or "夹层玻璃" => DeadLoads[1],
            "中空+夹层玻璃" or "双中空玻璃" => DeadLoads[2],
            "双夹层-中空玻璃" => DeadLoads[3],
            _ => DeadLoads[0],
        };

    /// <summary>按类别名精确/包含匹配。</summary>
    public static DeadLoadRow? FindDeadLoad(string? keyword)
        => DeadLoads.FirstOrDefault(d => d.Category == keyword)
           ?? DeadLoads.FirstOrDefault(d => !string.IsNullOrWhiteSpace(keyword)
                && d.Category.Contains(keyword!, StringComparison.OrdinalIgnoreCase));
}
