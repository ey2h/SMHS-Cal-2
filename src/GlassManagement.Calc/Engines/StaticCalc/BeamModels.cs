namespace GlassManagement.Calc.Engines.StaticCalc;

public enum LoadType
{
    Uniform,
    Concentrated,
    Triangular,
    Trapezoidal,
    Moment,
    PartialUniform,
    DoubleMoment,
    SymmetricConcentrated,
    MultipleConcentrated
}

public enum SupportCondition
{
    SimpleBeam,
    Cantilever,
    DoubleSpan,
    ContinuousThreeSpan,
    MultiSpanHingedStatical,
    MultiSpanHingedIndeterminate,
    OverhangBeam
}

public class BeamCalcRequest
{
    public SupportCondition Support { get; set; }
    public LoadType Load { get; set; }
    public double L { get; set; }
    public double L2 { get; set; }
    public double L3 { get; set; }
    public double Lc { get; set; }
    public double Q { get; set; }
    public double P { get; set; }
    public double A { get; set; }
    public double M0 { get; set; }
    public double Ma { get; set; }
    public double Mb { get; set; }
    public double LoadStart { get; set; }
    public double LoadEnd { get; set; }
    public double Q1 { get; set; }
    public double Q2 { get; set; }
    public double E { get; set; } = 70000;
    public double I { get; set; } = 1e6;
    public int NumPoints { get; set; } = 100;
    public int ConcentratedCount { get; set; } = 2;
}

public class DiagramPoint
{
    public double X { get; set; }
    public double Value { get; set; }
}

public class SupportReaction
{
    public string Name { get; set; } = "";
    public double Value { get; set; }
}

public class BeamCalcResult
{
    public List<DiagramPoint> MomentDiagram { get; set; } = new();
    public List<DiagramPoint> ShearDiagram { get; set; } = new();
    public List<DiagramPoint> DeflectionDiagram { get; set; } = new();
    public List<SupportReaction> Reactions { get; set; } = new();

    public double MaxMoment { get; set; }
    public double MaxMomentPosition { get; set; }
    public double MinMoment { get; set; }
    public double MinMomentPosition { get; set; }
    public double MaxShear { get; set; }
    public double MaxDeflection { get; set; }
    public double MaxDeflectionPosition { get; set; }

    public string Description { get; set; } = "";
    public string Reference { get; set; } = "";

    public string MechanicalDiagramSvg { get; set; } = "";
    public string MomentDiagramSvg { get; set; } = "";
    public string ShearDiagramSvg { get; set; } = "";
    public string DeflectionDiagramSvg { get; set; } = "";
}

public class MechanicalModel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public SupportCondition Support { get; set; }
    public LoadType Load { get; set; }
    public string Description { get; set; } = "";
    public string Reference { get; set; } = "";
}
