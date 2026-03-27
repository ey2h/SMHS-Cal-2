using Microsoft.Playwright;
using NUnit.Framework;

namespace GlassManagement.UITests;

[TestFixture]
public class ThemeTests : TestBase
{
    [SetUp]
    public async Task LoginFirst()
    {
        await LoginAsAdmin();
    }

    [Test]
    public async Task Test_ThemeSwitch_Classic()
    {
        await SwitchTheme("classic");
        await TakeScreenshot("Theme_Classic");
    }

    [Test]
    public async Task Test_ThemeSwitch_Apple()
    {
        await SwitchTheme("apple");
        await TakeScreenshot("Theme_Apple");
    }

    [Test]
    public async Task Test_ThemeSwitch_Xkool()
    {
        await SwitchTheme("xkool");
        await TakeScreenshot("Theme_Xkool");
    }

    [Test]
    public async Task Test_DarkMode()
    {
        // Look for dark mode toggle
        var darkToggle = Page.Locator("button:has-text('暗色'), button:has-text('深色'), [class*='dark-toggle'], [class*='theme-toggle']").First;
        if (!await darkToggle.IsVisibleAsync())
            darkToggle = Page.Locator(".material-symbols-outlined:has-text('dark_mode')").First;

        if (await darkToggle.IsVisibleAsync())
        {
            await darkToggle.ClickAsync();
            await Task.Delay(500);
            await TakeScreenshot("Theme_DarkMode");

            // Toggle back
            await darkToggle.ClickAsync();
            await Task.Delay(500);
            await TakeScreenshot("Theme_LightMode");
        }
        else
        {
            // Try JS approach
            await Page.EvaluateAsync("document.documentElement.setAttribute('data-theme', 'dark')");
            await Task.Delay(500);
            await TakeScreenshot("Theme_DarkMode_JS");
        }
    }

    private async Task SwitchTheme(string theme)
    {
        // Try clicking theme selector
        var themeBtn = Page.GetByText(theme, new() { Exact = false }).First;
        if (await themeBtn.IsVisibleAsync())
        {
            await themeBtn.ClickAsync();
            await Task.Delay(500);
        }
        else
        {
            // Try via JS or URL parameter
            await Page.EvaluateAsync($"document.documentElement.setAttribute('data-theme', '{theme}')");
            await Task.Delay(500);
        }
    }
}
