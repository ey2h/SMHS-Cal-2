using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassManagement.Calc.Data;
using GlassManagement.Calc.Engines;

namespace GlassManagement.Desktop.ViewModels.Calc;

/// <summary>
/// CW19「建筑幕墙（门窗）物理性能」视图模型。
/// 16 个子项共用：4 个计算页（139 抗风压 / 140 水密 / 149 门窗抗风压 / 150 门窗水密）
/// + 12 个说明页（IE-HTML 规范条文，文本取自 PhysicalPerformanceTexts）。
/// </summary>
public sealed partial class PhysicalPerformanceViewModel : ObservableObject
{
    public int MenuId { get; }
    public string Title { get; }
    /// <summary>规范条文正文（16 项均显示，方便使用者对照规范）。</summary>
    public string SpecText { get; }
    /// <summary>是否计算页（有公式与「计算」按钮）。</summary>
    public bool IsCalcPage { get; }
    /// <summary>是否空气声隔声页（id=137，输入为玻璃结构）。</summary>
    public bool IsInsulation => MenuId == 137;
    /// <summary>是否风荷载类计算页（输入为地面类型/高度/风压）。</summary>
    public bool IsWindLoad => MenuId is 139 or 140 or 149 or 150;

    public static readonly string[] TerrainOptions = { "A类", "B类", "C类", "D类" };
    [ObservableProperty] private int _terrainIndex = 2;       // 默认 C 类
    [ObservableProperty] private string _heightText = "100";  // 计算高度 m
    [ObservableProperty] private string _pressureText = "2.5"; // 风压（抗风压 KPa / 水密自动换算）
    [ObservableProperty] private bool _typhoon;               // 是否台风袭击区
    [ObservableProperty] private string _resultText = "";
    /// <summary>空气声隔声（id=137）玻璃结构下拉。</summary>
    public static readonly string[] StructureOptions =
        { "单片玻璃", "中空玻璃", "夹层玻璃", "中空+夹层玻璃", "三玻双中空", "三玻双夹层", "四玻双夹层中空", "四玻夹层双中空" };
    [ObservableProperty] private int _structureIndex;

    public PhysicalPerformanceViewModel(int menuId)
    {
        MenuId = menuId;
        Title = TitleFor(menuId);
        // id=137 空气声隔声：规范条文复用 143（幕墙空气声隔声）
        SpecText = menuId == 137 ? PhysicalPerformanceTexts.For(143) : PhysicalPerformanceTexts.For(menuId);
        IsCalcPage = menuId is 137 or 139 or 140 or 149 or 150;
        // 计算页默认风压按子项：抗风压 2.5KPa、水密 1500Pa（北京默认）
        PressureText = menuId is 140 or 150 ? "1500" : "2.5";
    }

    private static string TitleFor(int menuId) => menuId switch
    {
        137 => "建筑玻璃空气声隔声计算（只适用于玻璃构件）",
        139 => "建筑幕墙（门窗）物理性能-幕墙抗风压性能",
        140 => "建筑幕墙（门窗）物理性能-幕墙水密性能",
        141 => "建筑幕墙（门窗）物理性能-幕墙气密性能",
        142 => "建筑幕墙（门窗）物理性能-幕墙热工性能",
        143 => "建筑幕墙（门窗）物理性能-幕墙空气声隔声性能",
        144 => "建筑幕墙（门窗）物理性能-建筑幕墙平面内变形及抗震性能",
        145 => "建筑幕墙（门窗）物理性能-幕墙承载力性能",
        146 => "建筑幕墙（门窗）物理性能-建筑幕墙耐撞击性能",
        147 => "建筑幕墙（门窗）物理性能-建筑幕墙光学性能",
        149 => "建筑门窗物理性能-门窗抗风压性能",
        150 => "建筑门窗物理性能-门窗水密性能",
        151 => "建筑门窗物理性能-门窗气密性能",
        152 => "建筑门窗物理性能-门窗热工性能",
        153 => "建筑门窗物理性能-门窗空气声隔声性能",
        154 => "建筑门窗物理性能-门窗采光性能",
        156 => "建筑门窗物理性能-玻璃采光顶物理性能",
        _ => "建筑幕墙（门窗）物理性能",
    };

    private double Parse(string? s, double fallback)
        => double.TryParse((s ?? "").Trim(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private TerrainCategory Terrain => (TerrainCategory)TerrainIndex;

    [RelayCommand]
    private void Calc()
    {
        double z = Parse(HeightText, 100);
        double p = Parse(PressureText, 2.5);
        switch (MenuId)
        {
            case 139 or 149:   // 抗风压（幕墙 139 / 门窗 149，GB/T31433-2015 表2）
            {
                int g = WindResistanceEngine.Grade(p);
                // 逐字复刻 V31 实机 139/149 输出（规范条文 + 表2 + 分步比较），2026-10-28 T12b
                ResultText = PhysicalPerformanceOutput.WindResistance(MenuId == 149, "北京", p, g);
                break;
            }
            case 140:   // 幕墙水密（JGJ102-2003 4.2.5 / GB/T31433-2015 表17）
            {
                // 输入框按 Pa（默认 1500），模板输出按 KPa（1.5）
                ResultText = PhysicalPerformanceOutput.WallWaterTightness("北京", z, Terrain, Typhoon, p / 1000.0);
                break;
            }
            case 150:   // 门窗水密（JGJ103-2008 3.3.1-2 / GB/T31433-2015 表16）
            {
                ResultText = PhysicalPerformanceOutput.DoorWaterTightness("北京", z, Terrain, Typhoon, p);
                break;
            }
            case 137:   // 空气声隔声（7 行模板逐字复刻，2026-10-28 T12c）
            {
                var s = (SoundInsulationEngine.GlassStructure)StructureIndex;
                double r = SoundInsulationEngine.ComputeDefault(s);
                // 组合墙：墙厚 50mm / 等效厚 25mm（实机当前态 31.41dB；T11 记录干净默认内部 39.06，待重启复核）
                double rWall = SoundInsulationEngine.ComputeWall(r, 50, 25);
                ResultText = PhysicalPerformanceOutput.Insulation(
                    r, rWall,
                    SoundInsulationEngine.Grade8485(r),
                    SoundInsulationEngine.Grade50121(r),
                    SoundInsulationEngine.Grade21086(r));
                break;
            }
            default:
                ResultText = "本页为规范条文说明，无计算。";
                break;
        }
    }
}
