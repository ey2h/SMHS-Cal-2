using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GlassManagement.Desktop.Views.Calc;

public partial class ThermalGlassCalcView : UserControl
{
    public ThermalGlassCalcView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
