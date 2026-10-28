using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace GlassManagement.DesktopTests;

/// <summary>
/// 界面样式治理门禁（见 docs/UI_STYLE_SYSTEM_PLAN.md）。
///
/// <para><b>守什么</b>：视图（Views/**/*.axaml）里禁止出现 <c>"#RRGGBB"</c> 颜色字面量，
/// 一律用 <c>{DynamicResource HbXxx}</c> 取 <c>Themes/Tokens.axaml</c> 的令牌。</para>
///
/// <para><b>为什么</b>：改造前 75 个视图文件全部含硬编码色，且并存两套色系
/// （V31 复刻的 #F0F0F0/#A0A0A0 与现代 Tailwind 的 #1E293B/#334155），
/// 改一次主题要改 400 余处。令牌化后换皮肤只改 Tokens.axaml 一处。</para>
///
/// <para><b>存量怎么办</b>：登记在 <c>docs/ui_style_exempt.txt</c>，按批次递减；
/// 新文件不在清单里就必须干净，否则本测试失败。清单清零即治理完成。</para>
/// </summary>
[TestFixture]
public class StyleGovernanceTests
{
    /// <summary>
    /// 覆盖所有会吃颜色的属性（含 <c>Color=</c> / <c>Stroke=</c> 与 8 位 ARGB）。
    /// 早期只查 Background/Foreground/BorderBrush/Fill，导致模板里的 Stroke/Fill 与
    /// BoxShadow 的 #AARRGGBB 全部漏网，故此处一并纳入。
    /// </summary>
    private static readonly Regex ColorLiteral = new(
        @"(?:Color|Background|Foreground|BorderBrush|Stroke|Fill)=""#[0-9A-Fa-f]{3,8}""|#[0-9A-Fa-f]{8}""",
        RegexOptions.Compiled);

    /// <summary>从测试程序集位置向上找到仓库根（含 docs 目录的那一级）。</summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "docs")) &&
                Directory.Exists(Path.Combine(dir.FullName, "src")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("找不到仓库根目录（含 docs 与 src 的那一级）");
    }

    private static string BuildDir() => Path.Combine(RepoRoot(), "src", "GlassManagement.Desktop");

    private static HashSet<string> LoadExempt()
    {
        var path = Path.Combine(RepoRoot(), "docs", "ui_style_exempt.txt");
        if (!File.Exists(path)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return File.ReadAllLines(path)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith("#"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    [Test]
    public void 视图不得新增硬编码颜色()
    {
        var baseDir = BuildDir();
        var viewsDir = Path.Combine(baseDir, "Views");
        Assert.That(Directory.Exists(viewsDir), Is.True, $"找不到视图目录 {viewsDir}");

        var exempt = LoadExempt();
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(viewsDir, "*.axaml", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(baseDir, file).Replace('\\', '/');
            if (exempt.Contains(rel)) continue;

            var text = File.ReadAllText(file);
            var m = ColorLiteral.Match(text);
            if (m.Success)
            {
                var line = text.Take(m.Index).Count(c => c == '\n') + 1;
                offenders.Add($"{rel}:{line}  {m.Value}");
            }
        }

        Assert.That(offenders, Is.Empty,
            "以下视图使用了硬编码颜色，请改用 {DynamicResource HbXxx}（令牌见 Themes/Tokens.axaml）：\n  "
            + string.Join("\n  ", offenders));
    }

    [Test]
    public void 令牌文件存在且覆盖全部必需语义()
    {
        var tokens = Path.Combine(BuildDir(), "Themes", "Tokens.axaml");
        Assert.That(File.Exists(tokens), Is.True, "缺少 Themes/Tokens.axaml");

        var text = File.ReadAllText(tokens);
        // 这些是界面必须依赖的核心语义，缺任何一个都会让样式 Setter 解析为空。
        var required = new[]
        {
            "HbFormBg", "HbControlBg", "HbResultBg", "HbStatusBarBg", "HbTitleBarBg",
            "HbBorder", "HbBorderStrong", "HbText", "HbTextMuted", "HbTextDisabled",
            "HbTitleText", "HbButtonBg", "HbHover", "HbPressed", "HbAccent",
            "HbSuccess", "HbDanger", "HbWarnBg",
        };
        var missing = required.Where(k => !text.Contains($"x:Key=\"{k}\"")).ToArray();
        Assert.That(missing, Is.Empty, "令牌文件缺少：" + string.Join("、", missing));
    }

    [Test]
    public void 集中样式不得回退为颜色字面量()
    {
        var app = Path.Combine(BuildDir(), "App.axaml");
        Assert.That(File.Exists(app), Is.True);
        var text = File.ReadAllText(app);

        var hits = Regex.Matches(text, @"(Color|Background|Foreground|BorderBrush|Stroke|Fill)=""#[0-9A-Fa-f]{3,8}""|#[0-9A-Fa-f]{8}""")
            .Select(m => m.Value).ToArray();
        Assert.That(hits, Is.Empty,
            "App.axaml 的样式 Setter 必须引用令牌而非字面量，残留：" + string.Join("、", hits));

        Assert.That(text.Contains("{DynamicResource Hb"), Is.True,
            "App.axaml 未引用任何令牌，令牌层未生效");
    }

    [Test]
    public void 豁免清单只减不增()
    {
        var exempt = LoadExempt();
        Assert.That(exempt.Count, Is.LessThanOrEqualTo(74),
            $"豁免清单出现回潮（当前 {exempt.Count} 项，上限 74）。" +
            "新增文件请直接用令牌，不要往清单里加。");
    }
}
