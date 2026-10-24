using System.Collections.Generic;
using GlassManagement.Api.Models;
using Xunit;

namespace GlassManagement.Api.Tests
{
  public class Phase3Tests
  {
    private readonly List<Phase3Validator.PanelUI3> _validPanels;
    private readonly string _validRegion = "default";

    public Phase3Tests()
    {
      _validPanels = new List<Phase3Validator.PanelUI3>
      {
        new Phase3Validator.PanelUI3 { PanelId = "p3-1", Type = "Single", Width = 1200, Height = 2400, Thickness = 6 }
      };
    }

    [Fact]
    public void Phase3InputIsValid_ReturnsTrue_WhenAllInputsAreValid()
    {
      var result = Phase3Validator.Phase3InputIsValid(_validRegion, _validPanels);
      Assert.True(result);
    }

    [Fact]
    public void Phase3InputIsValid_ReturnsFalse_WhenRegionIsNull()
    {
      var result = Phase3Validator.Phase3InputIsValid(null, _validPanels);
      Assert.False(result);
    }

    [Fact]
    public void Phase3InputIsValid_ReturnsFalse_WhenRegionIsEmpty()
    {
      var result = Phase3Validator.Phase3InputIsValid("", _validPanels);
      Assert.False(result);
    }

    [Fact]
    public void Phase3InputIsValid_ReturnsFalse_WhenPanelsIsNull()
    {
      var result = Phase3Validator.Phase3InputIsValid(_validRegion, null);
      Assert.False(result);
    }

    [Fact]
    public void Phase3InputIsValid_ReturnsFalse_WhenPanelsIsEmpty()
    {
      var result = Phase3Validator.Phase3InputIsValid(_validRegion, new List<Phase3Validator.PanelUI3>());
      Assert.False(result);
    }

    [Fact]
    public void Phase3InputIsValid_ReturnsFalse_WhenPanelIdIsEmpty()
    {
      var panels = new List<Phase3Validator.PanelUI3>
      {
        new Phase3Validator.PanelUI3 { PanelId = "", Type = "Single", Width = 1200, Height = 2400, Thickness = 6 }
      };
      var result = Phase3Validator.Phase3InputIsValid(_validRegion, panels);
      Assert.False(result);
    }

    [Theory]
    [InlineData("InvalidType")]
    [InlineData("")]
    [InlineData("NotAPanelType")]
    public void Phase3InputIsValid_ReturnsFalse_WhenPanelTypeIsInvalid(string invalidType)
    {
      var panels = new List<Phase3Validator.PanelUI3>
      {
        new Phase3Validator.PanelUI3 { PanelId = "p3-1", Type = invalidType, Width = 1200, Height = 2400, Thickness = 6 }
      };
      var result = Phase3Validator.Phase3InputIsValid(_validRegion, panels);
      Assert.False(result);
    }

    [Theory]
    [InlineData("Single")]
    [InlineData("Laminated")]
    [InlineData("Insulating")]
    [InlineData("LaminatedInsulating")]
    public void Phase3InputIsValid_ReturnsTrue_ForAllValidPanelTypes(string validType)
    {
      var panels = new List<Phase3Validator.PanelUI3>
      {
        new Phase3Validator.PanelUI3 { PanelId = "p3-1", Type = validType, Width = 1200, Height = 2400, Thickness = 6 }
      };
      var result = Phase3Validator.Phase3InputIsValid(_validRegion, panels);
      Assert.True(result);
    }

    [Fact]
    public void Phase3InputIsValid_ReturnsFalse_WhenWidthIsZero()
    {
      var panels = new List<Phase3Validator.PanelUI3>
      {
        new Phase3Validator.PanelUI3 { PanelId = "p3-1", Type = "Single", Width = 0, Height = 2400, Thickness = 6 }
      };
      var result = Phase3Validator.Phase3InputIsValid(_validRegion, panels);
      Assert.False(result);
    }

    [Fact]
    public void Phase3InputIsValid_ReturnsFalse_WhenHeightIsNegative()
    {
      var panels = new List<Phase3Validator.PanelUI3>
      {
        new Phase3Validator.PanelUI3 { PanelId = "p3-1", Type = "Single", Width = 1200, Height = -100, Thickness = 6 }
      };
      var result = Phase3Validator.Phase3InputIsValid(_validRegion, panels);
      Assert.False(result);
    }

