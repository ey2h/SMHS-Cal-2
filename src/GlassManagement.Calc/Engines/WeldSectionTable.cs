using System.Globalization;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 「焊缝和转接件参数」帧里的转接件规格表（20 行）。
///
/// <para><b>数据来源</b>：V31 实机四个下拉的候选表，`CB_GETLBTEXT` 直读
/// （<c>v31runs/embed_weld_probe.txt</c>）——这是程序自带数据，不是实测输出，
/// 也不是按结果反推。<b>显示串逐字符保留</b>（含 V31 里 <c>'10.0 '</c> 这类尾空格），
/// 因为这三个下拉是按同一个行号联动的，界面上要能和 V31 逐字符对照。</para>
///
/// <para><b>联动关系（实机实证，<c>v31runs/embed_section_probe.txt</c>）</b>：
/// 「转接件型号规格」`(491,66)` 与 抵抗矩 `(684,48)` / 惯性矩 `(684,71)` /
/// 截面积 `(684,94)` **共用同一个行号**：把规格从 10# 改到 12#，三个一起跳到第 1 行
/// （39.7→57.7 / 198→346 / 1274.8→1536.2）；反过来单独把抵抗矩拉到第 10 行，
/// 转接件强度 4.43→9.352、挠度 0.000276→0.00082，
/// 后者只能用惯性矩也换成 66.7 cm⁴ 解释 ⇒ 确实换的是整行。</para>
///
/// <para><b>前 5 行</b>与参数库 <c>立柱.csv</c> 的「槽钢［10／［12.6／［14a／［14b」逐位相同
/// （Ix / Wx1 / 截面积）；<b>12# 参数库未收录</b>（标准值 346 cm⁴ / 57.7 cm³ / 15.362 cm²），
/// 所以 12# 一行只以程序下拉原文为准。
/// <b>第 6~20 行</b>是矩形断面，与 <c>I=b·h³/12</c>、<c>W=b·h²/6</c> 一致
/// （6×100 → A=600、I=50 cm⁴、W=10 cm³），唯 6×130 的 I 程序自带 109.9 而非 109.85，
/// 照抄程序值。</para>
/// </summary>
public static class WeldSectionTable
{
    /// <summary>一行转接件规格：V31 显示的三个字符串 + 解析出的数值。</summary>
    public readonly record struct Row(
        string Name, string WText, string IText, string AText,
        double ModulusCm3, double InertiaCm4, double AreaMm2);

    private static Row R(string name, string w, string i, string a)
        => new(name, w, i, a,
               double.Parse(w, CultureInfo.InvariantCulture),
               double.Parse(i, CultureInfo.InvariantCulture),
               double.Parse(a, CultureInfo.InvariantCulture));

    /// <summary>20 行，顺序＝V31 下拉顺序（不可重排：行号是三者共用的索引）。</summary>
    public static readonly Row[] Rows =
    {
        // ── 槽钢 5 行（GB 标准截面）──
        R("10#槽钢",     "39.7", "198",   "1274.8"),
        R("12#槽钢",     "57.7", "346",   "1536.2"),
        R("12.6#槽钢",   "62.1", "391",   "1569.2"),
        R("14a#槽钢",    "80.5", "564",   "1851.6"),
        R("14b#槽钢",    "87.1", "609",   "2131.6"),
        // ── 矩形断面 15 行（b×h，单位 mm；I/W 显示带一位小数）──
        R("断面宽6,高100",  "10.0 ", "50.0 ",  "600"),
        R("断面宽6,高110",  "12.1 ", "66.6 ",  "660"),
        R("断面宽6,高120",  "14.4 ", "86.4 ",  "720"),
        R("断面宽6,高130",  "16.9 ", "109.9 ", "780"),
        R("断面宽6,高140",  "19.6 ", "137.2 ", "840"),
        R("断面宽8,高100",  "13.3 ", "66.7 ",  "800"),
        R("断面宽8,高110",  "16.1 ", "88.7 ",  "880"),
        R("断面宽8,高120",  "19.2 ", "115.2 ", "960"),
        R("断面宽8,高130",  "22.5 ", "146.5 ", "1040"),
        R("断面宽8,高140",  "26.1 ", "182.9 ", "1120"),
        R("断面宽10,高100", "16.7 ", "83.3 ",  "1000"),
        R("断面宽10,高110", "20.2 ", "110.9 ", "1100"),
        R("断面宽10,高120", "24.0 ", "144.0 ", "1200"),
        R("断面宽10,高130", "28.2 ", "183.1 ", "1300"),
        R("断面宽10,高140", "32.7 ", "228.7 ", "1400"),
    };

    public static int Count => Rows.Length;

    /// <summary>取一行（越界自动夹到合法范围，避免界面索引越界崩）。</summary>
    public static Row At(int index) => Rows[System.Math.Clamp(index, 0, Rows.Length - 1)];

    /// <summary>惯性矩换到 mm⁴（表里是 cm⁴，实机除数 1980000 = 198×10⁴）。</summary>
    public static double InertiaMm4(int index) => At(index).InertiaCm4 * 1e4;

    /// <summary>抵抗矩换到 mm³（表里是 cm³，实机除数 39700 = 39.7×10³）。</summary>
    public static double ModulusMm3(int index) => At(index).ModulusCm3 * 1e3;
}
