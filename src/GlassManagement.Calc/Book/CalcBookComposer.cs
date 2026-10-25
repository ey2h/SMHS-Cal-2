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
    private static string Yes(bool ok) => ok ? "满足要求(√)" : "不满足要求(×)";

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
            b.Add(BookBlock.Table(
                new[] { "连接部位", "螺栓设计值(个)", "实际用(个)", "被连接件壁抗承压(N)", "角码壁抗承压(N)", "实际受力(N)", "判定" },
                new[]
                {
                    new[] { "横梁与角码", N(c.BeamBoltDesign), "2", N(c.BeamWebBearing), "—",
                            N(c.BeamForce), Yes(c.BeamBoltDesign <= 2 && c.BeamForce <= c.BeamWebBearing) },
                    new[] { "立柱与角码", N(c.ColumnBoltDesign), "2", N(c.ColumnWebBearing), N(c.BracketBearing),
                            N(c.ColumnForce), Yes(c.ColumnBoltDesign <= 2 && c.ColumnForce <= c.BracketBearing) },
                    new[] { "立柱与主体(钢转接件)", N(c.MainBoltDesign), "2", N(c.MainColumnBearing), N(c.MainBracketBearing),
                            N(c.MainForce), Yes(c.MainBoltDesign <= 2 && c.MainForce <= c.MainBracketBearing) },
                }));
            b.Add(BookBlock.P(
                $"注：螺栓单栓承载力按设计值取用（横梁/立柱与角码 {N(c.BeamBoltCapacity)} N，"
                + $"立柱与主体 {N(c.MainBoltCapacity)} N），螺栓设计值 = 实际受力 / 单栓承载力。"));
        }
        else b.Add(BookBlock.P("本章未参与计算。"));

        // ── 埋件与焊缝 ──
        b.Add(BookBlock.PageBreak());
        b.Add(BookBlock.H2("七、埋件与焊缝计算"));
        if (x.Embed is { } e)
        {
            b.Add(BookBlock.H3("7.1 内力设计值"));
            b.Add(BookBlock.Table(
                new[] { "项目", "数值", "单位" },
                new[]
                {
                    new[] { "剪力设计值 V", N(e.ShearDesign), "N" },
                    new[] { "轴力设计值 N", N(e.AxialDesign), "N" },
                    new[] { "弯矩设计值 M", N(e.MomentDesign), "N·mm" },
                }));

            b.Add(BookBlock.H3("7.2 预埋件锚筋"));
            b.Add(BookBlock.Table(
                new[] { "项目", "计算值", "实配 / 容许值", "判定" },
                new[]
                {
                    new[] { "锚筋总截面积", N(e.RebarAreaRequired) + " mm²", N(e.RebarAreaActual) + " mm²",
                            Yes(e.RebarAreaRequired <= e.RebarAreaActual) },
                    new[] { "0.5·fc·A", N(e.ConcreteBearing) + " N", "法向压力 " + N(e.AxialDesign) + " N",
                            Yes(e.AxialDesign <= e.ConcreteBearing) },
                }));
            b.Add(BookBlock.P($"锚筋层数影响系数 αr = {N(e.AlphaR)}；锚板厚度影响系数 αb = {N(e.AlphaB)}。"
                + "锚筋截面积取「V+N+M/(1.3αb·fy·z)」与「N+M/(0.4αb·fy·z)」两式的较大值。"));

            b.Add(BookBlock.H3("7.3 锚固长度（GB 50010）"));
            b.Add(BookBlock.Table(
                new[] { "项目", "数值", "单位" },
                new[]
                {
                    new[] { "基本锚固长度 lab", N(e.AnchorLab), "mm" },
                    new[] { "折减后锚固长度 la", N(e.AnchorLa), "mm" },
                }));
            b.Add(BookBlock.P(
                "锚筋末端采用弯钩或机械锚固措施时，包括弯钩和锚固端头在内的锚固长度（投影长度）"
                + "可取基本锚固长度的 60%，弯钩与机械锚固的形式和技术要求应符合 GB 50010 规定。"
                + "抗震设计的幕墙锚筋锚固长度不得小于 200 mm；锚固长度不应小于 15 倍锚筋直径；"
                + "当锚筋配置较多、实配面积超过计算所需 1.4 倍时，锚固长度可适当减少，但不应小于 180 mm。"));

            b.Add(BookBlock.H3("7.4 转接件与焊缝"));
            b.Add(BookBlock.Table(
                new[] { "项目", "计算值", "设计值", "判定" },
                new[]
                {
                    new[] { "转接件强度", N(e.TransferStress) + " MPa", N(e.TransferStressLimit) + " MPa",
                            Yes(e.TransferStress <= e.TransferStressLimit) },
                    new[] { "转接件挠度", N(e.TransferDeflection, 6) + " mm", N(e.TransferDeflLimit) + " mm",
                            Yes(e.TransferDeflection <= e.TransferDeflLimit) },
                    new[] { "焊缝强度", N(e.WeldStress) + " MPa", N(e.WeldStressLimit) + " MPa",
                            Yes(e.WeldStress <= e.WeldStressLimit) },
                }));
            b.Add(BookBlock.P($"焊缝计算面积 A = {N(e.WeldArea)} mm²，焊缝截面模量 W = {N(e.WeldSectionModulus)} mm³。"));
        }
        else b.Add(BookBlock.P("本章未参与计算。"));

        // ── 胶类 ──
        b.Add(BookBlock.PageBreak());
        b.Add(BookBlock.H2("八、胶类、伸缩及紧固计算"));
        if (x.Sealant is { } s)
        {
            b.Add(BookBlock.P($"面板部分风荷载标准值：{N(s.WkPanelKpa)} KPa（面板口径）。"));
            b.Add(BookBlock.H3("8.1 胶缝宽度与厚度"));
            b.Add(BookBlock.Table(
                new[] { "项目", "计算最小值", "实际取用", "判定" },
                new[]
                {
                    new[] { "玻璃与铝框间结构胶宽度", N(s.StructuralWidth) + " mm", N(s.ActualWidth, 0) + " mm",
                            Yes(s.StructuralWidth <= s.ActualWidth) },
                    new[] { "玻璃与铝框间结构胶厚度", N(s.StructuralThickness) + " mm", N(s.ActualThickness, 0) + " mm",
                            Yes(s.StructuralThickness <= s.ActualThickness) },
                    new[] { "硅酮耐候胶胶缝宽度", N(s.WeatherWidth) + " mm", N(s.WeatherActual, 0) + " mm",
                            Yes(s.WeatherWidth <= s.WeatherActual) },
                    new[] { "立柱伸缩缝", N(s.JointWidth) + " mm", N(s.JointActual, 0) + " mm",
                            Yes(s.JointWidth <= s.JointActual) },
                }));
            b.Add(BookBlock.P(
                "按 JGJ 102-2003 第 5.6.1 条，硅酮结构胶还应满足：1）粘接宽度 ≥ 7 mm；"
                + "2）12 mm ≥ 粘接厚度 ≥ 6 mm；3）粘接宽度大于厚度，但不宜大于厚度的 2 倍。"));

            b.Add(BookBlock.H3("8.2 压板与连接螺栓"));
            b.Add(BookBlock.Table(
                new[] { "项目", "计算值", "设计值 / 承载力", "判定" },
                new[]
                {
                    new[] { "压板最大弯曲应力", N(s.ClampBending) + " MPa", N(s.ClampBendingLimit) + " MPa",
                            Yes(s.ClampBending <= s.ClampBendingLimit) },
                    new[] { "压板最大剪应力", N(s.ClampShear) + " MPa", N(s.ClampShearLimit) + " MPa",
                            Yes(s.ClampShear <= s.ClampShearLimit) },
                    new[] { "连接螺栓受力", N(s.BoltForce) + " N", N(s.BoltCapacity) + " N",
                            Yes(s.BoltForce <= s.BoltCapacity) },
                }));
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
