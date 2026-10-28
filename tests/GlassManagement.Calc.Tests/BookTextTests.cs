using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GlassManagement.Calc.Book;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 守护「计算书正文逐字来自程序样本」这条线。
///
/// <para>做法：用样本工程自己的参数值套 <see cref="BookText"/> 模板，
/// 输出必须与被切分出来的样本原文**逐行完全一致**（含缩进、全角标点、分号）。
/// 一旦有人改了模板里的一个字，这条测试立刻红。</para>
///
/// <para>样本原文：<c>docs/recon/calcbooks/chapters/&lt;类型&gt;/&lt;NN&gt;-&lt;章名&gt;.txt</c>，
/// 来自 V31 自带 <c>计算书样本文件集/*.DOC</c> 的 Word COM 转录 + 按章切分。</para>
/// </summary>
public class BookTextTests
{
    private static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "docs")))
            d = d.Parent;
        if (d is null) throw new InvalidOperationException("找不到仓库根（含 docs 目录）");
        return d.FullName;
    }

    /// <summary>样本工程的值（8 份样本都是同一 demo 工程：北京 / C类 / 8度 / 0.2g / Ⅱ类 / Fa=1 / αmax=0.16）。</summary>
    private static BookTextVars SampleVars() => new()
    {
        Subject = "幕墙",
        Region = "北京地区",
        Terrain = "C",
        SeismicIntensity = "8",
        PeakAccel = "0.2",
        SeismicCategory = "标准设防类",
        AlphaMaxII = "0.16",
        SiteClass = "Ⅱ",
        Fa = "1",
        AlphaMax = "0.16",
    };

    private static string[] SampleLines(string kind, string fileName)
    {
        var p = Path.Combine(RepoRoot(), "docs", "recon", "calcbooks", "chapters", kind, fileName);
        Assert.True(File.Exists(p), "样本章节文件缺失：" + p);
        return File.ReadAllText(p).Replace("\r", "").TrimEnd('\n').Split('\n');
    }

    /// <summary>第 2 章「基本参数」：模板输出必须与样本原文逐行一致（跳过样本首行的章标题）。</summary>
    [Fact]
    public void Basic_全隐框_逐行等于样本原文()
    {
        var sample = SampleLines("全隐框玻璃幕墙", "02-基本参数.txt");
        // 样本首行是章标题「基本参数」，模板不含它（章标题由 Composer 输出 H2）
        Assert.Equal("基本参数", sample[0].Trim());

        var produced = BookText.Basic(SampleVars()).ToArray();
        var expected = sample.Skip(1).ToArray();

        Assert.Equal(expected.Length, produced.Length);
        for (int i = 0; i < expected.Length; i++)
            Assert.True(expected[i] == produced[i],
                $"第 {i + 1} 行不一致\n样本: {expected[i]}\n模板: {produced[i]}");
    }

    /// <summary>第 3 章「幕墙承受荷载计算」：模板输出必须与样本原文逐行一致（跳过章标题）。</summary>
    [Fact]
    public void Loads_全隐框_逐行等于样本原文()
    {
        var sample = SampleLines("全隐框玻璃幕墙", "03-幕墙承受荷载计算.txt");
        Assert.Equal("幕墙承受荷载计算", sample[0].Trim());

        var produced = BookText.Loads(SampleVars()).ToArray();
        var expected = sample.Skip(1).ToArray();

        Assert.Equal(expected.Length, produced.Length);
        for (int i = 0; i < expected.Length; i++)
            Assert.True(expected[i] == produced[i],
                $"第 {i + 1} 行不一致\n样本: [{expected[i]}]\n模板: [{produced[i]}]");
    }

    /// <summary>门窗样本用「门窗所在地区 / 门窗属于外围护构件」——主体词必须跟着类型变。</summary>
    [Fact]
    public void Basic_门窗_主体词随类型替换()
    {
        var v = SampleVars() with { Subject = "门窗" };
        var lines = BookText.Basic(v);
        Assert.Equal("门窗所在地区", lines[0]);
        Assert.StartsWith("    门窗属于外围护构件", lines[3]);
        // 其余条文不动
        Assert.Contains("A类：指近海海面和海岛", lines[4]);
    }
}
