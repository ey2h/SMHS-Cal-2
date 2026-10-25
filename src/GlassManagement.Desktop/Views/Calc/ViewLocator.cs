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
            ConnectCalcViewModel vm => new ConnectCalcView { DataContext = vm },
            EmbedCalcViewModel vm => new EmbedCalcView { DataContext = vm },
            SealantCalcViewModel vm => new SealantCalcView { DataContext = vm },
            CalcBookPanelViewModel vm => new CalcBookPanelView { DataContext = vm },
            _ => new TextBlock { Text = "未找到视图: " + data?.GetType().Name },
        };
        return view ?? (Control?)data;
    }

    public bool Match(object? data) => data is not null;
}
