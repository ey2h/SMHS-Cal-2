using System.Collections.Generic;

namespace GlassManagement.Api.Models {
    public class GlassInput {
        public string ProjectId { get; set; }
        public string RegionId { get; set; }
        public List<GlassPanelInput> Panels { get; set; } = new List<GlassPanelInput>();
        public GlobalSettings Settings { get; set; } = new GlobalSettings();
    }
    public class GlobalSettings {
        public string Wind { get; set; }
        public string LoadCase { get; set; }
    }
}
