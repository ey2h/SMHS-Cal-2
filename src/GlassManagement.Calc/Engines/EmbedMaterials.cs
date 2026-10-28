using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 埋件屏「土建预埋参数」帧里的两张材料表。
///
/// <para><b>来源 = V31 反汇编里的 If 链，代码级闭合</b>（不是查规范表，也不是从输出反推）。
/// 结构是 <c>If 等级 = "Cxx" Then fc = 字面量</c>，字面量以 <c>fld qword ptr [常量池]</c> 直接取：</para>
///
/// <code>
/// push "C25"(0x19225FE) → __vbaStrCmp(0x41364E) → je 跳过
/// 0x1922631  fld qword ptr [0x40B988]   ← 11.9
/// 0x1922637  fstp qword ptr [ebp-0x170]
/// 0x192263D  mov dword ptr [ebp-0x178], 5      ← VT_R8
/// 0x1922647  mov dword ptr [ebp-0x1dc], 0x12   ← 临时槽 18
/// 0x1922688  __vbaVarMove → 槽 18 = 11.9
/// </code>
///
/// <para>fc 链 <c>0x19224FB–0x1923168</c>（槽 18），ft 链 <c>0x1925853–0x19264B7</c>（槽 48），
/// 各 13 条分支，等级名以 UTF-16 内联串比对、数值以 double 字面量赋值。
/// 同一张表在 <c>0x1931A40</c> / <c>0x1A9A3BC</c> / <c>0x1B03FA0</c> 还有三处副本（内容一致）。</para>
///
/// <list type="bullet">
/// <item><b>混凝土 13 级</b>：下拉 <c>CB_GETLBTEXT</c> 直读 C20..C80；
///   fc / ft 逐级取自上表，每项地址见 <see cref="ConcreteFc"/> / <see cref="ConcreteFt"/> 的注释。</item>
/// <item><b>ft 的 C65~C80 是四条独立分支，每条都写字面量 2.04</b>
///   （`0x19261A1` / `0x19262A4` / `0x19263A7` / `0x19264AA`），
///   **不是**「表只有 9 项、越界读末项」——那是早前从输出观察得出的错误机制解释，
///   结果虽同（都 2.04），机制已按反汇编更正。</item>
/// <item><b>锚筋材质 2 项</b>（HPB300 / HRB400，只有两项，没有 HRB335）。
///   两个独立输出同时定死它：
///   ① <c>预埋件锚筋总截面积计算值 52.188</c>（∝ 1/fy）⇒ <b>fy = 300</b>；
///   ② <c>锚筋基本锚固长度lab 436.535</c> ⇒ <c>α·fy = 42.0</c> ⇒ <b>α = 0.14</b>。
///   两项互相印证，且 α 恰是「带肋 0.14 / 光圆 0.16」的规范分档。</item>
/// <item><b>锚筋外型下拉 en=0 的原因</b>：它由材质驱动——选 HPB300 显示「光圆」(sel=0)、
///   选 HRB400 显示「带肋」(sel=1)，实机同步变化，所以用户改不了。</item>
/// </list>
/// </summary>
public static class EmbedMaterials
{
    // ───────────────────────── 混凝土 ─────────────────────────

    /// <summary>混凝土等级下拉候选（与 V31 同序，13 项）。</summary>
    public static readonly string[] ConcreteGrades =
    {
        "C20", "C25", "C30", "C35", "C40", "C45", "C50",
        "C55", "C60", "C65", "C70", "C75", "C80",
    };

    /// <summary>
    /// 轴心抗压强度设计值 fc —— 逐项来自 fc 链的 <c>fld qword ptr [...]</c>。
    /// 地址依次：C20 <c>0x192252E</c>／C25 <c>0x1922631</c>／C30 <c>0x1922734</c>／
    /// C35 <c>0x1922837</c>／C40 <c>0x192293A</c>／C45 <c>0x1922A3D</c>／C50 <c>0x1922B40</c>／
    /// C55 <c>0x1922C43</c>／C60 <c>0x1922D46</c>／C65 <c>0x1922E49</c>／C70 <c>0x1922F4C</c>／
    /// C75 <c>0x192304F</c>／C80 <c>0x1923152</c>
    /// </summary>
    private static readonly double[] Fc =
    {
        9.6, 11.9, 14.3, 16.7, 19.1, 21.1, 23.1,
        25.3, 27.5, 29.7, 31.8, 33.8, 35.9,
    };

    /// <summary>
    /// 轴心抗拉强度设计值 ft —— 逐项来自 ft 链（槽 48）。
    /// 地址依次：C20 <c>0x1925886</c>／C25 <c>0x1925989</c>／C30 <c>0x1925A8C</c>／
    /// C35 <c>0x1925B8F</c>／C40 <c>0x1925C92</c>／C45 <c>0x1925D95</c>／C50 <c>0x1925E98</c>／
    /// C55 <c>0x1925F9B</c>／C60 <c>0x192609E</c>／C65 <c>0x19261A1</c>／C70 <c>0x19262A4</c>／
    /// C75 <c>0x19263A7</c>／C80 <c>0x19264AA</c>
    /// <para>★ C65/C70/C75/C80 是四条**各自独立**的分支，字面量都写 2.04（不是数组越界回退）。</para>
    /// </summary>
    private static readonly double[] Ft =
    {
        1.10, 1.27, 1.43, 1.57, 1.71, 1.80, 1.89,
        1.96, 2.04, 2.04, 2.04, 2.04, 2.04,
    };

    /// <summary>等级下标 → fc（13 级一一对应，无越界回退）。</summary>
    public static double ConcreteFc(int index)
        => Fc[Math.Clamp(index, 0, Fc.Length - 1)];

    /// <summary>等级下标 → ft（13 级一一对应；C65 起字面量即 2.04）。</summary>
    public static double ConcreteFt(int index)
        => Ft[Math.Clamp(index, 0, Ft.Length - 1)];

    // ───────────────────────── 锚筋 ─────────────────────────

    /// <summary>锚筋材质下拉候选（实机只有 2 项）。</summary>
    public static readonly string[] RebarMaterials = { "HPB300", "HRB400" };

    /// <summary>锚筋外型下拉候选（en=0，由材质驱动）。</summary>
    public static readonly string[] RebarShapes = { "光圆", "带肋" };

    /// <summary>锚筋直径下拉候选（14 项，实机同序）。</summary>
    public static readonly double[] RebarDiameters =
    {
        8, 10, 12, 14, 15, 16, 18, 20, 22, 25, 28, 32, 36, 40,
    };

    /// <summary>钢筋层数（下拉 3 项）。</summary>
    public static readonly string[] RebarLayers = { "2层", "3层", "4层" };

    /// <summary>每层筋数（下拉 3 项）。</summary>
    public static readonly string[] RebarPerLayer = { "2", "3", "4" };

    /// <summary>锚板厚度（下拉 10 项）。</summary>
    public static readonly double[] PlateThicknesses = { 6, 8, 10, 12, 14, 16, 18, 20, 22, 24 };

    /// <summary>锚筋抗拉强度设计值 fy（HPB300 270 / HRB400 300，实读）。</summary>
    public static double RebarFy(int index) => index == 1 ? 300.0 : 270.0;

    /// <summary>锚固长度公式系数 α（光圆 0.16 / 带肋 0.14）。</summary>
    public static double AnchorAlpha(int index) => index == 1 ? 0.14 : 0.16;

    /// <summary>材质对应的锚筋外型行号（0=光圆 1=带肋），实机联动已验证。</summary>
    public static int ShapeIndex(int materialIndex) => Math.Clamp(materialIndex, 0, 1);
}
