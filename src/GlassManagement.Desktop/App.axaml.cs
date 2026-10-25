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
            // --calc: 只打开 V35 结构计算主窗口（不再同时开 Glass 主窗口）
            var args = desktop.Args ?? System.Array.Empty<string>();
            if (System.Array.Exists(args, a => a == "--calc"))
            {
                // 诊断用：--show=<窗体标题关键字> 直接载入该窗体（仅显式传参时生效，
                // 正常启动仍然没有默认页）
                var show = System.Array.Find(args, a => a.StartsWith("--show="));
                GlassManagement.Desktop.Views.Calc.CalcMainWindow.ShowOnStartup =
                    show?.Substring("--show=".Length);
                // 诊断用：--menu=<顶级菜单>/<子项>，与真实鼠标点击同一 OpenFor 路径
                // （Avalonia ContextMenu 的 MenuItem 不支持 UIA InvokePattern，自动化回归经此入口）
                var menu = System.Array.Find(args, a => a.StartsWith("--menu="));
                if (menu is not null)
                {
                    GlassManagement.Desktop.Views.Calc.CalcMainWindow.MenuOnStartup = menu.Substring("--menu=".Length);
                }
                // 诊断用：--selftest=preview —— 自动走 第一屏→下一步→立柱屏→预演（回归验证）
                var selfTest = System.Array.Find(args, a => a.StartsWith("--selftest="));
                if (selfTest is not null)
                {
                    GlassManagement.Desktop.Views.Calc.CalcMainWindow.SelfTestOnStartup =
                        selfTest.Substring("--selftest=".Length);
                }
                // 诊断用：--dump-preview=<path> —— 自测后把预演结果导出为文本，供与 V31 自动对拍
                var dump = System.Array.Find(args, a => a.StartsWith("--dump-preview="));
                if (dump is not null)
                {
                    GlassManagement.Desktop.Views.Calc.CalcMainWindow.DumpPreviewPath =
                        dump.Substring("--dump-preview=".Length);
                }
                // 诊断/批量入口：--calcbook=<path> 直接出一份计算书后退出（无窗口）
                var bookPath = System.Array.Find(args, a => a.StartsWith("--calcbook="));
                if (bookPath is not null)
                {
                    var target = bookPath.Substring("--calcbook=".Length);
                    // 仅用于复现 V31「全隐框」对拍工程那组参数（wk 1.285 / 1100×1380 / 支点 3000）
                    if (System.Array.Exists(args, a => a == "--v31sample"))
                    {
                        var s0 = GlassManagement.Desktop.ViewModels.Calc.CalcSession.Default;
                        s0.WindStandardKpa = 1.285;
                        s0.WindStandardRawKpa = 1.285;
                        s0.ColumnBayWidthMm = 1100;
                        s0.ColumnSpanMm = 3000;
                        s0.BeamUpperGridHeightMm = 1380;
                        s0.BeamLeftBayMm = 1100;
                        s0.BeamRightBayMm = 1100;
                    }
                    var made = GlassManagement.Desktop.ViewModels.Calc.CalcWizardViewModel
                        .Default.GenerateCalcBook(string.IsNullOrWhiteSpace(target) ? null : target);
                    System.Console.WriteLine(made is null ? "CALCBOOK FAILED" : "CALCBOOK " + made);
                    System.Console.Out.Flush();
                    // 用 Environment.Exit 而不是 desktop.Shutdown()：后者会回到 Dispatcher 主循环
                    // 再抛 "Dispatcher shut down"，退出码变成非 0，批量脚本不好判。
                    System.Environment.Exit(made is null ? 1 : 0);
                }

                desktop.MainWindow = new GlassManagement.Desktop.Views.Calc.CalcMainWindow();
            }
            else
            {
                desktop.MainWindow = new MainWindow();
            }
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
