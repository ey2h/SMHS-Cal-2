using System;
using System.Linq;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using GlassCalcResult = GlassManagement.Api.Services.GlassCalcResult;
using MemberCalcResult = GlassManagement.Api.Services.MemberCalcResult;
using GlassManagement.Api.Services;

namespace CalcBookTestHarness
{
class Program
{
    static void Main(string[] args)
    {
        var input1 = new GlassCalcBookInput
        {
            ProjectName = "XX大厦全隐框玻璃幕墙工程",
            CompanyName = "XX幕墙工程有限公司",
            Designer = "张三",
            Reviewer = "李四",
            Approver = "王五",
            Supervisor = "赵六",
            ReportDate = DateTime.Now.ToString("yyyy-MM-dd"),
            City = "北京市",
            BuildingHeight = 85.0,
            InstallHeight = 80.0,
            PanelWidth = 1200,
            PanelHeight = 2400,
            BasicWindPressure = 55000,
            TerrainCategory = "B",
            SeismicIntensity = 8,
            GlassDescription = "6+1.52SGP+6双银LOW-E中空夹层钢化玻璃",
            GustFactor = 1.0,
            ShapeFactor = 1.2,
            HeightFactor = 1.0,
            WindPressureDesign = 15000,
            AlphaMax = 0.16,
            SiteFactor = 1.0,
            SiteCategory = "II",
            SgpShearModulus = 11.3,
            InterlayerThickness = 1.52,
            GlassThickness1 = 6,
            GlassThickness2 = 6,
            GlassDesignStrength = 84,
            Mus1Base = -1.0,
            Mus1At25 = -0.8,
            ColumnLeftWidth = 600,
            ColumnRightWidth = 600,
            ColumnMaterial = "6063-T5铝合金",
            ColumnInstallType = "偏心受拉",
            SpanL1 = 3600,
            SpanL2 = 4200,
            BoltDiameter = 5,
            BoltShearCapacity = 3920,
            BoltReductionFactor = 0.8,
            MinWallThickness = 2.5,
            BearingStrength = 230,
            AnchorDiameter = 10,
            AnchorFy = 300,
            ConcreteFt = 1.43,
            ConcreteFc = 14.3,
            AnchorSpacing = 60,
            WeldSize = 6,
            WeldStrength = 160,
            WeldBetaF = 0.7,
            TransferProfile = "10#槽钢",
            TransferArea = 1280,
            TransferIx = 198.3,
            TransferWx = 39.7,
            TransferLength = 120,
            TransferStrength = 215,
            GlassCalc = new GlassCalcResult
            {
                ShortSide = 1200,
                LongSide = 2400,
                EquivalentThickness = 8.5,
                LoadDesign = 15000,
                StressCoeff = 0.0838,
                DesignStrength = 84,
                MaxStress = 42.5,
                DeflectionCoeff = 0.00204,
                ElasticModulus = 72000,
                MaxDeflection = 15.8,
                DeflectionLimit = 20.0
            },
            Column = new MemberCalcResult
            {
                ProfileCode = "120系列铝合金立柱",
                SupportType = "双跨梁",
                Span = 4200,
                Ix = 180.5,
                Iy = 95.2,
                Wx = 35.2,
                Wnx2 = 30.8,
                Wy = 18.5,
                Area = 1850,
                LoadWidth = 1200,
                MaxStress = 68.5,
                AllowableStress = 85,
                ShearStrength = 49.4,
                MaxDeflection = 8.2,
                DeflectionLimit = 16.8,
                LinearDensity = 3.2,
                WebThickness = 2.5,
                Sx = 22.5,
                PlasticFactor = 1.0,
                ElasticModulus = 70000,
                DesignStrength = 85
            },
            Beam = new MemberCalcResult
            {
                ProfileCode = "80系列铝合金横梁",
                SupportType = "简支梁",
                Span = 1200,
                Ix = 45.2,
                Iy = 120.8,
                Wx = 12.5,
                Wnx2 = 10.2,
                Wy = 18.2,
                Area = 950,
                LoadWidth = 1200,
                MaxStress = 45.2,
                AllowableStress = 85,
                ShearStrength = 49.4,
                MaxDeflection = 2.8,
                DeflectionLimit = 6.7,
                LinearDensity = 1.8,
                WebThickness = 2.0,
                Sx = 8.5,
                PlasticFactor = 1.0,
                ElasticModulus = 70000,
                DesignStrength = 85
            }
        };

        var docBytes = new CalcBookWordService().GenerateGlassCurtainWallReport(input1);
        var outPath = @"D:\Code\Glass\calcbook_test_output_s1.docx";
        System.IO.File.WriteAllBytes(outPath, docBytes);
        Console.WriteLine($"Document generated: {outPath} (bytes={docBytes.Length})");
        
        int imageCount = CountImages(docBytes);
        Console.WriteLine($"[Validation] Image count: {imageCount}");
        
        bool hasToc = ContainsToc(docBytes);
        Console.WriteLine($"[Validation] TOC present: {hasToc}");
        
        bool hasFld = ContainsFieldCode(docBytes);
        Console.WriteLine($"[Validation] Field code present: {hasFld}");
    }

    private static int CountImages(byte[] docBytes)
    {
        using var ms = new MemoryStream(docBytes);
        using var zip = new System.IO.Compression.ZipArchive(ms, ZipArchiveMode.Read, true);
        int count = 0;
        foreach (var e in zip.Entries)
        {
            if (e.FullName.StartsWith("word/media/", StringComparison.OrdinalIgnoreCase)) count++;
        }
        return count;
    }

    private static bool ContainsToc(byte[] docBytes)
    {
        using var ms = new MemoryStream(docBytes);
        using var zip = new System.IO.Compression.ZipArchive(ms, ZipArchiveMode.Read, true);
        var entry = zip.GetEntry("word/document.xml");
        if (entry == null) return false;
        using var es = entry.Open();
        using var sr = new System.IO.StreamReader(es);
        var xml = sr.ReadToEnd();
        return xml.Contains("TOC");
    }

    private static bool ContainsFieldCode(byte[] docBytes)
    {
        using var ms = new MemoryStream(docBytes);
        using var zip = new System.IO.Compression.ZipArchive(ms, ZipArchiveMode.Read, true);
        var entry = zip.GetEntry("word/document.xml");
        if (entry == null) return false;
        using var es = entry.Open();
        using var sr = new System.IO.StreamReader(es);
        var xml = sr.ReadToEnd();
        return xml.Contains("fldSimple") || xml.Contains("instrText") || xml.Contains("fldChar");
    }
}
}
