using System;
using System.Collections.Generic;
using System.Globalization;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Calc.Book;

/// <summary>封面上那些可填的工程信息（「生成计算书」面板 (75,49)/(75,89) 两个框 + 四签 + 单位）。</summary>
public sealed record CalcBookMeta(
    string ProjectName, string ProjectSub, string Company,
    string Designer, string Checker, string Approver, string Ratifier);

/// <summary>计算书组装所需的各屏结果与工程信息。</summary>
public sealed class CalcBookInput
{
    // ── 封面（样本：工程名 / 幕墙类型 / 设计计算书 / 四签 / 单位） ──
    public string ProjectName { get; init; } = "北京xx幕墙装饰工程";
    public string ProjectSub { get; init; } = "全隐框玻璃幕墙";
    public string DocTitle { get; init; } = "设计计算书";
    public string Designer { get; init; } = "";
    public string Checker { get; init; } = "";
    public string Approver { get; init; } = "";
    public string Ratifier { get; init; } = "";
    public string Company { get; init; } = "";
    public DateTime Date { get; init; } = DateTime.Today;

    // ── 2 基本参数 ──
    public string Region { get; init; } = "";
    public string TerrainCategory { get; init; } = "";
    public string SeismicInfo { get; init; } = "";
    public double CalcHeightM { get; init; }

    // ── 3 幕墙承受荷载计算 ──
    public double WkKpa { get; init; }
    public double WkRawKpa { get; init; }
    public double WkPanelKpa { get; init; }
    public string WindSource { get; init; } = "";
    public double AlphaMax { get; init; }
    /// <summary>动力放大系数 β_E（V31/V35 取 5）。</summary>
    public double BetaE { get; init; } = 5.0;
    /// <summary>幕墙自重面荷载 N/mm²（算地震作用用）。</summary>
    public double DeadLoad { get; init; }
    public double QuakeKpa { get; init; }

    // ── 4/5/6 三屏摘要 ──
    public string? ColumnSummary { get; init; }
    public string? BeamSummary { get; init; }
    public string? GlassSummary { get; init; }

    // ── 7/8/9/10/11/12 引擎结果 ──
    public ConnectResult? Connect { get; init; }
    public EmbedResult? Embed { get; init; }
    public SealantResult? Sealant { get; init; }

    // ── 生成计算书面板上的开关 ──
    /// <summary>勾选「不输出附录」→ 跳过第 15 章。</summary>
    public bool NoAppendix { get; init; }
}

/// <summary>
/// 按 V31 自带样本 <c>计算书样本文件集/全隐框玻璃幕墙计算书.DOC</c> 的章节骨架组装计算书。
///
/// <para><b>骨架来源</b>：用 olefile 取该 DOC 的 WordDocument 流、按 UTF-16LE 扫描提正文
/// （<c>local-data/recon/calcbook_sample_dump.txt</c>）。15 章标题与 47 个小节标题逐字对齐样本。</para>
///
/// <para><b>V35 侧交叉核对</b>（<c>unpack/unpacked_runtime.exe</c>）：V35 没有"计算引用的规范"
/// 这类章节字样，因为它的正文是逐条公式文案 + <c>~x~</c> 下标 / <c>&amp;2&amp;</c> 上标 /
/// <c>A002(18)</c> 槽占位符拼出来的（<c>v35_column_calcbook_text.txt</c>）；
/// 章节顺序以 V31 样本 DOC 为准。</para>
///
/// <para>V31 把「埋件 / 转接件 / 焊缝」拆成 8/9/10 三章、把「胶类 / 压板」拆成 11/12 两章，
/// 本组装器与之一致（早期把四类合并成一章的做法是错的）。</para>
/// </summary>
public static class CalcBookComposer
{
    private static string Yes(bool ok) => ok ? "满足要求" : "不满足要求";

    private static string N(double v, int digits = 3)
        => v.ToString("F" + digits, CultureInfo.InvariantCulture);

