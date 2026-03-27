using System;
using System.IO;
using NPOI.XWPF.UserModel;

namespace GlassManagement.Api.Services
{
    public static class WordImageInserter
    {
        public static byte[] InsertImage(byte[] docBytes, byte[] imageBytes, string caption)
        {
            using var msDoc = new MemoryStream(docBytes);
            using var doc = new XWPFDocument(msDoc);
            InsertImageIntoDoc(doc, imageBytes, caption);
            using var outMs = new MemoryStream();
            doc.Write(outMs);
            return outMs.ToArray();
        }

        public static void InsertImage(XWPFDocument doc, byte[] imageBytes, string caption)
        {
            InsertImageIntoDoc(doc, imageBytes, caption);
        }

        private static void InsertImageIntoDoc(XWPFDocument doc, byte[] imageBytes, string caption)
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                var para = doc.CreateParagraph();
                var run = para.CreateRun();
                run.SetText($"[图片：{caption}]");
                para.Alignment = ParagraphAlignment.CENTER;
                return;
            }

            try
            {
                using var ms = new MemoryStream(imageBytes);
                
                var para = doc.CreateParagraph();
                para.Alignment = ParagraphAlignment.CENTER;
                
                var run = para.CreateRun();
                
                run.AddPicture(ms, (int)PictureType.PNG, "diagram.png", 
                    (int)(14.0 * 360000), 
                    (int)(8.0 * 360000));

                var capPara = doc.CreateParagraph();
                capPara.Alignment = ParagraphAlignment.CENTER;
                var capRun = capPara.CreateRun();
                capRun.SetText(caption);
            }
            catch (Exception)
            {
                var para = doc.CreateParagraph();
                var run = para.CreateRun();
                run.SetText($"[图片：{caption}]");
                para.Alignment = ParagraphAlignment.CENTER;
                
                var capPara = doc.CreateParagraph();
                capPara.Alignment = ParagraphAlignment.CENTER;
                var capRun = capPara.CreateRun();
                capRun.SetText($"{caption}");
            }
        }
    }
}
