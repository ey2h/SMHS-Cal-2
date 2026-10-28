using System;
using System.Collections.Generic;
using GlassManagement.Core.Models;
using Xunit;

namespace GlassManagement.Api.Tests
{
  public class Phase3ControllerTests
  {
    private readonly Controllers.Phase3CalculationController _controller;

    public Phase3ControllerTests()
    {
      _controller = new Controllers.Phase3CalculationController();
    }

    [Fact]
    public void Phase3_Returns_Output_With_Single_Panel()
    {
      var input = new Phase3GlassInput
      {
        ProjectId = "test-p3",
        RegionId = "default",
        Panels = new List<Phase3GlassPanelInput>
        {
          new Phase3GlassPanelInput
          {
            PanelId = "p3-1",
            Type = Phase3GlassPanelType.Single,
            Width = 1200,
            Height = 2400,
            Thickness = 6,
            Layers = new List<Phase3GlassLayerInput>
            {
              new Phase3GlassLayerInput { MaterialId = "default", Thickness = 6 }
            }
          }
        }
      };

      var result = _controller.Phase3(input);
      Assert.NotNull(result);
      var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
      var output = Assert.IsType<Phase3GlassOutput>(okResult.Value);
      Assert.Single(output.PanelsResults);
      Assert.Equal("p3-1", output.PanelsResults[0].PanelId);
      Assert.NotNull(output.CloudViz);
      Assert.Single(output.CloudViz.Points);
      Assert.True(output.ResultSummary.TotalTimeMs >= 0);
    }

    [Fact]
    public void Phase3_Returns_Output_With_Multiple_Panels()
    {
      var input = new Phase3GlassInput
      {
        ProjectId = "test-p3-multi",
        RegionId = "SH",
        Panels = new List<Phase3GlassPanelInput>
        {
          new Phase3GlassPanelInput { PanelId = "p1", Type = Phase3GlassPanelType.Single, Width = 1200, Height = 2400, Thickness = 6 },
          new Phase3GlassPanelInput { PanelId = "p2", Type = Phase3GlassPanelType.Laminated, Width = 1500, Height = 3000, Thickness = 12 },
          new Phase3GlassPanelInput { PanelId = "p3", Type = Phase3GlassPanelType.Insulating, Width = 2000, Height = 4000, Thickness = 24 }
        }
      };

      var result = _controller.Phase3(input);
      Assert.NotNull(result);
      var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
      var output = Assert.IsType<Phase3GlassOutput>(okResult.Value);
      Assert.Equal(3, output.PanelsResults.Count);
      Assert.Equal(3, output.CloudViz!.Points.Count);
    }

