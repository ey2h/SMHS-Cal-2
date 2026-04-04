using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using GlassManagement.Api.Controllers;
using GlassManagement.Api.Models;

namespace GlassManagement.Api.Tests
{
    public class Phase2Tests
    {
        [Fact]
        public void Phase2_Returns_GlassOutput_With_PanelResult()
        {
            // Arrange
            var controller = new CalculationController();
            var input = new GlassInput
            {
                ProjectId = "P1",
                RegionId = "R1",
                Panels = new List<GlassPanelInput>
                {
                    new GlassPanelInput
                    {
                        PanelId = "p2",
                        Type = GlassPanelType.Single,
                        Width = 1200,
                        Height = 2400,
                        Thickness = 6.0,
                        Layers = new List<GlassLayerInput>
                        {
                            new GlassLayerInput { MaterialId = "mat", Thickness = 6.0 }
                        }
                    }
                    ,
                    new GlassPanelInput
                    {
                        PanelId = "p3",
                        Type = GlassPanelType.Laminated,
                        Width = 900,
                        Height = 1600,
                        Thickness = 8.0,
                        Layers = new List<GlassLayerInput>
                        {
                            new GlassLayerInput { MaterialId = "mat2", Thickness = 8.0 },
                            new GlassLayerInput { MaterialId = "interlayer", Thickness = 0.76 }
                        }
                    }
                }
            };

            // Act
            var result = controller.Phase2(input) as OkObjectResult;
            Assert.NotNull(result);
            var output = result.Value as GlassOutput;
            Assert.NotNull(output);
            Assert.NotEmpty(output.PanelsResults);
        }

        [Fact]
        public void Phase2_Returns_GlassOutput_With_Fallback_When_No_Panels()
        {
            // Arrange
            var controller = new CalculationController();
            var input = new GlassInput { ProjectId = "P1-NoPanels", RegionId = "R1", Panels = new List<GlassPanelInput>() };

            // Act
            var result = controller.Phase2(input) as OkObjectResult;
            // Assert
            Assert.NotNull(result);
            var output = result.Value as GlassOutput;
            Assert.NotNull(output);
            Assert.NotEmpty(output.PanelsResults);
        }

        [Fact]
        public void Phase2_Returns_GlassOutput_With_Multiple_Panels()
        {
            // Arrange
            var controller = new CalculationController();
            var input = new GlassInput
            {
                ProjectId = "P1-Multi",
                RegionId = "R1",
                Panels = new List<GlassPanelInput>
                {
                    new GlassPanelInput
                    {
                        PanelId = "p2-1",
                        Type = GlassPanelType.Single,
                        Width = 1200,
                        Height = 2400,
                        Thickness = 6.0,
                        Layers = new List<GlassLayerInput>
                        {
                            new GlassLayerInput { MaterialId = "mat", Thickness = 6.0 }
                        }
                    },
                    new GlassPanelInput
                    {
                        PanelId = "p2-2",
                        Type = GlassPanelType.Laminated,
                        Width = 900,
                        Height = 1600,
                        Thickness = 8.0,
                        Layers = new List<GlassLayerInput>
                        {
                            new GlassLayerInput { MaterialId = "mat2", Thickness = 8.0 },
                            new GlassLayerInput { MaterialId = "interlayer", Thickness = 0.76 }
                        }
                    }
                }
            };

            // Act
            var result = controller.Phase2(input) as OkObjectResult;
            // Assert
            Assert.NotNull(result);
            var output = result.Value as GlassOutput;
            Assert.NotNull(output);
            Assert.True(output.PanelsResults.Count >= 2);
        }
        [Fact]
        public void Phase2_Returns_GlassOutput_With_Various_Types_2()
        {
            // Arrange
            var controller = new CalculationController();
            var input = new GlassInput
            {
                ProjectId = "P1-Mixed2",
                RegionId = "R1",
                Panels = new List<GlassPanelInput>
                {
                    new GlassPanelInput
                    {
                        PanelId = "p2-mx1",
                        Type = GlassPanelType.Single,
                        Width = 1200,
                        Height = 2400,
                        Thickness = 6.0,
                        Layers = new List<GlassLayerInput>
                        {
                            new GlassLayerInput { MaterialId = "matA", Thickness = 6.0 }
                        }
                    },
                    new GlassPanelInput
                    {
                        PanelId = "p2-mx2",
                        Type = GlassPanelType.Laminated,
                        Width = 900,
                        Height = 900,
                        Thickness = 8.0,
                        Layers = new List<GlassLayerInput>
                        {
                            new GlassLayerInput { MaterialId = "matB", Thickness = 8.0 },
                            new GlassLayerInput { MaterialId = "inter", Thickness = 0.76 }
                        }
                    }
                }
            };

            // Act
            var result = controller.Phase2(input) as OkObjectResult;
            // Assert
            Assert.NotNull(result);
            var output = result.Value as GlassOutput;
            Assert.NotNull(output);
            Assert.True(output.PanelsResults.Count >= 2);
        }
    }
}
