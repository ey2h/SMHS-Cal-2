using GlassManagement.Api.Models;
namespace GlassManagement.Api.Models {
    public static class PanelResultExtensions {
        public static PanelResult CreateSample(string panelId, string type) {
            return new PanelResult {
                PanelId = panelId,
                Type = type,
                Status = "Pass",
                Metrics = new Metrics { Thickness = 6.0, Deflection = 0.5, MaxStress = 12.3, EdgeEffect = 0.0, SafetyFactor = 1.5 }
            };
        }
    }
}
