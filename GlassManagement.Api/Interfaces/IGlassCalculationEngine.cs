using System.Threading.Tasks;
using GlassManagement.Api.Models;

namespace GlassManagement.Api.Interfaces {
    public interface IGlassCalculationEngine {
        Task<GlassOutput> CalculateGlassAsync(GlassInput input);
    }
}
