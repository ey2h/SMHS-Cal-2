using System.Collections.Generic;

namespace GlassManagement.Api.Models {
    public class GlassOutput {
        public List<PanelResult> PanelsResults { get; set; } = new List<PanelResult>();
        public Summary ResultSummary { get; set; } = new Summary();
        public CloudVizData CloudViz { get; set; }
    }
    public class PanelResult {
        public string PanelId { get; set; }
        public string Type { get; set; }
        public string Status { get; set; }
        public Metrics Metrics { get; set; } = new Metrics();
        public string Warnings { get; set; }
    }
    public class Metrics {
        public double Thickness { get; set; }
        public double Deflection { get; set; }
        public double MaxStress { get; set; }
        public double EdgeEffect { get; set; }
        public double SafetyFactor { get; set; }
    }
    public class Summary {
        public bool Passed { get; set; } = true;
        public long TotalTimeMs { get; set; }
    }
    public class CloudVizData {
        // placeholder for cloud viz data structure
        public List<object> Points { get; set; } = new List<object>();
    }
}
