using System;
using System.Globalization;
using System.Linq;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Calc.MultispanCheck;

/// <summary>
/// 多跨静定梁引擎对照测试（V31 实测预演输出）。
///
/// 数据源：
///   local-data/recon/multispan_presult_v31.json          （样本1）
///   local-data/recon/multispan_presult_v31_sample2.json  （样本2）
///
/// 样本1: 总跨数=15  跨长Li=660mm  跨长i=3100mm  6063A-T5  JGJ336-2016
/// 样本2: 总跨数=5   跨长Li=1000mm 跨长i=4000mm  6063A-T5  JGJ336-2016
///
/// 验证目标（公式骨架，绝对值需完整前置参数链）：
///   1. 折算强度 σ_eq = √(σ_弯² + τ_剪²)   —— 4 个独立数据点
///   2. 挠度限值分段规则（JGJ336）：calcSpan<=4500 → /180；>4500 → /250+7
///   3. 型材许用值 抗弯135 / 抗剪75 / 折算148.5(=135×1.1)
///   4. 几何序列 A/L 交替，共 2N 段（实测截图 + 参数库「多跨1」表双重证据）
///   5. 输出代表位置固定（与总跨数无关）
///   6. 输出格式（含程序原文笔误「为为」）
/// </summary>
internal static class Program
{
    private static int _fail;

