using System.Text.Json.Serialization;

namespace GlassManagement.Core.Models;

#region ===== 递归玻璃组合模型 (霸总设计) =====

/// <summary>玻璃结构类型</summary>
public enum GlassType
{
    SingleTempered,
    Laminated,
    SingleInsulating,
    DoubleInsulating,
    TripleInsulating
}

/// <summary>钢化类型</summary>
public enum TemperedType { Float, HalfTempered, FullTempered }

/// <summary>胶片类型</summary>
public enum FilmType { PVB, SGP }

/// <summary>气体类型</summary>
public enum GasType { Air, Ar }

/// <summary>面板类别</summary>
public enum PanelCategory { NonInsulating, SingleInsulating, DoubleInsulating, TripleInsulating }

/// <summary>
/// GlassBase 抽象基类：封装所有玻璃结构的公共属性接口。
/// 使用 System.Text.Json 多态序列化 (discriminator: "$type")。
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(SingleTemperedGlass), "SingleTempered")]
[JsonDerivedType(typeof(LaminatedGlass), "Laminated")]
[JsonDerivedType(typeof(SingleInsulatingGlass), "SingleInsulating")]
[JsonDerivedType(typeof(DoubleInsulatingGlass), "DoubleInsulating")]
[JsonDerivedType(typeof(TripleInsulatingGlass), "TripleInsulating")]
public abstract class GlassBase
{
    /// <summary>玻璃结构类型</summary>
    public GlassType GlassType { get; protected set; }

    /// <summary>钢化类型（单片直接使用，夹胶/中空可用于展示默认）</summary>
    public TemperedType TemperedType { get; set; }

    /// <summary>友好显示名称，如："12全钢化"、"12全钢化+1.52PVB+12全钢化" 等</summary>
    [JsonIgnore]
    public abstract string DisplayName { get; }

    /// <summary>将递归树转为扁平化 GlassConfigDto (用于API传输)</summary>
    public GlassConfigDto ToConfigDto()
    {
        return this switch
        {
            SingleTemperedGlass s => new GlassConfigDto
            {
                PanelCategory = PanelCategory.NonInsulating,
                OuterPane = new GlassPaneDto
                {
                    IsLaminated = false,
                    Thickness = s.Thickness,
                    TemperedType = s.TemperedType
                }
            },
            LaminatedGlass lam => new GlassConfigDto
            {
                PanelCategory = PanelCategory.NonInsulating,
                OuterPane = LaminatedToPane(lam)
            },
            SingleInsulatingGlass si => new GlassConfigDto
            {
                PanelCategory = PanelCategory.SingleInsulating,
                OuterPane = GlassBaseToPane(si.OuterGlass),
                InnerPane = GlassBaseToPane(si.InnerGlass),
                FirstGap = new InsulatingGapDto
                {
                    Thickness = si.InsulatingLayer.Thickness,
                    GasType = si.InsulatingLayer.GasType
                }
            },
            DoubleInsulatingGlass di => new GlassConfigDto
            {
                PanelCategory = PanelCategory.DoubleInsulating,
                OuterPane = GlassBaseToPane(di.OuterGlass),
                MiddlePane = GlassBaseToPane(di.MiddleGlass),
                InnerPane = GlassBaseToPane(di.InnerGlass),
                FirstGap = new InsulatingGapDto
                {
                    Thickness = di.FirstInsulatingLayer.Thickness,
                    GasType = di.FirstInsulatingLayer.GasType
                },
                SecondGap = new InsulatingGapDto
                {
                    Thickness = di.SecondInsulatingLayer.Thickness,
                    GasType = di.SecondInsulatingLayer.GasType
                }
            },
            _ => throw new InvalidOperationException($"未知的玻璃类型: {GetType().Name}")
        };
    }