    public static IReadOnlyList<BookBlock> Compose(CalcBookInput x)
    {
        var b = new List<BookBlock>();

        // ══════════ 封面 ══════════
        b.Add(BookBlock.H1(x.ProjectName));
        b.Add(BookBlock.P(x.ProjectSub));
        b.Add(BookBlock.P(x.DocTitle));
        b.Add(BookBlock.P(""));
        b.Add(BookBlock.P(""));
        b.Add(BookBlock.P($"设计：{x.Designer}"));
        b.Add(BookBlock.P($"校对：{x.Checker}"));
        b.Add(BookBlock.P($"审核：{x.Approver}"));
        b.Add(BookBlock.P($"批准：{x.Ratifier}"));
        b.Add(BookBlock.P(""));
        b.Add(BookBlock.P(x.Company));
        b.Add(BookBlock.P(x.Date.ToString("yyyy年MM月dd日")));
        b.Add(BookBlock.PageBreak());

        // ══════════ 1 计算引用的规范、标准及资料 ══════════
        b.Add(BookBlock.H2("1 计算引用的规范、标准及资料"));
        foreach (var (section, items) in BookSpecs.Grouped())
        {
            b.Add(BookBlock.H3("1 " + section + "："));
            foreach (var spec in items)
                b.Add(BookBlock.P($"《{spec.Title}》　{spec.Code}　{spec.Year}".TrimEnd()));
        }
        b.Add(BookBlock.PageBreak());

        // ══════════ 2 基本参数 ══════════
        b.Add(BookBlock.H2("2 基本参数"));
        b.Add(BookBlock.H3("2.1 幕墙所在地区"));
        b.Add(BookBlock.P(string.IsNullOrWhiteSpace(x.Region) ? "（未填写）" : x.Region));
        b.Add(BookBlock.H3("2.2 地面粗糙度分类等级"));
        b.Add(BookBlock.P(string.IsNullOrWhiteSpace(x.TerrainCategory) ? "（未填写）" : x.TerrainCategory));
        b.Add(BookBlock.H3("2.3 抗震设防"));
        b.Add(BookBlock.P(string.IsNullOrWhiteSpace(x.SeismicInfo) ? "（未填写）" : x.SeismicInfo));
        if (x.CalcHeightM > 0) b.Add(BookBlock.P($"计算标高：{N(x.CalcHeightM)} m"));
        b.Add(BookBlock.PageBreak());

        // ══════════ 3 幕墙承受荷载计算 ══════════
        b.Add(BookBlock.H2("3 幕墙承受荷载计算"));
        b.Add(BookBlock.H3("3.1 风荷载标准值的计算方法"));
        b.Add(BookBlock.P("按《建筑结构荷载规范》GB 50009-2012 第 8 章取 βgz、μsl、μz、w0，"
            + "并按《玻璃幕墙工程技术规范》JGJ 102 取阵风系数与局部体型系数。"));
        b.Add(BookBlock.H3("3.2 计算支承结构时的风荷载标准值"));
        b.Add(BookBlock.P($"wk = {N(x.WkKpa)} KPa"
            + (string.IsNullOrWhiteSpace(x.WindSource) ? "" : $"（{x.WindSource}）")));
        if (x.WkRawKpa > 0 && Math.Abs(x.WkRawKpa - x.WkKpa) > 1e-9)
            b.Add(BookBlock.P($"未取整值：{N(x.WkRawKpa, 6)} KPa"));
        b.Add(BookBlock.H3("3.3 计算面板材料时的风荷载标准值"));
        b.Add(BookBlock.P(x.WkPanelKpa > 0
            ? $"wk = {N(x.WkPanelKpa)} KPa（面板口径：μsl = 1.0，不折减 ψ）"
            : "（面板口径未写入）"));
        b.Add(BookBlock.H3("3.4 垂直于幕墙平面的分布水平地震作用标准值"));
        b.Add(BookBlock.P(x.QuakeKpa > 0
            ? $"qEk = βE·αmax·Gk = {N(x.BetaE)} × {N(x.AlphaMax)} × {N(x.DeadLoad, 6)} = {N(x.QuakeKpa, 6)} N/mm²"
            : "（未计算）"));
        b.Add(BookBlock.H3("3.5 平行于幕墙平面的集中水平地震作用标准值"));
        b.Add(BookBlock.P("按 JGJ 102 第 5.3.5 条取垂直于幕墙平面分布水平地震作用标准值的 1/5，仅用于侧向连接件校核。"));
        b.Add(BookBlock.H3("3.6 作用效应组合"));
        b.Add(BookBlock.P("承载力极限状态：S = 1.5·Swk + 0.7·1.4·SEk（风控制）；"
            + "正常使用极限状态：S = Swk + 0.5·SEk。"));
        b.Add(BookBlock.PageBreak());

        // ══════════ 4/5/6 ══════════
        b.Add(BookBlock.H2("4 幕墙立柱计算"));
        AddSummary(b, x.ColumnSummary, new[]
        {
            "4.1 立柱型材选材计算", "4.2 确定材料的截面参数", "4.3 选用立柱型材的截面特性",
            "4.4 立柱的抗弯强度计算", "4.5 立柱的挠度计算", "4.6 立柱的抗剪计算",
        });
        b.Add(BookBlock.PageBreak());
        b.Add(BookBlock.H2("5 幕墙横梁计算"));
        AddSummary(b, x.BeamSummary, new[]
        {
            "5.1 横梁型材选材计算", "5.2 确定材料的截面参数", "5.3 选用横梁型材的截面特性",
            "5.4 幕墙横梁的抗弯强度计算", "5.5 横梁的挠度计算", "5.6 横梁的抗剪计算",
        });
        b.Add(BookBlock.PageBreak());
        b.Add(BookBlock.H2("6 幕墙玻璃的选用与校核"));
        AddSummary(b, x.GlassSummary, new[]
        {
            "6.1 玻璃板块荷载计算", "6.2 玻璃板块强度计算", "6.3 玻璃的挠度计算",
        });

        // ══════════ 7 连接件计算 ══════════
        b.Add(BookBlock.PageBreak());
        b.Add(BookBlock.H2("7 连接件计算"));
        if (x.Connect is { } c)
        {
            b.Add(BookBlock.H3("7.1 横梁与角码间连接"));
            b.Add(BookBlock.Table(
                new[] { "项目", "螺栓设计值(个)", "实际用(个)", "横梁壁抗承压(N)", "实际受力(N)", "结论" },
                new[]
                {
                    new[] { "横梁与角码连接", N(c.BeamBoltDesign), "2", N(c.BeamWebBearing),
                            N(c.BeamForce), Yes(c.BeamBoltDesign <= 2 && c.BeamForce <= c.BeamWebBearing) },
                }));
            b.Add(BookBlock.P($"单个螺栓抗剪承载力设计值 {N(c.BeamBoltCapacity)} N；"
                + "螺栓数量设计值 = 实际受力 / 单栓承载力。"));
            b.Add(BookBlock.H3("7.2 角码与立柱连接"));
            b.Add(BookBlock.Table(
                new[] { "项目", "螺栓设计值(个)", "实际用(个)", "立柱壁抗承压(N)", "角码壁抗承压(N)", "实际受力(N)", "结论" },
                new[]
                {
                    new[] { "角码与立柱连接", N(c.ColumnBoltDesign), "2", N(c.ColumnWebBearing),
                            N(c.BracketBearing), N(c.ColumnForce),
                            Yes(c.ColumnBoltDesign <= 2 && c.ColumnForce <= Math.Min(c.ColumnWebBearing, c.BracketBearing)) },
                }));
            b.Add(BookBlock.H3("7.3 立柱与主结构连接"));
            b.Add(BookBlock.Table(
                new[] { "项目", "螺栓设计值(个)", "实际用(个)", "立柱壁抗承压(N)", "角码壁抗承压(N)", "实际受力(N)", "结论" },
                new[]
                {
                    new[] { "立柱与主体连接", N(c.MainBoltDesign), "2", N(c.MainColumnBearing),
                            N(c.MainBracketBearing), N(c.MainForce),
                            Yes(c.MainBoltDesign <= 2 && c.MainForce <= Math.Min(c.MainColumnBearing, c.MainBracketBearing)) },
                }));
            b.Add(BookBlock.P($"单个螺栓抗剪承载力设计值 {N(c.MainBoltCapacity)} N（按接触面数取双剪）。"));
        }
        else b.Add(BookBlock.P("本章未参与计算。"));

        // ══════════ 8 幕墙埋件计算 ══════════
        b.Add(BookBlock.PageBreak());
        b.Add(BookBlock.H2("8 幕墙埋件计算"));
        if (x.Embed is { } e)
        {
            b.Add(BookBlock.P("土建预埋"));
            b.Add(BookBlock.H3("8.1 荷载标准值与设计值计算"));
            b.Add(BookBlock.Table(
                new[] { "项目", "数值", "单位" },
                new[]
                {
                    new[] { "剪力设计值 V", N(e.ShearDesign), "N" },
                    new[] { "轴力设计值 N", N(e.AxialDesign), "N" },
                    new[] { "弯矩设计值 M", N(e.MomentDesign), "N·mm" },
                }));
            b.Add(BookBlock.H3("8.2 埋件计算"));
            b.Add(BookBlock.Table(
                new[] { "项目", "计算值", "实配值", "结论" },
                new[]
                {
                    new[] { "预埋件锚筋总截面积", N(e.RebarAreaRequired) + " mm²",
                            N(e.RebarAreaActual) + " mm²", Yes(e.RebarAreaRequired <= e.RebarAreaActual) },
                }));
            b.Add(BookBlock.P($"锚筋层数影响系数 αr = {N(e.AlphaR)}，锚板厚度影响系数 αb = {N(e.AlphaB)}。"
                + "总截面积取「V/(αr·fy) + N/(0.8·αb·fy) + M/(1.3·αb·fy·z)」"
                + "与「N/(0.8·αb·fy) + M/(0.4·αb·fy·z)」两式较大值。"));
            b.Add(BookBlock.H3("8.3 锚板总面积校核"));
            b.Add(BookBlock.Table(
                new[] { "项目", "数值", "比较值", "结论" },
                new[]
                {
                    new[] { "0.5·fc·A", N(e.ConcreteBearing) + " N", "法向压力 " + N(e.AxialDesign) + " N",
                            Yes(e.AxialDesign <= e.ConcreteBearing) },
                }));
            b.Add(BookBlock.H3("8.4 锚筋长度计算"));
            b.Add(BookBlock.Table(
                new[] { "项目", "数值", "单位" },
                new[]
                {
                    new[] { "基本锚固长度 lab", N(e.AnchorLab), "mm" },
                    new[] { "折减后锚固长度 la", N(e.AnchorLa), "mm" },
                }));
            b.Add(BookBlock.P("锚筋末端采用弯钩或机械锚固措施时，包括弯钩和锚固端头在内的锚固长度"
                + "（投影长度）可取基本锚固长度的 60%，其形式与技术要求应符合 GB 50010 规定。"
                + "抗震设计的幕墙锚筋锚固长度不得小于 200 mm；锚固长度不应小于 15 倍锚筋直径；"
                + "当实配面积超过计算所需 1.4 倍时，锚固长度可适当减少，但不应小于 180 mm。"));
        }
        else b.Add(BookBlock.P("本章未参与计算。"));

        // ══════════ 9 转接件 ══════════
        b.Add(BookBlock.PageBreak());
        b.Add(BookBlock.H2("9 幕墙转接件强度计算"));
        if (x.Embed is { } e2)
        {
            b.Add(BookBlock.H3("9.1 受力分析"));
            b.Add(BookBlock.P($"转接件承受轴力 N = {N(e2.AxialDesign)} N、弯矩 M = {N(e2.MomentDesign)} N·mm，按拉弯构件校核。"));
            b.Add(BookBlock.H3("9.2 转接件的强度计算"));
            b.Add(BookBlock.Table(
                new[] { "项目", "计算值", "设计值", "结论" },
                new[]
                {
                    new[] { "转接件强度", N(e2.TransferStress) + " MPa", N(e2.TransferStressLimit) + " MPa",
                            Yes(e2.TransferStress <= e2.TransferStressLimit) },
                }));
            b.Add(BookBlock.H3("9.3 转接件的挠度计算"));
            b.Add(BookBlock.Table(
                new[] { "项目", "计算值", "设计值", "结论" },
                new[]
                {
                    new[] { "转接件挠度", N(e2.TransferDeflection, 6) + " mm", N(e2.TransferDeflLimit) + " mm",
                            Yes(e2.TransferDeflection <= e2.TransferDeflLimit) },
                }));
        }
        else b.Add(BookBlock.P("本章未参与计算。"));

        // ══════════ 10 焊缝 ══════════
        b.Add(BookBlock.PageBreak());
        b.Add(BookBlock.H2("10 幕墙焊缝计算"));
        if (x.Embed is { } e3)
        {
            b.Add(BookBlock.H3("10.1 受力分析"));
            b.Add(BookBlock.P($"焊缝承受轴力 N = {N(e3.AxialDesign)} N、弯矩 M = {N(e3.MomentDesign)} N·mm、"
                + $"剪力 V = {N(e3.ShearDesign)} N，按三面围焊角焊缝计算。"));
            b.Add(BookBlock.H3("10.2 焊缝特性参数计算"));
            b.Add(BookBlock.Table(
                new[] { "项目", "数值", "单位" },
                new[]
                {
                    new[] { "焊缝计算面积 A", N(e3.WeldArea), "mm²" },
                    new[] { "焊缝截面模量 W", N(e3.WeldSectionModulus), "mm³" },
                }));
            b.Add(BookBlock.H3("10.3 焊缝校核计算"));
            b.Add(BookBlock.Table(
                new[] { "项目", "计算值", "设计值", "结论" },
                new[]
                {
                    new[] { "焊缝强度", N(e3.WeldStress) + " MPa", N(e3.WeldStressLimit) + " MPa",
                            Yes(e3.WeldStress <= e3.WeldStressLimit) },
                }));
            b.Add(BookBlock.P("角焊缝按 σf = √[(N/(βf·A) + M/(βf·W))² + (V/A)²] 计算，βf 取 1.22。"));
        }
        else b.Add(BookBlock.P("本章未参与计算。"));

        // ══════════ 11 胶类及伸缩缝 ══════════
        b.Add(BookBlock.PageBreak());
        b.Add(BookBlock.H2("11 全隐框玻璃幕墙胶类及伸缩缝计算"));
        if (x.Sealant is { } s)
        {
            b.Add(BookBlock.H3("11.1 硅酮结构密封胶的宽度计算"));
            b.Add(BookBlock.Table(
                new[] { "项目", "计算值", "实际取用", "结论" },
                new[]
                {
                    new[] { "结构胶粘接宽度", N(s.StructuralWidth) + " mm", N(s.ActualWidth, 0) + " mm",
                            Yes(s.StructuralWidth <= s.ActualWidth) },
                }));
            b.Add(BookBlock.H3("11.2 硅酮结构密封胶粘接厚度的计算"));
            b.Add(BookBlock.Table(
                new[] { "项目", "计算值", "实际取用", "结论" },
                new[]
                {
                    new[] { "结构胶粘接厚度", N(s.StructuralThickness) + " mm", N(s.ActualThickness, 0) + " mm",
                            Yes(s.StructuralThickness <= s.ActualThickness) },
                }));
            b.Add(BookBlock.H3("11.3 结构胶设计总结"));
            b.Add(BookBlock.P("按 JGJ 102-2003 第 5.6.1 条，硅酮结构胶还应满足："
                + "1）粘接宽度 ≥ 7 mm；2）12 mm ≥ 粘接厚度 ≥ 6 mm；"
                + "3）粘接宽度大于厚度，但不宜大于厚度的 2 倍。"));
            b.Add(BookBlock.H3("11.4 立柱连接伸缩缝计算"));
            b.Add(BookBlock.Table(
                new[] { "项目", "计算值", "实际取用", "结论" },
                new[]
                {
                    new[] { "立柱伸缩缝", N(s.JointWidth) + " mm", N(s.JointActual, 0) + " mm",
                            Yes(s.JointWidth <= s.JointActual) },
                }));
            b.Add(BookBlock.H3("11.5 耐候胶胶缝计算"));
            b.Add(BookBlock.Table(
                new[] { "项目", "计算值", "实际取用", "结论" },
                new[]
                {
                    new[] { "硅酮耐候胶胶缝宽度", N(s.WeatherWidth) + " mm", N(s.WeatherActual, 0) + " mm",
                            Yes(s.WeatherWidth <= s.WeatherActual) },
                }));
        }
        else b.Add(BookBlock.P("本章未参与计算。"));

        // ══════════ 12 压板 ══════════
        b.Add(BookBlock.PageBreak());
        b.Add(BookBlock.H2("12 幕墙板块压板计算"));
        if (x.Sealant is { } s2)
        {
            b.Add(BookBlock.H3("12.1 压板的弯矩设计值计算"));
            b.Add(BookBlock.P($"压板计算反力 {N(s2.ClampLoad)} N。"));
            b.Add(BookBlock.H3("12.2 压板的应力计算"));
            b.Add(BookBlock.Table(
                new[] { "项目", "计算值", "设计值", "结论" },
                new[]
                {
                    new[] { "压板最大弯曲应力", N(s2.ClampBending) + " MPa", N(s2.ClampBendingLimit) + " MPa",
                            Yes(s2.ClampBending <= s2.ClampBendingLimit) },
                    new[] { "压板最大剪应力", N(s2.ClampShear) + " MPa", N(s2.ClampShearLimit) + " MPa",
                            Yes(s2.ClampShear <= s2.ClampShearLimit) },
                }));
            b.Add(BookBlock.H3("12.3 螺栓抗拉强度验算"));
            b.Add(BookBlock.Table(
                new[] { "项目", "计算值", "承载力", "结论" },
                new[]
                {
                    new[] { "连接螺栓受力", N(s2.BoltForce) + " N", N(s2.BoltCapacity) + " N",
                            Yes(s2.BoltForce <= s2.BoltCapacity) },
                }));
        }
        else b.Add(BookBlock.P("本章未参与计算。"));

        // ══════════ 13/14 ══════════
        b.Add(BookBlock.PageBreak());
        b.Add(BookBlock.H2("13 玻璃副框计算"));
        b.Add(BookBlock.H3("13.1 副框抗拉强度计算"));
        b.Add(BookBlock.H3("13.2 副框抗剪强度计算"));
        b.Add(BookBlock.P("本章未参与计算（复刻版尚未实现副框计算）。"));
        b.Add(BookBlock.H2("14 玻璃承托件计算"));
        b.Add(BookBlock.H3("14.1 承托件抗弯强度计算"));
        b.Add(BookBlock.H3("14.2 承托件挠度计算"));
        b.Add(BookBlock.P("本章未参与计算（复刻版尚未实现承托件计算）。"));

        // ══════════ 15 附录（可由面板勾掉） ══════════
        if (!x.NoAppendix)
        {
            b.Add(BookBlock.PageBreak());
            b.Add(BookBlock.H2("15 附录 常用材料的力学及其它物理性能"));
            b.Add(BookBlock.Table(
                new[] { "材料", "弹性模量 E (N/mm²)", "泊松比 ν", "线膨胀系数 α (1/℃)", "自重 (kN/m³)" },
                new[]
                {
                    new[] { "铝合金 6063-T5", "70000", "0.33", "2.3×10⁻⁵", "28.0" },
                    new[] { "钢材 Q235", "206000", "0.30", "1.2×10⁻⁵", "78.5" },
                    new[] { "玻璃", "72000", "0.20", "1.0×10⁻⁵", "25.6" },
                    new[] { "硅酮结构胶", "—", "0.49", "—", "—" },
                }));
            b.Add(BookBlock.P("注：本附录数值取自参数库材料表；计算中实际取用值见各章。"));
        }

        // ══════════ 结论 ══════════
        b.Add(BookBlock.PageBreak());
        b.Add(BookBlock.H2("结论"));
        b.Add(BookBlock.P(BuildConclusion(x)));

        return b;
    }

    /// <summary>把某屏的预演摘要铺进该章。</summary>
    private static void AddSummary(List<BookBlock> b, string? summary, string[] subs)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            foreach (var s in subs) b.Add(BookBlock.H3(s));
            b.Add(BookBlock.P("本章未参与计算。"));
            return;
        }
        b.Add(BookBlock.H3(subs[0]));
        b.Add(BookBlock.P(summary));
    }

    private static string BuildConclusion(CalcBookInput x)
    {
        var fails = new List<string>();
        if (x.Connect is { } c)
        {
            if (c.BeamBoltDesign > 2) fails.Add("横梁与角码连接螺栓");
            if (c.ColumnBoltDesign > 2) fails.Add("角码与立柱连接螺栓");
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
            ? "经上述计算，本工程幕墙结构各项验算均满足现行规范要求。"
            : "经上述计算，以下项目不满足规范要求，需调整设计：" + string.Join("、", fails) + "。";
    }
}
