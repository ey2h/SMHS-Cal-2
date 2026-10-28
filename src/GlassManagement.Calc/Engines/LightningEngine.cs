using System;

namespace GlassManagement.Calc.Engines
{
    /// <summary>
    /// 建筑预计雷击次数计算引擎（菜单 id=181，窗体「建筑预计雷击次数计算」）。
    /// 公式（GB50057-2010 附录 A.1，V31 输出文案依据同规范）：
    ///   N = k · Ng · Ae（次/a）
    ///   Ng = 0.1 · Td（地面落雷密度，次/(km²·a)；Td 年平均雷暴日）
    ///   Ae = [LW + 2(L+W)·D + π·D²] × 10⁻⁶（D 为扩大宽度）
    /// 校正系数 k：一般 1.0；河边湖边/土山顶部/山谷风口/特别潮湿 1.5；
    ///   金属屋面未接地砖木 1.7；山顶或旷野孤立 2.0。
    /// 扩大宽度 D：H&lt;100m 时 D=√(H(200-H))；H≥100m 时 D=H。
    /// 数据：雷暴日 Td 取自安装目录 pic\热工\规范\leiji\*.inx（529 城，见 LightningCityTable）。
    /// </summary>
    public sealed class LightningEngine
    {
        /// <summary>校正系数 k 档位（GB50057 附录 A）。</summary>
        public enum KFactor { 一般情况 = 0, 河边湖边等 = 1, 金属屋面未接地砖木 = 2, 山顶旷野孤立 = 3 }

        /// <summary>校正系数 k 值。</summary>
        public static double KValue(KFactor f) => f switch
        {
            KFactor.河边湖边等 => 1.5,
            KFactor.金属屋面未接地砖木 => 1.7,
            KFactor.山顶旷野孤立 => 2.0,
            _ => 1.0,
        };

        /// <summary>地面落雷密度 Ng = 0.1·Td。</summary>
        public static double Ng(double td) => 0.1 * td;

        /// <summary>扩大宽度 D（mm 无关，输入 L/W/H 为 m）。</summary>
        public static double ExpandWidth(double h)
            => h < 100.0 ? Math.Sqrt(h * (200.0 - h)) : h;

        /// <summary>
        /// 周边建筑修正档（窗体「与建筑物截收相同雷击次数的等效面积要求」）。
        /// <para>★ 关键语义：**D 的公式跟选项组走，不跟实际 H 走**——选「小于100m」组选项
        /// 一律用 D=√(H(200−H))（V31 实证：H=150 选该组预演 N=0.228=√7500 代入值，
        /// 程序不校验 H 是否真的 &lt;100）；选「大于100m」组一律用 D=H。</para>
        /// <para>证据：V31 实机逐档预演（t7_surround_options.json H90_full 组 +
        /// t7_groupB2.json H=150 组），N 三位小数逐位反解命中：</para>
        /// <para>**低组（D=√(H(200−H))，H=90 实测）**：单座 0.272=[LW+2(L+W)D+πD²]；
        /// 周边2D有等高/低 0.245=单座−0.75D²；四周2D都有等高/低 0.122=[LW+(L+W)D+πD²/4]；
        /// 周边2D有更高 0.218=单座−1.5D²；四周2D都有更高 0.029=[LW]。</para>
        /// <para>**高组（D=H，H=150 实测）**：单座 0.482=[LW+2(L+W)H+πH²]；
        /// 周边2H有等高/低 0.441=单座−0.5H²；四周2H都有等高/低 0.191=[LW+(L+W)H+πH²/4]；
        /// 周边2H有更高 0.4=单座−H²；四周2H都有更高 0.029=[LW]。</para>
        /// <para>未定：「各部位高不同」分支（N=0.005 与 H 无关，单点无法定型）。</para>
        /// </summary>
        public enum Surround
        {
            低_单座 = 0,
            低_周边2X有等高或更低,
            低_四周2X都有等高或更低,
            低_周边2X有更高,
            低_四周2X都有更高,
            高_单座 = 5,
            高_周边2X有等高或更低,
            高_四周2X都有等高或更低,
            高_周边2X有更高,
            高_四周2X都有更高,
        }

        /// <summary>等效面积 Ae（km²）：按所选选项组决定 D 公式（低组=√(H(200−H))，高组=H）。</summary>
        public static double EquivalentArea(double l, double w, double h, Surround s)
        {
            bool tall = s >= Surround.高_单座;
            double d = tall ? h : Math.Sqrt(h * (200.0 - h));
            double lw = l * w;
            double x2 = d * d;
            double full = lw + 2.0 * (l + w) * d + Math.PI * x2;
            double ae = s switch
            {
                Surround.低_周边2X有等高或更低 => full - 0.75 * x2,
                Surround.低_周边2X有更高 => full - 1.5 * x2,
                Surround.高_周边2X有等高或更低 => full - 0.5 * x2,
                Surround.高_周边2X有更高 => full - 1.0 * x2,
                Surround.低_四周2X都有等高或更低 or Surround.高_四周2X都有等高或更低
                    => lw + (l + w) * d + Math.PI * x2 / 4.0,
                Surround.低_四周2X都有更高 or Surround.高_四周2X都有更高 => lw,
                _ => full,
            };
            return ae * 1e-6;
        }

        /// <summary>等效面积 Ae（km²）：按实际 H 自动选组（单座形态）。</summary>
        public static double EquivalentArea(double l, double w, double h)
            => EquivalentArea(l, w, h, h < 100.0 ? Surround.低_单座 : Surround.高_单座);

        /// <summary>年预计雷击次数 N（含周边修正档）。</summary>
        public static double Compute(double l, double w, double h, double td, KFactor kf, Surround s)
            => KValue(kf) * Ng(td) * EquivalentArea(l, w, h, s);

        /// <summary>年预计雷击次数 N。</summary>
        public static double Compute(double l, double w, double h, double td, KFactor kf)
            => Compute(l, w, h, td, kf, h < 100.0 ? Surround.低_单座 : Surround.高_单座);

        public static string FormatOutput(double l, double w, double h, double td, KFactor kf)
        {
            double k = KValue(kf);
            double ng = Ng(td);
            double d = ExpandWidth(h);
            double ae = EquivalentArea(l, w, h);
            double n = k * ng * ae;
            string Sep = "********************************************";
            return $"{Sep}  " +
                   $"建筑物预计雷击次数 N = k·Ng·Ae      " +
                   $"校正系数 k = {k:0.#}      " +
                   $"雷击大地年平均密度 Ng = 0.1·Td = 0.1×{td:0.#} = {ng:0.###} 次/(km²·a)      " +
                   $"扩大宽度 D = {d:0.###} m      " +
                   $"等效面积 Ae = [{l}×{w} + 2×({l}+{w})×{d:0.###} + π×{d:0.###}²]×10⁻⁶ = {ae:0.####} km²      " +
                   $"年预计雷击次数 N = {n:0.####} 次/a  " +
                   $"{Sep}";
        }
    }
}
