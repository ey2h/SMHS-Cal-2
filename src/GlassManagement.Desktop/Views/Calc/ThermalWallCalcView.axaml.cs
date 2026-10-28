using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GlassManagement.Desktop.Views.Calc;

public partial class ThermalWallCalcView : UserControl
{
    public ThermalWallCalcView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