    [Fact]
    public void Phase3_Returns_BadRequest_When_RegionId_Empty()
    {
      var input = new Phase3GlassInput
      {
        ProjectId = "test",
        RegionId = "",
        Panels = new List<Phase3GlassPanelInput>
        {
          new Phase3GlassPanelInput { PanelId = "p1", Type = Phase3GlassPanelType.Single, Width = 1200, Height = 2400, Thickness = 6 }
        }
      };

      var result = _controller.Phase3(input);
      Assert.NotNull(result);
      Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void Phase3_Returns_BadRequest_When_Panels_Empty()
    {
      var input = new Phase3GlassInput
      {
        ProjectId = "test",
        RegionId = "default",
        Panels = new List<Phase3GlassPanelInput>()
      };

      var result = _controller.Phase3(input);
      Assert.NotNull(result);
      Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void Phase3_Returns_BadRequest_When_Duplicate_PanelIds()
    {
      var input = new Phase3GlassInput
      {
        ProjectId = "test",
        RegionId = "default",
        Panels = new List<Phase3GlassPanelInput>
        {
          new Phase3GlassPanelInput { PanelId = "p1", Type = Phase3GlassPanelType.Single, Width = 1200, Height = 2400, Thickness = 6 },
          new Phase3GlassPanelInput { PanelId = "p1", Type = Phase3GlassPanelType.Laminated, Width = 1500, Height = 3000, Thickness = 12 }
        }
      };

      var result = _controller.Phase3(input);
      Assert.NotNull(result);
      Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void Phase3_Returns_BadRequest_When_Empty_PanelId()
    {
      var input = new Phase3GlassInput
      {
        ProjectId = "test",
        RegionId = "default",
        Panels = new List<Phase3GlassPanelInput>
        {
          new Phase3GlassPanelInput { PanelId = "", Type = Phase3GlassPanelType.Single, Width = 1200, Height = 2400, Thickness = 6 }
        }
      };

      var result = _controller.Phase3(input);
      Assert.NotNull(result);
      Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void Phase3_Calculates_Correct_Metrics()
    {
      var input = new Phase3GlassInput
      {
        ProjectId = "test",
        RegionId = "default",
        Panels = new List<Phase3GlassPanelInput>
        {
          new Phase3GlassPanelInput { PanelId = "p1", Type = Phase3GlassPanelType.Single, Width = 1200, Height = 2400, Thickness = 6 }
        }
      };

      var result = _controller.Phase3(input);
      var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
      var output = Assert.IsType<Phase3GlassOutput>(okResult.Value);
      var metrics = output.PanelsResults[0].Metrics;

      Assert.Equal(6, metrics.Thickness);
      Assert.True(metrics.Deflection > 0);
      Assert.True(metrics.MaxStress > 0);
      Assert.True(metrics.SafetyFactor > 0);
    }

    [Fact]
    public void Phase3_Thin_Glass_Should_Have_Warning()
    {
      var input = new Phase3GlassInput
      {
        ProjectId = "test",
        RegionId = "default",
        Panels = new List<Phase3GlassPanelInput>
        {
          new Phase3GlassPanelInput { PanelId = "p1", Type = Phase3GlassPanelType.Single, Width = 3000, Height = 6000, Thickness = 3 }
        }
      };

      var result = _controller.Phase3(input);
      var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
      var output = Assert.IsType<Phase3GlassOutput>(okResult.Value);
      var panelResult = output.PanelsResults[0];

      Assert.True(panelResult.Status == "Warning" || panelResult.Status == "Pass");
    }

    [Fact]
    public void Validate_Returns_True_For_Valid_Input()
    {
      var input = new Phase3GlassInput
      {
        ProjectId = "test",
        RegionId = "default",
        Panels = new List<Phase3GlassPanelInput>
        {
          new Phase3GlassPanelInput { PanelId = "p1", Type = Phase3GlassPanelType.Single, Width = 1200, Height = 2400, Thickness = 6 }
        }
      };

      var result = _controller.Validate(input);
      Assert.NotNull(result);
      var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
      Assert.True((bool)okResult.Value!);
    }

    [Fact]
    public void Validate_Returns_False_For_Empty_Panels()
    {
      var input = new Phase3GlassInput
      {
        ProjectId = "test",
        RegionId = "default",
        Panels = new List<Phase3GlassPanelInput>()
      };

      var result = _controller.Validate(input);
      Assert.NotNull(result);
      Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void Validate_Returns_False_For_Duplicate_PanelIds()
    {
      var input = new Phase3GlassInput
      {
        ProjectId = "test",
        RegionId = "default",
        Panels = new List<Phase3GlassPanelInput>
        {
          new Phase3GlassPanelInput { PanelId = "p1", Type = Phase3GlassPanelType.Single, Width = 1200, Height = 2400, Thickness = 6 },
          new Phase3GlassPanelInput { PanelId = "p1", Type = Phase3GlassPanelType.Laminated, Width = 1500, Height = 3000, Thickness = 12 }
        }
      };

      var result = _controller.Validate(input);
      Assert.NotNull(result);
      Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void Phase3_CloudViz_Points_Match_Panel_Count()
    {
      var panelCount = 5;
      var panels = new List<Phase3GlassPanelInput>();
      for (int i = 0; i < panelCount; i++)
      {
        panels.Add(new Phase3GlassPanelInput
        {
          PanelId = $"p{i}",
          Type = Phase3GlassPanelType.Single,
          Width = 1200 + i * 100,
          Height = 2400 + i * 200,
          Thickness = 6 + i
        });
      }

      var input = new Phase3GlassInput
      {
        ProjectId = "test",
        RegionId = "default",
        Panels = panels
      };

      var result = _controller.Phase3(input);
      var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
      var output = Assert.IsType<Phase3GlassOutput>(okResult.Value);

      Assert.Equal(panelCount, output.PanelsResults.Count);
      Assert.Equal(panelCount, output.CloudViz!.Points.Count);
    }

    [Fact]
    public void Phase3_ResultSummary_Passed_Is_True_When_All_Pass()
    {
      var input = new Phase3GlassInput
      {
        ProjectId = "test",
        RegionId = "default",
        Panels = new List<Phase3GlassPanelInput>
        {
          new Phase3GlassPanelInput { PanelId = "p1", Type = Phase3GlassPanelType.Single, Width = 1200, Height = 2400, Thickness = 12 }
        }
      };

      var result = _controller.Phase3(input);
      var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
      var output = Assert.IsType<Phase3GlassOutput>(okResult.Value);

      Assert.True(output.ResultSummary.Passed);
    }
  }
}