    /// <summary>从 GlassConfigDto 重建递归树</summary>
    public static GlassBase FromConfigDto(GlassConfigDto dto)
    {
        return dto.PanelCategory switch
        {
            PanelCategory.NonInsulating => PaneToGlassBase(dto.OuterPane),
            PanelCategory.SingleInsulating => new SingleInsulatingGlass
            {
                OuterGlass = PaneToGlassBase(dto.OuterPane),
                InnerGlass = PaneToGlassBase(dto.InnerPane ?? new GlassPaneDto()),
                InsulatingLayer = new InsulatingLayer
                {
                    Thickness = dto.FirstGap?.Thickness ?? 12,
                    GasType = dto.FirstGap?.GasType ?? GasType.Air
                }
            },
            PanelCategory.DoubleInsulating => new DoubleInsulatingGlass
            {
                OuterGlass = PaneToGlassBase(dto.OuterPane),
                MiddleGlass = PaneToGlassBase(dto.MiddlePane ?? new GlassPaneDto()),
                InnerGlass = PaneToGlassBase(dto.InnerPane ?? new GlassPaneDto()),
                FirstInsulatingLayer = new InsulatingLayer
                {
                    Thickness = dto.FirstGap?.Thickness ?? 12,
                    GasType = dto.FirstGap?.GasType ?? GasType.Air
                },
                SecondInsulatingLayer = new InsulatingLayer
                {
                    Thickness = dto.SecondGap?.Thickness ?? 12,
                    GasType = dto.SecondGap?.GasType ?? GasType.Air
                }
            },
            _ => throw new InvalidOperationException($"不支持的面板类别: {dto.PanelCategory}")
        };
    }

    #region 转换辅助方法

    private static GlassPaneDto GlassBaseToPane(GlassBase glass)
    {
        return glass switch
        {
            SingleTemperedGlass s => new GlassPaneDto
            {
                IsLaminated = false,
                Thickness = s.Thickness,
                TemperedType = s.TemperedType
            },
            LaminatedGlass lam => LaminatedToPane(lam),
            _ => new GlassPaneDto { Thickness = 6, TemperedType = TemperedType.FullTempered }
        };
    }

    private static GlassPaneDto LaminatedToPane(LaminatedGlass lam)
    {
        return new GlassPaneDto
        {
            IsLaminated = true,
            LaminatedSheets = lam.GlassSheets.Select(s => new GlassSheetDto
            {
                Thickness = s.Thickness,
                TemperedType = s.TemperedType
            }).ToList(),
            LaminatedLayers = lam.Layers.Select(l => new LaminatedLayerDto
            {
                FilmType = l.FilmType,
                FilmThickness = l.FilmThickness
            }).ToList()
        };
    }

    private static GlassBase PaneToGlassBase(GlassPaneDto pane)
    {
        if (!pane.IsLaminated)
        {
            return new SingleTemperedGlass
            {
                Thickness = pane.Thickness,
                TemperedType = pane.TemperedType
            };
        }
        else
        {
            var lam = new LaminatedGlass();
            if (pane.LaminatedSheets != null)
            {
                foreach (var s in pane.LaminatedSheets)
                    lam.GlassSheets.Add(new SingleTemperedGlass
                    {
                        Thickness = s.Thickness,
                        TemperedType = s.TemperedType
                    });
            }
            if (pane.LaminatedLayers != null)
            {
                foreach (var l in pane.LaminatedLayers)
                    lam.Layers.Add(new LaminatedLayer
                    {
                        FilmType = l.FilmType,
                        FilmThickness = l.FilmThickness
                    });
            }
            return lam;
        }
    }

    #endregion
}

/// <summary>
/// 单片钢化玻璃。
/// DisplayName 示例："12全钢化"
/// </summary>
public class SingleTemperedGlass : GlassBase
{
    /// <summary>玻璃厚度 (mm)</summary>
    public int Thickness { get; set; }

    public SingleTemperedGlass()
    {
        GlassType = GlassType.SingleTempered;
        TemperedType = TemperedType.FullTempered;
    }

    public override string DisplayName => $"{Thickness}{GetTemperedText()}";

    private string GetTemperedText() => TemperedType switch
    {
        TemperedType.Float => "浮法",
        TemperedType.HalfTempered => "半钢化",
        TemperedType.FullTempered => "全钢化",
        _ => string.Empty
    };
}

/// <summary>
/// 夹胶层（胶片）
/// </summary>
public class LaminatedLayer
{
    /// <summary>胶片材质类型</summary>
    public FilmType FilmType { get; set; }

    /// <summary>胶片厚度 (mm)</summary>
    public double FilmThickness { get; set; }

    public override string ToString() => $"{FilmThickness}{FilmType}";
}

/// <summary>
/// 夹胶玻璃：多片单片玻璃 + 胶片层交替构成。
/// DisplayName 示例："12全钢化+1.52PVB+12全钢化"
/// </summary>
public class LaminatedGlass : GlassBase
{
    /// <summary>组成夹胶的单片玻璃集合（顺序重要）</summary>
    public List<SingleTemperedGlass> GlassSheets { get; set; } = new();

