namespace GlassManagement.Calc.Data;

/// <summary>
/// 玻璃强度设计值 —— V31 玻璃屏「选用玻璃的强度设计值」那一行。
///
/// <para><b>数值来源</b>：参数库目录下的 <c>玻璃材料（复刻补充）.csv</c>
/// （<c>参数库.mdb</c> 的 59 张表全是型材/板材截面表，没有玻璃材料表；
/// 两处 mdb 的 MD5 一致、全量导出、无「强度设计值」列，已核对）。
/// 本类只做转发，<b>不再持有任何字面量强度值</b>。</para>
///
/// <para><b>实机取证</b>：6mm 钢化 → <c>84MPa(钢化玻璃)</c>，槽 <c>0x1C911BC [19]</c> 直读 = 84
/// （docs/V31_GLASS_PAGE.md 第二节）。</para>
///
/// <para><b>种类档是程序常量</b>（V31 玻璃屏「种类」5 个单选，非参数库数据）。</para>
/// </summary>
public static class GlassAllowables
{
    /// <summary>玻璃种类 5 档 —— V31 窗体单选固定项（程序常量，非参数库）。</summary>
    public static readonly string[] Kinds =
    {
        "普通玻璃", "浮法玻璃", "半钢化玻璃", "钢化玻璃", "夹丝玻璃",
    };

    /// <summary>降档厚度阈值 mm（参数库「强度分界厚度」，默认 12）：≥ 该值取厚侧强度。</summary>
    public static double ThicknessBreakMm => ParamLibrary.GlassStrengthBreakThickness;

    /// <summary>强度设计值 f_g（MPa）—— 查参数库「强度设计值」段。</summary>
    public static double Bending(string kind, double thicknessMm)
        => ParamLibrary.GlassBendingStrength(kind, thicknessMm);

    /// <summary>屏幕文案里的括号标注，如 <c>84MPa(钢化玻璃)</c>。</summary>
    public static string Caption(string kind, double thicknessMm)
        => $"{Bending(kind, thicknessMm):0.###}MPa({kind})";
}
