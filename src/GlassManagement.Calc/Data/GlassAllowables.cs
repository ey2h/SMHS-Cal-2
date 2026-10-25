namespace GlassManagement.Calc.Data;

/// <summary>
/// 玻璃强度设计值表 —— V31 玻璃屏「选用玻璃的强度设计值」那一行。
///
/// <para><b>取证</b>：V31 实机 6mm 钢化 → `84MPa(钢化玻璃)`，槽数组 <c>0x1C911BC slot[19]</c> 直读 = 84
/// （见 docs/V31_GLASS_PAGE.md 第二节）。厚 ≥12mm 降一档的口径与
/// <c>GlassManagement.Api/Controllers/ContourController.cs</c> 的 <c>GetDesignStrength</c> 一致。</para>
///
/// <para><b>不要</b>复用型材的 <c>MaterialAllowables</c>（那是 6063-T5 等铝合金，语义不同）。</para>
///
/// <para>⚠ 参数库里<b>没有</b>玻璃强度表（<c>local-data/hb_mdb/</c> 只有 U型玻璃/钢管点玻等型材表），
/// 故此处按 V31 的两档常量实现。半钢化/浮法的具体档位来自 API 层既有实现，
/// 尚未与 V31 实机逐档对拍（只验过钢化 6mm=84）。</para>
/// </summary>
public static class GlassAllowables
{
    /// <summary>降档厚度阈值 mm：≥12 取低档。</summary>
    public const double ThicknessBreakMm = 12.0;

    /// <summary>玻璃种类（与界面 5 档一致）。</summary>
    public static readonly string[] Kinds =
    {
        "普通玻璃", "浮法玻璃", "半钢化玻璃", "钢化玻璃", "夹丝玻璃",
    };

    /// <summary>强度设计值 f_g（MPa）。</summary>
    public static double Bending(string kind, double thicknessMm)
    {
        bool thick = thicknessMm >= ThicknessBreakMm;
        return kind switch
        {
            "钢化玻璃" => thick ? 72.0 : 84.0,
            "半钢化玻璃" => thick ? 38.0 : 41.0,
            "浮法玻璃" => thick ? 24.0 : 28.0,
            "普通玻璃" => thick ? 24.0 : 28.0,
            "夹丝玻璃" => thick ? 24.0 : 28.0,   // 夹丝按普通玻璃档（未逐档对拍）
            _ => thick ? 24.0 : 28.0,
        };
    }

    /// <summary>屏幕文案里的括号标注，如 <c>84MPa(钢化玻璃)</c>。</summary>
    public static string Caption(string kind, double thicknessMm)
        => $"{Bending(kind, thicknessMm):0.###}MPa({kind})";
}
