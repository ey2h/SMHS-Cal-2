using System.Text;

namespace GlassManagement.Calc.Guifan;

/// <summary>
/// 荷载组合分项系数表（原程序 guifan.dat）。
/// 每条记录格式（GBK 文本，逗号分隔）：
///    规范名称, ψE(地震作用组合值系数), γE(地震作用分项系数), ψw(风荷载组合值系数)
/// 原程序按"所选项目规范"查此表决定荷载组合中的 γE，而 γG/γw 与 ψw/ψE 的场景取值
/// 由 JGJ102-2003 第 5.4 节固定（风为主 / 地震为主 / 永久为主三档）。
/// 经逆向（计算书 表3-2 与 guifan.dat 逐条比对）确认：guifan 中间数恒为 γE
/// （JGJ102=1.4，广东地标=1.5），首/尾为 (ψE, ψw)。
/// </summary>
public sealed record GuifanEntry(string StandardName, double PsiE, double GammaE, double PsiW);

public sealed class GuifanData
{
    private readonly Dictionary<string, GuifanEntry> _entries;

    private GuifanData(Dictionary<string, GuifanEntry> entries)
    {
        _entries = entries;
    }

    /// <summary>JGJ102-2003 默认值（原程序无匹配规范时的回退）。</summary>
    public static readonly GuifanEntry Default = new("玻璃幕墙规范JGJ102-2003", 0.5, 1.4, 1.0);

    /// <summary>所有规范条目。</summary>
    public IReadOnlyCollection<GuifanEntry> All => _entries.Values;

    /// <summary>取规范对应条目；未命中返回 JGJ102 默认。</summary>
    public GuifanEntry GetOrDefault(string? standardName)
    {
        if (!string.IsNullOrWhiteSpace(standardName)
            && _entries.TryGetValue(standardName!, out var e))
        {
            return e;
        }
        return Default;
    }

    public bool TryGet(string standardName, out GuifanEntry entry) =>
        _entries.TryGetValue(standardName, out entry!);

    /// <summary>从 guifan.dat 文件路径加载（GBK 编码）。</summary>
    public static GuifanData LoadFromDat(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var encoding = GetGbkEncoding();
        return Parse(encoding.GetString(bytes));
    }

    /// <summary>从文本加载（用于测试/内嵌资源）。</summary>
    public static GuifanData Parse(string content)
    {
        var dict = new Dictionary<string, GuifanEntry>(StringComparer.Ordinal);
        foreach (var raw in content.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            // 规范名, ψE, γE, ψw
            var parts = line.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) continue; // 跳过 "工程实际情况" 等无系数行
            if (!double.TryParse(parts[1], out var psiE)) continue;
            if (!double.TryParse(parts[2], out var gammaE)) continue;
            if (!double.TryParse(parts[3], out var psiW)) continue;
            var name = parts[0].Trim();
            if (name.Length == 0) continue;
            dict[name] = new GuifanEntry(name, psiE, gammaE, psiW);
        }
        return new GuifanData(dict);
    }

    /// <summary>从内嵌资源 guifan-embedded.txt 加载（随软件发布，运行时无需依赖原程序安装目录）。</summary>
    public static GuifanData LoadEmbedded()
    {
        var asm = typeof(GuifanData).Assembly;
        using var stream = asm.GetManifestResourceStream("GlassManagement.Calc.Guifan.guifan-embedded.txt")
                         ?? throw new InvalidOperationException("内嵌 guifan 资源缺失");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return Parse(reader.ReadToEnd());
    }

    private static Encoding GetGbkEncoding()
    {
        // GBK/GB2312 需 CodePages 提供
        try
        {
            var enc = Encoding.GetEncoding("gbk");
            return enc;
        }
        catch (ArgumentException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding("gbk");
        }
    }
}
