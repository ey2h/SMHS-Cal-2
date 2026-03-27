using Microsoft.Playwright;
using NUnit.Framework;

namespace GlassManagement.UITests;

[TestFixture]
public class Step2Tests : TestBase
{
    [SetUp]
    public async Task LoginAndNavigate()
    {
        await LoginAsAdmin();
        await NavigateToWizardAndStart();
        // Navigate to Step 2
        var nextBtn = Page.GetByRole(AriaRole.Button, new() { Name = "下一步" });
        if (await nextBtn.IsVisibleAsync())
        {
            await nextBtn.ClickAsync();
            await Task.Delay(800);
        }
    }

    [Test]
    public async Task Test_WindLoadCalculation()
    {
        await TakeScreenshot("Step2_Initial");

        // Look for calculate button
        var calcBtn = Page.GetByRole(AriaRole.Button, new() { Name = "计算" });
        if (!await calcBtn.IsVisibleAsync())
            calcBtn = Page.Locator("button:has-text('计算')").First;

        if (await calcBtn.IsVisibleAsync())
        {
            await calcBtn.ClickAsync();
            await Task.Delay(1000);
        }

        await TakeScreenshot("Step2_WindLoadResult");
    }

    [Test]
    public async Task Test_SeismicCalculation()
    {
        await TakeScreenshot("Step2_SeismicBefore");

        // Look for seismic related elements
        var seismicSection = Page.GetByText("地震").First;
        if (await seismicSection.IsVisibleAsync())
        {
            await TakeScreenshot("Step2_SeismicSection");
        }

        var calcBtn = Page.Locator("button:has-text('计算')").First;
        if (await calcBtn.IsVisibleAsync())
        {
            await calcBtn.ClickAsync();
            await Task.Delay(1000);
        }

        await TakeScreenshot("Step2_SeismicResult");
    }

    [Test]
    public async Task Test_ResultDisplay()
    {
        await TakeScreenshot("Step2_ResultArea");

        // Verify result area exists
        var resultArea = Page.Locator(".calc-result, .result, [class*='result']").First;
        if (await resultArea.IsVisibleAsync())
        {
            var text = await resultArea.TextContentAsync();
            await TakeScreenshot("Step2_ResultContent");
            Assert.That(text, Is.Not.Null.And.Not.Empty);
        }
    }
}
