using Microsoft.Playwright;
using NUnit.Framework;

namespace GlassManagement.UITests;

[TestFixture]
public class Step5to7Tests : TestBase
{
    [SetUp]
    public async Task LoginAndNavigate()
    {
        await LoginAsAdmin();
        await NavigateToWizardAndStart();
    }

    private async Task GoToStep(int step)
    {
        var stepItems = Page.Locator(".wizard-step-item");
        await stepItems.Nth(step - 1).ClickAsync();
        await Task.Delay(800);
    }

    [Test]
    public async Task Test_ConnectionTypeSelect()
    {
        await GoToStep(5);
        await TakeScreenshot("Step5_Initial");

        var types = new[] { "螺栓", "焊接", "半焊半栓" };
        foreach (var t in types)
        {
            var option = Page.GetByText(t).First;
            if (await option.IsVisibleAsync())
            {
                await option.ClickAsync();
                await Task.Delay(500);
                await TakeScreenshot($"Step5_Connection_{t}");
            }
        }
    }

    [Test]
    public async Task Test_EmbedTypeSelect()
    {
        await GoToStep(6);
        await TakeScreenshot("Step6_Initial");

        // Look for embed type radio/select
        var radios = Page.Locator("input[type='radio']");
        var count = await radios.CountAsync();

        for (int i = 0; i < Math.Min(count, 4); i++)
        {
            await radios.Nth(i).ClickAsync();
            await Task.Delay(500);
            await TakeScreenshot($"Step6_EmbedType_{i + 1}");
        }
    }

    [Test]
    public async Task Test_SealantConfig()
    {
        await GoToStep(7);
        await TakeScreenshot("Step7_Initial");

        // Fill sealant parameters
        var inputs = Page.Locator("input[type='number']");
        var count = await inputs.CountAsync();

        for (int i = 0; i < count; i++)
        {
            var input = inputs.Nth(i);
            if (await input.IsVisibleAsync() && await input.IsEditableAsync())
            {
                await input.FillAsync("10");
            }
        }
        await TakeScreenshot("Step7_SealantFilled");
    }
}
