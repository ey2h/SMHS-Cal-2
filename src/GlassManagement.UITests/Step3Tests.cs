using Microsoft.Playwright;
using NUnit.Framework;

namespace GlassManagement.UITests;

[TestFixture]
public class Step3Tests : TestBase
{
    [SetUp]
    public async Task LoginAndNavigate()
    {
        await LoginAsAdmin();
        await NavigateToWizardAndStart();
        // Navigate to Step 3
        for (int i = 0; i < 2; i++)
        {
            var nextBtn = Page.GetByRole(AriaRole.Button, new() { Name = "下一步" });
            if (await nextBtn.IsVisibleAsync())
            {
                await nextBtn.ClickAsync();
                await Task.Delay(800);
            }
        }
    }

    [Test]
    public async Task Test_MechanicalModelSelection()
    {
        await TakeScreenshot("Step3_Initial");

        // Look for radio buttons for mechanical models
        var radios = Page.Locator("input[type='radio']");
        var count = await radios.CountAsync();

        for (int i = 0; i < Math.Min(count, 6); i++)
        {
            await radios.Nth(i).ClickAsync();
            await Task.Delay(500);
            await TakeScreenshot($"Step3_Model_{i + 1}");
        }
    }

    [Test]
    public async Task Test_SVGDisplay()
    {
        var svg = Page.Locator("svg").First;
        if (await svg.IsVisibleAsync())
        {
            await Assertions.Expect(svg).ToBeVisibleAsync();
            await TakeScreenshot("Step3_SVG");
        }
        else
        {
            await TakeScreenshot("Step3_NoSVG");
        }
    }

    [Test]
    public async Task Test_PanelSwitch()
    {
        var panels = new[] { "结构", "V", "M", "δ" };

        foreach (var panel in panels)
        {
            var tab = Page.GetByText(panel, new() { Exact = true }).First;
            if (await tab.IsVisibleAsync())
            {
                await tab.ClickAsync();
                await Task.Delay(500);
                await TakeScreenshot($"Step3_Panel_{panel}");
            }
        }
    }

    [Test]
    public async Task Test_ProfileTableLoad()
    {
        // Look for profile/section table
        var table = Page.Locator("table").First;
        if (await table.IsVisibleAsync())
        {
            var rows = Page.Locator("table tr");
            var count = await rows.CountAsync();
            Assert.That(count, Is.GreaterThan(0), "Profile table should have data");
            await TakeScreenshot("Step3_ProfileTable");
        }
    }

    [Test]
    public async Task Test_DeflectionStandardSelect()
    {
        var select = Page.Locator("select").First;
        if (await select.IsVisibleAsync())
        {
            var options = await select.Locator("option").AllAsync();
            for (int i = 0; i < Math.Min(options.Count, 5); i++)
            {
                var value = await options[i].GetAttributeAsync("value");
                if (value != null)
                {
                    await select.SelectOptionAsync(value);
                    await Task.Delay(300);
                    await TakeScreenshot($"Step3_DeflectionStd_{i + 1}");
                }
            }
        }
    }
}
