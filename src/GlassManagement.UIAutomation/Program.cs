/*
 * SMHS Desktop 客户端 — FlaUI 自动化测试
 * 
 * 使用 Windows UI Automation (UIA3) 操控 Avalonia 桌面窗口
 * 仅 Windows 可用！
 *
 * 使用方法:
 *   1. 先启动 API:     cd src/GlassManagement.Api && dotnet run
 *   2. 再启动 Desktop: cd src/GlassManagement.Desktop && dotnet run
 *   3. 运行本程序:     cd src/GlassManagement.UIAutomation && dotnet run
 *
 * 或者让本程序自动启动 Desktop:
 *   dotnet run -- --launch "../GlassManagement.Desktop"
 */

using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using System.Diagnostics;

// ==================== 配置 ====================
string? launchProject = null;
string screenshotDir = Path.Combine("test_results", "screenshots");
string reportFile = Path.Combine("test_results", "flaui_report.txt");

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--launch" && i + 1 < args.Length)
        launchProject = args[++i];
}

Directory.CreateDirectory(screenshotDir);
Directory.CreateDirectory(Path.GetDirectoryName(reportFile)!);

var results = new List<(string Name, bool Passed, string Detail)>();
int passed = 0, failed = 0;

void Record(string name, bool ok, string detail = "")
{
    results.Add((name, ok, detail));
    if (ok) passed++; else failed++;
    Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {name}" + (detail != "" ? $" — {detail}" : ""));
}

void TakeScreenshot(Window window, string name)
{
    try
    {
        var img = FlaUI.Core.Capturing.Capture.Element(window);
        img.ToFile(Path.Combine(screenshotDir, $"{name}.png"));
        img.Dispose();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  [WARN] 截图失败: {ex.Message}");
    }
}

// ==================== 启动/连接应用 ====================
Console.WriteLine("============================================================");
Console.WriteLine("SMHS Desktop 客户端 FlaUI 自动化测试");
Console.WriteLine($"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
Console.WriteLine("============================================================\n");

Application? app = null;
if (launchProject != null)
{
    Console.WriteLine($"启动 Desktop: dotnet run --project {launchProject}");
    app = Application.Launch("dotnet", $"run --project {launchProject}");
}

using var automation = new UIA3Automation();

// 查找主窗口
Window? mainWindow = null;

if (app != null)
{
    mainWindow = app.GetMainWindow(automation, TimeSpan.FromSeconds(30));
}
else
{
    // 尝试附加到已运行的进程
    Console.WriteLine("查找已运行的 SMHS Desktop 进程...");
    var procs = Process.GetProcesses()
        .Where(p => p.MainWindowTitle.Contains("SMHS") || p.MainWindowTitle.Contains("Glass"))
        .ToArray();

    if (procs.Length > 0)
    {
        app = Application.Attach(procs[0]);
        mainWindow = app.GetMainWindow(automation, TimeSpan.FromSeconds(10));
    }
}

// ==================== 测试1: 窗口启动 ====================
Console.WriteLine("\n=== 测试1: 窗口启动 ===");
Record("窗口启动", mainWindow != null, mainWindow != null ? $"标题: {mainWindow!.Title}" : "未找到窗口");

if (mainWindow == null)
{
    Console.WriteLine("\n错误: 未找到主窗口，测试终止。");
    Console.WriteLine("请确保 GlassManagement.Desktop 正在运行，或使用 --launch 参数。");
    return 1;
}

TakeScreenshot(mainWindow, "01_window_launch");

// ==================== 测试2: 服务器连接 ====================
Console.WriteLine("\n=== 测试2: 服务器连接 ===");
{
    var serverInput = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("ServerUrlInput"));
    var connectBtn = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("ConnectButton"));

    bool hasControls = serverInput != null && connectBtn != null;
    Record("找到连接控件", hasControls,
        $"ServerUrlInput={serverInput != null}, ConnectButton={connectBtn != null}");

    if (serverInput != null)
    {
        var textBox = serverInput.AsTextBox();
        Record("服务器地址默认值", textBox.Text.Contains("localhost"),
            $"当前值: {textBox.Text}");
    }

    if (connectBtn != null)
    {
        connectBtn.AsButton().Invoke();
        Thread.Sleep(2000);
        TakeScreenshot(mainWindow, "02_after_connect");
        Record("点击连接按钮", true);
    }
}

// ==================== 测试3: Step1 输入 ====================
Console.WriteLine("\n=== 测试3: Step1 输入 ===");
{
    var regionSearch = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("RegionSearch"));
    Record("找到地区搜索框", regionSearch != null);

    if (regionSearch != null)
    {
        var tb = regionSearch.AsTextBox();
        tb.Text = "北京";
        Thread.Sleep(1000);
        TakeScreenshot(mainWindow, "03_region_search");
        Record("地区搜索输入", tb.Text == "北京", $"当前值: {tb.Text}");
    }

    var themeSelector = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("ThemeSelector"));
    Record("找到主题选择器", themeSelector != null);
}

