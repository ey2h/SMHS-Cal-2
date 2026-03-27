using Microsoft.Playwright;
using NUnit.Framework;

namespace GlassManagement.UITests;

[TestFixture]
public class NavigationTests : TestBase
{
    [SetUp]
    public async Task LoginAndNavigate()
    {
        await LoginAsAdmin();
        await NavigateToWizardAndStart();
    }

    [Test]
    public async Task Test_StepNavigation_Forward()
    {
        for (int step = 1; step <= 8; step++)
        {
            await TakeScreenshot($"Nav_Step{step}");
            var nextBtn = Page.GetByRole(AriaRole.Button, new() { Name = "下一步" });
            if (await nextBtn.IsVisibleAsync())
            {
                await nextBtn.ClickAsync();
                await Task.Delay(500);
            }
        }
        await TakeScreenshot("Nav_Step9_Final");
    }

    [Test]
    public async Task Test_StepNavigation_Backward()
    {
        // Go forward a few steps first
        for (int i = 0; i < 3; i++)
        {
            var nextBtn = Page.GetByRole(AriaRole.Button, new() { Name = "下一步" });
            if (await nextBtn.IsVisibleAsync())
            {
                await nextBtn.ClickAsync();
                await Task.Delay(500);
            }
        }

        // Now go back
        for (int i = 0; i < 3; i++)
        {
            var prevBtn = Page.GetByRole(AriaRole.Button, new() { Name = "上一步" });
            if (await prevBtn.IsVisibleAsync())
            {
                await prevBtn.ClickAsync();
                await Task.Delay(500);
                await TakeScreenshot($"Nav_Back_Step{3 - i}");
            }
        }
    }

    [Test]
    public async Task Test_StepNavigation_DirectClick()
    {
        // Click step items directly in the sidebar
        var stepItems = Page.Locator(".wizard-step-item");
        var count = await stepItems.CountAsync();

        for (int i = 0; i < Math.Min(count, 9); i++)
        {
            await stepItems.Nth(i).ClickAsync();
            await Task.Delay(500);
            await TakeScreenshot($"Nav_Direct_Step{i + 1}");
        }
    }

    [Test]
    public async Task Test_AllStepsAccessible()
    {
        var stepItems = Page.Locator(".wizard-step-item");
        var count = await stepItems.CountAsync();
        Assert.That(count, Is.EqualTo(9), "Should have 9 step items");

        for (int i = 0; i < count; i++)
        {
            await stepItems.Nth(i).ClickAsync();
            await Task.Delay(800);

            // Verify step content area is visible
            var content = Page.Locator(".wizard-content .card");
            await Assertions.Expect(content).ToBeVisibleAsync();
            await TakeScreenshot($"Nav_AllSteps_Step{i + 1}");
        }
    }
}
