using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace GlassManagement.Calc.Book;

/// <summary>计算书里的一块：标题 / 正文 / 表格 / 分页。</summary>
public sealed record BookBlock(BookBlockKind Kind, string Text,
                               string[]? Headers = null, string[][]? Rows = null)
{
    public static BookBlock H1(string t) => new(BookBlockKind.H1, t);
    public static BookBlock H2(string t) => new(BookBlockKind.H2, t);
    public static BookBlock H3(string t) => new(BookBlockKind.H3, t);
    public static BookBlock P(string t) => new(BookBlockKind.P, t);
    public static BookBlock PageBreak() => new(BookBlockKind.PageBreak, "");
    public static BookBlock Table(string[] headers, string[][] rows)
        => new(BookBlockKind.Table, "", headers, rows);

    /// <summary>纯文本投影（测试与检索用；表格按「表头 + 行」摊平）。</summary>
    public string FlatText
    {
        get
        {
            if (Kind != BookBlockKind.Table) return Text ?? "";
            var sb = new System.Text.StringBuilder();
            if (Headers is not null) sb.Append(string.Join(" | ", Headers)).Append('\n');
            if (Rows is not null)
                foreach (var r in Rows) sb.Append(string.Join(" | ", r)).Append('\n');
            return sb.ToString();
        }
    }
}

public enum BookBlockKind { H1, H2, H3, P, Table, PageBreak }

/// <summary>
/// 最小 OOXML(.docx) 生成器 —— 只依赖 BCL，不引 DocumentFormat.OpenXml。
///
/// 为什么手写：桌面端没有 OpenXml 依赖，而计算书只需要「标题 + 段落 + 分页」三种块，
/// 引入一个几 MB 的包不值得。生成的包结构是标准 WordprocessingML，Word/WPS 均可打开。
/// </summary>
public static class CalcBookWriter
{
    private const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <summary>把块序列写成 .docx 文件。</summary>
    public static void Write(IReadOnlyList<BookBlock> blocks, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        Add(zip, "[Content_Types].xml", ContentTypes);
        Add(zip, "_rels/.rels", RootRels);
        Add(zip, "word/_rels/document.xml.rels", DocRels);
        Add(zip, "word/document.xml", BuildDocument(blocks));
    }

    private static void Add(ZipArchive zip, string name, string content)
    {
        var e = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var s = e.Open();
        var bytes = new UTF8Encoding(false).GetBytes(content);
        s.Write(bytes, 0, bytes.Length);
    }

