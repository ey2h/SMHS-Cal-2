using System.Collections.Generic;
using System.Threading.Tasks;
using GlassManagement.Api.Models;

namespace GlassManagement.Api.Services {
    public interface IRegionService
    {
        Task<List<RegionInfo>> GetRegionsAsync();
    }
}