// ==================== 测试4: 步骤导航 ====================
Console.WriteLine("\n=== 测试4: 步骤导航(下一步) ===");
{
    var nextBtn = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("NextStepButton"));
    Record("找到下一步按钮", nextBtn != null);

    int stepsVisited = 0;
    if (nextBtn != null)
    {
        for (int i = 0; i < 8; i++)
        {
            try
            {
                TakeScreenshot(mainWindow, $"04_step{i + 1}");
                // Re-find button (may have changed visibility)
                nextBtn = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("NextStepButton"));
                if (nextBtn == null || !nextBtn.IsEnabled) break;
                nextBtn.AsButton().Invoke();
                Thread.Sleep(500);
                stepsVisited++;
            }
            catch
            {
                break;
            }
        }
    }
    TakeScreenshot(mainWindow, "04_final_step");
    Record("步骤导航(下一步)", stepsVisited >= 4, $"成功导航{stepsVisited}步");
}

// ==================== 测试5: 上一步 ====================
Console.WriteLine("\n=== 测试5: 上一步 ===");
{
    var prevBtn = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("PrevStepButton"));
    Record("找到上一步按钮", prevBtn != null);

    if (prevBtn != null)
    {
        prevBtn.AsButton().Invoke();
        Thread.Sleep(500);
        TakeScreenshot(mainWindow, "05_step_back");
        Record("点击上一步", true);
    }
}

// ==================== 测试6: 主题切换 ====================
Console.WriteLine("\n=== 测试6: 主题切换 ===");
{
    var theme = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("ThemeSelector"));
    if (theme != null)
    {
        var combo = theme.AsComboBox();
        TakeScreenshot(mainWindow, "06_theme_before");

        if (combo.Items.Length > 1)
        {
            combo.Select(1); // 苹果
            Thread.Sleep(500);
            TakeScreenshot(mainWindow, "06_theme_apple");
            Record("主题切换到苹果", true);

            combo.Select(2); // 紫光
            Thread.Sleep(500);
            TakeScreenshot(mainWindow, "06_theme_purple");
            Record("主题切换到紫光", true);

            combo.Select(0); // 经典
            Thread.Sleep(500);
        }
        else
        {
            Record("主题切换", false, "主题选项不足");
        }
    }
    else
    {
        Record("主题切换", false, "未找到ThemeSelector");
    }
}

// ==================== 测试7: 列出所有控件 ====================
Console.WriteLine("\n=== 测试7: 控件发现 ===");
{
    var allButtons = mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
    var allTextBoxes = mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit));
    var allComboBoxes = mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.ComboBox));

    Record("控件发现", true,
        $"按钮={allButtons.Length}, 输入框={allTextBoxes.Length}, 下拉框={allComboBoxes.Length}");

    // 列出有 AutomationId 的控件
    foreach (var btn in allButtons.Where(b => !string.IsNullOrEmpty(b.AutomationId)))
        Console.WriteLine($"    Button: {btn.AutomationId} = \"{btn.Name}\"");
    foreach (var tb in allTextBoxes.Where(b => !string.IsNullOrEmpty(b.AutomationId)))
        Console.WriteLine($"    TextBox: {tb.AutomationId} = \"{tb.AsTextBox().Text}\"");
}

// ==================== 测试8: 窗口操作 ====================
Console.WriteLine("\n=== 测试8: 窗口操作 ===");
{
    var patterns = mainWindow.Patterns;
    try
    {
        // 最大化
        if (patterns.Window.IsSupported)
        {
            patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);
            Thread.Sleep(500);
            TakeScreenshot(mainWindow, "08_maximized");
            Record("窗口最大化", true);

            // 还原
            patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Normal);
            Thread.Sleep(500);
            Record("窗口还原", true);
        }
        else
        {
            Record("窗口操作", false, "Window pattern 不支持");
        }
    }
    catch (Exception ex)
    {
        Record("窗口操作", false, ex.Message);
    }
}

// ==================== 生成报告 ====================
Console.WriteLine($"\n============================================================");
Console.WriteLine($"测试完成: 通过 {passed} / 失败 {failed} / 总计 {passed + failed}");
Console.WriteLine($"报告: {reportFile}");
Console.WriteLine($"截图: {screenshotDir}");

using (var sw = new StreamWriter(reportFile, false, System.Text.Encoding.UTF8))
{
    sw.WriteLine("SMHS Desktop FlaUI 自动化测试报告");
    sw.WriteLine($"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    sw.WriteLine($"通过: {passed} / 失败: {failed} / 总计: {passed + failed}\n");
    foreach (var (name, ok, detail) in results)
    {
        sw.Write($"[{(ok ? "PASS" : "FAIL")}] {name}");
        if (detail != "") sw.Write($" — {detail}");
        sw.WriteLine();
    }
}

// 清理
if (launchProject != null && app != null)
{
    Console.WriteLine("\n关闭 Desktop 应用...");
    app.Close();
}

return failed > 0 ? 1 : 0;
