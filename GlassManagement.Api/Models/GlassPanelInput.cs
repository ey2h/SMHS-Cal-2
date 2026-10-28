using System.Collections.Generic;

namespace GlassManagement.Api.Models {
    public class GlassPanelInput {
        public string PanelId { get; set; }
        public GlassPanelType Type { get; set; }
        public List<GlassLayerInput> Layers { get; set; } = new List<GlassLayerInput>();
        public double Width { get; set; }
        public double Height { get; set; }
        public double Thickness { get; set; }
        public string EdgeTreatment { get; set; }
    }
    public enum GlassPanelType {
        Single,
        Laminated,
        Insulating,
        LaminatedInsulating
    }
}
