using System;

namespace GlassManagement.Calc.Engines
{
    /// <summary>
    /// 外遮阳系数计算引擎（菜单 id=134，窗体「外遮阳系数计算」）。
    /// 公式（V31 运行期输出逐字直读，GB50189-2005 附录 A 表 A.0.1）：
    ///   水平遮阳板 SDh = ah·PF² + bh·PF + 1   (ah=0.35, bh=-0.76)
    ///   垂直遮阳板 SDv = av·PF² + bv·PF + 1   (av=0.32, bv=-0.63)
    ///   遮阳板外挑系数 PF = A/B（A=外挑长度、B=根部到窗对边距离），PF&gt;1 时取 1。
    /// 系数 ah/bh/av/bv 为程序内置（GB50189-2005 表 A.0.1 写死），参数库 61 表无对应出处。
    /// </summary>
    public sealed class ExternalShadingEngine
    {
        /// <summary>遮阳板形式。</summary>
        public enum ShadingType { 水平遮阳板 = 0, 垂直遮阳板 = 1 }

        /// <summary>水平遮阳板系数（GB50189-2005 表 A.0.1）。</summary>
        public const double Ah = 0.35, Bh = -0.76;
        /// <summary>垂直遮阳板系数（GB50189-2005 表 A.0.1）。</summary>
        public const double Av = 0.32, Bv = -0.63;

        /// <summary>遮阳板外挑系数 PF = A/B，PF&gt;1 取 1（round4）。</summary>
        public static double OverhangFactor(double A, double B)
        {
            double pf = B > 0 ? A / B : 0.0;
            if (pf > 1.0) pf = 1.0;
            return WindLoadEngine.VbaRound(pf, 4);
        }

        /// <summary>外遮阳系数 SD（round4）。</summary>
        public static double ShadingCoefficient(ShadingType type, double A, double B)
        {
            double pf = OverhangFactor(A, B);
            double a = type == ShadingType.水平遮阳板 ? Ah : Av;
            double b = type == ShadingType.水平遮阳板 ? Bh : Bv;
            return WindLoadEngine.VbaRound(a * pf * pf + b * pf + 1.0, 4);
        }

        /// <summary>结果。</summary>
        public readonly record struct Result(double PF, double SD);

        public static Result Compute(ShadingType type, double A, double B)
        {
            double pf = OverhangFactor(A, B);
            double sd = ShadingCoefficient(type, A, B);
            return new Result(pf, sd);
        }

        /// <summary>V31 输出正文（逐字对齐 RichText）。</summary>
        public static string FormatOutput(ShadingType type, double A, double B)
        {
            var r = Compute(type, A, B);
            string Sep = "****************************************";
            string kind = type == ShadingType.水平遮阳板 ? "水平遮阳板" : "垂直遮阳板";
            string sdName = type == ShadingType.水平遮阳板 ? "SDh" : "SDv";
            string ca = type == ShadingType.水平遮阳板 ? "ah" : "av";
            string cb = type == ShadingType.水平遮阳板 ? "bh" : "bv";
            string a = type == ShadingType.水平遮阳板 ? Ah.ToString("0.##") : Av.ToString("0.##");
            string b = type == ShadingType.水平遮阳板 ? Bh.ToString("0.##") : Bv.ToString("0.##");
            string pfDisp = r.PF.ToString("0.####");
            string sdDisp = r.SD.ToString("0.####");

            string head =
                $"水平遮阳板的外遮阳系数应按下列公式计算确定:      水平遮阳板：SDh=ahPF2+bhPF+1 A.0.1-1      " +
                $"垂直遮阳板：SDv=avPF2+bvPF+1 A.0.1-2      遮阳板外挑系数：PF=A/B 　　  A.0.1-3  " +
                $"公式中:      SDh：水平遮阳板夏季外遮阳系数；      SDv：垂直遮阳板夏季外遮阳系数；      " +
                $"ah,bh,av,bv：计算系数，按规范GB50189-2005表A.0.1选择；      PF：遮阳板外挑系数，当计算得出的PF>1时，取PF=1；      " +
                $"A：遮阳板外挑长度；      B：遮阳板根部到窗对边距离；  ";

            string calc =
                $"此处计算的是{kind}：      PF=A/B      　={A}/{B}        ={pfDisp}  因此，PF取{pfDisp}  " +
                $"遮阳板的夏季外遮阳系数为:      {sdName}={ca}PF2+{cb}PF+1         " +
                $"={a}*{pfDisp}2+({b})*{pfDisp}+1         ={sdDisp}  ";

            string tail =
                $"特别说明:      水平遮阳板和垂直遮阳板组合成的综合遮阳，其外遮阳系数值应取水平遮阳板和垂直遮阳板的外遮阳系数的乘积；      " +
                $"软件计算的遮阳系数是一块遮阳板构件情况下的外遮阳系数，多个遮阳板的外遮阳系数为各个外遮阳板的外遮阳系数的乘积；      " +
                $"软件计算的遮阳系数为建筑外遮阳系数，实际幕墙（门窗）的遮阳系数=玻璃的遮阳系数*外遮阳的遮阳系数；      " +
                $"计算依据为：《公共建筑节能设计标准》GB50189-2005附录A，该规范新版本GB50189-2015已经换用太阳得热系数替代遮阳系数，所以本计算仅供参考。  ";

            return $"{Sep}  {head}{Sep}  {calc}{Sep}  {tail}{Sep}";
        }
    }
}
