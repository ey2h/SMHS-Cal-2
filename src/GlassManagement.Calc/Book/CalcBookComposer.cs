using System;
using System.Collections.Generic;
using System.Globalization;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Calc.Book;

/// <summary>计算书组装所需的各屏结果（缺失的屏按"未计算"标注）。</summary>
public sealed class CalcBookInput
{
    public string ProjectName { get; init; } = "全隐框玻璃幕墙";
    public DateTime Date { get; init; } = DateTime.Today;

    public double WkKpa { get; init; }
    public double WkPanelKpa { get; init; }
    public string WindSource { get; init; } = "";

    public ConnectResult? Connect { get; init; }
    public EmbedResult? Embed { get; init; }
    public SealantResult? Sealant { get; init; }

    /// <summary>玻璃屏结果（可选，缺失则不出该章）。</summary>
    public string? GlassSummary { get; init; }
    /// <summary>立柱屏结果（可选）。</summary>
    public string? ColumnSummary { get; init; }
    /// <summary>横梁屏结果（可选）。</summary>
    public string? BeamSummary { get; init; }
}

/// <summary>
/// 把各屏结果组装成计算书块序列（Markdown 风格的纯文本分层）。
///
/// 章节顺序与 V31 的"生成计算书"一致：封面 → 风荷载 → 立柱 → 横梁 → 玻璃 → 连接 → 埋件 → 胶类。
/// 每章只写**已算出的量**，缺的章节不编造，直接标注"本章未参与计算"。
/// </summary>
public static class CalcBookComposer
{
    private static string N(double v, int digits = 3)
        => v.ToString("F" + digits, CultureInfo.InvariantCulture);

