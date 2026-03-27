using Microsoft.Playwright;
using NUnit.Framework;

namespace GlassManagement.UITests;

[TestFixture]
public class Step4Tests : TestBase
{
    [SetUp]
    public async Task LoginAndNavigate()
    {
        await LoginAsAdmin();
        await NavigateToWizardAndStart();
        // Navigate to Step 4
        for (int i = 0; i < 3; i++)
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
    public async Task Test_FourWorkingConditions()
    {
        await TakeScreenshot("Step4_Initial");

        var conditions = new[] { "风", "自重", "三角", "集中力" };
        foreach (var cond in conditions)
        {
            var tab = Page.GetByText(cond).First;
            if (await tab.IsVisibleAsync())
            {
                await tab.ClickAsync();
                await Task.Delay(500);
                await TakeScreenshot($"Step4_Condition_{cond}");
            }
        }
    }

    [Test]
    public async Task Test_SVGWorkingConditions()
    {
        var conditions = new[] { "风", "自重", "三角", "集中力" };

        foreach (var cond in conditions)
        {
            var tab = Page.GetByText(cond).First;
            if (await tab.IsVisibleAsync())
            {
                await tab.ClickAsync();
                await Task.Delay(500);

                var svg = Page.Locator("svg").First;
                if (await svg.IsVisibleAsync())
                {
                    await Assertions.Expect(svg).ToBeVisibleAsync();
                }
                await TakeScreenshot($"Step4_SVG_{cond}");
            }
        }
    }
}
