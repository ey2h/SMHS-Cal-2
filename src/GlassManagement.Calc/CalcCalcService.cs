using GlassManagement.Calc.Engines;
using GlassManagement.Calc.Guifan;

namespace GlassManagement.Calc;

/// <summary>
/// 原程序计算逻辑复刻门面（Facade）。
/// 当前实现荷载效应组合引擎（guifan.dat 驱动，规范相关 γE）。
/// 后续可按相同模式补充玻璃面板、立柱双跨梁、结构胶等模块。
/// </summary>
public sealed class CalcService
{
    public GuifanData Guifan { get; }
    public LoadCombinationEngine LoadCombination { get; }

    public CalcService(GuifanData guifan)
    {
        Guifan = guifan;
        LoadCombination = new LoadCombinationEngine(guifan);
    }

    /// <summary>
    /// 使用内嵌的 guifan 系数表（随软件发布，无需依赖原程序安装目录）。
    /// </summary>
    public static CalcService CreateDefault()
    {
        var guifan = LoadEmbeddedGuifan();
        return new CalcService(guifan);
    }

    /// <summary>
    /// 从原程序安装目录的 guifan.dat 加载（GBK）；失败时回退到内嵌表。
    /// </summary>
    public static CalcService CreateFromInstall(string guifanDatPath)
    {
        GuifanData guifan;
        try
        {
            guifan = GuifanData.LoadFromDat(guifanDatPath);
        }
        catch
        {
            guifan = LoadEmbeddedGuifan();
        }
        return new CalcService(guifan);
    }

    private static GuifanData LoadEmbeddedGuifan()
    {
        var asm = typeof(CalcService).Assembly;
        using var stream = asm.GetManifestResourceStream(
            "GlassManagement.Calc.Guifan.guifan-embedded.txt")
            ?? throw new InvalidOperationException("内嵌 guifan 资源缺失");
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return GuifanData.Parse(reader.ReadToEnd());
    }
}
