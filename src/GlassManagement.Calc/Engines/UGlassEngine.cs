using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// U型玻璃「U型玻璃结构设计计算」屏引擎（V31 客户区 797×521）。
///
/// <para><b>模型</b>：U型玻璃是线构件（简支梁），单位长度线荷载 q=wk×翼宽，
/// 截面为 U 型（翼宽×翼高×厚）。算式来源：V31 实机 var 轨迹 <c>uboli_var.json</c>。</para>
///
/// <list type="bullet">
/// <item>线荷载 q = wk×翼宽（round4）；</item>
/// <item>qd' = 5·αmax·(γ/1e6)·截面面积 A（round4）；</item>
/// <item>q设计 = 1.5·q + 0.7·qd'（round3）；</item>
/// <item>M = q设计·L²/8（简支梁跨中弯矩）；</item>
/// <item>σ = M/W + γ·L（弯矩应力 + 自重应力）；</item>
/// <item>挠度 = 5·q·L⁴/(384·E·I)；</item>
/// <item>挠度设计值 = L/150。</item>
/// </list>
/// </summary>
public sealed class UGlassEngine
{
    /// <summary>U 型玻璃截面参数（翼宽×翼高×厚 → 面积/抵抗矩/惯性矩）。</summary>
    public readonly record struct USection(double WidthMm, double HeightMm, double ThicknessMm,
                                            double AreaMm2, double Wmm3, double Imm4, double EMpa);

    /// <summary>U 玻璃截面库（默认 U玻璃234*41*6）。
    /// 来源=参数库「U型玻璃」表行 1（hb_mdb/U型玻璃.csv）：截面积 1812.412 mm² 原文；
    /// W=翼板抗弯矩 5.49 cm³→5490 mm³、I=截面惯性矩 Ix 18.23 cm⁴→182300 mm⁴（cm→mm ×1e3/×1e4）；
    /// E=70000 运行期挠度链除数直读（uboli_var.json）。表内其余 6 行可同法换算扩展。</summary>
    public static readonly USection[] Sections =
    {
        new(234, 41, 6, 1812.412, 5490, 182300, 70000),
    };

    public sealed class Input
    {
        /// <summary>U 玻璃翼宽（mm，默认 234）。</summary>
        public double WidthMm = 234;

        /// <summary>截面面积 A（mm²，默认 1812.412）。</summary>
        public double AreaMm2 = 1812.412;

        /// <summary>截面抵抗矩 W（mm³，默认 5490）。</summary>
        public double Wmm3 = 5490;

        /// <summary>截面惯性矩 I（mm⁴，默认 182300）。</summary>
        public double Imm4 = 182300;

        /// <summary>U 玻璃弹性模量 E（N/mm²，默认 70000）。</summary>
        public double EMpa = 70000;

        /// <summary>U 玻璃跨度 L（mm，默认 2500）。</summary>
        public double SpanMm = 2500;

        /// <summary>地震影响系数最大值 αmax（默认 0.16）。</summary>
        public double AlphaMax = 0.16;

        /// <summary>风荷载标准值 wk（N/mm²，默认 0.00137）。</summary>
        public double Wk = 0.00137;
    }

    public sealed class Result
    {
        public double WindStandardKpa;        // 1.37
        public double LineLoad;               // q = 0.3206
        public double SeismicLoad;            // qd' = 0.0371
        public double DesignLoad;             // q设计 = 0.507
        public double Moment;                 // M = 396093.75
        public double Sigma;                  // 72.212
        public double SigmaAllowable;         // 51
        public bool SigmaOk;
        public double Deflection;             // 12.778
        public double DeflectionAllowable;    // 16.667
        public bool DeflectionOk;
    }

    public static Result Compute(Input it)
    {
        if (it is null) throw new ArgumentNullException(nameof(it));
        var r = new Result
        {
            WindStandardKpa = WindLoadEngine.VbaRound(it.Wk * 1000.0, 2),
        };

        double dens = GlassEngine.DensityNmm3;   // γ/1e6 = 25.6/1e6
        double L = it.SpanMm;

        // q = wk×翼宽
        r.LineLoad = WindLoadEngine.VbaRound(it.Wk * it.WidthMm, 4);   // 0.3206
        // qd' = 5·αmax·(γ/1e6)·A
        r.SeismicLoad = WindLoadEngine.VbaRound(5.0 * it.AlphaMax * dens * it.AreaMm2, 4); // 0.0371
        // q设计 = 1.5·q + 0.7·qd'
        r.DesignLoad = WindLoadEngine.VbaRound(1.5 * r.LineLoad + 0.7 * r.SeismicLoad, 3); // 0.507
        // M = q设计·L²/8
        r.Moment = WindLoadEngine.VbaRound(r.DesignLoad * L * L / 8.0, 3);  // 396093.75
        // σ = M/W + γ·L
        r.Sigma = WindLoadEngine.VbaRound(r.Moment / it.Wmm3 + dens * L, 3); // 72.212
        r.SigmaAllowable = 51;
        r.SigmaOk = r.Sigma <= r.SigmaAllowable;

        // 挠度 = 5·q·L⁴/(384·E·I)
        r.Deflection = WindLoadEngine.VbaRound(
            5.0 * r.LineLoad * Math.Pow(L, 4.0) / (384.0 * it.EMpa * it.Imm4), 3); // 12.778
        r.DeflectionAllowable = WindLoadEngine.VbaRound(L / 150.0, 3); // 16.667
        r.DeflectionOk = r.Deflection <= r.DeflectionAllowable;

        return r;
    }
}
