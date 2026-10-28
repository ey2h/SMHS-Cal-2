using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    /// <summary>设防类别（样本文案用「标准设防类」这类全称）。</summary>
    public string SeismicCategory { get; init; } = "标准设防类";
    /// <summary>抗震设防烈度（度）；0 = 未取到，文案里回落样本值。</summary>
    public double SeismicIntensity { get; init; }
    /// <summary>设计基本地震加速度（g）。</summary>
    public double PeakAccel { get; init; }
    /// <summary>水平地震影响系数最大值 αmaxⅡ（来自地区参数库，不是算出来的）。</summary>
    public double AlphaMaxII { get; init; }
    /// <summary>场地类别（Ⅰ/Ⅱ/Ⅲ/Ⅳ）。</summary>
    public string SiteClass { get; init; } = "Ⅱ";
    /// <summary>场地地震动峰值加速度调整系数 Fa（GB18306-2015 表 E.1）。</summary>
    public double Fa { get; init; } = 1.0;
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

    /// <summary>幕墙类型（决定章节骨架）：全隐框玻璃幕墙 / 铝单板幕墙 / UHPC板幕墙 /
    /// GRC板幕墙 / 单元幕墙 / 点式钢管结构 / 门窗 / 节能。见 <see cref="BookSkeletons"/>。</summary>
    public string Kind { get; init; } = "全隐框玻璃幕墙";

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
        // **以程序真实产出为准**：`计算书样本文件集/节能计算书.DOC` 的封面是
        //   工程名称 / 副标题(外装饰结构热工性能) / 设计计算书 / 空段 / 设计：校对：审核：批准：
        //   / 单位 / 中文日期 / 目录
        // `jss\模板.doc` 只是**骨架**（18 段纯文本，只到「批准：」+「目录」+indexindex），
        // 单位与日期是程序在四签之后动态插入的 —— 光看模板文件会误判成"没有这两行"。
        b.Add(BookBlock.Title(x.ProjectName));
        if (!string.IsNullOrWhiteSpace(x.ProjectSub)) b.Add(BookBlock.Title(x.ProjectSub));
        b.Add(BookBlock.Title(x.DocTitle));
        for (int i = 0; i < 10; i++) b.Add(BookBlock.Blank());
        b.Add(BookBlock.P($"设计：{x.Designer}"));
        b.Add(BookBlock.P($"校对：{x.Checker}"));
        b.Add(BookBlock.P($"审核：{x.Approver}"));
        b.Add(BookBlock.P($"批准：{x.Ratifier}"));
        if (!string.IsNullOrWhiteSpace(x.Company)) b.Add(BookBlock.P(x.Company));
        b.Add(BookBlock.P(ChineseDate(x.Date)));
        b.Add(BookBlock.PageBreak());

        // ══════════ 目录（模板里的 `目录` + `indexindex` 占位） ══════════
        b.Add(BookBlock.Center("目录"));
        b.Add(BookBlock.Toc());
        b.Add(BookBlock.PageBreak());

        // ══════════ 按幕墙类型的骨架逐章输出 ══════════
        // 章号与章标题、小节标题全部取自样本目录（BookSkeletons），不在此处拼。
        var chapters = BookSkeletons.Chapters(x.Kind);
        foreach (var ch in chapters)
        {
            // 面板「不输出附录」勾上 → 跳过附录章（章号仍按样本，不做重编号）
            if (x.NoAppendix && ch.Title.Contains("附录")) continue;
            b.Add(BookBlock.PageBreak());
            b.Add(BookBlock.H2($"{ch.No} {ch.Title}"));
            RenderChapter(b, ch, x);
        }

        // ══════════ 结论 ══════════
        b.Add(BookBlock.PageBreak());
        b.Add(BookBlock.H2("结论"));
        b.Add(BookBlock.P(BuildConclusion(x)));

        return b;
    }

    /// <summary>
    /// 按章标题分派内容。骨架决定「有哪些章」，这里决定「每章填什么」——
    /// 已实现的引擎/摘要填真值；未实现的章按样本小节逐节列标题并写明未复刻，**不编内容**。
    /// </summary>
    private static void RenderChapter(List<BookBlock> b, BookChapter ch, CalcBookInput x)
    {
        int no = ch.No;
        string title = ch.Title;
        if (title.Contains("规范")) { RenderSpecs(b, no, x); return; }
        if (title.Contains("基本参数")) { RenderBasic(b, ch, x); return; }
        if (title.Contains("承受荷载计算")) { RenderLoads(b, ch, x); return; }
        if (title.Contains("立柱")) { RenderSummary(b, ch, x.ColumnSummary, ColumnSubs); return; }
        if (title.Contains("横梁")) { RenderSummary(b, ch, x.BeamSummary, BeamSubs); return; }
        if (title.Contains("玻璃的选用") || title.Contains("大面玻璃") || title.Contains("玻璃板块的选用"))
        { RenderSummary(b, ch, x.GlassSummary, GlassSubs); return; }
        if (title.Contains("连接件计算") || title.Contains("连接部分计算") || title.Contains("连接结构计算"))
        { RenderConnect(b, no, x); return; }
        if (title.Contains("埋件计算")) { RenderEmbed(b, no, x); return; }
        if (title.Contains("转接件")) { RenderTransfer(b, no, x); return; }
        if (title.Contains("焊缝")) { RenderWeld(b, no, x); return; }
        if (title.Contains("胶类")) { RenderSealant(b, no, x); return; }
        if (title.Contains("压板")) { RenderClamp(b, no, x); return; }
        if (title.Contains("附录")) { RenderAppendix(b, x); return; }
        // 其余类型特有章（副框 / 承托件 / 挂件 / 槽式埋件 / 门窗槽口 / 传热 / 结露 …）
        // 按样本小节列标题，逐节写明未复刻——宁可空着，也不自己拼内容。
        RenderNotImplemented(b, ch, x);
    }

    /// <summary>未复刻的章：小节标题照样本列，正文写「未复刻」。</summary>
    private static void RenderNotImplemented(List<BookBlock> b, BookChapter ch, CalcBookInput x)
    {
        if (ch.Subs.Length == 0)
        {
            b.Add(BookBlock.P($"本章未参与计算（{x.Kind} 的「{ch.Title}」尚未复刻）。"));
            return;
        }
        foreach (var sub in ch.Subs)
        {
            b.Add(BookBlock.H3(sub));
            b.Add(BookBlock.P("（本节尚未复刻：样本有该节，复刻版暂无对应计算。）"));
        }
    }

    private static string Trim(int no, string t) => t;

    /// <summary>中文日期：样本封面写「二〇二五年三月二十四日」，不是 2025年03月24日。</summary>
    private static string ChineseDate(DateTime d)
    {
        const string DIG = "〇一二三四五六七八九";
        static string Num(int n)
        {
            // 1..31：1→一，10→十，11→十一，20→二十，21→二十一
            if (n <= 0) return "";
            if (n < 10) return DIG[n].ToString();
            var s = "";
            if (n >= 20) { s += DIG[n / 10] + "十"; n %= 10; }
            else { s += "十"; n -= 10; }
            if (n > 0) s += DIG[n];
            return s;
        }
        var year = string.Concat(d.Year.ToString().Select(c => DIG[c - '0']));
        return $"{year}年{Num(d.Month)}月{Num(d.Day)}日";
    }

    private static readonly string[] ColumnSubs =
    {
        "立柱型材选材计算", "确定材料的截面参数", "选用立柱型材的截面特性",
        "立柱的抗弯强度计算", "立柱的挠度计算", "立柱的抗剪计算",
    };
    private static readonly string[] BeamSubs =
    {
        "横梁型材选材计算", "确定材料的截面参数", "选用横梁型材的截面特性",
        "幕墙横梁的抗弯强度计算", "横梁的挠度计算", "横梁的抗剪计算",
    };
    private static readonly string[] GlassSubs =
    {
        "玻璃板块荷载计算", "玻璃板块强度计算", "玻璃的挠度计算",
    };

    /// <summary>
    /// 第 1 章「计算引用的规范、标准及资料」。
    /// <para><b>数据源</b>：V35 安装目录 <c>规范\规范1.txt</c>~<c>规范8.txt</c>，按幕墙类型选套
    /// （映射见 <see cref="BookSpecSets.SetByKind"/>）。条目保留原文的空格对齐，
    /// 以 <c>AAA&lt;N&gt;.</c> 开头的行提升为三级标题。</para>
    /// </summary>
    private static void RenderSpecs(List<BookBlock> b, int no, CalcBookInput x)
    {
        var lines = BookSpecSets.For(x.Kind);
        int gi = 0;
        foreach (var line in lines)
        {
            if (BookSpecSets.IsGroupHeader(line, out var title))
            {
                b.Add(BookBlock.H3($"{no}.{++gi} {title}"));
                continue;
            }
            // 无 AAA 分组的套（节能 6 / 隔声 8）没有小标题，条目直接铺在章下
            b.Add(BookBlock.P(line));
        }
    }

    /// <summary>取样本小节标题；样本没有这一节时才回落到内置文案。</summary>
    private static string Sub(BookChapter ch, int i, string fallback)
        => i < ch.Subs.Length ? ch.Subs[i] : $"{ch.No}.{i + 1} {fallback}";

    /// <summary>
    /// 第 2 章「基本参数」—— 正文**逐字取自样本**（<see cref="BookText.Basic"/>），
    /// 只把地区/粗糙度/抗震那几处换成实算值。一致性由 <c>BookTextTests</c> 守护。
    /// </summary>
    private static void RenderBasic(List<BookBlock> b, BookChapter ch, CalcBookInput x)
    {
        var v = new BookTextVars
        {
            Subject = x.Kind.Contains("门窗") ? "门窗" : "幕墙",
            // 样本写「北京地区；」，而参数库里的地区名是「北京」——补「地区」二字以逐字对齐
            Region = string.IsNullOrWhiteSpace(x.Region) ? "北京地区"
                     : x.Region.EndsWith("地区") ? x.Region : x.Region + "地区",
            Terrain = string.IsNullOrWhiteSpace(x.TerrainCategory) ? "C" : x.TerrainCategory,
            SeismicIntensity = x.SeismicIntensity > 0 ? N0(x.SeismicIntensity) : "8",
            PeakAccel = x.PeakAccel > 0 ? N1(x.PeakAccel) : "0.2",
            SeismicCategory = string.IsNullOrWhiteSpace(x.SeismicCategory) ? "标准设防类" : x.SeismicCategory,
            AlphaMaxII = x.AlphaMaxII > 0 ? N2(x.AlphaMaxII) : "0.16",
            SiteClass = string.IsNullOrWhiteSpace(x.SiteClass) ? "Ⅱ" : x.SiteClass,
            Fa = N2(x.Fa),
            AlphaMax = x.AlphaMax > 0 ? N2(x.AlphaMax) : "0.16",
        };

        var lines = BookText.Basic(v);
        foreach (var line in lines)
        {
            // 小节标题：样本目录里的 2.1/2.2/2.3 与正文行尾一致，用带编号的那个当 H3
            var sub = ch.Subs.FirstOrDefault(t => t.EndsWith(line));
            if (sub is not null) b.Add(BookBlock.H3(sub));
            else b.Add(BookBlock.P(line));
        }
    }

    /// <summary>整数值不带小数点（烈度 8 度不是 8.0 度）。</summary>
    private static string N0(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
    private static string N1(double v) => v.ToString("0.0##", CultureInfo.InvariantCulture);
    private static string N2(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);

    private static void RenderLoads(List<BookBlock> b, BookChapter ch, CalcBookInput x)
    {
        b.Add(BookBlock.H3(Sub(ch, 0, "风荷载标准值的计算方法")));
        b.Add(BookBlock.P("按《建筑结构荷载规范》GB 50009-2012 第 8 章取 βgz、μsl、μz、w0，"
            + "并按《玻璃幕墙工程技术规范》JGJ 102 取阵风系数与局部体型系数。"));
        b.Add(BookBlock.H3(Sub(ch, 1, "计算支承结构时的风荷载标准值")));
        b.Add(BookBlock.P($"wk = {N(x.WkKpa)} KPa"
            + (string.IsNullOrWhiteSpace(x.WindSource) ? "" : $"（{x.WindSource}）")));
        if (x.WkRawKpa > 0 && Math.Abs(x.WkRawKpa - x.WkKpa) > 1e-9)
            b.Add(BookBlock.P($"未取整值：{N(x.WkRawKpa, 6)} KPa"));
        b.Add(BookBlock.H3(Sub(ch, 2, "计算面板材料时的风荷载标准值")));
        b.Add(BookBlock.P(x.WkPanelKpa > 0
            ? $"wk = {N(x.WkPanelKpa)} KPa（面板口径：μsl = 1.0，不折减 ψ）"
            : "（面板口径未写入）"));
        b.Add(BookBlock.H3(Sub(ch, 3, "垂直于幕墙平面的分布水平地震作用标准值")));
        b.Add(BookBlock.P(x.QuakeKpa > 0
            ? $"qEk = βE·αmax·Gk = {N(x.BetaE)} × {N(x.AlphaMax)} × {N(x.DeadLoad, 6)} = {N(x.QuakeKpa, 6)} N/mm²"
            : "（未计算）"));
        b.Add(BookBlock.H3(Sub(ch, 4, "平行于幕墙平面的集中水平地震作用标准值")));
        b.Add(BookBlock.P("按 JGJ 102 第 5.3.5 条取垂直于幕墙平面分布水平地震作用标准值的 1/5，仅用于侧向连接件校核。"));
        b.Add(BookBlock.H3(Sub(ch, 5, "作用效应组合")));
        b.Add(BookBlock.P("承载力极限状态：S = 1.5·Swk + 0.7·1.4·SEk（风控制）；"
            + "正常使用极限状态：S = Swk + 0.5·SEk。"));
    }

    /// <summary>
    /// 立柱/横梁/玻璃这类「整章一段结果」的章：小节标题**全部**按样本列出，
    /// 结果文本整体跟在后面（不强行塞进某一节，避免编造小节归属）。
    /// </summary>
    private static void RenderSummary(List<BookBlock> b, BookChapter ch,
                                      string? summary, string[] fallbackSubs)
    {
        if (ch.Subs.Length > 0)
        {
            foreach (var s in ch.Subs) b.Add(BookBlock.H3(s));
        }
        else
        {
            int k = 0;
            foreach (var s in fallbackSubs) b.Add(BookBlock.H3($"{ch.No}.{++k} {s}"));
        }
        b.Add(BookBlock.P(string.IsNullOrWhiteSpace(summary) ? "本章未参与计算。" : summary));
    }

    private static void RenderConnect(List<BookBlock> b, int no, CalcBookInput x)
    {
        if (x.Connect is not { } c) { b.Add(BookBlock.P("本章未参与计算。")); return; }
        b.Add(BookBlock.H3($"{no}.1 横梁与角码间连接"));
        b.Add(BookBlock.Table(
            new[] { "项目", "螺栓设计值(个)", "实际用(个)", "横梁壁抗承压(N)", "实际受力(N)", "结论" },
            new[]
            {
                new[] { "横梁与角码连接", N(c.BeamBoltDesign), "2", N(c.BeamWebBearing),
                        N(c.BeamForce), Yes(c.BeamBoltDesign <= 2 && c.BeamForce <= c.BeamWebBearing) },
            }));
        b.Add(BookBlock.P($"单个螺栓抗剪承载力设计值 {N(c.BeamBoltCapacity)} N；螺栓数量设计值 = 实际受力 / 单栓承载力。"));
        b.Add(BookBlock.H3($"{no}.2 角码与立柱连接"));
        b.Add(BookBlock.Table(
            new[] { "项目", "螺栓设计值(个)", "实际用(个)", "立柱壁抗承压(N)", "角码壁抗承压(N)", "实际受力(N)", "结论" },
            new[]
            {
                new[] { "角码与立柱连接", N(c.ColumnBoltDesign), "2", N(c.ColumnWebBearing),
                        N(c.BracketBearing), N(c.ColumnForce),
                        Yes(c.ColumnBoltDesign <= 2 && c.ColumnForce <= Math.Min(c.ColumnWebBearing, c.BracketBearing)) },
            }));
        b.Add(BookBlock.H3($"{no}.3 立柱与主结构连接"));
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

    private static void RenderEmbed(List<BookBlock> b, int no, CalcBookInput x)
    {
        if (x.Embed is not { } e) { b.Add(BookBlock.P("本章未参与计算。")); return; }
        b.Add(BookBlock.P("土建预埋"));
        b.Add(BookBlock.H3($"{no}.1 荷载标准值与设计值计算"));
        b.Add(BookBlock.Table(
            new[] { "项目", "数值", "单位" },
            new[]
            {
                new[] { "剪力设计值 V", N(e.ShearDesign), "N" },
                new[] { "轴力设计值 N", N(e.AxialDesign), "N" },
                new[] { "弯矩设计值 M", N(e.MomentDesign), "N·mm" },
            }));
        b.Add(BookBlock.H3($"{no}.2 埋件计算"));
        b.Add(BookBlock.Table(
            new[] { "项目", "计算值", "实配值", "结论" },
            new[]
            {
                new[] { "预埋件锚筋总截面积", N(e.RebarAreaRequired) + " mm²",
                        N(e.RebarAreaActual) + " mm²", Yes(e.RebarAreaRequired <= e.RebarAreaActual) },
            }));
        b.Add(BookBlock.P($"锚筋抗剪影响系数 αv = {N(e.AlphaV)}，锚筋层数影响系数 αr = {N(e.AlphaR)}，"
            + $"锚板厚度影响系数 αb = {N(e.AlphaB)}。"
            + "总截面积取「V/(αr·αv·fy) + N/(0.8·αr·αb·fy) + M/(1.3·αr·αb·fy·z)」"
            + "与「N/(0.8·αr·αb·fy) + M/(0.4·αr·αb·fy·z)」两式较大值；"
            + "锚固长度 la 的折减比值恒取后一式（分支2），与屏幕显示的较大值无关。"));
        b.Add(BookBlock.H3($"{no}.3 锚板总面积校核"));
        b.Add(BookBlock.Table(
            new[] { "项目", "数值", "比较值", "结论" },
            new[]
            {
                new[] { "0.5·fc·A", N(e.ConcreteBearing) + " N", "法向压力 " + N(e.AxialDesign) + " N",
                        Yes(e.AxialDesign <= e.ConcreteBearing) },
            }));
        b.Add(BookBlock.H3($"{no}.4 锚筋长度计算"));
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

    private static void RenderTransfer(List<BookBlock> b, int no, CalcBookInput x)
    {
        if (x.Embed is not { } e) { b.Add(BookBlock.P("本章未参与计算。")); return; }
        b.Add(BookBlock.H3($"{no}.1 受力分析"));
        b.Add(BookBlock.P($"转接件承受轴力 N = {N(e.AxialDesign)} N、弯矩 M = {N(e.MomentDesign)} N·mm，按拉弯构件校核。"));
        b.Add(BookBlock.H3($"{no}.2 转接件的强度计算"));
        b.Add(BookBlock.Table(
            new[] { "项目", "计算值", "设计值", "结论" },
            new[]
            {
                new[] { "转接件强度", N(e.TransferStress) + " MPa", N(e.TransferStressLimit) + " MPa",
                        Yes(e.TransferStress <= e.TransferStressLimit) },
            }));
        b.Add(BookBlock.H3($"{no}.3 转接件的挠度计算"));
        b.Add(BookBlock.Table(
            new[] { "项目", "计算值", "设计值", "结论" },
            new[]
            {
                new[] { "转接件挠度", N(e.TransferDeflection, 6) + " mm", N(e.TransferDeflLimit) + " mm",
                        Yes(e.TransferDeflection <= e.TransferDeflLimit) },
            }));
    }

    private static void RenderWeld(List<BookBlock> b, int no, CalcBookInput x)
    {
        if (x.Embed is not { } e) { b.Add(BookBlock.P("本章未参与计算。")); return; }
        b.Add(BookBlock.H3($"{no}.1 受力分析"));
        b.Add(BookBlock.P($"焊缝承受轴力 N = {N(e.AxialDesign)} N、弯矩 M = {N(e.MomentDesign)} N·mm、"
            + $"剪力 V = {N(e.ShearDesign)} N，按三面围焊角焊缝计算。"));
        b.Add(BookBlock.H3($"{no}.2 焊缝特性参数计算"));
        b.Add(BookBlock.Table(
            new[] { "项目", "数值", "单位" },
            new[]
            {
                new[] { "焊缝计算面积 A", N(e.WeldArea), "mm²" },
                new[] { "焊缝截面模量 W", N(e.WeldSectionModulus), "mm³" },
            }));
        b.Add(BookBlock.H3($"{no}.3 焊缝校核计算"));
        b.Add(BookBlock.Table(
            new[] { "项目", "计算值", "设计值", "结论" },
            new[]
            {
                new[] { "焊缝强度", N(e.WeldStress) + " MPa", N(e.WeldStressLimit) + " MPa",
                        Yes(e.WeldStress <= e.WeldStressLimit) },
            }));
        b.Add(BookBlock.P("角焊缝按 σf = √[(N/(βf·A) + M/(βf·W))² + (V/A)²] 计算，βf 取 1.22。"));
    }

    private static void RenderSealant(List<BookBlock> b, int no, CalcBookInput x)
    {
        if (x.Sealant is not { } s) { b.Add(BookBlock.P("本章未参与计算。")); return; }
        b.Add(BookBlock.H3($"{no}.1 硅酮结构密封胶的宽度计算"));
        b.Add(BookBlock.Table(
            new[] { "项目", "计算值", "实际取用", "结论" },
            new[]
            {
                new[] { "结构胶粘接宽度", N(s.StructuralWidth) + " mm", N(s.ActualWidth, 0) + " mm",
                        Yes(s.StructuralWidth <= s.ActualWidth) },
            }));
        b.Add(BookBlock.H3($"{no}.2 硅酮结构密封胶粘接厚度的计算"));
        b.Add(BookBlock.Table(
            new[] { "项目", "计算值", "实际取用", "结论" },
            new[]
            {
                new[] { "结构胶粘接厚度", N(s.StructuralThickness) + " mm", N(s.ActualThickness, 0) + " mm",
                        Yes(s.StructuralThickness <= s.ActualThickness) },
            }));
        b.Add(BookBlock.H3($"{no}.3 结构胶设计总结"));
        b.Add(BookBlock.P("按 JGJ 102-2003 第 5.6.1 条，硅酮结构胶还应满足："
            + "1）粘接宽度 ≥ 7 mm；2）12 mm ≥ 粘接厚度 ≥ 6 mm；"
            + "3）粘接宽度大于厚度，但不宜大于厚度的 2 倍。"));
        b.Add(BookBlock.H3($"{no}.4 立柱连接伸缩缝计算"));
        b.Add(BookBlock.Table(
            new[] { "项目", "计算值", "实际取用", "结论" },
            new[]
            {
                new[] { "立柱伸缩缝", N(s.JointWidth) + " mm", N(s.JointActual, 0) + " mm",
                        Yes(s.JointWidth <= s.JointActual) },
            }));
        b.Add(BookBlock.H3($"{no}.5 耐候胶胶缝计算"));
        b.Add(BookBlock.Table(
            new[] { "项目", "计算值", "实际取用", "结论" },
            new[]
            {
                new[] { "硅酮耐候胶胶缝宽度", N(s.WeatherWidth) + " mm", N(s.WeatherActual, 0) + " mm",
                        Yes(s.WeatherWidth <= s.WeatherActual) },
            }));
    }

    private static void RenderClamp(List<BookBlock> b, int no, CalcBookInput x)
    {
        if (x.Sealant is not { } s) { b.Add(BookBlock.P("本章未参与计算。")); return; }
        b.Add(BookBlock.H3($"{no}.1 压板的弯矩设计值计算"));
        b.Add(BookBlock.P($"压板计算反力 {N(s.ClampLoad)} N。"));
        b.Add(BookBlock.H3($"{no}.2 压板的应力计算"));
        b.Add(BookBlock.Table(
            new[] { "项目", "计算值", "设计值", "结论" },
            new[]
            {
                new[] { "压板最大弯曲应力", N(s.ClampBending) + " MPa", N(s.ClampBendingLimit) + " MPa",
                        Yes(s.ClampBending <= s.ClampBendingLimit) },
                new[] { "压板最大剪应力", N(s.ClampShear) + " MPa", N(s.ClampShearLimit) + " MPa",
                        Yes(s.ClampShear <= s.ClampShearLimit) },
            }));
        b.Add(BookBlock.H3($"{no}.3 螺栓抗拉强度验算"));
        b.Add(BookBlock.Table(
            new[] { "项目", "计算值", "承载力", "结论" },
            new[]
            {
                new[] { "连接螺栓受力", N(s.BoltForce) + " N", N(s.BoltCapacity) + " N",
                        Yes(s.BoltForce <= s.BoltCapacity) },
            }));
    }

    private static void RenderStub(List<BookBlock> b, int no, string sub1, string sub2, string note)
    {
        b.Add(BookBlock.H3($"{no}.1 {sub1}"));
        b.Add(BookBlock.H3($"{no}.2 {sub2}"));
        b.Add(BookBlock.P($"本章未参与计算（{note}）。"));
    }

    private static void RenderAppendix(List<BookBlock> b, CalcBookInput x)
    {
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
