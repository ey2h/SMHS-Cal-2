namespace GlassManagement.Core.Models;

public class Phase3GlassInput
{
    public string ProjectId { get; set; } = string.Empty;
    public string RegionId { get; set; } = string.Empty;
    public List<Phase3GlassPanelInput> Panels { get; set; } = new();
    public Phase3GlobalSettings? Settings { get; set; }
}

public class Phase3GlobalSettings
{
    public string? Wind { get; set; }
    public string? LoadCase { get; set; }
}

public class Phase3GlassPanelInput
{
    public string PanelId { get; set; } = string.Empty;
    public Phase3GlassPanelType Type { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Thickness { get; set; }
    public string? EdgeTreatment { get; set; }
    public List<Phase3GlassLayerInput> Layers { get; set; } = new();
}

public class Phase3GlassLayerInput
{
    public string MaterialId { get; set; } = string.Empty;
    public double Thickness { get; set; }
    public string? Interlayer { get; set; }
    public string? Laminate { get; set; }
}

public enum Phase3GlassPanelType
{
    Single,
    Laminated,
    Insulating,
    LaminatedInsulating
}

public class Phase3GlassOutput
{
    public List<Phase3PanelResult> PanelsResults { get; set; } = new();
    public Phase3Summary ResultSummary { get; set; } = new();
    public Phase3CloudVizData? CloudViz { get; set; }
}

public class Phase3PanelResult
{
    public string PanelId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Phase3Metrics Metrics { get; set; } = new();
    public string? Warnings { get; set; }
}

public class Phase3Metrics
{
    public double Thickness { get; set; }
    public double Deflection { get; set; }
    public double MaxStress { get; set; }
    public double EdgeEffect { get; set; }
    public double SafetyFactor { get; set; }
}

public class Phase3Summary
{
    public bool Passed { get; set; } = true;
    public long TotalTimeMs { get; set; }
}

public class Phase3CloudVizData
{
    public List<Phase3CloudVizPoint> Points { get; set; } = new();
}

public class Phase3CloudVizPoint
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Value { get; set; }
}