    [Fact]
    public void Phase3InputIsValid_ReturnsFalse_WhenThicknessIsZero()
    {
      var panels = new List<Phase3Validator.PanelUI3>
      {
        new Phase3Validator.PanelUI3 { PanelId = "p3-1", Type = "Single", Width = 1200, Height = 2400, Thickness = 0 }
      };
      var result = Phase3Validator.Phase3InputIsValid(_validRegion, panels);
      Assert.False(result);
    }

    [Fact]
    public void ArePanelIdsUnique_ReturnsTrue_WhenAllIdsAreUnique()
    {
      var panels = new List<Phase3Validator.PanelUI3>
      {
        new Phase3Validator.PanelUI3 { PanelId = "p3-1", Type = "Single", Width = 1200, Height = 2400, Thickness = 6 },
        new Phase3Validator.PanelUI3 { PanelId = "p3-2", Type = "Laminated", Width = 1500, Height = 3000, Thickness = 12 }
      };
      var result = Phase3Validator.ArePanelIdsUnique(panels);
      Assert.True(result);
    }

    [Fact]
    public void ArePanelIdsUnique_ReturnsFalse_WhenDuplicateIdsExist()
    {
      var panels = new List<Phase3Validator.PanelUI3>
      {
        new Phase3Validator.PanelUI3 { PanelId = "p3-1", Type = "Single", Width = 1200, Height = 2400, Thickness = 6 },
        new Phase3Validator.PanelUI3 { PanelId = "p3-1", Type = "Laminated", Width = 1500, Height = 3000, Thickness = 12 }
      };
      var result = Phase3Validator.ArePanelIdsUnique(panels);
      Assert.False(result);
    }

    [Fact]
    public void ArePanelIdsUnique_ReturnsFalse_WhenDuplicateIdsDifferOnlyByCase()
    {
      var panels = new List<Phase3Validator.PanelUI3>
      {
        new Phase3Validator.PanelUI3 { PanelId = "p3-1", Type = "Single", Width = 1200, Height = 2400, Thickness = 6 },
        new Phase3Validator.PanelUI3 { PanelId = "P3-1", Type = "Laminated", Width = 1500, Height = 3000, Thickness = 12 }
      };
      var result = Phase3Validator.ArePanelIdsUnique(panels);
      Assert.False(result);
    }

    [Fact]
    public void ArePanelIdsUnique_ReturnsFalse_WhenAnyPanelIdIsEmpty()
    {
      var panels = new List<Phase3Validator.PanelUI3>
      {
        new Phase3Validator.PanelUI3 { PanelId = "p3-1", Type = "Single", Width = 1200, Height = 2400, Thickness = 6 },
        new Phase3Validator.PanelUI3 { PanelId = "", Type = "Laminated", Width = 1500, Height = 3000, Thickness = 12 }
      };
      var result = Phase3Validator.ArePanelIdsUnique(panels);
      Assert.False(result);
    }

    [Fact]
    public void ArePanelIdsUnique_ReturnsTrue_WhenSinglePanelHasValidId()
    {
      var panels = new List<Phase3Validator.PanelUI3>
      {
        new Phase3Validator.PanelUI3 { PanelId = "p3-1", Type = "Single", Width = 1200, Height = 2400, Thickness = 6 }
      };
      var result = Phase3Validator.ArePanelIdsUnique(panels);
      Assert.True(result);
    }

    [Fact]
    public void Phase3IsSubmittable_ReturnsTrue_WhenAllConditionsMet()
    {
      var result = Phase3Validator.Phase3IsSubmittable(_validRegion, _validPanels);
      Assert.True(result);
    }

    [Fact]
    public void Phase3IsSubmittable_ReturnsFalse_WhenRegionMissing()
    {
      var result = Phase3Validator.Phase3IsSubmittable(null, _validPanels);
      Assert.False(result);
    }

    [Fact]
    public void Phase3IsSubmittable_ReturnsFalse_WhenDuplicatePanelIds()
    {
      var panels = new List<Phase3Validator.PanelUI3>
      {
        new Phase3Validator.PanelUI3 { PanelId = "p3-1", Type = "Single", Width = 1200, Height = 2400, Thickness = 6 },
        new Phase3Validator.PanelUI3 { PanelId = "p3-1", Type = "Laminated", Width = 1500, Height = 3000, Thickness = 12 }
      };
      var result = Phase3Validator.Phase3IsSubmittable(_validRegion, panels);
      Assert.False(result);
    }