    public static IReadOnlyList<BookBlock> Compose(CalcBookInput x)
    {
        var b = new List<BookBlock>
        {
            BookBlock.H1("幕墙结构计算书"),
            BookBlock.P($"工程名称：{x.ProjectName}"),
            BookBlock.P($"计算日期：{x.Date:yyyy-MM-dd}"),
            BookBlock.P("计算软件：SMHS 幕墙门窗结构计算软件（复刻版）"),
            BookBlock.PageBreak(),

            BookBlock.H2("一、设计依据"),
            BookBlock.P("1. 《建筑结构荷载规范》GB 50009-2012"),
            BookBlock.P("2. 《玻璃幕墙工程技术规范》JGJ 102-2003"),
            BookBlock.P("3. 《混凝土结构设计规范》GB 50010-2010"),
            BookBlock.P("4. 《钢结构设计标准》GB 50017-2017"),
            BookBlock.PageBreak(),

            BookBlock.H2("二、风荷载"),
        };

        if (x.WkKpa > 0)
        {
            b.Add(BookBlock.P($"风荷载标准值（支承结构口径）：{N(x.WkKpa)} KPa"));
            if (x.WkPanelKpa > 0)
                b.Add(BookBlock.P($"风荷载标准值（面板口径）：{N(x.WkPanelKpa)} KPa"));
            if (!string.IsNullOrWhiteSpace(x.WindSource))
                b.Add(BookBlock.P($"取值来源：{x.WindSource}"));
        }
        else
        {
            b.Add(BookBlock.P("本章未参与计算（第一页未取风荷载）。"));
        }

        b.Add(BookBlock.PageBreak());
        b.Add(BookBlock.H2("三、立柱计算"));
        b.Add(string.IsNullOrWhiteSpace(x.ColumnSummary)
            ? BookBlock.P("本章未参与计算。")
            : BookBlock.P(x.ColumnSummary));

        b.Add(BookBlock.PageBreak());
        b.Add(BookBlock.H2("四、横梁计算"));
        b.Add(string.IsNullOrWhiteSpace(x.BeamSummary)
            ? BookBlock.P("本章未参与计算。")
            : BookBlock.P(x.BeamSummary));

        b.Add(BookBlock.PageBreak());
        b.Add(BookBlock.H2("五、玻璃计算"));
        b.Add(string.IsNullOrWhiteSpace(x.GlassSummary)
            ? BookBlock.P("本章未参与计算。")
            : BookBlock.P(x.GlassSummary));

        // ── 连接 ──
        b.Add(BookBlock.PageBreak());
        b.Add(BookBlock.H2("六、连接计算"));
        if (x.Connect is { } c)
        {
            b.Add(BookBlock.P(
                "1. 横梁与角码\n"
                + $"   连接螺栓设计值：{N(c.BeamBoltDesign)} 个\n"
                + $"   横梁壁抗承压能力设计值：{N(c.BeamWebBearing)} N\n"
                + $"   实际型材受力：{N(c.BeamForce)} N\n"
                + $"   {(c.BeamBoltDesign <= 2 ? "满足要求(√)" : "不满足要求(×)")}"));
            b.Add(BookBlock.P(
                "2. 立柱与角码\n"
                + $"   连接螺栓设计值：{N(c.ColumnBoltDesign)} 个\n"
                + $"   立柱壁抗承压能力设计值：{N(c.ColumnWebBearing)} N\n"
                + $"   立柱与横梁连接处角码壁抗承压能力设计值：{N(c.BracketBearing)} N\n"
                + $"   实际型材受力：{N(c.ColumnForce)} N\n"
                + $"   {(c.ColumnBoltDesign <= 2 ? "满足要求(√)" : "不满足要求(×)")}"));
            b.Add(BookBlock.P(
                "3. 立柱与主体（钢转接件）\n"
                + $"   连接螺栓设计值：{N(c.MainBoltDesign)} 个\n"
                + $"   立柱壁抗承压能力设计值：{N(c.MainColumnBearing)} N\n"
                + $"   角码壁抗承压能力设计值：{N(c.MainBracketBearing)} N\n"
                + $"   实际型材受力：{N(c.MainForce)} N\n"
                + $"   {(c.MainBoltDesign <= 2 ? "满足要求(√)" : "不满足要求(×)")}"));
        }
        else b.Add(BookBlock.P("本章未参与计算。"));

        // ── 埋件与焊缝 ──
        b.Add(BookBlock.PageBreak());
        b.Add(BookBlock.H2("七、埋件与焊缝计算"));
        if (x.Embed is { } e)
        {
            b.Add(BookBlock.P(
                "1. 内力\n"
                + $"   剪力设计值：{N(e.ShearDesign)} N\n"
                + $"   轴力设计值：{N(e.AxialDesign)} N\n"
                + $"   弯矩设计值：{N(e.MomentDesign)} N·mm"));
            b.Add(BookBlock.P(
                "2. 预埋件锚筋\n"
                + $"   锚筋总截面积计算值：{N(e.RebarAreaRequired)} mm²\n"
                + $"   实际锚筋总截面积：{N(e.RebarAreaActual)} mm²\n"
                + $"   {(e.RebarAreaRequired <= e.RebarAreaActual ? "满足要求(√)" : "不满足要求(×)")}\n"
                + $"   0.5fcA：{N(e.ConcreteBearing)} N\n"
                + $"   法向压力设计值：{N(e.AxialDesign)} N\n"
                + $"   {(e.AxialDesign <= e.ConcreteBearing ? "满足要求(√)" : "不满足要求(×)")}"));
            b.Add(BookBlock.P(
                "3. 锚固长度（GB 50010）\n"
                + $"   基本锚固长度 lab：{N(e.AnchorLab)} mm\n"
                + $"   折减后锚固长度 la：{N(e.AnchorLa)} mm\n"
                + "   抗震设计的幕墙锚筋锚固长度不得小于 200 mm；锚固长度不应小于 15 倍锚筋直径。"));
            b.Add(BookBlock.P(
                "4. 转接件与焊缝\n"
                + $"   转接件强度计算值：{N(e.TransferStress)} MPa（设计值 {N(e.TransferStressLimit)} MPa）\n"
                + $"   转接件挠度计算值：{N(e.TransferDeflection, 6)} mm（设计值 {N(e.TransferDeflLimit)} mm）\n"
                + $"   焊缝强度计算值：{N(e.WeldStress)} MPa（设计值 {N(e.WeldStressLimit)} MPa）"));
        }
        else b.Add(BookBlock.P("本章未参与计算。"));

        // ── 胶类 ──
        b.Add(BookBlock.PageBreak());
        b.Add(BookBlock.H2("八、胶类、伸缩及紧固计算"));
        if (x.Sealant is { } s)
        {
            b.Add(BookBlock.P(
                "1. 结构胶\n"
                + $"   面板部分风荷载标准值：{N(s.WkPanelKpa)} KPa\n"
                + $"   粘接宽度最小值：{N(s.StructuralWidth)} mm；实际取 {N(s.ActualWidth, 0)} mm\n"
                + $"   粘接厚度最小值：{N(s.StructuralThickness)} mm；实际取 {N(s.ActualThickness, 0)} mm\n"
                + "   按 JGJ 102-2003 第 5.6.1 条：粘接宽度≥7 mm；12 mm≥粘接厚度≥6 mm；"
                + "粘接宽度大于厚度且不宜大于厚度的 2 倍。"));
            b.Add(BookBlock.P(
                "2. 耐候胶胶缝\n"
                + $"   计算宽度：{N(s.WeatherWidth)} mm；实际 {N(s.WeatherActual, 0)} mm\n"
                + $"   {(s.WeatherWidth <= s.WeatherActual ? "满足要求(√)" : "不满足要求(×)")}"));
            b.Add(BookBlock.P(
                "3. 立柱伸缩缝\n"
                + $"   计算值：{N(s.JointWidth)} mm；实际 {N(s.JointActual, 0)} mm\n"
                + $"   {(s.JointWidth <= s.JointActual ? "满足要求(√)" : "不满足要求(×)")}"));
            b.Add(BookBlock.P(
                "4. 压板与连接螺栓\n"
                + $"   压板最大弯曲应力：{N(s.ClampBending)} MPa（设计值 {N(s.ClampBendingLimit)} MPa）\n"
                + $"   压板最大剪应力：{N(s.ClampShear)} MPa（设计值 {N(s.ClampShearLimit)} MPa）\n"
                + $"   连接螺栓抗拉承载力设计值：{N(s.BoltCapacity)} N\n"
                + $"   连接螺栓实际受力：{N(s.BoltForce)} N\n"
                + $"   {(s.BoltForce <= s.BoltCapacity ? "满足要求(√)" : "不满足要求(×)")}"));
        }
        else b.Add(BookBlock.P("本章未参与计算。"));

        b.Add(BookBlock.PageBreak());
        b.Add(BookBlock.H2("九、结论"));
        b.Add(BookBlock.P(BuildConclusion(x)));

        return b;
    }

