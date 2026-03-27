using System;
using System.IO;
using NPOI.HWPF;

var dir = @"C:\Program Files (x86)\汇宝幕墙门窗结构计算软件V31.0D\计算书样本文件集";
foreach (var file in Directory.GetFiles(dir, "*.DOC"))
{
    Console.WriteLine($"=== {Path.GetFileName(file)} ===");
    using var fs = File.OpenRead(file);
    var doc = new HWPFDocument(fs);
    var range = doc.GetRange();
    var count = Math.Min(range.NumParagraphs, 260);
    for (int i = 0; i < count; i++)
    {
        var p = range.GetParagraph(i);
        var txt = (p.Text ?? "").Replace("\r", "").Trim();
        if (string.IsNullOrWhiteSpace(txt)) continue;
        if (txt.Length > 70) txt = txt.Substring(0, 70);
        if (txt.Contains("目录") || txt.Contains("计算") || txt.StartsWith("一") || txt.StartsWith("二") || txt.StartsWith("三") || txt.StartsWith("四") || txt.StartsWith("1") || txt.StartsWith("2") || txt.StartsWith("3") || txt.StartsWith("4"))
        {
            Console.WriteLine($"{i:D4}: {txt}");
        }
    }
    Console.WriteLine();
}
