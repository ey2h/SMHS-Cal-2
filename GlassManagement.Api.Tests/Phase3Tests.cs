using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace GlassManagement.Api.Tests {
  public class Phase3Tests
  {
    private readonly HttpClient _client;

    public Phase3Tests()
    {
      // This is a placeholder test scaffold. In a real environment, you'd use a WebApplicationFactory<Startup> or similar.
      _client = new HttpClient();
    }

    [Fact]
    public async Task Phase3_Post_Returns_Success_When_Panels_Are_Provided()
    {
      // Arrange
      var input = new GlassManagement.Api.Models.GlassInput
      {
        ProjectId = "test-phase3",
        RegionId = "default",
        Panels = new System.Collections.Generic.List<GlassManagement.Api.Models.GlassPanelInput>
        {
          new GlassManagement.Api.Models.GlassPanelInput { PanelId = "p3-1", Type = GlassManagement.Api.Models.GlassPanelType.Single, Width = 100, Height = 100, Thickness = 6 }
        }
      };
      var json = JsonSerializer.Serialize(input);
      var content = new StringContent(json, Encoding.UTF8, "application/json");
      // Act
      var resp = await _client.PostAsync("http://localhost:5000/api/calculation/phase3", content);
      // Assert
      resp.EnsureSuccessStatusCode();
    }
  }
}