    private static string BuildConclusion(CalcBookInput x)
    {
        var fails = new List<string>();
        if (x.Connect is { } c)
        {
            if (c.BeamBoltDesign > 2) fails.Add("横梁与角码连接螺栓");
            if (c.ColumnBoltDesign > 2) fails.Add("立柱与角码连接螺栓");
            if (c.MainBoltDesign > 2) fails.Add("立柱与主体连接螺栓");
            if (c.BeamForce > c.BeamWebBearing) fails.Add("横梁壁抗承压");
            if (c.ColumnForce > c.BracketBearing) fails.Add("角码壁抗承压");
            if (c.MainForce > c.MainBracketBearing) fails.Add("主体角码壁抗承压");
        }
        if (x.Embed is { } e)
        {
            if (e.RebarAreaRequired > e.RebarAreaActual) fails.Add("预埋件锚筋");
            if (e.TransferStress > e.TransferStressLimit) fails.Add("转接件强度");
            if (e.TransferDeflection > e.TransferDeflLimit) fails.Add("转接件挠度");
            if (e.WeldStress > e.WeldStressLimit) fails.Add("焊缝强度");
        }
        if (x.Sealant is { } s)
        {
            if (s.WeatherWidth > s.WeatherActual) fails.Add("耐候胶胶缝");
            if (s.JointWidth > s.JointActual) fails.Add("立柱伸缩缝");
            if (s.BoltForce > s.BoltCapacity) fails.Add("压板连接螺栓");
        }

        return fails.Count == 0
            ? "各项验算均满足规范要求。"
            : "以下项目不满足要求，需调整设计：" + string.Join("、", fails) + "。";
    }
}