    [Fact]
    public void GetValidationError_ReturnsEmpty_WhenInputIsValid()
    {
      var result = Phase3Validator.GetValidationError(_validRegion, _validPanels);
      Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetValidationError_ReturnsRegionMissing_WhenRegionIsNull()
    {
      var result = Phase3Validator.GetValidationError(null, _validPanels);
      Assert.Equal("区域未选择", result);
    }

    [Fact]
    public void GetValidationError_ReturnsPanelsEmpty_WhenPanelsListIsEmpty()
    {
      var result = Phase3Validator.GetValidationError(_validRegion, new List<Phase3Validator.PanelUI3>());
      Assert.Equal("Panel 为空", result);
    }

    [Fact]
    public void GetValidationError_ReturnsPanelIdEmpty_WhenPanelIdIsBlank()
    {
      var panels = new List<Phase3Validator.PanelUI3>
      {
        new Phase3Validator.PanelUI3 { PanelId = "   ", Type = "Single", Width = 1200, Height = 2400, Thickness = 6 }
      };
      var result = Phase3Validator.GetValidationError(_validRegion, panels);
      Assert.Equal("PanelId 不能为空", result);
    }

    [Fact]
    public void GetValidationError_ReturnsInvalidType_WhenPanelTypeIsInvalid()
    {
      var panels = new List<Phase3Validator.PanelUI3>
      {
        new Phase3Validator.PanelUI3 { PanelId = "p3-1", Type = "BogusType", Width = 1200, Height = 2400, Thickness = 6 }
      };
      var result = Phase3Validator.GetValidationError(_validRegion, panels);
      Assert.Equal("Panel Type 非法: BogusType", result);
    }

    [Fact]
    public void GetValidationError_ReturnsDuplicateIds_WhenPanelIdsAreDuplicated()
    {
      var panels = new List<Phase3Validator.PanelUI3>
      {
        new Phase3Validator.PanelUI3 { PanelId = "p3-1", Type = "Single", Width = 1200, Height = 2400, Thickness = 6 },
        new Phase3Validator.PanelUI3 { PanelId = "p3-1", Type = "Laminated", Width = 1500, Height = 3000, Thickness = 12 }
      };
      var result = Phase3Validator.GetValidationError(_validRegion, panels);
      Assert.Equal("PanelId 重复", result);
    }

    [Fact]
    public void GetValidationError_ReturnsInvalidDimensions_WhenWidthIsNegative()
    {
      var panels = new List<Phase3Validator.PanelUI3>
      {
        new Phase3Validator.PanelUI3 { PanelId = "p3-1", Type = "Single", Width = -500, Height = 2400, Thickness = 6 }
      };
      var result = Phase3Validator.GetValidationError(_validRegion, panels);
      Assert.Equal("Panel 尺寸必须大于 0", result);
    }

    [Fact]
    public void Phase3InputIsValid_ReturnsTrue_ForMultipleValidPanels()
    {
      var panels = new List<Phase3Validator.PanelUI3>
      {
        new Phase3Validator.PanelUI3 { PanelId = "p3-1", Type = "Single", Width = 1200, Height = 2400, Thickness = 6 },
        new Phase3Validator.PanelUI3 { PanelId = "p3-2", Type = "Laminated", Width = 1500, Height = 3000, Thickness = 12 },
        new Phase3Validator.PanelUI3 { PanelId = "p3-3", Type = "Insulating", Width = 2000, Height = 4000, Thickness = 24 }
      };
      var result = Phase3Validator.Phase3InputIsValid(_validRegion, panels);
      Assert.True(result);
    }

    [Fact]
    public void Phase3IsSubmittable_ReturnsFalse_WhenAnyPanelHasZeroThickness()
    {
      var panels = new List<Phase3Validator.PanelUI3>
      {
        new Phase3Validator.PanelUI3 { PanelId = "p3-1", Type = "Single", Width = 1200, Height = 2400, Thickness = 6 },
        new Phase3Validator.PanelUI3 { PanelId = "p3-2", Type = "Laminated", Width = 1500, Height = 3000, Thickness = 0 }
      };
      var result = Phase3Validator.Phase3IsSubmittable(_validRegion, panels);
      Assert.False(result);
    }
  }
}
