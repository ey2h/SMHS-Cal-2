using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 空气声隔声（菜单 id=137，CW19「建筑玻璃空气声隔声计算」帧）。
/// 玻璃构件空气声隔声量 R 计算（b7_probe 运行期直读 + 扰动验证）。
/// 单片玻璃质量定律：R = 13.5·lg(M) + 13，M 面密度 = 玻璃厚度(mm) × 2.56 (kg/m²)。
/// 玻璃密度 2.56 g/cm³ = 25.6 kN/m³，来源 = 参数库「玻璃材料」表「玻璃密度 2.56e-05 N/mm³」。
/// 实测：4mm 单片 → R = 13.5·lg(10.24)+13 = 26.639（round3）。
/// 注：中空/夹层玻璃隔声（共振频率/吻合效应/PVB 阻尼）公式待专测。
/// </summary>
public sealed class SoundInsulationEngine
{
    /// <summary>玻璃密度 g/cm³（25.6 kN/m³；参数库「玻璃材料」表直读）。</summary>
    public const double GlassDensity = 2.56;

    /// <summary>玻璃结构（CW19 空气声隔声帧 combo，8 项）。</summary>
    public enum GlassStructure
    {
        单片玻璃 = 0, 中空玻璃 = 1, 夹层玻璃 = 2, 中空加夹层 = 3,
        三玻双中空 = 4, 三玻双夹层 = 5, 四玻双夹层中空 = 6, 四玻夹层双中空 = 7,
    }

    /// <summary>各结构的（玻璃片数, PVB 层数, 气隙腔数）。</summary>
    private static readonly (int Panes, int Pvb, int Cavities)[] Spec =
    {
        (1, 0, 0), (2, 0, 1), (2, 1, 0), (3, 1, 1),
        (3, 0, 2), (3, 2, 0), (4, 2, 1), (4, 1, 2),
    };

    /// <summary>结构切换预设的默认玻璃总厚度 mm（V31 切换结构自动联动厚度：四玻外片 20）。</summary>
    private static readonly double[] DefaultThickness = { 4, 8, 8, 12, 12, 12, 32, 32 };

    /// <summary>单片玻璃空气声隔声量 R(dB)，质量定律 round3。</summary>
    public static double SinglePane(double thicknessMm)
    {
        double m = thicknessMm * GlassDensity;   // 面密度 kg/m²
        return WindLoadEngine.VbaRound(13.5 * Math.Log10(m) + 13.0, 3);
    }

    /// <summary>
    /// 玻璃构件空气声隔声量 R(dB) = 13.5·lg(总厚×2.56) + 13 + 0.1×气隙总厚(mm)。
    /// <para>★ 公式来源 = V31 指令级还原（2026-10-28 T11，非输出反推）：</para>
    /// <para>① frida 链（t11_sound_chain.json，中空+气隙12mm 预演）：
    /// `Mul(13.5, lg(20.48)) @ra 0x172BD0C → Add(+13) @0x172BD20 → Add(+1.2) @0x172BD34 → 31.903`；</para>
    /// <para>② IDA 指令（0x172BCA4-0x172BD34）：`fld ds:dbl_40D9A0`(=13.5) → `fmul ds:dbl_401DA0`
    /// **(=0.1，气隙系数)** ← 操作数=气隙 combo 的 Text 属性（`call [eax+0A8h]`）经 sub_413738 转 double；
    /// 常量池实锤：0x40D9A0=13.5、0x40D9A8=2.56、0x401DA0=0.1；</para>
    /// <para>③ 实测交叉验证：8 结构默认态 + 中空气隙 5 档（5/6/9/12/15mm→R=31.203/31.303/31.603/31.903/32.203，
    /// 每+1mm R+0.1dB）共 13 点逐位命中。旧式「0.5×气隙个数」是默认气隙 5mm 的巧合（0.1×5=0.5）。</para>
    /// </summary>
    public static double Compute(GlassStructure s, double totalThicknessMm, double gapTotalMm)
    {
        var (_, pvb, _) = Spec[(int)s];
        double m = totalThicknessMm * GlassDensity;
        return WindLoadEngine.VbaRound(13.5 * Math.Log10(m) + 13.0 + 4.0 * pvb + 0.1 * gapTotalMm, 3);
    }

    /// <summary>按结构默认气隙（5mm/腔）计算（旧签名，兼容）。</summary>
    public static double Compute(GlassStructure s, double totalThicknessMm)
    {
        var (_, _, cav) = Spec[(int)s];
        return Compute(s, totalThicknessMm, 5.0 * cav);
    }

    /// <summary>按结构默认厚度计算（V31 切换结构后未改厚度的默认态）。</summary>
    public static double ComputeDefault(GlassStructure s)
        => Compute(s, DefaultThickness[(int)s]);

    /// <summary>
    /// 组合墙空气声隔声 R(dB) = R_玻璃 + 10·lg((t_墙 + t_等效) / t_等效)，round3。
    /// <para>公式来源 = V31 链级还原（2026-10-28，t11_wall_chain.json，ra 0x1731478-0x1731524）：</para>
    /// <para>`Add(t_墙, t_等效) → Div(和, t_等效) → Mul(10, lg(商)) → Add(R_玻璃, 积)`；
    /// 扰动实证：墙厚 50→60（35.483→35.945）、第二框 15→25（→36.674）均逐位命中。</para>
    /// <para>注：第二输入框「等效厚度」干净默认态内部值为 39.06（外层文字显示 '15'，
    /// VB6 两层不同步——写入/读取一律以预演链为准）。</para>
    /// </summary>
    public static double ComputeWall(double rGlass, double wallThicknessMm, double equivThicknessMm)
    {
        double ratio = equivThicknessMm != 0
            ? (wallThicknessMm + equivThicknessMm) / equivThicknessMm : 1.0;
        return WindLoadEngine.VbaRound(rGlass + 10.0 * Math.Log10(ratio), 3);
    }

    /// <summary>GB/T8485-2008《建筑外窗空气声隔声性能分级》表18（V31 语义 = 国标级 + 1）。
    /// <para>★ 2026-10-28 T12b 实机 4 点直读（隔声帧 RichText 0xa1bb2）：26.639→3级、31.903→4级、
    /// 34.703→4级、34.78→4级；对照国标表18（26.639→2、31.903→3）**实测恒比国标高 1 级**。
    /// 旧 probe35 期望（2/3/1）系读错 RichText 所致，已纠正。</para></summary>
    public static int Grade8485(double r)
    {
        if (r < 20) return 0;
        if (r < 25) return 2;
        if (r < 30) return 3;
        if (r < 35) return 4;
        if (r < 40) return 5;
        if (r < 45) return 6;
        if (r < 50) return 7;
        return 8;
    }

    /// <summary>GB/T50121-2005《建筑隔声评价标准》空气声隔声分级（= GB/T8485 + 1；实机 4 点一致）。</summary>
    public static int Grade50121(double r) => r < 20 ? 1 : Grade8485(r) + 1;

    /// <summary>GB/T21086-2007《建筑幕墙》表18 幕墙空气声隔声分级（V31 语义 = 国标级 + 1；实机 4 点一致）。</summary>
    public static int Grade21086(double r)
    {
        if (r < 25) return 0;
        if (r < 30) return 2;
        if (r < 35) return 3;
        if (r < 40) return 4;
        if (r < 45) return 5;
        return 6;
    }
}