    /// <summary>夹层胶片集合；与 GlassSheets 交替</summary>
    public List<LaminatedLayer> Layers { get; set; } = new();

    public LaminatedGlass()
    {
        GlassType = GlassType.Laminated;
    }

    public override string DisplayName
    {
        get
        {
            var parts = GlassSheets.Select((sheet, i) =>
                $"{sheet.Thickness}{GetTemperedText(sheet.TemperedType)}" +
                (i < Layers.Count ? $"+{Layers[i]}" : string.Empty));
            return string.Join("+", parts);
        }
    }

    private static string GetTemperedText(TemperedType type) => type switch
    {
        TemperedType.Float => "浮法",
        TemperedType.HalfTempered => "半钢化",
        TemperedType.FullTempered => "全钢化",
        _ => string.Empty
    };
}

/// <summary>
/// 中空间隔层
/// </summary>
public class InsulatingLayer
{
    /// <summary>间隔厚度 (mm)</summary>
    public int Thickness { get; set; }

    /// <summary>气体类型</summary>
    public GasType GasType { get; set; }

    public override string ToString() => $"{Thickness}{GasType}";
}

/// <summary>
/// 单中空玻璃：外片 + 间隔层 + 内片
/// 每片可以是 SingleTemperedGlass 或 LaminatedGlass (递归组合)
/// </summary>
public class SingleInsulatingGlass : GlassBase
{
    /// <summary>外层玻璃</summary>
    public GlassBase OuterGlass { get; set; } = new SingleTemperedGlass();

    /// <summary>内层玻璃</summary>
    public GlassBase InnerGlass { get; set; } = new SingleTemperedGlass();

    /// <summary>中间间隔层</summary>
    public InsulatingLayer InsulatingLayer { get; set; } = new();

    public SingleInsulatingGlass()
    {
        GlassType = GlassType.SingleInsulating;
    }

    public override string DisplayName =>
        $"{OuterGlass.DisplayName}+{InsulatingLayer}+{InnerGlass.DisplayName}";
}

/// <summary>
/// 双中空玻璃：外片 + 间隔层1 + 中间片 + 间隔层2 + 内片
/// </summary>
public class DoubleInsulatingGlass : GlassBase
{
    /// <summary>外层玻璃</summary>
    public GlassBase OuterGlass { get; set; } = new SingleTemperedGlass();

    /// <summary>中间层玻璃</summary>
    public GlassBase MiddleGlass { get; set; } = new SingleTemperedGlass();

    /// <summary>内层玻璃</summary>
    public GlassBase InnerGlass { get; set; } = new SingleTemperedGlass();

    /// <summary>第一间隔层（外-中）</summary>
    public InsulatingLayer FirstInsulatingLayer { get; set; } = new();

    /// <summary>第二间隔层（中-内）</summary>
    public InsulatingLayer SecondInsulatingLayer { get; set; } = new();

    public DoubleInsulatingGlass()
    {
        GlassType = GlassType.DoubleInsulating;
    }

    public override string DisplayName =>
        $"{OuterGlass.DisplayName}+{FirstInsulatingLayer}+{MiddleGlass.DisplayName}+" +
        $"{SecondInsulatingLayer}+{InnerGlass.DisplayName}";
}

/// <summary>
/// 三玻两腔玻璃：外片 + 间隔层1 + 中间片 + 间隔层2 + 内片 (两个空腔)
/// </summary>
public class TripleInsulatingGlass : GlassBase
{
    /// <summary>外层玻璃</summary>
    public GlassBase OuterGlass { get; set; } = new SingleTemperedGlass();

    /// <summary>中间层玻璃</summary>
    public GlassBase MiddleGlass { get; set; } = new SingleTemperedGlass();

    /// <summary>内层玻璃</summary>
    public GlassBase InnerGlass { get; set; } = new SingleTemperedGlass();

    /// <summary>第一间隔层（外-中）</summary>
    public InsulatingLayer FirstInsulatingLayer { get; set; } = new();

    /// <summary>第二间隔层（中-内）</summary>
    public InsulatingLayer SecondInsulatingLayer { get; set; } = new();

    public TripleInsulatingGlass()
    {
        GlassType = GlassType.TripleInsulating;
    }

    public override string DisplayName =>
        $"{OuterGlass.DisplayName}+{FirstInsulatingLayer}+{MiddleGlass.DisplayName}+" +
        $"{SecondInsulatingLayer}+{InnerGlass.DisplayName}";
}

#endregion
