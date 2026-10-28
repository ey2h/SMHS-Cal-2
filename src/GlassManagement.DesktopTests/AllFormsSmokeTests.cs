using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using GlassManagement.Desktop;
using GlassManagement.Desktop.Views.Calc;
using NUnit.Framework;

[assembly: AvaloniaTestApplication(typeof(GlassManagement.DesktopTests.TestAppBuilder))]

namespace GlassManagement.DesktopTests;

/// <summary>Headless AppBuilder（Avalonia.Headless 测试宿主）。</summary>
public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
}

/// <summary>
/// 全窗体 UI 冒烟：CalcFormCatalog.All（34 个已复刻窗体）逐个 XAML 加载 + 布局，
/// 任何异常/零尺寸即判定该页 UI 异常。对应「每页 UI 是否正常」。
/// </summary>
[TestFixture]
public class AllFormsSmokeTests
{
    [AvaloniaTest]
    public void 全部窗体_XAML加载与布局_无异常()
    {
        var failed = new List<string>();
        int ok = 0;
        foreach (var e in CalcFormCatalog.All)
        {
            try
            {
                var v = e.Create();
                Assert.That(v, Is.Not.Null, e.Title);
                v.Measure(new Size(e.Width, e.Height));
                v.Arrange(new Rect(0, 0, e.Width, e.Height));
                if (v.Bounds.Width <= 0 || v.Bounds.Height <= 0)
                    failed.Add($"{e.Title}: 零尺寸 {v.Bounds}");
                else
                    ok++;
            }
            catch (Exception ex)
            {
                failed.Add($"{e.Title}: {ex.GetType().Name}: {ex.Message}");
            }
        }
        TestContext.Out.WriteLine($"窗体加载成功 {ok}/{CalcFormCatalog.All.Count}");
        Assert.That(failed, Is.Empty, "UI 加载失败：\n" + string.Join("\n", failed));
    }
}
