using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using GlassManagement.Desktop.ViewModels.Calc;

namespace GlassManagement.Desktop.Views.Calc;

/// <summary>
/// MVVM View 定位器：向导 ContentControl 里的内容若是 ViewModel 则映射到对应 View；
/// 若本身已是 Control（静态复刻窗体）则原样返回。
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    public bool SupportsRecycling => false;

    public Control? Build(object? data)
    {
        Control? view = data switch
        {
            Control c => null,                       // 已是 View，原样返回
            BasicParamsViewModel vm => new BasicParamsView { DataContext = vm },
            ColumnCalcViewModel vm => new ColumnCalcView { DataContext = vm },
            MultiSpanViewModel vm => new MultiSpanInputView { DataContext = vm },
            BeamCalcViewModel vm => new BeamCalcView { DataContext = vm },
            UnitBeamCalcViewModel vm => new UnitBeamCalcView { DataContext = vm },
            GlassCalcViewModel vm => new GlassCalcView { DataContext = vm },
            OtherGlassCalcViewModel vm => new OtherGlassCalcView { DataContext = vm },
            ConnectCalcViewModel vm => new ConnectCalcView { DataContext = vm },
            EmbedCalcViewModel vm => new EmbedCalcView { DataContext = vm },
            SealantCalcViewModel vm => new SealantCalcView { DataContext = vm },
            MetalPlateCalcViewModel vm => new MetalPlateCalcView { DataContext = vm },
            StonePlateCalcViewModel vm => new StonePlateCalcView { DataContext = vm },
            ManmadePlateCalcViewModel vm => new ManmadePlateCalcView { DataContext = vm },
            BackboltPlateCalcViewModel vm => new BackboltPlateCalcView { DataContext = vm },
            FullGlassCalcViewModel vm => new FullGlassCalcView { DataContext = vm },
            DianshiGlassCalcViewModel vm => new DianshiGlassCalcView { DataContext = vm },
            UGlassCalcViewModel vm => new UGlassCalcView { DataContext = vm },
            FloorGlassCalcViewModel vm => new FloorGlassCalcView { DataContext = vm },
            SkylightCalcViewModel vm => new SkylightCalcView { DataContext = vm },
            AwningCalcViewModel vm => new AwningCalcView { DataContext = vm },
            DoorWindowCalcViewModel vm => new DoorWindowCalcView { DataContext = vm },
            ThermalGlassCalcViewModel vm => new ThermalGlassCalcView { DataContext = vm },
            ThermalWallCalcViewModel vm => new ThermalWallCalcView { DataContext = vm },
            PhysicalPerformanceViewModel vm => new PhysicalPerformanceView { DataContext = vm },
            ExternalShadingViewModel vm => new ExternalShadingView { DataContext = vm },
            WeightedAverageViewModel vm => new WeightedAverageView { DataContext = vm },
            StaticCalcViewModel vm => new StaticCalcView { DataContext = vm },
            SectionCalcViewModel vm => new SectionCalcView { DataContext = vm },
            LightningViewModel vm => new LightningView { DataContext = vm },
            InterpolationViewModel vm => new InterpolationView { DataContext = vm },
            EquationViewModel vm => new EquationView { DataContext = vm },
            CorbelViewModel vm => new CorbelView { DataContext = vm },
            CuttingStockViewModel vm => new CuttingStockView { DataContext = vm },
            CalcBookPanelViewModel vm => new CalcBookPanelView { DataContext = vm },
            // 独立工具线（B8）：15 个工具共用同一个通用视图，界面差异全部由 VM 字段列表驱动。
            SimpleToolViewModel vm => new SimpleToolView { DataContext = vm },
            _ => new TextBlock { Text = "未找到视图: " + data?.GetType().Name },
        };
        return view ?? (Control?)data;
    }

    public bool Match(object? data) => data is not null;
}
