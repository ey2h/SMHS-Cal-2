using System;
using System.Collections.Generic;
using System.Linq;

namespace GlassManagement.Calc.Engines
{
    /// <summary>
    /// 一维套裁下料优化引擎（菜单 id=192「型材优化 / 线材优化数据输入」）。
    /// <para>⚠ 算法说明：V31 的「算法一(优选算法)/算法二」是两套私有启发式，其精确实现需从
    /// 程序代码逐指令还原（镜像 0x527690），当前尚未完成。本引擎实现行业标准的
    /// <b>FFD（First-Fit Decreasing，降序首次适应）</b>，属通用最优近似算法，
    /// 输出格式与 V31 一致（每根原材料的切割组合 + 余料 + 利用率），
    /// 但<b>具体方案行不保证与 V31 逐行一致</b>（同余料下可能换一种切法）。</para>
    /// <para>输入：零件（下料长度, 数量）、原材料长度、锯缝、端头余量。</para>
    /// </summary>
    public sealed class CuttingStockEngine
    {
        public readonly record struct Part(double Length, int Qty);

        /// <summary>一种切割组合（同一组合的多根原料合并显示）。</summary>
        public sealed record CutPattern(
            IReadOnlyList<double> Pieces,   // 该组合切出的零件长度（降序）
            int Count,                      // 该组合用到几根原料
            double UsedLength,              // 单根已用长度（含锯缝）
            double Waste,                   // 单根余料
            double Utilization);            // 单根利用率 0..1

        public sealed record Result(
            IReadOnlyList<CutPattern> Patterns,
            int StockCount,
            double TotalStockLength,
            double TotalPieceLength,
            double OverallUtilization,
            string Note);

        /// <summary>
        /// FFD 套裁。kerf = 每刀锯缝（mm），endTrim = 每根原料端头余量（mm）。
        /// 一根原料可容：Σpieces + kerf×(n−1) + endTrim ≤ stockLength。
        /// </summary>
        public static Result Optimize(IEnumerable<Part> parts, double stockLength,
                                      double kerf = 5.0, double endTrim = 50.0)
        {
            var pieces = new List<double>();
            foreach (var p in parts)
            {
                if (p.Length <= 0 || p.Qty <= 0) continue;
                for (int i = 0; i < p.Qty; i++) pieces.Add(p.Length);
            }
            if (pieces.Count == 0 || stockLength <= 0)
                return new Result(Array.Empty<CutPattern>(), 0, 0, 0, 0, "无有效零件");

            // 降序（FFD 前提）
            pieces.Sort((a, b) => b.CompareTo(a));

            var overlong = new List<double>();
            var bins = new List<List<double>>();
            foreach (var piece in pieces)
            {
                if (piece + endTrim > stockLength) { overlong.Add(piece); continue; }
                bool placed = false;
                foreach (var bin in bins)
                {
                    double used = bin.Sum() + kerf * Math.Max(0, bin.Count - 1) + endTrim;
                    if (used + (bin.Count > 0 ? kerf : 0) + piece <= stockLength)
                    {
                        bin.Add(piece);
                        placed = true;
                        break;
                    }
                }
                if (!placed) bins.Add(new List<double> { piece });
            }

            // 按「组合」聚合
            var grouped = new Dictionary<string, (List<double> Pieces, int Count)>();
            foreach (var bin in bins)
            {
                var sorted = bin.OrderByDescending(x => x).ToList();
                string key = string.Join(",", sorted.Select(x => x.ToString("0.###")));
                if (grouped.TryGetValue(key, out var g)) g.Count++;
                else grouped[key] = (sorted, 1);
            }

            var patterns = new List<CutPattern>();
            foreach (var kv in grouped.OrderByDescending(k => k.Value.Pieces.Count)
                                       .ThenByDescending(k => k.Value.Pieces.Sum()))
            {
                var ps = kv.Value.Pieces;
                double used = ps.Sum() + kerf * Math.Max(0, ps.Count - 1) + endTrim;
                double waste = stockLength - used;
                patterns.Add(new CutPattern(ps, kv.Value.Count, used, waste,
                    stockLength > 0 ? ps.Sum() / stockLength : 0));
            }

            int stockCount = bins.Count;
            double totalStock = stockCount * stockLength;
            double totalPiece = pieces.Sum(x => x) - overlong.Sum();
            string note = overlong.Count > 0
                ? $"⚠ {overlong.Count} 根零件超长（>{stockLength - endTrim:0.###}mm）已跳过：{string.Join("、", overlong.Distinct())}"
                : $"FFD 降序首次适应：原料 {stockLength:0.###}mm，锯缝 {kerf:0.###}mm，端头余量 {endTrim:0.###}mm";

            return new Result(patterns, stockCount, totalStock, totalPiece,
                totalStock > 0 ? totalPiece / totalStock : 0, note);
        }

        /// <summary>文本报表（可保存/复制）。</summary>
        public static string ToReport(Result r, double stockLength)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"原材料长度：{stockLength:0.###} mm");
            sb.AppendLine(r.Note);
            sb.AppendLine(new string('-', 64));
            sb.AppendLine($"{"序号",-4}{"切割组合(mm)",-38}{"根数",-6}{"余料",-8}{"利用率"}");
            int idx = 1;
            foreach (var p in r.Patterns)
            {
                sb.AppendLine($"{idx++,-4}{string.Join(" + ", p.Pieces.Select(x => x.ToString("0.###"))),-38}" +
                              $"{p.Count,-6}{p.Waste:0.###}   {p.Utilization * 100:0.##}%");
            }
            sb.AppendLine(new string('-', 64));
            sb.AppendLine($"共需原料 {r.StockCount} 根，总长 {r.TotalStockLength:0.###} mm，" +
                          $"零件总长 {r.TotalPieceLength:0.###} mm，综合利用率 {r.OverallUtilization * 100:0.##}%");
            return sb.ToString();
        }
    }
}
