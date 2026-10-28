using System;
using System.Linq;
using System.Collections.Generic;
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
/// <summary>诊断入口：--screenshot=&lt;png 路径&gt; —— 自测后用离屏渲染存图（会话无桌面时唯一可行的视觉校验）。</summary>
    public static string? ScreenshotPath { get; set; }

        public static string? DumpPreviewPath { get; set; }

    /// <summary>诊断入口：--sweep-all=&lt;目录&gt; —— 遍历全部菜单项：打开 → 点「计算/预演」→ 离屏截图 → 收集文本 → 写 report.tsv。</summary>
    public static string? SweepAllDir { get; set; }

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
                SetHost(_wizard.CurrentContent);
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
                    else if (string.Equals(SelfTestOnStartup, "plate", System.StringComparison.OrdinalIgnoreCase))
                    {
                        _wizard.SelfTestPlatePreview();         // 板材线：第一屏→立柱→横梁→板块→预演
                        dumpText = _wizard.DumpPlatePreviewText();
                    }
                    else if (string.Equals(SelfTestOnStartup, "stone", System.StringComparison.OrdinalIgnoreCase))
                    {
                        _wizard.SelfTestStonePreview();         // 石材线：第一屏→立柱→横梁→石材板块→预演
                        dumpText = _wizard.DumpStonePreviewText();
                    }
                    else if (string.Equals(SelfTestOnStartup, "manmade", System.StringComparison.OrdinalIgnoreCase))
                    {
                        _wizard.SelfTestManmadePreview();       // 人造板线：第一屏→立柱→横梁→人造板板块→预演
                        dumpText = _wizard.DumpManmadePreviewText();
                    }
                    else if (string.Equals(SelfTestOnStartup, "backbolt", System.StringComparison.OrdinalIgnoreCase))
                    {
                        _wizard.SelfTestBackboltPreview();      // 背栓式点挂线：第一屏→立柱→横梁→背栓板块→预演
                        dumpText = _wizard.DumpBackboltPreviewText();
                    }
                    else if (string.Equals(SelfTestOnStartup, "fullglass", System.StringComparison.OrdinalIgnoreCase))
                    {
                        _wizard.SelfTestFullGlassPreview();     // 全玻璃幕墙设计计算屏：直接预演
                        dumpText = _wizard.DumpFullGlassPreviewText();
                    }
                    else if (string.Equals(SelfTestOnStartup, "fullglass-insulated", System.StringComparison.OrdinalIgnoreCase))
                    {
                        _wizard.SelfTestFullGlassInsulated();   // 中空玻璃构型对拍
                        dumpText = _wizard.DumpFullGlassPreviewText();
                    }
                    else if (SelfTestOnStartup.StartsWith("fullglass-cfg=", System.StringComparison.OrdinalIgnoreCase)
                             && int.TryParse(SelfTestOnStartup.Substring("fullglass-cfg=".Length), out var fgCfg))
                    {
                        _wizard.SelfTestFullGlassConfig(fgCfg);  // 指定大面玻璃构型（0-7）对拍
                        dumpText = _wizard.DumpFullGlassPreviewText();
                    }
                    else if (string.Equals(SelfTestOnStartup, "dianshi", System.StringComparison.OrdinalIgnoreCase))
                    {
                        _wizard.SelfTestDianshiPreview();        // 点式肋（无肋）驳接设计计算屏：直接预演
                        dumpText = _wizard.DumpDianshiPreviewText();
                    }
                    else if (string.Equals(SelfTestOnStartup, "uglass", System.StringComparison.OrdinalIgnoreCase))
                    {
                        _wizard.SelfTestUGlass();                // U型玻璃设计计算屏：直接预演
                        dumpText = _wizard.DumpUGlassPreviewText();
                    }
                    else if (string.Equals(SelfTestOnStartup, "floor", System.StringComparison.OrdinalIgnoreCase))
                    {
                        _wizard.SelfTestFloorGlass();            // 地板玻璃结构计算屏：直接预演
                        dumpText = _wizard.DumpFloorGlassPreviewText();
                    }
                    else if (string.Equals(SelfTestOnStartup, "skylight", System.StringComparison.OrdinalIgnoreCase))
                    {
                        _wizard.SelfTestSkylight();              // 单坡采光顶杆件屏：直接预演
                        dumpText = _wizard.DumpSkylightPreviewText();
                    }
                    else if (string.Equals(SelfTestOnStartup, "awning", System.StringComparison.OrdinalIgnoreCase))
                    {
                        _wizard.SelfTestAwning();                // 雨篷计算屏：直接预演
                        dumpText = _wizard.DumpAwningPreviewText();
                    }
                    else if (string.Equals(SelfTestOnStartup, "door", System.StringComparison.OrdinalIgnoreCase))
                    {
                        _wizard.SelfTestDoorWindow();            // 门窗系统计算：wk下传→竖梃→横梃→连接件
                        dumpText = _wizard.DumpDoorWindowPreviewText();
                    }
                    else if (string.Equals(SelfTestOnStartup, "col-custom", System.StringComparison.OrdinalIgnoreCase))                    {
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
                    if (!string.IsNullOrWhiteSpace(ScreenshotPath))
                    {
                        // 不在 UI 线程上再 await：先把挂起的布局/绑定任务跑完，再同步渲染
                        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                        CaptureTo(ScreenshotPath!, dumpText);
                    }
                });
            });
        }

        // 诊断：--sweep-all=<dir> —— 遍历全部菜单项（顶级/子项），逐页打开 → 点「计算/预演」→ 截图 → 收集文本 → report.tsv
        if (!string.IsNullOrWhiteSpace(SweepAllDir))
        {
            this.Loaded += (_, _) => System.Threading.Tasks.Task.Run(async () =>
            {
                await System.Threading.Tasks.Task.Delay(1500);
                var dir = SweepAllDir!;
                System.IO.Directory.CreateDirectory(dir);
                var rep = new System.Text.StringBuilder();
                rep.AppendLine("idx\ttop\tsub\tbuttons\tclicked\ttextLen\tnote");
                int i = 0;
                foreach (var top in CalcMenuCatalog.Menus)
                {
                    var subs = new List<CalcMenuCatalog.Sub>();
                    foreach (var s in top.Items) if (!s.Separator) subs.Add(s);
                    if (subs.Count == 0) subs.Add(null!);
                    foreach (var sub in subs)
                    {
                        i++;
                        string cap = sub?.Caption ?? "";
                        string key = cap.Length == 0 ? top.Title : top.Title + "/" + cap;
                        string file = i.ToString("000");
                        try
                        {
                            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                OpenFor(top.Title, cap.Length == 0 ? null : cap);
                                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                            });
                            await System.Threading.Tasks.Task.Delay(400);

                            int btnCount = 0, clicked = 0, textLen = 0;
                            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                var btns = new List<Button>();
                                Walk(_host, c => { if (c is Button b && b.IsVisible) btns.Add(b); });
                                var targets = btns.Where(b => (b.Content as string) is "计算" or "预演").ToList();
                                btnCount = targets.Count;
                                foreach (var b in targets)
                                {
                                    try
                                    {
                                        if (b.Command is not null && b.Command.CanExecute(b.CommandParameter))
                                        { b.Command.Execute(b.CommandParameter); clicked++; }
                                    }
                                    catch { }
                                }
                                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                            });
                            await System.Threading.Tasks.Task.Delay(350);

                            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                // 离屏截图前把内容宿主撑到窗口客户区尺寸，否则未显示/未激活时 Bounds 过窄会被压成窄条
                                var vw = this.Bounds.Width > 600 ? this.Bounds.Width - 24 : 1250;
                                var vh = this.Bounds.Height > 300 ? this.Bounds.Height - 110 : 700;
                                _host.Measure(new Size(vw, vh));
                                _host.Arrange(new Rect(0, 0, vw, vh));
                                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                                CaptureTo(System.IO.Path.Combine(dir, file + ".png"), key);
                                var sb = new System.Text.StringBuilder();
                                Walk(_host, c =>
                                {
                                    if (c is TextBox tb && !string.IsNullOrEmpty(tb.Text)) sb.AppendLine(tb.Text);
                                    else if (c is TextBlock tbk && !string.IsNullOrEmpty(tbk.Text)) sb.AppendLine(tbk.Text);
                                });
                                textLen = sb.Length;
                                try { System.IO.File.WriteAllText(System.IO.Path.Combine(dir, file + ".txt"), sb.ToString(), System.Text.Encoding.UTF8); } catch { }
                            });
                            rep.AppendLine($"{i}\t{top.Title}\t{cap}\t{btnCount}\t{clicked}\t{textLen}\tOK");
                        }
                        catch (Exception ex)
                        {
                            rep.AppendLine($"{i}\t{top.Title}\t{cap}\t0\t0\t0\tEX {ex.GetType().Name}: {ex.Message}");
                        }
                    }
                }
                try { System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "report.tsv"), rep.ToString(), System.Text.Encoding.UTF8); } catch { }
                Environment.Exit(0);
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
            case "生成计算书（支持微软office及金山wps office）":
                _wizard.GoBookPanel(); ShowWizard(); return;
            case "点式玻璃幕墙":
                if (sub == "点式肋（无肋）驳接" || sub == "点式钢管结构")
                    { _wizard.GoDianshi(); ShowWizard(); return; }
                break;
            case "U玻/地板玻璃/水下玻璃":
                if (sub == "U型玻璃") { _wizard.GoUGlass(); ShowWizard(); return; }
                if (sub == "地板玻璃") { _wizard.GoFloorGlass(); ShowWizard(); return; }
                break;
            case "采光顶":
                _wizard.GoSkylight(); ShowWizard(); return;
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

        SetHost(null);
        _status.Text = path + "   ·   该功能界面尚未复刻";
    }

    /// <summary>
    /// 统一设置内容宿主。
    /// ★ 有页面时必须铺 V31 窗体灰底（HbFormBg）：计算页根容器是透明的，
    ///   不铺底就会透出深绿客户区（HbAppBg），深色正文压在深绿上几乎不可读。
    ///   无页面时回到透明，让绿色画布与水印露出来（V31 空态观感）。
    /// </summary>
    private void SetHost(object? content)
    {
        _host.Content = content;
        _watermark.IsVisible = content is null;
        // 宿主自身始终透明：灰底由表单根元素铺（HbFormBg），四周露绿底 = V31 观感
        _host.Width = double.NaN;
        _host.Height = double.NaN;
    }

    private void ShowWizard()
    {
        // CurrentContent 为 ViewModel，由 ViewLocator 生成 View；
        // 状态文本由 _wizard.PropertyChanged 统一写入状态栏。
        SetHost(_wizard.CurrentContent);
        _status.Text = _wizard.StatusText;
    }

    private bool Show(CalcFormCatalog.Entry e, string path, string tag)
    {
        try
        {
            SetHost(e.Create());
            _status.Text = $"{path}   →   {e.Title}  ({e.Width}×{e.Height}px)  [{tag}·静态]";
            return true;
        }
        catch (Exception ex)
        {
            _status.Text = path + "  加载失败: " + ex.Message;
            return false;
        }
    }

    /// <summary>
    /// 关闭当前计算屏（供屏内「退出」按钮调用）。
    /// 主窗口是同一个窗口换内容，所以「退出」= 回到空白水印态，不是关整个程序。
    /// </summary>
    public void CloseCurrentScreen()
    {
        SetHost(null);
        _status.Text = "已退出当前计算屏";
    }

    /// <summary>
    /// 屏内「下一步」跳转：按标题关键字在 <see cref="CalcFormCatalog"/> 里找条目并装载。
    /// 与 <see cref="CalcBrowserWindow.ShowByTitle"/> 行为一致（同一份目录）。
    /// </summary>
    public bool ShowByTitle(string titleKeyword)
    {
        var e = CalcFormCatalog.All.FirstOrDefault(
            x => x.Title.Contains(titleKeyword, StringComparison.Ordinal));
        if (e is null)
        {
            _status.Text = $"目录里没有「{titleKeyword}」";
            return false;
        }
        return Show(e, "屏内下一步", "导航");
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

    /// <summary>
    /// 离屏渲染当前 FormHost 里的屏并存 PNG。
    /// 看点：控件是否互相遮挡、标签是否被裁、字体是否缺字。
    /// </summary>
    private void CaptureTo(string path, string note)
    {
        var log = new System.Text.StringBuilder();
        try
        {
            var target = FormHost.Content as Control ?? (Control)FormHost;
            log.AppendLine("target=" + target.GetType().Name);
            log.AppendLine("bounds=" + target.Bounds);

            // 强制一次布局：selftest 是在 InvokeAsync 里刚设置 Content 的，
            // 这时 Bounds 往往还是 0，直接渲染会得到空图。
            if (target.Bounds.Width <= 1)
            {
                target.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                target.Arrange(new Rect(target.DesiredSize));
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                log.AppendLine("after-layout bounds=" + target.Bounds + " desired=" + target.DesiredSize);
            }

            var w = (int)Math.Ceiling(target.Bounds.Width > 1 ? target.Bounds.Width : target.DesiredSize.Width);
            var h = (int)Math.Ceiling(target.Bounds.Height > 1 ? target.Bounds.Height : target.DesiredSize.Height);
            if (w <= 1 || h <= 1) { w = 795; h = 530; log.AppendLine("fallback size"); }
            log.AppendLine($"render={w}x{h}");

            var rtb = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
            rtb.Render(target);
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);
            rtb.Save(path);
            log.AppendLine("saved");
        }
        catch (Exception ex)
        {
            log.AppendLine("EX " + ex);
        }
        finally
        {
            try { System.IO.File.WriteAllText(path + ".log.txt", log + "\nnote=" + note); } catch { }
        }
    }

    /// <summary>诊断：深度遍历可视树（--sweep-all 用来找按钮 / 收集文本）。</summary>
    private static void Walk(Control c, Action<Control> act)
    {
        act(c);
        foreach (var ch in c.GetVisualChildren())
            if (ch is Control cc) Walk(cc, act);
    }

}
