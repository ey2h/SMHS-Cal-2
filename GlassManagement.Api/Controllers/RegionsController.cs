using Microsoft.AspNetCore.Mvc;
using GlassManagement.Api.Models;
using GlassManagement.Api.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GlassManagement.Api.Controllers {
    [ApiController]
    [Route("api/[controller]")]
    public class RegionsController : ControllerBase
    {
    private readonly IRegionService _regionService;
    public RegionsController()
    {
        // Fallback simple instantiation for MVP without DI wiring
        _regionService = new RegionService();
    }
        [HttpGet]
        public async Task<ActionResult<List<RegionInfo>>> GetRegions()
        {
            var regions = await _regionService.GetRegionsAsync();
            return Ok(regions);
        }
    }
}
