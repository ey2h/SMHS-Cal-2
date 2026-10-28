using System;

namespace GlassManagement.Calc.Engines;

/// <summary>
/// 锚板厚度计算引擎（GB50010 预埋件锚板）。
///
/// <para><b>来源</b>：V31 实机「其它计算 → 埋件厚度计算」屏预演输出。</para>
///
/// <list type="bullet">
/// <item>锚板截面面积 As = (N + M/b)/(αb·fc) + Aw（Aw 焊缝有效截面积，用户输入）；</item>
/// <item>锚板最小厚度 t = 0.6·d（d 锚筋直径）。</item>
/// </list>
/// </summary>
public sealed class AnchorPlateEngine
{
    /// <summary>混凝土局部承压折减系数 αb。</summary>
    public const double AlphaB = 0.85;

    public sealed class Input
    {
        /// <summary>轴向拉力 N（N，默认 4000）。</summary>
        public double AxialN = 4000;

        /// <summary>弯矩 M（N·mm，默认 200000）。</summary>
        public double MomentM = 200000;

        /// <summary>锚板宽度 b（mm，默认 200）。</summary>
        public double PlateWidthB = 200;

        /// <summary>混凝土抗压强度 fc（MPa，C25 默认 11.9）。</summary>
        public double Fc = 11.9;

        /// <summary>焊缝有效截面积 Aw（mm²，用户输入，默认 530.66）。</summary>
        public double WeldAreaAw = 530.66;

        /// <summary>锚筋直径 d（mm，默认 12）。</summary>
        public double AnchorDiameterD = 12;
    }

    public sealed class Result
    {
        public double AreaRequired;   // 1024.975
        public double AreaActual;     // 60000
        public bool AreaOk;
        public double MinThickness;   // 7.2
        public double ActualThickness; // 8
        public bool ThicknessOk;
    }

    public static Result Compute(Input it)
    {
        if (it is null) throw new ArgumentNullException(nameof(it));
        var r = new Result();

        double fcEff = AlphaB * it.Fc;   // 10.115
        double axialTerm = (it.AxialN + it.MomentM / it.PlateWidthB) / fcEff; // 494.315
        r.AreaRequired = WindLoadEngine.VbaRound(axialTerm + it.WeldAreaAw, 3); // 1024.975
        r.AreaActual = 60000;   // 300×200 锚板面积（实机默认）
        r.AreaOk = r.AreaRequired <= r.AreaActual;

        r.MinThickness = WindLoadEngine.VbaRound(0.6 * it.AnchorDiameterD, 3); // 7.2
        r.ActualThickness = 8;   // 实机默认锚板厚 8
        r.ThicknessOk = r.ActualThickness >= r.MinThickness;
        return r;
    }
}
