using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using GlassManagement.Desktop.ViewModels.Calc;

namespace GlassManagement.Desktop.Views.Calc;

/// <summary>
/// Calc V35 style main window: real menu tree (extracted from decrypted menu records),
/// MDI-like client area hosting the reconstructed calculation forms.
///
/// MVVM：向导链上的三屏（基本参数输入 / 立柱计算 / 多跨数据输入）由 CalcWizardViewModel
/// 驱动，FormHost.ContentTemplate = ViewLocator；链上参数存于共享 CalcSession，一路沿用。
/// 其余菜单仍走静态复刻窗体（Content 直接是 Control，ViewLocator 原样放行）。
/// </summary>
public partial class CalcMainWindow : Window
{
    /// <summary>诊断入口：仅当命令行显式给出 --show=&lt;标题关键字&gt; 时非空。</summary>
    public static string? ShowOnStartup { get; set; }

    /// <summary>诊断入口：--menu=&lt;顶级菜单&gt;/&lt;子项&gt;，与真实鼠标点击走同一 OpenFor 路径
    /// （Avalonia 的 ContextMenu MenuItem 不支持 UIA InvokePattern，自动化验证需经此入口）。</summary>
    public static string? MenuOnStartup { get; set; }

    /// <summary>诊断入口：--selftest=preview —— 自动 第一屏 →「下一步」→ 立柱屏 →「预演」。</summary>
    public static string? SelfTestOnStartup { get; set; }

    /// <summary>诊断入口：--dump-preview=&lt;文件路径&gt; —— 自测后把预演结果按行导出，供与 V31 自动对拍。</summary>
    public static string? DumpPreviewPath { get; set; }

    private readonly CalcWizardViewModel _wizard = CalcWizardViewModel.Default;
    private ContentControl _host = null!;
    private TextBlock _status = null!;
    private TextBlock _watermark = null!;

