using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using GlassManagement.Calc.Book;
using GlassManagement.Calc.Engines;
using Xunit;

namespace GlassManagement.Calc.Tests;

/// <summary>
/// 计算书组装与导出的验收：
/// 1) 各章数值与引擎输出逐位一致（不是手写死的字符串）；
/// 2) 生成的是合法 docx（zip 内三件套齐全、document.xml 能解析）；
/// 3) 缺章不编造（未计算的屏写"本章未参与计算"）。
/// </summary>
public class CalcBookTests
{
    private static CalcBookInput FullInput() => new()
    {
        ProjectName = "全隐框玻璃幕墙（对拍工程）",
        Date = new DateTime(2026, 10, 26),
        WkKpa = 1.285,
        WkPanelKpa = 1.37,
        Connect = ConnectEngine.Compute(ConnectEngine.DefaultInputs()),
        Embed = EmbedEngine.Compute(EmbedEngine.DefaultInputs()),
        Sealant = SealantEngine.Compute(SealantEngine.DefaultInputs()),
        ColumnSummary = "风荷载标准值为:1.285KPa\n抗弯矩预选:17675mm3\n满足要求(√)",
        BeamSummary = "风荷载标准值为:1.285KPa\n抗弯矩预选Wx:904.139mm3\n满足要求(√)",
        GlassSummary = "玻璃部分风荷载标准值为:1.37KPa\n玻璃强度计算值:24.793MPa\n满足要求(√)",
    };

    private static string AllText(CalcBookInput x)
        => string.Join("\n", CalcBookComposer.Compose(x).Select(b => b.Text));

    [Fact]
    public void 各章数值与引擎一致()
    {
        var x = FullInput();
        var text = AllText(x);

        // 连接
        Assert.Contains("连接螺栓设计值：0.166 个", text);
        Assert.Contains("横梁壁抗承压能力设计值：6660.000 N", text);
        Assert.Contains("实际型材受力：633.889 N", text);
        Assert.Contains("角码壁抗承压能力设计值：11100.000 N", text);
        Assert.Contains("连接螺栓设计值：0.228 个", text);
        Assert.Contains("实际型材受力：7304.379 N", text);

        // 埋件
        Assert.Contains("剪力设计值：1716.000 N", text);
        Assert.Contains("轴力设计值：7098.300 N", text);
        Assert.Contains("弯矩设计值：137280.000 N·mm", text);
        Assert.Contains("锚筋总截面积计算值：57.911 mm²", text);
        Assert.Contains("实际锚筋总截面积：452.160 mm²", text);
        Assert.Contains("基本锚固长度 lab：449.008 mm", text);
        Assert.Contains("折减后锚固长度 la：57.507 mm", text);
        Assert.Contains("转接件强度计算值：4.430 MPa", text);
        Assert.Contains("焊缝强度计算值：12.452 MPa", text);

        // 胶类
        Assert.Contains("粘接宽度最小值：6.121 mm", text);
        Assert.Contains("粘接厚度最小值：5.475 mm", text);
        Assert.Contains("计算宽度：7.705 mm", text);
        Assert.Contains("计算值：8.381 mm", text);
        Assert.Contains("压板最大弯曲应力：43.907 MPa", text);
        Assert.Contains("连接螺栓抗拉承载力设计值：2409.836 N", text);
    }

    /// <summary>立柱/横梁/玻璃三章由各自预演摘要填入，不能再是"本章未参与计算"。</summary>
    [Fact]
    public void 前五章都有内容()
    {
        var text = AllText(FullInput());
        Assert.Contains("抗弯矩预选:17675mm3", text);          // 立柱
        Assert.Contains("抗弯矩预选Wx:904.139mm3", text);      // 横梁
        Assert.Contains("玻璃强度计算值:24.793MPa", text);     // 玻璃
        Assert.DoesNotContain("三、立柱计算\n本章未参与计算", text);
        Assert.DoesNotContain("四、横梁计算\n本章未参与计算", text);
        Assert.DoesNotContain("五、玻璃计算\n本章未参与计算", text);
    }

    [Fact]
    public void 缺章不编造()
    {
        var x = new CalcBookInput { WkKpa = 0 };
        var text = AllText(x);
        Assert.Contains("本章未参与计算", text);
        Assert.DoesNotContain("连接螺栓设计值", text);
    }

    [Fact]
    public void 结论按验算结果给()
    {
        var text = AllText(FullInput());
        Assert.Contains("各项验算均满足规范要求。", text);
    }

    [Fact]
    public void 导出docx且结构合法()
    {
        var path = Path.Combine(Path.GetTempPath(), $"calcbook_test_{Guid.NewGuid():N}.docx");
        try
        {
            CalcBookWriter.Write(CalcBookComposer.Compose(FullInput()), path);
            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 1000, "docx 太小，可能没写进内容");

            using var zip = ZipFile.OpenRead(path);
            var names = zip.Entries.Select(e => e.FullName).ToList();
            Assert.Contains("[Content_Types].xml", names);
            Assert.Contains("_rels/.rels", names);
            Assert.Contains("word/document.xml", names);

            using var s = zip.GetEntry("word/document.xml")!.Open();
            using var sr = new StreamReader(s);
            var xml = sr.ReadToEnd();
            Assert.Contains("<w:document", xml);
            Assert.Contains("幕墙结构计算书", xml);
            Assert.Contains("57.911", xml);          // 埋件锚筋面积确实落进正文
            Assert.Contains("12.452", xml);          // 焊缝强度
            Assert.StartsWith("<?xml", xml);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
