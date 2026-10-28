using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GlassManagement.Desktop.Views.Calc;

/// <summary>地板玻璃「地板玻璃结构计算」屏（V31 客户区 945×542，按 JGJ113）。</summary>
public partial class FloorGlassCalcView : UserControl
{
    public FloorGlassCalcView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