    public CalcMainWindow()
    {
        InitializeComponent();

        var bar = this.FindControl<WrapPanel>("MenuBar")!;
        _host = this.FindControl<ContentControl>("FormHost")!;
        _status = this.FindControl<TextBlock>("StatusText")!;
        _watermark = this.FindControl<TextBlock>("Watermark")!;

        // MVVM：ViewModel → View 映射；已是 Control 的静态窗体原样显示
        _host.ContentTemplate = new ViewLocator();
        _wizard.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CalcWizardViewModel.StatusText))
                _status.Text = _wizard.StatusText;
            // 链上「退出/上一步/下一步」等由子 VM 触发整屏替换，这里统一同步宿主
            if (e.PropertyName == nameof(CalcWizardViewModel.CurrentContent))
            {
                _host.Content = _wizard.CurrentContent;
                _watermark.IsVisible = _wizard.CurrentContent is null;
            }
        };

        foreach (var top in CalcMenuCatalog.Menus)
        {
            // 顶级菜单做成扁平按钮，放进 WrapPanel，窗口变窄时整项折到下一行
            var btn = new Button { Content = top.Title };
            btn.Classes.Add("menubtn");

            if (top.Items.Count == 0)
            {
                var leaf = top;
                btn.Click += (_, _) => OpenFor(leaf.Title, null);
            }
            else
            {
                // 用 ContextMenu 承载下拉项（Avalonia 11.2 无 MenuFlyoutItem，ContextMenu 渲染标准竖向菜单）
                var menu = new ContextMenu();
                foreach (var sub in top.Items)
                {
                    if (sub.Separator)
                    {
                        menu.Items.Add(new Separator());
                        continue;
                    }
                    var item = new MenuItem { Header = sub.Caption, Tag = sub.Name };
                    var t = top.Title;
                    var c = sub.Caption;
                    item.Click += (_, _) => OpenFor(t, c);
                    menu.Items.Add(item);
                }
                btn.ContextMenu = menu;
                btn.Click += (_, _) => menu.Open(btn);
            }

            bar.Children.Add(btn);
        }

        // 不设默认页：启动后客户区保持空白水印，必须从菜单选择才打开对应界面
        // （诊断：仅当 --show=xxx 或 --menu=顶级/子项 显式指定时才预载）
        if (!string.IsNullOrWhiteSpace(MenuOnStartup))
        {
            var rest = MenuOnStartup!;
            var slash = rest.IndexOf('/');
            if (slash >= 0) OpenFor(rest.Substring(0, slash), rest.Substring(slash + 1));
            else OpenFor(rest, null);
        }
        else if (!string.IsNullOrWhiteSpace(ShowOnStartup))
        {
            OpenFor(ShowOnStartup!, null);
            // 等布局完成后再探测，否则视觉树尚未展开、Bounds 全为 0
            this.Loaded += (_, _) => System.Threading.Tasks.Task.Run(async () =>
            {
                await System.Threading.Tasks.Task.Delay(1500);
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(
                    () => ProbeStyles(_host, ShowOnStartup!));
            });
        }

        // 诊断：--selftest=preview —— 从第一屏走向导到立柱屏并执行「预演」
        //       --selftest=column-only —— 故意跳过第一屏直接进立柱屏预演（验证计算链门禁）
        if (!string.IsNullOrWhiteSpace(SelfTestOnStartup))
        {
            this.Loaded += (_, _) => System.Threading.Tasks.Task.Run(async () =>
            {
                await System.Threading.Tasks.Task.Delay(1200);
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    string dumpText;
                    if (string.Equals(SelfTestOnStartup, "column-only", System.StringComparison.OrdinalIgnoreCase))
                    {
                        _wizard.SelfTestColumnOnly();
                        dumpText = _wizard.DumpColumnPreviewText();
                    }
                    else if (string.Equals(SelfTestOnStartup, "sweep", System.StringComparison.OrdinalIgnoreCase))
                    {
                        dumpText = _wizard.SweepBasicPage();   // 第一页选项扫描（CSV）
                    }
                    else if (string.Equals(SelfTestOnStartup, "basic40", System.StringComparison.OrdinalIgnoreCase))
                    {
                        dumpText = _wizard.SweepBasic40();      // 第一页 40 案全量（与 V31 basicsweep 同名同序）
                    }
                    else if (string.Equals(SelfTestOnStartup, "region", System.StringComparison.OrdinalIgnoreCase))
                    {
                        dumpText = _wizard.DumpRegionLookup(); // 地区查找诊断
                    }
                    else if (string.Equals(SelfTestOnStartup, "param", System.StringComparison.OrdinalIgnoreCase))
                    {
                        dumpText = _wizard.DumpParamSource();  // 参数库数据源诊断
                    }
                    else if (string.Equals(SelfTestOnStartup, "col-doublespan", System.StringComparison.OrdinalIgnoreCase))
                    {
                        dumpText = _wizard.SelfTestDoubleSpan();  // 双跨梁两组参数（与 V31 0xD35D8C 对拍）
                    }
                    else if (string.Equals(SelfTestOnStartup, "col-matrix", System.StringComparison.OrdinalIgnoreCase))
                    {
                        dumpText = _wizard.SelfTestColumnMatrix();  // 模型×跨度矩阵（每组先回第一页重算）
                    }
                    else if (string.Equals(SelfTestOnStartup, "unit-beam", System.StringComparison.OrdinalIgnoreCase))
                    {
                        _wizard.SelfTestUnitBeamPreview();      // 单元式横梁（上下框 17 量）
                        dumpText = _wizard.DumpUnitBeamPreviewText();
                    }
                    else if (string.Equals(SelfTestOnStartup, "beam", System.StringComparison.OrdinalIgnoreCase))
                    {
                        _wizard.SelfTestBeamPreview();          // 第一屏→立柱→横梁→预演
                        dumpText = _wizard.DumpBeamPreviewText();
                    }
                    else if (string.Equals(SelfTestOnStartup, "col-custom", System.StringComparison.OrdinalIgnoreCase))
                    {
                        dumpText = _wizard.SelfTestCustomMaterial();  // 「其它材质」人工参数
                    }
                    else
                    {
                        _wizard.SelfTestColumnPreview();
                        dumpText = _wizard.DumpColumnPreviewText();
                    }
                    ShowWizard();
                    // 对拍用：把预演结果落盘，供 compare_v31.py 与 V31 实机输出逐项比较
                    if (!string.IsNullOrWhiteSpace(DumpPreviewPath))
                    {
                        try
                        {
                            System.IO.File.WriteAllText(
                                DumpPreviewPath!,
                                dumpText,
                                System.Text.Encoding.UTF8);
                        }
                        catch { /* 诊断路径失败不影响界面 */ }
                    }
                });
            });
        }
    }

    /// <summary>诊断：把已加载窗体里控件的实际字号/字体/尺寸写到文件，用于验证样式是否生效。</summary>
    private static void ProbeStyles(Control root, string name)
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("form=" + name);
            void Walk(Control c)
            {
                var fs = c.GetType().GetProperty("FontSize")?.GetValue(c)?.ToString() ?? "-";
                var ff = c.GetType().GetProperty("FontFamily")?.GetValue(c)?.ToString() ?? "-";
                var b = c.Bounds;
                sb.AppendLine($"{c.GetType().Name}\tfs={fs}\tff={ff}\t{b.Width:0}x{b.Height:0}");
                // 走视觉树：UserControl/模板内的子树不在逻辑 Children 里
                foreach (var ch in c.GetVisualChildren())
                    if (ch is Control cc) Walk(cc);
            }
            Walk(root);
            System.IO.File.WriteAllText(@"C:\code\Glass\local-data\recon\style_probe.txt", sb.ToString());
        }
        catch { /* 诊断失败不影响主流程 */ }
    }

    private void OpenFor(string top, string? sub)
    {
        var path = string.IsNullOrWhiteSpace(sub) ? top : top + " / " + sub;

        // 0) MVVM 向导链：菜单直接命中链上三屏 → 走共享会话（跨屏参数沿用）
        switch (top)
        {
            case "幕墙计算-基本参数输入":
                _wizard.GoBasic(); ShowWizard(); return;
            case "幕墙计算-立柱计算":
                _wizard.GoColumnCalc(); ShowWizard(); return;
            case "多跨数据输入（立柱计算·可操作复刻）":
                _wizard.GoMultiSpan(); ShowWizard(); return;
            case "幕墙计算-玻璃计算":
                _wizard.GoGlassCalc(); ShowWizard(); return;
            case "幕墙计算-连接计算":
                _wizard.GoConnectCalc(); ShowWizard(); return;
            case "幕墙计算-埋件与焊缝计算":
                _wizard.GoEmbedCalc(); ShowWizard(); return;
            case "幕墙计算-胶类、伸缩及紧固计算":
                _wizard.GoSealantCalc(); ShowWizard(); return;
        }
        // 「玻璃幕墙」顶级/「全隐框」子项：向导第一屏
        if (_wizard.OpenMenuPath(top, sub)) { ShowWizard(); return; }

        // 1) 优先走显式映射（菜单路径 -> 窗体标题）
        var mapped = CalcMenuMap.Resolve(top, sub);
        if (mapped is not null)
        {
            var hit = CalcFormCatalog.All.FirstOrDefault(x => x.Title == mapped);
            if (hit is not null && Show(hit, path, "精确绑定")) return;
        }

        // 2) 回退：菜单文字与窗体标题的模糊匹配
        var best = CalcFormCatalog.All
            .Select(e => (e, score: Score(path, e.Title)))
            .OrderByDescending(t => t.score)
            .First();
        if (best.score >= 2 && Show(best.e, path, "未绑定·模糊匹配")) return;

        _host.Content = null;
        _watermark.IsVisible = true;
        _status.Text = path + "   ·   该功能界面尚未复刻";
    }

    private void ShowWizard()
    {
        // CurrentContent 为 ViewModel，由 ViewLocator 生成 View；
        // 状态文本由 _wizard.PropertyChanged 统一写入状态栏。
        _host.Content = _wizard.CurrentContent;
        _watermark.IsVisible = _wizard.CurrentContent is null;
        _status.Text = _wizard.StatusText;
    }

    private bool Show(CalcFormCatalog.Entry e, string path, string tag)
    {
        try
        {
            _host.Content = e.Create();
            _watermark.IsVisible = false;
            _status.Text = $"{path}   →   {e.Title}  ({e.Width}×{e.Height}px)  [{tag}·静态]";
            return true;
        }
        catch (Exception ex)
        {
            _status.Text = path + "  加载失败: " + ex.Message;
            return false;
        }
    }

    /// <summary>共享二字词数量，作为标题相似度。</summary>
    private static int Score(string a, string b)
    {
        int s = 0;
        for (int i = 0; i + 2 <= a.Length; i++)
        {
            var g = a.Substring(i, 2);
            if (b.Contains(g, StringComparison.Ordinal)) s++;
        }
        return s;
    }
}
