using System;
using System.Collections.Generic;
using System.Linq;

namespace GlassManagement.Calc.Engines
{
    /// <summary>
    /// 民用建筑节能计算加权计算器引擎（菜单 id=136，窗体「节能计算加权计算器」）。
    /// 输入 N 行，每行 3 列：列1 = 权重（面积 A_i）、列2 = 值1（X_i）、列3 = 值2（Y_i）。
    /// 输出（V31 运行期实测，逐位直读）：
    ///   out1 = Σ A_i
    ///   out2 = Σ(A_i · X_i) / Σ A_i   （round3）
    ///   out3 = Σ(A_i · Y_i) / Σ A_i   （round3）
    /// 用途：民用建筑节能按面积加权平均（如加权平均传热系数等）。列1 为权重的通用加权工具。
    /// </summary>
    public sealed class WeightedAverageEngine
    {
        public readonly record struct Row(double Weight, double ValueA, double ValueB);

        public readonly record struct Result(double SumWeight, double WeightedA, double WeightedB);

        public static Result Compute(IReadOnlyList<Row> rows)
        {
            double sumW = 0, sumWA = 0, sumWB = 0;
            foreach (var r in rows)
            {
                sumW += r.Weight;
                sumWA += r.Weight * r.ValueA;
                sumWB += r.Weight * r.ValueB;
            }
            double wa = sumW != 0 ? WindLoadEngine.VbaRound(sumWA / sumW, 3) : 0;
            double wb = sumW != 0 ? WindLoadEngine.VbaRound(sumWB / sumW, 3) : 0;
            return new Result(sumW, wa, wb);
        }

        /// <summary>输出字符串（与 V31 三个结果框一致，去尾零格式）。</summary>
        public static string FormatSumWeight(double v) => v.ToString("0.###");
        public static string FormatWeighted(double v) => v.ToString("0.###");
    }
}
