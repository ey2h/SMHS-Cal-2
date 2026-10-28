using Microsoft.Maui.Hosting;
using Microsoft.Extensions.Hosting;

namespace GlassManagement.Desktop.Blazor;

public static class EntryPoint
{
    [STAThread]
    public static void Main(string[] args)
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        builder.Services.AddSingleton<HttpClient>(sp =>
            new HttpClient { BaseAddress = new Uri("http://localhost:5188") });

        var mauiApp = builder.Build();
        var host = mauiApp.Services.GetRequiredService<IHost>();
        host.Start();
        host.WaitForShutdown();
    }
}