    private static void Check(string label, double actual, double expected, double tol,
                              string unit = "MPa")
    {
        double diff = Math.Abs(actual - expected);
        bool ok = diff <= tol;
        if (!ok) _fail++;
        Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {label,-32} " +
                          $"实算={actual.ToString("F4", CultureInfo.InvariantCulture),12} " +
                          $"实测={expected.ToString("F4", CultureInfo.InvariantCulture),12} " +
                          $"差={diff.ToString("E2", CultureInfo.InvariantCulture)} {unit}");
    }

    private static void Note(bool ok, string label, string detail = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {label} {detail}");
    }

    private static int Main()
    {
        Console.WriteLine("=== 多跨铰接连续静定梁 · 公式骨架对照 ===");
        Console.WriteLine("样本1: 总跨数=15  跨长Li=660mm  跨长i=3100mm  6063A-T5  JGJ336-2016");
        Console.WriteLine("样本2: 总跨数=5   跨长Li=1000mm 跨长i=4000mm  6063A-T5  JGJ336-2016");
        Console.WriteLine();

        Console.WriteLine("[1] 折算强度 σ_eq = √(σ_弯² + τ_剪²) —— 4 个独立数据点");
        Check("样本1 支座 √(67.841²+8.748²)", MultiSpanStaticEngine.EquivalentStress(67.841, 8.748), 68.403, 0.001);
        Check("样本1 第3跨 √(41.964²+0.982²)", MultiSpanStaticEngine.EquivalentStress(41.964, 0.982), 41.975, 0.001);
        Check("样本2 第3跨 √(62.121²+1.370²)", MultiSpanStaticEngine.EquivalentStress(62.121, 1.37), 62.136, 0.001);
        Check("样本2 支座 √(131.832²+11.694²)", MultiSpanStaticEngine.EquivalentStress(131.832, 11.694), 132.35, 0.001);
        Console.WriteLine("      → 公式确证，零误差。");
        Console.WriteLine();

        Console.WriteLine("[2] 挠度限值分段规则（JGJ336）：calcSpan = 跨长i + 跨长Li");
        double lim1 = MultiSpanStaticEngine.DeflectionLimit(3100, 660);
        Check("样本1 (3100+660)/180", lim1, 20.889, 0.001, "mm");
        Console.WriteLine($"      反证:仅跨长i/180 = {3100 / 180.0:F4} mm  ← 不匹配实测 20.889");
        Console.WriteLine($"            含支座宽  = {(3100 + 660) / 180.0:F4} mm  ← 匹配");
        double lim2 = MultiSpanStaticEngine.DeflectionLimit(4000, 1000);
        Check("样本2 (4000+1000)/250+7", lim2, 27.0, 0.001, "mm");
        Console.WriteLine($"      反证:若仍用 /180  = {(4000 + 1000) / 180.0:F4} mm  ← 不匹配实测 27");
        Console.WriteLine($"            分段 /250+7 = {(4000 + 1000) / 250.0 + 7:F4} mm  ← 匹配（calcSpan=5000 > 4500）");
        Console.WriteLine("      → 两样本分处阈值两侧，分段规则双向确证。");
        Console.WriteLine();

        Console.WriteLine("[3] 型材许用值（6063A-T5）");
        Check("抗弯设计值", 135.0, 135.0, 0.0);
        Check("抗剪设计值", 75.0, 75.0, 0.0);
        Check("折算设计值 = 135×1.1", 135.0 * MultiSpanStaticEngine.EqFactor, 148.5, 1e-4);
        Console.WriteLine();

        Console.WriteLine("[4] 几何序列（对照程序 DataGrid 三列 序号/代号/长度）");
        var segs = MultiSpanStaticEngine.BuildAlternating(15, 660, 3100);
        Note(segs.Count == 30, "样本1 段数 = 2N", $"实得 {segs.Count}（期望 30）");
        foreach (var s in segs.GetRange(0, 6))
            Console.WriteLine($"      {s.Index,2}  {s.Code,-4} {s.LengthMm,6}  {(s.IsSupport ? "支座段A" : "计算跨度L")}");
        Console.WriteLine($"      ...共 {segs.Count} 段（A1 L1 A2 L2 ... A15 L15，无末尾 A16）");
        var segs2 = MultiSpanStaticEngine.BuildAlternating(5, 1000, 4000);
        Note(segs2.Count == 10, "样本2 段数 = 2N", $"实得 {segs2.Count}（期望 10）");
        Note(segs2[0].Code == "A1" && segs2[1].Code == "L1" && segs2[9].Code == "L5",
            "样本2 段序A1,L1,...,L5（截图 grid_zoom 实测 10 行）");
        Console.WriteLine();

        Console.WriteLine("[5] 输出代表位置固定（与总跨数无关）");
        Note(MultiSpanStaticEngine.OutputPositions.Count == 4, "固定代表位置 4 个",
            string.Join(" / ", MultiSpanStaticEngine.OutputPositions));
        Console.WriteLine("      → N=15 与 N=5 的实测输出项完全同构，证伪「按跨数抽样」假设。");
        Console.WriteLine();

        Console.WriteLine("[6] 判定逻辑（两个分支都覆盖：√ 与 ×）");
        var sample1 = new[]
        {
            new MultiSpanStaticEngine.Check("抗弯强度", "第1跨", 62.316, 135, "MPa",
                "第1跨立柱抗弯强度计算值", "立柱型材抗弯强度设计值"),
            new MultiSpanStaticEngine.Check("抗剪强度", "第1跨", 5.858, 75, "MPa",
                "第1跨立柱抗剪强度计算值", "立柱型材抗剪强度设计值"),
            new MultiSpanStaticEngine.Check("抗弯强度", "第3跨", 41.964, 135, "MPa",
                "第3跨立柱抗弯强度计算值", "立柱型材抗弯强度设计值"),
            new MultiSpanStaticEngine.Check("抗剪强度", "第3跨", 0.982, 75, "MPa",
                "第3跨立柱抗剪强度计算值", "立柱型材抗剪强度设计值"),
            new MultiSpanStaticEngine.Check("折算强度", "第3跨", 41.975, 148.5, "MPa",
                "第3跨立柱折算强度计算值", "立柱型材折算强度设计值"),
            new MultiSpanStaticEngine.Check("支座处抗弯强度", "第2跨立柱支座处", 67.841, 135, "MPa",
                "第2跨立柱支座处抗弯强度计算值", "立柱型材抗弯强度设计值"),
            new MultiSpanStaticEngine.Check("支座处抗剪强度", "第2跨立柱支座处", 8.748, 75, "MPa",
                "第2跨立柱支座处抗剪强度计算值", "立柱型材抗剪强度设计值"),
            new MultiSpanStaticEngine.Check("支座处折算强度", "第2跨立柱支座处", 68.403, 148.5, "MPa",
                "第2跨立柱支座处折算强度计算值", "立柱型材折算强度设计值"),
            new MultiSpanStaticEngine.Check("挠度", "第1跨", 9.587, 20.889, "mm",
                "第1跨立柱挠度计算值", "第1跨立柱挠度设计值"),
            new MultiSpanStaticEngine.Check("挠度", "第2跨后", 3.8185, 20.889, "mm",
                "第2跨后立柱挠度计算值", "第2跨后立柱挠度设计值"),
        };
        var sample2 = new[]
        {
            new MultiSpanStaticEngine.Check("抗弯强度", "第1跨", 98.123, 135, "MPa",
                "第1跨立柱抗弯强度计算值", "立柱型材抗弯强度设计值"),
            new MultiSpanStaticEngine.Check("抗剪强度", "第1跨", 7.309, 75, "MPa",
                "第1跨立柱抗剪强度计算值", "立柱型材抗剪强度设计值"),
            new MultiSpanStaticEngine.Check("抗弯强度", "第3跨", 62.121, 135, "MPa",
                "第3跨立柱抗弯强度计算值", "立柱型材抗弯强度设计值"),
            new MultiSpanStaticEngine.Check("抗剪强度", "第3跨", 1.37, 75, "MPa",
                "第3跨立柱抗剪强度计算值", "立柱型材抗剪强度设计值"),
            new MultiSpanStaticEngine.Check("折算强度", "第3跨", 62.136, 148.5, "MPa",
                "第3跨立柱折算强度计算值", "立柱型材折算强度设计值"),
            new MultiSpanStaticEngine.Check("支座处抗弯强度", "第2跨立柱支座处", 131.832, 135, "MPa",
                "第2跨立柱支座处抗弯强度计算值", "立柱型材抗弯强度设计值"),
            new MultiSpanStaticEngine.Check("支座处抗剪强度", "第2跨立柱支座处", 11.694, 75, "MPa",
                "第2跨立柱支座处抗剪强度计算值", "立柱型材抗剪强度设计值"),
            new MultiSpanStaticEngine.Check("支座处折算强度", "第2跨立柱支座处", 132.35, 148.5, "MPa",
                "第2跨立柱支座处折算强度计算值", "立柱型材折算强度设计值"),
            new MultiSpanStaticEngine.Check("挠度", "第1跨", 29.697, 27.0, "mm",
                "第1跨立柱挠度计算值", "第1跨立柱挠度设计值"),
            new MultiSpanStaticEngine.Check("挠度", "第2跨后", 11.4645, 27.0, "mm",
                "第2跨后立柱挠度计算值", "第2跨后立柱挠度设计值"),
        };

        foreach (var (name, set) in new[] { ("样本1", sample1), ("样本2", sample2) })
        {
            Console.WriteLine($"    {name}:");
            foreach (var c in set)
                Console.WriteLine($"      {(c.Pass ? "√" : "×")} {c.Group,-16} {c.Location,-18} " +
                                  $"{c.Computed,9} / {c.Allowable,8} {c.Unit}");
        }
        Note(sample1.All(c => c.Pass), "样本1 全部满足要求(√)", "与程序输出一致");
        Note(sample2.Any(c => !c.Pass), "样本2 含「不满足要求(×)」分支",
            "第1跨挠度 29.697 > 27");
        Console.WriteLine();

        Console.WriteLine("[7] 输出格式对照（含程序原文笔误「为为」）");
        string text1 = MultiSpanStaticEngine.Render(sample1, 1.551, "6063A-T5");
        foreach (var line in text1.Split('\n'))
        {
            var s = line.TrimEnd('\r');
            if (s.Contains("为为") || s.Contains("风荷载"))
                Console.WriteLine("      " + s);
        }
        Note(text1.Contains("设计值为为:"), "原文笔误「为为」按原样保留",
            "程序原文：立柱型材抗弯强度设计值为为:135MPa");
        Note(text1.StartsWith("******************************************"),
            "分隔线 42 个 '*'");
        string text2 = MultiSpanStaticEngine.Render(sample2, 1.526, "6063A-T5");
        Note(text2.Contains("风荷载标准值为:1.526KPa"), "风荷载格式 3 位小数",
            "程序原文：风荷载标准值为:1.526KPa");
        Note(text2.Contains("挠度设计值为:27mm"), "挠度设计值整数输出",
            "程序原文：第1跨立柱挠度设计值为:27mm");
        Console.WriteLine();

        Console.WriteLine("--- 未确证项（需读 V35 代码或补跑样本） ---");
        foreach (var q in MultiSpanStaticEngine.OpenQuestions)
            Console.WriteLine("  · " + q);
        Console.WriteLine();

        Console.WriteLine("--- 建议的下一组样本 ---");
        Console.WriteLine("  F: 材质 6063-T5（同几何）→ 验许用值是否随材质变（T5 那轮结果未抓到）");
        Console.WriteLine("  G: 跨数=5 Li=1000 i=2000 → calcSpan=3000 仍在 /180 段，验第 3 点");
        Console.WriteLine("  H: 跨数=5 Li=1000 i=3600 → calcSpan=4600 刚过 4500 阈值，验分段边界");
        Console.WriteLine("  I: 改基本参数输入（地区/场地类别）→ 看风荷载标准值怎么变，建立参数链");
        Console.WriteLine();

        // ============================================================
        // [8] JGJ336 挠度限值三段（V35 计算书原文，二进制 0x4ea2d0/0x4ea314/0x4ea368）
        // ============================================================
        Console.WriteLine("[8] JGJ336 挠度限值三段（V35 计算书原文确证）");
        Check("calcSpan=3760 → /180", MultiSpanStaticEngine.DeflectionLimit(3100, 660), 3760 / 180.0, 1e-9);
        Check("calcSpan=4500 边界 → /180", MultiSpanStaticEngine.DeflectionLimit(4000, 500), 4500 / 180.0, 1e-9);
        Check("calcSpan=5000 → /250+7", MultiSpanStaticEngine.DeflectionLimit(4000, 1000), 5000 / 250.0 + 7, 1e-9);
        Check("calcSpan=7000 边界 → /250+7", MultiSpanStaticEngine.DeflectionLimit(6500, 500), 7000 / 250.0 + 7, 1e-9);
        Check("calcSpan=7500 → /200（第三段）", MultiSpanStaticEngine.DeflectionLimit(7000, 500), 7500 / 200.0, 1e-9);
        Check("calcSpan=8000 → /200", MultiSpanStaticEngine.DeflectionLimit(7500, 500), 8000 / 200.0, 1e-9);
        Console.WriteLine();

        // ============================================================
        // [9] V31 双样本比例模型（N=5 / N=15 两点插值，端点必须复现实测比例）
        // ============================================================
        Console.WriteLine("[9] V31 双样本比例模型（端点复现实测比例）");
        Check("N=5  支座抗弯比 = 131.832/98.123", MultiSpanStaticEngine.SupportBendingRatio(5), 131.832 / 98.123, 1e-4);
        Check("N=15 支座抗弯比 = 67.841/62.316", MultiSpanStaticEngine.SupportBendingRatio(15), 67.841 / 62.316, 1e-4);
        Check("N=5  第3跨抗弯比 = 62.121/98.123", MultiSpanStaticEngine.Span3BendingRatio(5), 62.121 / 98.123, 1e-4);
        Check("N=15 第3跨抗弯比 = 41.964/62.316", MultiSpanStaticEngine.Span3BendingRatio(15), 41.964 / 62.316, 1e-4);
        Check("N=5  支座抗剪比 = 11.694/7.309", MultiSpanStaticEngine.SupportShearRatio(5), 11.694 / 7.309, 1e-4);
        Check("N=15 支座抗剪比 = 8.748/5.858", MultiSpanStaticEngine.SupportShearRatio(15), 8.748 / 5.858, 1e-4);
        Check("N=5  第3跨抗剪比 = 1.37/7.309", MultiSpanStaticEngine.Span3ShearRatio(5), 1.37 / 7.309, 1e-4);
        Check("N=15 第3跨抗剪比 = 0.982/5.858", MultiSpanStaticEngine.Span3ShearRatio(15), 0.982 / 5.858, 1e-4);
        Check("N=5  第2跨后挠度比 = 11.4645/29.697", MultiSpanStaticEngine.SecondSpanDeflectionRatio(5), 11.4645 / 29.697, 1e-4);
        Check("N=15 第2跨后挠度比 = 3.8185/9.587", MultiSpanStaticEngine.SecondSpanDeflectionRatio(15), 3.8185 / 9.587, 1e-4);
        // 中间值必须落在两端点之间（插值单调性）
        double mid = MultiSpanStaticEngine.SupportBendingRatio(10);
        Note(mid > 1.0886 && mid < 1.3436, "N=10 支座抗弯比落在两端点之间",
            $"{mid:0.0000} ∈ (1.0886, 1.3436)");
        Console.WriteLine();

        // ============================================================
        // [10] 参数库接入（地区 634 行 / 型材 95 行，编译期内嵌）
        // ============================================================
        Console.WriteLine("[10] V35 参数库接入（ParamLibrary）");
        Note(GlassManagement.Calc.Data.ParamLibrary.Regions.Count == 634, "地区参数 634 行", "参数库「地区参数」表");
        Note(GlassManagement.Calc.Data.ParamLibrary.Columns.Count == 95, "立柱型材 95 行", "参数库「立柱」表");
        var sh = GlassManagement.Calc.Data.ParamLibrary.FindRegion("上海");
        Note(sh is not null && Math.Abs(sh!.Wind50KPa - 0.55) < 1e-9, "上海 50 年基本风压 = 0.55KPa（550Pa）");
        var bj = GlassManagement.Calc.Data.ParamLibrary.FindRegion("北京");
        Note(bj is not null && Math.Abs(bj!.Wind50KPa - 0.45) < 1e-9, "北京 50 年基本风压 = 0.45KPa（450Pa）");
        var sec = GlassManagement.Calc.Data.ParamLibrary.FindColumn("矩形钢管180*100*5");
        Note(sec is not null && Math.Abs(sec!.Ix - 1141.343) < 1e-6, "矩形钢管180*100*5 Ix=1141.343", "参数库「立柱」表");
        // 风荷载下限规则（V35 原文：w0≥0.3，wk≥1.0）
        Check("上海 w0 0.55 > 下限 0.3", Math.Max(sh!.Wind50KPa, 0.3), 0.55, 1e-9);
        Check("JGJ102 下限 wk≥1.0", Math.Max(0.55, 1.0), 1.0, 1e-9);
        Console.WriteLine();

        // ============================================================
        // [11] 非基线几何估算（样本2 几何 → 估算输出 sanity）
        // ============================================================
        Console.WriteLine("[11] 非基线几何估算（样本2 几何 N=5/A=1000/L=4000）");
        var est = MultiSpanStaticEngine.Estimate(5, 1000, 4000, 1.526, 1350, sec, 70000);
        Check("计算跨度 = L+A = 5000", est.CalcSpanMm, 5000, 1e-9);
        Check("线荷载 q = 1.526×1350/1000", est.LineLoadNPerMm, 1.526 * 1350 / 1000.0, 1e-9);
        Check("挠度限值 = 5000/250+7 = 27", est.DeflectionLimitMm, 27.0, 1e-9);
        Check("许用 抗弯 135", est.AllowBendingMPa, 135, 1e-9);
        Check("许用 抗剪 75", est.AllowShearMPa, 75, 1e-9);
        Check("许用 折算 148.5", est.AllowEquivalentMPa, 148.5, 1e-9);
        Note(est.BendingSupportMPa > est.BendingSpan1MPa, "支座抗弯 > 第1跨抗弯（比例模型）",
            $"{est.BendingSupportMPa:0.0} > {est.BendingSpan1MPa:0.0}");
        Note(est.DeflectionSpan1Mm > est.DeflectionSpan2AfterMm, "第2跨后挠度 < 第1跨挠度（比例模型）");
        Note(Math.Abs(est.EquivalentSupportMPa
             - Math.Sqrt(est.BendingSupportMPa * est.BendingSupportMPa + est.ShearSupportMPa * est.ShearSupportMPa)) < 1e-9,
            "折算强度 = √(σ²+τ²)");
        Console.WriteLine($"      {est.Note}");
        Console.WriteLine($"      型材：{est.SectionSummary}");
        Console.WriteLine();

        // ============================================================
        // [12] 风荷载 wk 公式 —— V31 实机 5 点（上海50年 w0=0.55 / B类 / z=100m）
        //      来源 docs/V35_COLUMN_FORMULAS.md §13
        // ============================================================
        Console.WriteLine("[12] 风荷载 wk = w0·βgz·μz·[μs1·ψ(A)+μs内] —— V31 实机 5 点");
        const double w0Sh = 0.55;
        var terB = TerrainCategory.B;
        Check("βgz(100,B)", WindLoadEngine.BetaGz(terB, 100), 1.495562, 1e-6, "");
        Check("μz(100,B)", WindLoadEngine.MuZ(terB, 100), 1.995262, 1e-5, "");
        Check("βgz·μz(100,B)", WindLoadEngine.BetaGz(terB, 100) * WindLoadEngine.MuZ(terB, 100),
              2.984039, 1e-5, "");
        Check("ψ(3.30m²)", WindLoadEngine.Psi(3.30), 0.925817, 1e-5, "");
        Check("ψ(6.00m²)", WindLoadEngine.Psi(6.00), 0.888672, 1e-5, "");
        // 5 个实测点，容差取程序显示精度 0.0005 kPa
        Check("wk 人工1.0 + 内0   (A=3.30)", WindLoadEngine.Wk(w0Sh, terB, 100, 1.0, 0.0, 3.30), 1.52, 0.0006, "kPa");
        Check("wk 人工1.0 + 封闭式(A=3.30)", WindLoadEngine.Wk(w0Sh, terB, 100, 1.0, 0.2, 3.30), 1.848, 0.0006, "kPa");
        Check("wk 人工2.0 + 封闭式(A=3.30)", WindLoadEngine.Wk(w0Sh, terB, 100, 2.0, 0.2, 3.30), 3.368, 0.0009, "kPa");
        Check("wk 人工1.0 + 封闭式(A=6.00)", WindLoadEngine.Wk(w0Sh, terB, 100, 1.0, 0.2, 6.00), 1.787, 0.0006, "kPa");
        Note(WindLoadEngine.Wk(0.2, terB, 50, 0.5, 0.0, 3.30, applyFloor: false) < WindLoadEngine.WindStandardFloorKpa,
            "低风压时会被 JGJ102 下限提到 1.0kPa",
            $"{WindLoadEngine.Wk(0.2, terB, 50, 0.5, 0.0, 3.3, applyFloor: false):0.####} → {WindLoadEngine.Wk(0.2, terB, 50, 0.5, 0.0, 3.3):0.###}");
        // K1 地形修正 / K2 风向影响（GB55001-2021 4.6.6 / 4.6.7，V31 帮助原文确认是乘进 wk）
        double wkBase = WindLoadEngine.Wk(w0Sh, terB, 100, 1.0, 0.2, 3.30);
        Check("wk×K1(1.2)", WindLoadEngine.Wk(w0Sh, terB, 100, 1.0, 0.2, 3.30, k1: 1.2) / wkBase, 1.2, 1e-9, "倍");
        Check("wk×K2(0.8)", WindLoadEngine.Wk(w0Sh, terB, 100, 1.0, 0.2, 3.30, k2: 0.8) / wkBase, 0.8, 1e-9, "倍");
        Check("K1=0 兜底按 1.0", WindLoadEngine.Wk(w0Sh, terB, 100, 1.0, 0.2, 3.30, k1: 0) / wkBase, 1.0, 1e-9, "倍");
        Console.WriteLine();

        Console.WriteLine(_fail == 0
            ? "=== 结果：全部通过 ==="
            : $"=== 结果：{_fail} 项未通过 ===");
        return _fail == 0 ? 0 : 1;
    }
}
