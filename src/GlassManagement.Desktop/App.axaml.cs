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
            // ★ --calcbook 必须在这之前处理：它是**无窗口批量出口**，
            //   以前挂在 --calc 分支里面，漏带 --calc 时会掉到 else 去开 Glass 老主窗口（紫色那版）。
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
                    // 第一屏的门禁靠这两个标志；样例直接用 V31 实机的两套 wk 覆盖，
                    // 免得自动跑立柱/玻璃时被"尚未算风荷载"挡回来。
                    s0.WindManual = true;
                    s0.WindManualKpa = 1.285;
                    s0.WindStandardKpa = 1.285;
                    s0.WindStandardRawKpa = 1.285;
                    s0.WindStandardSource = "V31 对拍样例（支承口径 1.285）";
                    s0.WindStandardResolved = true;
                    // 面板口径：玻璃/胶类用的是 1.37，不是 1.285
                    s0.PanelWindKpa = 1.37;
                    s0.PanelWindRawKpa = 1.37;
                }
                // --kind=<幕墙类型>：按指定类型的骨架与规范清单出书（默认全隐框）
                var kindArg = System.Array.Find(args, a => a.StartsWith("--kind="));
                var kind = kindArg is null
                    ? "全隐框玻璃幕墙"
                    : kindArg.Substring("--kind=".Length);
                var made = GlassManagement.Desktop.ViewModels.Calc.CalcWizardViewModel
                    .Default.GenerateCalcBook(string.IsNullOrWhiteSpace(target) ? null : target,
                                              kind: kind);
                System.Console.WriteLine(made is null ? "CALCBOOK FAILED" : "CALCBOOK " + made);
                System.Console.Out.Flush();
                // 用 Environment.Exit 而不是 desktop.Shutdown()：后者会回到 Dispatcher 主循环
                // 再抛 "Dispatcher shut down"，退出码变成非 0，批量脚本不好判。
                System.Environment.Exit(made is null ? 1 : 0);
            }

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
                // 诊断用：--screenshot=<path> —— 自测后用 Avalonia 离屏渲染把当前屏存 PNG
                //（本机会话常无交互桌面，ImageGrab/PrintWindow 都抓不到，只能自己渲染）
                var shot = System.Array.Find(args, a => a.StartsWith("--screenshot="));
                if (shot is not null)
                {
                    GlassManagement.Desktop.Views.Calc.CalcMainWindow.ScreenshotPath =
                        shot.Substring("--screenshot=".Length);
                }
                // 诊断用：--dump-preview=<path> —— 自测后把预演结果导出为文本，供与 V31 自动对拍
                var dump = System.Array.Find(args, a => a.StartsWith("--dump-preview="));
                if (dump is not null)
                {
                    GlassManagement.Desktop.Views.Calc.CalcMainWindow.DumpPreviewPath =
                        dump.Substring("--dump-preview=".Length);
                }
                // 诊断用：--sweep-all=<目录> —— 遍历全部菜单项，逐页打开 + 点计算/预演 + 截图 + 收集文本
                //（一条命令完成「每页每个按钮」回归，产物：NNN.png / NNN.txt / report.tsv）
                var sweep = System.Array.Find(args, a => a.StartsWith("--sweep-all="));
                if (sweep is not null)
                {
                    GlassManagement.Desktop.Views.Calc.CalcMainWindow.SweepAllDir =
                        sweep.Substring("--sweep-all=".Length);
                }

                desktop.MainWindow = new GlassManagement.Desktop.Views.Calc.CalcMainWindow();
            }
            else if (System.Array.Exists(args, a => a == "--home"))
            {
                // ★ 老的 Glass 主窗（紫色主题）只在显式 --home 时打开。
                //   它是 GitHub 早期版本带过来的外壳，不是幕墙计算界面；
                //   以前这里是默认分支，导致直接双击打开会进紫色版（用户已多次纠正）。
                desktop.MainWindow = new MainWindow();
            }
            else
            {
                // 默认入口 = 幕墙计算主窗（绿色客户区 #006600 + 白色面板 + #C0FFFF 结果框），
                // 与 V31 观感一致，是项目唯一在用的界面。
                desktop.MainWindow = new GlassManagement.Desktop.Views.Calc.CalcMainWindow();
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
