using System;
using System.Collections.Generic;
using GlassManagement.Core.Models;
using Xunit;

namespace GlassManagement.Api.Tests
{
  public static class Phase3Validator
  {
    public class PanelUI3
    {
      public string PanelId { get; set; } = "p3-1";
      public string Type { get; set; } = "Single";
      public double Width { get; set; } = 1200;
      public double Height { get; set; } = 2400;
      public double Thickness { get; set; } = 6;
    }

    public static bool Phase3InputIsValid(string? selectedRegion, List<PanelUI3>? panels)
    {
      if (string.IsNullOrWhiteSpace(selectedRegion)) return false;
      if (panels == null || panels.Count == 0) return false;
      foreach (var p in panels)
      {
        if (string.IsNullOrWhiteSpace(p.PanelId)) return false;
        if (!Enum.TryParse<Phase3GlassPanelType>(p.Type, true, out var _)) return false;
        if (p.Width <= 0 || p.Height <= 0 || p.Thickness <= 0) return false;
      }
      return true;
    }

    public static bool ArePanelIdsUnique(List<PanelUI3>? panels)
    {
      if (panels == null) return false;
      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var p in panels)
      {
        if (string.IsNullOrWhiteSpace(p.PanelId)) return false;
        if (!seen.Add(p.PanelId)) return false;
      }
      return true;
    }

    public static bool Phase3IsSubmittable(string? selectedRegion, List<PanelUI3>? panels)
    {
      return Phase3InputIsValid(selectedRegion, panels) && ArePanelIdsUnique(panels);
    }

    public static string GetValidationError(string? selectedRegion, List<PanelUI3>? panels)
    {
      if (string.IsNullOrWhiteSpace(selectedRegion)) return "区域未选择";
      if (panels == null || panels.Count == 0) return "Panel 为空";
      foreach (var p in panels)
      {
        if (string.IsNullOrWhiteSpace(p.PanelId)) return "PanelId 不能为空";
        if (!Enum.TryParse<Phase3GlassPanelType>(p.Type, true, out var _)) return $"Panel Type 非法: {p.Type}";
        if (p.Width <= 0 || p.Height <= 0 || p.Thickness <= 0) return "Panel 尺寸必须大于 0";
      }
      if (!ArePanelIdsUnique(panels)) return "PanelId 重复";
      return string.Empty;
    }
  }
}
