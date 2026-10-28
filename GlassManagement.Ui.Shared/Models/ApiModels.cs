namespace GlassManagement.Ui.Shared.Models;

public class GlassInput
{
    public string ProjectId { get; set; } = string.Empty;
    public string RegionId { get; set; } = string.Empty;
    public List<GlassPanelInput> Panels { get; set; } = new();
    public GlobalSettings? Settings { get; set; }
}

public class GlobalSettings
{
    public string? Wind { get; set; }
    public string? LoadCase { get; set; }
}

public class GlassPanelInput
{
    public string PanelId { get; set; } = string.Empty;
    public GlassPanelType Type { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Thickness { get; set; }
    public string? EdgeTreatment { get; set; }
    public List<GlassLayerInput> Layers { get; set; } = new();
}

public enum GlassPanelType
{
    Single,
    Laminated,
    Insulating,
    LaminatedInsulating
}

public class GlassLayerInput
{
    public string MaterialId { get; set; } = string.Empty;
    public double Thickness { get; set; }
    public string? Interlayer { get; set; }
    public string? Laminate { get; set; }
}

public class GlassOutput
{
    public List<PanelResult> PanelsResults { get; set; } = new();
    public Summary ResultSummary { get; set; } = new();
    public CloudVizDataOutput? CloudViz { get; set; }
}

public class PanelResult
{
    public string PanelId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Metrics Metrics { get; set; } = new();
    public string? Warnings { get; set; }
}

public class Metrics
{
    public double Thickness { get; set; }
    public double Deflection { get; set; }
    public double MaxStress { get; set; }
    public double EdgeEffect { get; set; }
    public double SafetyFactor { get; set; }
}

public class Summary
{
    public bool Passed { get; set; } = true;
    public long TotalTimeMs { get; set; }
}

public class CloudVizDataOutput
{
    public List<object> Points { get; set; } = new();
}

public class RegionInfo
{
    public string RegionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
