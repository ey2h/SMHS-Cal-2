using Microsoft.Playwright;
using NUnit.Framework;

namespace GlassManagement.UITests;

[TestFixture]
public class SectionToolTests : TestBase
{
    [SetUp]
    public async Task LoginFirst()
    {
        await LoginAsAdmin();
    }

    [Test]
    public async Task Test_SectionToolPage_Open()
    {
        await Page.GotoAsync($"{BaseUrl}/section-tool");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await TakeScreenshot("SectionTool_Open");

        // Page should load without errors
        var content = Page.Locator("body");
        var text = await content.TextContentAsync();
        Assert.That(text, Is.Not.Null);
    }

    [Test]
    public async Task Test_SectionCalculate_Rectangle()
    {
        await Page.GotoAsync($"{BaseUrl}/section-tool");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Task.Delay(500);

        // Look for rectangle/矩形 option
        var rectOption = Page.GetByText("矩形").First;
        if (await rectOption.IsVisibleAsync())
        {
            await rectOption.ClickAsync();
            await Task.Delay(300);
        }

        // Fill dimensions
        var inputs = Page.Locator("input[type='number']");
        var count = await inputs.CountAsync();
        for (int i = 0; i < count; i++)
        {
            var input = inputs.Nth(i);
            if (await input.IsVisibleAsync() && await input.IsEditableAsync())
            {
                await input.FillAsync("100");
            }
        }

        // Calculate
        var calcBtn = Page.Locator("button:has-text('计算')").First;
        if (await calcBtn.IsVisibleAsync())
        {
            await calcBtn.ClickAsync();
            await Task.Delay(1000);
        }

        await TakeScreenshot("SectionTool_Rectangle");
    }

    [Test]
    public async Task Test_SectionCalculate_BreakHeat()
    {
        await Page.GotoAsync($"{BaseUrl}/section-tool");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Task.Delay(500);

        // Look for break-heat/断热 option
        var heatOption = Page.GetByText("断热").First;
        if (await heatOption.IsVisibleAsync())
        {
            await heatOption.ClickAsync();
            await Task.Delay(500);
        }

        await TakeScreenshot("SectionTool_BreakHeat");

        var calcBtn = Page.Locator("button:has-text('计算')").First;
        if (await calcBtn.IsVisibleAsync())
        {
            await calcBtn.ClickAsync();
            await Task.Delay(1000);
        }

        await TakeScreenshot("SectionTool_BreakHeatResult");
    }

    [Test]
    public async Task Test_SectionImport()
    {
        await Page.GotoAsync($"{BaseUrl}/section-tool");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Task.Delay(500);

        var importBtn = Page.Locator("button:has-text('导入'), button:has-text('保存到数据库')").First;
        if (await importBtn.IsVisibleAsync())
        {
            await importBtn.ClickAsync();
            await Task.Delay(500);
            await TakeScreenshot("SectionTool_Import");
        }
        else
        {
            await TakeScreenshot("SectionTool_NoImportBtn");
        }
    }
}
