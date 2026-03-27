using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using GlassManagement.Desktop.Views;

namespace GlassManagement.Desktop;

public partial class App : Application
{
    public static App? Instance { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        Instance = this;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }

    public void ApplyTheme(string theme, Window window)
    {
        switch (theme)
        {
            case "apple":
                SetResource(window, "PageBgBrush", "#0f0f1a");
                SetResource(window, "CardBgBrush", "#1a1f35");
                SetResource(window, "AccentBrush", "#007aff");
                SetResource(window, "AccentLightBrush", "#1a2744");
                SetResource(window, "TextPrimaryBrush", "#e2e8f0");
                SetResource(window, "TextSecondaryBrush", "#94a3b8");
                SetResource(window, "BorderBrush", "#2a3040");
                SetResource(window, "TopBarBgBrush", "#141720");
                SetResource(window, "TopBarTextBrush", "#e2e8f0");
                SetResource(window, "StepDoneBrush", "#30d158");
                SetResource(window, "StepFutureBrush", "#3a3f50");
                SetResource(window, "StepFutureTextBrush", "#64748b");
                SetResource(window, "ResultBgBrush", "#1a2e1a");
                SetResource(window, "StatusBarBgBrush", "#141720");
                break;
            case "xkool":
                SetResource(window, "PageBgBrush", "#f5f5f8");
                SetResource(window, "CardBgBrush", "#ffffff");
                SetResource(window, "AccentBrush", "#8c00ff");
                SetResource(window, "AccentLightBrush", "#f0e0ff");
                SetResource(window, "TextPrimaryBrush", "#1a1a2e");
                SetResource(window, "TextSecondaryBrush", "#6b7280");
                SetResource(window, "BorderBrush", "#e5e7eb");
                SetResource(window, "TopBarBgBrush", "#8c00ff");
                SetResource(window, "TopBarTextBrush", "#ffffff");
                SetResource(window, "StepDoneBrush", "#10b981");
                SetResource(window, "StepFutureBrush", "#d1d5db");
                SetResource(window, "StepFutureTextBrush", "#9ca3af");
                SetResource(window, "ResultBgBrush", "#f5f0ff");
                SetResource(window, "StatusBarBgBrush", "#f8f5ff");
                SetResource(window, "CardRadius", new CornerRadius(16));
                break;
            default: // classic
                SetResource(window, "PageBgBrush", "#fbfbfe");
                SetResource(window, "CardBgBrush", "#ffffff");
                SetResource(window, "AccentBrush", "#2f27ce");
                SetResource(window, "AccentLightBrush", "#e8e7fa");
                SetResource(window, "TextPrimaryBrush", "#1a1a2e");
                SetResource(window, "TextSecondaryBrush", "#6b7280");
                SetResource(window, "BorderBrush", "#e5e7eb");
                SetResource(window, "TopBarBgBrush", "#ffffff");
                SetResource(window, "TopBarTextBrush", "#1a1a2e");
                SetResource(window, "StepDoneBrush", "#10b981");
                SetResource(window, "StepFutureBrush", "#d1d5db");
                SetResource(window, "StepFutureTextBrush", "#9ca3af");
                SetResource(window, "ResultBgBrush", "#f0fdf4");
                SetResource(window, "StatusBarBgBrush", "#f8fafc");
                SetResource(window, "CardRadius", new CornerRadius(8));
                break;
        }
    }

    private static void SetResource(Window window, string key, string color)
    {
        window.Resources[key] = new SolidColorBrush(Color.Parse(color));
    }

    private static void SetResource(Window window, string key, CornerRadius radius)
    {
        window.Resources[key] = radius;
    }
}