    private static string BuildDocument(IReadOnlyList<BookBlock> blocks)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<w:document xmlns:w=\"").Append(W).Append("\"><w:body>");
        foreach (var b in blocks)
        {
            switch (b.Kind)
            {
                case BookBlockKind.PageBreak:
                    sb.Append("<w:p><w:r><w:br w:type=\"page\"/></w:r></w:p>");
                    break;
                case BookBlockKind.H1:
                    sb.Append(Para(b.Text, 28, true, true));
                    break;
                case BookBlockKind.H2:
                    sb.Append(Para(b.Text, 24, true, false));
                    break;
                case BookBlockKind.H3:
                    sb.Append(Para(b.Text, 22, true, false));
                    break;
                case BookBlockKind.Table:
                    sb.Append(Table(b.Headers ?? Array.Empty<string>(),
                                    b.Rows ?? Array.Empty<string[]>()));
                    break;
                default:
                    sb.Append(Para(b.Text, 21, false, false));
                    break;
            }
        }
        // 一节 A4 纵向
        sb.Append("<w:sectPr><w:pgSz w:w=\"11906\" w:h=\"16838\"/>")
          .Append("<w:pgMar w:top=\"1134\" w:right=\"1134\" w:bottom=\"1134\" w:left=\"1134\"/>")
          .Append("</w:sectPr>");
        sb.Append("</w:body></w:document>");
        return sb.ToString();
    }

    /// <summary>单线边框表格；单元格内必须有 &lt;w:p&gt;，否则 Word 判为损坏。</summary>
    private static string Table(string[] headers, string[][] rows)
    {
        const string borders =
            "<w:tblBorders>" +
            "<w:top w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/>" +
            "<w:left w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/>" +
            "<w:bottom w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/>" +
            "<w:right w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/>" +
            "<w:insideH w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/>" +
            "<w:insideV w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/>" +
            "</w:tblBorders>";

        var sb = new StringBuilder();
        int cols = headers.Length > 0
            ? headers.Length
            : (rows.Length > 0 ? rows[0].Length : 1);
        sb.Append("<w:tbl><w:tblPr><w:tblW w:w=\"5000\" w:type=\"pct\"/>").Append(borders)
          .Append("</w:tblPr>");
        // ★ <w:tblGrid> 是必填子元素：缺了 Word 会提示"内容有问题"，
        //   python-docx 直接抛 InvalidXmlError（required <w:tblGrid> child element not present）。
        sb.Append("<w:tblGrid>");
        for (int i = 0; i < cols; i++) sb.Append("<w:gridCol w:w=\"1700\"/>");
        sb.Append("</w:tblGrid>");
        if (headers.Length > 0) sb.Append(Row(headers, true));
        foreach (var r in rows) sb.Append(Row(r, false));
        sb.Append("</w:tbl>");
        // 表后补一个空段落，避免两张表贴在一起
        sb.Append("<w:p/>");
        return sb.ToString();
    }

    private static string Row(string[] cells, bool bold)
    {
        var sb = new StringBuilder("<w:tr>");
        foreach (var c in cells)
        {
            sb.Append("<w:tc><w:tcPr><w:tcW w:w=\"0\" w:type=\"auto\"/></w:tcPr><w:p><w:pPr>");
            sb.Append("<w:rPr><w:rFonts w:ascii=\"宋体\" w:eastAsia=\"宋体\" w:hAnsi=\"宋体\"/>");
            sb.Append("<w:sz w:val=\"18\"/>");
            if (bold) sb.Append("<w:b/>");
            sb.Append("</w:rPr></w:pPr><w:r><w:rPr>");
            sb.Append("<w:rFonts w:ascii=\"宋体\" w:eastAsia=\"宋体\" w:hAnsi=\"宋体\"/>");
            sb.Append("<w:sz w:val=\"18\"/>");
            if (bold) sb.Append("<w:b/>");
            sb.Append("</w:rPr><w:t xml:space=\"preserve\">").Append(Esc(c)).Append("</w:t></w:r></w:p></w:tc>");
        }
        sb.Append("</w:tr>");
        return sb.ToString();
    }

    private static string Para(string text, int halfPoints, bool bold, bool center)
    {
        var sb = new StringBuilder();
        sb.Append("<w:p><w:pPr>");
        if (center) sb.Append("<w:jc w:val=\"center\"/>");
        sb.Append("<w:rPr><w:rFonts w:ascii=\"宋体\" w:eastAsia=\"宋体\" w:hAnsi=\"宋体\"/>");
        sb.Append("<w:sz w:val=\"").Append(halfPoints).Append("\"/>");
        if (bold) sb.Append("<w:b/>");
        sb.Append("</w:rPr></w:pPr>");
        var lines = new List<string>(SplitLines(text));
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            sb.Append("<w:r><w:rPr><w:rFonts w:ascii=\"宋体\" w:eastAsia=\"宋体\" w:hAnsi=\"宋体\"/>");
            sb.Append("<w:sz w:val=\"").Append(halfPoints).Append("\"/>");
            if (bold) sb.Append("<w:b/>");
            sb.Append("</w:rPr><w:t xml:space=\"preserve\">").Append(Esc(line)).Append("</w:t></w:r>");
            // 只在段落内换行处加 <w:br/>；最后一行不加，否则每段末尾多一个空行
            if (i < lines.Count - 1) sb.Append("<w:r><w:br/></w:r>");
        }
        sb.Append("</w:p>");
        return sb.ToString();
    }

    private static IEnumerable<string> SplitLines(string t)
        => t.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    private static string Esc(string s) => s
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
        .Replace("\"", "&quot;");

    private const string ContentTypes =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
        "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
        "</Types>";

    private const string RootRels =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>" +
        "</Relationships>";

    private const string DocRels =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"/>";
}
