using GlassManagement.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace GlassManagement.Api.Tests
{
    public class Phase2Tests
    {
        [Fact]
        public void Phase3_Returns_Output_With_Single_Panel()
        {
            var controller = new Controllers.Phase3CalculationController();
            var input = new Phase3GlassInput
            {
                ProjectId = "P1",
                RegionId = "R1",
                Panels = new List<Phase3GlassPanelInput>
                {
                    new Phase3GlassPanelInput
                    {
                        PanelId = "p2",
                        Type = Phase3GlassPanelType.Single,
                        Width = 1200,
                        Height = 2400,
                        Thickness = 6.0,
                        Layers = new List<Phase3GlassLayerInput>
                        {
                            new Phase3GlassLayerInput { MaterialId = "mat", Thickness = 6.0 }
                        }
                    }
                }
            };

            var result = controller.Phase3(input);
            Assert.NotNull(result);
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var output = Assert.IsType<Phase3GlassOutput>(okResult.Value);
            Assert.NotEmpty(output.PanelsResults);
        }

        [Fact]
        public void Phase3_Returns_Output_With_Fallback_When_No_Panels()
        {
            var controller = new Controllers.Phase3CalculationController();
            var input = new Phase3GlassInput { ProjectId = "P1-NoPanels", RegionId = "R1", Panels = new List<Phase3GlassPanelInput>() };

            var result = controller.Phase3(input);
            Assert.NotNull(result);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public void Phase3_Returns_Output_With_Multiple_Panels()
        {
            var controller = new Controllers.Phase3CalculationController();
            var input = new Phase3GlassInput
            {
                ProjectId = "P1-Multi",
                RegionId = "R1",
                Panels = new List<Phase3GlassPanelInput>
                {
                    new Phase3GlassPanelInput
                    {
                        PanelId = "p2-1",
                        Type = Phase3GlassPanelType.Single,
                        Width = 1200,
                        Height = 2400,
                        Thickness = 6.0,
                        Layers = new List<Phase3GlassLayerInput>
                        {
                            new Phase3GlassLayerInput { MaterialId = "mat", Thickness = 6.0 }
                        }
                    },
                    new Phase3GlassPanelInput
                    {
                        PanelId = "p2-2",
                        Type = Phase3GlassPanelType.Laminated,
                        Width = 900,
                        Height = 1600,
                        Thickness = 8.0,
                        Layers = new List<Phase3GlassLayerInput>
                        {
                            new Phase3GlassLayerInput { MaterialId = "mat2", Thickness = 8.0 },
                            new Phase3GlassLayerInput { MaterialId = "interlayer", Thickness = 0.76 }
                        }
                    }
                }
            };

            var result = controller.Phase3(input);
            Assert.NotNull(result);
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var output = Assert.IsType<Phase3GlassOutput>(okResult.Value);
            Assert.True(output.PanelsResults.Count >= 2);
        }

        [Fact]
        public void Phase3_Returns_BadRequest_When_Duplicate_PanelIds()
        {
            var controller = new Controllers.Phase3CalculationController();
            var input = new Phase3GlassInput
            {
                ProjectId = "P1-Dup",
                RegionId = "R1",
                Panels = new List<Phase3GlassPanelInput>
                {
                    new Phase3GlassPanelInput { PanelId = "dup", Type = Phase3GlassPanelType.Single, Width = 1200, Height = 2400, Thickness = 6 },
                    new Phase3GlassPanelInput { PanelId = "dup", Type = Phase3GlassPanelType.Laminated, Width = 900, Height = 1600, Thickness = 8 }
                }
            };

            var result = controller.Phase3(input);
            Assert.NotNull(result);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
    }
}
