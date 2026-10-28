using Microsoft.Maui;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Controls.Hosting;

namespace GlassManagement.Desktop.Blazor;

public partial class App : Application
{
    public App()
    {
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage())
        {
            Title = "Glass Management",
            Width = 1400,
            Height = 900
        };
    }
}
